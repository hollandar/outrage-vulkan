using Silk.NET.Maths;

namespace ConsoleApp1.Engine.Scene
{
    public class Node
    {
        public uint ModelId { get; set; }
        public uint TextureId { get; set; }
        public Matrix4X4<float> MatrixModel { get; set; } = Matrix4X4<float>.Identity;

        public Node(Vector3D<float> position, Vector3D<float>? rotation = null, Vector3D<float>? scale = null)
        {
            var rotationVector = rotation ?? Vector3D<float>.Zero;
            var scaleVector = scale ?? Vector3D<float>.One;
            var positionMatrix = Matrix4X4.CreateScale(scaleVector) * Matrix4X4.CreateTranslation(position);
            var rotationMatrix = Matrix4X4.Transform(positionMatrix, Quaternion<float>.CreateFromYawPitchRoll(rotationVector.Y, rotationVector.X, rotationVector.Z));

            this.MatrixModel = rotationMatrix;
        }

    }
}
