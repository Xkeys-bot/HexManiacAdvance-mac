using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HavenSoft.HexManiac.AvaloniaUI.Implementations;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/TileImage.cs (TileImage, the shared converters,
/// PixelImage and GridDecorator).
/// </summary>
public class TileImage : Image {

   private TileViewModel ViewModel => (TileViewModel)DataContext;
   private INotifyPropertyChanged subscribed;
   private WriteableBitmap bitmap;

   public TileImage() {
      DataContextChanged += (sender, e) => UpdateDataContext();
      Stretch = Stretch.None;
      RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
   }

   private void UpdateDataContext() {
      if (subscribed != null) subscribed.PropertyChanged -= HandleDataContextPropertyChanged;
      subscribed = DataContext as INotifyPropertyChanged;
      if (subscribed != null) subscribed.PropertyChanged += HandleDataContextPropertyChanged;
      UpdateSource();
   }

   private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) => UpdateSource();

   public void UpdateSource() {
      if (DataContext is not TileViewModel) return;
      var bitsPerPixel = 4; // TODO
      var pixels = new byte[8 * bitsPerPixel];
      Array.Copy(ViewModel.DataStore, ViewModel.Start, pixels, 0, pixels.Length);
      var palette = ViewModel.Palette.Select(Convert16BitColor).ToList();

      // WPF handed the 4bpp buffer and a BitmapPalette to BitmapSource.Create with
      // PixelFormats.Indexed4. Skia has no indexed formats, so the nibbles are unpacked here.
      var expanded = new short[8 * 8];
      for (int y = 0; y < 8; y++) {
         for (int x = 0; x < 8; x++) {
            var b = pixels[y * 4 + x / 2];
            var index = x % 2 == 0 ? b & 0xF : b >> 4;
            expanded[y * 8 + x] = index < ViewModel.Palette.Count ? ViewModel.Palette[index] : (short)0;
         }
      }

      bitmap = PixelBuffer.EnsureSize(bitmap, 8, 8);
      PixelBuffer.Write555(bitmap, expanded, 8, 8);
      Source = bitmap;
      InvalidateVisual();
   }

   public static Color Convert16BitColor(short color) {
      byte b = (byte)((color >> 0) & 0b11111);
      byte g = (byte)((color >> 5) & 0b11111);
      byte r = (byte)((color >> 10) & 0b11111);

      return Color.FromArgb(255, ScaleUp(r), ScaleUp(g), ScaleUp(b));
   }

   public static byte ScaleUp(byte channel) => (byte)((channel * 255) / 31);

   public static short Convert16BitColor(Color color) {
      byte r = (byte)(color.R >> 3);
      byte g = (byte)(color.G >> 3);
      byte b = (byte)(color.B >> 3);

      return (short)((r << 10) | (g << 5) | b);
   }
}

public class PaletteColorConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var color = (short)value;
      var uiColor = TileImage.Convert16BitColor(color);
      return new SolidColorBrush(uiColor).ToImmutable();
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
   }
}

public class TilePaletteHueConverter : IValueConverter {
   private static readonly Color[] colors = new[] {
      Color.FromArgb(64,   0,   0,   0),
      Color.FromArgb(64, 255,   0,   0),
      Color.FromArgb(64, 255, 128,   0),
      Color.FromArgb(64, 255, 255,   0),

      Color.FromArgb(64, 128, 255,   0),
      Color.FromArgb(64,   0, 255,   0),
      Color.FromArgb(64,   0, 255, 128),
      Color.FromArgb(64,   0, 255, 255),

      Color.FromArgb(64,   0, 128, 255),
      Color.FromArgb(64,   0,   0, 255),
      Color.FromArgb(64, 128,   0, 255),
      Color.FromArgb(64, 255,   0, 255),

      Color.FromArgb(64, 255,   0, 128),
      Color.FromArgb(64,  85,  85,  85),
      Color.FromArgb(64, 170, 170, 170),
      Color.FromArgb(64, 255, 255, 255),
   };

