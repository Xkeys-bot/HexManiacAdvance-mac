using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HavenSoft.HexManiac.AvaloniaUI.Controls;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// WPF filled "Open Recent" with ItemsSource + an ItemContainerStyle. A NativeMenu has no
/// ItemsSource, so the submenu is projected from the view model collection here.
/// </summary>
public class RecentFilesMenuConverter : IValueConverter {
   public static RecentFilesMenuConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var menu = new NativeMenu();
      if (value is not IEnumerable items) return menu;
      foreach (var item in items) {
         var type = item.GetType();
         var header = type.GetProperty("ShortName")?.GetValue(item)?.ToString();
         var command = type.GetProperty("Open")?.GetValue(item) as ICommand;
         menu.Add(new NativeMenuItem(header ?? string.Empty) { Command = command });
      }
      return menu;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}

/// <summary>
/// WPF built the Utilities submenus in code (MainWindow.FillQuickEditMenu) by adding MenuItems.
/// Same idea, expressed as a converter so the NativeMenu can bind to each quick-edit list.
/// </summary>
public class QuickEditMenuConverter : IValueConverter {
   public static QuickEditMenuConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var menu = new NativeMenu();
      if (value is not IEnumerable items) return menu;
      foreach (var item in items) {
         var type = item.GetType();
         var header = type.GetProperty("Name")?.GetValue(item)?.ToString();
         var command = item as ICommand ?? type.GetProperty("Command")?.GetValue(item) as ICommand;
         var menuItem = new NativeMenuItem(header ?? item.ToString()) { Command = command };
         var tooltip = type.GetProperty("Description")?.GetValue(item);
         if (tooltip is IEnumerable<string> lines) menuItem.ToolTip = string.Join(Environment.NewLine, lines);
         else if (tooltip != null) menuItem.ToolTip = tooltip.ToString();
         menu.Add(menuItem);
      }
      return menu;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}

/// <summary>
/// WPF's Find panel used DataTriggers to swap a toggle's brush between Primary and Secondary.
/// </summary>
public class FindToggleBrushConverter : IValueConverter {
   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
      ThemeDictionary.Brush(value is true ? nameof(CoreTheme.Primary) : nameof(CoreTheme.Secondary));

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}

/// <summary>
/// WPF collapsed the tools tray with a DataTrigger on Tools.SelectedIndex == -1 (a negative
/// match). EqualityToBooleanConverter only does positive matches, so this is its inverse.
/// </summary>
public class NotEqualConverter : IValueConverter {
   public static NotEqualConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
      !Equals(value?.ToString(), parameter?.ToString());

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}

/// <summary>
/// WPF could stack several DataTriggers on one element to make it visible for any of a set of
/// values. Avalonia bindings have no "or", so a comma-separated ConverterParameter stands in:
/// "NPC,Tutor,Trade" is true when the bound value matches any of the three.
/// </summary>
public class MatchesAnyConverter : IValueConverter {
   public static MatchesAnyConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      var text = value?.ToString();
      if (parameter is not string options) return false;
      foreach (var option in options.Split(',')) {
         if (Equals(text, option.Trim())) return true;
      }
      return false;
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}

/// <summary>
/// WPF chose between Canvas.Left/Right and Canvas.Top/Bottom for the map connection shifters with
/// four DataTriggers on AnchorLeftEdge / AnchorTopEdge. An Avalonia setter cannot be swapped by a
/// trigger, so all four attached properties are set and this returns NaN - which Canvas treats as
/// "not set" - for the two that do not apply. It takes the position and the flag together so the
/// shifter still moves while it is dragged.
/// </summary>
public class EdgeAnchorConverter : IMultiValueConverter {
   /// Applies when the view model anchors to this edge (Canvas.Left / Canvas.Top).
   public static EdgeAnchorConverter Leading { get; } = new(true);
   /// Applies when it anchors to the opposite edge (Canvas.Right / Canvas.Bottom).
   public static EdgeAnchorConverter Trailing { get; } = new(false);

   private readonly bool wantAnchored;
   private EdgeAnchorConverter(bool wantAnchored) => this.wantAnchored = wantAnchored;

   public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
      if (values.Count < 2) return double.NaN;
      if (values[1] is not bool anchoredToLeadingEdge) return double.NaN;
      if (anchoredToLeadingEdge != wantAnchored) return double.NaN;
      return values[0] switch {
         int position => (double)position,
         double position => position,
         _ => double.NaN,
      };
   }
}

