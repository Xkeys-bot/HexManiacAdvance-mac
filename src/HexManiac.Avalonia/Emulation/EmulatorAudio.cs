using System;
using System.Runtime.InteropServices;

namespace HavenSoft.HexManiac.AvaloniaUI.Emulation;

/// <summary>
/// Sound output for the embedded emulator, through macOS AudioQueue.
///
/// Avalonia has no audio stack of its own and the project deliberately takes no extra NuGet
/// dependencies, so this talks to AudioToolbox directly. It is a plain producer/consumer ring: the
/// emulation thread pushes the samples libretro hands it, and AudioQueue's own thread drains the
/// ring into the buffers it recycles back to us. An underrun plays silence rather than stalling,
/// because a stalled queue never asks for another buffer and the sound dies for good.
///
/// Audio is optional. If anything here fails, <see cref="IsRunning"/> stays false, the emulator
/// runs silently, and the window says so - a testing tool that refuses to start because of a sound
/// device would be worse than one with no sound.
/// </summary>
public sealed class EmulatorAudio : IDisposable {
   private const int BufferCount = 4;
   private const uint FormatLinearPcm = 0x6C70636D;           // 'lpcm'
   private const uint FlagsSignedIntegerPacked = 4 | 8;
   private const int BytesPerFrame = 4;                        // 16-bit stereo

   private readonly object gate = new();
   private readonly short[] ring;
   private int writeIndex, readIndex, available;

   private readonly AudioQueueOutputCallback callback;   // kept alive for the native side
   private IntPtr queue;
   private readonly IntPtr[] buffers = new IntPtr[BufferCount];
   private bool disposed;

   public bool IsRunning { get; private set; }
   public string FailureReason { get; private set; }

   public EmulatorAudio(double sampleRate, int bufferFrames = 2048) {
      // Room for roughly a third of a second, which absorbs a slow frame without adding audible
      // latency at the sizes libretro cores actually deliver.
      ring = new short[Math.Max(bufferFrames * 8, 16384) * 2];
      callback = HandleBufferComplete;

      var format = new AudioStreamBasicDescription {
         SampleRate = sampleRate,
         FormatId = FormatLinearPcm,
         FormatFlags = FlagsSignedIntegerPacked,
         BytesPerPacket = BytesPerFrame,
         FramesPerPacket = 1,
         BytesPerFrame = BytesPerFrame,
         ChannelsPerFrame = 2,
         BitsPerChannel = 16,
         Reserved = 0,
      };

      try {
         var status = AudioQueueNewOutput(ref format, callback, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out queue);
         if (status != 0) { FailureReason = $"AudioQueueNewOutput failed ({status})"; return; }

         var byteSize = (uint)(bufferFrames * BytesPerFrame);
         for (int i = 0; i < BufferCount; i++) {
            status = AudioQueueAllocateBuffer(queue, byteSize, out buffers[i]);
            if (status != 0) { FailureReason = $"AudioQueueAllocateBuffer failed ({status})"; return; }
            // Prime with silence so the queue has something to play and starts calling back.
            FillBuffer(buffers[i]);
            AudioQueueEnqueueBuffer(queue, buffers[i], 0, IntPtr.Zero);
         }

         status = AudioQueueStart(queue, IntPtr.Zero);
         if (status != 0) { FailureReason = $"AudioQueueStart failed ({status})"; return; }
         IsRunning = true;
      } catch (DllNotFoundException e) {
         FailureReason = e.Message;
      } catch (EntryPointNotFoundException e) {
         FailureReason = e.Message;
      }
   }

   /// Called on the emulation thread with interleaved stereo samples.
   public void Push(short[] samples, int count) {
      if (!IsRunning) return;
      lock (gate) {
         for (int i = 0; i < count; i++) {
            if (available == ring.Length) {
               // Producer is ahead of the device (the emulator is running fast, or the queue is
               // paused). Drop the oldest sample rather than the newest, so the delay stops
               // growing instead of the audio freezing at an old moment.
               readIndex = (readIndex + 1) % ring.Length;
               available--;
            }
            ring[writeIndex] = samples[i];
            writeIndex = (writeIndex + 1) % ring.Length;
            available++;
         }
      }
   }

   public void Clear() {
      lock (gate) { writeIndex = readIndex = available = 0; }
   }

   public void SetPaused(bool paused) {
      if (!IsRunning) return;
      if (paused) AudioQueuePause(queue);
      else AudioQueueStart(queue, IntPtr.Zero);
   }

   private void HandleBufferComplete(IntPtr userData, IntPtr audioQueue, IntPtr buffer) {
      if (disposed) return;
      FillBuffer(buffer);
      AudioQueueEnqueueBuffer(audioQueue, buffer, 0, IntPtr.Zero);
   }

   private unsafe void FillBuffer(IntPtr buffer) {
      var header = (AudioQueueBufferHeader*)buffer;
      var capacitySamples = (int)header->AudioDataBytesCapacity / 2;
      var destination = (short*)header->AudioData;

      lock (gate) {
         var take = Math.Min(capacitySamples, available);
         for (int i = 0; i < take; i++) {
            destination[i] = ring[readIndex];
            readIndex = (readIndex + 1) % ring.Length;
         }
         available -= take;
         for (int i = take; i < capacitySamples; i++) destination[i] = 0;
      }

      header->AudioDataByteSize = header->AudioDataBytesCapacity;
   }

   public void Dispose() {
      if (disposed) return;
      disposed = true;
      if (queue == IntPtr.Zero) return;
      try {
         AudioQueueStop(queue, true);
         AudioQueueDispose(queue, true);
      } catch (Exception) {
         // shutting the device down must not be able to fail the app
      }
      queue = IntPtr.Zero;
      IsRunning = false;
   }

   #region AudioToolbox

   private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

   [StructLayout(LayoutKind.Sequential)]
   private struct AudioStreamBasicDescription {
      public double SampleRate;
      public uint FormatId;
      public uint FormatFlags;
      public uint BytesPerPacket;
      public uint FramesPerPacket;
      public uint BytesPerFrame;
      public uint ChannelsPerFrame;
      public uint BitsPerChannel;
      public uint Reserved;
   }

   /// The leading fields of AudioQueueBuffer. Only the first three matter to us, but the layout has
   /// to match up to the field we write.
   [StructLayout(LayoutKind.Sequential)]
   private struct AudioQueueBufferHeader {
      public uint AudioDataBytesCapacity;
      public IntPtr AudioData;
      public uint AudioDataByteSize;
      public IntPtr UserData;
   }

   [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
   private delegate void AudioQueueOutputCallback(IntPtr userData, IntPtr queue, IntPtr buffer);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueueNewOutput(ref AudioStreamBasicDescription format,
      AudioQueueOutputCallback callback, IntPtr userData, IntPtr callbackRunLoop,
      IntPtr callbackRunLoopMode, uint flags, out IntPtr queue);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueueAllocateBuffer(IntPtr queue, uint bufferByteSize, out IntPtr buffer);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueueEnqueueBuffer(IntPtr queue, IntPtr buffer, uint packetDescriptionCount, IntPtr packetDescriptions);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueueStart(IntPtr queue, IntPtr startTime);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueuePause(IntPtr queue);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueueStop(IntPtr queue, [MarshalAs(UnmanagedType.U1)] bool immediate);

   [DllImport(AudioToolbox)]
   private static extern int AudioQueueDispose(IntPtr queue, [MarshalAs(UnmanagedType.U1)] bool immediate);

   #endregion
}
