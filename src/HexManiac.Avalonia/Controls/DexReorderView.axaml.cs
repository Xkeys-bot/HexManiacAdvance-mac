using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/DexReorderView.xaml.cs.
/// Same drag-to-reorder shape as PaletteControl: WPF's per-tile DoubleAnimation becomes a
/// DoubleTransition on the tile's TranslateTransform, jumped then settled.
/// </summary>
public partial class DexReorderView : UserControl {
   private static readonly TimeSpan span = TimeSpan.FromMilliseconds(100);

   private DexReorderTab ViewModel => DataContext as DexReorderTab;
   private bool isPointerCaptured;

   public DexReorderView() {
      InitializeComponent();
      Container.PointerPressed += StartElementMove;
      Container.PointerMoved += ElementMove;
      Container.PointerReleased += EndElementMove;
      AddHandler(PointerWheelChangedEvent, ElementScroll, Avalonia.Interactivity.RoutingStrategies.Tunnel);
   }

   private int ExpectedElementWidth => (int)(64 * ViewModel.SpriteScale) + 2;
   private int ExpectedElementHeight => (int)(64 * ViewModel.SpriteScale) + 2;

   private int ToTile(Point p) {
      var tileWidth = (int)(Container.Bounds.Width / ExpectedElementWidth);
      if (tileWidth < 1) tileWidth = 1;
      var newTileX = (int)(p.X / ExpectedElementWidth);
      var newTileY = (int)(p.Y / ExpectedElementHeight);
      var tileIndex = newTileY * tileWidth + newTileX;
      return tileIndex.LimitToRange(0, ViewModel.Elements.Count);
   }

   private Point interactionPoint;

   private void StartElementMove(object sender, PointerPressedEventArgs e) {
      if (ViewModel == null) return;
      if (!e.GetCurrentPoint(Container).Properties.IsLeftButtonPressed) return;
      interactionPoint = e.GetPosition(Container);
      var tileIndex = ToTile(interactionPoint);

      if (e.KeyModifiers == KeyModifiers.Shift) {
         ViewModel.SelectionEnd = tileIndex;
      } else {
         ViewModel.SelectionStart = tileIndex;
      }

      e.Pointer.Capture(Container);
      isPointerCaptured = true;
   }

   private void ElementMove(object sender, PointerEventArgs e) {
      if (!isPointerCaptured || ViewModel == null) return;
      var oldTileIndex = ToTile(interactionPoint);

      interactionPoint = e.GetPosition(Container);
      var newTileIndex = ToTile(interactionPoint);

      var tilesToAnimate = ViewModel.HandleMove(oldTileIndex, newTileIndex);

      foreach (var (index, direction) in tilesToAnimate) {
         var image = VisualTreeExtensions.GetChild(Container, "PixelImage", ViewModel.Elements[index]);
         if (image == null) continue;
         if (image.RenderTransform is not TranslateTransform transform) {
            transform = new TranslateTransform {
               Transitions = new Transitions {
                  new DoubleTransition { Property = TranslateTransform.XProperty, Duration = span },
               },
            };
            image.RenderTransform = transform;
         }
         transform.X = ExpectedElementWidth * direction;
         Dispatcher.UIThread.Post(() => transform.X = 0, DispatcherPriority.Background);
      }
   }

   private void EndElementMove(object sender, PointerReleasedEventArgs e) {
      if (!isPointerCaptured) return;
      e.Pointer.Capture(null);
      isPointerCaptured = false;
      ViewModel?.CompleteCurrentInteraction();
   }

   private void ElementScroll(object sender, PointerWheelEventArgs e) {
      if (ViewModel == null) return;
      if (e.KeyModifiers != HexContent.CommandModifier) return;
      ViewModel.SpriteScale *= Math.Sign(e.Delta.Y) > 0 ? 2 : .5;
      e.Handled = true;
   }
}
