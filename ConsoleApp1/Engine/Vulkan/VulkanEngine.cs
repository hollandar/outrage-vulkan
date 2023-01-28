using ConsoleApp1.Engine.Models;
using ConsoleApp1.Engine.Textures;
using ConsoleApp1.Engine.Windowing;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace ConsoleApp1.Engine.Vulkan
{
    public unsafe class VulkanEngine: IDisposable
    {

        const string TEXTURE_PATH = @"Assets\viking_room.png";
        const int MAX_FRAMES_IN_FLIGHT = 2;
        const string MODEL_PATH = @"Assets\viking_room.obj";
        private VulkanModelsBuffer modelsBuffer;
        private VulkanTextures vulkanTextures;

        bool framebufferResized = false;
        private IAppWindow appWindow;
        private IVkSurface windowSurface;
        private Vk api;
        private Instance instance;
        readonly bool enableValidationLayers = true;
        private readonly string[] validationLayers = new[]
        {
            "VK_LAYER_KHRONOS_validation"
        };
        private readonly string[] deviceExtensions = new[]
        {
            KhrSwapchain.ExtensionName
        };
        private ExtDebugUtils? debugUtils;
        private DebugUtilsMessengerEXT debugMessenger;
        private SurfaceKHR surface;
        private KhrSurface khrSurface;
        private PhysicalDevice physicalDevice;
        private SampleCountFlags msaaSamples = SampleCountFlags.Count1Bit;
        private Silk.NET.Vulkan.Device device;
        private Queue graphicsQueue;
        private Queue presentQueue;
        private KhrSwapchain? khrSwapChain;
        private SwapchainKHR swapChain;
        private Image[] swapChainImages;
        private Format swapChainImageFormat;
        private Extent2D swapChainExtent;
        private ImageView[] swapChainImageViews;
        private RenderPass renderPass;
        private DescriptorSetLayout descriptorSetLayout;
        private PipelineLayout pipelineLayout;
        private Pipeline graphicsPipeline;
        private CommandPool commandPool;
        private Image colorImage;
        private DeviceMemory colorImageMemory;
        private ImageView colorImageView;
        private Image depthImage;
        private DeviceMemory depthImageMemory;
        private ImageView depthImageView;
        private Framebuffer[]? swapChainFramebuffers;
        private Buffer[]? uniformBuffers;
        private DeviceMemory[]? uniformBuffersMemory;
        private DescriptorPool descriptorPool;
        private DescriptorSet[]? descriptorSets;
        private VulkanCommandBuffers? commandBuffers;
        private Semaphore[]? imageAvailableSemaphores;
        private Semaphore[]? renderFinishedSemaphores;
        private Fence[]? inFlightFences;
        private Fence[]? imagesInFlight;
        private int currentFrame = 0;

        public VulkanEngine(IAppWindow appWindow, bool enableValidationLayers = true)
        {
            this.appWindow = appWindow;
            this.windowSurface = appWindow.GetVulkanSurface();
            this.enableValidationLayers = enableValidationLayers;
            appWindow.Resize += FramebufferResized;


            InitVulkan();            
        }

        public Vk Api { get => this.api; }
        public CommandPool CommandPool { get => this.commandPool; }
        public Device Device { get => this.device; }
        public PhysicalDevice PhysicalDevice { get => this.physicalDevice; }
        public RenderPass RenderPass { get => this.renderPass; }
        public Framebuffer[] Framebuffers { get => this.swapChainFramebuffers; }
        public Extent2D SwapChainExtent { get => this.swapChainExtent; }
        public Pipeline Pipeline { get => this.graphicsPipeline; }
        public PipelineLayout PipelineLayout { get => this.pipelineLayout; }
        public DescriptorSet[] DescriptorSets { get => this.descriptorSets; }
        private void FramebufferResized(Silk.NET.Maths.Vector2D<int> obj)
        {
            framebufferResized = true;
        }

        private void InitVulkan()
        {

            CreateInstance();
            SetupDebugMessenger();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateDescriptorSetLayout();
            CreateGraphicsPipeline();
            CreateCommandPool();
            CreateColorResources();
            CreateDepthResources();
            CreateFramebuffers();

            var texture = new Texture(TEXTURE_PATH);
            var textureCollection = new TexturesCollection();
            textureCollection.AddTexture(texture);
            vulkanTextures = new VulkanTextures(this);
            vulkanTextures.BindTextures(textureCollection);

            CreateUniformBuffers();
            CreateDescriptorPool();
            CreateDescriptorSets();

            var model = new Model(MODEL_PATH);
            var modelsCollection = new ModelsCollection();
            modelsCollection.AddModel(model);
            this.modelsBuffer = new VulkanModelsBuffer(this);
            this.modelsBuffer.BindModels(modelsCollection);

            this.commandBuffers = new VulkanCommandBuffers(this, modelsBuffer, swapChainFramebuffers!.Length);
            CreateSyncObjects();
            appWindow!.Render += DrawFrame;
        }

        private void CreateInstance()
        {
            api = Vk.GetApi();

            if (enableValidationLayers && !CheckValidationLayerSupport())
            {
                throw new Exception("validation layers requested, but not available!");
            }

            ApplicationInfo appInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo
            };

            var extensions = GetRequiredExtensions();
            createInfo.EnabledExtensionCount = (uint)extensions.Length;
            createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions); ;

            if (enableValidationLayers)
            {
                createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);

                DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
                PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
                createInfo.PNext = &debugCreateInfo;
            }
            else
            {
                createInfo.EnabledLayerCount = 0;
                createInfo.PNext = null;
            }

            if (api.CreateInstance(createInfo, null, out instance) != Result.Success)
            {
                throw new Exception("failed to create instance!");
            }

            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

            if (enableValidationLayers)
            {
                SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
            }
        }

        private bool CheckValidationLayerSupport()
        {
            uint layerCount = 0;
            api!.EnumerateInstanceLayerProperties(ref layerCount, null);
            var availableLayers = new LayerProperties[layerCount];
            fixed (LayerProperties* availableLayersPtr = availableLayers)
            {
                api!.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
            }

            var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

            return validationLayers.All(availableLayerNames.Contains);
        }

        private string[] GetRequiredExtensions()
        {
            var glfwExtensions = windowSurface.GetRequiredExtensions(out var glfwExtensionCount);
            var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

            if (enableValidationLayers)
            {
                return extensions.Append(ExtDebugUtils.ExtensionName).ToArray();
            }

            return extensions;
        }

        private void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
        {
            createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
            createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                         DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                         DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
            createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                     DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                     DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
            createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
        }

        private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
        {
            System.Diagnostics.Debug.WriteLine($"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

            return Vk.False;
        }

        private void SetupDebugMessenger()
        {
            if (!enableValidationLayers) return;

            //TryGetInstanceExtension equivilant to method CreateDebugUtilsMessengerEXT from original tutorial.
            if (!api!.TryGetInstanceExtension(instance, out debugUtils)) return;

            DebugUtilsMessengerCreateInfoEXT createInfo = new();
            PopulateDebugMessengerCreateInfo(ref createInfo);

            if (debugUtils!.CreateDebugUtilsMessenger(instance, in createInfo, null, out debugMessenger) != Result.Success)
            {
                throw new Exception("failed to set up debug messenger!");
            }
        }

        private void CreateSurface()
        {
            if (!api!.TryGetInstanceExtension<KhrSurface>(instance, out khrSurface))
            {
                throw new NotSupportedException("KHR_surface extension not found.");
            }

            surface = windowSurface.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
        }

        private void PickPhysicalDevice()
        {
            uint devicedCount = 0;
            api!.EnumeratePhysicalDevices(instance, ref devicedCount, null);

            if (devicedCount == 0)
            {
                throw new Exception("failed to find GPUs with Vulkan support!");
            }

            var devices = new PhysicalDevice[devicedCount];
            fixed (PhysicalDevice* devicesPtr = devices)
            {
                api!.EnumeratePhysicalDevices(instance, ref devicedCount, devicesPtr);
            }

            foreach (var device in devices)
            {
                if (IsDeviceSuitable(device))
                {
                    physicalDevice = device;
                    msaaSamples = GetMaxUsableSampleCount();
                    break;
                }
            }

            if (physicalDevice.Handle == 0)
            {
                throw new Exception("failed to find a suitable GPU!");
            }
        }

        private bool IsDeviceSuitable(PhysicalDevice device)
        {
            var indices = FindQueueFamilies(device);

            bool extensionsSupported = CheckDeviceExtensionsSupport(device);

            bool swapChainAdequate = false;
            if (extensionsSupported)
            {
                var swapChainSupport = QuerySwapChainSupport(device);
                swapChainAdequate = swapChainSupport.Formats.Any() && swapChainSupport.PresentModes.Any();
            }

            PhysicalDeviceFeatures supportedFeatures;
            api!.GetPhysicalDeviceFeatures(device, out supportedFeatures);

            return indices.IsComplete() && extensionsSupported && swapChainAdequate && supportedFeatures.SamplerAnisotropy;
        }

        private SampleCountFlags GetMaxUsableSampleCount()
        {
            api!.GetPhysicalDeviceProperties(physicalDevice, out var physicalDeviceProperties);

            var counts = physicalDeviceProperties.Limits.FramebufferColorSampleCounts & physicalDeviceProperties.Limits.FramebufferDepthSampleCounts;

            return counts switch
            {
                var c when (c & SampleCountFlags.Count64Bit) != 0 => SampleCountFlags.Count64Bit,
                var c when (c & SampleCountFlags.Count32Bit) != 0 => SampleCountFlags.Count32Bit,
                var c when (c & SampleCountFlags.Count16Bit) != 0 => SampleCountFlags.Count16Bit,
                var c when (c & SampleCountFlags.Count8Bit) != 0 => SampleCountFlags.Count8Bit,
                var c when (c & SampleCountFlags.Count4Bit) != 0 => SampleCountFlags.Count4Bit,
                var c when (c & SampleCountFlags.Count2Bit) != 0 => SampleCountFlags.Count2Bit,
                _ => SampleCountFlags.Count1Bit
            };
        }

        private QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            var indices = new QueueFamilyIndices();

            uint queueFamilityCount = 0;
            api!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, null);

            var queueFamilies = new QueueFamilyProperties[queueFamilityCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                api!.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, queueFamiliesPtr);
            }


            uint i = 0;
            foreach (var queueFamily in queueFamilies)
            {
                if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                {
                    indices.GraphicsFamily = i;
                }

                khrSurface!.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var presentSupport);

                if (presentSupport)
                {
                    indices.PresentFamily = i;
                }

                if (indices.IsComplete())
                {
                    break;
                }

                i++;
            }

            return indices;
        }

        private bool CheckDeviceExtensionsSupport(PhysicalDevice device)
        {
            uint extentionsCount = 0;
            api!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, null);

            var availableExtensions = new ExtensionProperties[extentionsCount];
            fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
            {
                api!.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extentionsCount, availableExtensionsPtr);
            }

            var availableExtensionNames = availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName)).ToHashSet();

            return deviceExtensions.All(availableExtensionNames.Contains);

        }

        private SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice physicalDevice)
        {
            var details = new SwapChainSupportDetails();

            khrSurface!.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, out details.Capabilities);

            uint formatCount = 0;
            khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, null);

            if (formatCount != 0)
            {
                details.Formats = new SurfaceFormatKHR[formatCount];
                fixed (SurfaceFormatKHR* formatsPtr = details.Formats)
                {
                    khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, ref formatCount, formatsPtr);
                }
            }
            else
            {
                details.Formats = Array.Empty<SurfaceFormatKHR>();
            }

            uint presentModeCount = 0;
            khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, null);

            if (presentModeCount != 0)
            {
                details.PresentModes = new PresentModeKHR[presentModeCount];
                fixed (PresentModeKHR* formatsPtr = details.PresentModes)
                {
                    khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, ref presentModeCount, formatsPtr);
                }

            }
            else
            {
                details.PresentModes = Array.Empty<PresentModeKHR>();
            }

            return details;
        }

        private void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(physicalDevice);

            var uniqueQueueFamilies = new[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
            uniqueQueueFamilies = uniqueQueueFamilies.Distinct().ToArray();

            using var mem = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
            var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());

            float queuePriority = 1.0f;
            for (int i = 0; i < uniqueQueueFamilies.Length; i++)
            {
                queueCreateInfos[i] = new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = uniqueQueueFamilies[i],
                    QueueCount = 1,
                    PQueuePriorities = &queuePriority
                };
            }

            PhysicalDeviceFeatures deviceFeatures = new()
            {
                SamplerAnisotropy = true,
            };


            DeviceCreateInfo createInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
                PQueueCreateInfos = queueCreateInfos,

                PEnabledFeatures = &deviceFeatures,

                EnabledExtensionCount = (uint)deviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions)
            };

            if (enableValidationLayers)
            {
                createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
            }
            else
            {
                createInfo.EnabledLayerCount = 0;
            }

            if (api!.CreateDevice(physicalDevice, in createInfo, null, out device) != Result.Success)
            {
                throw new Exception("failed to create logical device!");
            }

            api!.GetDeviceQueue(device, indices.GraphicsFamily!.Value, 0, out graphicsQueue);
            api!.GetDeviceQueue(device, indices.PresentFamily!.Value, 0, out presentQueue);

            if (enableValidationLayers)
            {
                SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
            }

            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        }

        private void CreateSwapChain()
        {
            var swapChainSupport = QuerySwapChainSupport(physicalDevice);

            var surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
            var presentMode = ChoosePresentMode(swapChainSupport.PresentModes);
            var extent = ChooseSwapExtent(swapChainSupport.Capabilities);

            var imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
            if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
            {
                imageCount = swapChainSupport.Capabilities.MaxImageCount;
            }

            SwapchainCreateInfoKHR creatInfo = new()
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = surface,

                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            };

            var indices = FindQueueFamilies(physicalDevice);
            var queueFamilyIndices = stackalloc[] { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };

            if (indices.GraphicsFamily != indices.PresentFamily)
            {
                creatInfo = creatInfo with
                {
                    ImageSharingMode = SharingMode.Concurrent,
                    QueueFamilyIndexCount = 2,
                    PQueueFamilyIndices = queueFamilyIndices,
                };
            }
            else
            {
                creatInfo.ImageSharingMode = SharingMode.Exclusive;
            }

            creatInfo = creatInfo with
            {
                PreTransform = swapChainSupport.Capabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                PresentMode = presentMode,
                Clipped = true,
            };

            if (khrSwapChain is null)
            {
                if (!api!.TryGetDeviceExtension(instance, device, out khrSwapChain))
                {
                    throw new NotSupportedException("VK_KHR_swapchain extension not found.");
                }
            }

            if (khrSwapChain!.CreateSwapchain(device, creatInfo, null, out swapChain) != Result.Success)
            {
                throw new Exception("failed to create swap chain!");
            }

            khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, null);
            swapChainImages = new Image[imageCount];
            fixed (Image* swapChainImagesPtr = swapChainImages)
            {
                khrSwapChain.GetSwapchainImages(device, swapChain, ref imageCount, swapChainImagesPtr);
            }

            swapChainImageFormat = surfaceFormat.Format;
            swapChainExtent = extent;
        }

        private SurfaceFormatKHR ChooseSwapSurfaceFormat(IReadOnlyList<SurfaceFormatKHR> availableFormats)
        {
            foreach (var availableFormat in availableFormats)
            {
                if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    return availableFormat;
                }
            }

            return availableFormats[0];
        }

        private PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
        {
            foreach (var availablePresentMode in availablePresentModes)
            {
                if (availablePresentMode == PresentModeKHR.MailboxKhr)
                {
                    return availablePresentMode;
                }
            }

            return PresentModeKHR.FifoKhr;
        }

        private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }
            else
            {
                var framebufferSize = appWindow.FramebufferSize;

                Extent2D actualExtent = new()
                {
                    Width = (uint)framebufferSize.X,
                    Height = (uint)framebufferSize.Y
                };

                actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
                actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

                return actualExtent;
            }
        }

        private void CreateImageViews()
        {
            swapChainImageViews = new ImageView[swapChainImages!.Length];

            for (int i = 0; i < swapChainImages.Length; i++)
            {

                swapChainImageViews[i] = CreateImageView(swapChainImages[i], swapChainImageFormat, ImageAspectFlags.ColorBit, 1);
            }
        }

        public ImageView CreateImageView(Image image, Format format, ImageAspectFlags aspectFlags, uint mipLevels)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = format,
                //Components =
                //    {
                //        R = ComponentSwizzle.Identity,
                //        G = ComponentSwizzle.Identity,
                //        B = ComponentSwizzle.Identity,
                //        A = ComponentSwizzle.Identity,
                //    },
                SubresourceRange =
                {
                    AspectMask = aspectFlags,
                    BaseMipLevel = 0,
                    LevelCount = mipLevels,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                }

            };

            ImageView imageView = default;

            if (api!.CreateImageView(device, createInfo, null, out imageView) != Result.Success)
            {
                throw new Exception("failed to create image views!");
            }

            return imageView;
        }

        private void CreateRenderPass()
        {
            AttachmentDescription colorAttachment = new()
            {
                Format = swapChainImageFormat,
                Samples = msaaSamples,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.ColorAttachmentOptimal,
            };

            AttachmentDescription depthAttachment = new()
            {
                Format = FindDepthFormat(),
                Samples = msaaSamples,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
            };

            AttachmentDescription colorAttachmentResolve = new()
            {
                Format = swapChainImageFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.DontCare,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr,
            };

            AttachmentReference colorAttachmentRef = new()
            {
                Attachment = 0,
                Layout = ImageLayout.ColorAttachmentOptimal,
            };

            AttachmentReference depthAttachmentRef = new()
            {
                Attachment = 1,
                Layout = ImageLayout.DepthStencilAttachmentOptimal,
            };

            AttachmentReference colorAttachmentResolveRef = new()
            {
                Attachment = 2,
                Layout = ImageLayout.ColorAttachmentOptimal,
            };

            SubpassDescription subpass = new()
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef,
                PDepthStencilAttachment = &depthAttachmentRef,
                PResolveAttachments = &colorAttachmentResolveRef,
            };

            SubpassDependency dependency = new()
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
            };

            var attachments = new[] { colorAttachment, depthAttachment, colorAttachmentResolve };

            fixed (AttachmentDescription* attachmentsPtr = attachments)
            {
                RenderPassCreateInfo renderPassInfo = new()
                {
                    SType = StructureType.RenderPassCreateInfo,
                    AttachmentCount = (uint)attachments.Length,
                    PAttachments = attachmentsPtr,
                    SubpassCount = 1,
                    PSubpasses = &subpass,
                    DependencyCount = 1,
                    PDependencies = &dependency,
                };

                if (api!.CreateRenderPass(device, renderPassInfo, null, out renderPass) != Result.Success)
                {
                    throw new Exception("failed to create render pass!");
                }
            }
        }

        private Format FindDepthFormat()
        {
            return FindSupportedFormat(new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint }, ImageTiling.Optimal, FormatFeatureFlags.DepthStencilAttachmentBit);
        }

        private Format FindSupportedFormat(IEnumerable<Format> candidates, ImageTiling tiling, FormatFeatureFlags features)
        {
            foreach (var format in candidates)
            {
                api!.GetPhysicalDeviceFormatProperties(physicalDevice, format, out var props);

                if (tiling == ImageTiling.Linear && (props.LinearTilingFeatures & features) == features)
                {
                    return format;
                }
                else if (tiling == ImageTiling.Optimal && (props.OptimalTilingFeatures & features) == features)
                {
                    return format;
                }
            }

            throw new Exception("failed to find supported format!");
        }

        private void CreateDescriptorSetLayout()
        {

            DescriptorSetLayoutBinding uboLayoutBinding = new()
            {
                Binding = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.VertexBit,
            };

            DescriptorSetLayoutBinding localNodeMatrixBinding = new()
            {
                Binding = 1,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBufferDynamic,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.VertexBit,
            };

            DescriptorSetLayoutBinding samplerLayoutBinding = new()
            {
                Binding = 2,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImmutableSamplers = null,
                StageFlags = ShaderStageFlags.FragmentBit,
            };


            var bindings = new DescriptorSetLayoutBinding[] { uboLayoutBinding, samplerLayoutBinding, localNodeMatrixBinding };

            fixed (DescriptorSetLayoutBinding* bindingsPtr = bindings)
            fixed (DescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
            {
                DescriptorSetLayoutCreateInfo layoutInfo = new()
                {
                    SType = StructureType.DescriptorSetLayoutCreateInfo,
                    BindingCount = (uint)bindings.Length,
                    PBindings = bindingsPtr,
                };

                if (api!.CreateDescriptorSetLayout(device, layoutInfo, null, descriptorSetLayoutPtr) != Result.Success)
                {
                    throw new Exception("failed to create descriptor set layout!");
                }
            }
        }

        private void CreateGraphicsPipeline()
        {
            var vertShaderCode = System.IO.File.ReadAllBytes("shaders/default/shader.vert.spv");
            var fragShaderCode = System.IO.File.ReadAllBytes("shaders/default/shader.frag.spv");

            var vertShaderModule = CreateShaderModule(vertShaderCode);
            var fragShaderModule = CreateShaderModule(fragShaderCode);

            PipelineShaderStageCreateInfo vertShaderStageInfo = new()
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertShaderModule,
                PName = (byte*)SilkMarshal.StringToPtr("main")
            };

            PipelineShaderStageCreateInfo fragShaderStageInfo = new()
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragShaderModule,
                PName = (byte*)SilkMarshal.StringToPtr("main")
            };

            var shaderStages = stackalloc[]
            {
                vertShaderStageInfo,
                fragShaderStageInfo
            };

            var bindingDescription = Vertex.GetBindingDescription();
            var attributeDescriptions = Vertex.GetAttributeDescriptions();

            fixed (VertexInputAttributeDescription* attributeDescriptionsPtr = attributeDescriptions)
            fixed (DescriptorSetLayout* descriptorSetLayoutPtr = &descriptorSetLayout)
            {

                PipelineVertexInputStateCreateInfo vertexInputInfo = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 1,
                    VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                    PVertexBindingDescriptions = &bindingDescription,
                    PVertexAttributeDescriptions = attributeDescriptionsPtr,
                };

                PipelineInputAssemblyStateCreateInfo inputAssembly = new()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList,
                    PrimitiveRestartEnable = false,
                };

                Viewport viewport = new()
                {
                    X = 0,
                    Y = 0,
                    Width = swapChainExtent.Width,
                    Height = swapChainExtent.Height,
                    MinDepth = 0,
                    MaxDepth = 1,
                };

                Rect2D scissor = new()
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = swapChainExtent,
                };

                PipelineViewportStateCreateInfo viewportState = new()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    PViewports = &viewport,
                    ScissorCount = 1,
                    PScissors = &scissor,
                };

                PipelineRasterizationStateCreateInfo rasterizer = new()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = false,
                    RasterizerDiscardEnable = false,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1,
                    CullMode = CullModeFlags.BackBit,
                    FrontFace = FrontFace.CounterClockwise,
                    DepthBiasEnable = false,
                };

                PipelineMultisampleStateCreateInfo multisampling = new()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = false,
                    RasterizationSamples = msaaSamples,
                };

                PipelineDepthStencilStateCreateInfo depthStencil = new()
                {
                    SType = StructureType.PipelineDepthStencilStateCreateInfo,
                    DepthTestEnable = true,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.Less,
                    DepthBoundsTestEnable = false,
                    StencilTestEnable = false,
                };

                PipelineColorBlendAttachmentState colorBlendAttachment = new()
                {
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                    BlendEnable = false,
                };

                PipelineColorBlendStateCreateInfo colorBlending = new()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = false,
                    LogicOp = LogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachment,
                };

                colorBlending.BlendConstants[0] = 0;
                colorBlending.BlendConstants[1] = 0;
                colorBlending.BlendConstants[2] = 0;
                colorBlending.BlendConstants[3] = 0;

                PipelineLayoutCreateInfo pipelineLayoutInfo = new()
                {
                    SType = StructureType.PipelineLayoutCreateInfo,
                    PushConstantRangeCount = 0,
                    SetLayoutCount = 1,
                    PSetLayouts = descriptorSetLayoutPtr
                };

                if (api!.CreatePipelineLayout(device, pipelineLayoutInfo, null, out pipelineLayout) != Result.Success)
                {
                    throw new Exception("failed to create pipeline layout!");
                }

                GraphicsPipelineCreateInfo pipelineInfo = new()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = 2,
                    PStages = shaderStages,
                    PVertexInputState = &vertexInputInfo,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizer,
                    PMultisampleState = &multisampling,
                    PDepthStencilState = &depthStencil,
                    PColorBlendState = &colorBlending,
                    Layout = pipelineLayout,
                    RenderPass = renderPass,
                    Subpass = 0,
                    BasePipelineHandle = default
                };

                if (api!.CreateGraphicsPipelines(device, default, 1, pipelineInfo, null, out graphicsPipeline) != Result.Success)
                {
                    throw new Exception("failed to create graphics pipeline!");
                }
            }

            api!.DestroyShaderModule(device, fragShaderModule, null);
            api!.DestroyShaderModule(device, vertShaderModule, null);

            SilkMarshal.Free((nint)vertShaderStageInfo.PName);
            SilkMarshal.Free((nint)fragShaderStageInfo.PName);
        }

        private ShaderModule CreateShaderModule(byte[] code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
            };

            ShaderModule shaderModule;

            fixed (byte* codePtr = code)
            {
                createInfo.PCode = (uint*)codePtr;

                if (api!.CreateShaderModule(device, createInfo, null, out shaderModule) != Result.Success)
                {
                    throw new Exception();
                }
            }

            return shaderModule;

        }

        private void CreateCommandPool()
        {
            var queueFamiliyIndicies = FindQueueFamilies(physicalDevice);

            CommandPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = queueFamiliyIndicies.GraphicsFamily!.Value,
            };

            if (api!.CreateCommandPool(device, poolInfo, null, out commandPool) != Result.Success)
            {
                throw new Exception("failed to create command pool!");
            }
        }

        private void CreateColorResources()
        {
            Format colorFormat = swapChainImageFormat;

            CreateImage(swapChainExtent.Width, swapChainExtent.Height, 1, msaaSamples, colorFormat, ImageTiling.Optimal, ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, ref colorImage, ref colorImageMemory);
            colorImageView = CreateImageView(colorImage, colorFormat, ImageAspectFlags.ColorBit, 1);
        }

        public void CreateImage(uint width, uint height, uint mipLevels, SampleCountFlags numSamples, Format format, ImageTiling tiling, ImageUsageFlags usage, MemoryPropertyFlags properties, ref Image image, ref DeviceMemory imageMemory)
        {
            ImageCreateInfo imageInfo = new()
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.Type2D,
                Extent =
            {
                Width = width,
                Height = height,
                Depth = 1,
            },
                MipLevels = mipLevels,
                ArrayLayers = 1,
                Format = format,
                Tiling = tiling,
                InitialLayout = ImageLayout.Undefined,
                Usage = usage,
                Samples = numSamples,
                SharingMode = SharingMode.Exclusive,
            };

            fixed (Image* imagePtr = &image)
            {
                if (api!.CreateImage(device, imageInfo, null, imagePtr) != Result.Success)
                {
                    throw new Exception("failed to create image!");
                }
            }

            MemoryRequirements memRequirements;
            api!.GetImageMemoryRequirements(device, image, out memRequirements);

            MemoryAllocateInfo allocInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
            };

            fixed (DeviceMemory* imageMemoryPtr = &imageMemory)
            {
                if (api!.AllocateMemory(device, allocInfo, null, imageMemoryPtr) != Result.Success)
                {
                    throw new Exception("failed to allocate image memory!");
                }
            }

            api!.BindImageMemory(device, image, imageMemory, 0);
        }

        private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
        {
            PhysicalDeviceMemoryProperties memProperties;
            api!.GetPhysicalDeviceMemoryProperties(physicalDevice, out memProperties);

            for (int i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
                {
                    return (uint)i;
                }
            }

            throw new Exception("failed to find suitable memory type!");
        }

        private void CreateDepthResources()
        {
            Format depthFormat = FindDepthFormat();

            CreateImage(swapChainExtent.Width, swapChainExtent.Height, 1, msaaSamples, depthFormat, ImageTiling.Optimal, ImageUsageFlags.DepthStencilAttachmentBit, MemoryPropertyFlags.DeviceLocalBit, ref depthImage, ref depthImageMemory);
            depthImageView = CreateImageView(depthImage, depthFormat, ImageAspectFlags.DepthBit, 1);
        }

        private void CreateFramebuffers()
        {
            swapChainFramebuffers = new Framebuffer[swapChainImageViews!.Length];

            for (int i = 0; i < swapChainImageViews.Length; i++)
            {
                var attachments = new[] { colorImageView, depthImageView, swapChainImageViews[i] };

                fixed (ImageView* attachmentsPtr = attachments)
                {
                    FramebufferCreateInfo framebufferInfo = new()
                    {
                        SType = StructureType.FramebufferCreateInfo,
                        RenderPass = renderPass,
                        AttachmentCount = (uint)attachments.Length,
                        PAttachments = attachmentsPtr,
                        Width = swapChainExtent.Width,
                        Height = swapChainExtent.Height,
                        Layers = 1,
                    };

                    if (api!.CreateFramebuffer(device, framebufferInfo, null, out swapChainFramebuffers[i]) != Result.Success)
                    {
                        throw new Exception("failed to create framebuffer!");
                    }
                }
            }
        }

        public void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties, ref Buffer buffer, ref DeviceMemory bufferMemory)
        {
            BufferCreateInfo bufferInfo = new()
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
            };

            fixed (Buffer* bufferPtr = &buffer)
            {
                if (api!.CreateBuffer(device, bufferInfo, null, bufferPtr) != Result.Success)
                {
                    throw new Exception("failed to create vertex buffer!");
                }
            }

            MemoryRequirements memRequirements = new();
            api!.GetBufferMemoryRequirements(device, buffer, out memRequirements);

            MemoryAllocateInfo allocateInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties),
            };

            fixed (DeviceMemory* bufferMemoryPtr = &bufferMemory)
            {
                if (api!.AllocateMemory(device, allocateInfo, null, bufferMemoryPtr) != Result.Success)
                {
                    throw new Exception("failed to allocate vertex buffer memory!");
                }
            }

            api!.BindBufferMemory(device, buffer, bufferMemory, 0);
        }

        public void TransitionImageLayout(Image image, Format format, ImageLayout oldLayout, ImageLayout newLayout, uint mipLevels)
        {
            CommandBuffer commandBuffer = BeginSingleTimeCommands();

            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = mipLevels,
                BaseArrayLayer = 0,
                LayerCount = 1,
            }
            };

            PipelineStageFlags sourceStage;
            PipelineStageFlags destinationStage;

            if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = AccessFlags.TransferWriteBit;

                sourceStage = PipelineStageFlags.TopOfPipeBit;
                destinationStage = PipelineStageFlags.TransferBit;
            }
            else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
                barrier.DstAccessMask = AccessFlags.ShaderReadBit;

                sourceStage = PipelineStageFlags.TransferBit;
                destinationStage = PipelineStageFlags.FragmentShaderBit;
            }
            else
            {
                throw new Exception("unsupported layout transition!");
            }

            api!.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, barrier);

            EndSingleTimeCommands(commandBuffer);

        }

        public void CopyBufferToImage(Buffer buffer, Image image, uint width, uint height)
        {
            CommandBuffer commandBuffer = BeginSingleTimeCommands();

            BufferImageCopy region = new()
            {
                BufferOffset = 0,
                BufferRowLength = 0,
                BufferImageHeight = 0,
                ImageSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
                ImageOffset = new Offset3D(0, 0, 0),
                ImageExtent = new Extent3D(width, height, 1),

            };

            api!.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, region);

            EndSingleTimeCommands(commandBuffer);
        }

        public void GenerateMipMaps(Image image, Format imageFormat, uint width, uint height, uint mipLevels)
        {
            api!.GetPhysicalDeviceFormatProperties(physicalDevice, imageFormat, out var formatProperties);

            if ((formatProperties.OptimalTilingFeatures & FormatFeatureFlags.SampledImageFilterLinearBit) == 0)
            {
                throw new Exception("texture image format does not support linear blitting!");
            }

            var commandBuffer = BeginSingleTimeCommands();

            ImageMemoryBarrier barrier = new()
            {
                SType = StructureType.ImageMemoryBarrier,
                Image = image,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseArrayLayer = 0,
                LayerCount = 1,
                LevelCount = 1,
            }
            };

            var mipWidth = width;
            var mipHeight = height;

            for (uint i = 1; i < mipLevels; i++)
            {
                barrier.SubresourceRange.BaseMipLevel = i - 1;
                barrier.OldLayout = ImageLayout.TransferDstOptimal;
                barrier.NewLayout = ImageLayout.TransferSrcOptimal;
                barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
                barrier.DstAccessMask = AccessFlags.TransferReadBit;

                api!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.TransferBit, 0,
                    0, null,
                    0, null,
                    1, barrier);

                ImageBlit blit = new()
                {
                    SrcOffsets =
                {
                    Element0 = new Offset3D(0,0,0),
                    Element1 = new Offset3D((int)mipWidth, (int)mipHeight, 1),
                },
                    SrcSubresource =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = i - 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
                    DstOffsets =
                {
                    Element0 = new Offset3D(0,0,0),
                    Element1 = new Offset3D((int)(mipWidth > 1 ? mipWidth / 2 : 1), (int)(mipHeight > 1 ? mipHeight / 2 : 1),1),
                },
                    DstSubresource =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    MipLevel = i,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },

                };

                api!.CmdBlitImage(commandBuffer,
                    image, ImageLayout.TransferSrcOptimal,
                    image, ImageLayout.TransferDstOptimal,
                    1, blit,
                    Filter.Linear);

                barrier.OldLayout = ImageLayout.TransferSrcOptimal;
                barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
                barrier.SrcAccessMask = AccessFlags.TransferReadBit;
                barrier.DstAccessMask = AccessFlags.ShaderReadBit;

                api!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0,
                    0, null,
                    0, null,
                    1, barrier);

                if (mipWidth > 1) mipWidth /= 2;
                if (mipHeight > 1) mipHeight /= 2;
            }

            barrier.SubresourceRange.BaseMipLevel = mipLevels - 1;
            barrier.OldLayout = ImageLayout.TransferDstOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            api!.CmdPipelineBarrier(commandBuffer, PipelineStageFlags.TransferBit, PipelineStageFlags.FragmentShaderBit, 0,
                0, null,
                0, null,
                1, barrier);

            EndSingleTimeCommands(commandBuffer);
        }

        private CommandBuffer BeginSingleTimeCommands()
        {
            CommandBufferAllocateInfo allocateInfo = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                Level = CommandBufferLevel.Primary,
                CommandPool = commandPool,
                CommandBufferCount = 1,
            };

            CommandBuffer commandBuffer = default;
            api!.AllocateCommandBuffers(device, allocateInfo, out commandBuffer);

            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            api!.BeginCommandBuffer(commandBuffer, beginInfo);

            return commandBuffer;
        }

        private void EndSingleTimeCommands(CommandBuffer commandBuffer)
        {
            api!.EndCommandBuffer(commandBuffer);

            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
            };

            api!.QueueSubmit(graphicsQueue, 1, submitInfo, default);
            api!.QueueWaitIdle(graphicsQueue);

            api!.FreeCommandBuffers(device, commandPool, 1, commandBuffer);
        }

        public void CopyBuffer(Buffer srcBuffer, Buffer dstBuffer, ulong size)
        {
            CommandBuffer commandBuffer = BeginSingleTimeCommands();

            BufferCopy copyRegion = new()
            {
                Size = size,
            };

            api!.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, copyRegion);

            EndSingleTimeCommands(commandBuffer);
        }

        private void CreateUniformBuffers()
        {
            ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();

            uniformBuffers = new Buffer[swapChainImages!.Length];
            uniformBuffersMemory = new DeviceMemory[swapChainImages!.Length];

            for (int i = 0; i < swapChainImages.Length; i++)
            {
                CreateBuffer(bufferSize, BufferUsageFlags.UniformBufferBit, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref uniformBuffers[i], ref uniformBuffersMemory[i]);
            }

        }

        private void CreateDescriptorPool()
        {
            var poolSizes = new DescriptorPoolSize[]
            {
            new DescriptorPoolSize()
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = (uint)swapChainImages!.Length,
            },
            new DescriptorPoolSize()
            {
                Type = DescriptorType.UniformBufferDynamic,
                DescriptorCount = (uint)swapChainImages!.Length,
            },
            new DescriptorPoolSize()
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = (uint)swapChainImages!.Length,
            }
            };

            fixed (DescriptorPoolSize* poolSizesPtr = poolSizes)
            fixed (DescriptorPool* descriptorPoolPtr = &descriptorPool)
            {

                DescriptorPoolCreateInfo poolInfo = new()
                {
                    SType = StructureType.DescriptorPoolCreateInfo,
                    PoolSizeCount = (uint)poolSizes.Length,
                    PPoolSizes = poolSizesPtr,
                    MaxSets = (uint)swapChainImages!.Length,
                };

                if (api!.CreateDescriptorPool(device, poolInfo, null, descriptorPoolPtr) != Result.Success)
                {
                    throw new Exception("failed to create descriptor pool!");
                }

            }
        }

        private void CreateDescriptorSets()
        {
            var layouts = new DescriptorSetLayout[swapChainImages!.Length];
            Array.Fill(layouts, descriptorSetLayout);

            fixed (DescriptorSetLayout* layoutsPtr = layouts)
            {
                DescriptorSetAllocateInfo allocateInfo = new()
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = descriptorPool,
                    DescriptorSetCount = (uint)swapChainImages!.Length,
                    PSetLayouts = layoutsPtr,
                };

                descriptorSets = new DescriptorSet[swapChainImages.Length];
                fixed (DescriptorSet* descriptorSetsPtr = descriptorSets)
                {
                    if (api!.AllocateDescriptorSets(device, allocateInfo, descriptorSetsPtr) != Result.Success)
                    {
                        throw new Exception("failed to allocate descriptor sets!");
                    }
                }
            }


            var texture = vulkanTextures.GetTexture(0);
            for (int i = 0; i < swapChainImages.Length; i++)
            {
                DescriptorBufferInfo bufferInfo = new()
                {
                    Buffer = uniformBuffers![i],
                    Offset = 0,
                    Range = (ulong)Unsafe.SizeOf<UniformBufferObject>(),

                };

                DescriptorImageInfo imageInfo = new()
                {
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                    ImageView = texture.ImageView,
                    Sampler = texture.Sampler,
                };

                var descriptorWrites = new WriteDescriptorSet[]
                {
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets[i],
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.UniformBuffer,
                    DescriptorCount = 1,
                    PBufferInfo = &bufferInfo,
                },
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets[i],
                    DstBinding = 1,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.UniformBufferDynamic,
                    DescriptorCount = 1,
                    PBufferInfo = &bufferInfo,
                },
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = descriptorSets[i],
                    DstBinding = 2,
                    DstArrayElement = 0,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    DescriptorCount = 1,
                    PImageInfo = &imageInfo,
                }
                };

                fixed (WriteDescriptorSet* descriptorWritesPtr = descriptorWrites)
                {
                    api!.UpdateDescriptorSets(device, (uint)descriptorWrites.Length, descriptorWritesPtr, 0, null);
                }
            }

        }


        private void CreateSyncObjects()
        {
            imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
            renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
            inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
            imagesInFlight = new Fence[swapChainImages!.Length];

            SemaphoreCreateInfo semaphoreInfo = new()
            {
                SType = StructureType.SemaphoreCreateInfo,
            };

            FenceCreateInfo fenceInfo = new()
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit,
            };

            for (var i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                if (api!.CreateSemaphore(device, semaphoreInfo, null, out imageAvailableSemaphores[i]) != Result.Success ||
                    api!.CreateSemaphore(device, semaphoreInfo, null, out renderFinishedSemaphores[i]) != Result.Success ||
                    api!.CreateFence(device, fenceInfo, null, out inFlightFences[i]) != Result.Success)
                {
                    throw new Exception("failed to create synchronization objects for a frame!");
                }
            }
        }

        private void DrawFrame(double delta)
        {
            api!.WaitForFences(device, 1, inFlightFences![currentFrame], true, ulong.MaxValue);

            uint imageIndex = 0;
            var result = khrSwapChain!.AcquireNextImage(device, swapChain, ulong.MaxValue, imageAvailableSemaphores![currentFrame], default, ref imageIndex);

            if (result == Result.ErrorOutOfDateKhr)
            {
                RecreateSwapChain();
                return;
            }
            else if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                throw new Exception("failed to acquire swap chain image!");
            }

            UpdateUniformBuffer(imageIndex);

            if (imagesInFlight![imageIndex].Handle != default)
            {
                api!.WaitForFences(device, 1, imagesInFlight[imageIndex], true, ulong.MaxValue);
            }
            imagesInFlight[imageIndex] = inFlightFences[currentFrame];

            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
            };

            var waitSemaphores = stackalloc[] { imageAvailableSemaphores[currentFrame] };
            var waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };

            var buffer = commandBuffers!.FrameCommandBuffers[imageIndex];

            submitInfo = submitInfo with
            {
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,

                CommandBufferCount = 1,
                PCommandBuffers = &buffer
            };

            var signalSemaphores = stackalloc[] { renderFinishedSemaphores![currentFrame] };
            submitInfo = submitInfo with
            {
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores,
            };

            api!.ResetFences(device, 1, inFlightFences[currentFrame]);

            if (api!.QueueSubmit(graphicsQueue, 1, submitInfo, inFlightFences[currentFrame]) != Result.Success)
            {
                throw new Exception("failed to submit draw command buffer!");
            }

            var swapChains = stackalloc[] { swapChain };
            PresentInfoKHR presentInfo = new()
            {
                SType = StructureType.PresentInfoKhr,

                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,

                SwapchainCount = 1,
                PSwapchains = swapChains,

                PImageIndices = &imageIndex
            };

            result = khrSwapChain.QueuePresent(presentQueue, presentInfo);

            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || framebufferResized)
            {
                framebufferResized = false;
                RecreateSwapChain();
            }
            else if (result != Result.Success)
            {
                throw new Exception("failed to present swap chain image!");
            }

            currentFrame = (currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;

        }

        private void RecreateSwapChain()
        {
            Vector2D<int> framebufferSize = appWindow.FramebufferSize;

            while (framebufferSize.X == 0 || framebufferSize.Y == 0)
            {
                framebufferSize = appWindow.FramebufferSize;
                appWindow.DoEvents();
            }

            api!.DeviceWaitIdle(device);

            CleanUpSwapChain();

            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateColorResources();
            CreateDepthResources();
            CreateFramebuffers();
            CreateUniformBuffers();
            CreateDescriptorPool();
            CreateDescriptorSets();
            this.commandBuffers = new VulkanCommandBuffers(this, this.modelsBuffer, swapChainFramebuffers!.Length);

            imagesInFlight = new Fence[swapChainImages!.Length];
        }

        private void CleanUpSwapChain()
        {
            api!.DestroyImageView(device, depthImageView, null);
            api!.DestroyImage(device, depthImage, null);
            api!.FreeMemory(device, depthImageMemory, null);

            api!.DestroyImageView(device, colorImageView, null);
            api!.DestroyImage(device, colorImage, null);
            api!.FreeMemory(device, colorImageMemory, null);

            foreach (var framebuffer in swapChainFramebuffers!)
            {
                api!.DestroyFramebuffer(device, framebuffer, null);
            }

            this.commandBuffers.Dispose();
            this.commandBuffers = null;

            api!.DestroyPipeline(device, graphicsPipeline, null);
            api!.DestroyPipelineLayout(device, pipelineLayout, null);
            api!.DestroyRenderPass(device, renderPass, null);

            foreach (var imageView in swapChainImageViews!)
            {
                api!.DestroyImageView(device, imageView, null);
            }

            khrSwapChain!.DestroySwapchain(device, swapChain, null);

            for (int i = 0; i < swapChainImages!.Length; i++)
            {
                api!.DestroyBuffer(device, uniformBuffers![i], null);
                api!.FreeMemory(device, uniformBuffersMemory![i], null);
            }

            api!.DestroyDescriptorPool(device, descriptorPool, null);
        }

        private void UpdateUniformBuffer(uint currentImage)
        {
            //Silk Window has timing information so we are skipping the time code.
            var time = (float)appWindow!.Time;

            UniformBufferObject ubo = new()
            {
                model = Matrix4X4<float>.Identity * Matrix4X4.CreateFromAxisAngle<float>(new Vector3D<float>(0, 0, 1), time * Scalar.DegreesToRadians(90.0f)),
                view = Matrix4X4.CreateLookAt(new Vector3D<float>(2, 2, 2), new Vector3D<float>(0, 0, 0), new Vector3D<float>(0, 0, 1)),
                proj = Matrix4X4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(45.0f), swapChainExtent.Width / swapChainExtent.Height, 0.1f, 10.0f),
            };
            ubo.proj.M22 *= -1;


            void* data;
            api!.MapMemory(device, uniformBuffersMemory![currentImage], 0, (ulong)Unsafe.SizeOf<UniformBufferObject>(), 0, &data);
            new Span<UniformBufferObject>(data, 1)[0] = ubo;
            api!.UnmapMemory(device, uniformBuffersMemory![currentImage]);

        }

        private void CleanUp()
        {
            api!.DeviceWaitIdle(device);

            CleanUpSwapChain();

            vulkanTextures.Dispose();

            api!.DestroyDescriptorSetLayout(device, descriptorSetLayout, null);

            this.modelsBuffer.Dispose();

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                api!.DestroySemaphore(device, renderFinishedSemaphores![i], null);
                api!.DestroySemaphore(device, imageAvailableSemaphores![i], null);
                api!.DestroyFence(device, inFlightFences![i], null);
            }

            api!.DestroyCommandPool(device, commandPool, null);

            api!.DestroyDevice(device, null);

            if (enableValidationLayers)
            {
                //DestroyDebugUtilsMessenger equivilant to method DestroyDebugUtilsMessengerEXT from original tutorial.
                debugUtils!.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
            }

            khrSurface!.DestroySurface(instance, surface, null);
            api!.DestroyInstance(instance, null);
            api!.Dispose();

        }

        public void Dispose()
        {
            CleanUp();
        }
    }
}
