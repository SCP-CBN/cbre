using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Documents;

namespace CBRE.Editor.Compiling {
    class BExport {
        public static void SaveToFile(string filename, Document document, ExportForm form) {
            List<string> textures = new List<string>();
            List<Dictionary<Vertex, int>> allVertToInd = new List<Dictionary<Vertex, int>>();
            List<List<Vertex>> allVertices = new List<List<Vertex>>();
            List<List<int>> allIndices = new List<List<int>>();
            
            { // Gathering mesh data.
                foreach (IGrouping<string, Face> group in document.Map.WorldSpawn.Find(x => x is Solid)
                        .SelectMany(x => ((Solid)x).Faces)
                        .GroupBy(x => x.Texture.Name.ToLowerInvariant())
                        .OrderBy(x => x.Key)
                        .Where(x => !x.Key.StartsWith("tooltextures/"))) {
                    textures.Add(group.Key);
                    Dictionary<Vertex, int> vertToInd = new Dictionary<Vertex, int>();
                    List<Vertex> vertices = new List<Vertex>();
                    List<int> indices = new List<int>();
                    allVertToInd.Add(vertToInd);
                    allVertices.Add(vertices);
                    allIndices.Add(indices);
                    foreach (Face f in group) {
                        foreach (Vertex v in f.Vertices) {
                            bool found = false;
                            for (int i = 0; i < vertices.Count; i++) {
                                if (vertices[i].Location.EquivalentTo(v.Location, 0.01M)
                                    && vertices[i].Parent.Plane.Normal.EquivalentTo(v.Parent.Plane.Normal, 0.01M)
                                    && Math.Abs(vertices[i].DTextureU - v.DTextureU) < 0.01
                                    && Math.Abs(vertices[i].DTextureV - v.DTextureV) < 0.01) {
                                    vertToInd.Add(v, i);
                                    found = true;
                                    break;
                                }
                            }
                            if (!found) {
                                vertToInd.Add(v, vertices.Count);
                                vertices.Add(v);
                            }
                        }

                        foreach (Vertex[] triVerts in f.GetTriangles()) {
                            if (triVerts.Length != 3) { throw new Exception("Invalid triangles??"); }
                            for (int i = 2; i >= 0; i--) {
                                indices.Add(vertToInd[triVerts[i]]);
                            }
                        }
                    }
                }
            }

            if (textures.Count > byte.MaxValue) { throw new Exception("Too many textures!"); }

            { // Writing.
                string filepath = System.IO.Path.GetDirectoryName(filename);
                filename = System.IO.Path.GetFileName(filename);
                filename = System.IO.Path.GetFileNameWithoutExtension(filename) + ".b";

                FileStream stream = new FileStream(filepath + "/" + filename, FileMode.Create);
                BinaryWriter br = new BinaryWriter(stream);

                br.Write((byte)textures.Count);
                for (int i = 0; i < textures.Count; i++) {
                    br.WriteNullTerminatedString(textures[i]);
                    Dictionary<Vertex, int> vertToInd = allVertToInd[i];
                    List<Vertex> vertices = allVertices[i];
                    List<int> indices = allIndices[i];

                    br.Write(vertices.Count);
                    foreach (Vertex v in vertices) {
                        br.Write((float)v.Location.DX);
                        br.Write((float)v.Location.DZ);
                        br.Write((float)v.Location.DY);
                        br.Write((float)v.Parent.Plane.Normal.DX);
                        br.Write((float)v.Parent.Plane.Normal.DZ);
                        br.Write((float)v.Parent.Plane.Normal.DY);
                        br.Write((float)v.Parent.Texture.UAxis.X);
                        br.Write((float)v.Parent.Texture.UAxis.Z);
                        br.Write((float)v.Parent.Texture.UAxis.Y);
                        br.Write((float)v.Parent.Texture.VAxis.X);
                        br.Write((float)v.Parent.Texture.VAxis.Z);
                        br.Write((float)v.Parent.Texture.VAxis.Y);
                        br.Write((float)v.DTextureU);
                        br.Write((float)v.DTextureV);
                    }
                    br.Write(indices.Count);
                    foreach (int index in indices) {
                        br.Write(index);
                    }
                }
            }

            form.ProgressLog.Invoke((MethodInvoker)(() => form.ProgressLog.AppendText("\nDone!")));
            form.ProgressBar.Invoke((MethodInvoker)(() => form.ProgressBar.Value = 10000));
        }
    }
}
