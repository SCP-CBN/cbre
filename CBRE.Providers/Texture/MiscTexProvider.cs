using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using CBRE.Common;
using CBRE.Graphics.Helpers;

namespace CBRE.Providers.Texture {
    public class MiscTexProvider : TextureProvider {
        static Dictionary<string, Tuple<Bitmap, TextureFlags>> loadedImages = new Dictionary<string, Tuple<Bitmap, TextureFlags>>();
        private static BitmapRef Parse(string file) {
            if (loadedImages.ContainsKey(file.ToLower())) {
                return new BitmapRef(loadedImages[file].Item1);
            }

            return null;
        }

        public override void LoadTextures(IEnumerable<TextureItem> items) {
            foreach (var item in items) {
                var bmp = Parse(item.Name);
                if (bmp == null) {
                    throw new Exception(item.Name);
                }
                TextureHelper.Create(item.Name, bmp.Bitmap, item.Width, item.Height, item.Flags);
            }
        }

        public override IEnumerable<TexturePackage> CreatePackages(IEnumerable<TextureCategory> sourceRoots) {
            // Sprite provider ignores the black/whitelists
            var dirs = sourceRoots.Where(sr => Directory.Exists(sr.Path));

            foreach (var dir in dirs) {
                var tp = new TexturePackage(dir.Path, dir.CategoryName, this);

                var sprs = Directory.GetFiles(dir.Path, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".jpg") || s.EndsWith(".jpeg") || s.EndsWith(".png"));
                if (!sprs.Any()) continue;

                foreach (var spr in sprs) {
                    var rel = Path.GetFullPath(spr).Substring(dir.Path.Length).TrimStart('/', '\\').Replace('\\', '/');
                    rel = rel.Replace(".jpg", "").Replace(".jpeg", "").Replace(".png", "").ToLower();

                    if (rel.Contains("_lm") || rel.EndsWith("bump") || rel.Contains("sky") || rel.EndsWith("_mask")) continue;

                    rel = dir.Prefix + rel;

                    if (!loadedImages.ContainsKey(rel)) {
                        Bitmap bmp = new Bitmap(spr);
                        TextureFlags flags = TextureFlags.None;
                        for (int x = 0; x < 4; x++) {
                            for (int y = 0; y < 4; y++) {
                                int mX = x * (bmp.Width - 1) / 3;
                                int mY = y * (bmp.Height - 1) / 3;
                                if (bmp.GetPixel(mX, mY).A != 255) {
                                    flags = TextureFlags.Transparent;
                                    break;
                                }
                            }
                            if (flags != TextureFlags.None) break;
                        }
                        loadedImages.Add(rel, new Tuple<Bitmap, TextureFlags>(bmp, flags));
                    }

                    tp.AddTexture(new TextureItem(tp, rel.ToLower(), loadedImages[rel].Item2, loadedImages[rel].Item1.Width, loadedImages[rel].Item1.Height));
                }
                if (tp.Items.Any()) yield return tp;
            }
        }

        public override void DeletePackages(IEnumerable<TexturePackage> packages) {

        }

        public override ITextureStreamSource GetStreamSource(int maxWidth, int maxHeight, IEnumerable<TexturePackage> packages) {
            return new MiscTextureStreamSource(packages);
        }

        private class MiscTextureStreamSource : ITextureStreamSource {
            private readonly List<TexturePackage> _packages;

            public MiscTextureStreamSource(IEnumerable<TexturePackage> packages) {
                _packages = packages.ToList();
            }

            public bool HasImage(TextureItem item) {
                return _packages.Any(x => x.Items.ContainsValue(item));
            }

            public BitmapRef GetImage(TextureItem item) {
                return Parse(item.Name);
            }

            public void Dispose() {
                _packages.Clear();
            }
        }
    }
}
