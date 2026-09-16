using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

public partial class TextBoxLookAlike : Border {
   /// The TextBlock AngleTextBox binds against. Resolved explicitly rather than through the
   /// generated field: naming the element "TextBlock" (its own type name) left the field null.
   public TextBlock Text { get; private set; }

   public TextBoxLookAlike() {
      InitializeComponent();
      Text = this.FindControl<TextBlock>("ContentText");
   }

}
