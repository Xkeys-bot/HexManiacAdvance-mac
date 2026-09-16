using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using HavenSoft.HexManiac.Core;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// A cross between a WrapPanel and a StackPanel.
/// Like a WrapPanel, new elements are added left-to-right, and further elements appear below the first row.
/// However, each column calculates its height separately:
///   in a 3-column layout, the 4th element will appear in the leftmost column,
///   directly after the end of the 1st element, ignoring the size of the 2nd and 3rd elemenst.
/// </summary>
/// <remarks>
/// Ported from HexManiac.WPF/Controls/ColumnStackPanel.cs. WPF's DependencyProperty becomes
/// Avalonia's StyledProperty/AttachedProperty; InternalChildren becomes Children; and the
/// invalidation that FrameworkPropertyMetadata declared is registered with AffectsMeasure.
/// </remarks>
public class ColumnStackPanel : Panel {
   /// <summary>
   /// During Measure, this grows based on the number of Headers found.
   /// During Arrange, this limits the maximum number of columns.
   /// </summary>
   private int expectedHeaderCount = 0;

   #region IsHeader

   /// <summary>
   /// Attached property for children within the panel. Set to 'true' in order to move to the next column.
   /// </summary>
   public static readonly AttachedProperty<bool> IsHeaderProperty =
      AvaloniaProperty.RegisterAttached<ColumnStackPanel, Control, bool>("IsHeader", false);

   public static bool GetIsHeader(Control element) => element.GetValue(IsHeaderProperty);
   public static void SetIsHeader(Control element, bool value) => element.SetValue(IsHeaderProperty, value);

   #endregion

   #region MinimumColumnWidth

   /// <summary>
   /// The minimum width needed for a column. The number of columns will scale based on available width.
   /// </summary>
   public static readonly StyledProperty<double> MinimumColumnWidthProperty =
      AvaloniaProperty.Register<ColumnStackPanel, double>(nameof(MinimumColumnWidth), 32d);

   public double MinimumColumnWidth {
      get => GetValue(MinimumColumnWidthProperty);
      set => SetValue(MinimumColumnWidthProperty, value);
   }

   #endregion

   #region ColumnMargin

   /// <summary>
   /// The space between columns. Should be left empty.
   /// </summary>
   public static readonly StyledProperty<double> ColumnMarginProperty =
      AvaloniaProperty.Register<ColumnStackPanel, double>(nameof(ColumnMargin), 0d);

   public double ColumnMargin {
      get => GetValue(ColumnMarginProperty);
      set => SetValue(ColumnMarginProperty, value);
   }

   #endregion

   #region HeaderMargin

   /// <summary>
   /// The space between the end of one section and the start of a new section below it within the same column.
   /// </summary>
   public static readonly StyledProperty<double> HeaderMarginProperty =
      AvaloniaProperty.Register<ColumnStackPanel, double>(nameof(HeaderMargin), 0d);

   public double HeaderMargin {
      get => GetValue(HeaderMarginProperty);
      set => SetValue(HeaderMarginProperty, value);
   }

   #endregion

   static ColumnStackPanel() {
      // WPF declared this through FrameworkPropertyMetadata's change callback / AffectsMeasure.
      AffectsMeasure<ColumnStackPanel>(MinimumColumnWidthProperty, ColumnMarginProperty, HeaderMarginProperty);
      AffectsArrange<ColumnStackPanel>(ColumnMarginProperty, HeaderMarginProperty);
   }

   protected override Size MeasureOverride(Size availableSize) {
      // count header elements among children
      expectedHeaderCount = 0;
      foreach (var child in Children) { if (GetContentIsHeader(child)) expectedHeaderCount += 1; }

      var (widthPerColumn, desiredColumnCount) = CalculateColumnWidth(availableSize, expectedHeaderCount);
      widthPerColumn = Math.Max(widthPerColumn, MinimumColumnWidth);
      var offerSize = new Size(widthPerColumn, availableSize.Height);

      // calculate the desired height of each column
      var desiredHeights = new double[desiredColumnCount];
      var activeColumn = -1;
      foreach (var child in Children) {
         if (GetContentIsHeader(child)) {
            activeColumn = (activeColumn + 1) % desiredColumnCount; // desiredHeights.IndexOf(desiredHeights.Min());
            if (desiredHeights[activeColumn] > 0) desiredHeights[activeColumn] += HeaderMargin;
         }
         if (activeColumn < 0) activeColumn = 0;
         child.Measure(offerSize);
         desiredHeights[activeColumn] += child.DesiredSize.Height;
      }

      return new Size(widthPerColumn * desiredColumnCount + ColumnMargin * (desiredColumnCount - 1), desiredHeights.Max());
   }

   protected override Size ArrangeOverride(Size finalSize) {
      var (widthPerColumn, desiredColumnCount) = CalculateColumnWidth(finalSize, expectedHeaderCount);

      // calculate the desired height of each column
      var usedHeight = new double[desiredColumnCount];
      var activeColumn = -1;
      foreach (var child in Children) {
         if (GetContentIsHeader(child)) {
            activeColumn = (activeColumn + 1) % desiredColumnCount; // usedHeight.IndexOf(usedHeight.Min());
            if (usedHeight[activeColumn] > 0) usedHeight[activeColumn] += HeaderMargin;
         }
         if (activeColumn < 0) activeColumn = 0;
         child.Arrange(new Rect(activeColumn * (widthPerColumn + ColumnMargin), usedHeight[activeColumn], widthPerColumn, child.DesiredSize.Height));
         usedHeight[activeColumn] += child.Bounds.Height; // WPF: child.RenderSize.Height
      }

      return new Size(widthPerColumn * desiredColumnCount + ColumnMargin * (desiredColumnCount - 1), usedHeight.Max());
   }

   private bool GetContentIsHeader(Control child) {
      return true;
      // WPF kept a commented-out walk down through ContentPresenters here; preserved as-is.
   }

   private (double columnWidth, int columnCount) CalculateColumnWidth(Size offer, int maxColumns = int.MaxValue) {
      // make sure we have a reasonable size to work with
      var columnWidth = Math.Max(MinimumColumnWidth, 16);
      var availableWidth = offer.Width;
      if (double.IsNaN(availableWidth) || double.IsPositiveInfinity(availableWidth)) availableWidth = columnWidth * 2;
      var desiredColumnCount = (int)((availableWidth + ColumnMargin) / (columnWidth + ColumnMargin));
      if (maxColumns < 1) maxColumns = 1;
      desiredColumnCount = desiredColumnCount.LimitToRange(1, maxColumns);
      var widthPerColumn = (availableWidth + ColumnMargin) / desiredColumnCount - ColumnMargin;
      return (widthPerColumn, desiredColumnCount);
   }
}