   // WPF froze each brush; Avalonia's equivalent is ToImmutable().
   private static readonly IBrush[] brushes = colors.Select(c => (IBrush)new SolidColorBrush(c).ToImmutable()).ToArray();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var source = (int)value;
      return brushes[source];
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      throw new NotImplementedException();
   }
}

public class EqualityToBooleanConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      return Equals(value?.ToString(), parameter?.ToString());
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      if (parameter is bool b && b == false) return !(bool)value;
      throw new NotImplementedException();
   }
}

public class PointConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var p = (ImageLocation)value;
      return new Point(p.X, p.Y);
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      var p = (Point)value;
      return new ImageLocation((int)p.X, (int)p.Y);
   }
}

/// <summary>
/// Avalonia has no Visibility enum -- visibility is the boolean IsVisible -- so bindings that
/// used this converter bind IsVisible directly. It is kept because WPF's "Hidden" case (keep the
/// layout slot, hide the pixels) still needs expressing, which Avalonia does with Opacity.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is bool b) {
         if (targetType == typeof(double)) return b ? 1.0 : 0.0; // the Hidden case
         return b;
      }
      return value;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is bool b) return b;
      if (value is double d) return d > 0;
      return value;
   }
}

public class PixelImage : Image {
   private IPixelViewModel ViewModel => DataContext as IPixelViewModel;
   private WriteableBitmap maskSource;
   // Two buffers, alternated. Avalonia's compositor reads the bitmap that is currently assigned
   // to Source on the render thread, so writing new pixels into that same instance tears - which
   // is what the flickering was. Writing into the *other* one and then swapping means the
   // compositor is never reading the buffer being written.
   private WriteableBitmap bitmapFront, bitmapBack;
   private INotifyPropertyChanged subscribed;
   // UpdateSource is async for map bitmaps, so several calls can be in flight at once and finish
   // out of order. Only the newest is allowed to publish.
   private int updateGeneration;

   private bool showDebugGrid;
   public bool ShowDebugGrid {
      get => showDebugGrid;
      set {
         showDebugGrid = value;
         UpdateSource();
      }
   }

   #region UseTrueTransparency

   public static readonly StyledProperty<bool> UseTrueTransparencyProperty =
      AvaloniaProperty.Register<PixelImage, bool>(nameof(UseTrueTransparency), false);

   public bool UseTrueTransparency {
      get => GetValue(UseTrueTransparencyProperty);
      set => SetValue(UseTrueTransparencyProperty, value);
   }

   #endregion

   #region ScaleAffectsLayout

   /// <summary>
   /// WPF applied SpriteScale as a LayoutTransform, and the image editor opted out of it with
   /// LayoutTransform="{x:Null}" so it could scale with a RenderTransform instead. Avalonia has
   /// no LayoutTransform on arbitrary controls, so the scale is baked into Width/Height here -
   /// and this is the opt-out. Without it the image editor scales twice: once in layout and
   /// again through its own RenderTransform.
   /// </summary>
   public static readonly StyledProperty<bool> ScaleAffectsLayoutProperty =
      AvaloniaProperty.Register<PixelImage, bool>(nameof(ScaleAffectsLayout), true);

   public bool ScaleAffectsLayout {
      get => GetValue(ScaleAffectsLayoutProperty);
      set => SetValue(ScaleAffectsLayoutProperty, value);
   }

   #endregion

   #region TransparentBrush

   public static readonly StyledProperty<IBrush> TransparentBrushProperty =
      AvaloniaProperty.Register<PixelImage, IBrush>(nameof(TransparentBrush), Brushes.Transparent);

   public IBrush TransparentBrush {
      get => GetValue(TransparentBrushProperty);
      set => SetValue(TransparentBrushProperty, value);
   }

   #endregion

   static PixelImage() {
      UseTrueTransparencyProperty.Changed.AddClassHandler<PixelImage>((self, e) => self.UpdateSource());
      ScaleAffectsLayoutProperty.Changed.AddClassHandler<PixelImage>((self, e) => self.UpdateSource());
      TransparentBrushProperty.Changed.AddClassHandler<PixelImage>((self, e) => self.UpdateSource());
   }

