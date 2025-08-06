using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Engine.Scenes
{
    public class Scene
    {
        private string name;
        List<Node> children = new List<Node>();

        public Scene (string name, params Node[] nodes)
        {
            this.name = name;
            if (nodes?.Any()??false)
            {
                children.AddRange(nodes);
            }
        }
    }
}
