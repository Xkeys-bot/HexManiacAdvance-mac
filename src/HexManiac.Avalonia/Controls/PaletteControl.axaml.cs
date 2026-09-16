using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.MacPlatform;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;
using Point = Avalonia.Point; // Core also has a Point (grid coordinates)

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/PaletteControl.xaml.cs.
///
/// Notable substitutions: mouse capture goes through the pointer; WPF's DoubleAnimation on each
/// moved swatch becomes a DoubleTransition on its TranslateTransform; ColorConverter becomes
/// Avalonia's Color.Parse; and the eyedropper calls MacPlatform.DesktopColorPicker instead of
/// the Win32 one.
/// </summary>
public partial class PaletteControl : UserControl {
   private const int ExpectedElementWidth = 16, ExpectedElementHeight = 16;
   private static readonly TimeSpan span = TimeSpan.FromMilliseconds(100);

   private readonly Popup swatchPopup = new Popup { Placement = PlacementMode.RightEdgeAlignedTop, VerticalOffset = -15 };
   private readonly Swatch swatch = new Swatch { Width = 230, Height = 200 };
   private readonly TextBox[] swatchTextBoxes = new[] {
      new TextBox { [ToolTip.TipProperty] = "Red (0 to 31)" },
      new TextBox { [ToolTip.TipProperty] = "Green (0 to 31)" },
      new TextBox { [ToolTip.TipProperty] = "Blue (0 to 31)" },
      new TextBox { [ToolTip.TipProperty] = "Color Code (0000 to 7FFF)" },
   };
   private readonly TextBox[] decompTextBoxes = new[] {
      new TextBox { [ToolTip.TipProperty] = "Red (0 to 255)" },
      new TextBox { [ToolTip.TipProperty] = "Green (0 to 255)" },
      new TextBox { [ToolTip.TipProperty] = "Blue (0 to 255)" },
      new TextBox { [ToolTip.TipProperty] = "Color Code (000000-FFFFFF or a color word)" },
   };

   private Point interactionPoint;
   private short[] initialColors;
   private int activeSelection;
   private bool isPointerCaptured;

   private const string EyeDropperToolTipText = "Click this, then click any color on your screen to pull that color into the palette.";

   private PaletteCollection ViewModel => DataContext as PaletteCollection;

   private int InteractionTileIndex {
      get {
         var elementWidth = Math.Max(1, ItemsControl.Bounds.Width / ViewModel.ColorWidth);
         var elementHeight = Math.Max(1, ItemsControl.Bounds.Height / ViewModel.ColorHeight);
         var x = (int)(interactionPoint.X / elementWidth);
         var y = (int)(interactionPoint.Y / elementHeight);
         var index = y * ViewModel.ColorWidth + x;
         index = Math.Min(Math.Max(0, index), ViewModel.Elements.Count - 1);
         return index;
      }
   }

   public bool LoseKeyboardFocusCausesLoseMultiSelect { get; set; }

