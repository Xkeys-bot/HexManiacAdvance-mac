using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia.Input;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// Chorded shortcuts such as "Ctrl+D, T" (MainWindow.xaml's DisplayAs bindings).
///
/// WPF subclassed KeyGesture and overrode Matches() so a KeyBinding could own the chord state.
/// Avalonia's KeyGesture is sealed and its KeyBinding does a plain equality match, so the chord
/// is tracked here instead and driven from a single KeyDown handler on the window.
/// </summary>
public class MultiKeyGesture {
   readonly KeyModifiers mods;
   readonly IReadOnlyList<Key> keys;
   int nextKey;

   public ICommand Command { get; }

   public MultiKeyGesture(ICommand command, KeyModifiers mods, params Key[] keys) {
      Command = command;
      this.mods = mods;
      this.keys = keys;
   }

   /// <summary>
   /// Feed each KeyDown in. Returns true on the key that completes the chord, and resets so the
   /// next press starts over -- the same state machine WPF's Matches() ran.
   /// </summary>
   public bool Matches(KeyEventArgs args) {
      if (nextKey == 0) {
         nextKey = args.Key == keys[0] && args.KeyModifiers == mods ? 1 : 0;
         return false;
      } else if (nextKey == keys.Count - 1) {
         nextKey = 0;
         return args.Key == keys[keys.Count - 1];
      } else {
         nextKey = args.Key == keys[nextKey] ? nextKey + 1 : 0;
         return false;
      }
   }

   public void Reset() => nextKey = 0;

   /// <summary>
   /// Parses the same text WPF's MultiKeyGestureExtension took, e.g. ("Ctrl+D", "T").
   /// "Ctrl" maps to the platform's command modifier, so these read as Cmd chords on macOS.
   /// </summary>
   public static MultiKeyGesture Parse(ICommand command, params string[] textPieces) {
      var text = string.Join(", ", textPieces);
      var mods = KeyModifiers.None;
      if (text.Contains("+")) {
         var parts = text.Split('+');
         text = parts[1];
         mods = ParseModifiers(parts[0]);
      }
      var keys = text.Split(',').Select(k => Enum.Parse<Key>(k.Trim(), ignoreCase: true)).ToArray();
      return new MultiKeyGesture(command, mods, keys);
   }

   static KeyModifiers ParseModifiers(string text) {
      var result = KeyModifiers.None;
      foreach (var piece in text.Split('+', StringSplitOptions.RemoveEmptyEntries)) {
         result |= piece.Trim().ToLowerInvariant() switch {
            // WPF's "Ctrl" is the command key on macOS, matching PORTING.md's Ctrl -> Meta rule.
            "ctrl" or "control" => PlatformCommandModifier,
            "shift" => KeyModifiers.Shift,
            "alt" => KeyModifiers.Alt,
            "win" or "windows" or "meta" or "cmd" => KeyModifiers.Meta,
            _ => KeyModifiers.None,
         };
      }
      return result;
   }

   public static KeyModifiers PlatformCommandModifier { get; } =
      OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
}

/// <summary>
/// Holds every chord for one window and routes KeyDown through them.
/// </summary>
public class MultiKeyGestureSet {
   readonly List<MultiKeyGesture> gestures = new();

   public void Add(MultiKeyGesture gesture) => gestures.Add(gesture);

   public bool HandleKeyDown(KeyEventArgs args) {
      var handled = false;
      foreach (var gesture in gestures) {
         if (!gesture.Matches(args)) continue;
         if (gesture.Command?.CanExecute(null) ?? false) gesture.Command.Execute(null);
         handled = true;
      }
      if (handled) foreach (var gesture in gestures) gesture.Reset();
      return handled;
   }
}
