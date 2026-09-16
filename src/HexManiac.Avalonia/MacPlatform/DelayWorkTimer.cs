using System;
using System.Threading;
using System.Threading.Tasks;
using HavenSoft.HexManiac.Core;

namespace HavenSoft.HexManiac.AvaloniaUI.MacPlatform;

/// <summary>
/// Port of HexManiac.WPF/Controls/DelayWorkTimer.cs. Core uses this to debounce work that
/// shouldn't run on every keystroke (search-as-you-type, table updates).
/// </summary>
public class DelayWorkTimer : IDelayWorkTimer {
   CancellationTokenSource cancel;

   public bool HasScheduledWork => cancel != null;

   public DelayWorkResult DelayCall(TimeSpan delay, Action action) {
      var result = HasScheduledWork ? DelayWorkResult.WorkScheduledAndPreviousWorkCleared : DelayWorkResult.WorkScheduled;
      cancel?.Cancel();
      cancel = new CancellationTokenSource();
      Run(delay, action, cancel.Token);
      return result;
   }

   public void Reset() {
      cancel?.Cancel();
      cancel = null;
   }

   async void Run(TimeSpan delay, Action action, CancellationToken token) {
      try {
         await Task.Delay(delay, token);
      } catch (TaskCanceledException) { }
      if (token.IsCancellationRequested) return;
      action();
      cancel = null;
   }
}
