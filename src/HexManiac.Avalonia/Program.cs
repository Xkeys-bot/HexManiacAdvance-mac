using System;
using Avalonia;

namespace HavenSoft.HexManiac.AvaloniaUI;

// Note: the namespace is "AvaloniaUI", not "Avalonia". A namespace ending in ".Avalonia"
// would shadow the real Avalonia namespaces inside this project and break every using.
internal static class Program {
   [STAThread]
   public static void Main(string[] args) =>
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

   public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>()
         .UsePlatformDetect()
         .WithInterFont()
         .LogToTrace();
}
