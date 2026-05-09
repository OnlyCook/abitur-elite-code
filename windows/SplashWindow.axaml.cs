using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void AddLoadingProgress(int val) => LoadingBar.Value += val;
        public double GetLoadingProgress() => LoadingBar.Value;

        public Task AnimateFakeProgressAsync(CancellationToken token)
        {
            var tcs = new TaskCompletionSource();
            var random = new Random();
            double currentProgress = 0.0;
            Timer? timer = null;
            int finished = 0;

            void Finish()
            {
                if (Interlocked.Exchange(ref finished, 1) == 1) return; // already finished
                timer?.Dispose();
                tcs.TrySetResult();
            }

            void OnTick(object? state)
            {
                if (finished == 1 || token.IsCancellationRequested || currentProgress >= 90.0)
                {
                    Finish();
                    return;
                }

                double jump = random.NextDouble() * 2.5 + 0.5;
                currentProgress = Math.Min(90.0, currentProgress + jump);
                double capturedProgress = currentProgress;

                Dispatcher.UIThread.Post(() =>
                {
                    // if cancelled by the time this runs -> dont fire burst
                    if (token.IsCancellationRequested) return;
                    LoadingBar.Value = capturedProgress;
                    System.Diagnostics.Debug.WriteLine("LoadingBar progressed! New value: " + LoadingBar.Value);
                }, DispatcherPriority.Send);

                if (finished == 1 || token.IsCancellationRequested)
                    Finish();
                else
                    timer?.Change(random.Next(40, 180), Timeout.Infinite);
            }

            // stop the timer immediately on cancel request
            token.Register(Finish);

            // one shot timer mode
            timer = new Timer(OnTick, null, random.Next(40, 100), Timeout.Infinite);

            return tcs.Task;
        }

        public async Task FinishProgressAsync()
        {
            while (LoadingBar.Value < 100)
            {
                await Task.Delay(30);
                LoadingBar.Value = Math.Min(100, LoadingBar.Value + 5.5);
            }

            await Task.Delay(50);
        }
    }
}