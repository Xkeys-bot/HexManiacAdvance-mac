using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>Ported from HexManiac.WPF/Controls/SelectedBlockEditor.xaml.cs.</summary>
public partial class SelectedBlockEditor : UserControl {
   private BlockEditor ViewModel => DataContext as BlockEditor;

   public SelectedBlockEditor() {
      InitializeComponent();

      foreach (var tile in new[] {
         LeftTopBack, LeftTopFront, RightTopBack, RightTopFront,
         LeftBottomBack, LeftBottomFront, RightBottomBack, RightBottomFront }) {
         tile.PointerEntered += MouseEnterTile;
         tile.PointerPressed += TilePointerPressed;
      }
      TileCanvas.PointerExited += (sender, e) => ViewModel?.ExitTiles();
      TileCanvas.PointerWheelChanged += (sender, e) => e.Handled = true; // WPF: WheelOverImage
   }

   private void MouseEnterTile(object sender, PointerEventArgs e) {
      if (sender is not Control element || ViewModel == null) return;
      ViewModel.EnterTile((IPixelViewModel)element.DataContext);
      e.Handled = true;
   }

   /// WPF split these across MouseLeftButtonDown / MouseRightButtonDown.
   private void TilePointerPressed(object sender, PointerPressedEventArgs e) {
      if (sender is not Control element || ViewModel == null) return;
      var properties = e.GetCurrentPoint(element).Properties;
      if (properties.IsRightButtonPressed) {
         ViewModel.GetSelectionFromTile((IPixelViewModel)element.DataContext);
      } else if (properties.IsLeftButtonPressed) {
         ViewModel.DrawOnTile((IPixelViewModel)element.DataContext);
      }
      e.Handled = true;
   }
}
