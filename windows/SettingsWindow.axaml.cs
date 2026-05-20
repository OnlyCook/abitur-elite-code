using AbiturEliteCode.cs;
using AbiturEliteCode.screens;
using AbiturEliteCode.windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
    private CheckBox _chkWordWrap = null!;
    private CheckBox _chkPortable = null!;
    private CheckBox _chkAutoUpdate = null!;
    private Slider _sliderScale = null!;
    private Slider _sliderFontSize = null!;
    private Slider _sliderSqlFontSize = null!;

    private TextBlock _txtVersionInfo = null!;
    private Button _btnCheckUpdate = null!;
    private Button _btnUpdateApp = null!;
    private ProgressBar _updateProgressBar = null!;

    private StackPanel _patchNotesPanel = null!;
    private List<(string Version, string Body)> _cachedReleases = null!;
    private int _loadedReleasesCount = 0;
    private bool _isLoadingReleases = false;
    private bool _reachedFirstVersion = false;

    private CheckBox _chkDiscordRpc = null!;
    private CheckBox _chkCommunityFeatures = null!;
    private CheckBox _chkSqlAntiSpoiler = null!;
    private StackPanel _communitySubPanel = null!;
    private Button _btnGithubLogin = null!;
    private TextBlock _txtGithubStatus = null!;

    private Control _editorPanel = null!;
    private StackPanel _displayPanel = null!;
    private Control _dataPanel = null!;
    private Control _updatesPanel = null!;
    private Button _btnScrollTop = null!;
    private StackPanel _miscPanel = null!;

    // github community
    private const string ClientId = "Ov23liAiILTN73TYZ1cj";
    private CancellationTokenSource? _loginCts;
    private bool _suppressCommunityHandler = false;

    public SettingsWindow(SettingsWindowContext ctx, bool openMiscTab = false)
    {
        _ctx = ctx;

        var snapshot = AppSettings.TakeSnapshot();
        _restoreSnapshot = () => AppSettings.RestoreSnapshot(snapshot);
        _hasChangedFromSnapshot = () => AppSettings.HasChangedFrom(snapshot);
        _refreshSnapshot = () =>
        {
            snapshot = AppSettings.TakeSnapshot();
        };

        _isPortable = SaveSystem.IsPortableModeEnabled();
        _originalPortableState = _isPortable;

        InitializeComponent();

        BtnSave.Content = ctx.LoadIcon("assets/icons/ic_save.svg", 20);
        BtnReset.Content = ctx.LoadIcon("assets/icons/ic_restart.svg", 20);
        ToolTip.SetTip(BtnSave, "Einstellungen speichern");
        ToolTip.SetTip(BtnReset, "Auf Standard zurücksetzen");

        BtnCatEditor.Content = _ctx.IsSqlMode ? "SQL Editor" : "C# Editor";

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

        if (openMiscTab)
        {
            ShowCategory(BtnCatMisc, _miscPanel);
        }
        else
        {
            ShowCategory(BtnCatEditor, _editorPanel);
        }

        BtnSave.Click += (_, _) => PerformSave();
        BtnReset.Click += async (_, _) => await ShowResetDialog();
        BtnClose.Click += async (_, _) => await AttemptClose();

        KeyDown += async (_, ev) =>
        {
            if (ev.Key == Key.Escape)
            {
                ev.Handled = true;
                await AttemptClose();
            }
        };

        Closing += (_, _) =>
        {
            if (!BtnSave.IsEnabled) return;

            _restoreSnapshot();
            _ctx.CodeEditor.FontSize = AppSettings.EditorFontSize;
            _ctx.SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
            _ctx.CodeEditor.WordWrap = AppSettings.IsWordWrapEnabled;
            _ctx.SqlQueryEditor.WordWrap = AppSettings.IsSqlWordWrapEnabled;
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
        SaveSystem.SaveToken(AppSettings.GithubToken, AppSettings.InstallKey);

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
        if (!BtnSave.IsEnabled)
        {
            Close();
            return;
        }

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
        dialog.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };
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

        var btnSaveClose = new Button
        {
            Content = "Speichern",
            Background = SolidColorBrush.Parse("#32A852"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnDiscard = new Button
        {
            Content = "Verwerfen",
            Background = SolidColorBrush.Parse("#B43232"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };

        btnSaveClose.Click += (_, _) =>
        {
            PerformSave();
            dialog.Close();
            Close();
        };
        btnDiscard.Click += (_, _) =>
        {
            dialog.Close();
            Close();
        };
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
        dialog.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

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
                _chkWordWrap.IsChecked = false;
            }
            else
            {
                _chkSyntax.IsChecked = false;
                _chkError.IsChecked = false;
                _chkErrorExplain.IsChecked = false;
                _chkVim.IsChecked = false;
                _chkAutocomplete.IsChecked = false;
                _chkWordWrap.IsChecked = false;
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

    private async Task ShowWarningDialog(string title, string message, int width, int height)
    {
        var dialog = new Window
        {
            Title = "Hinweis",
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };

        var contentPanel = new StackPanel
        {
            Spacing = 15,
            VerticalAlignment = VerticalAlignment.Center
        };
        contentPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#B43232"),
            FontSize = 16
        });
        contentPanel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
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

    private async Task<bool> ShowConfirmDialog(string title, string message, int height)
    {
        bool confirmed = false;

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };
        grid.Children.Add(new TextBlock
        {
            Text = message,
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

        var btnConfirm = new Button
        {
            Content = "Bestätigen",
            Background = SolidColorBrush.Parse("#B43232"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };

        btnConfirm.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        btnCancel.Click += (_, _) => dialog.Close();

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnConfirm);
        grid.Children.Add(btnPanel);
        dialog.Content = grid;

        await dialog.ShowDialog(this);
        return confirmed;
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

        // word wrap
        _chkWordWrap = new CheckBox
        {
            Content = "Zeilenumbruch (Word Wrap)",
            IsChecked = _ctx.IsSqlMode ? AppSettings.IsSqlWordWrapEnabled : AppSettings.IsWordWrapEnabled,
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

        // --- font size sliders ---
        _sliderFontSize = new Slider
        {
            Minimum = 8,
            Maximum = 48,
            Value = AppSettings.EditorFontSize,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 0.5,
            IsSnapToTickEnabled = true
        };
        var txtFontSizeVal = new TextBox
        {
            Text = $"{AppSettings.EditorFontSize:0.0}",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 25
        };
        StyleAsLabelTextBox(txtFontSizeVal);

        _sliderSqlFontSize = new Slider
        {
            Minimum = 8,
            Maximum = 48,
            Value = AppSettings.SqlEditorFontSize,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 0.5,
            IsSnapToTickEnabled = true
        };
        var txtSqlFontSizeVal = new TextBox
        {
            Text = $"{AppSettings.SqlEditorFontSize:0.0}",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 25
        };
        StyleAsLabelTextBox(txtSqlFontSizeVal);

        // text box input validation and parsing
        void UpdateFontSizeFromText(TextBox textBox, Slider slider)
        {
            string input = textBox.Text?.Replace("px", "").Replace(',', '.') ?? string.Empty;
            if (double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                slider.Value = Math.Clamp(Math.Round(val * 2, MidpointRounding.AwayFromZero) / 2.0, slider.Minimum, slider.Maximum);
            }
            textBox.Text = $"{slider.Value:0.0}";
        }

        txtFontSizeVal.LostFocus += (_, _) => UpdateFontSizeFromText(txtFontSizeVal, _sliderFontSize);
        txtFontSizeVal.KeyUp += (_, ev) => { if (ev.Key == Key.Enter) UpdateFontSizeFromText(txtFontSizeVal, _sliderFontSize); };

        txtSqlFontSizeVal.LostFocus += (_, _) => UpdateFontSizeFromText(txtSqlFontSizeVal, _sliderSqlFontSize);
        txtSqlFontSizeVal.KeyUp += (_, ev) => { if (ev.Key == Key.Enter) UpdateFontSizeFromText(txtSqlFontSizeVal, _sliderSqlFontSize); };

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
                    "In der Prüfung müssen Fehler selbstständig gefunden werden. Es wird empfohlen ohne dieses Feature zu üben!\n\nAchtung: Diese Funktion setzt sich nach jedem Level-Wechsel zurück.",
                    400, 250
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
                    "Detaillierte Fehlerbeschreibungen stehen in der Prüfung nicht zur Verfügung. Nutze dies nur, wenn du absolut nicht weiterkommst.",
                    400, 180
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

        _chkWordWrap.IsCheckedChanged += (_, _) =>
        {
            if (_ctx.IsSqlMode)
            {
                AppSettings.IsSqlWordWrapEnabled = _chkWordWrap.IsChecked ?? false;
                _ctx.SqlQueryEditor.WordWrap = AppSettings.IsSqlWordWrapEnabled;
            }
            else
            {
                AppSettings.IsWordWrapEnabled = _chkWordWrap.IsChecked ?? false;
                _ctx.CodeEditor.WordWrap = AppSettings.IsWordWrapEnabled;
            }
            CheckChanges();
        };

        _sliderFontSize.ValueChanged += (_, ev) =>
        {
            AppSettings.EditorFontSize = ev.NewValue;
            txtFontSizeVal.Text = $"{AppSettings.EditorFontSize:0.0}";
            _ctx.CodeEditor.FontSize = AppSettings.EditorFontSize;
            _ctx.TutorialEditor.FontSize = AppSettings.EditorFontSize;
            CheckChanges();
        };

        _sliderSqlFontSize.ValueChanged += (_, ev) =>
        {
            AppSettings.SqlEditorFontSize = ev.NewValue;
            txtSqlFontSizeVal.Text = $"{AppSettings.SqlEditorFontSize:0.0}";
            _ctx.SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
            CheckChanges();
        };

        var fontRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            IsVisible = !_ctx.IsSqlMode
        };
        fontRow.Children.Add(_sliderFontSize);

        var fontSizeValPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2
        };
        fontSizeValPanel.Children.Add(txtFontSizeVal);
        fontSizeValPanel.Children.Add(new TextBlock
        {
            Text = "px",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        fontRow.Children.Add(fontSizeValPanel);

        var sqlFontRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            IsVisible = _ctx.IsSqlMode
        };
        sqlFontRow.Children.Add(_sliderSqlFontSize);

        var sqlFontSizeValPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2
        };
        sqlFontSizeValPanel.Children.Add(txtSqlFontSizeVal);
        sqlFontSizeValPanel.Children.Add(new TextBlock
        {
            Text = "px",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        sqlFontRow.Children.Add(sqlFontSizeValPanel);

        var lblCsharpFont = new TextBlock
        {
            Text = "C# Editor Schriftgröße",
            Foreground = Brushes.LightGray,
            IsVisible = !_ctx.IsSqlMode
        };
        var lblSqlFont = new TextBlock
        {
            Text = "SQL Editor Schriftgröße",
            Foreground = Brushes.LightGray,
            IsVisible = _ctx.IsSqlMode
        };

        string editorTitle = _ctx.IsSqlMode ? "SQL Query Editor" : "C# Code Editor";
        var editorContentPanel = new StackPanel { Spacing = 15 };

        editorContentPanel.Children.Add(new TextBlock
        {
            Text = editorTitle,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });

        // append font layouts
        editorContentPanel.Children.Add(lblCsharpFont);
        editorContentPanel.Children.Add(fontRow);
        editorContentPanel.Children.Add(lblSqlFont);
        editorContentPanel.Children.Add(sqlFontRow);

        // append settings checkboxes
        editorContentPanel.Children.Add(_chkSyntax);
        editorContentPanel.Children.Add(_chkAutocomplete);
        editorContentPanel.Children.Add(_chkWordWrap);
        editorContentPanel.Children.Add(_chkError);
        editorContentPanel.Children.Add(_chkErrorExplain);
        editorContentPanel.Children.Add(_chkVim);

        // wrap inner stackpanel into a scroll container
        _editorPanel = new ScrollViewer
        {
            Content = editorContentPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
    }

    private void StyleAsLabelTextBox(TextBox textBox)
    {
        textBox.Background = Brushes.Transparent;
        textBox.BorderBrush = Brushes.Transparent;
        textBox.Padding = new Thickness(4, 2);
        textBox.MinHeight = 0;

        textBox.PointerEntered += (_, _) =>
        {
            if (!textBox.IsFocused)
                textBox.Background = SolidColorBrush.Parse("#2D2D30");
        };

        textBox.PointerExited += (_, _) =>
        {
            if (!textBox.IsFocused)
            {
                textBox.Background = Brushes.Transparent;
                textBox.BorderBrush = Brushes.Transparent;
            }
        };

        textBox.GotFocus += (_, _) =>
        {
            textBox.Background = SolidColorBrush.Parse("#3C3C3C");
            textBox.BorderBrush = SolidColorBrush.Parse("#007ACC");
        };

        textBox.LostFocus += (_, _) =>
        {
            textBox.Background = textBox.IsPointerOver ? SolidColorBrush.Parse("#2D2D30") : Brushes.Transparent;
            textBox.BorderBrush = Brushes.Transparent;
        };
    }

    private void BuildDisplayPanel()
    {
        // clamp ui scale and round to whole percentage before loading
        AppSettings.UiScale = Math.Clamp(Math.Round(AppSettings.UiScale * 100) / 100.0, 0.5, 2.0);

        _sliderScale = new Slider
        {
            Minimum = 0.5,
            Maximum = 2.0,
            Value = AppSettings.UiScale,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            TickFrequency = 0.01,
            IsSnapToTickEnabled = true
        };
        var txtScaleVal = new TextBox
        {
            Text = $"{AppSettings.UiScale * 100:F0}",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 25
        };
        StyleAsLabelTextBox(txtScaleVal);

        // text box input parsing for scale percentage
        void UpdateScaleFromText()
        {
            string input = txtScaleVal.Text?.Replace("%", "").Trim() ?? "100";
            if (double.TryParse(input.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                // round to whole number before clamping
                double roundedVal = Math.Round(val);
                _sliderScale.Value = Math.Clamp(roundedVal / 100.0, _sliderScale.Minimum, _sliderScale.Maximum);
            }
            txtScaleVal.Text = $"{_sliderScale.Value * 100:F0}";
        }

        txtScaleVal.LostFocus += (_, _) => UpdateScaleFromText();
        txtScaleVal.KeyUp += (_, ev) =>
        {
            if (ev.Key == Key.Enter) UpdateScaleFromText();
        };

        var chkAutoSaveLayout = new CheckBox
        {
            Content = "App-Layout (automatisch) speichern",
            IsChecked = AppSettings.IsLayoutAutoSaveEnabled,
            Foreground = Brushes.White
        };
        ToolTip.SetTip(chkAutoSaveLayout, "Speichert sowie lädt App-Layout Änderungen automatisch (der Level-Designer wird separat behandelt)");

        var btnResetLayout = new Button
        {
            Content = "Layout auf Standard zurücksetzen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 5, 0, 0)
        };

        // event handlers
        _sliderScale.ValueChanged += (_, ev) =>
        {
            double roundedScale = Math.Round(ev.NewValue * 100) / 100.0;
            AppSettings.UiScale = roundedScale;
            txtScaleVal.Text = $"{roundedScale * 100:F0}";
            _ctx.ApplyUiScale();
            CheckChanges();
        };

        chkAutoSaveLayout.IsCheckedChanged += (_, _) =>
        {
            AppSettings.IsLayoutAutoSaveEnabled = chkAutoSaveLayout.IsChecked ?? false;
            if (!AppSettings.IsLayoutAutoSaveEnabled)
            {
                _ctx.DeleteSavedLayout();
            }
            CheckChanges();
        };

        btnResetLayout.Click += (_, _) => _ctx.ResetAppLayout();

        var scaleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        scaleRow.Children.Add(_sliderScale);

        var scaleValPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2
        };
        scaleValPanel.Children.Add(txtScaleVal);
        scaleValPanel.Children.Add(new TextBlock
        {
            Text = "%",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        scaleRow.Children.Add(scaleValPanel);

        _displayPanel = new StackPanel { Spacing = 15 };
        _displayPanel.Children.Add(new TextBlock
        {
            Text = "Darstellung",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        _displayPanel.Children.Add(new TextBlock
        {
            Text = "UI Skalierung",
            Foreground = Brushes.LightGray
        });
        _displayPanel.Children.Add(scaleRow);
        _displayPanel.Children.Add(new Border
        {
            Height = 1,
            Background = SolidColorBrush.Parse("#333"),
            Margin = new Thickness(0, 10, 0, 10)
        });
        _displayPanel.Children.Add(chkAutoSaveLayout);
        _displayPanel.Children.Add(btnResetLayout);
    }

    private void BuildDataPanel()
    {
        // portable mode
        _chkPortable = new CheckBox
        {
            Content = "Portable Mode",
            IsChecked = _isPortable,
            Foreground = Brushes.White
        };
        var txtPortableInfo = new TextBlock
        {
            Text = "Wenn aktiviert, wird der Speicherstand direkt neben der ausführbaren Datei gespeichert. Ideal für USB-Sticks und empfohlen für Schulcomputer.",
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

        var innerContentPanel = new StackPanel { Spacing = 15 };

        innerContentPanel.Children.Add(new TextBlock
        {
            Text = "Daten & Speicher",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        innerContentPanel.Children.Add(_chkPortable);
        innerContentPanel.Children.Add(txtPortableInfo);

        // backup and recovery
        innerContentPanel.Children.Add(new Border
        {
            Height = 1,
            Background = SolidColorBrush.Parse("#333"),
            Margin = new Thickness(0, 15, 0, 15)
        });

        innerContentPanel.Children.Add(new TextBlock
        {
            Text = "Backup / Wiederherstellung",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });

        innerContentPanel.Children.Add(new TextBlock
        {
            Text = "Übertrage deinen Fortschritt als Code auf ein anderes Gerät. Hinweis: Die GitHub-Anmeldung wird aus Sicherheitsgründen nicht mit kopiert.",
            Foreground = Brushes.LightGray,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 15)
        });

        var btnGenerateOffline = new Button
        {
            Content = "Code generieren",
            Width = 155,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(15, 8)
        };
        ToolTip.SetTip(btnGenerateOffline, "Generiert lokal einen langen Code ohne Ablaufdatum oder benötigten Internetanschluss");

        var btnGenerateOnline = new Button
        {
            Content = "Kurz-Code generieren",
            Width = 185,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = SolidColorBrush.Parse("#32A852"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(15, 8)
        };
        ToolTip.SetTip(btnGenerateOnline, "Generiert einen 8-stelligen Code online durch 'Pastefy' der nach 14 Tagen abläuft");

        var btnGeneratePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        btnGeneratePanel.Children.Add(btnGenerateOffline);
        btnGeneratePanel.Children.Add(btnGenerateOnline);

        var exportCodePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            IsVisible = false,
            Margin = new Thickness(0, 10, 0, 5)
        };

        var txtExport = new TextBox
        {
            IsReadOnly = true,
            Width = 300,
            FontFamily = new FontFamily("Consolas, monospace"),
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = SolidColorBrush.Parse("#007ACC"),
            BorderBrush = SolidColorBrush.Parse("#333"),
            CornerRadius = new CornerRadius(6),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var btnCopyExport = new Button
        {
            Content = _ctx.LoadIcon("assets/icons/ic_copy.svg", 18),
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(btnCopyExport, "Code kopieren");

        exportCodePanel.Children.Add(txtExport);
        exportCodePanel.Children.Add(btnCopyExport);

        btnGenerateOffline.Click += (_, _) =>
        {
            string rawCode = SaveSystem.ExportSaveString();
            string formattedCode = $"AEC-SAVE-{DateTime.Now:dd.MM.yyyy}-{rawCode}";

            txtExport.Text = formattedCode;
            txtExport.Foreground = SolidColorBrush.Parse("#007ACC");
            txtExport.FontSize = 14;
            txtExport.FontWeight = FontWeight.Normal;
            txtExport.Width = 300;
            txtExport.TextAlignment = TextAlignment.Left;

            exportCodePanel.IsVisible = true;
        };

        btnGenerateOnline.Click += async (_, _) =>
        {
            btnGenerateOnline.Content = "Wird generiert...";
            btnGenerateOnline.IsEnabled = false;

            string rawCode = SaveSystem.ExportSaveString();
            string formattedCode = $"AEC-SAVE-{DateTime.Now:dd.MM.yyyy}-{rawCode}";

            string? shortId = await UploadToPastefy(formattedCode);

            if (shortId != null)
            {
                txtExport.Text = shortId;
                txtExport.Foreground = SolidColorBrush.Parse("#32A852"); // green for short code
                txtExport.FontSize = 17;
                txtExport.FontWeight = FontWeight.Bold;
                txtExport.Width = 300;
                txtExport.TextAlignment = TextAlignment.Center;

                exportCodePanel.IsVisible = true;
            }
            else
            {
                await ShowWarningDialog("Netzwerkfehler", "Es konnte keine Verbindung zu Pastefy hergestellt werden. Bitte überprüfe deine Internetverbindung oder nutze den Offline-Code.", 380, 180);
            }

            // spam prevention
            for (int i = 5; i > 0; i--)
            {
                btnGenerateOnline.Content = $"Bitte warten... ({i}s)";
                await Task.Delay(1000); // wait 1 second per loop
            }

            btnGenerateOnline.Content = "Kurz-Code generieren";
            btnGenerateOnline.IsEnabled = true;
        };

        btnCopyExport.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(txtExport.Text))
            {
                await topLevel.Clipboard.SetTextAsync(txtExport.Text);
                btnCopyExport.Background = SolidColorBrush.Parse("#2E8B57"); // flash green
                btnCopyExport.Content = _ctx.LoadIcon("assets/icons/ic_success.svg", 18);
                await Task.Delay(500);
                btnCopyExport.Background = SolidColorBrush.Parse("#3C3C3C");
                btnCopyExport.Content = _ctx.LoadIcon("assets/icons/ic_copy.svg", 18);
            }
        };

        var separator = new Border
        {
            Height = 1,
            Background = SolidColorBrush.Parse("#303030"),
            Margin = new Thickness(0, 5, 0, 5)
        };

        var importPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 5, 0, 10)
        };

        var txtImport = new TextBox
        {
            Watermark = "Code hier einfügen...",
            Width = 300,
            FontFamily = new FontFamily("Consolas, monospace"),
            Background = Brushes.Transparent,
            BorderBrush = SolidColorBrush.Parse("#555"),
            CornerRadius = new CornerRadius(4),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var btnImport = new Button
        {
            Content = _ctx.LoadIcon("assets/icons/ic_load.svg", 18),
            Background = SolidColorBrush.Parse("#007ACC"),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(btnImport, "Code laden");

        importPanel.Children.Add(txtImport);
        importPanel.Children.Add(btnImport);

        var btnRevert = new Button
        {
            Content = "Letzten Import rückgängig machen",
            Background = SolidColorBrush.Parse("#B43232"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(15, 8),
            IsVisible = SaveSystem.HasBackup()
        };

        btnImport.Click += async (_, _) =>
        {
            string input = txtImport.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(input)) return;

            btnImport.IsEnabled = false;

            string actualCode = input;
            string dateStr = "Unbekannt";
            bool isOnline = false;

            // short Code parsing via pastefy
            if (!input.StartsWith("AEC-SAVE-") && input.Length < 50)
            {
                string? fetched = await FetchFromPastefy(input);
                if (!string.IsNullOrEmpty(fetched) && fetched.StartsWith("AEC-SAVE-"))
                {
                    isOnline = true;
                    var fetchedParts = fetched.Split(new[] { '-' }, 4);
                    if (fetchedParts.Length == 4)
                    {
                        dateStr = fetchedParts[2];
                        actualCode = fetchedParts[3];
                    }
                }
                else
                {
                    await ShowWarningDialog("Fehler", "Der eingegebene Kurz-Code ist ungültig, abgelaufen oder es besteht keine Internetverbindung.", 380, 180);
                    btnImport.IsEnabled = true;
                    return;
                }
            }
            // standard new format parsing
            else if (input.StartsWith("AEC-SAVE-"))
            {
                var parts = input.Split(new[] { '-' }, 4);
                if (parts.Length == 4)
                {
                    dateStr = parts[2];
                    actualCode = parts[3];
                }
            }

            // provide context about the loaded code inside confirmation prompt
            string confirmMsg = isOnline
                ? $"Möchtest du das Online-Backup vom {dateStr} wirklich laden?"
                : $"Möchtest du das lokale Backup vom {dateStr} wirklich laden?";

            if (dateStr == "Unbekannt")
                confirmMsg = "Möchtest du dieses ältere oder unformatierte Backup laden?";

            if (SaveSystem.HasActiveSave())
                confirmMsg += "\n\nDein aktueller Spielstand wird überschrieben! Ein lokales Notfall-Backup wird zur Sicherheit angelegt.";

            bool confirm = await ShowConfirmDialog("Spielstand überschreiben?", confirmMsg, 200);

            if (!confirm)
            {
                btnImport.IsEnabled = true;
                return;
            }

            bool success = SaveSystem.ImportSaveString(actualCode);
            if (success)
            {
                btnRevert.IsVisible = SaveSystem.HasBackup();
                txtImport.Text = string.Empty;
                await ShowWarningDialog("Erfolgreich", "Der Spielstand wurde erfolgreich geladen!\n\nDas Programm wird nun automatisch neu gestartet, um die Änderungen zu übernehmen.", 400, 200);

                RestartApplication();
            }
            else
            {
                await ShowWarningDialog("Fehler", "Der eingegebene Save-Code ist ungültig oder beschädigt.", 350, 150);
            }

            btnImport.IsEnabled = true;
        };

        btnRevert.Click += async (_, _) =>
        {
            bool confirm = await ShowConfirmDialog("Rückgängig machen?", "Möchtest du den Zustand vor dem letzten Import wiederherstellen?", 140);
            if (confirm)
            {
                SaveSystem.RevertSave();
                await ShowWarningDialog("Erfolgreich", "Der vorherige Zustand wurde wiederhergestellt.\n\nDas Programm wird nun automatisch neu gestartet, um die Änderungen zu übernehmen.", 400, 200);

                RestartApplication();
            }
        };

        innerContentPanel.Children.Add(btnGeneratePanel);
        innerContentPanel.Children.Add(exportCodePanel);
        innerContentPanel.Children.Add(separator);
        innerContentPanel.Children.Add(importPanel);
        innerContentPanel.Children.Add(btnRevert);

        _dataPanel = new ScrollViewer
        {
            Content = innerContentPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
    }

    private async Task<string?> UploadToPastefy(string content)
    {
        try
        {
            using var http = new HttpClient();
            var payload = new
            {
                title = "AbiturEliteCode Backup",
                content = content,
                visibility = "UNLISTED",
                expire_at = DateTime.Now.AddDays(14).ToString("yyyy-MM-dd HH:mm:ss") // expires in 14 days
            };

            var json = JsonSerializer.Serialize(payload);
            var body = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await http.PostAsync("https://pastefy.app/api/v2/paste", body);
            if (response.IsSuccessStatusCode)
            {
                var resJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(resJson);

                if (doc.RootElement.TryGetProperty("paste", out var pasteNode) &&
                    pasteNode.TryGetProperty("id", out var idNode))
                {
                    return idNode.GetString();
                }
            }
        }
        catch { }

        return null;
    }

    private async Task<string?> FetchFromPastefy(string id)
    {
        try
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"https://pastefy.app/api/v2/paste/{id}");
            if (response.IsSuccessStatusCode)
            {
                var resJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(resJson);

                if (doc.RootElement.TryGetProperty("content", out var contentNode))
                {
                    return contentNode.GetString();
                }
            }
        }
        catch { }

        return null;
    }

    private void RestartApplication()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            // set flag to bypass auto save on close (prevents saves to be overwritten)
            if (desktop.MainWindow is MainWindow mw)
            {
                mw.SkipSaveOnExit = true;
            }

            var module = System.Diagnostics.Process.GetCurrentProcess().MainModule;
            if (module != null)
            {
                System.Diagnostics.Process.Start(module.FileName);
            }

            desktop.Shutdown();
        }
        else
        {
            Environment.Exit(0);
        }
    }


    private void BuildUpdatesPanel()
    {
        _chkAutoUpdate = new CheckBox
        {
            Content = "Beim Start automatisch nach Updates suchen",
            // visually force to true if community features are enabled
            IsChecked = AppSettings.IsCommunityFeaturesEnabled ? true : AppSettings.AutoCheckForUpdates,
            Foreground = Brushes.White,
            // lock control if community is enabled
            IsHitTestVisible = !AppSettings.IsCommunityFeaturesEnabled,
            Opacity = AppSettings.IsCommunityFeaturesEnabled ? 0.5 : 1.0
        };

        var autoUpdateWrapper = new Panel
        {
            Background = Brushes.Transparent
        };
        autoUpdateWrapper.Children.Add(_chkAutoUpdate);
        if (AppSettings.IsCommunityFeaturesEnabled)
        {
            ToolTip.SetTip(autoUpdateWrapper, "Community Features benötigen die aktuellste App-Version. Die automatische Update-Suche ist daher erzwungen.");
        }

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
            if (AppSettings.IsCommunityFeaturesEnabled) return;

            AppSettings.AutoCheckForUpdates = _chkAutoUpdate.IsChecked ?? false;
            CheckChanges();
        };

        _btnCheckUpdate.Click += async (_, _) =>
        {
            _btnCheckUpdate.Content = "Suche...";
            _btnCheckUpdate.IsEnabled = false;

            var result = await UpdateManager.CheckForUpdatesAsync();

            // handle network error
            if (result.Status == UpdateManager.UpdateStatus.NetworkError)
            {
                _txtVersionInfo.Text = "Keine Internetverbindung.";
                _txtVersionInfo.Foreground = Brushes.Red;
            }
            else if (result.UpdateAvailable)
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

        var actionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        actionRow.Children.Add(_btnCheckUpdate);
        actionRow.Children.Add(_btnUpdateApp);

        var separator = new Border
        {
            Height = 1,
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Margin = new Thickness(0, 10, 0, 0)
        };

        // create inner content panel for updates
        var innerContentPanel = new StackPanel
        {
            Spacing = 15
        };
        innerContentPanel.Children.Add(new TextBlock
        {
            Text = "Updates",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        innerContentPanel.Children.Add(autoUpdateWrapper);
        innerContentPanel.Children.Add(_txtVersionInfo);
        innerContentPanel.Children.Add(_updateProgressBar);
        innerContentPanel.Children.Add(actionRow);
        innerContentPanel.Children.Add(separator);

        _patchNotesPanel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 0)
        };
        innerContentPanel.Children.Add(_patchNotesPanel);

        var updateScrollViewer = new ScrollViewer
        {
            Content = innerContentPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        // scroll to top button
        _btnScrollTop = new Button
        {
            Content = _ctx.LoadIcon("assets/icons/ic_arrow_up.svg", 28),
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = SolidColorBrush.Parse("#3C3C3C"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 15, 25, 0),
            IsVisible = false
        };

        _btnScrollTop.Click += (_, _) => updateScrollViewer.Offset = new Vector(0, 0);

        // infinite scrolling / scroll to top
        updateScrollViewer.ScrollChanged += async (sender, _) =>
        {
            var sv = (ScrollViewer)sender!;

            // show button only after scrolling down a good amount
            _btnScrollTop.IsVisible = sv.Offset.Y > 180;

            // check if scrolled near the bottom (20px threshold)
            if (sv.Extent.Height > 0 && sv.Viewport.Height + sv.Offset.Y >= sv.Extent.Height - 20)
            {
                await LoadMorePatchNotesAsync();
            }
        };

        var containerGrid = new Grid();
        containerGrid.Children.Add(updateScrollViewer);
        containerGrid.Children.Add(_btnScrollTop);

        _updatesPanel = containerGrid;

        _ = LoadInitialPatchNotesAsync();
    }

    private async Task LoadInitialPatchNotesAsync()
    {
        if (_isLoadingReleases) return;
        _isLoadingReleases = true;

        try
        {
            _cachedReleases ??= await UpdateManager.GetAllReleasesAsync();

            _patchNotesPanel.Children.Clear();
            _loadedReleasesCount = 0;
            _reachedFirstVersion = false;

            if (_cachedReleases.Count > 0)
            {
                // load only the most recent patch note initially
                AddPatchNoteUI(_cachedReleases[0]);
                _loadedReleasesCount = 1;

                if (_cachedReleases[0].Version == "0.1.0")
                    _reachedFirstVersion = true;
            }
        }
        finally
        {
            _isLoadingReleases = false;
        }
    }

    private async Task LoadMorePatchNotesAsync()
    {
        if (_isLoadingReleases || _reachedFirstVersion || _cachedReleases == null) return;
        _isLoadingReleases = true;

        try
        {
            int take = 3;
            int added = 0;

            // load the next batches iteratively
            while (added < take && _loadedReleasesCount < _cachedReleases.Count)
            {
                var release = _cachedReleases[_loadedReleasesCount];
                AddPatchNoteUI(release);

                _loadedReleasesCount++;
                added++;

                // break condition: reached version 0.1.0 (initial release)
                if (release.Version == "0.1.0")
                {
                    _reachedFirstVersion = true;
                    break;
                }
            }
        }
        finally
        {
            _isLoadingReleases = false;
        }
    }

    private void AddPatchNoteUI((string Version, string Body) release)
    {
        Version.TryParse(UpdateManager.CurrentVersion, out var currentVer);
        Version.TryParse(release.Version, out var releaseVer);

        bool isCurrentVersion = releaseVer != null && currentVer != null && releaseVer == currentVer;
        bool isNewerVersion = releaseVer != null && currentVer != null && releaseVer > currentVer;

        string titleText = $"Patch Notes {release.Version}";
        if (isNewerVersion) titleText += " (Neu)";

        var header = new TextBlock
        {
            Text = titleText,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            // highlight current version in green, others in the default (blue)
            Foreground = SolidColorBrush.Parse(isCurrentVersion ? "#32A852" : "#007ACC"),
            Margin = new Thickness(0, 15, 0, 5)
        };

        var bodyPanel = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 0, 0, 10)
        };

        // parse markdown lines
        MarkdownRenderer.RenderMarkdownToPanel(bodyPanel, release.Body, isSqlMode: false, useSelectableText: false);

        var separator = new Border
        {
            Height = 1,
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Margin = new Thickness(0, 5, 0, 5)
        };

        _patchNotesPanel.Children.Add(header);
        _patchNotesPanel.Children.Add(bodyPanel);
        _patchNotesPanel.Children.Add(separator);
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

        _chkCommunityFeatures = new CheckBox
        {
            Content = "Community Features",
            IsChecked = AppSettings.IsCommunityFeaturesEnabled,
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeight.Bold
        };
        ToolTip.SetTip(_chkCommunityFeatures, "Erlaube das Laden von Kommentaren und Likes über GitHub");

        // github status and login button
        _txtGithubStatus = new TextBlock
        {
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 5, 0, 10)
        };

        _btnGithubLogin = new Button
        {
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4)
        };

        var communityDescription = new TextBlock
        {
            Text = "Aktiviere diese Funktion und melde dich an, um Level zu bewerten und Kommentare zu schreiben.",
            Foreground = Brushes.LightGray,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        _communitySubPanel = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _communitySubPanel.Children.Add(communityDescription);
        _communitySubPanel.Children.Add(_txtGithubStatus);
        _communitySubPanel.Children.Add(_btnGithubLogin);

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

        _chkCommunityFeatures.IsCheckedChanged += async (_, _) =>
        {
            if (_suppressCommunityHandler) return;

            bool enabling = _chkCommunityFeatures.IsChecked ?? false;

            // confirm sign out when disabling while logged in
            if (!enabling && !string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                bool confirmed = await ShowCommunityDisableDialog();
                if (!confirmed)
                {
                    _suppressCommunityHandler = true;
                    _chkCommunityFeatures.IsChecked = true;
                    _suppressCommunityHandler = false;
                    return;
                }

                // prompt the user if they want to wait for the api queue to finish
                if (!await CheckApiQueueBeforeLogout())
                {
                    _suppressCommunityHandler = true;
                    _chkCommunityFeatures.IsChecked = true;
                    _suppressCommunityHandler = false;
                    return;
                }

                MainWindow.ClearApiQueue();

                // sign out
                AppSettings.GithubToken = string.Empty;
                // leave username to be able to compare it on next login
                SaveSystem.DeleteToken();
                AppSettings.ApplyTo(_ctx.PlayerData.Settings);
                SaveSystem.Save(_ctx.PlayerData);
            }

            AppSettings.IsCommunityFeaturesEnabled = enabling;

            // update the visually enforced auto update checkbox
            if (_chkAutoUpdate != null)
            {
                if (enabling)
                {
                    _chkAutoUpdate.IsChecked = true;
                    _chkAutoUpdate.IsHitTestVisible = false;
                    _chkAutoUpdate.Opacity = 0.5;
                    var wrapper = _chkAutoUpdate.Parent as Panel;
                    if (wrapper != null) ToolTip.SetTip(wrapper, "Community Features benötigen die aktuellste App-Version. Die automatische Update-Suche ist daher erzwungen.");
                }
                else
                {
                    _chkAutoUpdate.IsChecked = AppSettings.AutoCheckForUpdates;
                    _chkAutoUpdate.IsHitTestVisible = true;
                    _chkAutoUpdate.Opacity = 1.0;
                    var wrapper = _chkAutoUpdate.Parent as Panel;
                    if (wrapper != null) ToolTip.SetTip(wrapper, null);
                }
            }

            // save immediately
            AppSettings.ApplyTo(_ctx.PlayerData.Settings);
            SaveSystem.Save(_ctx.PlayerData);

            // refresh snapshot so CheckChanges() doesnt count this as a pending change
            _refreshSnapshot();

            UpdateGithubUiState();
            NotifyMainWindowCommunityState();
        };

        _btnGithubLogin.Click += async (_, _) =>
        {
            if (!string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                // prompt user if they want to wait for api queue to finish
                if (!await CheckApiQueueBeforeLogout()) return;

                MainWindow.ClearApiQueue();

                AppSettings.GithubToken = string.Empty;
                // leave username as is
                SaveSystem.DeleteToken();

                // save immediately
                AppSettings.ApplyTo(_ctx.PlayerData.Settings);
                SaveSystem.Save(_ctx.PlayerData);

                // refresh snapshot so this doesnt appear as an unsaved change
                _refreshSnapshot();

                UpdateGithubUiState();
                NotifyMainWindowCommunityState();
            }
            else
            {
                await ShowGithubLoginDialog();
            }
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
        _miscPanel.Children.Add(new Border
        {
            Height = 1,
            Background = SolidColorBrush.Parse("#333"),
            Margin = new Thickness(0, 10, 0, 10)
        });
        _miscPanel.Children.Add(_chkCommunityFeatures);
        _miscPanel.Children.Add(_communitySubPanel);

        UpdateGithubUiState();
    }

    private async Task<bool> ShowCommunityDisableDialog()
    {
        bool confirmed = false;

        var dialog = new Window
        {
            Title = "Community Features deaktivieren?",
            Width = 380,
            Height = 175,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) => 
        {
            if (ev.Key == Key.Escape)
                dialog.Close();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };
        grid.Children.Add(new TextBlock
        {
            Text = "Du bist aktuell angemeldet. Das Deaktivieren der Community Features meldet dich automatisch ab. Fortfahren?",
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

        var btnConfirm = new Button
        {
            Content = "Ja, abmelden",
            Background = SolidColorBrush.Parse("#B43232"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };

        btnConfirm.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };
        btnCancel.Click += (_, _) => dialog.Close();

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnConfirm);
        grid.Children.Add(btnPanel);
        dialog.Content = grid;

        await dialog.ShowDialog(this);
        return confirmed;
    }

    private void UpdateGithubUiState()
    {
        bool isCommunityEnabled = AppSettings.IsCommunityFeaturesEnabled;
        bool isLoggedIn = !string.IsNullOrEmpty(AppSettings.GithubToken);

        // gray out sub-panel when community features are disabled
        _communitySubPanel.Opacity = isCommunityEnabled ? 1.0 : 0.4;
        _btnGithubLogin.IsEnabled = isCommunityEnabled;

        if (!isCommunityEnabled)
        {
            _txtGithubStatus.Text = "Status: Community Features deaktiviert";
            _btnGithubLogin.Content = "Mit GitHub anmelden";
            _btnGithubLogin.Background = SolidColorBrush.Parse("#2ea043"); // green
            return;
        }

        if (isLoggedIn)
        {
            string displayName = string.IsNullOrEmpty(AppSettings.GithubUsername)
                ? "Unbekannt"
                : AppSettings.GithubUsername;
            _txtGithubStatus.Text = $"Status: Angemeldet als {displayName}";
            _btnGithubLogin.Content = "Abmelden";
            _btnGithubLogin.Background = SolidColorBrush.Parse("#B43232"); // red
        }
        else
        {
            _txtGithubStatus.Text = "Status: Nicht angemeldet";
            _btnGithubLogin.Content = "Mit GitHub anmelden";
            _btnGithubLogin.Background = SolidColorBrush.Parse("#2ea043"); // green
        }
    }

    private async Task ShowGithubLoginDialog()
    {
        _loginCts = new CancellationTokenSource();
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AbiturEliteCode");

        // request device code
        var deviceResp = await http.PostAsync("https://github.com/login/device/code",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("scope", "public_repo")
            }
        ));

        var deviceData = JsonDocument.Parse(await deviceResp.Content.ReadAsStringAsync());
        string userCode = deviceData.RootElement.GetProperty("user_code").GetString()!;
        string deviceCode = deviceData.RootElement.GetProperty("device_code").GetString()!;
        string verificationUri = deviceData.RootElement.GetProperty("verification_uri").GetString()!;
        int interval = deviceData.RootElement.GetProperty("interval").GetInt32();

        var dialog = new Window
        {
            Title = "GitHub Login",
            Width = 450,
            Height = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };

        var contentPanel = new StackPanel
        {
            Spacing = 15,
            VerticalAlignment = VerticalAlignment.Center
        };

        var titlePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10
        };

        titlePanel.Children.Add(new TextBlock
        {
            Text = "GitHub Authentifizierung",
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#2ea043"),
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center
        });

        var btnInfo = new Button
        {
            Content = "?",
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Background = SolidColorBrush.Parse("#3C3C3C"),
            CornerRadius = new CornerRadius(15),
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(btnInfo, "Warum diese Berechtigungen?");

        // setup ripple effect
        var rippleBorder = new Border
        {
            Background = SolidColorBrush.Parse("#2ea043"),
            CornerRadius = new CornerRadius(15),
            Width = 24,
            Height = 24,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform
            {
                ScaleX = 1.0,
                ScaleY = 1.0
            }
        };

        var btnWrapper = new Panel();
        btnWrapper.Children.Add(rippleBorder);
        btnWrapper.Children.Add(btnInfo);

        bool isRippling = true;

        _ = Task.Run(async () =>
        {
            // loop while rippling is active and the dialog has not been closed/cancelled
            while (isRippling && _loginCts != null && !_loginCts.Token.IsCancellationRequested)
            {
                for (int i = 0; i <= 60 && isRippling && !_loginCts.Token.IsCancellationRequested; i++)
                {
                    await Task.Delay(16); // ~60 fps
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        double progress = i / 60.0;
                        rippleBorder.Opacity = 0.5 * (1.0 - progress);
                        if (rippleBorder.RenderTransform is ScaleTransform st)
                        {
                            st.ScaleX = 1.0 + (0.8 * progress);
                            st.ScaleY = 1.0 + (0.8 * progress);
                        }
                    });
                }

                if (isRippling && !_loginCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(600);
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() => rippleBorder.IsVisible = false);
        });

        btnInfo.Click += async (_, _) =>
        {
            isRippling = false; // stop ripple effect

            var infoDialog = new Window
            {
                Title = "Transparenz & Datenschutz",
                Width = 580,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };
            infoDialog.KeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Escape) infoDialog.Close();
            };

            var infoGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(20)
            };

            var contentStack = new StackPanel
            {
                Spacing = 15
            };

            contentStack.Children.Add(new TextBlock
            {
                Text = "Transparenz & Datenschutz",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = SolidColorBrush.Parse("#2ea043")
            });

            contentStack.Children.Add(new TextBlock
            {
                Text = "Um die Community Features nutzen zu können, fordert diese App eine spezifische GitHub-Berechtigungen an:",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });

            Control CreatePermItem(string num, string title, string code, string desc)
            {
                var pnl = new StackPanel
                {
                    Spacing = 4,
                    Margin = new Thickness(0, 5, 0, 10)
                };
                var titlePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };

                titlePanel.Children.Add(new Border
                {
                    Background = SolidColorBrush.Parse("#3C3C3C"),
                    CornerRadius = new CornerRadius(12),
                    Width = 24,
                    Height = 24,
                    Child = new TextBlock
                    {
                        Text = num,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = FontWeight.Bold,
                        FontSize = 12
                    }
                });

                titlePanel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 15
                });
                titlePanel.Children.Add(new TextBlock
                {
                    Text = $"({code})",
                    Foreground = SolidColorBrush.Parse("#888888"),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });

                pnl.Children.Add(titlePanel);

                var descText = new TextBlock
                {
                    Text = desc,
                    Foreground = SolidColorBrush.Parse("#CCCCCC"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(34, 0, 0, 0),
                    LineHeight = 18
                };
                pnl.Children.Add(descText);

                return pnl;
            }

            contentStack.Children.Add(CreatePermItem("1", "Lesen von Repositories", "public_repo",
                "Wird benötigt, um die Level-Diskussionen und Kommentare abrufen und hier anzeigen zu können, als auch diese zu bewerten."));

            var promiseCard = new Border
            {
                Background = SolidColorBrush.Parse("#1A1E1C"),
                BorderBrush = SolidColorBrush.Parse("#2ea043"),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 5, 0, 0)
            };

            var promiseStack = new StackPanel
            {
                Spacing = 10
            };
            promiseStack.Children.Add(new TextBlock
            {
                Text = "Unser Versprechen",
                FontWeight = FontWeight.Bold,
                Foreground = SolidColorBrush.Parse("#2ea043"),
                FontSize = 15
            });

            var promiseText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };

            const string repoUrl = "https://github.com/aec-community-bot/aec-community";

            promiseStack.Children.Add(new TextBlock
            {
                Text = "Wir greifen auf nichts anderes als deinen Benutzernamen zu und führen nur Aktionen auf Diskussionen in diesem Repository aus:",
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });

            var repoLink = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Left,
                Content = new TextBlock
                {
                    Text = repoUrl,
                    Foreground = SolidColorBrush.Parse("#6495ED"),
                    TextDecorations = TextDecorations.Underline,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            repoLink.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(repoUrl)
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            promiseStack.Children.Add(repoLink);

            promiseStack.Children.Add(new TextBlock
            {
                Text = "Das Repository wird automatisch stummgeschaltet, damit du niemals ungewollte E-Mails von GitHub erhältst.",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });

            promiseStack.Children.Add(promiseText);

            promiseStack.Children.Add(new TextBlock
            {
                Text = "Tipp: Wenn du dennoch Bedenken hast, kannst du jederzeit einen separaten GitHub-Account nur für die Community Features erstellen.",
                FontStyle = FontStyle.Italic,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -25, 0, 0)
            });

            promiseCard.Child = promiseStack;
            contentStack.Children.Add(promiseCard);

            var scroll = new ScrollViewer
            {
                Content = contentStack,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 10)
            };
            infoGrid.Children.Add(scroll);

            var btnCloseInfo = new Button
            {
                Content = "Verstanden",
                Background = SolidColorBrush.Parse("#2ea043"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(10, 6)
            };
            Grid.SetRow(btnCloseInfo, 1);
            btnCloseInfo.Click += (s, ev) => infoDialog.Close();

            infoGrid.Children.Add(btnCloseInfo);
            infoDialog.Content = infoGrid;

            await infoDialog.ShowDialog(dialog);
        };

        titlePanel.Children.Add(btnWrapper);
        contentPanel.Children.Add(titlePanel);

        contentPanel.Children.Add(new TextBlock
        {
            Text = "1. Klicke auf 'Im Browser öffnen'.\n2. Füge den unten stehenden Code auf der GitHub-Seite ein.\n3. Bestätige die Autorisierung.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            LineHeight = 22
        });

        var codePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10
        };

        var txtCode = new TextBox
        {
            Text = userCode, // dynamically bind the real user code
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Consolas, monospace"),
            TextAlignment = TextAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsReadOnly = true,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = SolidColorBrush.Parse("#007ACC"),
            BorderBrush = SolidColorBrush.Parse("#333"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8,4),
            Margin = new Thickness(0, 10, 0, 10)
        };

        var btnCopyCode = new Button
        {
            Content = _ctx.LoadIcon("assets/icons/ic_copy.svg", 18),
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center
        };

        btnCopyCode.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(dialog);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(userCode);
                btnCopyCode.Background = SolidColorBrush.Parse("#2E8B57"); // flash green
                btnCopyCode.Content = _ctx.LoadIcon("assets/icons/ic_success.svg", 18);
                await Task.Delay(500);
                btnCopyCode.Background = SolidColorBrush.Parse("#3C3C3C");
                btnCopyCode.Content = _ctx.LoadIcon("assets/icons/ic_copy.svg", 18);
            }
        };

        codePanel.Children.Add(txtCode);
        codePanel.Children.Add(btnCopyCode);
        contentPanel.Children.Add(codePanel);

        grid.Children.Add(contentPanel);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(btnPanel, 1);

        var btnOpenBrowser = new Button
        {
            Content = "Im Browser öffnen",
            Background = SolidColorBrush.Parse("#2ea043"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };

        btnOpenBrowser.Click += (_, _) =>
        {
            // automatically copy to clipboard (ux)
            TopLevel.GetTopLevel(dialog)?.Clipboard?.SetTextAsync(userCode);
            UpdateManager.OpenBrowser(verificationUri);
        };
        btnCancel.Click += (_, _) => dialog.Close();

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnOpenBrowser);
        grid.Children.Add(btnPanel);

        dialog.Content = grid;

        // start polling in background
        _ = Task.Run(async () => {
            while (!_loginCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval + 1));

                var pollResp = await http.PostAsync("https://github.com/login/oauth/access_token",
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("client_id", ClientId),
                        new KeyValuePair<string, string>("device_code", deviceCode),
                        new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                    }
                ));

                var pollDoc = JsonDocument.Parse(await pollResp.Content.ReadAsStringAsync());
                if (pollDoc.RootElement.TryGetProperty("access_token", out var tokenProp))
                {
                    await Dispatcher.UIThread.InvokeAsync(async () => {
                        AppSettings.GithubToken = tokenProp.GetString()!;

                        // fetch github username
                        try
                        {
                            using var userClient = new HttpClient();
                            userClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppSettings.GithubToken}");
                            userClient.DefaultRequestHeaders.UserAgent.ParseAdd("AbiturEliteCode");
                            userClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                            var userResp = await userClient.GetAsync("https://api.github.com/user");
                            if (userResp.IsSuccessStatusCode)
                            {
                                var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync());
                                if (userDoc.RootElement.TryGetProperty("login", out var loginProp))
                                {
                                    string newUsername = loginProp.GetString() ?? string.Empty;

                                    // clear community cache only if logged in as a different user (not on logout)
                                    if (!string.IsNullOrEmpty(AppSettings.GithubUsername) && AppSettings.GithubUsername != newUsername)
                                    {
                                        SaveSystem.ClearCommunityUserState();
                                    }

                                    AppSettings.GithubUsername = newUsername;
                                }
                            }
                        }
                        catch { }

                        // auto-ignore the repository to prevent github emails/notifications natively
                        try
                        {
                            using var ignoreClient = new HttpClient();
                            ignoreClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppSettings.GithubToken}");
                            ignoreClient.DefaultRequestHeaders.UserAgent.ParseAdd("AbiturEliteCode");
                            ignoreClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                            var ignorePayload = new StringContent("{\"ignored\": true}", Encoding.UTF8, "application/json");
                            await ignoreClient.PutAsync("https://api.github.com/repos/aec-community-bot/aec-community/subscription", ignorePayload);
                        }
                        catch { }

                        // save immediately
                        AppSettings.ApplyTo(_ctx.PlayerData.Settings);
                        SaveSystem.Save(_ctx.PlayerData);
                        SaveSystem.SaveToken(AppSettings.GithubToken, AppSettings.InstallKey);

                        // refresh snapshot so login doesnt appear as an unsaved change
                        _refreshSnapshot();

                        UpdateGithubUiState();
                        NotifyMainWindowCommunityState();
                        dialog.Close();
                    });
                    break;
                }

                if (pollDoc.RootElement.TryGetProperty("error", out var err) && err.GetString() == "expired_token") break;
            }
        });

        await dialog.ShowDialog(this);
        _loginCts.Cancel(); // stop polling if window is closed manually
    }

    private async Task<bool> CheckApiQueueBeforeLogout()
    {
        if (MainWindow.GetApiQueueSnapshot().Count == 0 && MainWindow.GetApiQueueInFlightCount() == 0)
            return true;

        return await ShowLogoutApiQueueDialog();
    }

    private async Task<bool> ShowLogoutApiQueueDialog()
    {
        return await ApiQueueDialog.ShowAsync(this, new ApiQueueDialogConfig
        {
            SubtitleText = "Es befinden sich noch Aktionen in der Warteschlange. Wenn du dich jetzt abmeldest, werden diese abgebrochen und gehen verloren.",
            CancelButtonText = "Abbrechen",
            DestructiveButtonText = "Trotzdem abmelden",
            GetSnapshot = MainWindow.GetApiQueueSnapshot,
            GetNextAvailableApiTime = MainWindow.GetNextAvailableApiTime,
            GetInFlightCount = MainWindow.GetApiQueueInFlightCount
        });
    }

    private void NotifyMainWindowCommunityState()
    {
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RefreshCommunityUI();
            }
        }
    }
}
