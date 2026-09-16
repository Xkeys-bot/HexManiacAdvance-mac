using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace HavenSoft.HexManiac.AvaloniaUI.Emulation;

/// <summary>
/// Owns a running core: the emulation thread, frame pacing, the latest picture, and audio.
///
/// The emulation thread is the only thread that touches the core. The UI thread interacts through
/// three narrow channels - button bits (atomic, inside LibretroCore), a command flag checked once
/// per frame, and <see cref="CopyLatestFrame"/>, which takes a lock the emulation thread holds for
/// only the length of one array copy.
/// </summary>
public sealed class EmulatorSession : IDisposable {
   private readonly object frameGate = new();
   private uint[] latestFrame = Array.Empty<uint>();
   private int frameWidth, frameHeight;
   private long frameCounter;

   private LibretroCore core;
   private EmulatorAudio audio;
   private Thread thread;
   private volatile bool running, paused, stopRequested;
   private byte[] pendingRom;
   private string pendingRomName;
   private bool pendingKeepPlace;
   private byte[] loadedRom;        // kept so Reset can reboot without retro_reset
   private string loadedRomName;

   public string CorePath => core?.CorePath;
   public string CoreName => core?.LibraryName ?? "no core";
   public string CoreVersion => core?.LibraryVersion ?? string.Empty;
   public int ScreenWidth => core?.ScreenWidth ?? 240;
   public int ScreenHeight => core?.ScreenHeight ?? 160;
   public bool IsRunning => running;
   public bool IsPaused => paused;
   public bool AudioEnabled => audio?.IsRunning ?? false;
   public string AudioFailureReason => audio?.FailureReason;
   public double MeasuredFps { get; private set; }

   /// Raised on the emulation thread if the core throws. The window shows it and stops.
   public event Action<Exception> Faulted;

   /// Raised (on the emulation thread) whenever a new frame is available, so the view can repaint
   /// on demand instead of polling a fixed timer.
   public event Action FramePresented;

   public void Start(string corePath, byte[] rom, string romName) {
      Stop();

      startedCorePath = corePath;
      core = new LibretroCore(corePath);
      core.FrameReady += StoreFrame;
      core.LoadRom(rom, romName);
      loadedRom = rom;
      loadedRomName = romName;

      audio = new EmulatorAudio(core.SampleRate);
      core.SamplesReady += (samples, count) => audio.Push(samples, count);

      stopRequested = false;
      paused = false;
      running = true;
      thread = new Thread(EmulationLoop) {
         Name = "HexManiac Emulator",
         IsBackground = true,
         // Frame pacing wants to be woken on time; the work per frame is small.
         Priority = ThreadPriority.AboveNormal,
      };
      thread.Start();
   }

   /// <summary>
   /// Reboots the core on the ROM as it stands right now. This is the button that makes the
   /// feature worth having: edit a map or a script in the editor, press it, and the game boots the
   /// edited data without anything being written to disk.
   /// </summary>
   /// <param name="keepPlace">
   /// Save the console's exact state first and restore it after the new ROM is in, so you stay
   /// where you were instead of being dropped back at the title screen. It does not always work -
   /// a save state refers to memory as the old ROM laid it out - so the result is reported through
   /// <see cref="ReloadFinished"/> rather than assumed.
   /// </param>
   public void ReloadRom(byte[] rom, string romName, bool keepPlace = false) {
      if (!running) return;
      Volatile.Write(ref loadedRom, rom);
      loadedRomName = romName;
      pendingRomName = romName;
      pendingKeepPlace = keepPlace;
      Volatile.Write(ref pendingRom, rom);
   }

   /// Raised on the emulation thread after a reload: true when the previous place was restored.
   public event Action<bool> ReloadFinished;

   private string startedCorePath;

   /// <summary>
   /// Restarts the game by throwing the core away and building a new one on the bytes the editor
   /// currently holds. Verified to reboot the game on mGBA and VBA-M.
   ///
   /// The lighter routes are not used. `retro_reset` used to take the process down with it - that
   /// turned out to be the null log callback, and it no longer crashes - but a core is free to
   /// implement it however it likes, and a reset that silently declines is worse than no reset
   /// button. Reloading through `retro_unload_game` + `retro_load_game` on the same instance is
   /// the same gamble. A fresh instance is the one route that cannot be quietly ignored, and it
   /// costs the second or two it takes to load the ROM again - which is what a console's reset
   /// feels like anyway. It also picks up any edits made since the game started.
   /// </summary>
   public void Reset() {
      var rom = Volatile.Read(ref loadedRom);
      var corePath = startedCorePath;
      if (rom == null || corePath == null) return;
      Start(corePath, rom, loadedRomName);
   }

   public void SetPaused(bool value) {
      paused = value;
      audio?.SetPaused(value);
      if (value) core?.ClearButtons();
   }

   public void SetButton(Libretro.JoypadButton button, bool pressed) => core?.SetButton(button, pressed);

   public void ClearButtons() => core?.ClearButtons();

   /// Diagnostic: the button bitmask the core is currently being handed, and the last one it
   /// actually sampled.
   public int PressedButtons => core?.PressedButtons ?? 0;
   public int LastLatchedButtons => core?.LastLatchedButtons ?? 0;
   public double SecondsSinceLastLatch => core?.SecondsSinceLastLatch ?? double.MaxValue;

   public byte[] SaveState() => core?.SaveState();
   public bool LoadState(byte[] state) => core?.LoadState(state) ?? false;

