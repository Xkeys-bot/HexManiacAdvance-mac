using System;
using System.Runtime.InteropServices;
using Avalonia.Media;

namespace HavenSoft.HexManiac.AvaloniaUI.MacPlatform;

/// <summary>
/// macOS replacement for HexManiac.WPF/Controls/Swatch.xaml.cs's DesktopColorPicker, the
/// eyedropper used by the palette editor (PaletteControl).
///
/// The WPF version is pure Win32: GetDC / CreateCompatibleBitmap / BitBlt through gdi32, plus
/// System.Windows.Forms for the cursor position. The CoreGraphics equivalents are a direct
/// translation:
///   * Control.MousePosition        -> CGEventCreate + CGEventGetLocation
///   * BitBlt from the desktop DC   -> CGDisplayCreateImageForRect
///
/// The captured CGImage is redrawn into a 1x1 RGBA bitmap context rather than read directly,
/// because the format CoreGraphics hands back varies (it is normally 32-bit little-endian BGRX,
/// and it is 2x2 for a one-point rect on a Retina display). Drawing into a context we describe
/// ourselves makes the byte order and the scaling both somebody else's problem.
///
/// Screen capture needs Screen Recording permission; macOS prompts the first time the process
/// asks. Until it is granted CGDisplayCreateImageForRect returns null and so does this, which
/// leaves the palette entry untouched rather than setting it to a wrong colour.
///
/// Verified against a known region of the app's own window: a 1-point rect comes back as a 2x2
/// CGImage on this display and the colour read matches the same pixel read from a 16x16 grab.
/// </summary>
public static class DesktopColorPicker {
   const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
   const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

   [StructLayout(LayoutKind.Sequential)]
   private struct CGPoint {
      public double X, Y;
      public CGPoint(double x, double y) { X = x; Y = y; }
   }

   [StructLayout(LayoutKind.Sequential)]
   private struct CGSize {
      public double Width, Height;
      public CGSize(double width, double height) { Width = width; Height = height; }
   }

   [StructLayout(LayoutKind.Sequential)]
   private struct CGRect {
      public CGPoint Origin;
      public CGSize Size;
      public CGRect(double x, double y, double width, double height) {
         Origin = new CGPoint(x, y);
         Size = new CGSize(width, height);
      }
   }

   // kCGImageAlphaNoneSkipLast | kCGBitmapByteOrder32Big: bytes land as R, G, B, unused.
   const uint RgbxBigEndian = 5 | (4u << 12);

   [DllImport(CoreGraphics)] private static extern IntPtr CGEventCreate(IntPtr source);
   [DllImport(CoreGraphics)] private static extern CGPoint CGEventGetLocation(IntPtr eventRef);
   [DllImport(CoreGraphics)] private static extern uint CGMainDisplayID();
   [DllImport(CoreGraphics)] private static extern IntPtr CGDisplayCreateImageForRect(uint display, CGRect rect);
   [DllImport(CoreGraphics)] private static extern void CGImageRelease(IntPtr image);
   [DllImport(CoreGraphics)] private static extern IntPtr CGColorSpaceCreateDeviceRGB();
   [DllImport(CoreGraphics)] private static extern void CGColorSpaceRelease(IntPtr space);
   [DllImport(CoreGraphics)] private static extern IntPtr CGBitmapContextCreate(
      IntPtr data, nuint width, nuint height, nuint bitsPerComponent, nuint bytesPerRow, IntPtr space, uint bitmapInfo);
   [DllImport(CoreGraphics)] private static extern void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);
   [DllImport(CoreGraphics)] private static extern void CGContextSetInterpolationQuality(IntPtr context, int quality);
   [DllImport(CoreGraphics)] private static extern void CGContextRelease(IntPtr context);
   [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr handle);

   /// <summary>Port of the WPF method of the same name: the colour under the mouse, right now.</summary>
   public static Color? GrabMousePixelColorFromScreen() {
      if (!TryGetCursorPosition(out var x, out var y)) return null;
      return CaptureDesktopPixel(x, y);
   }

   public static bool TryGetCursorPosition(out int x, out int y) {
      x = y = 0;
      try {
         var eventRef = CGEventCreate(IntPtr.Zero);
         if (eventRef == IntPtr.Zero) return false;
         try {
            var location = CGEventGetLocation(eventRef);
            x = (int)location.X;
            y = (int)location.Y;
            return true;
         } finally {
            CFRelease(eventRef);
         }
      } catch (DllNotFoundException) {
         return false;
      } catch (EntryPointNotFoundException) {
         return false;
      }
   }

   public static Color? CaptureDesktopPixel(int x, int y) {
      try {
         // A one-point rect comes back as a 2x2 image on a Retina display; asking for the
         // surrounding 1x1 point and scaling it down to one pixel handles both cases.
         var image = CGDisplayCreateImageForRect(CGMainDisplayID(), new CGRect(x, y, 1, 1));
         if (image == IntPtr.Zero) return null; // no Screen Recording permission, or off-screen
         try {
            return ReadSinglePixel(image);
         } finally {
            CGImageRelease(image);
         }
      } catch (DllNotFoundException) {
         return null;
      } catch (EntryPointNotFoundException) {
         return null;
      }
   }

   private static Color? ReadSinglePixel(IntPtr image) {
      var colorSpace = CGColorSpaceCreateDeviceRGB();
      if (colorSpace == IntPtr.Zero) return null;
      var buffer = Marshal.AllocHGlobal(4);
      try {
         Marshal.WriteInt32(buffer, 0);
         var context = CGBitmapContextCreate(buffer, 1, 1, 8, 4, colorSpace, RgbxBigEndian);
         if (context == IntPtr.Zero) return null;
         try {
            // kCGInterpolationNone: an eyedropper wants the exact device pixel, not an average
            // of the 2x2 Retina block, which is also what WPF's BitBlt gave.
            CGContextSetInterpolationQuality(context, 1);
            CGContextDrawImage(context, new CGRect(0, 0, 1, 1), image);
         } finally {
            CGContextRelease(context);
         }
         var pixel = new byte[4];
         Marshal.Copy(buffer, pixel, 0, 4);
         return Color.FromRgb(pixel[0], pixel[1], pixel[2]);
      } finally {
         Marshal.FreeHGlobal(buffer);
         CGColorSpaceRelease(colorSpace);
      }
   }
}
