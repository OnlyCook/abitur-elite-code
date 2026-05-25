#nullable disable
using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public partial class MainWindow
{
    public static readonly string[] CSharpTags = { "Arrays", "Loops", "If-Else", "Methods", "Classes", "Recursion", "Strings", "Math", "Algorithms", "Data Structures", "LINQ", "Regex", "File I/O", "Exceptions", "Collections" };
    public static readonly string[] SqlTags = { "SELECT", "JOIN", "WHERE", "GROUP BY", "ORDER BY", "HAVING", "Subqueries", "DDL", "DML", "Functions", "Views", "Triggers", "Index", "Constraints", "Transactions" };

    private static DateTime _lastLevelPublishTime = DateTime.MinValue;

    private async void BtnDesignerPublish_Click(object sender, RoutedEventArgs e)
    {
        if (_isSqlMode && _verifiedSqlDraftState == null) return;
        if (!_isSqlMode && _verifiedDraftState == null) return;

        string currentDiscussionId = _isSqlMode ? _currentSqlDraft.DiscussionId : _currentDraft.DiscussionId;
        bool isEditMode = !string.IsNullOrEmpty(currentDiscussionId);
        int currentVersion = isEditMode ? (_isSqlMode ? _currentSqlDraft.PublishVersion : _currentDraft.PublishVersion) : 0;

        await OpenPublishDialog(isEditMode, currentVersion, currentDiscussionId);
    }

    private bool ValidateLevelSecurity(out string errorFeedback)
    {
        errorFeedback = "";

        if (!_isSqlMode)
        {
            string combinedCode = $"{_currentDraft.StarterCode}\n{_currentDraft.ValidationCode}\n{_currentDraft.TestCode}";

            // basic infinite loop fallback check (semantic analyzer handles the rest)
            var infiniteLoopRegex = new Regex(@"while\s*\(\s*true\s*\)|for\s*\(\s*;\s*;\s*\)", RegexOptions.IgnoreCase);
            if (infiniteLoopRegex.IsMatch(combinedCode))
            {
                errorFeedback = "Sicherheitsrisiko: Offensichtliche Endlosschleifen (z.B. while(true)) sind in veröffentlichten Leveln verboten.";
                return false;
            }

            // run semantic analysis purely to validate safety
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(combinedCode);
            var refs = GetSafeReferences();
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Validation", new[] { tree }, refs);

            var semanticModel = compilation.GetSemanticModel(tree);
            var safetyCheck = SandboxSecurity.AnalyzeUserCode(tree, semanticModel);

            if (!safetyCheck.IsSafe)
            {
                errorFeedback = safetyCheck.ErrorFeedback;
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

    private async Task OpenPublishDialog(bool isEditMode = false, int editVersion = 1, string currentDiscussionId = null)
    {
        bool isDirty = false;
        bool isPublishing = false;
        bool forceClose = false;

        var dialog = new Window
        {
            Title = isEditMode ? "Level Bearbeiten" : "Level Veröffentlichen",
            Width = 550,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushBgPanel,
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
            Foreground = Scheme.BrushGlobalFg
        });
        contentStack.Children.Add(new TextBlock
        {
            Text = isEditMode
                ? $"Teile dein bearbeitetes Level mit der Community. Du teilst aktuell Version v{editVersion + 1}. Es wird stets auf Sicherheit und Größe geprüft."
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

        string defaultTitle = _isSqlMode ? _currentSqlDraft.Name : _currentDraft.Name;
        var txtTitle = new TextBox
        {
            Text = defaultTitle,
            MaxLength = 100,
            Background = Scheme.BrushBgPanel3,
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
            Background = Scheme.BrushBgPanel11,
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
            Background = Scheme.BrushBgPanel3,
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
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.Gray
        };
        Grid.SetColumn(txtTags, 0);

        // prefill edit mode data
        if (isEditMode)
        {
            string savedDiff = _isSqlMode ? _currentSqlDraft.PublishDifficulty : _currentDraft.PublishDifficulty;
            if (!string.IsNullOrEmpty(savedDiff)) cmbDiff.SelectedItem = savedDiff;

            string savedTags = _isSqlMode ? _currentSqlDraft.PublishTags : _currentDraft.PublishTags;
            if (!string.IsNullOrEmpty(savedTags)) txtTags.Text = savedTags;
        }

        var btnTags = new Button
        {
            Content = "Tags wählen",
            Background = Scheme.BrushBgPanel2,
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
                Background = Scheme.BrushTextNormal3,
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
            string[] tags = _isSqlMode ? SqlTags : CSharpTags;

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
                Background = Scheme.BrushGlobalBg,
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
            Foreground = Scheme.BrushTextHighlight,
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
                Background = Scheme.BrushDiffHard,
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            btnDelete.Click += async (_, __) =>
            {
                // show confirmation dialog before proceeding with deletion
                bool isConfirmed = false;
                var confirmDialog = new Window
                {
                    Title = "Level löschen",
                    Width = 400,
                    Height = 205,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SystemDecorations = SystemDecorations.BorderOnly,
                    Background = Scheme.BrushTextNormal3,
                    CornerRadius = new CornerRadius(8)
                };

                var dGrid = new Grid
                {
                    RowDefinitions = new RowDefinitions("*, Auto, Auto"),
                    Margin = new Thickness(20)
                };

                dGrid.Children.Add(new TextBlock
                {
                    Text = "Möchtest du dieses Level wirklich online löschen? Diese Aktion kann nicht rückgängig gemacht werden.\n\nBitte tippe \"loeschen\" ein, um fortzufahren.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Top
                });

                var txtConfirm = new TextBox
                {
                    Background = Scheme.BrushBgPanel3,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 15, 0, 15)
                };
                Grid.SetRow(txtConfirm, 1);
                dGrid.Children.Add(txtConfirm);

                var dBtnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10
                };
                Grid.SetRow(dBtnPanel, 2);

                var btnCancelDel = new Button
                {
                    Content = "Abbrechen",
                    Background = Scheme.BrushBgPanel2,
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4)
                };
                var btnConfirmDel = new Button
                {
                    Content = "Endgültig Löschen",
                    Background = Scheme.BrushDiffHard,
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4),
                    IsEnabled = false
                };

                txtConfirm.TextChanged += (s, e) => btnConfirmDel.IsEnabled = txtConfirm.Text == "loeschen";

                btnCancelDel.Click += (s, e) => confirmDialog.Close();
                btnConfirmDel.Click += (s, e) =>
                {
                    isConfirmed = true;
                    confirmDialog.Close();
                };

                dBtnPanel.Children.Add(btnCancelDel);
                dBtnPanel.Children.Add(btnConfirmDel);
                dGrid.Children.Add(dBtnPanel);
                confirmDialog.Content = dGrid;

                await confirmDialog.ShowDialog(dialog);

                if (!isConfirmed) return;

                // global cooldown check for edits/deletes
                double secondsSinceLast = (DateTime.Now - _lastLevelPublishTime).TotalSeconds;
                if (secondsSinceLast < 60)
                {
                    txtStatus.Text = $"Bitte warte {(int)(60 - secondsSinceLast)} Sekunden vor der nächsten Aktion.";
                    txtStatus.Foreground = Brushes.Orange;
                    pnlProgress.IsVisible = true;
                    dialog.Height = 515;
                    return;
                }

                txtStatus.Text = "Level wird gelöscht...";
                txtStatus.Foreground = Brushes.LightGray;
                pnlProgress.IsVisible = true;
                btnDelete.IsEnabled = false;
                dialog.Height = 515;

                try
                {
                    string endpoint = $"{RenderEndpoint(1).TrimEnd('/')}/level?discussionId={currentDiscussionId}";
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, endpoint);
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

                    var resp = await _httpClient.SendAsync(requestMessage);
                    string resBody = await resp.Content.ReadAsStringAsync();

                    if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && (resBody == "BANNED" || resBody == "PERMA_BANNED"))
                    {
                        forceClose = true;
                        dialog.Close();
                        if (resBody == "PERMA_BANNED") ShowPermaBanDialog(); else ShowBanDialog();
                        return;
                    }

                    if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "PUBLISH_LIMIT_REACHED")
                    {
                        forceClose = true;
                        dialog.Close();
                        await ShowLimitIncreaseDialog();
                        return;
                    }

                    if (resp.StatusCode == (System.Net.HttpStatusCode)429)
                    {
                        txtStatus.Text = "Zu viele Anfragen. Bitte warte eine Minute.";
                        txtStatus.Foreground = Brushes.Orange;
                        progressBar.IsIndeterminate = false;
                        btnDelete.IsEnabled = true;
                        return;
                    }

                    if (resp.IsSuccessStatusCode)
                    {
                        _lastLevelPublishTime = DateTime.Now;

                        txtStatus.Text = "Level erfolgreich gelöscht!";
                        txtStatus.Foreground = Brushes.LightGreen;
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = 100;
                        progressBar.Foreground = Brushes.LightGreen;

                        if (_isSqlMode)
                        {
                            _currentSqlDraft.DiscussionId = null;
                            _currentSqlDraft.PublishVersion = 0;
                        }
                        else
                        {
                            _currentDraft.DiscussionId = null;
                            _currentDraft.PublishVersion = 0;
                        }
                        await SaveDesignerDraft();

                        await Task.Delay(1500);
                        forceClose = true;
                        dialog.Close();
                    }
                    else
                    {
                        txtStatus.Text = $"Fehler beim Löschen: {resBody}";
                        txtStatus.Foreground = Brushes.Red;
                        progressBar.IsIndeterminate = false;
                        btnDelete.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    txtStatus.Text = $"Netzwerkfehler: {ex.Message}";
                    txtStatus.Foreground = Brushes.Red;
                    progressBar.IsIndeterminate = false;
                    btnDelete.IsEnabled = true;
                }
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
            Background = Scheme.BrushBgPanel2,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnPublish = new Button
        {
            Content = isEditMode ? "Aktualisieren" : "Veröffentlichen",
            Background = Scheme.BrushGlobalBg,
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
                    Background = Scheme.BrushTextNormal3,
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
                    Background = Scheme.BrushDiffHard,
                    Foreground = Brushes.White
                };
                var btnNo = new Button
                {
                    Content = "Nein",
                    Background = Scheme.BrushBgPanel2,
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

            if (txtTitle.Text.Length > 100)
            {
                txtStatus.Text = "Level Name darf maximal 100 Zeichen lang sein!";
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

            // global 60s publish cooldown check
            double secondsSinceLast = (DateTime.Now - _lastLevelPublishTime).TotalSeconds;
            if (secondsSinceLast < 60)
            {
                txtStatus.Text = $"Bitte warte {(int)(60 - secondsSinceLast)} Sekunden vor der nächsten Veröffentlichung.";
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

            txtStatus.Text = "Generiere finale Diagramme...";
            await Task.Delay(200);

            try
            {
                await GenerateDiagramByIndex(0);
                if (!_isSqlMode)
                {
                    for (int i = 0; i < _currentDraft.MaterialDiagrams.Count; i++)
                    {
                        await GenerateDiagramByIndex(i + 1);
                    }
                }
            }
            catch
            {
                // diagrams could not be generated (network/timeout)
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

            int pubVersion = isEditMode ? editVersion + 1 : 1;
            string difficulty = cmbDiff.SelectedItem?.ToString() ?? "Einfach";
            string selectedTags = txtTags.Text ?? "";

            string fullTitle = $"[v{pubVersion}] {txtTitle.Text} by {AppSettings.GithubUsername} - {difficulty}";
            if (!string.IsNullOrWhiteSpace(selectedTags))
            {
                fullTitle += $" | {selectedTags}";
            }

            if (fullTitle.Length > 200)
            {
                txtStatus.Text = $"Titel zu lang ({fullTitle.Length}/200 Zeichen). Kürze den Level-Namen oder wähle weniger Tags.";
                txtStatus.Foreground = Brushes.Orange;
                return;
            }

            string fullBody = encryptedData;

            var proxyPayload = new
            {
                title = fullTitle,
                body = fullBody,
                isSql = _isSqlMode,
                discussionId = isEditMode ? currentDiscussionId : null
            };

            try
            {
                string endpoint = $"{RenderEndpoint(1).TrimEnd('/')}/level";
                var content = new StringContent(JsonSerializer.Serialize(proxyPayload), Encoding.UTF8, "application/json");

                using var requestMessage = new HttpRequestMessage(isEditMode ? HttpMethod.Put : HttpMethod.Post, endpoint);
                requestMessage.Content = content;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

                var resp = await _httpClient.SendAsync(requestMessage);
                string resBody = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && (resBody == "BANNED" || resBody == "PERMA_BANNED"))
                {
                    forceClose = true;
                    dialog.Close();
                    if (resBody == "PERMA_BANNED") ShowPermaBanDialog(); else ShowBanDialog();
                    return;
                }

                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "PUBLISH_LIMIT_REACHED")
                {
                    forceClose = true;
                    dialog.Close();
                    await ShowLimitIncreaseDialog();
                    return;
                }

                if (resp.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    txtStatus.Text = "Zu viele Anfragen. Bitte warte eine Minute.";
                    txtStatus.Foreground = Brushes.Orange;
                    progressBar.IsIndeterminate = false;
                    btnCancel.IsEnabled = true;
                    isPublishing = false;
                    return;
                }

                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(resBody);
                    var node = isEditMode ? "updateDiscussion" : "createDiscussion";
                    var newDiscussionId = doc.RootElement.GetProperty("data").GetProperty(node).GetProperty("discussion").GetProperty("id").GetString();

                    _lastLevelPublishTime = DateTime.Now;

                    // sync to draft
                    if (_isSqlMode)
                    {
                        _currentSqlDraft.DiscussionId = newDiscussionId;
                        _currentSqlDraft.PublishVersion = pubVersion;
                        _currentSqlDraft.PublishDifficulty = difficulty;
                        _currentSqlDraft.PublishTags = selectedTags;
                    }
                    else
                    {
                        _currentDraft.DiscussionId = newDiscussionId;
                        _currentDraft.PublishVersion = pubVersion;
                        _currentDraft.PublishDifficulty = difficulty;
                        _currentDraft.PublishTags = selectedTags;
                    }

                    await SaveDesignerDraft();

                    txtStatus.Text = isEditMode ? "Erfolgreich aktualisiert!" : "Erfolgreich veröffentlicht!";
                    txtStatus.Foreground = Brushes.LightGreen;
                    progressBar.IsIndeterminate = false;
                    progressBar.Value = 100;
                    progressBar.Foreground = Brushes.LightGreen;

                    await Task.Delay(1500);
                    forceClose = true;
                    dialog.Close();
                }
                else
                {
                    txtStatus.Text = $"Fehler vom Server: {resBody}";
                    txtStatus.Foreground = Brushes.Red;
                    progressBar.IsIndeterminate = false;
                    btnCancel.IsEnabled = true;
                    isPublishing = false;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Netzwerkfehler: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
                progressBar.IsIndeterminate = false;
                btnCancel.IsEnabled = true;
                isPublishing = false;
            }
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
        var bytes = Encoding.UTF8.GetBytes(jsonText);
        using var mso = new System.IO.MemoryStream();
        using (var gs = new System.IO.Compression.GZipStream(mso, System.IO.Compression.CompressionMode.Compress, true))
        {
            gs.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(mso.ToArray());
    }

    private async Task ShowLimitIncreaseDialog()
    {
        var dialog = new Window
        {
            Title = "Upload-Limit erreicht",
            Width = 400,
            Height = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushTextNormal3,
            CornerRadius = new CornerRadius(8)
        };

        var stack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Upload-Limit erreicht",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Scheme.BrushDeniedFg
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Du hast das Limit von 5 Leveln erreicht. Möchtest du eine Erhöhung auf 50 Level beantragen? Bitte nenne einen kurzen Grund.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var txtMessage = new TextBox
        {
            Watermark = "Deine Begründung...",
            AcceptsReturn = true,
            MaxHeight = 80,
            Height = 80,
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.White,
            BorderBrush = Scheme.BrushBgPanel5,
            CornerRadius = new CornerRadius(4)
        };
        stack.Children.Add(txtMessage);

        var txtError = new TextBlock
        {
            Foreground = Scheme.BrushDeniedFg,
            FontWeight = FontWeight.SemiBold,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(txtError);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = Scheme.BrushBgPanel2
        };
        var btnSend = new Button
        {
            Content = "Beantragen",
            Background = Scheme.BrushTextHighlight2
        };

        btnCancel.Click += (s, e) => dialog.Close();
        btnSend.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;

            if (txtMessage.Text.Length > 5000)
            {
                dialog.Height = 310;

                txtError.Text = "Die Nachricht darf maximal 5000 Zeichen lang sein.";
                txtError.IsVisible = true;
                return;
            }

            txtError.IsVisible = false;
            btnSend.IsEnabled = false;
            btnSend.Content = "Wird gesendet...";

            var payload = new
            {
                type = "Limit Increase Request",
                user = AppSettings.GithubUsername,
                message = txtMessage.Text
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RenderEndpoint(0));
                requestMessage.Content = content;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                await _httpClient.SendAsync(requestMessage);
            }
            catch { }

            dialog.Close();
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnSend);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;

        await dialog.ShowDialog(this);
    }
}