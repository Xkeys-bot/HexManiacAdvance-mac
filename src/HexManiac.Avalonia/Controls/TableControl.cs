using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using CoreTheme = HavenSoft.HexManiac.Core.ViewModels.Theme;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/TableControl.cs.
/// (Upstream this is still a sketch -- it draws one circle per table member, and no XAML
/// references it -- so the port keeps exactly that behaviour rather than inventing a finished
/// control. It derives from Control rather than Panel because Avalonia seals Panel.Render; it
/// never had children, so the layout overrides behave identically.)
/// </summary>
public class TableControl : Control {

   #region Groups

   public static readonly StyledProperty<ObservableCollection<TableGroupViewModel>> GroupsProperty =
      AvaloniaProperty.Register<TableControl, ObservableCollection<TableGroupViewModel>>("Groups");

   public ObservableCollection<TableGroupViewModel> Groups {
      get => GetValue(GroupsProperty);
      set => SetValue(GroupsProperty, value);
   }

   static TableControl() {
      GroupsProperty.Changed.AddClassHandler<TableControl>((self, e) => self.OnGroupsChanged(e));
   }

   protected virtual void OnGroupsChanged(AvaloniaPropertyChangedEventArgs e) {
      if (e.OldValue is ObservableCollection<TableGroupViewModel> oldGroups) {
         oldGroups.CollectionChanged -= CollectionChanged;
      }
      if (e.NewValue is ObservableCollection<TableGroupViewModel> newGroups) {
         newGroups.CollectionChanged += CollectionChanged;
      }
      InvalidateVisual();
   }

   private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
      InvalidateVisual();
   }

   #endregion

   public override void Render(DrawingContext dc) {
      base.Render(dc);
      if (Groups == null) return;
      int count = 0;
      var primary = ThemeDictionary.Brush(nameof(CoreTheme.Primary));
      foreach (var group in Groups) {
         foreach (var member in group.Members) {
            dc.DrawEllipse(primary, null, new Point(30, 30 + count * 60), 30, 30);
            count++;
         }
      }
   }

   protected override Size MeasureOverride(Size availableSize) {
      return new Size(60, 600);
   }

   protected override Size ArrangeOverride(Size finalSize) {
      var size = base.ArrangeOverride(finalSize);
      return new Size(size.Width, 600);
   }
}
