using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Engine.Scenes
{
    public sealed class ModelNode:Node
    {
        public uint ModelId { get; set; }
        public uint TextureId { get; set; }
        public Matrix4X4<float> Local { get; set; } = Matrix4X4<float>.Identity;

        public ModelNode(uint modelId, uint textureId, Vector3D<float> position, Vector3D<float>? rotation = null, Vector3D<float>? scale = null): base()
        {
            this.ModelId = modelId;
            this.TextureId = textureId;

            var rotationVector = rotation ?? Vector3D<float>.Zero;
            var scaleVector = scale ?? Vector3D<float>.One;
            var positionMatrix = Matrix4X4.CreateScale(scaleVector) * Matrix4X4.CreateTranslation(position);
            var rotationMatrix = Matrix4X4.Transform(positionMatrix, Quaternion<float>.CreateFromYawPitchRoll(rotationVector.Y, rotationVector.X, rotationVector.Z));

            this.Local = rotationMatrix;
        }

    }
}
