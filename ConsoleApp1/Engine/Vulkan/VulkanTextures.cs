using ConsoleApp1.Engine.Textures;
using Silk.NET.OpenAL;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace ConsoleApp1.Engine.Vulkan
{
    public class VulkanTexture
    {
        public uint MipLevels { get; internal set; }
        public Image Image { get; set; }
        public DeviceMemory ImageMemory { get; set; }
        public Sampler Sampler { get; internal set; }
        public ImageView ImageView { get; internal set; }
    }

    public class VulkanTextures : IDisposable
    {
        private readonly VulkanEngine vulkanEngine;
        private IDictionary<uint, VulkanTexture> vulkanTextures = new Dictionary<uint, VulkanTexture>();

        public VulkanTextures(VulkanEngine vulkanEngine)
        {
            this.vulkanEngine = vulkanEngine;
        }

        public VulkanTexture GetTexture(uint id)
        {
            if (vulkanTextures.TryGetValue(id, out var texture))
                return texture;

            throw new ArgumentException($"Texture with id {id} not found.");
        }

        public unsafe void BindTextures(TexturesCollection texturesCollection)
        {
            Cleanup();
            vulkanTextures.Clear();

            foreach (var texture in texturesCollection.Textures)
            {
                var img = texture.Value;
                var vulkanTexture = new VulkanTexture();
                ulong imageSize = (ulong)(img.Width * img.Height * img.BitsPerPixel / 8);
                vulkanTexture.MipLevels = (uint)(Math.Floor(Math.Log2(Math.Max(img.Width, img.Height))) + 1);

                Buffer stagingBuffer = default;
                DeviceMemory stagingBufferMemory = default;
                vulkanEngine.CreateBuffer(imageSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref stagingBuffer, ref stagingBufferMemory);

                void* data;
                vulkanEngine.Api.MapMemory(vulkanEngine.Device, stagingBufferMemory, 0, imageSize, 0, &data);
                img.CopyTo(new Span<byte>(data, (int)imageSize));
                vulkanEngine.Api.UnmapMemory(vulkanEngine.Device, stagingBufferMemory);
                Image textureImage = new Image();
                DeviceMemory textureImageMemory = new DeviceMemory();

                vulkanEngine.CreateImage((uint)img.Width, (uint)img.Height, vulkanTexture.MipLevels, SampleCountFlags.Count1Bit, Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.TransferSrcBit | ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, MemoryPropertyFlags.DeviceLocalBit, ref textureImage, ref textureImageMemory);

                vulkanEngine.TransitionImageLayout(textureImage, Format.R8G8B8A8Srgb, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, vulkanTexture.MipLevels);
                vulkanEngine.CopyBufferToImage(stagingBuffer, textureImage, (uint)img.Width, (uint)img.Height);
                //Transitioned to VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL while generating mipmaps

                vulkanEngine.Api.DestroyBuffer(vulkanEngine.Device, stagingBuffer, null);
                vulkanEngine.Api.FreeMemory(vulkanEngine.Device, stagingBufferMemory, null);

                vulkanEngine.GenerateMipMaps(textureImage, Format.R8G8B8A8Srgb, (uint)img.Width, (uint)img.Height, vulkanTexture.MipLevels);

                vulkanTexture.Image = textureImage;
                vulkanTexture.ImageMemory = textureImageMemory;

                vulkanTexture.ImageView = vulkanEngine.CreateImageView(vulkanTexture.Image, Format.R8G8B8A8Srgb, ImageAspectFlags.ColorBit, vulkanTexture.MipLevels);

                PhysicalDeviceProperties properties;
                vulkanEngine.Api.GetPhysicalDeviceProperties(vulkanEngine.PhysicalDevice, out properties);

                SamplerCreateInfo samplerInfo = new()
                {
                    SType = StructureType.SamplerCreateInfo,
                    MagFilter = Filter.Linear,
                    MinFilter = Filter.Linear,
                    AddressModeU = SamplerAddressMode.Repeat,
                    AddressModeV = SamplerAddressMode.Repeat,
                    AddressModeW = SamplerAddressMode.Repeat,
                    AnisotropyEnable = true,
                    MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
                    BorderColor = BorderColor.IntOpaqueBlack,
                    UnnormalizedCoordinates = false,
                    CompareEnable = false,
                    CompareOp = CompareOp.Always,
                    MipmapMode = SamplerMipmapMode.Linear,
                    MinLod = 0,
                    MaxLod = vulkanTexture.MipLevels,
                    MipLodBias = 0,
                };

                Sampler textureSampler;
                if (vulkanEngine.Api.CreateSampler(vulkanEngine.Device, samplerInfo, null, &textureSampler) != Result.Success)
                {
                    throw new Exception("failed to create texture sampler!");
                }

                vulkanTexture.Sampler = textureSampler;

                vulkanTextures[texture.Key] = vulkanTexture;
            }
        }

        private unsafe void Cleanup()
        {
            foreach (var texture in vulkanTextures)
            {
                vulkanEngine.Api.DestroyImage(vulkanEngine.Device, texture.Value.Image, null);
                vulkanEngine.Api.FreeMemory(vulkanEngine.Device, texture.Value.ImageMemory, null);
                vulkanEngine.Api.DestroyImageView(vulkanEngine.Device, texture.Value.ImageView, null);
                vulkanEngine.Api.DestroySampler(vulkanEngine.Device, texture.Value.Sampler, null);
            }
        }
        public void Dispose()
        {
            Cleanup();
        }
    }
}