   public PaletteControl() {
      InitializeComponent();
      swatchPopup.PlacementTarget = ItemsControl;
      ((ISetLogicalParent)swatchPopup).SetParent(this);

      var eyeDropper = new Button {
         Padding = new Thickness(0),
         Content = new Path {
            Data = IconExtension.GetIcon("EyeDropper"),
            Fill = ThemeDictionary.Brush("Primary"),
            Stretch = Stretch.Uniform,
            Width = 16,
            Height = 16,
         },
         [ToolTip.TipProperty] = EyeDropperToolTipText,
      };
      DockPanel.SetDock(eyeDropper, Dock.Left);
      eyeDropper.Click += GrabScreenColor;

      swatchPopup.Child = new StackPanel {
         Children = {
            new Grid {
               HorizontalAlignment = HorizontalAlignment.Center,
               Children = {
                  new Rectangle { Fill = ThemeDictionary.Brush("Background"), Opacity = .5 },
                  new TextBlock { Text = "Shift/Ctrl+Click to edit multiple colors.", FontStyle = FontStyle.Italic, Foreground = ThemeDictionary.Brush("Secondary") },
               },
            },
            swatch,
            new DockPanel {
               Children = {
                  eyeDropper,
                  new UniformGrid {
                     Columns = 4,
                     Rows = 2,
                     Children = {
                        swatchTextBoxes[3],
                        swatchTextBoxes[0],
                        swatchTextBoxes[1],
                        swatchTextBoxes[2],
                        decompTextBoxes[3],
                        decompTextBoxes[0],
                        decompTextBoxes[1],
                        decompTextBoxes[2],
                     },
                  },
               },
            },
         },
      };
      LoseKeyboardFocusCausesLoseMultiSelect = true;
      DetachedFromVisualTree += (sender, e) => { EndScreenGrab(); ClosePopup(); };

      swatchTextBoxes[0].TextChanged += UpdateSwatchColorFromTextBoxes;
      swatchTextBoxes[1].TextChanged += UpdateSwatchColorFromTextBoxes;
      swatchTextBoxes[2].TextChanged += UpdateSwatchColorFromTextBoxes;
      swatchTextBoxes[3].TextChanged += UpdateSwatchColorFromBytesBox;
      decompTextBoxes[0].TextChanged += UpdateSwatchColorFromDecompTextBoxes;
      decompTextBoxes[1].TextChanged += UpdateSwatchColorFromDecompTextBoxes;
      decompTextBoxes[2].TextChanged += UpdateSwatchColorFromDecompTextBoxes;
      decompTextBoxes[3].TextChanged += UpdateSwatchColorFromDecompBytesBox;

      BuildContextMenu();
      BuildKeyBindings();

      PointerPressed += StartPaletteColorMove;
      PointerMoved += PaletteColorMove;
      PointerReleased += EndPaletteColorMove;
   }


   /// WPF declared these in UserControl.ContextMenu / UserControl.InputBindings with Ctrl.
   private void BuildContextMenu() {
      var eyeDropperItem = new MenuItem { Header = "Choose Color from Screen", [ToolTip.TipProperty] = EyeDropperToolTipText };
      eyeDropperItem.Click += GrabScreenColor;
      ContextMenu = new ContextMenu {
         ItemsSource = new Control[] {
            new MenuItem { Header = "Copy", InputGesture = new KeyGesture(Key.C, HexContent.CommandModifier), [!MenuItem.CommandProperty] = new Avalonia.Data.Binding(nameof(PaletteCollection.Copy)), CommandParameter = FileSystem },
            new MenuItem { Header = "Paste", InputGesture = new KeyGesture(Key.V, HexContent.CommandModifier), [!MenuItem.CommandProperty] = new Avalonia.Data.Binding(nameof(PaletteCollection.Paste)), CommandParameter = FileSystem },
            new MenuItem { Header = "Create Gradient", InputGesture = new KeyGesture(Key.G, HexContent.CommandModifier), [!MenuItem.CommandProperty] = new Avalonia.Data.Binding(nameof(PaletteCollection.CreateGradient)) },
            new MenuItem { Header = "Merge Colors", InputGesture = new KeyGesture(Key.M, HexContent.CommandModifier), [!MenuItem.CommandProperty] = new Avalonia.Data.Binding(nameof(PaletteCollection.SingleReduce)) },
            eyeDropperItem,
         },
      };
   }

   private void BuildKeyBindings() {
      void Add(Key key, KeyModifiers modifiers, string path, bool withFileSystem = false) {
         var binding = new KeyBinding { Gesture = new KeyGesture(key, modifiers) };
         binding.Bind(KeyBinding.CommandProperty, new Avalonia.Data.Binding($"DataContext.{path}") { Source = this });
         if (withFileSystem) binding.CommandParameter = FileSystem;
         KeyBindings.Add(binding);
      }
      Add(Key.C, HexContent.CommandModifier, nameof(PaletteCollection.Copy), true);
      Add(Key.V, HexContent.CommandModifier, nameof(PaletteCollection.Paste), true);
      Add(Key.G, HexContent.CommandModifier, nameof(PaletteCollection.CreateGradient));
      Add(Key.M, HexContent.CommandModifier, nameof(PaletteCollection.SingleReduce));
      Add(Key.Delete, KeyModifiers.None, nameof(PaletteCollection.DeleteColor));
   }

   private IFileSystem FileSystem {
      get {
         var top = TopLevel.GetTopLevel(this);
         if (top != null && top.TryFindResource("FileSystem", out var resource)) return resource as IFileSystem;
         return null;
      }
   }

   public void ClosePopup() {
      swatchPopup.IsOpen = false;
      swatch.ResultChanged -= SwatchResultChanged;
   }

