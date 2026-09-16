using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.Core.ViewModels;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/ImageEditorView.xaml.cs.
///
/// WPF had separate MouseLeftButtonDown / MouseRightButtonDown / MouseDown routed events;
/// Avalonia raises one PointerPressed, so the button is read off the pointer's properties and
/// dispatched to the same three view model paths (tool / pan / eye-dropper).
/// The KeyBindings WPF declared in XAML are built here so Ctrl maps to Cmd on macOS.
/// </summary>
public partial class ImageEditorView : UserControl {
   private ImageEditorViewModel ViewModel => DataContext as ImageEditorViewModel;
   private bool isPointerCaptured;
   private MouseButton capturedButton;

   public ImageEditorView() {
      InitializeComponent();
      AttachedToVisualTree += (sender, e) => Focus();
      DetachedFromVisualTree += (sender, e) => ClearPopups();

      ImageContainer.PointerPressed += ImagePointerPressed;
      ImageContainer.PointerMoved += (sender, e) => ViewModel?.Hover(Point(e));
      ImageContainer.PointerReleased += ImagePointerReleased;
      ImageContainer.PointerWheelChanged += WheelMouse;

      BuildKeyBindings();
   }

   private void BuildKeyBindings() {
      var cmd = HexContent.CommandModifier;

      void Add(Key key, KeyModifiers modifiers, string path, object parameter = null) {
         var binding = new KeyBinding { Gesture = new KeyGesture(key, modifiers) };
         binding.Bind(KeyBinding.CommandProperty, new Avalonia.Data.Binding($"DataContext.{path}") { Source = this });
         if (parameter != null) binding.CommandParameter = parameter;
         KeyBindings.Add(binding);
      }

      Add(Key.OemPlus, KeyModifiers.None, nameof(ImageEditorViewModel.ZoomInCommand));
      Add(Key.OemMinus, KeyModifiers.None, nameof(ImageEditorViewModel.ZoomOutCommand));
      Add(Key.Delete, KeyModifiers.None, nameof(ImageEditorViewModel.DeleteCommand));

      Add(Key.A, KeyModifiers.None, nameof(ImageEditorViewModel.SelectTool), ImageEditorTools.Pan);
      Add(Key.S, KeyModifiers.None, nameof(ImageEditorViewModel.SelectTool), ImageEditorTools.Select);
      Add(Key.D, KeyModifiers.None, nameof(ImageEditorViewModel.SelectTool), ImageEditorTools.Draw);
      Add(Key.F, KeyModifiers.None, nameof(ImageEditorViewModel.SelectTool), ImageEditorTools.Fill);
      Add(Key.G, KeyModifiers.None, nameof(ImageEditorViewModel.SelectTool), ImageEditorTools.EyeDropper);
      Add(Key.H, KeyModifiers.None, nameof(ImageEditorViewModel.SelectTool), ImageEditorTools.TilePalette);

      Add(Key.C, cmd, nameof(ImageEditorViewModel.Copy), FileSystem);
      Add(Key.V, cmd, nameof(ImageEditorViewModel.Paste), FileSystem);
      Add(Key.A, cmd, nameof(ImageEditorViewModel.SelectAll));

      Add(Key.Space, KeyModifiers.None, nameof(ImageEditorViewModel.SelectColor), 0);
      for (int i = 0; i <= 7; i++) {
         Add(Key.D0 + i, KeyModifiers.None, nameof(ImageEditorViewModel.SelectColor), i);
      }
      Add(Key.D1, KeyModifiers.Shift, nameof(ImageEditorViewModel.SetCursorSize), 1);
      Add(Key.D2, KeyModifiers.Shift, nameof(ImageEditorViewModel.SetCursorSize), 2);
      Add(Key.D3, KeyModifiers.Shift, nameof(ImageEditorViewModel.SetCursorSize), 4);
      Add(Key.D4, KeyModifiers.Shift, nameof(ImageEditorViewModel.SetCursorSize), 8);
   }

   private Core.Models.IFileSystem FileSystem {
      get {
         var top = TopLevel.GetTopLevel(this);
         if (top != null && top.TryFindResource("FileSystem", out var resource)) return resource as Core.Models.IFileSystem;
         return null;
      }
   }

   private ModelPoint Point(PointerEventArgs e) {
      var p = e.GetPosition(ImageContainer);
      p = new Avalonia.Point(p.X - ImageContainer.Bounds.Width / 2, p.Y - ImageContainer.Bounds.Height / 2);
      return new ModelPoint((int)p.X, (int)p.Y);
   }

   private void ImagePointerPressed(object sender, PointerPressedEventArgs e) {
      if (ViewModel == null) return;
      var properties = e.GetCurrentPoint(ImageContainer).Properties;
      e.Pointer.Capture(ImageContainer);
      isPointerCaptured = true;

      if (properties.IsMiddleButtonPressed) {
         capturedButton = MouseButton.Middle;
         ViewModel.PanDown(Point(e));
      } else if (properties.IsRightButtonPressed) {
         capturedButton = MouseButton.Right;
         ViewModel.EyeDropperDown(Point(e));
      } else if (properties.IsLeftButtonPressed) {
         capturedButton = MouseButton.Left;
         PaletteControl.ClosePopup();
         ViewModel.ToolDown(Point(e), e.KeyModifiers == HexContent.CommandModifier);
         Focus();
      }
   }

   private void ImagePointerReleased(object sender, PointerReleasedEventArgs e) {
      if (!isPointerCaptured || ViewModel == null) return;
      var point = Point(e);
      if (capturedButton == MouseButton.Middle) ViewModel.PanUp(point);
      else if (capturedButton == MouseButton.Right) ViewModel.EyeDropperUp(point);
      else ViewModel.ToolUp(point);
      e.Pointer.Capture(null);
      isPointerCaptured = false;
   }

   private void WheelMouse(object sender, PointerWheelEventArgs e) {
      if (ViewModel == null) return;
      var delta = e.Delta.Y;
      if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) {
         if (delta > 0) ViewModel.SpritePage = (ViewModel.SpritePage + 1) % ViewModel.SpritePages;
         if (delta < 0) ViewModel.SpritePage = ViewModel.SpritePage == 0 ? ViewModel.SpritePages - 1 : ViewModel.SpritePage - 1;
      } else {
         if (delta > 0) ViewModel.ZoomIn(Point(e));
         if (delta < 0) ViewModel.ZoomOut(Point(e));
      }
      e.Handled = true;
   }

   private void ClearPopups() {
      PaletteControl.ClosePopup();
      PaletteControl.SingleSelect();
      PaletteMixer.ClosePopup();
      PaletteMixer.SingleSelect();
   }
}
