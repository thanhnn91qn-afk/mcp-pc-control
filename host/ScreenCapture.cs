using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace McpPcHost
{
    /// <summary>Chụp màn hình ảo / theo monitor — cùng ý tưởng ScreenCaptureEngine của HospitalPC Tray.</summary>
    public static class ScreenCapture
    {
        public static object ScreenInfo()
        {
            Rectangle virt = SystemInformation.VirtualScreen;
            Screen[] screens = Screen.AllScreens;
            var list = new object[screens.Length];
            for (int i = 0; i < screens.Length; i++)
            {
                Rectangle b = screens[i].Bounds;
                list[i] = new
                {
                    index = i,
                    primary = screens[i].Primary,
                    x = b.X,
                    y = b.Y,
                    width = b.Width,
                    height = b.Height
                };
            }
            return new
            {
                virtualScreen = new { x = virt.X, y = virt.Y, width = virt.Width, height = virt.Height },
                monitors = list,
                cursor = Cursor.Position
            };
        }

        /// <summary>
        /// Chụp JPEG. monitorIndex &lt; 0 = toàn màn hình ảo.
        /// maxWidth &gt; 0 thì thu nhỏ giữ tỉ lệ (tiết kiệm token cho AI).
        /// </summary>
        public static byte[] CaptureJpeg(int monitorIndex, int quality, int maxWidth)
        {
            Rectangle bounds = GetBounds(monitorIndex);
            using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                Bitmap toSave = bmp;
                Bitmap scaled = null;
                try
                {
                    if (maxWidth > 0 && bmp.Width > maxWidth)
                    {
                        int h = (int)Math.Round(bmp.Height * (maxWidth / (double)bmp.Width));
                        scaled = new Bitmap(maxWidth, Math.Max(1, h));
                        using (Graphics g2 = Graphics.FromImage(scaled))
                        {
                            g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g2.DrawImage(bmp, 0, 0, maxWidth, h);
                        }
                        toSave = scaled;
                    }

                    quality = Math.Max(10, Math.Min(95, quality <= 0 ? 60 : quality));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        SaveJpeg(toSave, ms, quality);
                        return ms.ToArray();
                    }
                }
                finally
                {
                    if (scaled != null) scaled.Dispose();
                }
            }
        }

        private static Rectangle GetBounds(int monitorIndex)
        {
            Screen[] screens = Screen.AllScreens;
            if (monitorIndex >= 0 && monitorIndex < screens.Length)
                return screens[monitorIndex].Bounds;
            return SystemInformation.VirtualScreen;
        }

        private static void SaveJpeg(Image img, Stream stream, int quality)
        {
            ImageCodecInfo jpeg = null;
            foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
            {
                if (c.FormatID == ImageFormat.Jpeg.Guid) { jpeg = c; break; }
            }
            if (jpeg == null)
            {
                img.Save(stream, ImageFormat.Jpeg);
                return;
            }
            using (EncoderParameters ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                img.Save(stream, jpeg, ep);
            }
        }
    }
}
