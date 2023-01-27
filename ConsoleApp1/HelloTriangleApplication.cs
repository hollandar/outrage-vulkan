using ConsoleApp1;
using ConsoleApp1.Engine.Vulkan;
using ConsoleApp1.Engine.Windowing;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

unsafe class HelloTriangleApplication
{
    const int WIDTH = 800;
    const int HEIGHT = 600;

    public void Run()
    {
        using var window = new AppWindow(WIDTH, HEIGHT, "Vulkan");
        using var engine = new VulkanEngine(window, true);

        window!.Run();
    }
}