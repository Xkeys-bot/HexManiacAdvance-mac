using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace HavenSoft.HexManiac.AvaloniaUI.Emulation;

/// <summary>
/// Draws the emulator's output.
///
/// Repaints are driven by the compositor through RequestAnimationFrame rather than by the
/// emulation thread, for the same reason PixelImage double-buffers: the render thread reads the
/// bitmap that is currently assigned, so a frame written from another thread while the compositor
/// is reading it tears. Here the emulation thread only ever fills a plain array; this control is
/// the only thing that touches a WriteableBitmap, always on the UI thread, and always into the
/// buffer that is not on screen.
///
/// Scaling is nearest-neighbour and aspect-correct: a GBA frame is 240x160 pixel art, and smoothing
/// it would defeat the purpose of looking at edited tiles.
/// </summary>
public sealed class EmulatorScreen : Control {
   private WriteableBitmap front, back;
   private uint[] pixels = Array.Empty<uint>();
   private int width, height;
   private long lastFrameSeen;
   private bool animationFrameQueued;

   private EmulatorSession session;
   public EmulatorSession Session {
      get => session;
      set {
         session = value;
         lastFrameSeen = 0;
         QueueAnimationFrame();
      }
   }

   /// When true the screen is drawn at an exact integer multiple of 240x160 (centred, letterboxed)
   /// so every GBA pixel is the same size. Off, it fills the panel keeping 3:2.
   public bool IntegerScaling { get; set; } = true;

   static EmulatorScreen() {
      AffectsRender<EmulatorScreen>();
   }

   public EmulatorScreen() {
      RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
      ClipToBounds = true;
      // Control is not focusable by default, so Focus() on it silently does nothing and the
      // keyboard stays wherever it was - on a toolbar button, which eats Enter and Space itself.
      Focusable = true;
   }

   protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e) {
      base.OnPointerPressed(e);
      Focus();
   }

   protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
      base.OnAttachedToVisualTree(e);
      QueueAnimationFrame();
   }

   private void QueueAnimationFrame() {
      if (animationFrameQueued) return;
      var top = TopLevel.GetTopLevel(this);
      if (top == null) return;
      animationFrameQueued = true;
      top.RequestAnimationFrame(_ => {
         animationFrameQueued = false;
         PumpFrame();
         if (this.GetVisualRoot() != null) QueueAnimationFrame();
      });
   }

   private unsafe void PumpFrame() {
      var current = session;
      if (current == null) return;
      if (!current.CopyLatestFrame(ref pixels, ref width, ref height, ref lastFrameSeen)) return;
      if (width <= 0 || height <= 0) return;

      if (back == null || back.PixelSize.Width != width || back.PixelSize.Height != height) {
         back?.Dispose();
         back = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
      }

      using (var frame = back.Lock()) {
         // The emulator's uint pixels are 0xAARRGGBB, which little-endian is already B,G,R,A -
         // the byte order Bgra8888 wants - so each row is a straight copy.
         fixed (uint* source = pixels) {
            for (int y = 0; y < height; y++) {
               Buffer.MemoryCopy(source + y * width, (byte*)frame.Address + y * frame.RowBytes, frame.RowBytes, width * 4);
            }
         }
      }

      (front, back) = (back, front);
      InvalidateVisual();
   }

   public override void Render(DrawingContext context) {
      var bounds = Bounds;
      context.FillRectangle(Brushes.Black, new Rect(bounds.Size));
      if (front == null) return;

      var source = new Rect(0, 0, front.PixelSize.Width, front.PixelSize.Height);
      context.DrawImage(front, source, DestinationRect(bounds.Size, front.PixelSize));
   }

   private Rect DestinationRect(Size available, PixelSize native) {
      if (available.Width <= 0 || available.Height <= 0) return default;

      double drawWidth, drawHeight;
      if (IntegerScaling) {
         var scale = Math.Min(available.Width / native.Width, available.Height / native.Height);
         var whole = Math.Max(1, Math.Floor(scale));
         drawWidth = native.Width * whole;
         drawHeight = native.Height * whole;
         // Below 1x there is no integer multiple that fits, so fall back to filling.
         if (drawWidth > available.Width || drawHeight > available.Height) {
            var fit = Math.Min(available.Width / native.Width, available.Height / native.Height);
            drawWidth = native.Width * fit;
            drawHeight = native.Height * fit;
         }
      } else {
         var fit = Math.Min(available.Width / native.Width, available.Height / native.Height);
         drawWidth = native.Width * fit;
         drawHeight = native.Height * fit;
      }

      return new Rect(
         Math.Floor((available.Width - drawWidth) / 2),
         Math.Floor((available.Height - drawHeight) / 2),
         drawWidth, drawHeight);
   }

   protected override Size MeasureOverride(Size availableSize) => new(240, 160);
}
