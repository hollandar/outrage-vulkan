using Silk.NET.Assimp;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Silk.NET.GLFW.GlfwCallbacks;

namespace ConsoleApp1.Engine.Windowing
{
    internal class AppWindow : IDisposable, IAppWindow
    {
        private string title = "Vulkan";
        private int width = 800;
        private int height = 600;
        private IWindow window;

        public event Action<Vector2D<int>> Resize;
        public event Action<double> Render;

        public AppWindow(int width, int height, string title)
        {
            this.width = width;
            this.height = height;
            this.title = title;

            var options = WindowOptions.DefaultVulkan with
            {
                Size = new Vector2D<int>(width, height),
                Title = title,
            };

            window = Window.Create(options);
            window.Initialize();

            window.Resize += Window_Resize;
            window.Render += Window_Render;
        }

        public Vector2D<int> FramebufferSize { get => window.FramebufferSize; }
        public double Time { get => window.Time; }

        private void Window_Render(double delta)
        {
            if (Render != null)
                Render(delta);
        }

        private void Window_Resize(Vector2D<int> obj)
        {
            if (Resize != null)
                Resize(obj);
        }

        public void Run()
        {
            window.Run();
        }

        public void DoEvents()
        {
            window.DoEvents();
        }

        public IVkSurface GetVulkanSurface()
        {
            if (window.VkSurface is null)
            {
                throw new Exception("Windowing platform doesn't support Vulkan.");
            }

            return window.VkSurface;
        }

        public void Dispose()
        {
            window.Dispose();
        }
    }
}
