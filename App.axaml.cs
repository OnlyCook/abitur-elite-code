using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

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
                    await splash.AnimateProgressAsync();

                    var mainWindow = new MainWindow();
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    splash.Close();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}