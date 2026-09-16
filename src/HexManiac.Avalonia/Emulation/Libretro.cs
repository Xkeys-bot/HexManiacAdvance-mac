using System;
using System.Runtime.InteropServices;

namespace HavenSoft.HexManiac.AvaloniaUI.Emulation;

/// <summary>
/// The libretro C ABI, as exported by a GBA core such as mGBA.
///
/// This is a plain transcription of libretro.h - no HexManiac concepts appear here. The ABI is
/// frozen at api_version 1 and every core exports the same entry points, so the same binding works
/// for mGBA, VBA-M, gpSP or any other GBA core the user points us at.
///
/// Every delegate is declared Cdecl and must be kept alive by the managed side for as long as the
/// core is loaded: the core stores the raw function pointers, and the GC has no idea they are
/// referenced from native memory.
/// </summary>
public static class Libretro {
   public const uint DeviceJoypad = 1;

   /// RETRO_DEVICE_ID_JOYPAD_*. The GBA uses ten of these; Y and X are unused.
   public enum JoypadButton : uint {
      B = 0, Y = 1, Select = 2, Start = 3,
      Up = 4, Down = 5, Left = 6, Right = 7,
      A = 8, X = 9, L = 10, R = 11,
   }

   /// <summary>
   /// RETRO_DEVICE_ID_JOYPAD_MASK. Asked as an id like any other, but it means "give me every
   /// button at once", with each JoypadButton as a bit. Cores that use it ask for it *instead of*
   /// the individual ids, not as well - so a host that does not answer it hands the game a pad
   /// with nothing pressed, forever, while every other part of input looks like it is working.
   /// </summary>
   public const uint JoypadIdMask = 256;

   public enum PixelFormat : uint {
      ZeroRgb1555 = 0,
      XRgb8888 = 1,
      Rgb565 = 2,
   }

   /// <summary>
   /// RETRO_ENVIRONMENT_*. Only the commands the host actually answers are named; everything else
   /// is refused, which the spec explicitly allows (the core falls back to its defaults).
   /// </summary>
   public enum EnvironmentCommand : uint {
      GetOverscan = 2,
      GetCanDupe = 3,
      SetMessage = 6,
      Shutdown = 7,
      SetPerformanceLevel = 8,
      GetSystemDirectory = 9,
      SetPixelFormat = 10,
      SetInputDescriptors = 11,
      GetVariable = 15,
      SetVariables = 16,
      GetVariableUpdate = 17,
      GetLibretroPath = 19,
      GetLogInterface = 27,
      GetCoreAssetsDirectory = 30,
      GetSaveDirectory = 31,
      SetSystemAvInfo = 32,
      SetGeometry = 37,
      GetLanguage = 39,

      /// RETRO_ENVIRONMENT_GET_INPUT_BITMASKS: 51 | RETRO_ENVIRONMENT_EXPERIMENTAL (0x10000).
      GetInputBitmasks = 0x10033,
   }

   /// Memory regions a core can expose. SaveRam is the battery-backed save.
   public const uint MemorySaveRam = 0;
   public const uint MemorySystemRam = 2;
   public const uint MemoryVideoRam = 3;

   [StructLayout(LayoutKind.Sequential)]
   public struct SystemInfo {
      public IntPtr LibraryName;
      public IntPtr LibraryVersion;
      public IntPtr ValidExtensions;
      [MarshalAs(UnmanagedType.U1)] public bool NeedFullPath;
      [MarshalAs(UnmanagedType.U1)] public bool BlockExtract;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct GameGeometry {
      public uint BaseWidth, BaseHeight, MaxWidth, MaxHeight;
      public float AspectRatio;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct SystemTiming {
      public double Fps;
      public double SampleRate;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct SystemAvInfo {
      public GameGeometry Geometry;
      public SystemTiming Timing;
   }

   [StructLayout(LayoutKind.Sequential)]
   public struct GameInfo {
      public IntPtr Path;
      public IntPtr Data;
      public UIntPtr Size;
      public IntPtr Meta;
   }

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   [return: MarshalAs(UnmanagedType.U1)]
   public delegate bool EnvironmentCallback(uint command, IntPtr data);

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   public delegate void VideoRefreshCallback(IntPtr data, uint width, uint height, UIntPtr pitch);

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   public delegate void AudioSampleCallback(short left, short right);

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   public delegate UIntPtr AudioSampleBatchCallback(IntPtr data, UIntPtr frames);

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   public delegate void InputPollCallback();

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   public delegate short InputStateCallback(uint port, uint device, uint index, uint id);

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate uint ApiVersionFn();
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void VoidFn();
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GetSystemInfoFn(out SystemInfo info);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void GetSystemAvInfoFn(out SystemAvInfo info);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetEnvironmentFn(EnvironmentCallback callback);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetVideoRefreshFn(VideoRefreshCallback callback);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetAudioSampleFn(AudioSampleCallback callback);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetAudioSampleBatchFn(AudioSampleBatchCallback callback);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetInputPollFn(InputPollCallback callback);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetInputStateFn(InputStateCallback callback);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.U1)] public delegate bool LoadGameFn(ref GameInfo game);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void SetControllerPortDeviceFn(uint port, uint device);

   /// <summary>
   /// retro_log_printf_t: void (*)(enum retro_log_level, const char *fmt, ...).
   ///
   /// Declared with only the two named parameters. That is safe on every ABI this runs on: the
   /// variadic arguments live on the stack (arm64) or in registers the callee simply never reads,
   /// and the caller cleans up under cdecl. The format string is passed through unexpanded - the
   /// point of this callback is that it exists, not what it prints.
   /// </summary>
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void LogPrintfFn(uint level, IntPtr format);

   /// struct retro_log_callback { retro_log_printf_t log; }
   [StructLayout(LayoutKind.Sequential)] public struct LogCallback { public IntPtr Log; }
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate UIntPtr SerializeSizeFn();
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.U1)] public delegate bool SerializeFn(IntPtr data, UIntPtr size);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate IntPtr GetMemoryDataFn(uint id);
   [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate UIntPtr GetMemorySizeFn(uint id);
}

/// <param name="Id">The libretro RETRO_MEMORY_* id.</param>
/// <param name="Name">What to call it in the UI.</param>
/// <param name="GbaAddress">Where it lives in the GBA's address space.</param>
/// <param name="Size">How many bytes the core exposes.</param>
public record MemoryRegion(uint Id, string Name, uint GbaAddress, int Size);
