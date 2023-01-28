using Silk.NET.OpenAL;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace ConsoleApp1.Engine.Vulkan
{
    internal class VulkanCommandBuffers: IDisposable
    {
        private VulkanEngine vulkanEngine { get; set; }

        private readonly VulkanModelsBuffer modelBuffers;
        private int frameCount;
        private CommandBuffer[]? commandBuffers;

        public VulkanCommandBuffers (VulkanEngine engine, VulkanModelsBuffer modelBuffers, int frameCount)
        {
            if (!modelBuffers.Bound)
            {
                throw new ArgumentException("VulkanModelsBuffer has no models bound to it.");
            }

            this.vulkanEngine = engine;
            this.modelBuffers = modelBuffers;
            this.frameCount = frameCount;
            CreateCommandBuffers();
        }



        public CommandBuffer[] FrameCommandBuffers { get => this.commandBuffers; }
        private unsafe void CreateCommandBuffers()
        {
            commandBuffers = new CommandBuffer[frameCount];

            CommandBufferAllocateInfo allocInfo = new()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = vulkanEngine.CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)commandBuffers.Length,
            };

            fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
            {
                if (vulkanEngine.Api.AllocateCommandBuffers(vulkanEngine.Device, allocInfo, commandBuffersPtr) != Result.Success)
                {
                    throw new Exception("failed to allocate command buffers!");
                }
            }


            for (int i = 0; i < commandBuffers.Length; i++)
            {
                CommandBufferBeginInfo beginInfo = new()
                {
                    SType = StructureType.CommandBufferBeginInfo,
                };

                if (vulkanEngine.Api.BeginCommandBuffer(commandBuffers[i], beginInfo) != Result.Success)
                {
                    throw new Exception("failed to begin recording command buffer!");
                }

                RenderPassBeginInfo renderPassInfo = new()
                {
                    SType = StructureType.RenderPassBeginInfo,
                    RenderPass = vulkanEngine.RenderPass,
                    Framebuffer = vulkanEngine.Framebuffers[i],
                    RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = vulkanEngine.SwapChainExtent,
                }
                };

                var clearValues = new ClearValue[]
                {
                new()
                {
                    Color = new (){ Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 },
                },
                new()
                {
                    DepthStencil = new () { Depth = 1, Stencil = 0 }
                }
                };


                fixed (ClearValue* clearValuesPtr = clearValues)
                {
                    renderPassInfo.ClearValueCount = (uint)clearValues.Length;
                    renderPassInfo.PClearValues = clearValuesPtr;

                    vulkanEngine.Api.CmdBeginRenderPass(commandBuffers[i], &renderPassInfo, SubpassContents.Inline);
                }

                vulkanEngine.Api.CmdBindPipeline(commandBuffers[i], PipelineBindPoint.Graphics, vulkanEngine.Pipeline);

                var vertexBuffers = new Buffer[] { modelBuffers.VertexBuffer };
                var modelOffset = modelBuffers.GetModelOffset(0);
                var offsets = new ulong[] { modelOffset.VertexOffset };
                var sizes = new ulong[] { modelOffset.VertexSize };
                var strides = new ulong[] { modelOffset.VertexCount };

                fixed (ulong* offsetsPtr = offsets)
                fixed (ulong* sizesPtr = sizes)
                fixed (ulong* stridesPtr = strides)
                fixed (Buffer* vertexBuffersPtr = vertexBuffers)
                {
                    vulkanEngine.Api.CmdBindVertexBuffers(commandBuffers[i], 0, 1, vertexBuffersPtr, offsetsPtr);
                }

                vulkanEngine.Api.CmdBindIndexBuffer(commandBuffers[i], modelBuffers.IndexBuffer, modelOffset.IndexOffset, IndexType.Uint32);

                vulkanEngine.Api.CmdBindDescriptorSets(commandBuffers[i], PipelineBindPoint.Graphics, vulkanEngine.PipelineLayout, 0, 1, vulkanEngine.DescriptorSets![i], 0, null);

                vulkanEngine.Api.CmdDrawIndexed(commandBuffers[i], (uint)modelOffset.IndexCount, 1, 0, 0, 0);

                vulkanEngine.Api.CmdEndRenderPass(commandBuffers[i]);

                if (vulkanEngine.Api.EndCommandBuffer(commandBuffers[i]) != Result.Success)
                {
                    throw new Exception("failed to record command buffer!");
                }

            }
        }

        private unsafe void Cleanup()
        {
            fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
            {
                this.vulkanEngine.Api.FreeCommandBuffers(vulkanEngine.Device, vulkanEngine.CommandPool, (uint)commandBuffers!.Length, commandBuffersPtr);
            }

        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
