using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/Swatch.xaml.cs.
/// WPF's MouseLeftButtonDown/MouseMove/MouseLeftButtonUp become pointer events, and mouse
/// capture goes through the pointer rather than CaptureMouse/ReleaseMouseCapture.
/// </summary>
public partial class Swatch : UserControl {

   private (double hue, double sat, double bright) hsb;

   #region Result

   public static readonly StyledProperty<string> ResultProperty =
      AvaloniaProperty.Register<Swatch, string>("Result", "#000000",
         defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

   public string Result {
      get => GetValue(ResultProperty);
      set => SetValue(ResultProperty, value);
   }

   public event EventHandler<string> ResultChanged;

   static Swatch() {
      ResultProperty.Changed.AddClassHandler<Swatch>((self, e) => self.OnResultPropertyChanged(e));
   }

   private void OnResultPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
      if (!CoreTheme.TryConvertColor(Result, out var rgb)) return;
      hsb = CoreTheme.ToHSB(rgb.r, rgb.g, rgb.b);
      UpdateSBPickerHue();
      UpdateSelections();
      ResultChanged?.Invoke(this, e.OldValue as string);
   }

   #endregion

   private readonly TranslateTransform hueSelector = new();
   private readonly TranslateTransform sbSelector = new();

   public Swatch() {
      InitializeComponent();
      HueSelectorEllipse.RenderTransform = hueSelector;
      SBSelectorEllipse.RenderTransform = sbSelector;
      var brush = new LinearGradientBrush {
         StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
         EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
      };
      for (int i = 0; i < 100; i++) {
         var (red, green, blue) = CoreTheme.FromHSB(i / 100.0, 1, 1);
         brush.GradientStops.Add(new GradientStop(Color.FromRgb(red, green, blue), i / 100.0));
      }
      HuePicker.Background = brush;

      HuePicker.PointerPressed += (s, e) => HuePickerDown(e);
      HuePicker.PointerMoved += (s, e) => HuePickerDown(e);
      HuePicker.PointerReleased += PickerUp;
      SBPicker.PointerPressed += (s, e) => SBPickerDown(e);
      SBPicker.PointerMoved += (s, e) => SBPickerDown(e);
      SBPicker.PointerReleased += PickerUp;

      SizeChanged += (s, e) => UpdateSelections();
   }


   private void UpdateSBPickerHue() {
      var (red, green, blue) = CoreTheme.FromHSB(hsb.hue, 1, 1);
      SBPickerContainer.Background = new SolidColorBrush(Color.FromRgb(red, green, blue));
   }

   private void SBPickerDown(PointerEventArgs e) {
      if (!e.GetCurrentPoint(SBPicker).Properties.IsLeftButtonPressed) return;
      e.Handled = true;
      e.Pointer.Capture(SBPicker);
      var hueBackup = hsb.hue;
      var position = e.GetPosition(SBPicker);
      var saturation = position.X / SBPicker.Bounds.Width;
      var brightness = 1 - position.Y / SBPicker.Bounds.Height;
      hsb.sat = saturation.LimitToRange(0, 1);
      hsb.bright = brightness.LimitToRange(0, 1);
      UpdateSelections();
      Result = CoreTheme.FromHSB(hsb.hue, hsb.sat, hsb.bright).ToHexString();
      hsb.hue = hueBackup;
      UpdateSBPickerHue();
      UpdateSelections();
   }

   private void HuePickerDown(PointerEventArgs e) {
      if (!e.GetCurrentPoint(HuePicker).Properties.IsLeftButtonPressed) return;
      e.Handled = true;
      e.Pointer.Capture(HuePicker);
      var position = e.GetPosition(HuePicker);
      var hue = position.Y / HuePicker.Bounds.Height;
      while (hue > 1) hue -= 1;
      while (hue < 0) hue += 1;
      hsb.hue = hue;
      UpdateSelections();
      Result = CoreTheme.FromHSB(hsb.hue, hsb.sat, hsb.bright).ToHexString();
      UpdateSBPickerHue();
   }

   private void UpdateSelections() {
      sbSelector.X = hsb.sat * SBPicker.Bounds.Width;
      sbSelector.Y = (1 - hsb.bright) * SBPicker.Bounds.Height;
      hueSelector.Y = hsb.hue * HuePicker.Bounds.Height;
   }

   private void PickerUp(object sender, PointerReleasedEventArgs e) => e.Pointer.Capture(null);
}
