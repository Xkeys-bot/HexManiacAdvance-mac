using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// Port of the container/child lookup helpers HexManiac.WPF keeps on MainWindow
/// (MainWindow.GetChild), which PaletteControl and others call to animate item containers.
/// WPF walked the visual tree with VisualTreeHelper and ItemContainerGenerator; Avalonia exposes
/// ContainerFromItem plus GetVisualDescendants.
/// </summary>
public static class VisualTreeExtensions {
   public static Control GetChild(ItemsControl parent, string name, object item) {
      if (parent == null || item == null) return null;
      var container = parent.ContainerFromItem(item);
      if (container == null) return null;
      if (container.Name == name) return container;
      return container.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name) ?? container;
   }

   public static T FindDescendant<T>(this Visual root, string name = null) where T : Visual {
      if (root == null) return null;
      return root.GetVisualDescendants().OfType<T>()
         .FirstOrDefault(v => name == null || (v as StyledElement)?.Name == name);
   }
}
