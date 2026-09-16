using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HavenSoft.HexManiac.AvaloniaUI.Emulation;

/// <summary>
/// A loaded libretro core, driven one frame at a time.
///
/// Not thread safe by itself: every method except <see cref="SetButton"/> must be called from the
/// single thread that owns the core (EmulatorSession's emulation thread). Input is the exception,
/// because the UI thread writes it while the emulation thread reads it.
///
/// The core is never told about a file on disk. <see cref="LoadRom"/> hands it a copy of the bytes
/// HexManiac currently has in memory, which is the whole point: the emulator runs the edits, saved
/// or not. mGBA reports need_fullpath = false, which is what makes that legal.
/// </summary>
public sealed class LibretroCore : IDisposable {
   private readonly IntPtr library;

   // Delegates handed to the core. These fields exist to keep them alive: the core holds raw
   // function pointers, and a collected delegate is a crash the GC gives no warning about.
   private readonly Libretro.EnvironmentCallback environmentCallback;
   private readonly Libretro.VideoRefreshCallback videoCallback;
   private readonly Libretro.AudioSampleCallback audioSampleCallback;
   private readonly Libretro.AudioSampleBatchCallback audioBatchCallback;
   private readonly Libretro.InputPollCallback inputPollCallback;
   private readonly Libretro.InputStateCallback inputStateCallback;

   private readonly Libretro.VoidFn init, deinit, run, reset, unloadGame;
   private readonly Libretro.GetSystemInfoFn getSystemInfo;
   private readonly Libretro.GetSystemAvInfoFn getSystemAvInfo;
   private readonly Libretro.LoadGameFn loadGame;
   private readonly Libretro.SetControllerPortDeviceFn setControllerPortDevice;
   private readonly Libretro.LogPrintfFn logCallback;
   private readonly Libretro.SerializeSizeFn serializeSize;
   private readonly Libretro.SerializeFn serialize, unserialize;
   private readonly Libretro.GetMemoryDataFn getMemoryData;
   private readonly Libretro.GetMemorySizeFn getMemorySize;

   private readonly string systemDirectory, saveDirectory;
   private readonly IntPtr systemDirectoryPtr, saveDirectoryPtr;

   /// <summary>
   /// Button state, written by the UI thread and read by the emulation thread. One bit per
   /// RETRO_DEVICE_ID_JOYPAD_* id; Interlocked keeps the two threads honest without a lock on the
   /// hot path (the core asks for every button, every frame).
   ///
   /// <see cref="buttons"/> is what is physically held right now. <see cref="holdPolls"/> is the
   /// floor under it: a tap that begins and ends between two of the core's input polls would
   /// otherwise be invisible to the game, so every press also arms a countdown and the button
   /// keeps reading as down until that countdown runs out. See <see cref="MinimumHoldPolls"/>.
   /// </summary>
   private int buttons, polledButtons;
   private readonly int[] holdPolls = new int[16];

   /// <summary>
   /// How many input polls (frames) a press is guaranteed to be visible for.
   ///
   /// One frame is what the hardware technically needs - a game reads the key register once per
   /// frame and a single frame of "down" is a new press - but it is not what any real core
   /// delivers reliably, and it is not what a human does either: the shortest deliberate tap on a
   /// keyboard is 50-100ms, i.e. three to six frames. At one frame, taps sent by the MCP tools or
   /// by a quick keypress landed on the title screen and did nothing perhaps five times out of
   /// six. Four frames is still far too short to feel sticky and lands every time.
   /// </summary>
   private const int MinimumHoldPolls = 4;

   private IntPtr romBuffer;
   private Libretro.PixelFormat pixelFormat = Libretro.PixelFormat.ZeroRgb1555;
   private bool gameLoaded, disposed;

   public string CorePath { get; }
   public string LibraryName { get; private set; } = "unknown";
   public string LibraryVersion { get; private set; } = string.Empty;
   public bool NeedsFullPath { get; private set; }

   public int ScreenWidth { get; private set; } = 240;
   public int ScreenHeight { get; private set; } = 160;
   public double FramesPerSecond { get; private set; } = 59.7275;
   public double SampleRate { get; private set; } = 32768;

   /// Raised on the emulation thread with a finished frame already converted to BGRA.
   public event Action<uint[], int, int> FrameReady;

   /// Raised on the emulation thread with interleaved 16-bit stereo samples.
   public event Action<short[], int> SamplesReady;

