using Avalonia.Controls;
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

        public async Task AnimateProgressAsync()
        {
            for (int i = 0; i <= 100; i += 2)
            {
                LoadingBar.Value = i;
                await Task.Delay(15);
            }
        }

        public async Task AnimateFakeProgressAsync(CancellationToken token)
        {
            var random = new Random();

            double currentProgress = 0.0;

            try
            {
                // cap fake progress at 90%
                while (currentProgress < 90.0 && !token.IsCancellationRequested)
                {
                    // random wait
                    int waitTime = random.Next(50, 400);

                    await Task.Delay(waitTime, token);

                    // random jump
                    double jump = random.NextDouble() * 2.5 + 0.5;
                    currentProgress += jump;

                    if (currentProgress > 90.0) currentProgress = 90.0;

                    LoadingBar.Value = currentProgress;
                }
            }
            catch (TaskCanceledException)
            {
                // this means real loading finished
            }
        }

        public async Task FinishProgressAsync()
        {
            // fast push to 100% starting from wherever fake progress was interrupted
            while (LoadingBar.Value < 100)
            {
                await Task.Delay(30);
                LoadingBar.Value = Math.Min(100, LoadingBar.Value + 5.5);
            }

            await Task.Delay(50); // small pause to let progress finish
        }
    }
}