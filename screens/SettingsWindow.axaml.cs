using System;
using System.Threading.Tasks;
using AbiturEliteCode.cs.MainWindow;
using AbiturEliteCode.screens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AbiturEliteCode;

public partial class SettingsWindow : Window
{
    private readonly SettingsWindowContext _ctx;

    private Action _restoreSnapshot = null!;
    private Func<bool> _hasChangedFromSnapshot = null!;
    private Action _refreshSnapshot = null!;

    private bool _isPortable;
    private bool _originalPortableState;

    private CheckBox _chkSyntax = null!;
    private CheckBox _chkAutocomplete = null!;
    private CheckBox _chkError = null!;
    private CheckBox _chkErrorExplain = null!;
    private CheckBox _chkVim = null!;
    private CheckBox _chkPortable = null!;
    private CheckBox _chkAutoUpdate = null!;
    private CheckBox _chkDiscordRpc = null!;
    private CheckBox _chkSqlAntiSpoiler = null!;
    private Slider _sliderScale = null!;
    private Slider _sliderFontSize = null!;
    private Slider _sliderSqlFontSize = null!;

    private TextBlock _txtVersionInfo = null!;
    private Button _btnCheckUpdate = null!;
    private Button _btnUpdateApp = null!;
    private ProgressBar _updateProgressBar = null!;

    private StackPanel _editorPanel = null!;
    private StackPanel _displayPanel = null!;
    private StackPanel _dataPanel = null!;
    private StackPanel _updatesPanel = null!;
    private StackPanel _miscPanel = null!;

    public SettingsWindow(SettingsWindowContext ctx)
    {
        _ctx = ctx;

        var snapshot = AppSettings.TakeSnapshot();
        _restoreSnapshot = () => AppSettings.RestoreSnapshot(snapshot);
        _hasChangedFromSnapshot = () => AppSettings.HasChangedFrom(snapshot);
        _refreshSnapshot = () => { snapshot = AppSettings.TakeSnapshot(); };

        _isPortable = SaveSystem.IsPortableModeEnabled();
        _originalPortableState = _isPortable;

        InitializeComponent();

        BtnSave.Content = ctx.LoadIcon("assets/icons/ic_save.svg", 20);
        BtnReset.Content = ctx.LoadIcon("assets/icons/ic_restart.svg", 20);
        ToolTip.SetTip(BtnSave, "Einstellungen speichern");
        ToolTip.SetTip(BtnReset, "Auf Standard zurücksetzen");

        UpdatesBadge.IsVisible = ctx.UpdateAvailable;

        BuildEditorPanel();
        BuildDisplayPanel();
        BuildDataPanel();
        BuildUpdatesPanel();
        BuildMiscPanel();

        BtnCatEditor.Click += (_, _) => ShowCategory(BtnCatEditor, _editorPanel);
        BtnCatDisplay.Click += (_, _) => ShowCategory(BtnCatDisplay, _displayPanel);
        BtnCatData.Click += (_, _) => ShowCategory(BtnCatData, _dataPanel);
        BtnCatUpdates.Click += (_, _) => ShowCategory(BtnCatUpdates, _updatesPanel);
        BtnCatMisc.Click += (_, _) => ShowCategory(BtnCatMisc, _miscPanel);

        ShowCategory(BtnCatEditor, _editorPanel);

        BtnSave.Click += (_, _) => PerformSave();
        BtnReset.Click += async (_, _) => await ShowResetDialog();
        BtnClose.Click += async (_, _) => await AttemptClose();

        KeyDown += async (_, ev) =>
        {
            if (ev.Key == Key.Escape) { ev.Handled = true; await AttemptClose(); }
        };

        Closing += (_, _) =>
        {
            if (!BtnSave.IsEnabled) return;

            _restoreSnapshot();
            _ctx.CodeEditor.FontSize = AppSettings.EditorFontSize;
            _ctx.SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
            _ctx.UpdateVimState();
            _ctx.ApplySyntaxHighlighting();
            _ctx.ApplyUiScale();
            _ctx.ClearDiagnostics();
        };
    }

    private void ShowCategory(Button activeBtn, Control content)
    {
        BtnCatEditor.Background = Brushes.Transparent;
        BtnCatDisplay.Background = Brushes.Transparent;
        BtnCatData.Background = Brushes.Transparent;
        BtnCatUpdates.Background = Brushes.Transparent;
        BtnCatMisc.Background = Brushes.Transparent;

        activeBtn.Background = SolidColorBrush.Parse("#3E3E42");
        RightPanel.Child = content;
    }