   private uint[] frameBuffer = Array.Empty<uint>();
   private short[] sampleBuffer = new short[4096];

   public LibretroCore(string corePath) {
      CorePath = corePath;
      library = NativeLibrary.Load(corePath);

      T Bind<T>(string name) where T : Delegate =>
         (T)Marshal.GetDelegateForFunctionPointer(NativeLibrary.GetExport(library, name), typeof(T));

      var apiVersion = Bind<Libretro.ApiVersionFn>("retro_api_version")();
      if (apiVersion != 1) {
         NativeLibrary.Free(library);
         throw new NotSupportedException($"{Path.GetFileName(corePath)} reports libretro API {apiVersion}; this host speaks API 1.");
      }

      init = Bind<Libretro.VoidFn>("retro_init");
      deinit = Bind<Libretro.VoidFn>("retro_deinit");
      run = Bind<Libretro.VoidFn>("retro_run");
      reset = Bind<Libretro.VoidFn>("retro_reset");
      unloadGame = Bind<Libretro.VoidFn>("retro_unload_game");
      getSystemInfo = Bind<Libretro.GetSystemInfoFn>("retro_get_system_info");
      getSystemAvInfo = Bind<Libretro.GetSystemAvInfoFn>("retro_get_system_av_info");
      loadGame = Bind<Libretro.LoadGameFn>("retro_load_game");
      setControllerPortDevice = Bind<Libretro.SetControllerPortDeviceFn>("retro_set_controller_port_device");
      serializeSize = Bind<Libretro.SerializeSizeFn>("retro_serialize_size");
      serialize = Bind<Libretro.SerializeFn>("retro_serialize");
      unserialize = Bind<Libretro.SerializeFn>("retro_unserialize");
      getMemoryData = Bind<Libretro.GetMemoryDataFn>("retro_get_memory_data");
      getMemorySize = Bind<Libretro.GetMemorySizeFn>("retro_get_memory_size");

      getSystemInfo(out var info);
      LibraryName = Marshal.PtrToStringUTF8(info.LibraryName) ?? "unknown";
      LibraryVersion = Marshal.PtrToStringUTF8(info.LibraryVersion) ?? string.Empty;
      NeedsFullPath = info.NeedFullPath;

      systemDirectory = EmulatorPaths.SystemDirectory;
      saveDirectory = EmulatorPaths.SaveDirectory;
      systemDirectoryPtr = Marshal.StringToHGlobalAnsi(systemDirectory);
      saveDirectoryPtr = Marshal.StringToHGlobalAnsi(saveDirectory);

      environmentCallback = HandleEnvironment;
      videoCallback = HandleVideo;
      audioSampleCallback = HandleAudioSample;
      audioBatchCallback = HandleAudioBatch;
      logCallback = WriteCoreLog;
      inputPollCallback = LatchInput;
      inputStateCallback = HandleInputState;

      Bind<Libretro.SetEnvironmentFn>("retro_set_environment")(environmentCallback);
      Bind<Libretro.SetVideoRefreshFn>("retro_set_video_refresh")(videoCallback);
      Bind<Libretro.SetAudioSampleFn>("retro_set_audio_sample")(audioSampleCallback);
      Bind<Libretro.SetAudioSampleBatchFn>("retro_set_audio_sample_batch")(audioBatchCallback);
      Bind<Libretro.SetInputPollFn>("retro_set_input_poll")(inputPollCallback);
      Bind<Libretro.SetInputStateFn>("retro_set_input_state")(inputStateCallback);

      init();
   }

