using System;
using System.Threading.Tasks;
using AbiturEliteCode.cs.MainWindow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        bool originalVimEnabled = AppSettings.IsVimEnabled;
        bool originalSqlVimEnabled = AppSettings.IsSqlVimEnabled;
        bool originalSyntaxEnabled = AppSettings.IsSyntaxHighlightingEnabled;
        bool originalSqlSyntaxEnabled = AppSettings.IsSqlSyntaxHighlightingEnabled;
        bool originalAutocompleteEnabled = AppSettings.IsAutocompleteEnabled;
        bool originalSqlAutocompleteEnabled = AppSettings.IsSqlAutocompleteEnabled;
        bool originalErrorEnabled = AppSettings.IsErrorHighlightingEnabled;
        bool originalErrorExplanation = AppSettings.IsErrorExplanationEnabled;
        double originalEditorFontSize = AppSettings.EditorFontSize;
        double originalSqlFontSize = AppSettings.SqlEditorFontSize;
        double originalUiScale = AppSettings.UiScale;
        bool originalAutoUpdateEnabled = AppSettings.AutoCheckForUpdates;
        bool isPortable = SaveSystem.IsPortableModeEnabled();
        bool originalPortableState = isPortable;
        bool originalSqlAntiSpoilerEnabled = AppSettings.IsSqlAntiSpoilerEnabled;
        bool originalDiscordRpcEnabled = AppSettings.IsDiscordRpcEnabled;

        var settingsWin = new Window
        {
            Title = "Einstellungen",
            Width = 600,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = BrushBgPanel,
            SystemDecorations = SystemDecorations.BorderOnly
        };

        var rootBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = BrushBgPanel,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160, *"),
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(0)
        };

        var leftPanelGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Background = SolidColorBrush.Parse("#2D2D30")
        };

        var categoriesPanel = new StackPanel();

        categoriesPanel.Children.Add(
            new TextBlock
            {
                Text = "Einstellungen",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextTitle,
                Margin = new Thickness(15)
            }
        );

        Button CreateCatBtn(string text, bool showBadge = false)
        {
            var btn = new Button
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            if (showBadge)
            {
                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*, Auto")
                };
                grid.Children.Add(new TextBlock
                {
                    Text = text,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var badge = new Border
                {
                    Background = SolidColorBrush.Parse("#B43232"),
                    Width = 8,
                    Height = 8,
                    CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0)
                };
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
                btn.Content = grid;
            }
            else
            {
                btn.Content = text;
            }

            return btn;
        }

        var btnCatEditor = CreateCatBtn("Editor");
        var btnCatDisplay = CreateCatBtn("Darstellung");
        var btnCatData = CreateCatBtn("Daten");
        var btnCatUpdates = CreateCatBtn("Updates", _updateAvailable);
        var btnCatMisc = CreateCatBtn("Sonstiges");

        categoriesPanel.Children.Add(btnCatEditor);
        categoriesPanel.Children.Add(btnCatDisplay);
        categoriesPanel.Children.Add(btnCatData);
        categoriesPanel.Children.Add(btnCatUpdates);
        categoriesPanel.Children.Add(btnCatMisc);

        leftPanelGrid.Children.Add(categoriesPanel);

        var actionButtonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10,
            Margin = new Thickness(10)
        };

        var btnSave = new Button
        {
            Width = 50,
            Height = 40,
            Background = SolidColorBrush.Parse("#32A852"),
            CornerRadius = new CornerRadius(5),
            IsEnabled = false,
            Opacity = 0.5
        };
        btnSave.Content = LoadIcon("assets/icons/ic_save.svg", 20);
        ToolTip.SetTip(btnSave, "Einstellungen speichern");

        var btnReset = new Button
        {
            Width = 50,
            Height = 40,
            Background = SolidColorBrush.Parse("#B43232"),
            CornerRadius = new CornerRadius(5)
        };
        btnReset.Content = LoadIcon("assets/icons/ic_restart.svg", 20);
        ToolTip.SetTip(btnReset, "Auf Standard zurücksetzen");

        actionButtonsPanel.Children.Add(btnSave);
        actionButtonsPanel.Children.Add(btnReset);

        Grid.SetRow(actionButtonsPanel, 1);
        leftPanelGrid.Children.Add(actionButtonsPanel);

        Grid.SetRowSpan(leftPanelGrid, 2);
        mainGrid.Children.Add(leftPanelGrid);

        var rightPanel = new Border
        {
            Padding = new Thickness(20),
            Background = BrushBgPanel
        };
        Grid.SetColumn(rightPanel, 1);
        mainGrid.Children.Add(rightPanel);

        // --- CONTROLS CREATION ---

        // syntax highlighting
        var chkSyntax = new CheckBox
        {
            Content = "Syntax-Hervorhebung",
            IsChecked = _isSqlMode
                ? AppSettings.IsSqlSyntaxHighlightingEnabled
                : AppSettings.IsSyntaxHighlightingEnabled,
            Foreground = Brushes.White
        };

        // autocompletion
        var chkAutocomplete = new CheckBox
        {
            Content = "Autovervollständigung",
            IsChecked = _isSqlMode ? AppSettings.IsSqlAutocompleteEnabled : AppSettings.IsAutocompleteEnabled,
            Foreground = Brushes.White
        };

        // error highlighting (c# only, for now)
        var chkError = new CheckBox
        {
            Content = "Error-Hervorhebung",
            IsChecked = AppSettings.IsErrorHighlightingEnabled,
            Foreground = Brushes.White,
            IsVisible = !_isSqlMode
        };

        var chkErrorExplain = new CheckBox
        {
            Content = "Error-Erklärungen",
            IsChecked = AppSettings.IsErrorExplanationEnabled,
            IsEnabled = AppSettings.IsErrorHighlightingEnabled,
            Foreground = Brushes.White,
            IsVisible = !_isSqlMode,
            Margin = new Thickness(20, 0, 0, 0) // indent
        };

        // vim controls (c# only)
        var chkVim = new CheckBox
        {
            Content = "Vim Steuerung",
            IsChecked = _isSqlMode ? AppSettings.IsSqlVimEnabled : AppSettings.IsVimEnabled,
            Foreground = Brushes.White
        };

        // lock vim toggle during tutorial
        if (_isTutorialMode)
        {
            chkVim.IsEnabled = false;
            ToolTip.SetTip(chkVim, "Während des Tutorials nicht änderbar");
        }

        // display settings
        var sliderScale = new Slider
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

        // editor font size
        var sliderFontSize = new Slider
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

        // sql font size
        var sliderSqlFontSize = new Slider
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

        // data settings
        var chkPortable = new CheckBox
        {
            Content = "Portable Mode",
            IsChecked = isPortable,
            Foreground = Brushes.White
        };
        var txtPortableInfo = new TextBlock
        {
            Text =
                "Wenn aktiviert, wird der Speicherstand direkt neben der ausführbaren Datei gespeichert. Ideal für USB-Sticks.",
            Foreground = Brushes.Gray,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(25, 0, 0, 0)
        };

        // check permissions
        bool canWriteRoot = SaveSystem.CanWriteToRoot();
        if (!canWriteRoot)
        {
            chkPortable.IsEnabled = false;
            chkPortable.Content += " (Keine Schreibrechte)";
            chkPortable.Foreground = Brushes.Gray;
            txtPortableInfo.Text =
                "Portable Mode ist hier nicht verfügbar, da keine Schreibrechte im Programmordner bestehen.";
            txtPortableInfo.Foreground = Brushes.Red;
        }

        // update settings panel
        var updatesSettingsPanel = new StackPanel { Spacing = 15 };
        updatesSettingsPanel.Children.Add(new TextBlock
        {
            Text = "Updates",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var chkAutoUpdate = new CheckBox
        {
            Content = "Beim Start automatisch nach Updates suchen",
            IsChecked = AppSettings.AutoCheckForUpdates,
            Foreground = Brushes.White
        };

        var txtVersionInfo = new TextBlock
        {
            Text = _updateAvailable
                ? $"Eine neue Version ist verfügbar: {_latestVersion}\nAktuelle Version: {UpdateManager.CurrentVersion}"
                : $"Aktuelle Version: {UpdateManager.CurrentVersion}",
            Foreground = _updateAvailable ? SolidColorBrush.Parse("#32A852") : Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 10)
        };

        var btnCheckUpdate = new Button
        {
            Content = "Nach Updates suchen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4),
            MinWidth = 180,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var btnUpdateApp = new Button
        {
            Content = "App aktualisieren",
            Background = SolidColorBrush.Parse("#007ACC"),
            Foreground = Brushes.White,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4),
            IsEnabled = _updateAvailable
        };

        var updateProgressBar = new ProgressBar
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

        var updatesActionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        updatesActionRow.Children.Add(btnCheckUpdate);
        updatesActionRow.Children.Add(btnUpdateApp);

        updatesSettingsPanel.Children.Add(chkAutoUpdate);
        updatesSettingsPanel.Children.Add(txtVersionInfo);
        updatesSettingsPanel.Children.Add(updateProgressBar);
        updatesSettingsPanel.Children.Add(updatesActionRow);

        btnCheckUpdate.Click += async (s, ev) =>
        {
            btnCheckUpdate.Content = "Suche...";
            btnCheckUpdate.IsEnabled = false;

            var result = await UpdateManager.CheckForUpdatesAsync();
            if (result.UpdateAvailable)
            {
                _updateAvailable = true;
                _latestVersion = result.LatestVersion;
                _updateDownloadUrl = result.DownloadUrl;
                BadgeSettings.IsVisible = true;

                txtVersionInfo.Text =
                    $"Eine neue Version ist verfügbar: {_latestVersion}\nAktuelle Version: {UpdateManager.CurrentVersion}";
                txtVersionInfo.Foreground = SolidColorBrush.Parse("#32A852");
                btnUpdateApp.IsEnabled = true;

                // add badge to category button dynamically if it doesnt have it
                if (btnCatUpdates.Content is string)
                {
                    var grid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*, Auto")
                    };
                    grid.Children.Add(new TextBlock
                    {
                        Text = "Updates",
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    var badge = new Border
                    {
                        Background = SolidColorBrush.Parse("#B43232"),
                        Width = 8,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    Grid.SetColumn(badge, 1);
                    grid.Children.Add(badge);
                    btnCatUpdates.Content = grid;
                }
            }
            else
            {
                txtVersionInfo.Text =
                    $"Du bist auf dem neusten Stand.\nAktuelle Version: {UpdateManager.CurrentVersion}";
                txtVersionInfo.Foreground = Brushes.Gray;
            }

            btnCheckUpdate.Content = "Nach Updates suchen";
            btnCheckUpdate.IsEnabled = true;
        };

        btnUpdateApp.Click += async (s, ev) =>
        {
            btnUpdateApp.Content = "Bereite Update vor...";
            btnUpdateApp.IsEnabled = false;
            btnCheckUpdate.IsEnabled = false;
            updateProgressBar.IsVisible = true;
            updateProgressBar.Value = 0;

            // progress reporter to get update state
            var progress = new Progress<(string message, double percentage)>(p =>
            {
                btnUpdateApp.Content = p.message;
                updateProgressBar.Value = p.percentage;
            });

            var updateResult = await UpdateManager.PerformUpdateAsync(_updateDownloadUrl, progress);

            if (updateResult != UpdateManager.UpdateStatus.Success)
            {
                // reset ui
                btnUpdateApp.Content = "App aktualisieren";
                btnUpdateApp.IsEnabled = true;
                btnCheckUpdate.IsEnabled = true;
                updateProgressBar.IsVisible = false;

                // show manual update dialog
                await ShowManualUpdateDialog(updateResult, _updateDownloadUrl, settingsWin);
            }
        };

        // misc
        var miscSettingsPanel = new StackPanel { Spacing = 15 };
        miscSettingsPanel.Children.Add(new TextBlock
        {
            Text = "Sonstiges",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var chkSqlAntiSpoiler = new CheckBox
        {
            Content = "SQL Anti-Spoiler Modus",
            IsChecked = AppSettings.IsSqlAntiSpoilerEnabled,
            Foreground = Brushes.White
        };
        ToolTip.SetTip(chkSqlAntiSpoiler, "Mögliche Lösungsansätze aus den Levelnamen verbergen");

        var chkDiscordRpc = new CheckBox
        {
            Content = "Discord Rich Presence",
            IsChecked = AppSettings.IsDiscordRpcEnabled,
            Foreground = Brushes.White
        };
        ToolTip.SetTip(chkDiscordRpc, "Zeige deinen Status auf Discord an");

        miscSettingsPanel.Children.Add(chkSqlAntiSpoiler);
        miscSettingsPanel.Children.Add(chkDiscordRpc);

        void CheckChanges()
        {
            bool hasChanges =
                (!_isSqlMode && chkVim.IsChecked != originalVimEnabled) ||
                (_isSqlMode && chkVim.IsChecked != originalSqlVimEnabled) ||
                (!_isSqlMode && chkSyntax.IsChecked != originalSyntaxEnabled) ||
                (_isSqlMode && chkSyntax.IsChecked != originalSqlSyntaxEnabled) ||
                (!_isSqlMode && chkAutocomplete.IsChecked != originalAutocompleteEnabled) ||
                (_isSqlMode && chkAutocomplete.IsChecked != originalSqlAutocompleteEnabled) ||
                chkError.IsChecked != originalErrorEnabled ||
                chkErrorExplain.IsChecked != originalErrorExplanation ||
                Math.Abs(sliderFontSize.Value - originalEditorFontSize) > 0.004 ||
                Math.Abs(sliderSqlFontSize.Value - originalSqlFontSize) > 0.004 ||
                chkPortable.IsChecked != isPortable ||
                chkAutoUpdate.IsChecked != originalAutoUpdateEnabled ||
                chkSqlAntiSpoiler.IsChecked != originalSqlAntiSpoilerEnabled ||
                chkDiscordRpc.IsChecked != originalDiscordRpcEnabled ||
                Math.Abs(sliderScale.Value - originalUiScale) > 0.004;

            btnSave.IsEnabled = hasChanges;
            btnSave.Opacity = hasChanges ? 1.0 : 0.5;
            btnSave.Background = hasChanges ? SolidColorBrush.Parse("#32A852") : SolidColorBrush.Parse("#464646");
        }

        // reset button click
        btnReset.Click += async (s, ev) =>
        {
            var confirmDialog = new Window
            {
                Title = "Reset",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            var cGrid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };
            cGrid.Children.Add(new TextBlock
            {
                Text = "Einstellungen wirklich auf Standard zurücksetzen?",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cBtnPanel = new StackPanel
                { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
            Grid.SetRow(cBtnPanel, 1);

            var btnYes = new Button
            {
                Content = "Ja, zurücksetzen",
                Background = SolidColorBrush.Parse("#B43232"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            var btnNo = new Button
            {
                Content = "Abbrechen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };

            btnYes.Click += (_, __) =>
            {
                // reset values based on mode
                if (_isSqlMode)
                {
                    chkSyntax.IsChecked = false;
                    chkVim.IsChecked = false;
                    chkAutocomplete.IsChecked = false;
                }
                else
                {
                    chkSyntax.IsChecked = false;
                    chkError.IsChecked = false;
                    chkErrorExplain.IsChecked = false;
                    chkVim.IsChecked = false;
                    chkAutocomplete.IsChecked = false;
                }

                sliderFontSize.Value = 16.0;
                sliderSqlFontSize.Value = 16.0;
                chkPortable.IsChecked = false;
                sliderScale.Value = 1.0;
                chkAutoUpdate.IsChecked = true;
                chkSqlAntiSpoiler.IsChecked = false;
                chkDiscordRpc.IsChecked = false;

                confirmDialog.Close();
            };
            btnNo.Click += (_, __) => confirmDialog.Close();

            cBtnPanel.Children.Add(btnNo);
            cBtnPanel.Children.Add(btnYes);
            cGrid.Children.Add(cBtnPanel);

            confirmDialog.Content = cGrid;
            await confirmDialog.ShowDialog(settingsWin);
        };

        // --- EVENT HANDLERS ---

        chkSyntax.IsCheckedChanged += (s, ev) =>
        {
            if (_isSqlMode)
            {
                AppSettings.IsSqlSyntaxHighlightingEnabled = chkSyntax.IsChecked ?? false;
                ApplySqlSyntaxHighlighting();
            }
            else
            {
                AppSettings.IsSyntaxHighlightingEnabled = chkSyntax.IsChecked ?? false;
                ApplySyntaxHighlighting();
            }

            CheckChanges();
        };

        chkError.IsCheckedChanged += async (s, ev) =>
        {
            if (chkError.IsChecked == true && !originalErrorEnabled)
                await ShowWarningDialog(
                    "Error-Hervorhebung",
                    "In der Prüfung müssen Fehler selbstständig gefunden werden. Es wird empfohlen ohne dieses Feature zu üben!\n\nAchtung: Diese Funktion setzt sich nach jedem Level-Wechsel zurück."
                );

            AppSettings.IsErrorHighlightingEnabled = chkError.IsChecked ?? false;

            if (!AppSettings.IsErrorHighlightingEnabled)
            {
                chkErrorExplain.IsChecked = false;
                chkErrorExplain.IsEnabled = false;
            }
            else
            {
                chkErrorExplain.IsEnabled = true;
            }

            if (AppSettings.IsErrorHighlightingEnabled)
                UpdateDiagnostics();
            else
                ClearDiagnostics();

            CheckChanges();
        };

        chkErrorExplain.IsCheckedChanged += async (s, ev) =>
        {
            if (chkErrorExplain.IsChecked == true && !originalErrorExplanation)
                await ShowWarningDialog(
                    "Error-Erklärungen",
                    "Detaillierte Fehlerbeschreibungen stehen in der Prüfung nicht zur Verfügung. Nutze dies nur, wenn du absolut nicht weiterkommst."
                );
            AppSettings.IsErrorExplanationEnabled = chkErrorExplain.IsChecked ?? false;
            CheckChanges();
        };

        chkAutocomplete.IsCheckedChanged += (s, ev) =>
        {
            if (_isSqlMode)
            {
                AppSettings.IsSqlAutocompleteEnabled = chkAutocomplete.IsChecked ?? false;
                if (AppSettings.IsSqlAutocompleteEnabled)
                    _sqlAutocompleteService?.ScanTokens(SqlQueryEditor.Text);
                else
                    _sqlAutocompleteService?.ClearSuggestion();
            }
            else
            {
                AppSettings.IsAutocompleteEnabled = chkAutocomplete.IsChecked ?? false;
                if (AppSettings.IsAutocompleteEnabled)
                    _csharpAutocompleteService?.ScanTokens(CodeEditor.Text);
                else
                    _csharpAutocompleteService?.ClearSuggestion();
            }

            CheckChanges();
        };

        chkVim.IsCheckedChanged += (s, ev) =>
        {
            if (_isSqlMode) AppSettings.IsSqlVimEnabled = chkVim.IsChecked ?? false;
            else AppSettings.IsVimEnabled = chkVim.IsChecked ?? false;
            UpdateVimState();
            CheckChanges();
        };
        chkPortable.IsCheckedChanged += (s, ev) => { CheckChanges(); };

        sliderScale.ValueChanged += (s, ev) =>
        {
            AppSettings.UiScale = ev.NewValue;
            txtScaleVal.Text = $"{ev.NewValue:P0}";
            ApplyUiScale();
            CheckChanges();
        };

        sliderFontSize.ValueChanged += (s, ev) =>
        {
            AppSettings.EditorFontSize = ev.NewValue;
            txtFontSizeVal.Text = $"{ev.NewValue:F0}px";
            CodeEditor.FontSize = ev.NewValue;
            TutorialEditor.FontSize = ev.NewValue;
            CheckChanges();
        };

        sliderSqlFontSize.ValueChanged += (s, ev) =>
        {
            AppSettings.SqlEditorFontSize = ev.NewValue;
            txtSqlFontSizeVal.Text = $"{ev.NewValue:F0}px";
            SqlQueryEditor.FontSize = ev.NewValue;
            CheckChanges();
        };

        chkAutoUpdate.IsCheckedChanged += (s, ev) =>
        {
            AppSettings.AutoCheckForUpdates = chkAutoUpdate.IsChecked ?? false;
            CheckChanges();
        };

        chkDiscordRpc.IsCheckedChanged += (s, ev) =>
        {
            AppSettings.IsDiscordRpcEnabled = chkDiscordRpc.IsChecked ?? false;
            CheckChanges();
        };

        chkSqlAntiSpoiler.IsCheckedChanged += (s, ev) =>
        {
            AppSettings.IsSqlAntiSpoilerEnabled = chkSqlAntiSpoiler.IsChecked ?? false;
            CheckChanges();
        };

        // --- LAYOUT ASSEMBLY ---

        // editor
        var editorSettings = new StackPanel { Spacing = 15 };
        string editorTitle = _isSqlMode ? "SQL Query Editor" : "C# Code Editor";
        editorSettings.Children.Add(new TextBlock
        {
            Text = editorTitle,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        editorSettings.Children.Add(chkSyntax);
        editorSettings.Children.Add(chkAutocomplete);
        editorSettings.Children.Add(chkError);
        editorSettings.Children.Add(chkErrorExplain);
        editorSettings.Children.Add(chkVim);

        // display
        var displaySettings = new StackPanel { Spacing = 15 };
        var scalePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        var fontPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        fontPanel.Children.Add(sliderFontSize);
        fontPanel.Children.Add(txtFontSizeVal);
        var sqlFontPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };
        sqlFontPanel.Children.Add(sliderSqlFontSize);
        sqlFontPanel.Children.Add(txtSqlFontSizeVal);

        scalePanel.Children.Add(sliderScale);
        scalePanel.Children.Add(txtScaleVal);
        displaySettings.Children.Add(new TextBlock
        {
            Text = "Darstellung",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        displaySettings.Children.Add(new TextBlock
        {
            Text = "UI Skalierung",
            Foreground = Brushes.LightGray
        });
        displaySettings.Children.Add(scalePanel);
        displaySettings.Children.Add(new TextBlock
        {
            Text = "C# Editor Schriftgröße",
            Foreground = Brushes.LightGray
        });
        displaySettings.Children.Add(fontPanel);
        displaySettings.Children.Add(new TextBlock
        {
            Text = "SQL Editor Schriftgröße",
            Foreground = Brushes.LightGray
        });
        displaySettings.Children.Add(sqlFontPanel);

        // data
        var dataSettingsPanel = new StackPanel { Spacing = 15 };
        dataSettingsPanel.Children.Add(new TextBlock
        {
            Text = "Daten & Speicher",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
        dataSettingsPanel.Children.Add(chkPortable);
        dataSettingsPanel.Children.Add(txtPortableInfo);

        void ShowCategory(Button activeBtn, Control content)
        {
            btnCatEditor.Background = Brushes.Transparent;
            btnCatDisplay.Background = Brushes.Transparent;
            btnCatData.Background = Brushes.Transparent;
            btnCatUpdates.Background = Brushes.Transparent;
            btnCatMisc.Background = Brushes.Transparent;

            activeBtn.Background = SolidColorBrush.Parse("#3E3E42");
            rightPanel.Child = content;
        }

        btnCatEditor.Click += (s, ev) => ShowCategory(btnCatEditor, editorSettings);
        btnCatDisplay.Click += (s, ev) => ShowCategory(btnCatDisplay, displaySettings);
        btnCatData.Click += (s, ev) => ShowCategory(btnCatData, dataSettingsPanel);
        btnCatUpdates.Click += (s, ev) => ShowCategory(btnCatUpdates, updatesSettingsPanel);
        btnCatMisc.Click += (s, ev) => ShowCategory(btnCatMisc, miscSettingsPanel);

        ShowCategory(btnCatEditor, editorSettings);

        // --- WINDOW CONTROLS ---

        var closeBtn = new Button
        {
            Content = "Schließen",
            Width = 100,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(20, 10),
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White
        };

        Grid.SetRow(closeBtn, 1);
        Grid.SetColumn(closeBtn, 1);
        mainGrid.Children.Add(closeBtn);

        // save logic
        Action performSave = () =>
        {
            playerData.Settings.IsVimEnabled = AppSettings.IsVimEnabled;
            playerData.Settings.IsSqlVimEnabled = AppSettings.IsSqlVimEnabled;
            playerData.Settings.IsSyntaxHighlightingEnabled = AppSettings.IsSyntaxHighlightingEnabled;
            playerData.Settings.IsSqlSyntaxHighlightingEnabled = AppSettings.IsSqlSyntaxHighlightingEnabled;
            playerData.Settings.IsAutocompleteEnabled = AppSettings.IsAutocompleteEnabled;
            playerData.Settings.IsSqlAutocompleteEnabled = AppSettings.IsSqlAutocompleteEnabled;
            playerData.Settings.EditorFontSize = AppSettings.EditorFontSize;
            playerData.Settings.SqlEditorFontSize = AppSettings.SqlEditorFontSize;
            playerData.Settings.UiScale = AppSettings.UiScale;
            playerData.Settings.AutoCheckForUpdates = AppSettings.AutoCheckForUpdates;
            playerData.Settings.IsSqlAntiSpoilerEnabled = AppSettings.IsSqlAntiSpoilerEnabled;
            playerData.Settings.IsDiscordRpcEnabled = AppSettings.IsDiscordRpcEnabled;

            if (AppSettings.IsDiscordRpcEnabled)
            {
                DiscordRpcManager.Initialize();
                if (_isDesignerMode)
                    DiscordRpcManager.UpdatePresence("C# Level Designer", "Creating their own level", "aec_app_icon",
                        "Custom");
                else if (_isSqlMode)
                    DiscordRpcManager.UpdatePresence($"SQL Level {currentSqlLevel?.Id}", "Querying greatness",
                        "mysql_icon", "MySQL");
                else
                    DiscordRpcManager.UpdatePresence($"C# Level {currentLevel?.Id}", "Coding greatness", "chsarp_icon",
                        "C#");
            }
            else
            {
                DiscordRpcManager.Deinitialize();
            }

            SaveSystem.Save(playerData);

            if (chkPortable.IsChecked != originalPortableState)
                try
                {
                    SaveSystem.SetPortableMode(chkPortable.IsChecked == true);
                    isPortable = chkPortable.IsChecked == true;
                    originalPortableState = isPortable;

                    string location = isPortable ? "Programmordner" : "AppData";
                    AddToConsole($"\n> Speicherort geändert auf: {location}", Brushes.LightGray);
                }
                catch (Exception ex)
                {
                    AddToConsole($"\n> Fehler beim Ändern des Speicherorts: {ex.Message}", Brushes.Red);
                }

            // update original state references (allow subsequent saves)
            originalVimEnabled = AppSettings.IsVimEnabled;
            originalSqlVimEnabled = AppSettings.IsSqlVimEnabled;
            originalSyntaxEnabled = AppSettings.IsSyntaxHighlightingEnabled;
            originalSqlSyntaxEnabled = AppSettings.IsSqlSyntaxHighlightingEnabled;
            originalAutocompleteEnabled = AppSettings.IsAutocompleteEnabled;
            originalSqlAutocompleteEnabled = AppSettings.IsSqlAutocompleteEnabled;
            originalErrorEnabled = AppSettings.IsErrorHighlightingEnabled;
            originalErrorExplanation = AppSettings.IsErrorExplanationEnabled;
            originalEditorFontSize = AppSettings.EditorFontSize;
            originalSqlFontSize = AppSettings.SqlEditorFontSize;
            originalUiScale = AppSettings.UiScale;
            originalAutoUpdateEnabled = AppSettings.AutoCheckForUpdates;
            originalSqlAntiSpoilerEnabled = AppSettings.IsSqlAntiSpoilerEnabled;
            originalDiscordRpcEnabled = AppSettings.IsDiscordRpcEnabled;

            btnSave.IsEnabled = false;
            btnSave.Opacity = 0.5;
        };

        // btnSave click uses the Action
        btnSave.Click += (s, ev) => performSave();

        closeBtn.Click += async (s, ev) =>
        {
            if (btnSave.IsEnabled)
            {
                var unsavedDialog = new Window
                {
                    Title = "Ungespeicherte Änderungen",
                    Width = 350,
                    Height = 160,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SystemDecorations = SystemDecorations.BorderOnly,
                    Background = SolidColorBrush.Parse("#252526"),
                    CornerRadius = new CornerRadius(8)
                };

                var dGrid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };
                dGrid.Children.Add(new TextBlock
                {
                    Text = "Du hast ungespeicherte Änderungen. Möchtest du diese speichern?",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var dBtnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10,
                    Margin = new Thickness(0, 15, 0, 0)
                };
                Grid.SetRow(dBtnPanel, 1);

                var btnSaveClose = new Button
                {
                    Content = "Speichern", Background = SolidColorBrush.Parse("#32A852"), Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4)
                };
                var btnDiscard = new Button
                {
                    Content = "Verwerfen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4)
                };
                var btnCancel = new Button
                {
                    Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4)
                };

                btnSaveClose.Click += (_, __) =>
                {
                    performSave();
                    unsavedDialog.Close();
                    settingsWin.Close();
                };

                btnDiscard.Click += (_, __) =>
                {
                    unsavedDialog.Close();
                    settingsWin.Close();
                };

                btnCancel.Click += (_, __) => unsavedDialog.Close();

                dBtnPanel.Children.Add(btnCancel);
                dBtnPanel.Children.Add(btnDiscard);
                dBtnPanel.Children.Add(btnSaveClose);

                dGrid.Children.Add(dBtnPanel);
                unsavedDialog.Content = dGrid;

                await unsavedDialog.ShowDialog(settingsWin);
            }
            else
            {
                settingsWin.Close();
            }
        };

        // close (revert if not saved)
        settingsWin.Closing += (s, ev) =>
        {
            if (btnSave.IsEnabled)
            {
                AppSettings.IsVimEnabled = originalVimEnabled;
                AppSettings.IsSqlVimEnabled = originalSqlVimEnabled;
                AppSettings.IsSyntaxHighlightingEnabled = originalSyntaxEnabled;
                AppSettings.IsSqlSyntaxHighlightingEnabled = originalSqlSyntaxEnabled;
                AppSettings.IsAutocompleteEnabled = originalAutocompleteEnabled;
                AppSettings.IsSqlAutocompleteEnabled = originalSqlAutocompleteEnabled;
                AppSettings.IsErrorHighlightingEnabled = originalErrorEnabled;
                AppSettings.IsErrorExplanationEnabled = originalErrorExplanation;
                AppSettings.EditorFontSize = originalEditorFontSize;
                AppSettings.SqlEditorFontSize = originalSqlFontSize;
                CodeEditor.FontSize = originalEditorFontSize;
                SqlQueryEditor.FontSize = originalSqlFontSize;
                AppSettings.UiScale = originalUiScale;
                AppSettings.AutoCheckForUpdates = originalAutoUpdateEnabled;
                AppSettings.IsSqlAntiSpoilerEnabled = originalSqlAntiSpoilerEnabled;
                AppSettings.IsDiscordRpcEnabled = originalDiscordRpcEnabled;

                UpdateVimState();
                ApplySyntaxHighlighting();
                ApplyUiScale();
                ClearDiagnostics();
            }
        };

        rootBorder.Child = mainGrid;
        settingsWin.Content = rootBorder;
        settingsWin.ShowDialog(this);
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

        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };

        var contentPanel = new StackPanel
        {
            Spacing = 15,
            VerticalAlignment = VerticalAlignment.Center
        };
        contentPanel.Children.Add(
            new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextHighlight,
                FontSize = 16
            }
        );
        contentPanel.Children.Add(
            new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White
            }
        );

        mainGrid.Children.Add(contentPanel);

        var btn = new Button
        {
            Content = "Verstanden",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = BrushTextTitle,
            Foreground = Brushes.White,
            Padding = new Thickness(20, 8),
            CornerRadius = new CornerRadius(4)
        };
        btn.Click += (_, __) => dialog.Close();

        Grid.SetRow(btn, 1);
        mainGrid.Children.Add(btn);

        dialog.Content = mainGrid;
        await dialog.ShowDialog(this);
    }
}