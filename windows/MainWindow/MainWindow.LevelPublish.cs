using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private async void BtnDesignerPublish_Click(object sender, RoutedEventArgs e)
    {
        if (_isSqlMode && _verifiedSqlDraftState == null) return;
        if (!_isSqlMode && _verifiedDraftState == null) return;

        // placeholder logic to determine if the level is already published
        bool isEditMode = false;
        int currentVersion = 1; // placeholder: fetch how many times it was edited

        await OpenPublishDialog(isEditMode, currentVersion);
    }

    private bool ValidateLevelSecurity(out string errorFeedback)
    {
        errorFeedback = "";

        if (!_isSqlMode)
        {
            string combinedCode = $"{_currentDraft.StarterCode}\n{_currentDraft.ValidationCode}\n{_currentDraft.TestCode}";

            // blacklist of potentially harmful namespaces, classes and methods
            string[] csharpBlacklist = {
                "System.IO", "System.Net", "System.Reflection.Emit", "System.Diagnostics",
                "System.Runtime.InteropServices", "System.Threading", "DllImport",
                "Process.Start", "File.", "Directory.", "HttpClient", "WebRequest",
                "unsafe", "stackalloc", "AppDomain", "Assembly.Load", "Environment.Exit",
                "StreamReader", "StreamWriter", "Socket", "TcpClient"
            };

            foreach (var term in csharpBlacklist)
            {
                if (combinedCode.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    errorFeedback = $"Sicherheitsrisiko: Die Verwendung von '{term}' ist aus Sicherheitsgründen nicht gestattet.";
                    return false;
                }
            }

            // static infinite loop detection
            var infiniteLoopRegex = new Regex(@"while\s*\(\s*true\s*\)|for\s*\(\s*;\s*;\s*\)", RegexOptions.IgnoreCase);
            if (infiniteLoopRegex.IsMatch(combinedCode))
            {
                errorFeedback = "Sicherheitsrisiko: Endlosschleifen (z.B. while(true)) sind in veröffentlichten Leveln verboten, um Abstürze zu verhindern.";
                return false;
            }

            // bloatware / memory exhaustion checks (large arrays)
            if (Regex.IsMatch(combinedCode, @"new\s+(int|byte|string|double|float|long|short|char)\[\s*[0-9]{6,}\s*\]"))
            {
                errorFeedback = "Sicherheitsrisiko: Extrem große Arrays können den Arbeitsspeicher anderer Nutzer überlasten.";
                return false;
            }
        }
        else
        {
            string combinedSql = $"{_currentSqlDraft.SetupScript}\n{_currentSqlDraft.VerificationQuery}\n{_currentSqlDraft.SampleSolution}";

            // blacklist of destructive or bypass-prone sqlite pragmas/functions
            string[] sqlBlacklist = {
                "PRAGMA", "ATTACH DATABASE", "DETACH DATABASE", "SYSTEM(", "EXEC(", "XP_CMDSHELL"
            };

            string upperSql = combinedSql.ToUpper();
            foreach (var term in sqlBlacklist)
            {
                if (upperSql.Contains(term))
                {
                    errorFeedback = $"Sicherheitsrisiko: Der SQL-Befehl '{term}' ist nicht gestattet.";
                    return false;
                }
            }
        }

        return true;
    }

    private async Task OpenPublishDialog(bool isEditMode = false, int editVersion = 1)
    {
        bool isDirty = false;
        bool isPublishing = false;
        bool forceClose = false;

        var dialog = new Window
        {
            Title = isEditMode ? "Veröffentlichtes Level Bearbeiten" : "Level Veröffentlichen",
            Width = 550,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#202124"),
            CornerRadius = new CornerRadius(8)
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto")
        };
        var contentStack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20)
        };

        contentStack.Children.Add(new TextBlock
        {
            Text = isEditMode ? "Veröffentlichtes Level Bearbeiten" : "Level Veröffentlichen",
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#7a30b0")
        });
        contentStack.Children.Add(new TextBlock
        {
            Text = isEditMode
                ? $"Teile dein Level mit der Community. Du teilst aktuell Version v{editVersion + 1}. Es wird auf Sicherheit und Größe geprüft."
                : "Teile dein Level mit der Community. Es wird vor der Veröffentlichung auf Sicherheit und Größe geprüft.",
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        });

        var formGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("120, *"),
            RowDefinitions = new RowDefinitions("Auto, Auto, Auto, Auto"),
            Margin = new Thickness(0, 10, 0, 0)
        };

        // title
        formGrid.Children.Add(new TextBlock
        {
            Text = "Level Name:",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        var txtTitle = new TextBox
        {
            Text = _isSqlMode ? _currentSqlDraft.Name : _currentDraft.Name,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = Brushes.White
        };
        txtTitle.TextChanged += (s, e) => isDirty = true;
        Grid.SetColumn(txtTitle, 1);
        formGrid.Children.Add(txtTitle);

        // author
        var lblAuthor = new TextBlock
        {
            Text = "Autor:",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(lblAuthor, 1);
        var txtAuthor = new TextBox
        {
            Text = AppSettings.GithubUsername,
            IsReadOnly = true,
            Background = SolidColorBrush.Parse("#111111"),
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetColumn(txtAuthor, 1);
        Grid.SetRow(txtAuthor, 1);
        formGrid.Children.Add(lblAuthor);
        formGrid.Children.Add(txtAuthor);

        // difficulty
        var lblDiff = new TextBlock
        {
            Text = "Schwierigkeit:",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(lblDiff, 2);
        var cmbDiff = new ComboBox
        {
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = Brushes.White,
            Margin = new Thickness(0, 15, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        cmbDiff.Items.Add("Einfach");
        cmbDiff.Items.Add("Mittel");
        cmbDiff.Items.Add("Schwer");
        cmbDiff.Items.Add("Abitur");
        cmbDiff.SelectedIndex = -1; // force manual selection
        cmbDiff.SelectionChanged += (s, e) => isDirty = true;
        Grid.SetColumn(cmbDiff, 1);
        Grid.SetRow(cmbDiff, 2);
        formGrid.Children.Add(lblDiff);
        formGrid.Children.Add(cmbDiff);

        // tags system
        var lblTags = new TextBlock
        {
            Text = "Tags (max 3):",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(lblTags, 3);

        var tagsPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetColumn(tagsPanel, 1);
        Grid.SetRow(tagsPanel, 3);

        var txtTags = new TextBox
        {
            Watermark = "Keine Tags ausgewählt",
            IsReadOnly = true,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = Brushes.Gray
        };
        Grid.SetColumn(txtTags, 0);

        var btnTags = new Button
        {
            Content = "Tags wählen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(btnTags, 1);

        tagsPanel.Children.Add(txtTags);
        tagsPanel.Children.Add(btnTags);

        btnTags.Click += async (s, e) =>
        {
            var tagDialog = new Window
            {
                Title = "Tags auswählen",
                Width = 350,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            var dGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto, *, Auto"),
                Margin = new Thickness(20)
            };

            dGrid.Children.Add(new TextBlock
            {
                Text = "Wähle bis zu 3 Tags:",
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);

            var stack = new StackPanel { Spacing = 10 };

            // setup context-aware tags
            string[] tags = _isSqlMode
                ? new[] { "SELECT", "JOIN", "WHERE", "GROUP BY", "ORDER BY", "HAVING", "Subqueries", "DDL", "DML", "Functions", "Views", "Triggers", "Index", "Constraints", "Transactions" }
                : new[] { "Arrays", "Loops", "If-Else", "Methods", "Classes", "Recursion", "Strings", "Math", "Algorithms", "Data Structures", "LINQ", "Regex", "File I/O", "Exceptions", "Collections" };

            var currentSelected = txtTags.Text?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            var checkBoxes = new List<CheckBox>();

            foreach (var t in tags)
            {
                var cb = new CheckBox { Content = t, Foreground = Brushes.White, IsChecked = currentSelected.Contains(t) };
                checkBoxes.Add(cb);
                stack.Children.Add(cb);

                cb.IsCheckedChanged += (cs, ce) =>
                {
                    int count = checkBoxes.Count(c => c.IsChecked == true);
                    if (count > 3 && cb.IsChecked == true)
                    {
                        cb.IsChecked = false; // revert
                    }
                    else
                    {
                        // lock out remaining unchecked boxes if limit reached
                        foreach (var other in checkBoxes)
                        {
                            if (other.IsChecked != true)
                                other.IsEnabled = count < 3;
                        }
                    }
                };
            }

            // apply initial state limit locks immediately
            int initCount = checkBoxes.Count(c => c.IsChecked == true);
            foreach (var cb in checkBoxes) if (cb.IsChecked != true) cb.IsEnabled = initCount < 3;

            scroll.Content = stack;
            dGrid.Children.Add(scroll);

            var btnOk = new Button
            {
                Content = "Übernehmen",
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = SolidColorBrush.Parse("#7a30b0"),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 15, 0, 0),
                CornerRadius = new CornerRadius(4)
            };
            Grid.SetRow(btnOk, 2);

            btnOk.Click += (_, __) =>
            {
                var selected = checkBoxes.Where(c => c.IsChecked == true).Select(c => c.Content.ToString());
                txtTags.Text = string.Join(", ", selected);
                isDirty = true;
                tagDialog.Close();
            };
            dGrid.Children.Add(btnOk);

            tagDialog.Content = dGrid;
            await tagDialog.ShowDialog(dialog);
        };

        formGrid.Children.Add(lblTags);
        formGrid.Children.Add(tagsPanel);

        // clickable community guidelines inside checkbox
        var chkLegalContent = new WrapPanel { Orientation = Orientation.Horizontal };

        string legalTextStr = "Ich bestätige, dass dieses Level keine Schadsoftware enthält und stimme den";
        foreach (var word in legalTextStr.Split(' '))
        {
            chkLegalContent.Children.Add(new TextBlock
            {
                Text = word + " ",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.NoWrap
            });
        }

        var lnkCommunity = new TextBlock
        {
            Text = "Community-Richtlinien",
            Foreground = SolidColorBrush.Parse("#7a30b0"),
            TextDecorations = TextDecorations.Underline,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Margin = new Thickness(0, 0, 4, 0)
        };
        lnkCommunity.PointerPressed += (s, e) =>
        {
            e.Handled = true; // prevents checkbox state toggle
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://docs.github.com/en/site-policy/github-terms/github-community-guidelines",
                UseShellExecute = true
            });
        };

        chkLegalContent.Children.Add(lnkCommunity);
        chkLegalContent.Children.Add(new TextBlock
        {
            Text = "zu.",
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.NoWrap
        });

        contentStack.Children.Add(formGrid);

        var chkLegal = new CheckBox
        {
            Content = chkLegalContent,
            Margin = new Thickness(0, 15, 0, 0)
        };

        // progress area
        var pnlProgress = new StackPanel
        {
            IsVisible = false,
            Spacing = 10,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var txtStatus = new TextBlock
        {
            Text = "Überprüfe Sicherheit...",
            Foreground = Brushes.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        pnlProgress.Children.Add(progressBar);
        pnlProgress.Children.Add(txtStatus);

        contentStack.Children.Add(chkLegal);
        contentStack.Children.Add(pnlProgress);
        rootGrid.Children.Add(contentStack);

        // action buttons grid
        var actionButtonsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *"),
            Margin = new Thickness(20, 15, 20, 20)
        };
        Grid.SetRow(actionButtonsGrid, 1);

        if (isEditMode)
        {
            var btnDelete = new Button
            {
                Content = "Level löschen",
                Background = SolidColorBrush.Parse("#B43232"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            btnDelete.Click += async (_, __) =>
            {
                txtStatus.Text = "Level wird gelöscht...";
                pnlProgress.IsVisible = true;
                btnDelete.IsEnabled = false;

                // placeholder delete logic
                await Task.Delay(1000);

                forceClose = true;
                dialog.Close();
            };
            Grid.SetColumn(btnDelete, 0);
            actionButtonsGrid.Children.Add(btnDelete);
        }

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Grid.SetColumn(btnPanel, 1);

        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnPublish = new Button
        {
            Content = isEditMode ? "Aktualisieren" : "Veröffentlichen",
            Background = SolidColorBrush.Parse("#7a30b0"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            IsEnabled = false
        };

        chkLegal.IsCheckedChanged += (s, e) => btnPublish.IsEnabled = chkLegal.IsChecked == true;

        dialog.Closing += async (s, e) =>
        {
            if (isDirty && !isPublishing && !forceClose)
            {
                e.Cancel = true;
                var confirmDialog = new Window
                {
                    Title = "Achtung",
                    Width = 350,
                    Height = 130,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SystemDecorations = SystemDecorations.BorderOnly,
                    Background = SolidColorBrush.Parse("#252526"),
                    CornerRadius = new CornerRadius(8)
                };
                var dGrid = new Grid
                {
                    RowDefinitions = new RowDefinitions("*, Auto"),
                    Margin = new Thickness(20)
                };
                dGrid.Children.Add(new TextBlock
                {
                    Text = "Du hast ungespeicherte Eingaben. Möchtest du wirklich abbrechen?",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var dBtnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10
                };
                Grid.SetRow(dBtnPanel, 1);
                var btnYes = new Button
                {
                    Content = "Ja",
                    Background = SolidColorBrush.Parse("#B43232"),
                    Foreground = Brushes.White
                };
                var btnNo = new Button
                {
                    Content = "Nein",
                    Background = SolidColorBrush.Parse("#3C3C3C"),
                    Foreground = Brushes.White
                };
                btnYes.Click += (_, __) =>
                {
                    forceClose = true;
                    confirmDialog.Close();
                    dialog.Close();
                };
                btnNo.Click += (_, __) => confirmDialog.Close();
                dBtnPanel.Children.Add(btnNo);
                dBtnPanel.Children.Add(btnYes);

                dGrid.Children.Add(dBtnPanel);

                confirmDialog.Content = dGrid;
                await confirmDialog.ShowDialog(dialog);
            }
        };

        btnCancel.Click += (_, __) => dialog.Close();

        btnPublish.Click += async (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                txtStatus.Text = "Level Name darf nicht leer sein!";
                txtStatus.Foreground = Brushes.Orange;
                pnlProgress.IsVisible = true;
                dialog.Height = 515;
                return;
            }

            if (cmbDiff.SelectedIndex == -1)
            {
                txtStatus.Text = "Bitte wähle eine Schwierigkeit aus!";
                txtStatus.Foreground = Brushes.Orange;
                pnlProgress.IsVisible = true;
                dialog.Height = 515;
                return;
            }

            isPublishing = true;
            btnPublish.IsEnabled = false;
            btnCancel.IsEnabled = false;
            formGrid.Opacity = 0.5;
            chkLegal.Opacity = 0.5;
            pnlProgress.IsVisible = true;
            dialog.Height = 515;
            txtStatus.Foreground = Brushes.LightGray;

            await Task.Delay(500);

            // check security limits locally
            if (!ValidateLevelSecurity(out string securityError))
            {
                txtStatus.Text = securityError;
                txtStatus.Foreground = Brushes.Red;
                progressBar.IsIndeterminate = false;
                progressBar.Value = 100;
                progressBar.Foreground = Brushes.Red;
                btnCancel.IsEnabled = true;
                isPublishing = false;
                return;
            }

            txtStatus.Text = "Generiere finale Exportdaten...";
            await Task.Delay(200);

            // generate payload to check file size limits (we mimic the internal exporter objects)
            string encryptedData = "";
            try
            {
                string rawJson = "";

                if (_isSqlMode)
                {
                    var exportData = new
                    {
                        Title = txtTitle.Text, // apply new name overriding the draft
                        Author = AppSettings.GithubUsername,
                        _currentSqlDraft.Description,
                        MaterialDocs = _currentSqlDraft.Materials,
                        _currentSqlDraft.Prerequisites,
                        _currentSqlDraft.SetupScript,
                        VerificationQuery = _currentSqlDraft.IsDmlMode ? _currentSqlDraft.VerificationQuery : "",
                        ExpectedSchema = _verifiedExpectedSchema,
                        ExpectedResult = _verifiedExpectedResult,
                        PlantUMLSources = new List<string> { _currentSqlDraft.PlantUmlSource },
                        DiagramPaths = new List<string> { _currentSqlDraft.PlantUmlSvgContent },
                        _currentSqlDraft.IsRelationalModelReadOnly,
                        InitialRelationalModel = _currentSqlDraft.IsRelationalModelReadOnly ? _currentSqlDraft.InitialRelationalModel : new List<RTable>()
                    };
                    rawJson = JsonSerializer.Serialize(exportData);
                }
                else
                {
                    var exportData = new
                    {
                        Name = txtTitle.Text, // apply new name
                        Author = AppSettings.GithubUsername,
                        _currentDraft.Description,
                        MaterialDocs = _currentDraft.Materials,
                        _currentDraft.Prerequisites,
                        _currentDraft.StarterCode,
                        _currentDraft.ValidationCode,
                        PlantUmlSvgs = _currentDraft.PlantUmlSvgContents,
                        _currentDraft.PlantUmlSources,
                        MaterialDiagramSvgs = _currentDraft.MaterialDiagrams.Select(d => d.PlantUmlSvgContent).ToList()
                    };
                    rawJson = JsonSerializer.Serialize(exportData);
                }

                // compress data before encrypting
                string compressedData = CompressLevelData(rawJson);
                encryptedData = LevelEncryption.Encrypt(compressedData);
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Kompilierungsfehler beim Export: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
                progressBar.IsIndeterminate = false;
                btnCancel.IsEnabled = true;
                isPublishing = false;
                return;
            }

            if (encryptedData.Length > 50000)
            {
                txtStatus.Text = $"Level-Datei ist zu groß ({encryptedData.Length} / 50.000 Zeichen). Bitte kürze Beschreibungen, Code oder Diagramme.";
                txtStatus.Foreground = Brushes.Orange;
                progressBar.IsIndeterminate = false;
                progressBar.Value = 100;
                progressBar.Foreground = Brushes.Orange;
                btnCancel.IsEnabled = true;
                isPublishing = false;
                return;
            }

            txtStatus.Text = "Sende an Community-Server (Proxy)...";

            // placeholder for actual proxy pushing logic 
            await Task.Delay(2000);

            txtStatus.Text = isEditMode ? "Erfolgreich aktualisiert!" : "Erfolgreich veröffentlicht!";
            txtStatus.Foreground = Brushes.LightGreen;
            progressBar.IsIndeterminate = false;
            progressBar.Value = 100;
            progressBar.Foreground = Brushes.LightGreen;

            await Task.Delay(1500);
            forceClose = true;
            dialog.Close();
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnPublish);
        actionButtonsGrid.Children.Add(btnPanel);
        rootGrid.Children.Add(actionButtonsGrid);

        dialog.Content = rootGrid;

        await dialog.ShowDialog(this);
    }

    private string CompressLevelData(string jsonText)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(jsonText);
        using var msi = new System.IO.MemoryStream(bytes);
        using var mso = new System.IO.MemoryStream();
        using (var gs = new System.IO.Compression.GZipStream(mso, System.IO.Compression.CompressionMode.Compress))
        {
            msi.CopyTo(gs);
        }
        return Convert.ToBase64String(mso.ToArray());
    }
}