   /// <summary>
   /// Boots the given ROM image. The bytes are copied into unmanaged memory for the duration of
   /// the call: the libretro contract says the core must take its own copy, but pinning a
   /// HexManiac model array across a native call that can run for a while is not worth the risk -
   /// and a copy also guarantees the emulator keeps running the ROM as it was at boot, rather than
   /// silently following later edits.
   /// </summary>
   public void LoadRom(byte[] rom, string displayPath) {
      if (gameLoaded) { unloadGame(); gameLoaded = false; }

      // A core may inspect this path's extension to pick a system, so make sure it has one.
      var path = displayPath ?? string.Empty;
      if (!path.EndsWith(".gba", StringComparison.OrdinalIgnoreCase)) path += ".gba";

      // The buffer has to outlive the call. libretro says a core must copy what it needs, but not
      // all of them do - mGBA copies, VBA-M keeps the pointer and reads through it while it runs,
      // which turned freeing this into a use-after-free and a silent native crash. Hold it until
      // the next load or until the core is disposed; one spare copy of the ROM is a cheap fix.
      FreeRomCopy();
      romBuffer = Marshal.AllocHGlobal(rom.Length);
      var pathPtr = Marshal.StringToHGlobalAnsi(path);
      try {
         Marshal.Copy(rom, 0, romBuffer, rom.Length);
         var info = new Libretro.GameInfo {
            Path = pathPtr,
            Data = romBuffer,
            Size = (UIntPtr)rom.Length,
            Meta = IntPtr.Zero,
         };
         if (!loadGame(ref info)) {
            FreeRomCopy();
            throw new InvalidOperationException($"{LibraryName} refused to load the ROM.");
         }
      } finally {
         Marshal.FreeHGlobal(pathPtr);
      }
      gameLoaded = true;

      getSystemAvInfo(out var av);
      ScreenWidth = (int)av.Geometry.BaseWidth;
      ScreenHeight = (int)av.Geometry.BaseHeight;
      if (av.Timing.Fps > 1) FramesPerSecond = av.Timing.Fps;
      if (av.Timing.SampleRate > 1) SampleRate = av.Timing.SampleRate;
      frameBuffer = new uint[ScreenWidth * ScreenHeight];

      // retro_set_controller_port_device, called here and nowhere else. The spec says a port
      // already defaults to RETRO_DEVICE_JOYPAD, so this "should" be redundant - but VBA-M only
      // starts asking for pad buttons once it has been told, and until then it polls nothing but
      // its sensor ids and the game ignores the controller completely.
      //
      // The timing is the whole trick: called before retro_load_game it dereferences null and
      // takes the editor down with a SIGSEGV no managed handler can catch, which is why an earlier
      // round removed the call outright. After the game is loaded it is safe.
      setControllerPortDevice(0, Libretro.DeviceJoypad);
   }

   private void FreeRomCopy() {
      if (romBuffer == IntPtr.Zero) return;
      Marshal.FreeHGlobal(romBuffer);
      romBuffer = IntPtr.Zero;
   }

   public void RunFrame() => run();

   public void Reset() { if (gameLoaded) reset(); }

   public void SetButton(Libretro.JoypadButton button, bool pressed) {
      var id = (int)button;
      if (id < 0 || id >= holdPolls.Length) return;
      var mask = 1 << id;
      int original, updated;
      do {
         original = Volatile.Read(ref buttons);
         updated = pressed ? original | mask : original & ~mask;
      } while (Interlocked.CompareExchange(ref buttons, updated, original) != original);
      // Releasing does not cancel the floor: the whole point is that a press the core never got
      // to see still counts. Holding longer than the floor is fine - `buttons` carries it.
      if (pressed) Interlocked.Exchange(ref holdPolls[id], MinimumHoldPolls);
   }

   public void ClearButtons() {
      Volatile.Write(ref buttons, 0);
      for (int i = 0; i < holdPolls.Length; i++) Interlocked.Exchange(ref holdPolls[i], 0);
      Volatile.Write(ref polledButtons, 0);
   }

   /// The core is about to sample input for this frame: whatever is held now, plus anything whose
   /// guaranteed hold has not run out yet. Counting down here rather than on a timer ties the hold
   /// to frames the game actually ran, so it survives a pause, a stall, or a slow core.
   private void LatchInput() {
      var mask = Volatile.Read(ref buttons);
      for (int i = 0; i < holdPolls.Length; i++) {
         if (Volatile.Read(ref holdPolls[i]) <= 0) continue;
         mask |= 1 << i;
         Interlocked.Decrement(ref holdPolls[i]);
      }
      Volatile.Write(ref polledButtons, mask);
      if (mask != 0) {
         Volatile.Write(ref lastLatched, mask);
         Volatile.Write(ref lastLatchedAt, Stopwatch.GetTimestamp());
      }
   }

   /// What the console is being told right now, as a RETRO_DEVICE_ID_JOYPAD_* bitmask. Diagnostic
   /// only - this is the value <see cref="HandleInputState"/> answers from.
   public int PressedButtons => Volatile.Read(ref polledButtons);

   /// <summary>
   /// The last non-empty mask the core actually sampled, and how long ago. A press lasts four
   /// frames, so anything polling this at UI rates would almost always miss it live; remembering
   /// the last one makes it visible. It is recorded inside the core's own input poll, so it is
   /// evidence the core sampled the press rather than evidence we set a bit.
   /// </summary>
   public int LastLatchedButtons => Volatile.Read(ref lastLatched);
   public double SecondsSinceLastLatch {
      get {
         var at = Volatile.Read(ref lastLatchedAt);
         return at == 0 ? double.MaxValue : (Stopwatch.GetTimestamp() - at) / (double)Stopwatch.Frequency;
      }
   }

