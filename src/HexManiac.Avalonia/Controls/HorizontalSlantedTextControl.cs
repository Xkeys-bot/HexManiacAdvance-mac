using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
// Control.Theme (Avalonia) shadows Core's Theme class inside a Control subclass.
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// The slanted column headers above the hex view.
/// Ported from HexManiac.WPF/Controls/HorizontalSlantedTextControl.cs.
///
/// WPF's OnRender(DrawingContext) becomes Avalonia's Render(DrawingContext); PushTransform/Pop
/// pairs become using-scoped pushes; and theme brushes come from ThemeDictionary rather than
/// Application.Current.Resources.MergedDictionaries[0].
/// </summary>
public class HorizontalSlantedTextControl : Control {

   #region HeaderRows

   public ObservableCollection<ColumnHeaderRow> HeaderRows {
      get => GetValue(HeaderRowsProperty);
      set => SetValue(HeaderRowsProperty, value);
   }

   public static readonly StyledProperty<ObservableCollection<ColumnHeaderRow>> HeaderRowsProperty =
      AvaloniaProperty.Register<HorizontalSlantedTextControl, ObservableCollection<ColumnHeaderRow>>(nameof(HeaderRows));

   private void OnHeaderRowsChanged(AvaloniaPropertyChangedEventArgs e) {
      var oldCollection = e.OldValue as ObservableCollection<ColumnHeaderRow>;
      if (oldCollection != null) {
         oldCollection.CollectionChanged -= HeaderRowsCollectionChanged;
      }
      var newCollection = e.NewValue as ObservableCollection<ColumnHeaderRow>;
      if (newCollection != null) {
         newCollection.CollectionChanged += HeaderRowsCollectionChanged;
         foreach (var element in newCollection) {
            foreach (var header in element.ColumnHeaders) header.PropertyChanged += (s1, e1) => InvalidateVisual();
         }
      }
      UpdateDesiredHeight();
      InvalidateVisual();
   }

