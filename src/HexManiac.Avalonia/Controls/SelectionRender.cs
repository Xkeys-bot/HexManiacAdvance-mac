using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/SelectionRender.cs.
///
/// The marching-ants outline is still built as an 8-bit index buffer exactly as WPF built it,
/// but Avalonia's WriteableBitmap has no indexed pixel format (WPF used Indexed8 plus a
/// three-entry BitmapPalette), so the indices are expanded to Bgra8888 on the way out.
/// </summary>
public class SelectionRender : Image {
   private ImageEditorViewModel ViewModel => DataContext as ImageEditorViewModel;
   private ImageEditorViewModel subscribed;

   public SelectionRender() {
      DataContextChanged += (sender, e) => UpdateDataContext();
      Stretch = Stretch.None;
      RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
   }

   /// WPF got old/new from DependencyPropertyChangedEventArgs; Avalonia's DataContextChanged
   /// carries no values, so the previous view model is tracked here.
   private void UpdateDataContext() {
      if (subscribed != null) {
         subscribed.PropertyChanged -= HandleDataContextPropertyChanged;
         subscribed.RefreshSelection -= HandleRefreshSelection;
      }
      subscribed = ViewModel;
      if (subscribed != null) {
         subscribed.PropertyChanged += HandleDataContextPropertyChanged;
         subscribed.RefreshSelection += HandleRefreshSelection;
      }
      UpdateSource();
   }

   private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (!e.PropertyName.IsAny(
         nameof(ViewModel.SpriteScale)
      )) {
         return;
      }
      UpdateSource();
   }

   private void HandleRefreshSelection(object sender, EventArgs e) => UpdateSource();

   public void UpdateSource() {
      if (ViewModel == null) return;
      var desiredWidth = ViewModel.PixelWidth * (int)ViewModel.SpriteScale + 2;
      var desiredHeight = ViewModel.PixelHeight * (int)ViewModel.SpriteScale + 2;

      int stride = desiredWidth + 2;
      var pixels = new byte[stride * desiredHeight];
      Width = desiredWidth;
      Height = desiredHeight;
      FillSelection(pixels, stride);

      if (Source is not WriteableBitmap wSource ||
         wSource.PixelSize.Width != desiredWidth ||
         wSource.PixelSize.Height != desiredHeight) {
         Source = new WriteableBitmap(
            new PixelSize(desiredWidth, desiredHeight), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Unpremul);
      }

      WritePixels((WriteableBitmap)Source, pixels, stride, desiredWidth, desiredHeight);
   }

   /// The palette WPF passed to the Indexed8 WriteableBitmap: transparent, black, white.
   static readonly uint[] Palette = { 0x00000000u, 0xFF000000u, 0xFFFFFFFFu };

   static void WritePixels(WriteableBitmap bitmap, byte[] indexed, int stride, int width, int height) {
      using var frame = bitmap.Lock();
      var row = new byte[frame.RowBytes];
      var rowStart = frame.Address;
      for (int y = 0; y < height; y++) {
         for (int x = 0; x < width; x++) {
            var index = indexed[y * stride + x];
            var color = index < Palette.Length ? Palette[index] : 0u;
            var i = x * 4;
            row[i + 0] = (byte)(color & 0xFF);         // B
            row[i + 1] = (byte)((color >> 8) & 0xFF);  // G
            row[i + 2] = (byte)((color >> 16) & 0xFF); // R
            row[i + 3] = (byte)((color >> 24) & 0xFF); // A
         }
         Marshal.Copy(row, 0, rowStart, frame.RowBytes);
         rowStart += frame.RowBytes;
      }
   }

   private const byte BLACK = 1;
   private const byte WHITE = 2;
   private void FillSelection(byte[] pixels, int stride) {
      byte currentEdgeColor;
      var zoom = (int)ViewModel.SpriteScale;
      void Line(int start, int next) {
         for (int i = 0; i < zoom; i++) {
            pixels[start] = currentEdgeColor;
            start += next;
         }
      }

      for (int x = 0; x < ViewModel.PixelWidth; x++) {
         for (int y = 0; y < ViewModel.PixelHeight; y++) {
            if (!ViewModel.ShowSelectionRect(x, y)) continue;
            var pixelColor = ViewModel.PixelData[y * ViewModel.PixelWidth + x];
            var grayScale = (pixelColor >> 10) + ((pixelColor >> 5) & 31) + (pixelColor & 31);
            currentEdgeColor = grayScale > 46 ? BLACK : WHITE;

            // each diagonal maps to a single pixel being placed
            if (!ViewModel.ShowSelectionRect(x - 1, y - 1)) pixels[(y * stride + x) * zoom] = currentEdgeColor;
            if (!ViewModel.ShowSelectionRect(x - 1, y + 1)) pixels[((y + 1) * zoom + 1) * stride + x * zoom] = currentEdgeColor;
            if (!ViewModel.ShowSelectionRect(x + 1, y - 1)) pixels[(y * stride + x + 1) * zoom + 1] = currentEdgeColor;
            if (!ViewModel.ShowSelectionRect(x + 1, y + 1)) pixels[((y + 1) * zoom + 1) * stride + (x + 1) * zoom + 1] = currentEdgeColor;

            // each edge maps to a line being placed
            if (!ViewModel.ShowSelectionRect(x - 1, y)) {
               Line((y * zoom + 1) * stride + x * zoom, stride);
            }
            if (!ViewModel.ShowSelectionRect(x + 1, y)) {
               Line((y * zoom + 1) * stride + (x + 1) * zoom + 1, stride);
            }
            if (!ViewModel.ShowSelectionRect(x, y - 1)) {
               Line(y * zoom * stride + x * zoom + 1, 1);
            }
            if (!ViewModel.ShowSelectionRect(x, y + 1)) {
               Line(((y + 1) * zoom + 1) * stride + x * zoom + 1, 1);
            }
         }
      }
   }
}