    private void CheckChanges()
    {
        bool hasChanges = _hasChangedFromSnapshot() || _chkPortable.IsChecked != _isPortable;
        BtnSave.IsEnabled = hasChanges;
        BtnSave.Opacity = hasChanges ? 1.0 : 0.5;
        BtnSave.Background = hasChanges
            ? SolidColorBrush.Parse("#32A852")
            : SolidColorBrush.Parse("#464646");
    }

    private void PerformSave()
    {
        AppSettings.ApplyTo(_ctx.PlayerData.Settings);

        if (AppSettings.IsDiscordRpcEnabled)
        {
            DiscordRpcManager.Initialize();
            if (_ctx.IsDesignerMode)
                DiscordRpcManager.UpdatePresence("C# Level Designer", "Creating their own level", "aec_app_icon", "Custom");
            else if (_ctx.IsSqlMode)
                DiscordRpcManager.UpdatePresence($"SQL Level {_ctx.CurrentSqlLevelId}", "Querying greatness", "mysql_icon", "MySQL");
            else
                DiscordRpcManager.UpdatePresence($"C# Level {_ctx.CurrentLevelId}", "Coding greatness", "chsarp_icon", "C#");
        }
        else
        {
            DiscordRpcManager.Deinitialize();
        }

        SaveSystem.Save(_ctx.PlayerData);

        if (_chkPortable.IsChecked != _originalPortableState)
            try
            {
                SaveSystem.SetPortableMode(_chkPortable.IsChecked == true);
                _isPortable = _chkPortable.IsChecked == true;
                _originalPortableState = _isPortable;

                string location = _isPortable ? "Programmordner" : "AppData";
                _ctx.AddToConsole($"\n> Speicherort geändert auf: {location}", Brushes.LightGray);
            }
            catch (Exception ex)
            {
                _ctx.AddToConsole($"\n> Fehler beim Ändern des Speicherorts: {ex.Message}", Brushes.Red);
            }

        _refreshSnapshot();
        BtnSave.IsEnabled = false;
        BtnSave.Opacity = 0.5;
    }


