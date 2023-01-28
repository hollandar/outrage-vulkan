using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Engine.Models
{
    public class ModelsCollection
    {
        private uint nextIndex = 0;
        private IDictionary<uint, Model> models = new Dictionary<uint, Model>();

        public ModelsCollection() { }

        public IEnumerable<uint> ModelIds { get => models.Keys; }
        public ulong VertexCount
        {
            get
            {
                ulong vertexCount = 0;
                foreach (var model in models.Values)
                {
                    vertexCount += (ulong)model.VertexCount;
                }

                return vertexCount;
            }
        }
        public ulong IndexCount
        {
            get
            {
                ulong indexCount = 0;
                foreach (var model in models.Values)
                {
                    indexCount += (ulong)model.IndexCount;
                }

                return indexCount;
            }
        }
        public Model? GetModel(uint ix)
        {
            if (models.TryGetValue(ix, out var model))
            {
                return model;
            }

            return null;
        }

        public uint AddModel(Model model)
        {
            var ix = nextIndex++;
            models[ix] = model;

            return ix;
        }
    }
}
