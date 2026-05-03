using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            GC.KeepAlive(typeof(Avalonia.Svg.Skia.SvgImage).Assembly);
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                var splash = new SplashWindow();
                desktop.MainWindow = splash;

                splash.Opened += async (s, e) =>
                {
                    await Task.Delay(150); // breathing time for cpu

                    // create a token to stop the fake progress when real loading finishes
                    var cts = new CancellationTokenSource();

                    // start the stutter-y filler (fake progress)
                    var fakeProgressTask = splash.AnimateFakeProgressAsync(cts.Token);

                    // now run the actual heavy initialization in the background
                    var initTask = Task.Run(() =>
                    {
                        PrerequisiteSystem.Initialize();
                        SqlPrerequisiteSystem.Initialize();
                        Curriculum.GetLevels();
                        SaveSystem.Load();
                        SaveSystem.LoadCustom();
                    });

                    // wait only for the heavy background loading to finish
                    await initTask;

                    // create mainWindow on the ui thread before finishing progress
                    var mainWindow = new MainWindow();

                    // the real loading (and window creation) is done -> cut the filler
                    cts.Cancel();

                    // let the real progress take control from wherever the fake bar ended up
                    await splash.FinishProgressAsync();

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    splash.Close();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}