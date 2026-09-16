using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>Ported from HexManiac.WPF/Controls/TextEditor.xaml.cs.</summary>
public partial class TextEditor : UserControl {
   #region IsReadOnly

   public static readonly StyledProperty<bool> IsReadOnlyProperty =
      AvaloniaProperty.Register<TextEditor, bool>(nameof(IsReadOnly), false);

   public bool IsReadOnly {
      get => GetValue(IsReadOnlyProperty);
      set => SetValue(IsReadOnlyProperty, value);
   }

   protected virtual void OnIsReadOnlyChanged() {
      TransparentLayer.IsReadOnly = IsReadOnly;
   }

   #endregion

   #region ContextMenuOverride

   public ContextMenu ContextMenuOverride {
      get => GetValue(ContextMenuOverrideProperty);
      set => SetValue(ContextMenuOverrideProperty, value);
   }

   public static readonly StyledProperty<ContextMenu> ContextMenuOverrideProperty =
      AvaloniaProperty.Register<TextEditor, ContextMenu>(nameof(ContextMenuOverride));

   #endregion

   #region TextBox-Like properties

   public event EventHandler<RoutedEventArgs> SelectionChanged;

   public double VerticalOffset => scrollViewer?.Offset.Y ?? 0;

   #endregion

   public TextEditorViewModel ViewModel => DataContext as TextEditorViewModel;

   /// The real TextBox under the colour layers; AutocompleteOverlay attaches to this.
   public TextBox EditBox => TransparentLayer;

   private IEnumerable<TextBlock> Layers => new[] { BasicLayer, AccentLayer, ConstantsLayer, NumericLayer, CommentLayer, TextLayer };

   private readonly Dictionary<TextBlock, TranslateTransform> layerTransforms = new();
   private ScrollViewer scrollViewer;
   private TextEditorViewModel subscribed;

   static TextEditor() {
      IsReadOnlyProperty.Changed.AddClassHandler<TextEditor>((self, e) => self.OnIsReadOnlyChanged());
   }

   public TextEditor() {
      InitializeComponent();
      foreach (var layer in Layers) {
         var transform = new TranslateTransform();
         layer.RenderTransform = transform;
         layerTransforms[layer] = transform;
      }

      TransparentLayer.PropertyChanged += (sender, e) => {
         if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty) {
            SelectionChanged?.Invoke(this, new RoutedEventArgs());
         }
      };
      DataContextChanged += (sender, e) => HandleDataContextChanged();

      // ExtentWidth is not observable, so check for the horizontal scroll bar when the text changes
      TransparentLayer.TextChanged += (sender, e) => {
         var typeface = new Typeface(TransparentLayer.FontFamily, FontStyle.Normal, FontWeight.Normal);
         var width = new FormattedText(TransparentLayer.Text ?? string.Empty, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, TransparentLayer.FontSize, Brushes.Transparent).Width;
         foreach (var layer in Layers) layer.Width = width;
         if (scrollViewer != null && width > scrollViewer.Viewport.Width && scrollViewer.Viewport.Height > scrollViewer.Extent.Height) {
            CornerCover.Width = 16;
            CornerCover.Height = 17;
         } else {
            CornerCover.Width = 0;
            CornerCover.Height = 0;
         }
      };