   private int lastLatched;
   private long lastLatchedAt;

   public byte[] SaveState() {
      var size = (int)serializeSize();
      if (size <= 0) return null;
      var buffer = Marshal.AllocHGlobal(size);
      try {
         if (!serialize(buffer, (UIntPtr)size)) return null;
         var managed = new byte[size];
         Marshal.Copy(buffer, managed, 0, size);
         return managed;
      } finally {
         Marshal.FreeHGlobal(buffer);
      }
   }

   public bool LoadState(byte[] state) {
      if (state == null || state.Length == 0) return false;
      var buffer = Marshal.AllocHGlobal(state.Length);
      try {
         Marshal.Copy(state, 0, buffer, state.Length);
         return unserialize(buffer, (UIntPtr)state.Length);
      } finally {
         Marshal.FreeHGlobal(buffer);
      }
   }

   /// The emulator's battery save, so a test run's progress can be carried back out if wanted.
   public byte[] ReadSaveRam() {
      var size = (int)getMemorySize(Libretro.MemorySaveRam);
      var data = getMemoryData(Libretro.MemorySaveRam);
      if (size <= 0 || data == IntPtr.Zero) return null;
      var managed = new byte[size];
      Marshal.Copy(data, managed, 0, size);
      return managed;
   }

   /// <summary>
   /// The memory regions this core publishes, with the GBA address each one corresponds to.
   ///
   /// Which regions exist, and what they mean, is up to the core: mGBA maps SYSTEM_RAM to the GBA's
   /// 32KB IWRAM, while VBA-M, VBA-Next and gpSP map it to the 256KB EWRAM where a Pokemon game
   /// keeps its flags, variables and party. Rather than hard-code one core's choice, the address is
   /// inferred from the size - which is unambiguous, because the two regions are different sizes.
   /// </summary>
   public IReadOnlyList<MemoryRegion> MemoryRegions {
      get {
         var regions = new List<MemoryRegion>();
         void Add(uint id, string name) {
            var size = (int)getMemorySize(id);
            if (size <= 0 || getMemoryData(id) == IntPtr.Zero) return;
            var address = id switch {
               Libretro.MemorySystemRam => size == 0x8000 ? 0x03000000u : 0x02000000u,
               Libretro.MemoryVideoRam => 0x06000000u,
               Libretro.MemorySaveRam => 0x0E000000u,
               _ => 0u,
            };
            var label = id == Libretro.MemorySystemRam && size == 0x8000 ? "IWRAM" : name;
            regions.Add(new MemoryRegion(id, label, address, size));
         }
         Add(Libretro.MemorySaveRam, "Save RAM");
         Add(Libretro.MemorySystemRam, "EWRAM");
         Add(Libretro.MemoryVideoRam, "VRAM");
         return regions;
      }
   }

   /// <summary>Copies live memory out of the running game. Returns null if the region is absent.</summary>
   public byte[] ReadMemory(uint id, int offset, int length) {
      var size = (int)getMemorySize(id);
      var data = getMemoryData(id);
      if (size <= 0 || data == IntPtr.Zero) return null;
      if (offset < 0 || offset >= size) return null;
      length = Math.Min(length, size - offset);
      var managed = new byte[length];
      Marshal.Copy(data + offset, managed, 0, length);
      return managed;
   }

   /// <summary>Writes into the running game's memory. False if the region is absent or too small.</summary>
   public bool WriteMemory(uint id, int offset, byte[] bytes) {
      var size = (int)getMemorySize(id);
      var data = getMemoryData(id);
      if (size <= 0 || data == IntPtr.Zero) return false;
      if (offset < 0 || offset + bytes.Length > size) return false;
      Marshal.Copy(bytes, 0, data + offset, bytes.Length);
      return true;
   }

   #region Callbacks from the core

