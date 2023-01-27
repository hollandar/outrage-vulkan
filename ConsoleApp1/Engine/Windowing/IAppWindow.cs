using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Engine.Windowing
{
    public interface IAppWindow: IDisposable
    {
        Vector2D<int> FramebufferSize { get; }
        double Time { get; }

        event Action<Vector2D<int>> Resize;
        event Action<double> Render;

        void DoEvents();
        IVkSurface GetVulkanSurface();
        void Run();
    }
}