      // WPF used the ScrollViewer.ScrollChanged attached event; Avalonia exposes the TextBox's
      // own ScrollViewer only once the template is applied.
      TransparentLayer.TemplateApplied += (sender, e) => {
         scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
         if (scrollViewer != null) scrollViewer.ScrollChanged += TextScrollChanged;
      };
   }


   private void HandleDataContextChanged() {
      if (subscribed != null) {
         subscribed.PropertyChanged -= HandleViewModelPropertyChanged;
         subscribed.RequestCaretMove -= HandleViewModelCaretMove;
         subscribed.RequestKeyboardFocus -= HandleViewModelRequestKeyboardFocus;
         subscribed.ErrorLocations.CollectionChanged -= HandleViewModelErrorUpdate;
      }
      subscribed = ViewModel;
      if (subscribed != null) {
         subscribed.PropertyChanged += HandleViewModelPropertyChanged;
         subscribed.RequestCaretMove += HandleViewModelCaretMove;
         subscribed.RequestKeyboardFocus += HandleViewModelRequestKeyboardFocus;
         subscribed.ErrorLocations.CollectionChanged += HandleViewModelErrorUpdate;
         UpdateErrorDecorations();
      }
   }

   private void HandleViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) {
      if (e.PropertyName == nameof(TextEditorViewModel.CommentContent)) {
         UpdateErrorDecorations();
      }
   }

   private void HandleViewModelCaretMove(object sender, EventArgs e) {
      var vm = (TextEditorViewModel)sender;
      if (TransparentLayer.CaretIndex != vm.CaretIndex) TransparentLayer.CaretIndex = vm.CaretIndex;
   }

   /// WPF had to suppress RequestBringIntoView around the focus call; Avalonia's Focus() does not
   /// scroll ancestors, so the plain call is equivalent.
   private void HandleViewModelRequestKeyboardFocus(object sender, EventArgs e) => TransparentLayer.Focus();

   private void HandleViewModelErrorUpdate(object sender, EventArgs e) => UpdateErrorDecorations();

   public void ScrollToVerticalOffset(double offset) {
      if (scrollViewer != null) scrollViewer.Offset = new Vector(scrollViewer.Offset.X, offset);
   }

   protected override void OnPointerPressed(PointerPressedEventArgs e) {
      base.OnPointerPressed(e);
      if (e.ClickCount != 2 || ViewModel == null) return;

      // expand the selection left until the next whitespace
      var start = Math.Min(TransparentLayer.SelectionStart, TransparentLayer.SelectionEnd);
      var length = Math.Abs(TransparentLayer.SelectionEnd - TransparentLayer.SelectionStart);
      while (start > 0 && !char.IsWhiteSpace(ViewModel.Content[start - 1])) { start--; length++; }
      while ((start + length - 1).InRange(0, ViewModel.Content.Length) && !char.IsWhiteSpace(ViewModel.Content[start + length - 1])) length++;
      TransparentLayer.SelectionStart = start;
      TransparentLayer.SelectionEnd = start + length;
   }

   private void TextScrollChanged(object sender, ScrollChangedEventArgs e) {
      foreach (var layer in Layers) {
         var transform = layerTransforms[layer];
         transform.Y = -scrollViewer.Offset.Y;
         transform.X = -scrollViewer.Offset.X;
         layer.Width = scrollViewer.Extent.Width;
      }
   }

   private static IBrush Brush(string name) => ThemeDictionary.Brush(name);

   /// <summary>
   /// The squiggly underline. WPF built a TextDecoration with a tiled DrawingBrush pen;
   /// Avalonia's TextDecoration takes the same idea with Stroke / StrokeThickness.
   /// </summary>
   private static TextDecoration Squiggle(IBrush color) {
      var geometry = Geometry.Parse("M0,0 L1,1 2,0");
      var brush = new DrawingBrush(new GeometryDrawing { Brush = color, Geometry = geometry }) {
         TileMode = TileMode.Tile,
         DestinationRect = new RelativeRect(0, 0, 3, 2, RelativeUnit.Absolute),
      };
      return new TextDecoration {
         Location = TextDecorationLocation.Underline,
         Stroke = brush,
         StrokeThickness = 2,
         StrokeOffset = -1,
         StrokeOffsetUnit = TextDecorationUnit.Pixel,
         StrokeThicknessUnit = TextDecorationUnit.Pixel,
      };
   }

   private void UpdateErrorDecorations() {
      if (ViewModel == null) return;
      var text = ViewModel.CommentContent ?? string.Empty;
      var inlines = CommentLayer.Inlines;
      inlines.Clear();
      int character = 0, line = 0;

      var errorDecoration = new TextDecorationCollection { Squiggle(Brush(nameof(CoreTheme.Error))) };
      var warningDecoration = new TextDecorationCollection { Squiggle(Brush(nameof(CoreTheme.Data1))) };

      var lineEnd = Environment.NewLine.ToCharArray().Last();

      foreach (var error in ViewModel.ErrorLocations) {
         var previousLines = character;
         while (line < error.Line && character < text.Length) {
            character++;
            if (text[character - 1] == lineEnd) line++;
         }

         if (error.Start + error.Length > text.Length - character) break;
         inlines.Add(new Run(text.Substring(previousLines, character - previousLines + error.Start)));
         if (error.Type == SegmentType.Error) {
            inlines.Add(new Run(text.Substring(character + error.Start, error.Length)) { TextDecorations = errorDecoration });
         } else if (error.Type == SegmentType.Warning) {
            inlines.Add(new Run(text.Substring(character + error.Start, error.Length)) { TextDecorations = warningDecoration });
         } else {
            throw new NotImplementedException();
         }

         character += error.Start + error.Length;
      }

      if (character < text.Length) inlines.Add(new Run(text.Substring(character)));
   }
}