   private bool HandleEnvironment(uint command, IntPtr data) {
      switch ((Libretro.EnvironmentCommand)command) {
         case Libretro.EnvironmentCommand.GetCanDupe:
            // "May I skip the video callback on a duplicated frame?" Yes - the screen keeps the
            // previous frame, which is exactly what a dupe means.
            if (data != IntPtr.Zero) Marshal.WriteByte(data, 1);
            return true;
         case Libretro.EnvironmentCommand.GetOverscan:
            if (data != IntPtr.Zero) Marshal.WriteByte(data, 0);
            return true;
         case Libretro.EnvironmentCommand.SetPixelFormat:
            if (data == IntPtr.Zero) return false;
            var requested = (Libretro.PixelFormat)(uint)Marshal.ReadInt32(data);
            if (requested != Libretro.PixelFormat.Rgb565 && requested != Libretro.PixelFormat.XRgb8888) return false;
            pixelFormat = requested;
            return true;
         case Libretro.EnvironmentCommand.GetSystemDirectory:
         case Libretro.EnvironmentCommand.GetCoreAssetsDirectory:
            if (data != IntPtr.Zero) Marshal.WriteIntPtr(data, systemDirectoryPtr);
            return true;
         case Libretro.EnvironmentCommand.GetSaveDirectory:
            if (data != IntPtr.Zero) Marshal.WriteIntPtr(data, saveDirectoryPtr);
            return true;
         case Libretro.EnvironmentCommand.SetInputDescriptors:
         case Libretro.EnvironmentCommand.SetPerformanceLevel:
         case Libretro.EnvironmentCommand.SetVariables:
         case Libretro.EnvironmentCommand.SetMessage:
            return true;  // accepted and ignored
         case Libretro.EnvironmentCommand.GetVariable:
            // Refusing every option query leaves the core on its own defaults, which is what we
            // want: HexManiac is not a RetroArch replacement and has no core-options UI.
            return false;
         case Libretro.EnvironmentCommand.GetLogInterface:
            // Handing the core a logger is not a nicety. VBA-M stores whatever this returns and
            // calls it unconditionally - it installs no fallback when the host declines - so a
            // refusal here leaves a null function pointer that the core jumps through the moment
            // it wants to say anything. That is the SIGSEGV at address 0 seen from both
            // retro_init and retro_set_controller_port_device, and it is why an earlier round
            // concluded those entry points were simply broken and stopped calling one of them.
            if (data == IntPtr.Zero) return false;
            Marshal.WriteIntPtr(data, Marshal.GetFunctionPointerForDelegate(logCallback));
            return true;
         case Libretro.EnvironmentCommand.GetVariableUpdate:
            if (data != IntPtr.Zero) Marshal.WriteByte(data, 0);
            return true;
         case Libretro.EnvironmentCommand.GetLanguage:
            if (data != IntPtr.Zero) Marshal.WriteInt32(data, 0); // RETRO_LANGUAGE_ENGLISH
            return true;
         case Libretro.EnvironmentCommand.SetSystemAvInfo:
         case Libretro.EnvironmentCommand.SetGeometry:
            if (data == IntPtr.Zero) return false;
            ApplyGeometryChange(command, data);
            return true;
         default:
            // Everything else - perf counters, the experimental range, RETRO_ENVIRONMENT_
            // GET_INPUT_BITMASKS - is declined. The spec requires cores to cope. Note that
            // HandleInputState answers the bitmask id anyway: answering a question without
            // claiming the feature costs nothing and works for cores on either side of it.
            return false;
      }
   }

   private void ApplyGeometryChange(uint command, IntPtr data) {
      Libretro.GameGeometry geometry;
      if ((Libretro.EnvironmentCommand)command == Libretro.EnvironmentCommand.SetGeometry) {
         geometry = Marshal.PtrToStructure<Libretro.GameGeometry>(data);
      } else {
         var av = Marshal.PtrToStructure<Libretro.SystemAvInfo>(data);
         geometry = av.Geometry;
         if (av.Timing.Fps > 1) FramesPerSecond = av.Timing.Fps;
         if (av.Timing.SampleRate > 1) SampleRate = av.Timing.SampleRate;
      }
      if (geometry.BaseWidth == 0 || geometry.BaseHeight == 0) return;
      ScreenWidth = (int)geometry.BaseWidth;
      ScreenHeight = (int)geometry.BaseHeight;
      if (frameBuffer.Length != ScreenWidth * ScreenHeight) frameBuffer = new uint[ScreenWidth * ScreenHeight];
   }

