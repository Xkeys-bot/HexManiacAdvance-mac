using Avalonia.Controls;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// Fills the theme brushes in at design time so the previewer isn't blank.
/// WPF used DesignerProperties.GetIsInDesignMode; Avalonia exposes Design.IsDesignMode.
/// </summary>
public class DesignerThemeResource : ResourceDictionary {
   public DesignerThemeResource() {
      if (Design.IsDesignMode) {
         var theme = new Theme(new string[0]);
         ThemeDictionary.Fill(this, theme);
      }
   }
}