   public void SingleSelect() => ViewModel?.SingleSelect();

   protected override void OnLostFocus(RoutedEventArgs e) {
      if (swatchPopup.IsOpen && swatchPopup.Child is InputElement child && child.IsKeyboardFocusWithin) return;
      ClosePopup();
      if (LoseKeyboardFocusCausesLoseMultiSelect) SingleSelect();
      base.OnLostFocus(e);
   }

   protected override void OnPointerExited(PointerEventArgs e) {
      base.OnPointerExited(e);
      if (ViewModel == null) return;
      ViewModel.HoverIndex = -1;
   }

   private void StartPaletteColorMove(object sender, PointerPressedEventArgs e) {
      if (ViewModel == null) return;
      swatch.ResultChanged -= SwatchResultChanged;
      Focus();

      var properties = e.GetCurrentPoint(this).Properties;
      interactionPoint = e.GetPosition(ItemsControl);
      var elementWidth = Math.Max(1, ItemsControl.Bounds.Width / ViewModel.ColorWidth);
      if (interactionPoint.X > elementWidth * ViewModel.ColorWidth || interactionPoint.X < 0) {
         ClosePopup();
         return;
      }
      var tileIndex = InteractionTileIndex;

      if (e.KeyModifiers == KeyModifiers.Shift) {
         ViewModel.SelectionEnd = tileIndex;
      } else if (e.KeyModifiers == HexContent.CommandModifier) {
         ViewModel.ToggleSelection(tileIndex);
      } else if (ViewModel.Elements[tileIndex].Selected && properties.IsLeftButtonPressed && swatchPopup.IsOpen) {
         e.Handled = true;
         ClosePopup();
         return;
      } else {
         ViewModel.SelectionStart = tileIndex;
      }

      e.Pointer.Capture(this);
      isPointerCaptured = true;
      e.Handled = true;

      if (e.KeyModifiers != KeyModifiers.Shift && e.KeyModifiers != HexContent.CommandModifier) {
         swatch.Result = Color32For(tileIndex);
         UpdateSwatchTextBoxContentFromSwatch();
         initialColors = CollectColorList();
         activeSelection = tileIndex;
         if (properties.IsLeftButtonPressed) {
            swatchPopup.IsOpen = ViewModel.CanEditColors;
            if (swatchPopup.IsOpen) {
               swatch.ResultChanged += SwatchResultChanged;
               commitTextboxChanges = true;
            }
         }
      } else {
         ClosePopup();
      }
   }

   private bool commitTextboxChanges = true;

   private void UpdateSwatchTextBoxContentFromSwatch() {
      if (!commitTextboxChanges) return;
      commitTextboxChanges = false;

      if (Color.TryParse(swatch.Result, out var color32)) UpdateTextBoxes(color32, ignoreSwatch: true);

      commitTextboxChanges = true;
   }

   private void UpdateSwatchColorFromTextBoxes(object sender, TextChangedEventArgs e) {
      if (!commitTextboxChanges) return;
      commitTextboxChanges = false;

      var color16 = ChannelStringsToColor16(swatchTextBoxes.Select(box => box.Text).ToArray());
      var color32 = TileImage.Convert16BitColor(color16);
      UpdateTextBoxes(color32, ignore5bitChannels: true);

      commitTextboxChanges = true;
   }

   private void UpdateSwatchColorFromBytesBox(object sender, TextChangedEventArgs e) {
      if (!commitTextboxChanges) return;
      commitTextboxChanges = false;

      if (short.TryParse(swatchTextBoxes[3].Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var color16)) {
         color16 = PaletteRun.FlipColorChannels(color16);
         var color32 = TileImage.Convert16BitColor(color16);
         UpdateTextBoxes(color32, ignore16bitColor: true);
      }

      commitTextboxChanges = true;
   }

   private void UpdateSwatchColorFromDecompTextBoxes(object sender, TextChangedEventArgs e) {
      if (!commitTextboxChanges) return;
      commitTextboxChanges = false;

      if (
         byte.TryParse(decompTextBoxes[0].Text, out var red) &&
         byte.TryParse(decompTextBoxes[1].Text, out var green) &&
         byte.TryParse(decompTextBoxes[2].Text, out var blue)
      ) {
         var color32 = Color.FromRgb(red, green, blue);
         UpdateTextBoxes(color32, ignore8bitChannels: true);
      }

      commitTextboxChanges = true;
   }