/// <summary>
/// WPF picked the shifter arrow with one DataTrigger per MapSliderIcons value. The enum names
/// match the icon names in Icons.axaml, so a lookup covers all six.
/// </summary>
public class MapSliderIconConverter : IValueConverter {
   public static MapSliderIconConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch {
      MapSliderIcons.ExtendLeft => IconExtension.GetIcon(Icons.LineArrowLeft),
      MapSliderIcons.ExtendUp => IconExtension.GetIcon(Icons.LineArrowUp),
      MapSliderIcons.ExtendRight => IconExtension.GetIcon(Icons.LineArrowRight),
      MapSliderIcons.ExtendDown => IconExtension.GetIcon(Icons.LineArrowDown),
      MapSliderIcons.LeftRight => IconExtension.GetIcon(Icons.ArrowsLeftRight),
      MapSliderIcons.UpDown => IconExtension.GetIcon(Icons.ArrowsUpDown),
      _ => null,
   };

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotImplementedException();
}

/// <summary>
/// WPF made a control visible from several independent DataTriggers, each of which set
/// Visibility=Visible - so the effect was "visible if any of these is true". Avalonia binds
/// IsVisible to a single value, so the same set of flags is combined here instead.
/// </summary>
public class AnyTrueConverter : IMultiValueConverter {
   public static AnyTrueConverter Instance { get; } = new();

   public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
      foreach (var value in values) if (value is bool flag && flag) return true;
      return false;
   }
}

/// <summary>
/// Guards a TwoWay ComboBox selection binding against the "empty selection" write that happens
/// when the control is torn down.
///
/// Switching tabs unloads the outgoing tab's controls, which detaches their ItemsSource bindings.
/// An Avalonia SelectingItemsControl with no items coerces SelectedIndex to -1 (SelectedItem to
/// null) and, because those properties bind TwoWay by default, writes that straight back into the
/// view model. The view models here map selections onto ROM fields, so the write is real data
/// corruption: PrimaryMap.SelectedNameIndex = -1 stores regionSectionID 0x57 on FireRed and the
/// map loses its name. WPF never did this, so no HexManiac.Core setter defends against it.
///
/// BindingOperations.DoNothing cancels just that write, leaving a genuine user selection alone.
/// </summary>
public class IgnoreEmptySelectionConverter : IValueConverter {
   public static IgnoreEmptySelectionConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value;

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is null) return BindingOperations.DoNothing;
      if (value is int index && index < 0) return BindingOperations.DoNothing;
      return value;
   }
}

/// <summary>
/// Builds the goto prefix token's hover tooltip, or nothing at all when the token has no tip.
///
/// WPF put a HexContentToolTip in a Setter and cleared it with
/// `DataTrigger Binding="{Binding HoverTip}" Value="{x:Null}"`, so a token with no tip had no
/// tooltip. The port attached one unconditionally and only hid its content, which is not the same
/// thing: Avalonia still opened a tooltip popup, and an empty popup lands under the pointer, takes
/// the hover away from the button, closes, and lets the button take it back - an open/close loop
/// that ran at about 30Hz for as long as the pointer kept moving over the token. That is the
/// flicker on the "data" / "scripts" buttons.
///
/// Returning null here leaves ToolTip.Tip unset, exactly as WPF's cleared Setter did.
/// </summary>
public class GotoHoverTipConverter : IValueConverter {
   public static GotoHoverTipConverter Instance { get; } = new();

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is not IEnumerable content) return null;
      var enumerator = content.GetEnumerator();
      try {
         if (!enumerator.MoveNext()) return null;
      } finally {
         (enumerator as IDisposable)?.Dispose();
      }
      return new HexContentToolTip { DataContext = value };
   }

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}

/// <summary>
/// True when the first flag is set and the second is not.
///
/// Exists for the code editor's context menu. WPF swapped the whole menu with a DataTrigger per
/// case, so a later trigger simply won - its comment notes that Goto Source has to override Goto
/// Address "for Goto trainerstats/ED to work right for Emerald". Avalonia shows or hides each item
/// independently, so that precedence has to be written down rather than falling out of ordering.
/// </summary>
public class AndNotConverter : IMultiValueConverter {
   public static AndNotConverter Instance { get; } = new();

   public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture) {
      if (values.Count < 2) return false;
      return values[0] is true && values[1] is not true;
   }
}