   private void HeaderRowsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
      UpdateDesiredHeight();
      InvalidateVisual();
      if (e.NewItems != null) {
         foreach (var item in e.NewItems) {
            if (item is ColumnHeaderRow element) {
               foreach (var header in element.ColumnHeaders) header.PropertyChanged += (s1, e1) => InvalidateVisual();
            }
         }
      }
   }

   #endregion

   #region ColumnWidth

   public static readonly StyledProperty<double> ColumnWidthProperty =
      AvaloniaProperty.Register<HorizontalSlantedTextControl, double>(nameof(ColumnWidth), 0.0);

   public double ColumnWidth {
      get => GetValue(ColumnWidthProperty);
      set => SetValue(ColumnWidthProperty, value);
   }

   #endregion

   #region HorizontalOffset

   public double HorizontalOffset {
      get => GetValue(HorizontalOffsetProperty);
      set => SetValue(HorizontalOffsetProperty, value);
   }

   public static readonly StyledProperty<double> HorizontalOffsetProperty =
      AvaloniaProperty.Register<HorizontalSlantedTextControl, double>("HorizontalOffset", 0.0);

   #endregion

   #region SlantAngle

   public double SlantAngle {
      get => GetValue(SlantAngleProperty);
      set => SetValue(SlantAngleProperty, value);
   }

   public static readonly StyledProperty<double> SlantAngleProperty =
      AvaloniaProperty.Register<HorizontalSlantedTextControl, double>(nameof(SlantAngle), 30.0);

   #endregion

   #region FontSize

   public static readonly StyledProperty<int> FontSizeProperty =
      AvaloniaProperty.Register<HorizontalSlantedTextControl, int>(nameof(FontSize), 0);

   public int FontSize {
      get => GetValue(FontSizeProperty);
      set => SetValue(FontSizeProperty, value);
   }

   private void OnFontSizeChanged(AvaloniaPropertyChangedEventArgs e) {
      UpdateDesiredHeight();
      InvalidateVisual();
   }

   #endregion

   static HorizontalSlantedTextControl() {
      // WPF wired these through FrameworkPropertyMetadata change callbacks.
      HeaderRowsProperty.Changed.AddClassHandler<HorizontalSlantedTextControl>((self, e) => self.OnHeaderRowsChanged(e));
      FontSizeProperty.Changed.AddClassHandler<HorizontalSlantedTextControl>((self, e) => self.OnFontSizeChanged(e));
      AffectsRender<HorizontalSlantedTextControl>(ColumnWidthProperty, HorizontalOffsetProperty, SlantAngleProperty);
   }

   public HorizontalSlantedTextControl() => ClipToBounds = true;

   public override void Render(DrawingContext drawingContext) {
      base.Render(drawingContext);
      if (HeaderRows == null) return;

      var theme1 = nameof(CoreTheme.Secondary);
      var theme2 = nameof(CoreTheme.Primary);

      // handle horizontal scrolling
      using var scrollTransform = drawingContext.PushTransform(Matrix.CreateTranslation(-HorizontalOffset, 0));

      int maxLength = 1;
      if (HeaderRows.Count > 0) maxLength = HeaderRows.Max(row => row.ColumnHeaders.Max(header => header.ColumnTitle.Length));
      var sampleFormat = Format(new string('0', maxLength), theme1);
      var angle = SlantAngle * Math.PI / 180;
      var heightPerRow = Math.Max(sampleFormat.Width * Math.Sin(angle) + sampleFormat.Height * Math.Cos(angle), sampleFormat.Height);
      var angledTextHeight = sampleFormat.Height * Math.Cos(SlantAngle * Math.PI / 180);
      var pen = new Pen(ThemeDictionary.Brush(nameof(CoreTheme.Backlight)), 1);

      double yOffset = heightPerRow;

      for (var i = 0; i < HeaderRows.Count; i++) {
         var row = HeaderRows[i];
         double xOffset = 70.0;
         var height = Bounds.Height - (HeaderRows.Count - i - 1) * heightPerRow;
         foreach (var header in row.ColumnHeaders) {
            var theta = (90 - SlantAngle) * Math.PI / 180;
            var separatorLength = sampleFormat.Width * Math.Cos(SlantAngle * Math.PI / 180) * 2 / 3;
            drawingContext.DrawLine(pen, new Point(xOffset, height), new Point(xOffset + Math.Sin(theta) * separatorLength, height - Math.Cos(theta) * separatorLength));

            xOffset += header.ByteWidth * ColumnWidth / 2;

            var text = Format(header.ColumnTitle, header.IsSelected ? theme2 : theme1);
            var additionalXOffsetForTilt = header.ColumnTitle.Length > 1 ? 3 : -2;
            var additionalYOffsetForTilt = header.ColumnTitle.Length > 1 ? angledTextHeight : sampleFormat.Height;

            var translate = Matrix.CreateTranslation(xOffset + additionalXOffsetForTilt, yOffset - additionalYOffsetForTilt);
            if (header.ColumnTitle.Length > 1) {
               using var tilt = drawingContext.PushTransform(Matrix.CreateRotation(Matrix.ToRadians(-SlantAngle)) * translate);
               drawingContext.DrawText(text, new Point());
            } else {
               using var straight = drawingContext.PushTransform(translate);
               drawingContext.DrawText(text, new Point());
            }

            xOffset += header.ByteWidth * ColumnWidth / 2;
         }
         yOffset += heightPerRow;
      }
   }

   private void UpdateDesiredHeight() {
      var maxLength = (HeaderRows?.Count ?? 0) == 0 ? 1 : HeaderRows.Max(row => row.ColumnHeaders.Max(header => header.ColumnTitle.Length));
      var theme = nameof(CoreTheme.Secondary);
      var formattedText = Format(new string('0', maxLength), theme);
      var rows = HeaderRows?.Count ?? 1;
      var angle = SlantAngle * Math.PI / 180;
      var heightPerRow = Math.Max(formattedText.Width * Math.Sin(angle) + formattedText.Height * Math.Cos(angle), formattedText.Height);
      var finalHeight = Math.Ceiling(heightPerRow * rows);
      Height = finalHeight;
   }

   private FormattedText Format(string text, string theme) {
      var typeface = new Typeface("Consolas");
      return new FormattedText(
         text,
         CultureInfo.CurrentCulture,
         FlowDirection.LeftToRight,
         typeface,
         Math.Max(FontSize * 3.0 / 4, 1),
         ThemeDictionary.Brush(theme));
   }
}
