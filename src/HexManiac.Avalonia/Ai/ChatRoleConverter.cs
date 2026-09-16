using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace HavenSoft.HexManiac.AvaloniaUI.Ai;

/// <summary>
/// Turns a ChatRole into the style-class booleans the transcript template binds. Avalonia has no
/// DataTrigger, so "this bubble is a user message" is carried as a class rather than a trigger.
/// </summary>
public class ChatRoleConverter : IValueConverter {
   public static ChatRoleConverter IsUser { get; } = new(ChatRole.User);
   public static ChatRoleConverter IsTool { get; } = new(ChatRole.Tool);

   private readonly ChatRole match;
   private ChatRoleConverter(ChatRole match) => this.match = match;

   public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
      value is ChatRole role && role == match;

   public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
