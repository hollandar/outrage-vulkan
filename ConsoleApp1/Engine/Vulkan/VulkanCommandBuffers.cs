using Silk.NET.OpenAL;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace ConsoleApp1.Engine.Vulkan
{
    internal class VulkanCommandBuffers : IDisposable
    {
        private VulkanEngine vulkanEngine { get; set; }

        private readonly VulkanModelsBuffer modelBuffers;
        private int frameCount;
        private CommandBuffer[]? commandBuffers;

        public VulkanCommandBuffers(VulkanEngine engine, VulkanModelsBuffer modelBuffers, int frameCount)
        {
            if (!modelBuffers.Bound)
            {
                throw new ArgumentException("VulkanModelsBuffer has no models bound to it.");
            }

            this.vulkanEngine = engine;
            this.modelBuffers = modelBuffers;
            this.frameCount = frameCount;
            AllocateCommandBuffers();

            for (int i = 0; i < commandBuffers.Length; i++)
                CreateCommandBuffers(i);
        }



        private unsafe void AllocateCommandBuffers()
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

        }
        public CommandBuffer[] FrameCommandBuffers { get => this.commandBuffers; }
        private unsafe void CreateCommandBuffers(int i)
        {
            var commandBuffer = commandBuffers[i];
            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo,
            };

            if (vulkanEngine.Api.BeginCommandBuffer(commandBuffer, beginInfo) != Result.Success)
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

                vulkanEngine.Api.CmdBeginRenderPass(commandBuffer, &renderPassInfo, SubpassContents.Inline);
            }

            vulkanEngine.Api.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, vulkanEngine.Pipeline);

            var vertexBuffers = new Buffer[] { modelBuffers.VertexBuffer };
            var modelOffset = modelBuffers.GetModelOffset(0);
            var offsets = new ulong[] { modelOffset.VertexOffset };

            fixed (ulong* offsetsPtr = offsets)
            fixed (Buffer* vertexBuffersPtr = vertexBuffers)
            {
                vulkanEngine.Api.CmdBindVertexBuffers(commandBuffer, 0, 1, vertexBuffersPtr, offsetsPtr);
            }

            vulkanEngine.Api.CmdBindIndexBuffer(commandBuffer, modelBuffers.IndexBuffer, modelOffset.IndexOffsetBytes, IndexType.Uint32);

            var offsetCounts = new uint[] { 0 };
            fixed (uint* offsetCountsPtr = offsetCounts)
            {
                vulkanEngine.Api.CmdBindDescriptorSets(commandBuffer, PipelineBindPoint.Graphics, vulkanEngine.PipelineLayout, 0, 1, vulkanEngine.DescriptorSets![i], 1, offsetCountsPtr);
            }

            vulkanEngine.Api.CmdDrawIndexed(commandBuffer, (uint)modelOffset.IndexCount, 1, (uint)modelOffset.IndexOffset, (int)modelOffset.VertexOffset, 0);

            vulkanEngine.Api.CmdEndRenderPass(commandBuffer);

            if (vulkanEngine.Api.EndCommandBuffer(commandBuffer) != Result.Success)
            {
                throw new Exception("failed to record command buffer!");
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
