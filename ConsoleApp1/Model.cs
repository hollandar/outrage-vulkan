﻿using Silk.NET.Assimp;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public class Model
    {
        private Vertex[]? vertices;
        private uint[]? indices;

        public Model(string modelPath)
        {
            if (!System.IO.File.Exists(modelPath))
                throw new Exception($"Could not load file {modelPath}, it does not exist.");

            LoadModel(modelPath);
        }

        public Vertex[]? Vertices => vertices;
        public int VerticesLength => vertices?.Length ?? 0;
        public uint[]? Indices => indices;
        public int IndicesLength => indices?.Length ?? 0;

        private unsafe void LoadModel(string modelPath)
        {
            using var assimp = Assimp.GetApi();
            var scene = assimp.ImportFile(modelPath, (uint)PostProcessPreset.TargetRealTimeMaximumQuality);

            var vertexMap = new Dictionary<Vertex, uint>();
            var vertices = new List<Vertex>();
            var indices = new List<uint>();

            VisitSceneNode(scene->MRootNode);

            assimp.ReleaseImport(scene);

            this.vertices = vertices.ToArray();
            this.indices = indices.ToArray();

            void VisitSceneNode(Node* node)
            {
                for (int m = 0; m < node->MNumMeshes; m++)
                {
                    var mesh = scene->MMeshes[node->MMeshes[m]];

                    for (int f = 0; f < mesh->MNumFaces; f++)
                    {
                        var face = mesh->MFaces[f];

                        for (int i = 0; i < face.MNumIndices; i++)
                        {
                            uint index = face.MIndices[i];

                            var position = mesh->MVertices[index];
                            var texture = mesh->MTextureCoords[0][(int)index];

                            Vertex vertex = new Vertex
                            {
                                pos = new Vector3D<float>(position.X, position.Y, position.Z),
                                color = new Vector3D<float>(1, 1, 1),
                                //Flip Y for OBJ in Vulkan
                                textCoord = new Vector2D<float>(texture.X, 1.0f - texture.Y)
                            };

                            if (vertexMap.TryGetValue(vertex, out var meshIndex))
                            {
                                indices.Add(meshIndex);
                            }
                            else
                            {
                                indices.Add((uint)vertices.Count);
                                vertexMap[vertex] = (uint)vertices.Count;
                                vertices.Add(vertex);
                            }
                        }
                    }
                }

                for (int c = 0; c < node->MNumChildren; c++)
                {
                    VisitSceneNode(node->MChildren[c]);
                }
            }
        }
    }
}