   private void UpdateSwatchColorFromDecompBytesBox(object sender, TextChangedEventArgs e) {
      if (!commitTextboxChanges) return;
      commitTextboxChanges = false;

      if (Color.TryParse(decompTextBoxes[3].Text, out var color32)) {
         UpdateTextBoxes(color32, ignore32bitColor: true);
      } else if (Color.TryParse("#" + decompTextBoxes[3].Text, out color32)) { // maybe its 6 hex characters?
         UpdateTextBoxes(color32, ignore32bitColor: true);
      }

      commitTextboxChanges = true;
   }

   private void UpdateTextBoxes(
      Color color32,
      bool ignore5bitChannels = false,
      bool ignore8bitChannels = false,
      bool ignore16bitColor = false,
      bool ignore32bitColor = false,
      bool ignoreSwatch = false
   ) {
      var color16 = TileImage.Convert16BitColor(color32);
      var channels = Color16ToChannelStrings(color16);
      color16 = PaletteRun.FlipColorChannels(color16);

      if (!ignore5bitChannels) {
         for (int i = 0; i < channels.Length; i++) {
            swatchTextBoxes[i].Text = channels[i];
         }
      }

      if (!ignore16bitColor) {
         swatchTextBoxes[3].Text = color16.ToString("X4");
      }

      if (!ignore8bitChannels) {
         for (int i = 0; i < channels.Length; i++) {
            decompTextBoxes[i].Text = (int.Parse(channels[i]) * 255 / 31).ToString();
         }
      }

      if (!ignore32bitColor) {
         decompTextBoxes[3].Text = color32.ToString().Substring(3); // cut off the #FF at the beginning
      }

      if (!ignoreSwatch) {
         swatch.Result = color32.ToString();
      }
   }

   private void PaletteColorMove(object sender, PointerEventArgs e) {
      if (ViewModel == null || !ViewModel.CanEditColors || isInScreenGrabMode) return;
      var oldTileIndex = InteractionTileIndex;
      interactionPoint = e.GetPosition(ItemsControl);
      var newTileIndex = InteractionTileIndex;
      var elementWidth = Math.Max(1, ItemsControl.Bounds.Width / ViewModel.ColorWidth);

      if (!isPointerCaptured) {
         ViewModel.HoverIndex = newTileIndex;
         if (interactionPoint.X < 0 || interactionPoint.X > elementWidth * ViewModel.ColorWidth) ViewModel.HoverIndex = -1;
         return;
      }

      var tilesToAnimate = ViewModel.HandleMove(oldTileIndex, newTileIndex);
      if (oldTileIndex != newTileIndex) {
         ClosePopup();
      }

      foreach (var (index, direction) in tilesToAnimate) {
         var tile = VisualTreeExtensions.GetChild(ItemsControl, "PaletteColor", ViewModel.Elements[index]);
         if (tile == null) continue;
         if (tile.RenderTransform is not TranslateTransform transform) {
            transform = new TranslateTransform {
               // WPF animated with DoubleAnimation(from, to, span); Avalonia animates a property
               // change, so the transform carries the transition and we jump-then-settle.
               Transitions = new Transitions {
                  new DoubleTransition { Property = TranslateTransform.XProperty, Duration = span },
               },
            };
            tile.RenderTransform = transform;
         }
         transform.X = elementWidth * direction;
         Dispatcher.UIThread.Post(() => transform.X = 0, DispatcherPriority.Background);
      }
   }

   private void EndPaletteColorMove(object sender, PointerReleasedEventArgs e) {
      if (!isPointerCaptured) return;
      e.Pointer.Capture(null);
      isPointerCaptured = false;

      ViewModel?.CompleteCurrentInteraction();
   }

   private short[] CollectColorList() => ViewModel.Elements.Select(element => element.Color).ToArray();

   private (double lightDif, double aDif, double bDif) GetColorDif(Color newColor) {
      var oldColor = TileImage.Convert16BitColor(initialColors[activeSelection]);
      var (light, a, b) = CoreTheme.ToOklab(newColor.R, newColor.G, newColor.B);
      var (oldLight, oldA, oldB) = CoreTheme.ToOklab(oldColor.R, oldColor.G, oldColor.B);
      return (light - oldLight, a - oldA, b - oldB);
   }

