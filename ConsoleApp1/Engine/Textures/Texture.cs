using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ConsoleApp1.Engine.Textures
{
    public class Texture : IDisposable
    {
        private string texturePath;
        private Image<Rgba32> image;
        public Texture(string texturePath)
        {
            this.texturePath = texturePath;
            image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(texturePath);
        }

        public Image<Rgba32> Image { get => this.Image; }
        public int Width { get => this.image.Width; }
        public int Height { get => this.image.Height; }
        public int BitsPerPixel { get => this.image.PixelType.BitsPerPixel; }

        public void CopyTo(Span<byte> destination)
        {
            this.image.CopyPixelDataTo(destination);
        }

        private void Cleanup()
        {
            image?.Dispose();
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