   /// <summary>
   /// Copies the most recent frame into <paramref name="destination"/>, resizing it if the core
   /// changed geometry. Returns false when there is nothing new since <paramref name="lastSeen"/>,
   /// so the view can skip a repaint entirely.
   /// </summary>
   public bool CopyLatestFrame(ref uint[] destination, ref int width, ref int height, ref long lastSeen) {
      lock (frameGate) {
         if (frameCounter == lastSeen || frameWidth == 0) return false;
         if (destination == null || destination.Length != latestFrame.Length) destination = new uint[latestFrame.Length];
         Array.Copy(latestFrame, destination, latestFrame.Length);
         width = frameWidth;
         height = frameHeight;
         lastSeen = frameCounter;
         return true;
      }
   }

   /// <summary>What live memory the running core exposes. Empty when nothing is running.</summary>
   public IReadOnlyList<MemoryRegion> MemoryRegions => core?.MemoryRegions ?? Array.Empty<MemoryRegion>();

   /// <summary>
   /// Reads the running game's memory. Safe to call from any thread: the pointers the core hands
   /// out stay valid for the life of the loaded game, and a torn read of a byte or two is a far
   /// better trade than stalling the emulation thread for a memory inspector.
   /// </summary>
   public byte[] ReadMemory(uint id, int offset, int length) => core?.ReadMemory(id, offset, length);

   public bool WriteMemory(uint id, int offset, byte[] bytes) => core?.WriteMemory(id, offset, bytes) ?? false;

   /// <summary>A one-shot copy of the current picture, for callers with no frame bookkeeping.</summary>
   public bool TryGetFrame(out uint[] pixels, out int width, out int height) {
      lock (frameGate) {
         if (frameWidth == 0 || latestFrame.Length == 0) {
            pixels = null;
            width = height = 0;
            return false;
         }
         pixels = (uint[])latestFrame.Clone();
         width = frameWidth;
         height = frameHeight;
         return true;
      }
   }

   private void StoreFrame(uint[] pixels, int width, int height) {
      lock (frameGate) {
         if (latestFrame.Length != pixels.Length) latestFrame = new uint[pixels.Length];
         Array.Copy(pixels, latestFrame, pixels.Length);
         frameWidth = width;
         frameHeight = height;
         frameCounter++;
      }
      FramePresented?.Invoke();
   }

   private void EmulationLoop() {
      var clock = Stopwatch.StartNew();
      var frameTicks = Stopwatch.Frequency / core.FramesPerSecond;
      var nextFrame = clock.ElapsedTicks + frameTicks;
      var fpsWindowStart = clock.ElapsedTicks;
      var framesThisWindow = 0;

      try {
         while (!stopRequested) {
            var swap = Interlocked.Exchange(ref pendingRom, null);
            if (swap != null) {
               // Snapshot before the new ROM goes in. SaveState can legitimately fail (a core may
               // refuse mid-frame), and that is not a reason to abandon the reload.
               byte[] place = null;
               if (pendingKeepPlace) {
                  try { place = core.SaveState(); } catch (Exception) { place = null; }
               }

               core.LoadRom(swap, pendingRomName);

               var restored = false;
               if (place != null) {
                  // A state written against the old ROM may simply be rejected, which leaves the
                  // fresh boot in place - the safe outcome, and the one we report.
                  try { restored = core.LoadState(place); } catch (Exception) { restored = false; }
               }
               ReloadFinished?.Invoke(restored);
               audio?.Clear();
               // Geometry or timing can change with the new game; re-derive the pace.
               frameTicks = Stopwatch.Frequency / core.FramesPerSecond;
               nextFrame = clock.ElapsedTicks + frameTicks;
            }

            if (paused) {
               Thread.Sleep(16);
               nextFrame = clock.ElapsedTicks + frameTicks;
               continue;
            }

            core.RunFrame();
            framesThisWindow++;

            var windowTicks = clock.ElapsedTicks - fpsWindowStart;
            if (windowTicks > Stopwatch.Frequency) {
               MeasuredFps = framesThisWindow * Stopwatch.Frequency / (double)windowTicks;
               framesThisWindow = 0;
               fpsWindowStart = clock.ElapsedTicks;
            }

            // Sleep for the bulk of the wait and spin for the tail: Thread.Sleep on macOS
            // routinely overshoots by several milliseconds, which at 60Hz is a visibly uneven
            // frame rate, but spinning the whole interval would burn a core for nothing.
            nextFrame += frameTicks;
            while (true) {
               var remaining = nextFrame - clock.ElapsedTicks;
               if (remaining <= 0) break;
               var milliseconds = remaining * 1000.0 / Stopwatch.Frequency;
               if (milliseconds > 2) Thread.Sleep((int)(milliseconds - 1));
               else Thread.SpinWait(200);
            }

            // If we have fallen far behind (the app was suspended, a dialog blocked us), give up
            // on catching up rather than running a burst of fast-forwarded frames.
            if (clock.ElapsedTicks - nextFrame > frameTicks * 4) nextFrame = clock.ElapsedTicks + frameTicks;
         }
      } catch (Exception e) {
         running = false;
         Faulted?.Invoke(e);
      }
   }

   public void Stop() {
      stopRequested = true;
      running = false;
      if (thread != null && thread.IsAlive) thread.Join(TimeSpan.FromSeconds(2));
      thread = null;

      audio?.Dispose();
      audio = null;

      if (core != null) {
         core.FrameReady -= StoreFrame;
         core.Dispose();
         core = null;
      }

      lock (frameGate) {
         latestFrame = Array.Empty<uint>();
         frameWidth = frameHeight = 0;
         frameCounter = 0;
      }
   }

   public void Dispose() => Stop();
}
