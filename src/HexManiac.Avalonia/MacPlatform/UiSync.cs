using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace HavenSoft.HexManiac.AvaloniaUI.MacPlatform;

/// <summary>
/// HexManiac.Core expects synchronous file dialogs, clipboard, and message boxes (WPF has them).
/// Avalonia's versions are async. Blocking with .Result on the UI thread would deadlock, so this
/// runs a nested dispatcher loop until the async work finishes: the same trick WPF's modal
/// dialogs use internally.
/// </summary>
public static class UiSync {
   public static T Run<T>(Func<Task<T>> work) {
      if (!Dispatcher.UIThread.CheckAccess()) {
         return Dispatcher.UIThread.InvokeAsync(work).GetAwaiter().GetResult();
      }

      var task = work();
      if (!task.IsCompleted) {
         using var loopDone = new CancellationTokenSource();
         task.ContinueWith(_ => Dispatcher.UIThread.Post(loopDone.Cancel), TaskScheduler.Default);
         Dispatcher.UIThread.MainLoop(loopDone.Token);
      }
      return task.GetAwaiter().GetResult();
   }

   public static void Run(Func<Task> work) => Run(async () => { await work(); return true; });
}