   public PixelImage() {
      DataContextChanged += (sender, e) => UpdateDataContext();
      UseLayoutRounding = true; // WPF: SnapsToDevicePixels
      Stretch = Stretch.Fill;
      RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
      // WPF bound a ScaleTransform to LayoutTransform. Avalonia has no LayoutTransform on
      // arbitrary controls, so SpriteScale is applied to the laid-out size instead, which
      // affects layout the same way.
   }

   #region DataContext Changed

   private void UpdateDataContext() {
      if (subscribed != null) subscribed.PropertyChanged -= HandleDataContextPropertyChanged;
      subscribed = DataContext as INotifyPropertyChanged;
      if (subscribed != null) subscribed.PropertyChanged += HandleDataContextPropertyChanged;
      UpdateSource();
   }

   private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (!e.PropertyName.IsAny(
         nameof(ViewModel.PixelWidth),
         nameof(ViewModel.PixelHeight),
         nameof(ViewModel.PixelData),
         nameof(ViewModel.SpriteScale)
      )) return;
      UpdateSource();
   }

   #endregion

   private static readonly List<WriteableBitmap> wbCache = new();
   private static readonly List<IPixelViewModel> pvmCache = new();

   public static WriteableBitmap WriteOnce(IPixelViewModel viewModel) {
      var cacheIndex = pvmCache.IndexOf(viewModel);
      if (cacheIndex >= 0) return wbCache[cacheIndex];

      var pixels = viewModel.PixelData;
      if (pixels == null) return null;
      var expectedLength = viewModel.PixelWidth * viewModel.PixelHeight;
      if (pixels.Length < expectedLength || pixels.Length == 0) return null;

      var source = PixelBuffer.EnsureSize(null, viewModel.PixelWidth, viewModel.PixelHeight);
      PixelBuffer.Write555(source, pixels, viewModel.PixelWidth, viewModel.PixelHeight);

      if (wbCache.Count == 20) { wbCache.RemoveAt(0); pvmCache.RemoveAt(0); }
      wbCache.Add(source);
      pvmCache.Add(viewModel);

      return source;
   }

   public async void UpdateSource() {
      var vm = ViewModel;
      if (vm == null || vm.PixelWidth < 0 || vm.PixelHeight < 0) return;
      var generation = ++updateGeneration;
      short[] pixels;
      if (vm is not BlockMapViewModel) {
         pixels = vm.PixelData;
      } else {
         pixels = await Task.Run(() => vm.PixelData);
      }
      // A newer update started while this one was off building its pixels: that one wins.
      if (generation != updateGeneration) return;
      if (pixels == null || ViewModel == null) return;
      if (!UseTrueTransparency) {
         pixels = ConvertTransparentPixels(pixels);
         ClearOwnOpacityMask();
      } else if (ViewModel.Transparent != -1) {
         OpacityMask = CreateOpacityMask();
      } else {
         ClearOwnOpacityMask();
      }
      var expectedLength = ViewModel.PixelWidth * ViewModel.PixelHeight;
      if (pixels.Length < expectedLength || pixels.Length == 0) { Source = null; return; }
      if (ShowDebugGrid) { pixels = pixels.ToArray(); DrawDebugGrid(pixels, ViewModel.PixelWidth); }

      // Write into the back buffer, then swap it in. See the field comment above.
      bitmapBack = PixelBuffer.EnsureSize(bitmapBack, ViewModel.PixelWidth, ViewModel.PixelHeight);
      PixelBuffer.Write555(bitmapBack, pixels, ViewModel.PixelWidth, ViewModel.PixelHeight);
      (bitmapFront, bitmapBack) = (bitmapBack, bitmapFront);
      Source = bitmapFront;

      // Stands in for WPF's LayoutTransform ScaleTransform bound to SpriteScale. Only assign when
      // it actually changes: an unconditional assignment re-runs layout on every pixel update,
      // which on the map editor (where these refresh constantly) is a second source of flicker.
      var layoutScale = ScaleAffectsLayout ? ViewModel.SpriteScale : 1;
      var width = ViewModel.PixelWidth * layoutScale;
      var height = ViewModel.PixelHeight * layoutScale;
      if (Width != width) Width = width;
      if (Height != height) Height = height;
   }

   /// <summary>
   /// Only clears a mask this control created. WPF checked `OpacityMask is VisualBrush` for the
   /// same reason: the map template puts its own RadialGradientBrush here for the hold-space
   /// reveal, and clearing that unconditionally both broke the effect and raised a property
   /// change - and so a re-render - on every pixel update.
   /// </summary>
   private void ClearOwnOpacityMask() {
      if (OpacityMask is ImageBrush) OpacityMask = null;
   }

   private IBrush CreateOpacityMask() {
      if (ViewModel.Transparent == -1) return null;
      maskSource = PixelBuffer.EnsureSize(maskSource, ViewModel.PixelWidth, ViewModel.PixelHeight);
      var pixels = new int[ViewModel.PixelWidth * ViewModel.PixelHeight];
      for (int y = 0; y < ViewModel.PixelHeight; y++) {
         for (int x = 0; x < ViewModel.PixelWidth; x++) {
            var i = y * ViewModel.PixelWidth + x;
            pixels[i] = ViewModel.PixelData[i] == ViewModel.Transparent ? 0 : -1;
         }
      }
      PixelBuffer.WriteBgra(maskSource, pixels, ViewModel.PixelWidth, ViewModel.PixelHeight);
      // WPF wrapped the mask bitmap in a VisualBrush over an Image; Avalonia masks with a brush.
      return new ImageBrush(maskSource) { Stretch = Stretch.Fill };
   }

   private short[] ConvertTransparentPixels(short[] pixels) {
      if (ViewModel.Transparent == -1) return pixels;
      if (TransparentBrush is not ISolidColorBrush colorBrush) return pixels;
      pixels = pixels.ToArray();
      // Channel order copied verbatim from the WPF build, including its B/R swap.
      short newColor = (short)((colorBrush.Color.B >> 3) << 10);
      newColor += (short)((colorBrush.Color.G >> 3) << 5);
      newColor += (short)(colorBrush.Color.R >> 3);
      for (int i = 0; i < pixels.Length; i++) {
         if (pixels[i] == ViewModel.Transparent) pixels[i] = newColor;
      }
      return pixels;
   }

   private void DrawDebugGrid(short[] pixels, int width) {
      for (int y = 0; y < pixels.Length; y += width * 8) {
         for (int x = 0; x < width; x += 2) pixels[y + x] = 0b_10000_10000_10000;
      }
      for (int x = 0; x < width; x += 8) {
         for (int y = 0; y < pixels.Length; y += width * 2) pixels[y + x] = 0b_10000_10000_10000;
      }
   }
}

public class GridDecorator : Decorator {
   public override void Render(DrawingContext surface) {
      base.Render(surface);
      if (DataContext is not IPixelViewModel vm) return;
      if (vm.SpriteScale < 4) return;
      var pen = new Pen(Brush(nameof(CoreTheme.Secondary)), 1);
      for (double x = 0; x < Bounds.Width; x += 8 * vm.SpriteScale) {
         surface.DrawLine(pen, new(x, 0), new(x, Bounds.Height));
      }
      for (double y = 0; y < Bounds.Height; y += 8 * vm.SpriteScale) {
         surface.DrawLine(pen, new(0, y), new(Bounds.Width, y));
      }
   }

   private readonly Dictionary<string, IBrush> cachedBrushes = new();
   private IBrush Brush(string name) {
      if (name == null) return null;
      if (cachedBrushes.TryGetValue(name, out var brush)) return brush;
      cachedBrushes[name] = ThemeDictionary.Brush(name);
      return cachedBrushes[name];
   }
}
