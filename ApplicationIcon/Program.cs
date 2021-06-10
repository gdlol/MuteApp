using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KGySoft.Drawing;

namespace ApplicationIcon
{
    class Program
    {
        static UIElement LayoutElement(UIElement element, int width, int height)
        {
            var viewbox = new Viewbox
            {
                Child = element
            };
            viewbox.Measure(new Size(width, height));
            viewbox.Arrange(new Rect(0, 0, width, height));
            viewbox.UpdateLayout();
            return viewbox;
        }

        static byte[] GetPixels(Func<UIElement> elementFactory, int width, int height)
        {
            var element = elementFactory();
            element = LayoutElement(element, width, height);
            var bitmap = new RenderTargetBitmap(width, height, 0, 0, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var pixels = new byte[4 * width * height];
            bitmap.CopyPixels(pixels, 4 * width, 0);
            return pixels;
        }

        static System.Drawing.Icon GetIcon(Func<UIElement> elementFactory, int width, int height)
        {
            var pixels = GetPixels(elementFactory, width, height);
            var bitmap = new System.Drawing.Bitmap(width, height);
            var data = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap.ToIcon();
        }

        static void SaveIcon(Func<UIElement> elementFactory, Stream stream)
        {
            var icons = new List<System.Drawing.Icon>();
            try
            {
                var sizeList = new[] { 10, 16, 24, 32, 40, 48, 128, 256 };
                foreach (int size in sizeList)
                {
                    var icon = GetIcon(elementFactory, size, size);
                    icons.Add(icon);
                }
                using var combinedIcon = Icons.Combine(icons);
                combinedIcon.Save(stream);
            }
            finally
            {
                foreach (var icon in icons)
                {
                    icon.Dispose();
                }
            }
        }

        static UIElement GetIconElement()
        {
            var fontFamily = new FontFamily("Segoe MDL2 Assets");

            var grid = new Grid
            {
                Width = 100,
                Height = 100
            };

            var background = new Grid
            {
                Width = 100,
                Height = 100,
                Background = Brushes.Gray,
                Opacity = 0.5
            };

            var gamepadBox = new Viewbox
            {
                Child = new TextBlock
                {
                    Text = "\xE767",
                    FontFamily = fontFamily,
                    Foreground = Brushes.White
                },
                Width = 100,
                Height = 100
            };

            var batteryBox = new Viewbox
            {
                Child = new TextBlock
                {
                    Text = "\xE74F",
                    FontFamily = fontFamily,
                    Foreground = Brushes.White
                },
                Width = 100,
                Height = 100
            };

            grid.Children.Add(background);
            grid.Children.Add(gamepadBox);
            grid.Children.Add(batteryBox);

            return grid;
        }

        static string GetFilePath([CallerFilePath] string path = null)
        {
            return path;
        }

        [STAThread]
        static void Main()
        {
            try
            {
                string filePath = GetFilePath();
                string projectPath = new FileInfo(filePath).Directory.Parent.FullName;
                string iconPath = Path.Combine(projectPath, "MuteApp", "icon.ico");
                using var file = File.OpenWrite(iconPath);
                SaveIcon(GetIconElement, file);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}
