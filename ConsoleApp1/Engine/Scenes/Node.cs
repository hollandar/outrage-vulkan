using Silk.NET.Maths;

namespace ConsoleApp1.Engine.Scenes
{
    public abstract class Node
    {
        static uint nextId = 0;
        uint id;

        public Node()
        {
            id = nextId++;
        }

        public uint Id { get => this.id; }
    }
}
