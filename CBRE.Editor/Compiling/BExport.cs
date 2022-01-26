using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Documents;

namespace CBRE.Editor.Compiling {
    class BExport {
        private class BVertex {
            public BVertex(Coordinate pos, Coordinate norm, Coordinate tang, Coordinate bitang, double u, double v) {
                this.pos = pos; this.norm = norm; this.tang = tang; this.bitang = bitang; this.u = u; this.v = v;
            }
            public Coordinate pos;
            public Coordinate norm;
            public Coordinate tang;
            public Coordinate bitang;
            public double u; public double v;
        }

        public static void SaveToFile(string filename, Document document, ExportForm form) {
            List<string> textures = new List<string>();
            List<Dictionary<Vertex, int>> allVertToInd = new List<Dictionary<Vertex, int>>();
            List<List<BVertex>> allVertices = new List<List<BVertex>>();
            List<List<int>> allIndices = new List<List<int>>();
            
            { // Gathering mesh data.
                foreach (IGrouping<string, Face> group in document.Map.WorldSpawn.Find(x => x is Solid)
                        .SelectMany(x => ((Solid)x).Faces)
                        .GroupBy(x => x.Texture.Name.ToLowerInvariant())
                        .OrderBy(x => x.Key)
                        .Where(x => !x.Key.StartsWith("tooltextures/"))) {
                    textures.Add(group.Key);
                    Dictionary<Vertex, int> vertToInd = new Dictionary<Vertex, int>();
                    List<BVertex> vertices = new List<BVertex>();
                    List<int> indices = new List<int>();
                    allVertToInd.Add(vertToInd);
                    allVertices.Add(vertices);
                    allIndices.Add(indices);
                    foreach (Face f in group) {
                        foreach (Vertex v in f.Vertices) {
                            bool found = false;
                            for (int i = 0; i < vertices.Count; i++) {
                                if (vertices[i].pos.EquivalentTo(v.Location, 0.01M)) {
                                    if (vertices[i].norm.EquivalentTo(v.Parent.Plane.Normal, 0.01M)
                                    && Math.Abs(vertices[i].u - v.DTextureU) < 0.01
                                    && Math.Abs(vertices[i].v - v.DTextureV) < 0.01) {
                                        vertToInd.Add(v, i);
                                        found = true;
                                        break;
                                    }
                                    if ((vertices[i].norm - v.Parent.Plane.Normal).VectorMagnitude() < 1.0M) {
                                        form.ProgressLog.Invoke((MethodInvoker)(() => form.ProgressLog.AppendText("\nBased Department!")));
                                        vertices[i].norm = ((vertices[i].norm + v.Parent.Plane.Normal) * 0.5M).Normalise();
                                        vertices[i].tang = ((vertices[i].tang + v.Parent.Texture.UAxis) * 0.5M).Normalise();
                                        vertices[i].bitang = ((vertices[i].bitang + v.Parent.Texture.VAxis) * 0.5M).Normalise();

                                        vertToInd.Add(v, vertices.Count);
                                        vertices.Add(new BVertex(v.Location,
                                            vertices[i].norm, vertices[i].tang, vertices[i].bitang,
                                            v.DTextureU, v.DTextureV
                                        ));

                                        found = true;
                                        break;
                                    }
                                }
                            }
                            if (!found) {
                                vertToInd.Add(v, vertices.Count);
                                vertices.Add(new BVertex(v.Location,
                                    v.Parent.Plane.Normal, v.Parent.Texture.UAxis, v.Parent.Texture.VAxis,
                                    v.DTextureU, v.DTextureV)
                                );
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
                    List<BVertex> vertices = allVertices[i];
                    List<int> indices = allIndices[i];

                    br.Write(vertices.Count);
                    foreach (BVertex v in vertices) {
                        br.Write((float)v.pos.DX);
                        br.Write((float)v.pos.DZ);
                        br.Write((float)v.pos.DY);
                        br.Write((float)v.norm.DX);
                        br.Write((float)v.norm.DZ);
                        br.Write((float)v.norm.DY);
                        br.Write((float)v.tang.DX);
                        br.Write((float)v.tang.DZ);
                        br.Write((float)v.tang.DY);
                        br.Write((float)v.bitang.DX);
                        br.Write((float)v.bitang.DZ);
                        br.Write((float)v.bitang.DY);
                        br.Write((float)v.u);
                        br.Write((float)v.v);
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
