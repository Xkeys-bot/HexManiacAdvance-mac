using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace HavenSoft.HexManiac.AvaloniaUI.Implementations;

/// <summary>
/// HexManiac.Core hands out pixels as 16-bit 5r5g5b shorts (blue in the low bits), which WPF
/// could push straight into a WriteableBitmap with PixelFormats.Bgr555. Skia has no 15/16-bit
/// or indexed formats, so every image path in the Avalonia build expands to Bgra8888 here.
/// </summary>
public static class PixelBuffer {
   public static byte ScaleUp(int channel) => (byte)((channel * 255) / 31);

   public static void Write555(WriteableBitmap bitmap, short[] pixels, int width, int height) {
      using var frame = bitmap.Lock();
      var row = new byte[frame.RowBytes];
      var rowStart = frame.Address;
      for (int y = 0; y < height; y++) {
         for (int x = 0; x < width; x++) {
            var color = pixels[y * width + x];
            var i = x * 4;
            row[i + 0] = ScaleUp((color >> 0) & 0x1F);  // B
            row[i + 1] = ScaleUp((color >> 5) & 0x1F);  // G
            row[i + 2] = ScaleUp((color >> 10) & 0x1F); // R
            row[i + 3] = 0xFF;
         }
         Marshal.Copy(row, 0, rowStart, frame.RowBytes);
         rowStart += frame.RowBytes;
      }
   }

   /// <summary>Writes a straight Bgra8888 int buffer (used for opacity masks).</summary>
   public static void WriteBgra(WriteableBitmap bitmap, int[] pixels, int width, int height) {
      using var frame = bitmap.Lock();
      var row = new byte[frame.RowBytes];
      var rowStart = frame.Address;
      for (int y = 0; y < height; y++) {
         for (int x = 0; x < width; x++) {
            var color = pixels[y * width + x];
            var i = x * 4;
            row[i + 0] = (byte)(color & 0xFF);
            row[i + 1] = (byte)((color >> 8) & 0xFF);
            row[i + 2] = (byte)((color >> 16) & 0xFF);
            row[i + 3] = (byte)((color >> 24) & 0xFF);
         }
         Marshal.Copy(row, 0, rowStart, frame.RowBytes);
         rowStart += frame.RowBytes;
      }
   }

   public static WriteableBitmap EnsureSize(WriteableBitmap existing, int width, int height) {
      if (existing != null && existing.PixelSize.Width == width && existing.PixelSize.Height == height) return existing;
      return new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
   }
}
