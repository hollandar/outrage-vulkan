using ConsoleApp1.Engine.Models;
using Silk.NET.OpenAL;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace ConsoleApp1.Engine.Vulkan
{
    public class ModelOffset
    {
        public ulong VertexOffset;
        public ulong IndexOffsetBytes;
        internal ulong VertexSize;
        internal ulong IndexSize;
        internal ulong VertexCount;
        internal ulong IndexCount;
        internal ulong IndexOffset;
    }

    public class VulkanModelsBuffer : IDisposable
    {
        public IDictionary<uint, ModelOffset> modelOffsets;
        private VulkanEngine vulkanEngine;
        private bool bound = false;
        private Buffer vertexBuffer;
        private DeviceMemory vertexBufferMemory;
        private Buffer indexBuffer;
        private DeviceMemory indexBufferMemory;

        public VulkanModelsBuffer(VulkanEngine vulkanEngine)
        {
            this.vulkanEngine = vulkanEngine;
        }

        public bool Bound { get => this.bound; }

        public void BindModels(ModelsCollection modelsCollection) { 
            this.modelOffsets = modelsCollection.ModelIds.ToDictionary(r => r, r => new ModelOffset());
            Cleanup();
            CreateVertexBuffer(modelsCollection);
            CreateIndexBuffer(modelsCollection);
            bound = true;
        }

        public Buffer VertexBuffer { get => this.vertexBuffer; }
        public Buffer IndexBuffer { get => this.indexBuffer; }
        
        public ModelOffset? GetModelOffset(uint index)
        {
            if (modelOffsets.TryGetValue(index, out var modelOffset))
            {
                return modelOffset;
            }

            return null;
        }

        private unsafe void CreateVertexBuffer(ModelsCollection modelsCollection)
        {
            ulong bufferSize = (ulong)Unsafe.SizeOf<Vertex>() * modelsCollection.VertexCount;

            Buffer stagingBuffer = default;
            DeviceMemory stagingBufferMemory = default;
            vulkanEngine.CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref stagingBuffer, ref stagingBufferMemory);

            void* data;
            ulong vertices = 0;
            foreach (var modelId in modelsCollection.ModelIds)
            {
                var model = modelsCollection.GetModel(modelId);
                ulong vertexOffset = (ulong)Unsafe.SizeOf<Vertex>() * vertices;
                ulong vertexBufferSize = (ulong)(Unsafe.SizeOf<Vertex>() * model.VertexCount);
                
                vulkanEngine.Api.MapMemory(vulkanEngine.Device, stagingBufferMemory, vertexOffset, vertexBufferSize, 0, &data);
                model.Vertices.AsSpan().CopyTo(new Span<Vertex>(data, model.VertexCount));
                vulkanEngine.Api.UnmapMemory(vulkanEngine.Device, stagingBufferMemory);

                modelOffsets[modelId].VertexOffset = vertexOffset;
                modelOffsets[modelId].VertexSize = vertexBufferSize;
                modelOffsets[modelId].VertexCount = (ulong)model.VertexCount;
                vertices += (ulong)model.VertexCount;
            }

            vulkanEngine.CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit, MemoryPropertyFlags.DeviceLocalBit, ref vertexBuffer, ref vertexBufferMemory);

            vulkanEngine.CopyBuffer(stagingBuffer, vertexBuffer, bufferSize);

            vulkanEngine.Api.DestroyBuffer(vulkanEngine.Device, stagingBuffer, null);
            vulkanEngine.Api.FreeMemory(vulkanEngine.Device, stagingBufferMemory, null);
        }

        private unsafe void CreateIndexBuffer(ModelsCollection modelsCollection)
        {
            ulong bufferSize = (ulong)Unsafe.SizeOf<uint>() * modelsCollection.IndexCount;

            Buffer stagingBuffer = default;
            DeviceMemory stagingBufferMemory = default;
            vulkanEngine.CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref stagingBuffer, ref stagingBufferMemory);

            void* data;
            ulong indices = 0;
            foreach (var modelId in modelsCollection.ModelIds)
            {
                var model = modelsCollection.GetModel(modelId);
                ulong indexOffset = (ulong)Unsafe.SizeOf<uint>() * indices;
                ulong indexBufferSize = (ulong)(Unsafe.SizeOf<uint>() * model.IndexCount);

                vulkanEngine.Api.MapMemory(vulkanEngine.Device, stagingBufferMemory, indexOffset, indexBufferSize, 0, &data);
                model.Indices.AsSpan().CopyTo(new Span<uint>(data, model.IndexCount));
                vulkanEngine.Api.UnmapMemory(vulkanEngine.Device, stagingBufferMemory);
                
                modelOffsets[modelId].IndexOffsetBytes = indexOffset;
                modelOffsets[modelId].IndexOffset = indices;
                modelOffsets[modelId].IndexSize = indexBufferSize;
                modelOffsets[modelId].IndexCount = (ulong)model.IndexCount;
                indices += (ulong)model.IndexCount;
            }

            vulkanEngine.CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit, MemoryPropertyFlags.DeviceLocalBit, ref indexBuffer, ref indexBufferMemory);

            vulkanEngine.CopyBuffer(stagingBuffer, indexBuffer, bufferSize);

            vulkanEngine.Api.DestroyBuffer(vulkanEngine.Device, stagingBuffer, null);
            vulkanEngine.Api.FreeMemory(vulkanEngine.Device, stagingBufferMemory, null);
        }

        private unsafe void Cleanup()
        {
            if (bound)
            {
                vulkanEngine.Api.DestroyBuffer(vulkanEngine.Device, indexBuffer, null);
                vulkanEngine.Api.FreeMemory(vulkanEngine.Device, indexBufferMemory, null);

                vulkanEngine.Api.DestroyBuffer(vulkanEngine.Device, vertexBuffer, null);
                vulkanEngine.Api.FreeMemory(vulkanEngine.Device, vertexBufferMemory, null);
            }
        }

        public void Dispose()
        {
            Cleanup();
        }
    }

}