   /// <summary>
   /// Grabs the initial color at index and applies a HSB dif to it, returning the new short color
   /// </summary>
   private short ApplyDif(int index, (double lightDif, double aDif, double bDif) colorDif) {
      var originalColor = TileImage.Convert16BitColor(initialColors[index]);
      var (light, a, b) = CoreTheme.ToOklab(originalColor.R, originalColor.G, originalColor.B);
      light += colorDif.lightDif;
      a += colorDif.aDif;
      b += colorDif.bDif;
      var (red, green, blue) = CoreTheme.FromOklab(light, a, b);
      var newColor = Color.FromRgb(red, green, blue);
      return TileImage.Convert16BitColor(newColor);
   }

   private void SwatchResultChanged(object sender, string oldValue) {
      if (!Color.TryParse(swatch.Result, out var newColor)) return;
      var dif = GetColorDif(newColor);

      for (int i = 0; i < ViewModel.Elements.Count; i++) {
         if (!ViewModel.Elements[i].Selected) continue;
         if (i == activeSelection) {
            ViewModel.Elements[i].Color = TileImage.Convert16BitColor(newColor);
            continue;
         }

         ViewModel.Elements[i].Color = ApplyDif(i, dif);
      }

      UpdateSwatchTextBoxContentFromSwatch();

      ViewModel.PushColorsToModel();
   }

   private string Color32For(int tileIndex) {
      var color = TileImage.Convert16BitColor(ViewModel.Elements[tileIndex].Color);
      return color.ToString();
   }

   private static string[] Color16ToChannelStrings(short color16) {
      var r = color16 >> 10;
      var g = (color16 >> 5) & 0x1F;
      var b = color16 & 0x1F;
      return new[] { r.ToString(), g.ToString(), b.ToString() };
   }

   private bool isInScreenGrabMode = false;
   private int colorIndexForScreenGrab;

   /// <summary>
   /// WPF armed this with CaptureMouse(), so the next click anywhere the process could see it
   /// was routed back here. Avalonia cannot capture the pointer before a press has happened, so
   /// the arming handler goes on the TopLevel instead: any click inside the app - the map, the
   /// hex view, another palette - commits the colour under the cursor.
   ///
   /// Clicking a *different application's* window cannot be intercepted on macOS without a
   /// global event tap (which needs Accessibility permission), so that click goes to the other
   /// app and the grab stays armed until the user clicks back inside this one.
   /// </summary>
   private void GrabScreenColor(object sender, EventArgs e) {
      if (ViewModel == null) return;
      ViewModel.SingleSelect();
      colorIndexForScreenGrab = ViewModel.SelectionStart;
      if (isInScreenGrabMode) return;
      isInScreenGrabMode = true;
      screenGrabHost = this.GetVisualRoot() as InputElement;
      screenGrabHost?.AddHandler(PointerPressedEvent, GrabColorFromScreen, RoutingStrategies.Tunnel);
   }

   private InputElement screenGrabHost;

   private void GrabColorFromScreen(object sender, PointerPressedEventArgs e) {
      var color = DesktopColorPicker.GrabMousePixelColorFromScreen();
      if (color.HasValue && ViewModel != null && colorIndexForScreenGrab >= 0 && colorIndexForScreenGrab < ViewModel.Elements.Count) {
         var color16 = TileImage.Convert16BitColor(color.Value);
         ViewModel.Elements[colorIndexForScreenGrab].Color = color16;
         ViewModel.PushColorsToModel();
      }

      EndScreenGrab();
      e.Handled = true;
      ClosePopup();
   }

   private void EndScreenGrab() {
      if (!isInScreenGrabMode) return;
      isInScreenGrabMode = false;
      screenGrabHost?.RemoveHandler(PointerPressedEvent, GrabColorFromScreen);
      screenGrabHost = null;
   }

   private short ChannelStringsToColor16(string[] channels) {
      int.TryParse(channels[0], out int r);
      int.TryParse(channels[1], out int g);
      int.TryParse(channels[2], out int b);
      r = r.LimitToRange(0, 0x1F);
      g = g.LimitToRange(0, 0x1F);
      b = b.LimitToRange(0, 0x1F);
      var color = (r << 10) + (g << 5) + b;
      return (short)color;
   }
}
