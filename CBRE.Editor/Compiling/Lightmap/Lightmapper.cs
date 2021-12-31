﻿using CBRE.Common;
using CBRE.DataStructures.Geometric;
using CBRE.DataStructures.MapObjects;
using CBRE.Editor.Documents;
using CBRE.Editor.Popup;
using CBRE.Editor.Rendering;
using CBRE.Graphics;
using CBRE.Providers.Texture;
using CBRE.Settings;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CBRE.Editor.Compiling.Lightmap {
    sealed class Lightmapper {
        public readonly Document Document;
        public readonly ImmutableHashSet<LMFace> OpaqueFaces;
        public readonly ImmutableHashSet<LMFace> TranslucentFaces;
        public readonly ImmutableHashSet<LMFace> ToolFaces;
        public readonly ImmutableHashSet<LMFace> UnclassifiedFaces;
        public readonly ImmutableHashSet<LightmapGroup> Groups;
        
        public Lightmapper(Document document) {
            Document = document;

            var flattenedObjectList = Document.Map.WorldSpawn
                .GetSelfAndAllChildren();
            var solids = flattenedObjectList
                .OfType<Solid>();
            var allFaces = solids.SelectMany(s => s.Faces);
            
            HashSet<LMFace> opaqueFaces = new();
            HashSet<LMFace> translucentFaces = new();
            HashSet<LMFace> toolFaces = new();
            HashSet<LMFace> unclassifiedFaces = new();
            foreach (var face in allFaces) {
                face.Vertices.ForEach(v => { v.LMU = -500.0f; v.LMV = -500.0f; });
                face.UpdateBoundingBox();
                
                LMFace lmFace = new LMFace(face);

                if (lmFace.Texture.Name.StartsWith("ToolTextures/", StringComparison.OrdinalIgnoreCase)) {
                    toolFaces.Add(lmFace);
                } else if (lmFace.Texture.Texture is { } texture) {
                    (texture.HasTransparency() ? translucentFaces : opaqueFaces)
                        .Add(lmFace);
                } else {
                    unclassifiedFaces.Add(lmFace);
                }
            }

            OpaqueFaces = opaqueFaces.ToImmutableHashSet();
            TranslucentFaces = translucentFaces.ToImmutableHashSet();
            ToolFaces = toolFaces.ToImmutableHashSet();
            UnclassifiedFaces = unclassifiedFaces.ToImmutableHashSet();

            HashSet<LightmapGroup> groups = new();

            foreach (var face in OpaqueFaces) {
                LightmapGroup group = LightmapGroup.FindCoplanar(groups, face);
                if (group is null) {
                    group = new LightmapGroup();
                    groups.Add(group);
                }
                group.AddFace(face);
            }

            Groups = groups.ToImmutableHashSet();
        }


        public class Atlas {
            public readonly ImmutableHashSet<LightmapGroup> Groups;

            public Atlas(IEnumerable<LightmapGroup> groups) {
                Groups = groups.ToImmutableHashSet();
                foreach (var group in Groups) {
                    foreach (var face in group.Faces) {
                        face.UpdateLmUv(group);
                    }
                }
            }
        }

        public void Render() {
            var atlases = PrepareUvCoords();
            
        }
        
        private List<Atlas> PrepareUvCoords() {
            List<LightmapGroup> remainingGroups = Groups
                .OrderByDescending(g => g.Width * g.Height)
                .ThenByDescending(g => g.Width)
                .ThenByDescending(g => g.Height)
                .ToList();

            List<Atlas> atlases = new();
            
            while (remainingGroups.Any()) {
                int prevCount = remainingGroups.Count;
                
                var prevGroups = remainingGroups.ToArray();
                CalculateUv(
                    remainingGroups,
                    new Rectangle(
                        1,
                        1,
                        LightmapConfig.TextureDims-2,
                        LightmapConfig.TextureDims-2),
                    out _,
                    out _);

                if (prevCount == remainingGroups.Count) {
                    throw new Exception(
                        $"{prevCount} lightmap groups do not fit within the given resolution and downscale factor");
                }

                var newAtlas = new Atlas(prevGroups.Where(g => !remainingGroups.Contains(g)));
                atlases.Add(newAtlas);
            }

            return atlases;
        }
        
        

        private static void CalculateUv(
            List<LightmapGroup> lmGroups,
            Rectangle area,
            out int usedWidth,
            out int usedHeight
        ) {
            usedWidth = 0;
            usedHeight = 0;
            if (lmGroups.Count <= 0) { return; }

            for (int i = 0; i < lmGroups.Count; i++) {
                LightmapGroup lmGroup = lmGroups[i];

                //Make the aspect ratio of the group
                //closer to the aspect ratio of the
                //available area, since this gives
                //better odds of the group fitting
                if ((area.Width <= area.Height) != (lmGroup.Width <= lmGroup.Height)) {
                    lmGroup.SwapUv();
                }

                int downscaledWidth = 0;
                int downscaledHeight = 0;
                bool fits = false;
                
                for (int attempts = 0; attempts < 2; attempts++) {
                    downscaledWidth = (int)Math.Ceiling(lmGroup.Width / LightmapConfig.DownscaleFactor);
                    downscaledHeight = (int)Math.Ceiling(lmGroup.Height / LightmapConfig.DownscaleFactor);

                    if (downscaledWidth > area.Width || downscaledHeight > area.Height) {
                        //The group did not fit, try flipping the group
                        //because it might be able to fit that way
                        lmGroup.SwapUv();
                    } else {
                        fits = true;
                        break;
                    }
                }

                if (!fits) { return; } //The given group simply does not fit in the given area, give up

                usedWidth += downscaledWidth;
                usedHeight += downscaledHeight;
                lmGroups.RemoveAt(i); i--; //Remove the current group from the list of pending groups
                lmGroup.WriteU = area.Left;
                lmGroup.WriteV = area.Top;
                
                //There are now four regions that are considered to introduce more groups:
                //  XXXXXXXX | AAAAAAAA
                //  XXXXXXXX | AAAAAAAA
                //  XXXXXXXX | AAAAAAAA
                //  -------------------
                //  BBBBBBBB | CCCCCCCC
                //  BBBBBBBB | CCCCCCCC
                //  BBBBBBBB | CCCCCCCC
                //
                //Region X is completely taken up by the current group.
                //Regions A, B and C are extra space that should be filled,
                //their dimensions are based on the size of region X

                //Try to fill region A
                if (downscaledWidth < area.Width) {
                    int subWidth = -1;
                    usedWidth += LightmapConfig.PlaneMargin;
                    while (subWidth != 0) {
                        CalculateUv(lmGroups, new Rectangle(
                                area.Left + usedWidth,
                                area.Top,
                                area.Width - usedWidth,
                                downscaledHeight),
                            out subWidth, out _);
                        usedWidth += subWidth + LightmapConfig.PlaneMargin;
                    }
                }

                //Try to fill region B
                if (downscaledHeight < area.Height) {
                    int subHeight = -1;
                    usedHeight += LightmapConfig.PlaneMargin;
                    while (subHeight != 0) {
                        CalculateUv(lmGroups, new Rectangle(
                                area.Left,
                                area.Top + usedHeight,
                                downscaledWidth,
                                area.Height - usedHeight),
                            out _, out subHeight);
                        usedHeight += subHeight + LightmapConfig.PlaneMargin;
                    }
                }

                //Try to fill region C
                if (downscaledWidth < area.Width && downscaledHeight < area.Height) {
                    Rectangle remainder = new Rectangle(
                        area.Left + downscaledWidth + LightmapConfig.PlaneMargin,
                        area.Top + downscaledHeight + LightmapConfig.PlaneMargin,
                        area.Width - downscaledWidth - LightmapConfig.PlaneMargin,
                        area.Height - downscaledHeight - LightmapConfig.PlaneMargin);

                    CalculateUv(lmGroups, remainder,
                        out int subWidth, out int subHeight);

                    usedWidth += subWidth;
                    usedHeight += subHeight;
                }
            }
        }
    }
}
