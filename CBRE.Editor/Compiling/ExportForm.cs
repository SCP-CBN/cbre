using CBRE.Editor.Documents;
using CBRE.Settings;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CBRE.Editor.Compiling {
    public partial class ExportForm : Form {
        public Document Document;
        public static Form exportForm;

        public ExportForm() {
            InitializeComponent();
            exportForm = this;
        }

        private void label6_Click(object sender, EventArgs e) {

        }

        private void textureDims_LostFocus(object sender, EventArgs e) {
            int dims = -1; int.TryParse(((TextBox)sender).Text, out dims);
            if (dims >= 512 && dims <= 4096) {
                LightmapConfig.TextureDims = dims;
            } else {
                ((TextBox)sender).Text = LightmapConfig.TextureDims.ToString();
            }
        }

        private void downscaleFactor_LostFocus(object sender, EventArgs e) {
            float factor = -1; float.TryParse(((TextBox)sender).Text, out factor);
            if (factor >= 1 && factor <= 128) {
                LightmapConfig.DownscaleFactor = factor;
            } else {
                ((TextBox)sender).Text = LightmapConfig.DownscaleFactor.ToString();
            }
        }

        private void blurRadius_LostFocus(object sender, EventArgs e) {
            int radius = -1; int.TryParse(((TextBox)sender).Text, out radius);
            if (radius >= 0 && radius <= 150) {
                LightmapConfig.BlurRadius = radius;
            } else {
                ((TextBox)sender).Text = LightmapConfig.BlurRadius.ToString();
            }
        }

        private string SaveFileName = "";
        private void render_Click(object sender, EventArgs e) {
            ProgressLog.Text = "Rendering lightmap";
            ProgressBar.Enabled = true;

            actionThread = new Thread(() => { PerformAction(false); }) { CurrentCulture = CultureInfo.InvariantCulture };
            actionThread.Start();
        }

        private void export_Click(object sender, EventArgs e) {
            using (SaveFileDialog save = new SaveFileDialog()) {
                var filter = "";
#if RM2
                filter += "SCP-CB v1.4 RM2 (*.rm2)|*.rm2|";
#endif
                filter += "SCP-CB v1.3.11 RMesh (*.rmesh)|*.rmesh";
                filter += "|SCP-087-B (*.b)|*.b";
                filter += "|Autodesk Filmbox (*.fbx)|*.fbx";
                filter += "|Wavefront Object (*.obj)|*.obj";
                filter += "|Stereolithography (*.stl)|*.stl";
                filter += "|Stanford Polygon Library (*.ply)|*.ply";
                save.Filter = filter;
                if (save.ShowDialog() == DialogResult.OK) {
                    SaveFileName = save.FileName;

                    ProgressLog.Text = "Exporting to " + save.FileName;
                    ProgressBar.Enabled = true;

                    actionThread = new Thread(() => { PerformAction(true); }) { CurrentCulture = CultureInfo.InvariantCulture };
                    actionThread.Start();
                }
            }
        }

        private void ambientRed_LostFocus(object sender, EventArgs e) {
            int r = -1; int.TryParse(((TextBox)sender).Text, out r);
            if (r >= 0 && r <= 255) {
                LightmapConfig.AmbientColorR = r;
                ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
            } else {
                ((TextBox)sender).Text = LightmapConfig.AmbientColorR.ToString();
            }
        }

        private void ambientGreen_LostFocus(object sender, EventArgs e) {
            int g = -1; int.TryParse(((TextBox)sender).Text, out g);
            if (g >= 0 && g <= 255) {
                LightmapConfig.AmbientColorG = g;
                ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
            } else {
                ((TextBox)sender).Text = LightmapConfig.AmbientColorG.ToString();
            }
        }

        private void ambientBlue_LostFocus(object sender, EventArgs e) {
            int b = -1; int.TryParse(((TextBox)sender).Text, out b);
            if (b >= 0 && b <= 255) {
                LightmapConfig.AmbientColorB = b;
                ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
            } else {
                ((TextBox)sender).Text = LightmapConfig.AmbientColorB.ToString();
            }
        }

        private void ambientRed_TextChanged(object sender, EventArgs e) {
            int r = -1; int.TryParse(((TextBox)sender).Text, out r);
            if (r >= 0 && r <= 255) {
                LightmapConfig.AmbientColorR = r;
                ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
            }
        }

        private void ambientGreen_TextChanged(object sender, EventArgs e) {
            int g = -1; int.TryParse(((TextBox)sender).Text, out g);
            if (g >= 0 && g <= 255) {
                LightmapConfig.AmbientColorG = g;
                ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
            }
        }

        private void ambientBlue_TextChanged(object sender, EventArgs e) {
            int b = -1; int.TryParse(((TextBox)sender).Text, out b);
            if (b >= 0 && b <= 255) {
                LightmapConfig.AmbientColorB = b;
                ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
            }
        }

        private void ambientColorBox_Click(object sender, EventArgs e) {
            if (ambientRed.Enabled) {
                using (var cb = new System.Windows.Forms.ColorDialog()) {
                    if (cb.ShowDialog() == DialogResult.OK) {
                        LightmapConfig.AmbientColorR = cb.Color.R;
                        LightmapConfig.AmbientColorG = cb.Color.G;
                        LightmapConfig.AmbientColorB = cb.Color.B;
                        ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);
                        ambientRed.Text = LightmapConfig.AmbientColorR.ToString();
                        ambientGreen.Text = LightmapConfig.AmbientColorG.ToString();
                        ambientBlue.Text = LightmapConfig.AmbientColorB.ToString();
                    }
                }
            }
        }

        private void SetCancelEnabled(bool enabled) {
            Invoke((MethodInvoker)(() => {
                textureDims.Enabled = !enabled;
                downscaleFactor.Enabled = !enabled;
                blurRadius.Enabled = !enabled;

                ambientRed.Enabled = !enabled;
                ambientGreen.Enabled = !enabled;
                ambientBlue.Enabled = !enabled;

                render.Enabled = !enabled;
                export.Enabled = !enabled;
                cancel.Enabled = enabled;

                ProgressBar.Enabled = enabled;
            }));
        }

        Thread actionThread = null;
        private void PerformAction(bool export) {
            try {
                SetCancelEnabled(true);
                if (export) {
                    string extension = System.IO.Path.GetExtension(SaveFileName);
                    if (extension.Equals(".rm2", StringComparison.OrdinalIgnoreCase)) {
                        RM2Export.SaveToFile(SaveFileName, Document, this);
                    } else if (extension.Equals(".rmesh", StringComparison.OrdinalIgnoreCase)) {
                        RMeshExport.SaveToFile(SaveFileName, Document, this);
                    } else if (extension.Equals(".b", StringComparison.OrdinalIgnoreCase)) {
                        BExport.SaveToFile(SaveFileName, Document, this);
                    } else if (extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".obj", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".dae", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".stl", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".ply", StringComparison.OrdinalIgnoreCase)) {
                        GenericExport.SaveToFile(SaveFileName, Document, this, extension.Substring(1));
                    } else {
                        throw new Exception($"Unknown file extension ({extension})");
                    }
                } else {
                    Lightmap.Lightmapper.Render(Document, this, out _, out _);
                }
            } catch (ThreadAbortException) {
                foreach (Thread thread in (Lightmap.Lightmapper.FaceRenderThreads ?? Enumerable.Empty<Thread>())) {
                    if (thread.IsAlive) {
                        thread.Abort();
                    }
                }

                ProgressLog.Invoke((MethodInvoker)(() => ProgressLog.AppendText("\nCancelled by the user")));
                ProgressBar.Invoke((MethodInvoker)(() => ProgressBar.Value = 0));
            } catch (Exception e) {
                ProgressLog.Invoke((MethodInvoker)(() => {
                    ProgressLog.SelectionStart = ProgressLog.TextLength;
                    ProgressLog.SelectionLength = 0;
                    ProgressLog.SelectionColor = Color.Red;
                    ProgressLog.AppendText("\nError: " + e.Message + "\n" + e.StackTrace);
                    ProgressLog.SelectionColor = ProgressLog.ForeColor;
                }));
                ProgressBar.Invoke((MethodInvoker)(() => ProgressBar.Value = 0));
            } finally {
                SetCancelEnabled(false);
            }
        }

        private void formClosing(object sender, FormClosingEventArgs args) {
            if (actionThread != null && actionThread.IsAlive) {
                args.Cancel = true;
            }
        }

        private void cancel_Click(object sender, EventArgs e) {
            if (actionThread != null && actionThread.IsAlive) {
                actionThread.Abort();
            }
        }

        private void ExportForm_Load(object sender, EventArgs e) {
            ambientRed.Text = LightmapConfig.AmbientColorR.ToString();
            ambientGreen.Text = LightmapConfig.AmbientColorG.ToString();
            ambientBlue.Text = LightmapConfig.AmbientColorB.ToString();

            ambientColorBox.BackColor = Color.FromArgb(LightmapConfig.AmbientColorR, LightmapConfig.AmbientColorG, LightmapConfig.AmbientColorB);

            textureDims.Text = LightmapConfig.TextureDims.ToString();

            downscaleFactor.Text = LightmapConfig.DownscaleFactor.ToString();

            blurRadius.Text = LightmapConfig.BlurRadius.ToString();
        }
    }
}
