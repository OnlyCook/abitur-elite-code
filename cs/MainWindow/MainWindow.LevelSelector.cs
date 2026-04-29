using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AbiturEliteCode.cs;
using AbiturEliteCode.cs.MainWindow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void BtnLevelSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_isSqlMode && sqlLevels == null) sqlLevels = SqlCurriculum.GetLevels();
        levels ??= Curriculum.GetLevels();

        var win = new Window
        {
            Title = "Level Wählen",
            Width = 450,
            Height = 700,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = BrushBgPanel,
            SystemDecorations = SystemDecorations.BorderOnly
        };
        win.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) win.Close(); };

        var root = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = BrushBgPanel,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1)
        };

        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto"),
            Margin = new Thickness(15)
        };

        // header
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
            Margin = new Thickness(0, 0, 0, 15)
        };

        // title
        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        var txtTitle = new TextBlock
        {
            Text = _isSqlMode ? "SQL Levels" : "C# Levels",
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleStack.Children.Add(txtTitle);

        // badge level counter
        var countBadge = new Border
        {
            Background = SolidColorBrush.Parse("#2D2D30"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 5),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = true,
            Child = new TextBlock
            {
                Name = "BadgeText",
                Text = "",
                Foreground = BrushTextTitle,
                FontWeight = FontWeight.Bold,
                FontSize = 14
            }
        };
        titleStack.Children.Add(countBadge);

        headerGrid.Children.Add(titleStack);

        // search / code input container
        var searchContainer = new Border
        {
            Margin = new Thickness(15, 0)
        };
        Grid.SetColumn(searchContainer, 1);
        headerGrid.Children.Add(searchContainer);

        // right header panel
        var headerRightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Grid.SetColumn(headerRightPanel, 2);
        headerGrid.Children.Add(headerRightPanel);

        // body and footer
        var contentScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var footerPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };

        mainGrid.Children.Add(headerGrid);
        Grid.SetRow(contentScroll, 1);
        mainGrid.Children.Add(contentScroll);
        Grid.SetRow(footerPanel, 2);
        mainGrid.Children.Add(footerPanel);

        root.Child = mainGrid;
        win.Content = root;

        bool isCustomMode = false;

        // ui refresh logic
        void RefreshUI()
        {
            searchContainer.Child = null;
            headerRightPanel.Children.Clear();
            contentScroll.Content = null;
            footerPanel.Children.Clear();

            // footer buttons
            var btnClose = new Button
            {
                Content = "Schließen",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(15, 10),
                CornerRadius = new CornerRadius(4),
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                Margin = new Thickness(10, 15, 0, 0)
            };
            btnClose.Click += (_, __) => win.Close();

            // toggle custom levels button
            var btnToggleMode = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        LoadIcon(isCustomMode ? "assets/icons/ic_folder.svg" : "assets/icons/ic_folder_custom.svg", 18),
                        new TextBlock
                        {
                            Text = isCustomMode ? "Standard Levels" : "Eigene Levels",
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Padding = new Thickness(15, 10),
                CornerRadius = new CornerRadius(4),
                Background = SolidColorBrush.Parse("#2D2D30"),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 15, 10, 0)
            };

            btnToggleMode.Click += (_, __) =>
            {
                isCustomMode = !isCustomMode;
                RefreshUI();
            };

            Grid.SetColumn(btnToggleMode, 0);
            Grid.SetColumn(btnClose, 1);
            footerPanel.Children.Add(btnToggleMode);
            footerPanel.Children.Add(btnClose);

            if (!isCustomMode)
            {
                // title and badge
                if (_isSqlMode)
                {
                    txtTitle.Text = "SQL Levels";
                    int completedCount = sqlLevels.Count(l => playerData.CompletedSqlLevelIds.Contains(l.Id));
                    ((TextBlock)countBadge.Child).Text = $"{completedCount}/{sqlLevels.Count}";
                }
                else
                {
                    txtTitle.Text = "C# Levels";
                    int completedCount = levels.Count(l => playerData.CompletedLevelIds.Contains(l.Id));
                    ((TextBlock)countBadge.Child).Text = $"{completedCount}/{levels.Count}";
                }

                countBadge.IsVisible = true;

                // code input field
                var codePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };
                codePanel.Children.Add(new TextBlock
                {
                    Text = "Code:",
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var txtLevelCode = new TextBox
                {
                    Width = 60,
                    MaxLength = 3,
                    Background = SolidColorBrush.Parse("#141414"),
                    Foreground = Brushes.White,
                    BorderBrush = SolidColorBrush.Parse("#333"),
                    CornerRadius = new CornerRadius(4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    FontFamily = MonospaceFontFamily
                };

                // code input logic
                txtLevelCode.TextChanged += (s, ev) =>
                {
                    if (txtLevelCode.Text?.Length == 3)
                    {
                        string code = txtLevelCode.Text.ToUpper();
                        if (_isSqlMode)
                        {
                            var lvl = sqlLevels.FirstOrDefault(l => l.SkipCode == code);
                            if (lvl != null)
                            {
                                // actually unlock the level (permanently)
                                if (!playerData.UnlockedSqlLevelIds.Contains(lvl.Id))
                                {
                                    playerData.UnlockedSqlLevelIds.Add(lvl.Id);
                                    SaveSystem.Save(playerData);
                                }

                                LoadSqlLevel(lvl);
                                win.Close();
                            }
                        }
                        else
                        {
                            var lvl = levels.FirstOrDefault(l => l.SkipCode == code);
                            if (lvl != null)
                            {
                                // actually unlock the level (permanently)
                                if (!playerData.UnlockedLevelIds.Contains(lvl.Id))
                                {
                                    playerData.UnlockedLevelIds.Add(lvl.Id);
                                    SaveSystem.Save(playerData);
                                }

                                LoadLevel(lvl);
                                win.Close();
                            }
                        }
                    }
                };
                codePanel.Children.Add(txtLevelCode);

                var btnLevelGuide = new Button
                {
                    Content = LoadIcon("assets/icons/ic_guide.svg", 16),
                    Background = SolidColorBrush.Parse("#007ACC"),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTip.SetTip(btnLevelGuide, "Level Codes & Lösungen");
                btnLevelGuide.Click += (_, __) =>
                {
                    try
                    {
                        var url =
                            $"https://github.com/OnlyCook/abitur-elite-code/blob/main/py/LEVEL_CODES.md{(_isSqlMode ? "#sql-levels" : "")}";
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
                    }
                    catch
                    {
                    }
                };
                codePanel.Children.Add(btnLevelGuide);

                headerRightPanel.Children.Add(codePanel);


                // level list
                var levelStack = new StackPanel { Spacing = 8 };

                if (_isSqlMode)
                {
                    // sql levels
                    var groups = sqlLevels.GroupBy(l => l.Section);
                    foreach (var group in groups)
                    {
                        bool isSectionComplete = group.All(l => playerData.CompletedSqlLevelIds.Contains(l.Id));

                        var headerPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10
                        };
                        headerPanel.Children.Add(new TextBlock
                        {
                            Text = group.Key,
                            Foreground = BrushTextTitle,
                            FontWeight = FontWeight.Bold,
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        if (isSectionComplete) headerPanel.Children.Add(LoadIcon("assets/icons/ic_done.svg", 16));

                        var sectionContent = new StackPanel
                        {
                            Spacing = 5,
                            Margin = new Thickness(0, 5, 0, 0)
                        };

                        foreach (var lvl in group)
                        {
                            bool unlocked = playerData.UnlockedSqlLevelIds.Contains(lvl.Id);
                            bool completed = playerData.CompletedSqlLevelIds.Contains(lvl.Id);

                            var btnContent = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 10
                            };
                            string iconPath = completed ? "assets/icons/ic_check.svg" :
                                unlocked ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_lock.svg";
                            btnContent.Children.Add(LoadIcon(iconPath, 16));
                            btnContent.Children.Add(new TextBlock
                            {
                                Text = $"S{lvl.Id}. {lvl.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)}",
                                VerticalAlignment = VerticalAlignment.Center
                            });

                            var btn = new Button
                            {
                                Content = btnContent,
                                IsEnabled = unlocked,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                Padding = new Thickness(10, 10),
                                Background = unlocked
                                    ? SolidColorBrush.Parse("#313133")
                                    : SolidColorBrush.Parse("#191919"),
                                Foreground = unlocked ? Brushes.White : Brushes.Gray,
                                CornerRadius = new CornerRadius(4)
                            };
                            btn.Click += (_, __) =>
                            {
                                LoadSqlLevel(lvl);
                                win.Close();
                            };
                            sectionContent.Children.Add(btn);
                        }

                        levelStack.Children.Add(new Expander
                        {
                            Header = headerPanel,
                            Content = sectionContent,
                            IsExpanded = !isSectionComplete,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 0, 5)
                        });
                    }
                }
                else
                {
                    // c# levels
                    var groups = levels.GroupBy(l => l.Section);
                    foreach (var group in groups)
                    {
                        bool isSectionComplete = group.All(l => playerData.CompletedLevelIds.Contains(l.Id));

                        var headerPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10
                        };
                        headerPanel.Children.Add(new TextBlock
                        {
                            Text = group.Key,
                            Foreground = BrushTextTitle,
                            FontWeight = FontWeight.Bold,
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        if (isSectionComplete) headerPanel.Children.Add(LoadIcon("assets/icons/ic_done.svg", 16));

                        var sectionContent = new StackPanel
                        {
                            Spacing = 5,
                            Margin = new Thickness(0, 5, 0, 0)
                        };

                        foreach (var lvl in group)
                        {
                            bool unlocked = playerData.UnlockedLevelIds.Contains(lvl.Id);
                            bool completed = playerData.CompletedLevelIds.Contains(lvl.Id);

                            var btnContent = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 10
                            };
                            string iconPath = completed ? "assets/icons/ic_check.svg" :
                                unlocked ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_lock.svg";
                            btnContent.Children.Add(LoadIcon(iconPath, 16));
                            btnContent.Children.Add(new TextBlock
                            {
                                Text = $"{lvl.Id}. {lvl.Title}",
                                VerticalAlignment = VerticalAlignment.Center
                            });

                            var btn = new Button
                            {
                                Content = btnContent,
                                IsEnabled = unlocked,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                Padding = new Thickness(10, 10),
                                Background = unlocked
                                    ? SolidColorBrush.Parse("#313133")
                                    : SolidColorBrush.Parse("#191919"),
                                Foreground = unlocked ? Brushes.White : Brushes.Gray,
                                CornerRadius = new CornerRadius(4)
                            };
                            btn.Click += (_, __) =>
                            {
                                LoadLevel(lvl);
                                win.Close();
                            };
                            sectionContent.Children.Add(btn);
                        }

                        levelStack.Children.Add(new Expander
                        {
                            Header = headerPanel,
                            Content = sectionContent,
                            IsExpanded = !isSectionComplete,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 0, 5)
                        });
                    }
                }

                contentScroll.Content = levelStack;
            }
            else
            {
                // custom levels (for now only c#)
                txtTitle.Text = "Eigene Levels";
                countBadge.IsVisible = false;

                var customStack = new StackPanel { Spacing = 5 };
                var customLevels = GetCustomLevels();
                var rootLevels = customLevels.Where(x => x.Section == "Einzelne Levels").OrderBy(x => x.Name).ToList();
                var folderGroups = customLevels.Where(x => x.Section != "Einzelne Levels").GroupBy(x => x.Section)
                    .OrderBy(g => g.Key).ToList();

                // search Box
                var txtSearch = new TextBox
                {
                    Watermark = "Suchen...",
                    MinWidth = 150,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = SolidColorBrush.Parse("#141414"),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = SolidColorBrush.Parse("#333"),
                    CornerRadius = new CornerRadius(4)
                };

                // search logic
                txtSearch.TextChanged += (s, e) =>
                {
                    string query = txtSearch.Text?.ToLower() ?? "";
                    foreach (var child in customStack.Children)
                        if (child is Expander exp && exp.Content is StackPanel groupPanel)
                        {
                            bool groupHasMatch = false;
                            foreach (var item in groupPanel.Children)
                                if (item is Grid row && row.Tag is CustomLevelInfo info)
                                {
                                    bool match = info.Name.ToLower().Contains(query) ||
                                                 info.Author.ToLower().Contains(query);
                                    row.IsVisible = match;
                                    if (match) groupHasMatch = true;
                                }

                            exp.IsVisible = groupHasMatch;
                            if (!string.IsNullOrEmpty(query)) exp.IsExpanded = true;
                            else exp.IsExpanded = false;
                        }
                        else if (child is Grid row && row.Tag is CustomLevelInfo info)
                        {
                            bool match = info.Name.ToLower().Contains(query) || info.Author.ToLower().Contains(query);
                            row.IsVisible = match;
                        }
                };
                searchContainer.Child = txtSearch;

                // custom level header buttons
                var btnOpenFolder = new Button
                {
                    Content = LoadIcon("assets/icons/ic_folder_open.svg", 18),
                    Background = SolidColorBrush.Parse("#3C3C3C"),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTip.SetTip(btnOpenFolder, "Levels Ordner öffnen");
                btnOpenFolder.Click += (_, __) => OpenLevelsFolder();
                headerRightPanel.Children.Add(btnOpenFolder);

                var btnAdd = new Button
                {
                    Content = LoadIcon("assets/icons/ic_add.svg", 18),
                    Background = SolidColorBrush.Parse("#32A852"),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTip.SetTip(btnAdd, "Neues Level erstellen");
                btnAdd.Click += async (_, __) =>
                {
                    string newPath = await ShowAddLevelDialog(win);
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        _newlyCreatedLevelPath = newPath;
                        RefreshUI();
                    }
                };
                headerRightPanel.Children.Add(btnAdd);

                if (!customLevels.Any())
                {
                    customStack.Children.Add(new TextBlock
                    {
                        Text =
                            "Keine eigenen Levels gefunden.\nErstelle eins mit '+' oder \nöffne den Ordner und füge Levels hinzu.",
                        Foreground = Brushes.Gray,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 50, 0, 0)
                    });
                }
                else
                {
                    Grid CreateLevelRow(CustomLevelInfo cl)
                    {
                        var rowGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                            Tag = cl,
                            Margin = new Thickness(0, 0, 0, 5),
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };

                        string iconPath;
                        if (cl.IsDraft) iconPath = "assets/icons/ic_lock.svg";
                        else if (_isSqlMode
                                     ? customPlayerData.CompletedCustomSqlLevels.Contains(cl.Name)
                                     : customPlayerData.CompletedCustomLevels.Contains(cl.Name))
                            iconPath = "assets/icons/ic_check.svg";
                        else iconPath = "assets/icons/ic_lock_open.svg";

                        var iconImage = LoadIcon(iconPath, 16);
                        iconImage.Margin = new Thickness(0, 0, 10, 0);
                        iconImage.VerticalAlignment = VerticalAlignment.Center;

                        var btnContentGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto, *")
                        };
                        btnContentGrid.Children.Add(iconImage);

                        var textStack = new StackPanel { Spacing = 2 };
                        Grid.SetColumn(textStack, 1);

                        string displayName = cl.Name;
                        if (_isSqlMode && AppSettings.IsSqlAntiSpoilerEnabled && cl.Section != null &&
                            !cl.Section.StartsWith("Sektion 7"))
                            displayName = Regex.Replace(cl.Name, @"\s*\(.*?\)", "").Trim();

                        textStack.Children.Add(new TextBlock
                        {
                            Text = displayName + (cl.IsDraft ? " (Entwurf)" : ""),
                            Foreground = cl.IsDraft ? Brushes.Orange : Brushes.White,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        textStack.Children.Add(new TextBlock
                        {
                            Text = "von " + cl.Author,
                            FontSize = 11,
                            Foreground = Brushes.Gray,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        btnContentGrid.Children.Add(textStack);

                        var btnMain = new Button
                        {
                            Content = btnContentGrid,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Background = cl.FilePath == _newlyCreatedLevelPath
                                ? SolidColorBrush.Parse("#2E8B57")
                                : SolidColorBrush.Parse("#313133"),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(10),
                            Cursor = cl.IsDraft ? Cursor.Default : Cursor.Parse("Hand")
                        };

                        // remove highlight after delay
                        if (cl.FilePath == _newlyCreatedLevelPath)
                        {
                            var timer = new DispatcherTimer
                            {
                                Interval = TimeSpan.FromSeconds(2)
                            };
                            timer.Tick += (s, args) =>
                            {
                                btnMain.Background = SolidColorBrush.Parse("#313133");
                                _newlyCreatedLevelPath = null;
                                timer.Stop();
                            };
                            timer.Start();
                        }

                        btnMain.Click += (_, __) =>
                        {
                            if (!cl.IsDraft)
                            {
                                LoadCustomLevelFromFile(cl.FilePath);
                                win.Close();
                            }
                        };

                        Grid.SetColumnSpan(btnMain, 3);
                        rowGrid.Children.Add(btnMain);

                        // action buttons
                        var actionPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            Margin = new Thickness(0, 0, 10, 0),
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Background = Brushes.Transparent
                        };
                        Grid.SetColumn(actionPanel, 2);

                        if (cl.IsDraft && cl.QuickGenerate)
                        {
                            var btnQuickExport = new Button
                            {
                                Content = LoadIcon("assets/icons/ic_generate.svg", 16),
                                Background = Brushes.Transparent,
                                Padding = new Thickness(8),
                                Tag = "idle"
                            };
                            ToolTip.SetTip(btnQuickExport, "Quick Export (Automatisch)");

                            btnQuickExport.Click += async (s, e) =>
                            {
                                if (btnQuickExport.Tag.ToString() != "idle") return;
                                btnQuickExport.Tag = "pending";
                                btnQuickExport.Content = LoadIcon("assets/icons/ic_pending.svg", 16);
                                btnQuickExport.IsEnabled = false;

                                var cts = new CancellationTokenSource();
                                EventHandler<WindowClosingEventArgs> closingHandler = (sender, args) => cts.Cancel();
                                win.Closing += closingHandler;

                                try
                                {
                                    if (_isSqlMode)
                                    {
                                        AddSqlOutput("System", $"> Quick Export gestartet für: {cl.Name}...",
                                            Brushes.LightGray);
                                        var draft = SqlLevelDesigner.LoadDraft(cl.FilePath);

                                        var validData =
                                            await Task
                                                .Run<(bool Success, List<SqlExpectedColumn> Schema, List<string[]>
                                                    Result)>(() =>
                                                {
                                                    try
                                                    {
                                                        using (var connection =
                                                               new SqliteConnection("Data Source=:memory:"))
                                                        {
                                                            connection.Open();

                                                            // run setup code
                                                            using (var setupCmd = connection.CreateCommand())
                                                            {
                                                                setupCmd.CommandText = draft.SetupScript;
                                                                setupCmd.ExecuteNonQuery();
                                                            }

                                                            // exclude empty input buffers
                                                            var cleanedSchema = draft.ExpectedSchema
                                                                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                                                                .ToList();
                                                            int validCols = cleanedSchema.Count;

                                                            var cleanedResult = new List<string[]>();
                                                            foreach (var r in draft.ExpectedResult)
                                                            {
                                                                var rowData = r.Take(validCols).Select(c => c ?? "")
                                                                    .ToArray();
                                                                if (rowData.Any(c => !string.IsNullOrWhiteSpace(c)))
                                                                    cleanedResult.Add(rowData);
                                                            }

                                                            if (validCols == 0)
                                                                throw new Exception(
                                                                    "Die Erwartungstabelle (Expected Table) darf nicht komplett leer sein.");

                                                            DataTable actualDt = null;
                                                            string sampleSolution =
                                                                SqlLevelTester.ConvertMysqlToSqlite(connection,
                                                                    draft.SampleSolution);

                                                            if (draft.IsDmlMode)
                                                            {
                                                                using (var dmlCmd = connection.CreateCommand())
                                                                {
                                                                    dmlCmd.CommandText = sampleSolution;
                                                                    dmlCmd.ExecuteNonQuery();
                                                                }

                                                                if (string.IsNullOrWhiteSpace(draft.VerificationQuery))
                                                                    throw new Exception(
                                                                        "Im DML Modus muss eine Verifizierungs-Abfrage angegeben werden.");

                                                                string verifyQuery =
                                                                    SqlLevelTester.ConvertMysqlToSqlite(connection,
                                                                        draft.VerificationQuery);
                                                                actualDt = ExecuteDbQuery(connection, verifyQuery);
                                                            }
                                                            else
                                                            {
                                                                actualDt = ExecuteDbQuery(connection, sampleSolution);
                                                            }

                                                            if (actualDt.Columns.Count != validCols)
                                                                throw new Exception(
                                                                    $"Spaltenanzahl stimmt nicht überein. Erwartet: {validCols}, Ist: {actualDt.Columns.Count}");

                                                            for (int i = 0; i < validCols; i++)
                                                                if (!actualDt.Columns[i].ColumnName
                                                                        .Equals(cleanedSchema[i].Name,
                                                                            StringComparison.OrdinalIgnoreCase))
                                                                    throw new Exception(
                                                                        $"Spaltenname an Position {i + 1} stimmt nicht. Erwartet: '{cleanedSchema[i].Name}', Ist: '{actualDt.Columns[i].ColumnName}'");

                                                            if (actualDt.Rows.Count != cleanedResult.Count)
                                                                throw new Exception(
                                                                    $"Zeilenanzahl stimmt nicht überein. Erwartet: {cleanedResult.Count}, Ist: {actualDt.Rows.Count}");

                                                            for (int r = 0; r < cleanedResult.Count; r++)
                                                            for (int c = 0; c < validCols; c++)
                                                            {
                                                                string expectedVal = cleanedResult[r][c] ?? "";
                                                                if (expectedVal == "") expectedVal = "NULL";

                                                                string actualVal = actualDt.Rows[r][c]?.ToString()
                                                                    ?.Replace(",", ".") ?? "";
                                                                if (actualDt.Rows[r][c] == DBNull.Value ||
                                                                    string.IsNullOrEmpty(actualVal)) actualVal = "NULL";

                                                                if (double.TryParse(expectedVal, NumberStyles.Any,
                                                                        CultureInfo.InvariantCulture,
                                                                        out double expNum) &&
                                                                    double.TryParse(actualVal, NumberStyles.Any,
                                                                        CultureInfo.InvariantCulture,
                                                                        out double actNum))
                                                                {
                                                                    if (Math.Abs(expNum - actNum) > 0.0001)
                                                                        throw new Exception(
                                                                            $"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                                                                }
                                                                else if (!expectedVal.Equals(actualVal,
                                                                             StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    throw new Exception(
                                                                        $"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                                                                }
                                                            }

                                                            return (true, cleanedSchema, cleanedResult);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        if (!cts.Token.IsCancellationRequested)
                                                            Dispatcher.UIThread.InvokeAsync(() =>
                                                                AddSqlOutput("Error",
                                                                    $"❌ Export Fehler ({cl.Name}): {ex.Message}",
                                                                    Brushes.Red));
                                                        return (false, null, null);
                                                    }
                                                }, cts.Token);

                                        if (!validData.Success) throw new Exception("Validierung fehlgeschlagen.");

                                        AddSqlOutput("System", "> Generiere Diagramme...", Brushes.LightGray);

                                        if (!string.IsNullOrWhiteSpace(draft.PlantUmlSource))
                                        {
                                            string prepared = PreparePlantUmlSource(draft.PlantUmlSource);
                                            draft.PlantUmlSvgContent =
                                                await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                        }

                                        SqlLevelDesigner.ExportLevel(cl.FilePath, draft, validData.Schema,
                                            validData.Result);
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_success.svg", 16);
                                        AddSqlOutput("System", $"> {cl.Name} erfolgreich exportiert!",
                                            Brushes.LightGreen);

                                        await Task.Delay(2000);
                                        RefreshUI();
                                    }
                                    else
                                    {
                                        AddToConsole($"\n> Quick Export gestartet für: {cl.Name}...",
                                            Brushes.LightGray);
                                        var draft = LevelDesigner.LoadDraft(cl.FilePath);

                                        bool valid = await Task.Run(async () =>
                                        {
                                            try
                                            {
                                                string fullCode =
                                                    "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" +
                                                    draft.TestCode;
                                                string validatorCode =
                                                    "using System;\nusing System.Reflection;\nusing System.Collections.Generic;\nusing System.Linq;\npublic static class DesignerValidator { " +
                                                    draft.ValidationCode + " }";

                                                var references = GetSafeReferences();

                                                var tree = CSharpSyntaxTree.ParseText(fullCode,
                                                    cancellationToken: cts.Token);
                                                var compilation = CSharpCompilation.Create(
                                                    $"QuickExport_{Guid.NewGuid()}",
                                                    new[] { tree },
                                                    references,
                                                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                                                using (var ms = new MemoryStream())
                                                {
                                                    var result = compilation.Emit(ms, cancellationToken: cts.Token);
                                                    if (!result.Success)
                                                    {
                                                        var diag = result.Diagnostics.FirstOrDefault(d =>
                                                            d.Severity == DiagnosticSeverity.Error);
                                                        throw new Exception(
                                                            $"Kompilierfehler: {diag?.GetMessage() ?? "Unbekannt"}");
                                                    }

                                                    ms.Seek(0, SeekOrigin.Begin);
                                                    var assembly = Assembly.Load(ms.ToArray());

                                                    // compile validator
                                                    var valTree = CSharpSyntaxTree.ParseText(validatorCode,
                                                        cancellationToken: cts.Token);
                                                    var valCompilation = CSharpCompilation.Create(
                                                        $"Validator_{Guid.NewGuid()}",
                                                        new[] { valTree },
                                                        references,
                                                        new CSharpCompilationOptions(
                                                            OutputKind.DynamicallyLinkedLibrary));

                                                    using (var valMs = new MemoryStream())
                                                    {
                                                        var valResult = valCompilation.Emit(valMs,
                                                            cancellationToken: cts.Token);
                                                        if (!valResult.Success)
                                                            throw new Exception("Fehler im Validierungs-Code.");

                                                        valMs.Seek(0, SeekOrigin.Begin);
                                                        var valAssembly = Assembly.Load(valMs.ToArray());
                                                        var valType = valAssembly.GetType("DesignerValidator");
                                                        var valMethod = valType.GetMethods(BindingFlags.Public |
                                                                BindingFlags.NonPublic | BindingFlags.Static)
                                                            .FirstOrDefault(m =>
                                                                m.ReturnType == typeof(bool) &&
                                                                m.GetParameters().Length == 2);

                                                        // run validation
                                                        object[] args = new object[] { assembly, null };
                                                        bool passed = (bool)valMethod.Invoke(null, args);

                                                        if (!passed)
                                                            throw new Exception(
                                                                $"Validierung nicht bestanden: {args[1]}");
                                                        return true;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (!cts.Token.IsCancellationRequested)
                                                    await Dispatcher.UIThread.InvokeAsync(() =>
                                                        AddToConsole($"\n❌ Export Fehler ({cl.Name}): {ex.Message}",
                                                            Brushes.Red));
                                                return false;
                                            }
                                        }, cts.Token);

                                        if (!valid) throw new Exception("Validierung fehlgeschlagen.");

                                        // generate diagrams
                                        AddToConsole("\n> Generiere Diagramme...", Brushes.LightGray);

                                        if (draft.PlantUmlSources != null && draft.PlantUmlSources.Count > 0 &&
                                            !string.IsNullOrWhiteSpace(draft.PlantUmlSources[0]))
                                        {
                                            string prepared = PreparePlantUmlSource(draft.PlantUmlSources[0]);
                                            string svgContent = await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                            if (draft.PlantUmlSvgContents == null)
                                                draft.PlantUmlSvgContents = new List<string>();
                                            if (draft.PlantUmlSvgContents.Count == 0) draft.PlantUmlSvgContents.Add("");
                                            draft.PlantUmlSvgContents[0] = svgContent;
                                        }

                                        for (int i = 0; i < draft.MaterialDiagrams.Count; i++)
                                            if (!string.IsNullOrWhiteSpace(draft.MaterialDiagrams[i].PlantUmlSource))
                                            {
                                                string prepared =
                                                    PreparePlantUmlSource(draft.MaterialDiagrams[i].PlantUmlSource);
                                                draft.MaterialDiagrams[i].PlantUmlSvgContent =
                                                    await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                            }

                                        // export
                                        LevelDesigner.ExportLevel(cl.FilePath, draft);
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_success.svg", 16);
                                        AddToConsole($"\n> {cl.Name} erfolgreich exportiert!", Brushes.LightGreen);

                                        await Task.Delay(2000);
                                        RefreshUI();
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                }
                                catch (Exception)
                                {
                                    btnQuickExport.Content = LoadIcon("assets/icons/ic_error.svg", 16);
                                    btnQuickExport.IsEnabled = true;
                                    btnQuickExport.Tag = "idle";
                                    await Task.Delay(2000);
                                    btnQuickExport.Content = LoadIcon("assets/icons/ic_generate.svg", 16);
                                }
                                finally
                                {
                                    win.Closing -= closingHandler;
                                    cts.Dispose();

                                    // reset button state after delay
                                    await Task.Delay(2000);
                                    if (btnQuickExport.Content != null)
                                    {
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_generate.svg", 16);
                                        btnQuickExport.IsEnabled = true;
                                        btnQuickExport.Tag = "idle";
                                    }
                                }
                            };
                            actionPanel.Children.Add(btnQuickExport);
                        }

                        if (cl.IsDraft)
                        {
                            var btnEdit = new Button
                            {
                                Content = LoadIcon("assets/icons/ic_edit.svg", 16),
                                Background = Brushes.Transparent,
                                Padding = new Thickness(8)
                            };
                            ToolTip.SetTip(btnEdit, "Level im Designer bearbeiten");
                            btnEdit.Click += (_, __) =>
                            {
                                win.Close();
                                ToggleDesignerMode(true, cl.FilePath);
                            };
                            actionPanel.Children.Add(btnEdit);
                        }

                        // delete button
                        var btnDelete = new Button
                        {
                            Content = LoadIcon("assets/icons/ic_delete.svg", 16),
                            Background = Brushes.Transparent,
                            Padding = new Thickness(8)
                        };
                        ToolTip.SetTip(btnDelete, "Level löschen");
                        btnDelete.Click += async (_, __) =>
                        {
                            await DeleteCustomLevel(cl, win);
                            RefreshUI();
                        };
                        actionPanel.Children.Add(btnDelete);

                        rowGrid.Children.Add(actionPanel);
                        return rowGrid;
                    }

                    // show groups
                    foreach (var group in folderGroups)
                    {
                        var groupContent = new StackPanel { Spacing = 5, Margin = new Thickness(0, 5, 0, 0) };
                        foreach (var cl in group) groupContent.Children.Add(CreateLevelRow(cl));

                        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                        headerPanel.Children.Add(new TextBlock
                        {
                            Text = group.Key,
                            Foreground = BrushTextTitle,
                            FontWeight = FontWeight.Bold,
                            VerticalAlignment = VerticalAlignment.Center
                        });

                        bool allComplete = group.All(l =>
                            !l.IsDraft && (_isSqlMode
                                ? customPlayerData.CompletedCustomSqlLevels.Contains(l.Name)
                                : customPlayerData.CompletedCustomLevels.Contains(l.Name)));
                        if (allComplete && group.Any())
                            headerPanel.Children.Add(LoadIcon("assets/icons/ic_done.svg", 16));

                        customStack.Children.Add(new Expander
                        {
                            Header = headerPanel,
                            Content = groupContent,
                            IsExpanded = false,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 0, 5)
                        });
                    }

                    foreach (var cl in rootLevels) customStack.Children.Add(CreateLevelRow(cl));
                }

                contentScroll.Content = customStack;
            }
        }

        RefreshUI();
        win.ShowDialog(this);
        CodeEditor.Focus();
    }

    private void LoadLevel(Level level)
    {
        if (level.Id > 0)
        {
            _isCustomLevelMode = false;
            _currentCustomValidationCode = null;
            _currentCustomAuthor = "";
            _currentCustomSvgs = null;
            _nextCustomLevelPath = null;
        }

        SaveCurrentProgress();

        // reset error highlighting on every load
        AppSettings.IsErrorHighlightingEnabled = false;
        ClearDiagnostics();

        currentLevel = level;
        _currentDiagramIndex = 0;
        UpdateNavigationButtons();

        PnlDiagramSwitch.IsVisible = false;
        BtnDiagram1.IsVisible = false;
        BtnDiagram2.IsVisible = false;
        BtnDiagram3.IsVisible = false;

        if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
        {
            ImgDiagram.Source = LoadDiagramImage(level.DiagramPaths[0]);
            TxtNoDiagram.IsVisible = false;

            if (level.DiagramPaths.Count > 1)
            {
                PnlDiagramSwitch.IsVisible = true;
                BtnDiagram1.IsVisible = true;
                BtnDiagram2.IsVisible = true;

                // highlight first button
                BtnDiagram1.Background = SolidColorBrush.Parse("#007ACC");
                BtnDiagram2.Background = SolidColorBrush.Parse("#3C3C3C");
                BtnDiagram3.Background = SolidColorBrush.Parse("#3C3C3C");

                if (level.DiagramPaths.Count >= 3) BtnDiagram3.IsVisible = true;
            }
        }
        else
        {
            ImgDiagram.Source = null;
            TxtNoDiagram.IsVisible = true;
        }

        string rawCode = level.StarterCode;
        if (_isCustomLevelMode)
        {
            // custom levels
            if (customPlayerData.UserCode.ContainsKey(level.Title)) rawCode = customPlayerData.UserCode[level.Title];
        }
        else
        {
            // standard levels
            if (playerData.UserCode.ContainsKey(level.Id)) rawCode = playerData.UserCode[level.Id];
        }

        CodeEditor.Text = rawCode;
        CodeEditor.CaretOffset = 0; // reset caret pos

        // reset uml zoom
        if (!_isSqlMode && !level.NoUMLAutoScale)
            _currentScale = 0.5;
        else
            _currentScale = 1.0;
        if (ImgScale != null)
        {
            ImgScale.ScaleX = _currentScale;
            ImgScale.ScaleY = _currentScale;
        }

        if (ImgTranslate != null)
        {
            ImgTranslate.X = 0;
            ImgTranslate.Y = 0;
        }

        if (ImgDiagram != null)
        {
            ImgDiagram.HorizontalAlignment = HorizontalAlignment.Center;
            ImgDiagram.VerticalAlignment = VerticalAlignment.Center;
        }

        PnlTask.Children.Clear();

        if (_isCustomLevelMode)
        {
            // custom level header
            PnlTask.Children.Add(
                new SelectableTextBlock
                {
                    Text = level.Title,
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0)
                }
            );

            if (!string.IsNullOrEmpty(_currentCustomAuthor))
            {
                PnlTask.Children.Add(
                    new SelectableTextBlock
                    {
                        Text = $"von {_currentCustomAuthor}",
                        FontSize = 14,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(0, 0, 0, 20)
                    }
                );
            }
            else
            {
                if (PnlTask.Children.Last() is Control last) last.Margin = new Thickness(0, 0, 0, 20);
            }
        }
        else
        {
            // standard level header
            PnlTask.Children.Add(
                new SelectableTextBlock
                {
                    Text = $"{level.Id}. {level.Title}",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                }
            );
        }

        WrapPanel tagsPanel = BuildTagsPanel(level.Difficulty, level.Topics, level.DiagramTags, false);
        if (tagsPanel != null) PnlTask.Children.Add(tagsPanel);

        RenderRichText(PnlTask, level.Description);

        try
        {
            if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
            {
                ImgDiagram.Source = LoadDiagramImage(level.DiagramPaths[0]);
                TxtNoDiagram.IsVisible = false;
            }
            else
            {
                ImgDiagram.Source = null;
                TxtNoDiagram.IsVisible = true;
            }
        }
        catch
        {
            ImgDiagram.Source = null;
            TxtNoDiagram.IsVisible = true;
        }

        GenerateMaterials(level, _isCustomLevelMode ? _currentCustomSvgs : null);

        TxtConsole.Inlines?.Clear();

        if (!_isCustomLevelMode)
            AddToConsole($"> System initialisiert.\n> Level {level.Id} (Code: {level.SkipCode}) geladen.",
                Brushes.LightGray);
        else
            AddToConsole("> System initialisiert.", Brushes.LightGray);

        DiscordRpcManager.ResetTimer();
        if (_isCustomLevelMode)
            DiscordRpcManager.UpdatePresence("C# Custom Level", "Solving a custom level", "aec_app_icon", "Custom");
        else
            DiscordRpcManager.UpdatePresence($"C# Level {level.Id}", "Coding greatness", "chsarp_icon", "C#");

        UpdateSemanticHighlighting(); // init scan

        Dispatcher.UIThread.Post(() => CodeEditor.Focus());
    }

    private void OpenLevelsFolder()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", path);
        }
        catch (Exception ex)
        {
            AddToConsole($"\n> Fehler beim Öffnen des Ordners: {ex.Message}", Brushes.Orange);
        }
    }

    private List<CustomLevelInfo> GetCustomLevels()
    {
        var list = new List<CustomLevelInfo>();
        string rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");

        if (!Directory.Exists(rootPath)) return list;

        (string name, string author, bool quickGen) GetMetadata(string file)
        {
            try
            {
                string json = File.ReadAllText(file);

                if (!file.EndsWith("draft", StringComparison.OrdinalIgnoreCase) && !json.TrimStart().StartsWith("{"))
                    json = LevelEncryption.Decrypt(json);

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    string name = root.TryGetProperty("Name", out var n)
                        ? n.GetString()
                        : Path.GetFileNameWithoutExtension(file);
                    string author = root.TryGetProperty("Author", out var a) ? a.GetString() : "Unbekannt";

                    bool quickGen = false;
                    if (root.TryGetProperty("QuickGenerate", out var qg))
                    {
                        if (qg.ValueKind == JsonValueKind.True) quickGen = true;
                        if (qg.ValueKind == JsonValueKind.String && qg.GetString().ToLower() == "true") quickGen = true;
                    }

                    return (name, author, quickGen);
                }
            }
            catch
            {
                return (Path.GetFileNameWithoutExtension(file), "Fehler", false);
            }
        }

        void ScanDirectory(string dir, string sectionName)
        {
            if (!Directory.Exists(dir)) return;

            // regular custom levels
            foreach (var file in Directory.GetFiles(dir, _isSqlMode ? "*.eliteslvl" : "*.elitelvl"))
            {
                var meta = GetMetadata(file);
                list.Add(new CustomLevelInfo
                {
                    Name = meta.name,
                    Author = meta.author,
                    FilePath = file,
                    Section = sectionName,
                    IsDraft = false
                });
            }

            // custom level drafts
            foreach (var file in Directory.GetFiles(dir, _isSqlMode ? "*.eliteslvldraft" : "*.elitelvldraft"))
            {
                var meta = GetMetadata(file);
                list.Add(new CustomLevelInfo
                {
                    Name = meta.name,
                    Author = meta.author,
                    FilePath = file,
                    Section = sectionName,
                    IsDraft = true,
                    QuickGenerate = meta.quickGen
                });
            }
        }

        // scan subdirectories (groups)
        foreach (var subdir in Directory.GetDirectories(rootPath))
        {
            string dirName = new DirectoryInfo(subdir).Name;
            ScanDirectory(subdir, dirName);
        }

        // ccan root directory
        ScanDirectory(rootPath, "Einzelne Levels");

        return list;
    }

    private async Task<string> ShowAddLevelDialog(Window owner)
    {
        var dialog = new Window
        {
            Title = "Neues Level",
            Width = 400,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto"),
            Margin = new Thickness(20)
        };

        // header
        rootGrid.Children.Add(new TextBlock
        {
            Text = "Neues Custom Level",
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            FontSize = 16,
            Margin = new Thickness(0, 0, 0, 20)
        });

        var contentPanel = new Panel();
        Grid.SetRow(contentPanel, 1);

        // manual mode ui
        var panelManual = new StackPanel
        {
            Spacing = 15,
            IsVisible = true
        };

        panelManual.Children.Add(new TextBlock
        {
            Text = "Name:",
            Foreground = Brushes.Gray
        });
        var txtName = new TextBox
        {
            Watermark = "Level Name",
            Background = SolidColorBrush.Parse("#1E1E1E"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8)
        };
        panelManual.Children.Add(txtName);

        panelManual.Children.Add(new TextBlock
        {
            Text = "Autor:",
            Foreground = Brushes.Gray
        });
        var txtAuthor = new TextBox
        {
            Watermark = "Autor Name",
            Background = SolidColorBrush.Parse("#1E1E1E"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8)
        };
        panelManual.Children.Add(txtAuthor);

        contentPanel.Children.Add(panelManual);

        // ai mode ui
        var panelAi = new Grid
        {
            IsVisible = false,
            RowDefinitions = new RowDefinitions("*, Auto")
        };

        var txtJson = new TextBox
        {
            Watermark = "Füge hier den JSON-Code der KI ein...",
            Background = SolidColorBrush.Parse("#1E1E1E"),
            Foreground = Brushes.Gray,
            BorderThickness = new Thickness(1),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily(MonospaceFontFamily),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top,
            MinHeight = 120
        };
        panelAi.Children.Add(txtJson);

        // ai tools row
        var aiRowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            Margin = new Thickness(0, 10, 0, 0)
        };
        Grid.SetRow(aiRowGrid, 1);

        // error container
        var errorStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var txtErrorMsg = new TextBlock
        {
            Text = "Ungültiges JSON", // placeholder
            Foreground = SolidColorBrush.Parse("#FF5555"),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        };
        ToolTip.SetTip(txtErrorMsg, "Fehler im JSON Format");

        string fullErrorText = ""; // store full error for copy

        var btnCopyError = new Button
        {
            Content = LoadIcon("assets/icons/ic_copy.svg", 14),
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnCopyError, "Fehler kopieren");
        btnCopyError.Click += async (s, e) =>
        {
            var topLevel = GetTopLevel(dialog);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(fullErrorText))
            {
                await topLevel.Clipboard.SetTextAsync(fullErrorText);
                btnCopyError.Background = SolidColorBrush.Parse("#2E8B57"); // flash green
                await Task.Delay(500);
                btnCopyError.Background = SolidColorBrush.Parse("#3C3C3C");
            }
        };

        errorStack.Children.Add(txtErrorMsg);
        errorStack.Children.Add(btnCopyError);
        aiRowGrid.Children.Add(errorStack);

        // tools container
        var aiToolsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Grid.SetColumn(aiToolsPanel, 1);

        var btnGuide = new Button
        {
            Content = LoadIcon("assets/icons/ic_guide.svg", 18),
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnGuide, "Anleitung öffnen");
        btnGuide.Click += (_, __) =>
        {
            try
            {
                var url = _isSqlMode
                    ? "https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_AI_LEVEL_CREATION_GUIDE"
                    : "https://github.com/OnlyCook/abitur-elite-code/wiki/CS_AI_LEVEL_CREATION_GUIDE";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
            }
            catch
            {
            }
        };

        var btnPaste = new Button
        {
            Content = LoadIcon("assets/icons/ic_import.svg", 18),
            Background = SolidColorBrush.Parse("#007ACC"),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnPaste, "Aus Zwischenablage einfügen");
        btnPaste.Click += async (_, __) =>
        {
            var topLevel = GetTopLevel(dialog);
            if (topLevel?.Clipboard != null)
            {
                string text = await topLevel.Clipboard.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    txtJson.Text = text;
                    errorStack.IsVisible = false; // reset error
                }
            }
        };

        aiToolsPanel.Children.Add(btnGuide);
        aiToolsPanel.Children.Add(btnPaste);
        aiRowGrid.Children.Add(aiToolsPanel);

        panelAi.Children.Add(aiRowGrid);
        contentPanel.Children.Add(panelAi);
        rootGrid.Children.Add(contentPanel);

        // footer grid
        var footerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *"),
            Margin = new Thickness(0, 20, 0, 0)
        };
        Grid.SetRow(footerGrid, 2);

        // mode switching button
        var btnSwitchMode = new Button
        {
            Content = "KI Import Modus",
            Background = Brushes.Transparent,
            Foreground = SolidColorBrush.Parse("#0088e3"),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursor.Parse("Hand"),
            Padding = new Thickness(5, 5, 5, 5)
        };

        bool isAiMode = false;
        btnSwitchMode.Click += (_, __) =>
        {
            isAiMode = !isAiMode;
            panelManual.IsVisible = !isAiMode;
            panelAi.IsVisible = isAiMode;
            errorStack.IsVisible = false;

            if (isAiMode)
            {
                btnSwitchMode.Content = "Manueller Modus";
                btnSwitchMode.Foreground = Brushes.Gray;
                txtJson.Focus();
            }
            else
            {
                btnSwitchMode.Content = "KI Import Modus";
                btnSwitchMode.Foreground = SolidColorBrush.Parse("#0088e3");
                txtName.Focus();
            }
        };
        footerGrid.Children.Add(btnSwitchMode);

        // action buttons
        var actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Grid.SetColumn(actionPanel, 1);

        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White
        };
        var btnCreate = new Button
        {
            Content = "Erstellen",
            Background = SolidColorBrush.Parse("#32A852"),
            Foreground = Brushes.White
        };

        string resultPath = null;

        btnCancel.Click += (_, __) => dialog.Close();
        btnCreate.Click += (_, __) =>
        {
            try
            {
                if (isAiMode)
                {
                    if (string.IsNullOrWhiteSpace(txtJson.Text)) return;

                    try
                    {
                        var doc = JsonDocument.Parse(txtJson.Text);

                        if (!doc.RootElement.TryGetProperty("Name", out var nameProp) ||
                            !doc.RootElement.TryGetProperty("Author", out var authProp))
                            throw new Exception("JSON muss 'Name' und 'Author' enthalten.");

                        string name = nameProp.GetString();
                        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                        string filename = $"{safeName}.{(_isSqlMode ? "eliteslvldraft" : "elitelvldraft")}";
                        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        string path = Path.Combine(dir, filename);
                        File.WriteAllText(path, txtJson.Text);

                        resultPath = path;
                        dialog.Close();
                    }
                    catch (Exception ex)
                    {
                        fullErrorText = ex.Message;
                        txtErrorMsg.Text = "Fehler: " + ex.Message;
                        ToolTip.SetTip(txtErrorMsg, ex.Message); // show full error on hover
                        errorStack.IsVisible = true;
                    }
                }
                else // manual mode
                {
                    if (string.IsNullOrWhiteSpace(txtName.Text)) return;

                    string safeName = string.Join("_", txtName.Text.Split(Path.GetInvalidFileNameChars()));
                    string filename = $"{safeName}.{(_isSqlMode ? "eliteslvldraft" : "elitelvldraft")}";
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    string path = Path.Combine(dir, filename);

                    var newDraft = new LevelDraft
                    {
                        Name = txtName.Text,
                        Author = txtAuthor.Text
                    };

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(newDraft, options);

                    File.WriteAllText(path, json);
                    resultPath = path;

                    dialog.Close();
                }
            }
            catch (Exception ex)
            {
                AddToConsole($"\n> Fehler beim Erstellen: {ex.Message}", Brushes.Red);
            }
        };

        actionPanel.Children.Add(btnCancel);
        actionPanel.Children.Add(btnCreate);
        footerGrid.Children.Add(actionPanel);

        rootGrid.Children.Add(footerGrid);

        dialog.Content = new Border { Child = rootGrid };
        await dialog.ShowDialog(owner);

        return resultPath;
    }

    private async Task DeleteCustomLevel(CustomLevelInfo info, Window owner)
    {
        var dialog = new Window
        {
            Title = "Löschen?",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };
        grid.Children.Add(new TextBlock
        {
            Text = $"Möchtest du '{info.Name}' wirklich löschen?",
            TextWrapping = TextWrapping.Wrap,
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

        var btnYes = new Button
        {
            Content = "Löschen",
            Background = SolidColorBrush.Parse("#B43232"),
            Foreground = Brushes.White
        };
        var btnNo = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White
        };

        btnNo.Click += (_, __) => dialog.Close();
        btnYes.Click += (_, __) =>
        {
            try
            {
                if (File.Exists(info.FilePath))
                    File.Delete(info.FilePath);

                // remove saved data for this level
                if (!info.IsDraft)
                {
                    bool changed = false;
                    if (_isSqlMode)
                    {
                        if (customPlayerData.CompletedCustomSqlLevels.Contains(info.Name))
                        {
                            customPlayerData.CompletedCustomSqlLevels.Remove(info.Name);
                            changed = true;
                        }

                        if (customPlayerData.UserSqlCode.ContainsKey(info.Name))
                        {
                            customPlayerData.UserSqlCode.Remove(info.Name);
                            changed = true;
                        }

                        if (customPlayerData.UserSqlModels.ContainsKey(info.Name))
                        {
                            customPlayerData.UserSqlModels.Remove(info.Name);
                            changed = true;
                        }
                    }
                    else
                    {
                        if (customPlayerData.CompletedCustomLevels.Contains(info.Name))
                        {
                            customPlayerData.CompletedCustomLevels.Remove(info.Name);
                            changed = true;
                        }

                        if (customPlayerData.UserCode.ContainsKey(info.Name))
                        {
                            customPlayerData.UserCode.Remove(info.Name);
                            changed = true;
                        }
                    }

                    if (changed) SaveSystem.SaveCustom(customPlayerData);
                }
            }
            catch (Exception ex)
            {
                AddToConsole($"\n> Fehler: {ex.Message}", Brushes.Red);
            }

            dialog.Close();
        };

        btnPanel.Children.Add(btnNo);
        btnPanel.Children.Add(btnYes);
        grid.Children.Add(btnPanel);

        dialog.Content = grid;
        await dialog.ShowDialog(owner);
    }

    private void LoadCustomLevelFromFile(string path)
    {
        if (path.EndsWith(".eliteslvl", StringComparison.OrdinalIgnoreCase))
        {
            string json = File.ReadAllText(path);

            if (!json.TrimStart().StartsWith("{")) json = LevelEncryption.Decrypt(json);

            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                int customId = path.GetHashCode();
                if (customId > 0) customId *= -1;

                var loadedLevel = new SqlLevel
                {
                    Id = customId,
                    Title = root.TryGetProperty("Title", out var titleProp) ? titleProp.GetString() : "Unbekannt",
                    Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                    MaterialDocs = root.TryGetProperty("MaterialDocs", out var matProp) ? matProp.GetString() : "",
                    SetupScript = root.TryGetProperty("SetupScript", out var setupProp) ? setupProp.GetString() : "",
                    VerificationQuery = root.TryGetProperty("VerificationQuery", out var vqProp)
                        ? vqProp.GetString()
                        : "",
                    SkipCode = "CUST",
                    Section = "Eigene Levels",
                    Prerequisites = new List<string>(),
                    ExpectedSchema = new List<SqlExpectedColumn>(),
                    ExpectedResult = new List<string[]>(),
                    DiagramPaths = new List<string>(),
                    PlantUMLSources = new List<string>()
                };

                if (root.TryGetProperty("Prerequisites", out var prereqElem))
                    foreach (var p in prereqElem.EnumerateArray())
                        loadedLevel.Prerequisites.Add(p.GetString());

                if (root.TryGetProperty("ExpectedSchema", out var schemaElem))
                    foreach (var col in schemaElem.EnumerateArray())
                        loadedLevel.ExpectedSchema.Add(new SqlExpectedColumn
                        {
                            Name = col.GetProperty("Name").GetString(),
                            Type = col.GetProperty("Type").GetString(),
                            StrictName = col.GetProperty("StrictName").GetBoolean()
                        });

                if (root.TryGetProperty("ExpectedResult", out var resElem))
                    foreach (var row in resElem.EnumerateArray())
                    {
                        var arr = new string[row.GetArrayLength()];
                        int i = 0;
                        // replace commas with periods cuz globalization issues
                        foreach (var cell in row.EnumerateArray()) arr[i++] = cell.GetString()?.Replace(",", ".");
                        loadedLevel.ExpectedResult.Add(arr);
                    }

                if (root.TryGetProperty("Author", out var authorElem))
                    _currentCustomAuthor = authorElem.GetString();

                if (root.TryGetProperty("DiagramPaths", out var svgsListElem))
                {
                    int idx = 0;
                    foreach (var svgElem1 in svgsListElem.EnumerateArray())
                    {
                        string svgContent = svgElem1.GetString();
                        if (!string.IsNullOrEmpty(svgContent))
                        {
                            string tempSvgPath = Path.Combine(Path.GetTempPath(),
                                $"elite_custom_{Math.Abs(customId)}_{idx}.svg");
                            File.WriteAllText(tempSvgPath, svgContent);
                            loadedLevel.DiagramPaths.Add(tempSvgPath);
                        }

                        idx++;
                    }
                }

                if (root.TryGetProperty("PlantUMLSources", out var srcListElem))
                    foreach (var s in srcListElem.EnumerateArray())
                        loadedLevel.PlantUMLSources.Add(s.GetString());

                _isCustomLevelMode = true;
                _nextCustomLevelPath = null;

                LoadSqlLevel(loadedLevel);
                AddSqlOutput("System", $"> Custom Level geladen: {loadedLevel.Title}", Brushes.LightGreen);
            }

            return;
        }

        string json2 = File.ReadAllText(path);

        if (!json2.TrimStart().StartsWith("{")) json2 = LevelEncryption.Decrypt(json2);

        using (var doc = JsonDocument.Parse(json2))
        {
            var root = doc.RootElement;
            int customId = path.GetHashCode();
            if (customId > 0) customId *= -1;

            var loadedLevel = new Level
            {
                Id = customId,
                Title = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() :
                    root.TryGetProperty("Title", out var titleProp2) ? titleProp2.GetString() : "Unbekannt",
                Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                StarterCode = root.TryGetProperty("StarterCode", out var scProp) ? scProp.GetString() : "",
                MaterialDocs = root.TryGetProperty("MaterialDocs", out var matProp) ? matProp.GetString() : "",
                SkipCode = "CUST",
                Section = "Eigene Levels",
                Prerequisites = new List<string>(),
                AuxiliaryIds = new List<string>(),
                DiagramPaths = new List<string>(),
                PlantUMLSources = new List<string>()
            };

            if (root.TryGetProperty("Author", out var authorElem))
                _currentCustomAuthor = authorElem.GetString();

            if (root.TryGetProperty("Prerequisites", out var prereqElem))
                foreach (var p in prereqElem.EnumerateArray())
                    loadedLevel.Prerequisites.Add(p.GetString());

            if (root.TryGetProperty("PlantUmlSvg", out var svgElem))
            {
                string svgContent = svgElem.GetString();
                if (!string.IsNullOrEmpty(svgContent))
                {
                    string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}.svg");
                    File.WriteAllText(tempSvgPath, svgContent);

                    if (loadedLevel.DiagramPaths == null) loadedLevel.DiagramPaths = new List<string>();
                    loadedLevel.DiagramPaths.Add(tempSvgPath);
                }
            }

            _currentCustomSvgs = new List<string>();
            if (root.TryGetProperty("MaterialDiagramSvgs", out var svgsElem))
                foreach (var s in svgsElem.EnumerateArray())
                    _currentCustomSvgs.Add(s.GetString());

            if (root.TryGetProperty("PlantUmlSvgs", out var svgsListElem))
            {
                int idx = 0;
                foreach (var svgElem1 in svgsListElem.EnumerateArray())
                {
                    string svgContent = svgElem1.GetString();
                    if (!string.IsNullOrEmpty(svgContent))
                    {
                        string tempSvgPath = Path.Combine(Path.GetTempPath(),
                            $"elite_custom_{Math.Abs(customId)}_{idx}.svg");
                        File.WriteAllText(tempSvgPath, svgContent);
                        loadedLevel.DiagramPaths.Add(tempSvgPath);
                    }

                    idx++;
                }
            }
            else if (root.TryGetProperty("PlantUmlSvg", out var singleSvgElem)) // fallback
            {
                string svgContent = singleSvgElem.GetString();
                if (!string.IsNullOrEmpty(svgContent))
                {
                    string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}.svg");
                    File.WriteAllText(tempSvgPath, svgContent);
                    loadedLevel.DiagramPaths.Add(tempSvgPath);
                }
            }

            if (root.TryGetProperty("PlantUmlSources", out var srcListElem))
                foreach (var s in srcListElem.EnumerateArray())
                    loadedLevel.PlantUMLSources.Add(s.GetString());
            else if (root.TryGetProperty("PlantUmlSource", out var singleSrcElem)) // fallback
                loadedLevel.PlantUMLSources.Add(singleSrcElem.GetString());

            _currentCustomValidationCode =
                root.TryGetProperty("ValidationCode", out var valProp) ? valProp.GetString() : "";
            _isCustomLevelMode = true;
            _nextCustomLevelPath = null;

            LoadLevel(loadedLevel);
            AddToConsole($"\n> Custom Level geladen: {loadedLevel.Title}", Brushes.LightGreen);
        }
    }

    private void LoadSqlLevel(SqlLevel level)
    {
        // reset custom variables if its a standard level
        if (level.Id > 0)
        {
            _isCustomLevelMode = false;
            _currentCustomAuthor = "";
            _nextCustomLevelPath = null;
        }

        // check if leaving level 4 unresolved (completes the mission to not annoy user)
        if (currentSqlLevel?.Id == 4 && !playerData.Settings.SqlSpoilerHintDismissed)
        {
            playerData.Settings.SqlSpoilerHintDismissed = true;
            SaveSystem.Save(playerData);
        }

        SaveCurrentProgress();

        UpdateFocusedColumn(null, null);

        _consecutiveSqlFails = 0;

        currentSqlLevel = level;
        UpdateNavigationButtons();

        if (_isCustomLevelMode)
        {
            if (customPlayerData.UserSqlCode.ContainsKey(level.Title))
                SqlQueryEditor.Text = customPlayerData.UserSqlCode[level.Title];
            else
                SqlQueryEditor.Text = "";
        }
        else
        {
            if (playerData.UserSqlCode.ContainsKey(level.Id))
                SqlQueryEditor.Text = playerData.UserSqlCode[level.Id];
            else
                SqlQueryEditor.Text = "";
        }

        SqlQueryEditor.CaretOffset = 0; // reset caret

        PnlSqlOutput.Children.Clear();

        PnlTask.Children.Clear();

        if (_isCustomLevelMode)
        {
            PnlTask.Children.Add(new SelectableTextBlock
            {
                Text = level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled),
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextNormal,
                Margin = new Thickness(0)
            });

            if (!string.IsNullOrEmpty(_currentCustomAuthor))
            {
                PnlTask.Children.Add(new SelectableTextBlock
                {
                    Text = $"S{level.Id}. {level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)}",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    Margin = new Thickness(0, 0, 0, 15)
                });
            }
            else
            {
                if (PnlTask.Children.Last() is Control last) last.Margin = new Thickness(0, 0, 0, 20);
            }
        }
        else
        {
            // standard level header
            PnlTask.Children.Add(new SelectableTextBlock
            {
                Text = $"S{level.Id}. {level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)}",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextNormal,
                Margin = new Thickness(0, 0, 0, 15)
            });
        }

        PnlTaskRelationalModel.Children.Clear();
        PnlUmlRelationalModel.Children.Clear();

        _currentRelationalModel.Clear();
        if (!level.IsRelationalModelReadOnly)
        {
            if (_isCustomLevelMode && customPlayerData.UserSqlModels.ContainsKey(level.Title))
                try
                {
                    _currentRelationalModel =
                        JsonSerializer.Deserialize<List<RTable>>(customPlayerData.UserSqlModels[level.Title]) ??
                        new List<RTable>();
                }
                catch
                {
                }
            else if (!_isCustomLevelMode && playerData.UserSqlModels.ContainsKey(level.Id))
                try
                {
                    _currentRelationalModel =
                        JsonSerializer.Deserialize<List<RTable>>(playerData.UserSqlModels[level.Id]) ??
                        new List<RTable>();
                }
                catch
                {
                }

            if (_currentRelationalModel.Count == 0 && level.InitialRelationalModel != null &&
                level.InitialRelationalModel.Count > 0)
            {
                string json = JsonSerializer.Serialize(level.InitialRelationalModel);
                _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(json);
            }
        }
        else if (level.InitialRelationalModel != null && level.InitialRelationalModel.Count > 0)
        {
            string json = JsonSerializer.Serialize(level.InitialRelationalModel);
            _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(json);
        }

        _initialRelationalModelJson = JsonSerializer.Serialize(_currentRelationalModel);

        UpdateSqlAutocompleteSchema();

        // initial rendering for active tab
        if (MainTabs.SelectedIndex == 0) RenderRelationalModel(PnlTaskRelationalModel, level.IsRelationalModelReadOnly);
        else if (MainTabs.SelectedIndex == 1)
            RenderRelationalModel(PnlUmlRelationalModel, level.IsRelationalModelReadOnly);

        WrapPanel tagsPanel = BuildTagsPanel(level.Difficulty, null, level.DiagramTags, true);
        if (tagsPanel != null) PnlTask.Children.Add(tagsPanel);

        RenderRichText(PnlTask, level.Description);

        // materials
        GenerateMaterials(new Level
        {
            MaterialDocs = level.MaterialDocs,
            AuxiliaryIds = level.AuxiliaryIds,
            Prerequisites = level.Prerequisites,
            OptionalPrerequisites = level.OptionalPrerequisites
        });

        // diagrams
        PnlDiagramSwitch.IsVisible = false;

        ImgDiagram.Source = null; // reset first
        bool diagramLoaded = false;

        if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
        {
            var loadedImage = LoadDiagramImage(level.DiagramPaths[0]);
            if (loadedImage != null)
            {
                ImgDiagram.Source = loadedImage;
                diagramLoaded = true;
            }
        }

        _currentScale = 1.0;
        if (ImgScale != null)
        {
            ImgScale.ScaleX = _currentScale;
            ImgScale.ScaleY = _currentScale;
        }

        if (ImgTranslate != null)
        {
            ImgTranslate.X = 0;
            ImgTranslate.Y = 0;
        }

        TxtNoDiagram.IsVisible = !diagramLoaded;

        if (_isCustomLevelMode)
            AddSqlOutput("System", "Level geladen.\nDatenbank zurückgesetzt.", Brushes.Gray);
        else
            AddSqlOutput("System", $"Level S{level.Id} (Code: {level.SkipCode}) geladen.\nDatenbank zurückgesetzt.",
                Brushes.Gray);

        HideSpoilerHint();
        _spoilerDelayMet = false;
        _spoilerDelayTimer.Stop();

        if (!AppSettings.IsSqlAntiSpoilerEnabled && !playerData.Settings.SqlSpoilerHintDismissed)
            if (level.Id == 3 || level.Id == 4)
                _spoilerDelayTimer.Start();

        DiscordRpcManager.ResetTimer();
        if (_isCustomLevelMode)
            DiscordRpcManager.UpdatePresence("SQL Custom Level", "Solving a custom level", "aec_app_icon", "Custom");
        else
            DiscordRpcManager.UpdatePresence($"SQL Level {level.Id}", "Querying greatness", "mysql_icon", "MySQL");
    }
}