    private async Task AttemptClose()
    {
        if (!BtnSave.IsEnabled) { Close(); return; }

        var dialog = new Window
        {
            Title = "Ungespeicherte Änderungen",
            Width = 350,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        var grid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };
        grid.Children.Add(new TextBlock
        {
            Text = "Du hast ungespeicherte Änderungen. Möchtest du diese speichern?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(btnPanel, 1);

        var btnSaveClose = new Button { Content = "Speichern", Background = SolidColorBrush.Parse("#32A852"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
        var btnDiscard = new Button { Content = "Verwerfen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
        var btnCancel = new Button { Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };

        btnSaveClose.Click += (_, _) => { PerformSave(); dialog.Close(); Close(); };
        btnDiscard.Click += (_, _) => { dialog.Close(); Close(); };
        btnCancel.Click += (_, _) => dialog.Close();

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnDiscard);
        btnPanel.Children.Add(btnSaveClose);
        grid.Children.Add(btnPanel);
        dialog.Content = grid;

        await dialog.ShowDialog(this);
    }


    private async Task ShowResetDialog()
    {
        var dialog = new Window
        {
            Title = "Zurücksetzen?",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        var grid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };
        grid.Children.Add(new TextBlock
        {
            Text = "Einstellungen wirklich auf Standard zurücksetzen?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Grid.SetRow(btnPanel, 1);

        var btnYes = new Button { Content = "Ja, zurücksetzen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
        var btnNo  = new Button { Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };

        btnYes.Click += (_, _) =>
        {
            if (_ctx.IsSqlMode)
            {
                _chkSyntax.IsChecked = false;
                _chkVim.IsChecked = false;
                _chkAutocomplete.IsChecked = false;
            }
            else
            {
                _chkSyntax.IsChecked = false;
                _chkError.IsChecked = false;
                _chkErrorExplain.IsChecked = false;
                _chkVim.IsChecked = false;
                _chkAutocomplete.IsChecked = false;
            }

            _sliderFontSize.Value = 16.0;
            _sliderSqlFontSize.Value = 16.0;
            _chkPortable.IsChecked = false;
            _sliderScale.Value = 1.0;
            _chkAutoUpdate.IsChecked = true;
            _chkSqlAntiSpoiler.IsChecked = false;
            _chkDiscordRpc.IsChecked = false;

            dialog.Close();
        };
        btnNo.Click += (_, _) => dialog.Close();

        btnPanel.Children.Add(btnNo);
        btnPanel.Children.Add(btnYes);
        grid.Children.Add(btnPanel);
        dialog.Content = grid;

        await dialog.ShowDialog(this);
    }

    private async Task ShowWarningDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = "Hinweis",
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        var grid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };

        var contentPanel = new StackPanel { Spacing = 15, VerticalAlignment = VerticalAlignment.Center };
        contentPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#32A852"),
            FontSize = 16
        });
        contentPanel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Brushes.White
        });
        grid.Children.Add(contentPanel);

        var btn = new Button
        {
            Content = "Verstanden",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = SolidColorBrush.Parse("#32A852"),
            Foreground = Brushes.White,
            Padding = new Thickness(20, 8),
            CornerRadius = new CornerRadius(4)
        };
        btn.Click += (_, _) => dialog.Close();

        Grid.SetRow(btn, 1);
        grid.Children.Add(btn);
        dialog.Content = grid;

        await dialog.ShowDialog(this);
    }

    private void BuildEditorPanel()
    {
        // syntax highlighting
        _chkSyntax = new CheckBox
        {
            Content = "Syntax-Hervorhebung",
            IsChecked = _ctx.IsSqlMode
                ? AppSettings.IsSqlSyntaxHighlightingEnabled
                : AppSettings.IsSyntaxHighlightingEnabled,
            Foreground = Brushes.White
        };

        // auto completion
        _chkAutocomplete = new CheckBox
        {
            Content = "Autovervollständigung",
            IsChecked = _ctx.IsSqlMode ? AppSettings.IsSqlAutocompleteEnabled : AppSettings.IsAutocompleteEnabled,
            Foreground = Brushes.White
        };

        // error highlighting (c# only)
        _chkError = new CheckBox
        {
            Content = "Error-Hervorhebung",
            IsChecked = AppSettings.IsErrorHighlightingEnabled,
            Foreground = Brushes.White,
            IsVisible = !_ctx.IsSqlMode
        };

        _chkErrorExplain = new CheckBox
        {
            Content = "Error-Erklärungen",
            IsChecked = AppSettings.IsErrorExplanationEnabled,
            IsEnabled = AppSettings.IsErrorHighlightingEnabled,
            Foreground = Brushes.White,
            IsVisible = !_ctx.IsSqlMode,
            Margin = new Thickness(20, 0, 0, 0) // indent (under error highlighting)
        };

        // vim controls
        _chkVim = new CheckBox
        {
            Content = "Vim Steuerung",
            IsChecked = _ctx.IsSqlMode ? AppSettings.IsSqlVimEnabled : AppSettings.IsVimEnabled,
            Foreground = Brushes.White
        };

        if (_ctx.IsTutorialMode)
        {
            _chkVim.IsEnabled = false;
            ToolTip.SetTip(_chkVim, "Während des Tutorials nicht änderbar");
        }

        // event handlers
        _chkSyntax.IsCheckedChanged += (_, _) =>
        {
            if (_ctx.IsSqlMode)
            {
                AppSettings.IsSqlSyntaxHighlightingEnabled = _chkSyntax.IsChecked ?? false;
                _ctx.ApplySqlSyntaxHighlighting();
            }
            else
            {
                AppSettings.IsSyntaxHighlightingEnabled = _chkSyntax.IsChecked ?? false;
                _ctx.ApplySyntaxHighlighting();
            }
            CheckChanges();
        };

        _chkError.IsCheckedChanged += async (_, _) =>
        {
            if (_chkError.IsChecked == true && !AppSettings.IsErrorHighlightingEnabled)
                await ShowWarningDialog(
                    "Error-Hervorhebung",
                    "In der Prüfung müssen Fehler selbstständig gefunden werden. Es wird empfohlen ohne dieses Feature zu üben!\n\nAchtung: Diese Funktion setzt sich nach jedem Level-Wechsel zurück."
                );

            AppSettings.IsErrorHighlightingEnabled = _chkError.IsChecked ?? false;

            if (!AppSettings.IsErrorHighlightingEnabled)
            {
                _chkErrorExplain.IsChecked = false;
                _chkErrorExplain.IsEnabled = false;
            }
            else
            {
                _chkErrorExplain.IsEnabled = true;
            }

            if (AppSettings.IsErrorHighlightingEnabled)
                _ctx.UpdateDiagnostics();
            else
                _ctx.ClearDiagnostics();

            CheckChanges();
        };

        _chkErrorExplain.IsCheckedChanged += async (_, _) =>
        {
            if (_chkErrorExplain.IsChecked == true && !AppSettings.IsErrorExplanationEnabled)
                await ShowWarningDialog(
                    "Error-Erklärungen",
                    "Detaillierte Fehlerbeschreibungen stehen in der Prüfung nicht zur Verfügung. Nutze dies nur, wenn du absolut nicht weiterkommst."
                );
            AppSettings.IsErrorExplanationEnabled = _chkErrorExplain.IsChecked ?? false;
            CheckChanges();
        };

        _chkAutocomplete.IsCheckedChanged += (_, _) =>
        {
            if (_ctx.IsSqlMode)
            {
                AppSettings.IsSqlAutocompleteEnabled = _chkAutocomplete.IsChecked ?? false;
                if (AppSettings.IsSqlAutocompleteEnabled)
                    _ctx.ScanSqlTokens?.Invoke(_ctx.SqlQueryEditor.Text);
                else
                    _ctx.ClearSqlSuggestion?.Invoke();
            }
            else
            {
                AppSettings.IsAutocompleteEnabled = _chkAutocomplete.IsChecked ?? false;
                if (AppSettings.IsAutocompleteEnabled)
                    _ctx.ScanCsharpTokens?.Invoke(_ctx.CodeEditor.Text);
                else
                    _ctx.ClearCsharpSuggestion?.Invoke();
            }
            CheckChanges();
        };

        _chkVim.IsCheckedChanged += (_, _) =>
        {
            if (_ctx.IsSqlMode) AppSettings.IsSqlVimEnabled = _chkVim.IsChecked ?? false;
            else AppSettings.IsVimEnabled = _chkVim.IsChecked ?? false;

            if (_chkVim.IsChecked == true)
                _ctx.SetVimMode(MainWindow.VimMode.Normal);

            _ctx.UpdateVimState();
            CheckChanges();
        };

        string editorTitle = _ctx.IsSqlMode ? "SQL Query Editor" : "C# Code Editor";
        _editorPanel = new StackPanel { Spacing = 15 };
        _editorPanel.Children.Add(new TextBlock
        {
            Text = editorTitle,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _editorPanel.Children.Add(_chkSyntax);
        _editorPanel.Children.Add(_chkAutocomplete);
        _editorPanel.Children.Add(_chkError);
        _editorPanel.Children.Add(_chkErrorExplain);
        _editorPanel.Children.Add(_chkVim);
    }

    private void BuildDisplayPanel()
    {
        _sliderScale = new Slider
        {
            Minimum = 0.5,
            Maximum = 2.0,
            Value = AppSettings.UiScale,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var txtScaleVal = new TextBlock
        {
            Text = $"{AppSettings.UiScale:P0}",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        _sliderFontSize = new Slider
        {
            Minimum = 8,
            Maximum = 48,
            Value = AppSettings.EditorFontSize,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var txtFontSizeVal = new TextBlock
        {
            Text = $"{AppSettings.EditorFontSize:F0}px",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        _sliderSqlFontSize = new Slider
        {
            Minimum = 8,
            Maximum = 48,
            Value = AppSettings.SqlEditorFontSize,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var txtSqlFontSizeVal = new TextBlock
        {
            Text = $"{AppSettings.SqlEditorFontSize:F0}px",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };

        // event handlers
        _sliderScale.ValueChanged += (_, ev) =>
        {
            AppSettings.UiScale = ev.NewValue;
            txtScaleVal.Text = $"{ev.NewValue:P0}";
            _ctx.ApplyUiScale();
            CheckChanges();
        };

        _sliderFontSize.ValueChanged += (_, ev) =>
        {
            AppSettings.EditorFontSize = ev.NewValue;
            txtFontSizeVal.Text = $"{ev.NewValue:F0}px";
            _ctx.CodeEditor.FontSize = ev.NewValue;
            _ctx.TutorialEditor.FontSize = ev.NewValue;
            CheckChanges();
        };

        _sliderSqlFontSize.ValueChanged += (_, ev) =>
        {
            AppSettings.SqlEditorFontSize = ev.NewValue;
            txtSqlFontSizeVal.Text = $"{ev.NewValue:F0}px";
            _ctx.SqlQueryEditor.FontSize = ev.NewValue;
            CheckChanges();
        };

        var scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        scaleRow.Children.Add(_sliderScale);
        scaleRow.Children.Add(txtScaleVal);

        var fontRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        fontRow.Children.Add(_sliderFontSize);
        fontRow.Children.Add(txtFontSizeVal);

        var sqlFontRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        sqlFontRow.Children.Add(_sliderSqlFontSize);
        sqlFontRow.Children.Add(txtSqlFontSizeVal);

        _displayPanel = new StackPanel { Spacing = 15 };
        _displayPanel.Children.Add(new TextBlock
        {
            Text = "Darstellung",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _displayPanel.Children.Add(new TextBlock { Text = "UI Skalierung", Foreground = Brushes.LightGray });
        _displayPanel.Children.Add(scaleRow);
        _displayPanel.Children.Add(new TextBlock { Text = "C# Editor Schriftgröße", Foreground = Brushes.LightGray });
        _displayPanel.Children.Add(fontRow);
        _displayPanel.Children.Add(new TextBlock { Text = "SQL Editor Schriftgröße", Foreground = Brushes.LightGray });
        _displayPanel.Children.Add(sqlFontRow);
    }

    private void BuildDataPanel()
    {
        _chkPortable = new CheckBox
        {
            Content = "Portable Mode",
            IsChecked = _isPortable,
            Foreground = Brushes.White
        };
        var txtPortableInfo = new TextBlock
        {
            Text = "Wenn aktiviert, wird der Speicherstand direkt neben der ausführbaren Datei gespeichert. Ideal für USB-Sticks.",
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(25, 0, 0, 0)
        };

        bool canWriteRoot = SaveSystem.CanWriteToRoot();
        if (!canWriteRoot)
        {
            _chkPortable.IsEnabled = false;
            _chkPortable.Content += " (Keine Schreibrechte)";
            _chkPortable.Foreground = Brushes.Gray;
            txtPortableInfo.Text = "Portable Mode ist hier nicht verfügbar, da keine Schreibrechte im Programmordner bestehen.";
            txtPortableInfo.Foreground = Brushes.Red;
        }

        _chkPortable.IsCheckedChanged += (_, _) => CheckChanges();

        _dataPanel = new StackPanel { Spacing = 15 };
        _dataPanel.Children.Add(new TextBlock
        {
            Text = "Daten & Speicher",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _dataPanel.Children.Add(_chkPortable);
        _dataPanel.Children.Add(txtPortableInfo);
    }

    private void BuildUpdatesPanel()
    {
        _chkAutoUpdate = new CheckBox
        {
            Content = "Beim Start automatisch nach Updates suchen",
            IsChecked = AppSettings.AutoCheckForUpdates,
            Foreground = Brushes.White
        };

        _txtVersionInfo = new TextBlock
        {
            Text = _ctx.UpdateAvailable
                ? $"Eine neue Version ist verfügbar: {_ctx.LatestVersion}\nAktuelle Version: {UpdateManager.CurrentVersion}"
                : $"Aktuelle Version: {UpdateManager.CurrentVersion}",
            Foreground = _ctx.UpdateAvailable ? SolidColorBrush.Parse("#32A852") : Brushes.Gray,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 10)
        };

        _btnCheckUpdate = new Button
        {
            Content = "Nach Updates suchen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4),
            MinWidth = 180,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        _btnUpdateApp = new Button
        {
            Content = "App aktualisieren",
            Background = SolidColorBrush.Parse("#007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4),
            IsEnabled = _ctx.UpdateAvailable
        };

        _updateProgressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Foreground = SolidColorBrush.Parse("#32A852"),
            Background = SolidColorBrush.Parse("#1A1A1A"),
            IsVisible = false,
            Margin = new Thickness(0, 5, 0, 15)
        };

        // event handlers
        _chkAutoUpdate.IsCheckedChanged += (_, _) =>
        {
            AppSettings.AutoCheckForUpdates = _chkAutoUpdate.IsChecked ?? false;
            CheckChanges();
        };

        _btnCheckUpdate.Click += async (_, _) =>
        {
            _btnCheckUpdate.Content = "Suche...";
            _btnCheckUpdate.IsEnabled = false;

            var result = await UpdateManager.CheckForUpdatesAsync();
            if (result.UpdateAvailable)
            {
                _ctx.UpdateAvailable = true;
                _ctx.LatestVersion = result.LatestVersion;
                _ctx.UpdateDownloadUrl = result.DownloadUrl;
                _ctx.ShowUpdateBadge();

                _txtVersionInfo.Text = $"Eine neue Version ist verfügbar: {_ctx.LatestVersion}\nAktuelle Version: {UpdateManager.CurrentVersion}";
                _txtVersionInfo.Foreground = SolidColorBrush.Parse("#32A852");
                _btnUpdateApp.IsEnabled = true;

                UpdatesBadge.IsVisible = true;
            }
            else
            {
                _txtVersionInfo.Text = $"Du bist auf dem neusten Stand.\nAktuelle Version: {UpdateManager.CurrentVersion}";
                _txtVersionInfo.Foreground = Brushes.Gray;
            }

            _btnCheckUpdate.Content = "Nach Updates suchen";
            _btnCheckUpdate.IsEnabled = true;
        };

        _btnUpdateApp.Click += async (_, _) =>
        {
            _btnUpdateApp.Content = "Bereite Update vor...";
            _btnUpdateApp.IsEnabled = false;
            _btnCheckUpdate.IsEnabled = false;
            _updateProgressBar.IsVisible = true;
            _updateProgressBar.Value = 0;

            var progress = new Progress<(string message, double percentage)>(p =>
            {
                _btnUpdateApp.Content = p.message;
                _updateProgressBar.Value = p.percentage;
            });

            var updateResult = await UpdateManager.PerformUpdateAsync(_ctx.UpdateDownloadUrl, progress);

            if (updateResult != UpdateManager.UpdateStatus.Success)
            {
                _btnUpdateApp.Content = "App aktualisieren";
                _btnUpdateApp.IsEnabled = true;
                _btnCheckUpdate.IsEnabled = true;
                _updateProgressBar.IsVisible = false;

                await _ctx.ShowManualUpdateDialog(updateResult, _ctx.UpdateDownloadUrl, this);
            }
        };

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actionRow.Children.Add(_btnCheckUpdate);
        actionRow.Children.Add(_btnUpdateApp);

        _updatesPanel = new StackPanel { Spacing = 15 };
        _updatesPanel.Children.Add(new TextBlock
        {
            Text = "Updates",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _updatesPanel.Children.Add(_chkAutoUpdate);
        _updatesPanel.Children.Add(_txtVersionInfo);
        _updatesPanel.Children.Add(_updateProgressBar);
        _updatesPanel.Children.Add(actionRow);
    }

    private void BuildMiscPanel()
    {
        _chkSqlAntiSpoiler = new CheckBox
        {
            Content = "SQL Anti-Spoiler Modus",
            IsChecked = AppSettings.IsSqlAntiSpoilerEnabled,
            Foreground = Brushes.White
        };
        ToolTip.SetTip(_chkSqlAntiSpoiler, "Mögliche Lösungsansätze aus den Levelnamen verbergen");

        _chkDiscordRpc = new CheckBox
        {
            Content = "Discord Rich Presence",
            IsChecked = AppSettings.IsDiscordRpcEnabled,
            Foreground = Brushes.White
        };
        ToolTip.SetTip(_chkDiscordRpc, "Zeige deinen Status auf Discord an");

        // event handlers
        _chkSqlAntiSpoiler.IsCheckedChanged += (_, _) =>
        {
            AppSettings.IsSqlAntiSpoilerEnabled = _chkSqlAntiSpoiler.IsChecked ?? false;
            CheckChanges();
        };

        _chkDiscordRpc.IsCheckedChanged += (_, _) =>
        {
            AppSettings.IsDiscordRpcEnabled = _chkDiscordRpc.IsChecked ?? false;
            CheckChanges();
        };

        _miscPanel = new StackPanel { Spacing = 15 };
        _miscPanel.Children.Add(new TextBlock
        {
            Text = "Sonstiges",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _miscPanel.Children.Add(_chkSqlAntiSpoiler);
        _miscPanel.Children.Add(_chkDiscordRpc);
    }
}