   private void HandleVideo(IntPtr data, uint width, uint height, UIntPtr pitch) {
      // A null frame means "same picture as last time"; we said GetCanDupe, so just skip it.
      if (data == IntPtr.Zero || width == 0 || height == 0) return;

      var w = (int)width;
      var h = (int)height;
      if (frameBuffer.Length != w * h) frameBuffer = new uint[w * h];
      var stride = (int)pitch;

      if (pixelFormat == Libretro.PixelFormat.Rgb565) ConvertRgb565(data, w, h, stride, frameBuffer);
      else ConvertXrgb8888(data, w, h, stride, frameBuffer);

      FrameReady?.Invoke(frameBuffer, w, h);
   }

   /// <summary>
   /// RGB565 to the BGRA byte order Avalonia's Bgra8888 bitmaps want. Each channel is expanded by
   /// replicating its high bits into the low ones (5 bits -> b<<3 | b>>2), so full-scale input maps
   /// to full-scale output instead of topping out at 248.
   /// </summary>
   private static unsafe void ConvertRgb565(IntPtr source, int width, int height, int stride, uint[] destination) {
      fixed (uint* destStart = destination) {
         for (int y = 0; y < height; y++) {
            var row = (ushort*)((byte*)source + y * stride);
            var dest = destStart + y * width;
            for (int x = 0; x < width; x++) {
               var value = row[x];
               uint r = (uint)((value >> 11) & 0x1F);
               uint g = (uint)((value >> 5) & 0x3F);
               uint b = (uint)(value & 0x1F);
               r = (r << 3) | (r >> 2);
               g = (g << 2) | (g >> 4);
               b = (b << 3) | (b >> 2);
               dest[x] = 0xFF000000u | (r << 16) | (g << 8) | b;
            }
         }
      }
   }

   private static unsafe void ConvertXrgb8888(IntPtr source, int width, int height, int stride, uint[] destination) {
      fixed (uint* destStart = destination) {
         for (int y = 0; y < height; y++) {
            var row = (uint*)((byte*)source + y * stride);
            var dest = destStart + y * width;
            for (int x = 0; x < width; x++) dest[x] = row[x] | 0xFF000000u;
         }
      }
   }

   private void HandleAudioSample(short left, short right) {
      sampleBuffer[0] = left;
      sampleBuffer[1] = right;
      SamplesReady?.Invoke(sampleBuffer, 2);
   }

   private UIntPtr HandleAudioBatch(IntPtr data, UIntPtr frames) {
      var count = (int)frames * 2;
      if (count <= 0 || data == IntPtr.Zero) return frames;
      if (sampleBuffer.Length < count) sampleBuffer = new short[count];
      Marshal.Copy(data, sampleBuffer, 0, count);
      SamplesReady?.Invoke(sampleBuffer, count);
      return frames;
   }

   /// <summary>
   /// The core's logger. Nothing may escape from here: this is called from native code, and an
   /// exception crossing back would tear down the process with no useful report.
   /// </summary>
   private void WriteCoreLog(uint level, IntPtr format) {
      try {
         if (format == IntPtr.Zero) return;
         var message = Marshal.PtrToStringAnsi(format);
         if (string.IsNullOrWhiteSpace(message)) return;
         // Unexpanded: the varargs are deliberately not read. Printf escapes would only be noise.
         Debug.WriteLine($"[{LibraryName}] {message.TrimEnd()}");
      } catch (Exception) {
         // A core that cannot be logged is still a core that should keep running.
      }
   }

   private short HandleInputState(uint port, uint device, uint index, uint id) {
      if (port != 0 || device != Libretro.DeviceJoypad) return 0;
      var polled = Volatile.Read(ref polledButtons);

      // "Give me the whole pad at once." VBA-M does not use this, but mGBA and most modern cores
      // do, and a core that asks and gets nothing reads every button as released.
      if (id == Libretro.JoypadIdMask) return (short)polled;

      if (id > 15) return 0;
      return (short)((polled >> (int)id) & 1);
   }

   #endregion

   public void Dispose() {
      if (disposed) return;
      disposed = true;
      try {
         if (gameLoaded) { unloadGame(); gameLoaded = false; }
         deinit();
      } catch (Exception) {
         // A core that faults on the way out must not take the editor with it.
      }
      FreeRomCopy();
      if (systemDirectoryPtr != IntPtr.Zero) Marshal.FreeHGlobal(systemDirectoryPtr);
      if (saveDirectoryPtr != IntPtr.Zero) Marshal.FreeHGlobal(saveDirectoryPtr);
      // The library itself is deliberately left loaded. Cores are not built to be initialized
      // twice in one process, but they are fine being re-init'd after a deinit - unloading and
      // reloading the dylib is what tends to crash.
   }
}
