using CBRE.Common;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.DataStructures.Transformations;
using CBRE.DataStructures.GameData;
using CBRE.DataStructures.MapObjects;
using CBRE.Providers.Texture;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace CBRE.Providers.Map {
    public class RMeshProvider : MapProvider {
        protected override bool IsValidForFileName(string filename) {
            return filename.EndsWith(".rmesh", StringComparison.OrdinalIgnoreCase);
        }

        protected override IEnumerable<MapFeature> GetFormatFeatures() {
            return new[] {
                MapFeature.Solids,
                MapFeature.Entities,
                MapFeature.Displacements, // TODO
                MapFeature.Groups,
                MapFeature.SingleVisgroups,
                MapFeature.MultipleVisgroups,
            };
        }

        private struct Property {
            public string name;
            public VariableType type;
        }


        private enum Lightmapped : byte {
            No = 0,
            Fully = 1,
            Outdated = 2,
        }

        // # RMesh blitz3d byte stream ----
        private string ReadByteString(BinaryReader reader) {
            int len = reader.ReadInt32();
            string retVal = "";
            for( int i=0; i<len; i++) {
                retVal = retVal + ((char)reader.ReadByte());
            }
            return retVal;
        }


        private void ReadVisgroups(BinaryReader reader, MapObject mo) {
            int visNum = reader.ReadInt32();
            for (int i = 0; i < visNum; i++) {
                mo.Visgroups.Add(reader.ReadInt32());
            }
        }

        protected override void SaveToStream(Stream stream, DataStructures.MapObjects.Map map, DataStructures.GameData.GameData gameData, TextureCollection textureCollection) {
            throw new NotImplementedException("don't save to 3dw, ew");
        }

        protected override DataStructures.MapObjects.Map GetFromStream(Stream stream, IEnumerable<string> modelDirs, out Image[] lightmaps) {
            BinaryReader reader = new BinaryReader(stream);
            DataStructures.MapObjects.Map map = new DataStructures.MapObjects.Map();
            map.WorldSpawn = new World(map.IDGenerator.GetNextObjectID());
            map.CordonBounds = new Box(Coordinate.One * -16384, Coordinate.One * 16384);
            lightmaps = null;

            int headerLen = reader.ReadInt32();
            string headerStr = reader.ReadFixedLengthString(System.Text.Encoding.UTF8, headerLen);
            bool isTriggerBox = false;
            //throw new ProviderException(headerStr);
            if (headerStr == "RoomMesh.HasTriggerBox") { isTriggerBox = true; }
            else if(headerStr != "RoomMesh") { throw new ProviderException("Failed to load file: Not an RMesh"); }

            int meshCount = reader.ReadInt32();
            List<string> textures = new List<string>();

            List<MapObject> meshes = new List<MapObject>();
            List<MapObject> entities = new List<MapObject>();

            for (int i=0; i<meshCount; i++) {
                Entity ent = new Entity(map.IDGenerator.GetNextObjectID());
                ent.Colour = Color.White;
                ent.ClassName = "model";
                ent.EntityData.Name = "model";
                entities.Add(ent);

                int isAlpha = 0;
                string[] texTiles = new string[2];
                int[] texCoords = new int[2];

                Solid mesh = new Solid(map.IDGenerator.GetNextObjectID());
                meshes.Add(mesh);

                for (int j = 0; j <= 1; j++) {
                    int alphaType = reader.ReadByte();
                    if (alphaType != 0) {
                        if (reader.PeekChar() != 0) {
                            string texture = ReadByteString(reader);
                            if (!textures.Contains(texture)) { textures.Add(texture); }
                            texTiles[j] = texture;
                        } else {
                            reader.ReadInt32();
                        }
                    }
                    if (texTiles[j] != "") {
                        isAlpha = 2;
                        if (alphaType == 3) { isAlpha = 1; }
                        texCoords[j] = 1 - j;
                    }
                }
                if (isAlpha == 1) {
                    //textureBlend texTiles[1];
                } else {
                    // textureBlend;
                }

                int vertexCount = reader.ReadInt32();
                List<Coordinate> vertexCoords = new List<Coordinate>();
                decimal[] vertexU = new decimal[vertexCount];
                decimal[] vertexV = new decimal[vertexCount];
                int[] vertexCR = new int[vertexCount];
                int[] vertexCG = new int[vertexCount];
                int[] vertexCB = new int[vertexCount];

                for (int j=0; j<vertexCount; j++) {
                    // Vertexes
                    decimal x = (decimal)reader.ReadSingle();
                    decimal y = (decimal)reader.ReadSingle();
                    decimal z = (decimal)reader.ReadSingle();
                    Coordinate pos = new Coordinate(x, y, z);
                    vertexCoords.Insert(j, new Coordinate(x, y, z));
                    reader.ReadSingle(); // unused?
                    reader.ReadSingle(); // unused?
                    decimal u = new decimal(reader.ReadSingle());
                    decimal v = new decimal(reader.ReadSingle());
                    vertexU[j] = u;
                    vertexV[j] = v;
                    int cRed = reader.ReadByte();
                    int cGreen = reader.ReadByte();
                    int cBlue = reader.ReadByte();
                    vertexCR[j] = cRed;
                    vertexCG[j] = cGreen;
                    vertexCB[j] = cBlue;
                }


                int polyCount = reader.ReadInt32();
                int[] triangleX = new int[polyCount];
                int[] triangleY = new int[polyCount];
                int[] triangleZ = new int[polyCount];

                for (int j=0;j<polyCount;j++) {
                    // Triangles
                    //decimal x = new decimal(reader.ReadInt32());
                    //decimal y = new decimal(reader.ReadInt32());
                    //decimal z = new decimal(reader.ReadInt32());
                    int x = reader.ReadInt32();
                    int y = reader.ReadInt32();
                    int z = reader.ReadInt32();
                    triangleX[j] = x;
                    triangleY[j] = y;
                    triangleZ[j] = z;
                }

                int visgroupIndex = 1;
                //int vertexCount = reader.ReadInt32();
                //List<Coordinate> vertexCoords = new List<Coordinate>();
                //decimal[] vertexU = new decimal[vertexCount];
                //decimal[] vertexV = new decimal[vertexCount];
                //int[] vertexCR = new int[vertexCount];
                //int[] vertexCG = new int[vertexCount];
                //int[] vertexCB = new int[vertexCount];

                List<Face> faces = new List<Face>();
                for(int j=0; j<polyCount; j++) {
                    Face face = new Face(map.IDGenerator.GetNextFaceID());
                    face.Vertices.Insert(0, new Vertex(vertexCoords[triangleX[j]], face));
                    face.Vertices.Insert(0, new Vertex(vertexCoords[triangleY[j]], face));
                    face.Vertices.Insert(0, new Vertex(vertexCoords[triangleZ[j]], face));
                    face.Plane = new Plane(face.Vertices[0].Location, face.Vertices[1].Location, face.Vertices[2].Location);
                    face.UpdateBoundingBox();

                    face.Transform(new UnitScale(Coordinate.One, face.BoundingBox.Center), TransformFlags.None);

                    faces.Add(face);
                    face.Parent = mesh;
                    mesh.Faces.Add(face);
                    mesh.Visgroups.Add(1);

                    mesh.SetParent(map.WorldSpawn);
                    mesh.Transform(new UnitScale(Coordinate.One, mesh.BoundingBox.Center), TransformFlags.None);
                }

                if (isAlpha==1) {
                    // Add mesh with 0 alpha
                } else {
                    // add mesh as opaque
                    // parent collisionMeshes?
                    // set childmesh alpha 0
                    // set type hit_map
                    // pickmode 2?
                    // double-side collision
                    // flipChild=copyMesh(childmesh);
                }
            } // done each mesh

            stream.Close();
            return map;

        }
    }

}
