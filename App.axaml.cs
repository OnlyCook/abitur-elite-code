using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
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
                    await Task.Delay(150);

                    var cts = new CancellationTokenSource();
                    _ = splash.AnimateFakeProgressAsync(cts.Token);
                    System.Diagnostics.Debug.WriteLine($"Fake Loading started.. {DateTime.Now}.{DateTime.Now.Millisecond}");

                    // capture results
                    List<Level>? levels = null;
                    PlayerData? playerData = null;
                    CustomPlayerData? customPlayerData = null;

                    var initTask = Task.Run(() =>
                    {
                        PrerequisiteSystem.Initialize();
                        SqlPrerequisiteSystem.Initialize();
                        levels = Curriculum.GetLevels();
                        playerData = SaveSystem.Load();
                        customPlayerData = SaveSystem.LoadCustom();
                    });

                    await initTask;

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    // pass already loaded data in
                    var mainWindow = new MainWindow(levels!, playerData!, customPlayerData!, splash);
                    sw.Stop();
                    System.Diagnostics.Debug.WriteLine($"new MainWindow() took: {sw.ElapsedMilliseconds}ms");

                    cts.Cancel();
                    System.Diagnostics.Debug.WriteLine($"Fake Loading cancelled.. {DateTime.Now}.{DateTime.Now.Millisecond}");

                    await splash.FinishProgressAsync();

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    System.Diagnostics.Debug.WriteLine($"MainWindow shown.. {DateTime.Now}.{DateTime.Now.Millisecond}");
                    splash.Close();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}