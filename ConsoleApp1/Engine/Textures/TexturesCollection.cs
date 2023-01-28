using Silk.NET.Assimp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Engine.Textures
{
    public class TexturesCollection
    {
        private uint nextTexture = 0;
        private IDictionary<uint, Texture> textures = new Dictionary<uint, Texture>();

        public TexturesCollection()
        {

        }

        public IEnumerable<KeyValuePair<uint, Texture>> Textures { get => this.textures.ToImmutableList(); }
        
        public uint AddTexture(Texture texture)
        {
            var id = nextTexture++;
            textures[id] = texture;

            return id;
        }

        public Texture GetTexture(uint id)
        {
            if (textures.TryGetValue(id, out var texture))
            {
                return texture;
            }

            throw new ArgumentException($"Texture with id {id} does not exist.");
        }
    }
}
