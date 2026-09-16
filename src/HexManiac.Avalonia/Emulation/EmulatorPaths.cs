using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HavenSoft.HexManiac.AvaloniaUI.MacPlatform;

namespace HavenSoft.HexManiac.AvaloniaUI.Emulation;

/// <summary>
/// Where the emulator keeps its files, and where it looks for a core to run.
///
/// HexManiacAdvance does not ship an emulator. A GBA core is a separate, separately licensed
/// binary, so the editor borrows one the user already has - in practice RetroArch's, which ships
/// its cores as frameworks inside the app bundle - or one they point us at explicitly.
/// </summary>
public static class EmulatorPaths {
   private static string EmulatorRoot {
      get {
         var dir = Path.Combine(MacFileSystem.AppSupportDirectory, "Emulator");
         Directory.CreateDirectory(dir);
         return dir;
      }
   }

   /// Where a core looks for BIOS images (mGBA runs fine without one, using its HLE BIOS).
   public static string SystemDirectory {
      get {
         var dir = Path.Combine(EmulatorRoot, "system");
         Directory.CreateDirectory(dir);
         return dir;
      }
   }

   /// Battery saves written by the core, kept out of the user's ROM folder.
   public static string SaveDirectory {
      get {
         var dir = Path.Combine(EmulatorRoot, "saves");
         Directory.CreateDirectory(dir);
         return dir;
      }
   }

   /// <summary>
   /// Where named save states live, per ROM.
   ///
   /// These are what makes "test this edit" quick: play to a spot once, name the state, and from
   /// then on every edit is Reload ROM away from being tested right there. They are per-ROM because
   /// a state from one game is meaningless in another.
   /// </summary>
   public static string StateDirectory(string romName) {
      var safe = new string((romName ?? "rom").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
      var dir = Path.Combine(EmulatorRoot, "states", safe);
      Directory.CreateDirectory(dir);
      return dir;
   }

   public static IReadOnlyList<string> SavedStates(string romName) {
      try {
         // Most recently written first: after a save that is the one you just made, and on a fresh
         // start it is the place you were last working - either way, the one to offer.
         return new DirectoryInfo(StateDirectory(romName)).EnumerateFiles("*.state")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => Path.GetFileNameWithoutExtension(file.Name)).ToList();
      } catch (IOException) {
         return Array.Empty<string>();
      }
   }

   public static string StatePath(string romName, string stateName) {
      var safe = new string((stateName ?? "state").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
      return Path.Combine(StateDirectory(romName), safe + ".state");
   }

   /// A core the user chose by hand, remembered across sessions.
   public static string ConfiguredCoreFile => Path.Combine(EmulatorRoot, "core-path.txt");

   public static string ConfiguredCore {
      get {
         try {
            if (!File.Exists(ConfiguredCoreFile)) return null;
            var path = File.ReadAllText(ConfiguredCoreFile).Trim();
            return File.Exists(path) ? path : null;
         } catch (IOException) {
            return null;
         }
      }
      set {
         try {
            if (string.IsNullOrEmpty(value)) File.Delete(ConfiguredCoreFile);
            else File.WriteAllText(ConfiguredCoreFile, value);
         } catch (IOException) {
            // remembering the choice is a convenience, not a requirement
         }
      }
   }

   /// <summary>
   /// Cores that can run a .gba, best first. mGBA is preferred because it is the most accurate of
   /// the widely available GBA cores; the others are listed so a user without it still gets
   /// something. RetroArch stores each core as Foo.libretro.framework/Versions/Current/Foo.libretro.
   /// </summary>
   private static readonly string[] PreferredCoreNames = { "mgba", "vbam", "vba.next", "gpsp" };

   public static IReadOnlyList<GbaCore> FindCores() {
      var found = new List<GbaCore>();

      var configured = ConfiguredCore;
      if (configured != null) found.Add(new GbaCore(Path.GetFileName(configured), configured, "chosen by you"));

      foreach (var root in RetroArchFrameworkDirectories()) {
         if (!Directory.Exists(root)) continue;
         foreach (var name in PreferredCoreNames) {
            var framework = Path.Combine(root, name + ".libretro.framework");
            var binary = Path.Combine(framework, "Versions", "Current", name + ".libretro");
            if (!File.Exists(binary)) binary = Path.Combine(framework, name + ".libretro");
            if (File.Exists(binary) && !found.Any(core => core.Path == binary)) {
               found.Add(new GbaCore(name, binary, "from RetroArch"));
            }
         }
      }

      // The plain-dylib layout, used by anyone who downloaded cores outside RetroArch.
      var coresDirectory = Path.Combine(EmulatorRoot, "cores");
      if (Directory.Exists(coresDirectory)) {
         foreach (var file in Directory.EnumerateFiles(coresDirectory, "*.dylib")) {
            if (!found.Any(core => core.Path == file)) {
               found.Add(new GbaCore(Path.GetFileNameWithoutExtension(file), file, "installed locally"));
            }
         }
      }

      return found;
   }

   private static IEnumerable<string> RetroArchFrameworkDirectories() {
      yield return "/Applications/RetroArch.app/Contents/Frameworks";
      yield return Path.Combine(
         Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
         "Applications", "RetroArch.app", "Contents", "Frameworks");
   }

   /// Where to send a user who has no core at all.
   public const string CoreHelpUrl = "https://www.retroarch.com/?page=platforms";
}

/// <param name="Name">Short core name, e.g. "mgba".</param>
/// <param name="Path">Absolute path to the dylib to load.</param>
/// <param name="Source">Human-readable note about where it came from.</param>
public record GbaCore(string Name, string Path, string Source);
