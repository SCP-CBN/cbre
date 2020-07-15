using CBRE.Common;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.DataStructures.Transformations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CBRE.Providers.Map {
    public class L3DWProvider : MapProvider {
        protected override DataStructures.MapObjects.Map GetFromFile(string filename) {
            using (var strm = new FileStream(filename, FileMode.Open, FileAccess.Read)) {
                return GetFromStream(strm);
            }
        }

        protected override void SaveToFile(string filename, DataStructures.MapObjects.Map map) {
            using (var strm = new FileStream(filename, FileMode.Create, FileAccess.Write)) {
                SaveToStream(strm, map);
            }
        }

        protected override bool IsValidForFileName(string filename) {
            return filename.EndsWith(".3dw", StringComparison.OrdinalIgnoreCase);
        }

        protected override DataStructures.MapObjects.Map GetFromStream(Stream stream) {
            var map = new DataStructures.MapObjects.Map();
            map.CordonBounds = new Box(Coordinate.One * -16384, Coordinate.One * 16384);
            BinaryReader br = new BinaryReader(stream);

            //header
            UInt16 mapVersion = br.ReadUInt16();
            byte mapFlags = br.ReadByte();
            Int32 nameCount = br.ReadInt32();
            Int32 nameOffset = br.ReadInt32();
            Int32 objectCount = br.ReadInt32();
            Int32 objectOffset = br.ReadInt32();

            //get names, needed to understand the objects
            List<string> names = new List<string>();
            br.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
            for (int i = 0; i < nameCount; i++) {
                string name = br.ReadNullTerminatedString();
                names.Add(name);
            }

            //now we can parse the object table
            List<string> materials = new List<string>();
            List<Tuple<int, string>> meshReferences = new List<Tuple<int, string>>();
            Dictionary<int, Group> groups = new Dictionary<int, Group>();
            Dictionary<int, int> visgroups = new Dictionary<int, int>();
            br.BaseStream.Seek(objectOffset, SeekOrigin.Begin);
            long objectStartPos = br.BaseStream.Position;
            for (int i = 0; i < objectCount; i++) {
                int index = br.ReadInt32() - 1;
                int size = br.ReadInt32();
                string name = null;
                if (index >= 0 && index < names.Count) {
                    name = names[index];
                }

                if (name == "group") {
                    byte flags = br.ReadByte();
                    Int32 groupIndex = br.ReadInt32();

                    Group newGroup = new Group(map.IDGenerator.GetNextObjectID());
                    newGroup.SetParent(map.WorldSpawn);

                    groups.Add(i, newGroup);
                } else if (name == "visgroup") {
                    byte flags = br.ReadByte();
                    string groupName = names[br.ReadInt32() - 1];
                    byte colorR = br.ReadByte(); byte colorG = br.ReadByte(); byte colorB = br.ReadByte();

                    Visgroup newGroup = new Visgroup() { Name = groupName, ID = visgroups.Count+1 };
                    newGroup.Colour = System.Drawing.Color.FromArgb(colorR, colorG, colorB);
                    map.Visgroups.Add(newGroup);
                    visgroups.Add(i, newGroup.ID);
                } else if (name == "meshreference") {
                    byte flags = br.ReadByte();

                    Int32 groupNameInd = br.ReadInt32() - 1;
                    Int32 objectNameInd = br.ReadInt32() - 1;

                    byte limbCount = br.ReadByte();

                    meshReferences.Add(new Tuple<int, string>(i, names[objectNameInd]));
                } else if (name == "material") {
                    byte materialFlags = br.ReadByte();
                    Int32 groupIndex = br.ReadInt32();
                    string objectName = names[br.ReadInt32() - 1];
                    Int32 extensionNameIndex = -1;
                    if ((materialFlags & 2) != 0) {
                        extensionNameIndex = br.ReadInt32(); //TODO: what the heck is this
                    }
                    materials.Add(objectName);
                } else {
                    br.BaseStream.Seek(size, SeekOrigin.Current);
                }
            }
            br.BaseStream.Position = objectStartPos;
            for (int i = 0; i < objectCount; i++) {
                int index = br.ReadInt32() - 1;
                int size = br.ReadInt32();
                string name = null;
                if (index >= 0 && index < names.Count) {
                    name = names[index];
                }

                if (name == "mesh") {
                    Property newProperty;

                    long startPos = br.BaseStream.Position;

                    byte flags = br.ReadByte();

                    Entity entity = new Entity(map.IDGenerator.GetNextObjectID());
                    entity.ClassName = "model";
                    entity.EntityData.Name = "model";
                    entity.Colour = Colour.GetDefaultEntityColour();

                    Int32 keyCount = br.ReadInt32();
                    for (int j = 0; j < keyCount; j++) {
                        Int32 keyNameInd = br.ReadInt32() - 1;
                        Int32 keyValueInd = br.ReadInt32() - 1;
                        if (names[keyNameInd] != "classname") {
                            newProperty = new Property();
                            newProperty.Key = names[keyNameInd];
                            newProperty.Value = names[keyValueInd];

                            if (newProperty.Key == "file") {
                                newProperty.Value = System.IO.Path.GetFileNameWithoutExtension(newProperty.Value);
                            }

                            entity.EntityData.Properties.Add(newProperty);
                        }
                    }

                    Int32 groupIndex = br.ReadInt32() - 1;
                    Int32 visgroupIndex = br.ReadInt32() - 1;
                    if (visgroups.ContainsKey(visgroupIndex)) {
                        entity.Visgroups.Add(visgroups[visgroupIndex]);
                    }

                    byte red = br.ReadByte(); byte green = br.ReadByte(); byte blue = br.ReadByte();

                    Int32 meshRefIndex = br.ReadInt32() - 1;

                    float x = br.ReadSingle();
                    float z = br.ReadSingle();
                    float y = br.ReadSingle();
                    if (entity != null) entity.Origin = new Coordinate((decimal)x, (decimal)y, (decimal)z);

                    if (entity.EntityData.GetPropertyValue("file") == null) {
                        newProperty = new Property();
                        newProperty.Key = "file";
                        newProperty.Value = meshReferences.Find(q => q.Item1 == meshRefIndex).Item2;

                        entity.EntityData.Properties.Add(newProperty);
                    }

                    float pitch = br.ReadSingle();
                    float yaw = br.ReadSingle();
                    float roll = br.ReadSingle();
                    newProperty = new Property();
                    newProperty.Key = "angles";
                    newProperty.Value = pitch.ToString() + " " + yaw.ToString() + " " + roll.ToString();

                    entity.EntityData.Properties.Add(newProperty);

                    float xScale = 1.0f;
                    float yScale = 1.0f;
                    float zScale = 1.0f;

                    if ((flags & 1) == 0) {
                        xScale = br.ReadSingle();
                        yScale = br.ReadSingle();
                        zScale = br.ReadSingle();
                    }

                    newProperty = new Property();
                    newProperty.Key = "scale";
                    newProperty.Value = xScale.ToString() + " " + yScale.ToString() + " " + zScale.ToString();

                    entity.EntityData.Properties.Add(newProperty);

                    br.BaseStream.Position += size - (br.BaseStream.Position - startPos);

                    entity.UpdateBoundingBox();

                    if (groups.ContainsKey(groupIndex)) {
                        entity.SetParent(groups[groupIndex]);
                    } else {
                        entity.SetParent(map.WorldSpawn);
                    }
                } else if (name == "entity") {
                    byte flags = br.ReadByte();
                    float x = br.ReadSingle();
                    float z = br.ReadSingle();
                    float y = br.ReadSingle();

                    Entity entity = new Entity(map.IDGenerator.GetNextObjectID());
                    entity.Colour = Colour.GetDefaultEntityColour();
                    entity.Origin = new Coordinate((decimal)x, (decimal)y, (decimal)z);

                    Int32 keyCount = br.ReadInt32();
                    for (int j = 0; j < keyCount; j++) {
                        Int32 keyNameInd = br.ReadInt32() - 1;
                        Int32 keyValueInd = br.ReadInt32() - 1;
                        if (names[keyNameInd] == "classname") {
                            entity.ClassName = names[keyValueInd];
                            entity.EntityData.Name = names[keyValueInd];
                        } else {
                            Property newProperty = new Property();
                            newProperty.Key = names[keyNameInd];
                            newProperty.Value = names[keyValueInd];
                            entity.EntityData.Properties.Add(newProperty);
                        }
                    }

                    Int32 groupIndex = br.ReadInt32() - 1;
                    Int32 visgroupIndex = br.ReadInt32() - 1;
                    if (visgroups.ContainsKey(visgroupIndex)) {
                        entity.Visgroups.Add(visgroups[visgroupIndex]);
                    }

                    entity.UpdateBoundingBox();
                    if (groups.ContainsKey(groupIndex)) {
                        entity.SetParent(groups[groupIndex]);
                    } else {
                        entity.SetParent(map.WorldSpawn);
                    }
                } else if (name == "brush") {
                    bool invisibleCollision = false;

                    byte brushFlags = br.ReadByte(); //TODO: ???
                    Int32 keys = br.ReadInt32();
                    for (int j = 0; j < keys; j++) {
                        Int32 keyNameInd = br.ReadInt32();
                        Int32 keyValueInd = br.ReadInt32();
                        string keyName = names[keyNameInd - 1];
                        if (keyName.Equals("classname", StringComparison.OrdinalIgnoreCase)) {
                            string keyValue = names[keyValueInd - 1];
                            if (keyValue.Equals("field_hit", StringComparison.OrdinalIgnoreCase)) {
                                invisibleCollision = true;
                            }
                        }
                    }
                    Int32 groupIndex = br.ReadInt32() - 1;
                    Int32 visgroupIndex = br.ReadInt32() - 1;

                    byte red = br.ReadByte(); byte green = br.ReadByte(); byte blue = br.ReadByte();

                    List<Coordinate> vertices = new List<Coordinate>();
                    byte vertexCount = br.ReadByte();
                    for (int j = 0; j < vertexCount; j++) {
                        decimal x = (decimal)br.ReadSingle(); decimal z = (decimal)br.ReadSingle(); decimal y = (decimal)br.ReadSingle();
                        vertices.Add(new Coordinate(x, y, z));
                    }
                    List<Face> faces = new List<Face>();
                    byte faceCount = br.ReadByte();
                    for (int j = 0; j < faceCount; j++) {
                        byte faceFlags = br.ReadByte();

                        //TODO: maybe we need these unused bits for something idk
                        decimal planeEq0 = (decimal)br.ReadSingle(); decimal planeEq1 = (decimal)br.ReadSingle(); decimal planeEq2 = (decimal)br.ReadSingle(); decimal planeEq3 = (decimal)br.ReadSingle();

                        decimal texPosX = (decimal)br.ReadSingle(); decimal texPosY = (decimal)br.ReadSingle();
                        decimal texScaleX = (decimal)br.ReadSingle(); decimal texScaleY = (decimal)br.ReadSingle();
                        float texRotX = br.ReadSingle(); float texRotY = br.ReadSingle();

                        decimal uTexPlane0 = (decimal)br.ReadSingle(); decimal uTexPlane1 = (decimal)br.ReadSingle(); decimal uTexPlane2 = (decimal)br.ReadSingle(); decimal uTexPlane3 = (decimal)br.ReadSingle();
                        decimal vTexPlane0 = (decimal)br.ReadSingle(); decimal vTexPlane1 = (decimal)br.ReadSingle(); decimal vTexPlane2 = (decimal)br.ReadSingle(); decimal vTexPlane3 = (decimal)br.ReadSingle();

                        float luxelSize = br.ReadSingle();

                        Int32 smoothGroupInd = br.ReadInt32();
                        Int32 materialInd = br.ReadInt32() - 1;

                        Int32 lightmapInd = -1;
                        if ((faceFlags & 16) != 0) {
                            lightmapInd = br.ReadInt32();
                        }

                        byte indexCount = br.ReadByte();
                        List<byte> vertsInFace = new List<byte>();
                        for (int k = 0; k < indexCount; k++) {
                            byte vertIndex = br.ReadByte();
                            vertsInFace.Add(vertIndex);

                            float texCoordX = br.ReadSingle(); float texCoordY = br.ReadSingle();

                            float lmCoordX = 0.0f; float lmCoordY = 0.0f;
                            if ((faceFlags & 16) != 0) {
                                lmCoordX = br.ReadSingle(); lmCoordY = br.ReadSingle();
                            }
                        }

                        Coordinate norm = new Coordinate(planeEq0, planeEq2, planeEq1);

                        if (Math.Abs((float)norm.LengthSquared()) > 0.001f) {
                            if (Math.Abs((double)norm.LengthSquared() - 1) > 0.001) throw new Exception(norm.LengthSquared().ToString());

                            Face newFace = new Face(map.IDGenerator.GetNextFaceID());

                            foreach (byte vertInd in vertsInFace) {
                                newFace.Vertices.Insert(0, new Vertex(vertices[vertInd], newFace));
                            }

                            newFace.Plane = new Plane(newFace.Vertices[0].Location, newFace.Vertices[1].Location, newFace.Vertices[2].Location);

                            newFace.UpdateBoundingBox();

                            Coordinate uNorm = new Coordinate(uTexPlane0, uTexPlane2, uTexPlane1).Normalise();
                            Coordinate vNorm = new Coordinate(vTexPlane0, vTexPlane2, vTexPlane1).Normalise();
                            if (Math.Abs((double)(uNorm.LengthSquared() - vNorm.LengthSquared())) > 0.001) throw new Exception(uNorm.LengthSquared().ToString() + " " + vNorm.LengthSquared().ToString());

                            newFace.Texture.Name = (faceFlags & 4) != 0 ? "tooltextures/remove_face" :
                                                    invisibleCollision ? "tooltextures/invisible_collision" :
                                                                          materials[materialInd];
                            newFace.AlignTextureToWorld();

                            newFace.Texture.UAxis = uNorm * (decimal)Math.Cos(-texRotX * Math.PI / 180.0) + vNorm * (decimal)Math.Sin(-texRotX * Math.PI / 180.0);
                            newFace.Texture.VAxis = vNorm * (decimal)Math.Cos(-texRotX * Math.PI / 180.0) - uNorm * (decimal)Math.Sin(-texRotX * Math.PI / 180.0);

                            //huh?????
                            if (Math.Abs(texScaleX) < 0.0001m) {
                                if (Math.Abs(texScaleY) < 0.0001m) {
                                    texScaleX = 1m;
                                    texScaleY = 1m;
                                } else {
                                    texScaleX = texScaleY;
                                }
                            } else if (Math.Abs(texScaleY) < 0.0001m) {
                                texScaleY = texScaleX;
                            }
                            newFace.Texture.XScale = texScaleX / 2;
                            newFace.Texture.YScale = texScaleY / 2;
                            newFace.Texture.XShift = -texPosX * 2 / texScaleX;
                            newFace.Texture.YShift = texPosY * 2 / texScaleY;
                            newFace.Texture.Rotation = (decimal)texRotX;

                            //seriously, what the FUCK???????????
                            if ((texRotX - texRotY) > 120.0f) {
                                newFace.Texture.XScale *= -1m;
                                newFace.Texture.YScale *= -1m;
                                newFace.Texture.Rotation -= 180m;
                                newFace.Texture.UAxis = -newFace.Texture.UAxis;
                            } else if ((texRotY - texRotX) > 120.0f) {
                                newFace.Texture.XScale *= -1m;
                                newFace.Texture.YScale *= -1m;
                                newFace.Texture.Rotation -= 180m;
                                newFace.Texture.VAxis = -newFace.Texture.VAxis;
                            }

                            newFace.Transform(new UnitScale(Coordinate.One, newFace.BoundingBox.Center), TransformFlags.None);

                            faces.Add(newFace);
                        }
                    }

                    Solid newSolid = new Solid(map.IDGenerator.GetNextObjectID());
                    foreach (Face face in faces) {
                        face.Parent = newSolid;
                        newSolid.Faces.Add(face);
                    }
                    if (visgroups.ContainsKey(visgroupIndex)) {
                        newSolid.Visgroups.Add(visgroups[visgroupIndex]);
                    }
                    newSolid.Colour = Colour.GetRandomBrushColour();
                    newSolid.UpdateBoundingBox();

                    MapObject parent = map.WorldSpawn;
                    if (groups.ContainsKey(groupIndex)) {
                        parent = groups[groupIndex];
                    }

                    if (newSolid.IsValid()) {
                        newSolid.SetParent(parent);

                        newSolid.Transform(new UnitScale(Coordinate.One, newSolid.BoundingBox.Center), TransformFlags.None);
                    } else {
                        var offset = newSolid.BoundingBox.Center;
                        // Not a valid solid, decompose into tetrahedrons/etc
                        foreach (var face in faces) {
                            var polygon = new Polygon(face.Vertices.Select(x => x.Location));
                            if (!polygon.IsValid() || !polygon.IsConvex()) {
                                // tetrahedrons
                                foreach (var triangle in face.GetTrianglesReversed()) {
                                    var tf = new Face(map.IDGenerator.GetNextFaceID());
                                    tf.Plane = new Plane(triangle[0].Location, triangle[1].Location, triangle[2].Location);
                                    tf.Vertices.AddRange(triangle.Select(x => new Vertex(x.Location, tf)));
                                    tf.Texture = face.Texture.Clone();
                                    tf.UpdateBoundingBox();
                                    newSolid = SolidifyFace(map, tf, offset);
                                    newSolid.SetParent(parent);
                                    newSolid.UpdateBoundingBox();

                                    newSolid.Transform(new UnitScale(Coordinate.One, newSolid.BoundingBox.Center), TransformFlags.None);
                                }
                            } else {
                                // cone/pyramid/whatever
                                newSolid = SolidifyFace(map, face, offset);
                                newSolid.SetParent(parent);
                                newSolid.UpdateBoundingBox();

                                newSolid.Transform(new UnitScale(Coordinate.One, newSolid.BoundingBox.Center), TransformFlags.None);
                            }
                        }
                    }
                } else {
                    if (name == "terrain") {
                        MapProvider.warnings = "This map contains displacements, which are currently not supported. The map will appear incomplete.";
                    }
                    br.BaseStream.Seek(size, SeekOrigin.Current);
                }
            }

            return map;
        }

        private Solid SolidifyFace(DataStructures.MapObjects.Map map, Face face, Coordinate offset) {
            var solid = new Solid(map.IDGenerator.GetNextObjectID());
            solid.Faces.Add(face);
            face.Parent = solid;
            var center = face.Vertices.Aggregate(Coordinate.Zero, (sum, v) => sum + v.Location) / face.Vertices.Count;
            var normalOffset = center - face.Plane.Normal * 5;
            if (face.Plane.Normal.Dot(offset - center) >= 0) { offset = normalOffset; }
            for (var i = 0; i < face.Vertices.Count; i++) {
                var v1 = face.Vertices[i];
                var v2 = face.Vertices[(i + 1) % face.Vertices.Count];
                var f = new Face(map.IDGenerator.GetNextFaceID());
                f.Parent = solid;
                f.Plane = new Plane(v1.Location, offset, v2.Location);
                f.Parent = solid;
                f.Vertices.Add(new Vertex(offset, f));
                f.Vertices.Add(new Vertex(v2.Location, f));
                f.Vertices.Add(new Vertex(v1.Location, f));
                f.Texture.Name = "tooltextures/remove_face";
                f.UpdateBoundingBox();

                solid.Faces.Add(f);
            }
            solid.Colour = Colour.GetRandomBrushColour();
            return solid;
        }

        protected override void SaveToStream(Stream stream, DataStructures.MapObjects.Map map) {
            throw new NotImplementedException("don't save to 3dw, ew");
        }

        protected override IEnumerable<MapFeature> GetFormatFeatures() {
            return new[]
            {
                MapFeature.Worldspawn,
                MapFeature.Solids,
                MapFeature.Entities
            };
        }
    }
}
