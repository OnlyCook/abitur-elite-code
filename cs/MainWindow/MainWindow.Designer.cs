using AbiturEliteCode.cs;
using AbiturEliteCode.cs.MainWindow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static AbiturEliteCode.cs.LevelDraft;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void RunSqlDesignerTest()
    {
        PnlSqlOutput.Children.Clear();
        AddSqlOutput("System", "> Starte SQL Validierung...", Brushes.LightGray);

        try
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                // run setup code
                using (var setupCmd = connection.CreateCommand())
                {
                    setupCmd.CommandText = _currentSqlDraft.SetupScript;
                    setupCmd.ExecuteNonQuery();
                }

                // exclude empty input buffers
                var cleanedSchema = _currentSqlDraft.ExpectedSchema.Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .ToList();
                int validCols = cleanedSchema.Count;

                var cleanedResult = new List<string[]>();
                foreach (var r in _currentSqlDraft.ExpectedResult)
                {
                    var rowData = r.Take(validCols).Select(c => c ?? "").ToArray();
                    if (rowData.Any(c => !string.IsNullOrWhiteSpace(c))) cleanedResult.Add(rowData);
                }

                if (validCols == 0)
                    throw new Exception("Die Erwartungstabelle (Expected Table) darf nicht komplett leer sein.");

                // run verification query depending on mode
                DataTable actualDt = null;
                string sampleSolution =
                    SqlLevelTester.ConvertMysqlToSqlite(connection, _currentSqlDraft.SampleSolution);

                if (_currentSqlDraft.IsDmlMode)
                {
                    using (var dmlCmd = connection.CreateCommand())
                    {
                        dmlCmd.CommandText = sampleSolution;
                        dmlCmd.ExecuteNonQuery();
                    }

                    if (string.IsNullOrWhiteSpace(_currentSqlDraft.VerificationQuery))
                        throw new Exception("Im DML Modus muss eine Verifizierungs-Abfrage angegeben werden.");

                    string verifyQuery =
                        SqlLevelTester.ConvertMysqlToSqlite(connection, _currentSqlDraft.VerificationQuery);
                    actualDt = ExecuteDbQuery(connection, verifyQuery);
                }
                else
                {
                    actualDt = ExecuteDbQuery(connection, sampleSolution);
                }

                // always output table
                if (actualDt != null) AddSqlTable(actualDt);

                // compare columns (count and name)
                if (actualDt.Columns.Count != validCols)
                    throw new Exception(
                        $"Spaltenanzahl stimmt nicht überein. Erwartet: {validCols}, Ist: {actualDt.Columns.Count}");

                for (int i = 0; i < validCols; i++)
                    if (!actualDt.Columns[i].ColumnName
                            .Equals(cleanedSchema[i].Name, StringComparison.OrdinalIgnoreCase))
                        throw new Exception(
                            $"Spaltenname an Position {i + 1} stimmt nicht. Erwartet: '{cleanedSchema[i].Name}', Ist: '{actualDt.Columns[i].ColumnName}'");

                // compare row count
                if (actualDt.Rows.Count != cleanedResult.Count)
                    throw new Exception(
                        $"Zeilenanzahl stimmt nicht überein. Erwartet: {cleanedResult.Count}, Ist: {actualDt.Rows.Count}");

                // deep compare values
                for (int r = 0; r < cleanedResult.Count; r++)
                for (int c = 0; c < validCols; c++)
                {
                    string expectedVal = cleanedResult[r][c] ?? "";
                    if (expectedVal == "") expectedVal = "NULL"; // map empty cell to "NULL"

                    string actualVal = actualDt.Rows[r][c]?.ToString()?.Replace(",", ".") ?? "";
                    if (actualDt.Rows[r][c] == DBNull.Value || string.IsNullOrEmpty(actualVal)) actualVal = "NULL";

                    if (double.TryParse(expectedVal, NumberStyles.Any, CultureInfo.InvariantCulture,
                            out double expNum) &&
                        double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double actNum))
                    {
                        if (Math.Abs(expNum - actNum) > 0.0001)
                            throw new Exception(
                                $"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                    }
                    else if (!expectedVal.Equals(actualVal, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception(
                            $"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                    }
                }

                // success (expected matched sample solution identically)
                string json = JsonSerializer.Serialize(_currentSqlDraft);
                _verifiedSqlDraftState = JsonSerializer.Deserialize<SqlLevelDraft>(json);
                _verifiedExpectedSchema = cleanedSchema;
                _verifiedExpectedResult = cleanedResult;

                BtnDesignerExport.IsEnabled = true;
                TxtDesignerStatus.Text = "Bereit zum Export";
                AddSqlOutput("System",
                    "✓ DESIGNER TEST BESTANDEN! Die Musterlösung erzeugt exakt das erwartete Ergebnis.",
                    Brushes.LightGreen);

                AddSqlTable(actualDt);
            }
        }
        catch (Exception ex)
        {
            AddSqlOutput("Error", $"❌ VALIDIERUNG FEHLGESCHLAGEN:\n{ex.Message}", Brushes.Orange);
            BtnDesignerExport.IsEnabled = false;
            TxtDesignerStatus.Text = "Entwurf geändert (Nicht verifiziert)";
            _verifiedSqlDraftState = null;
        }
    }

    private void SyncEditorToDesigner()
    {
        if (!_isDesignerMode || _activeDesignerSource == DesignerSource.None) return;

        if (_activeDesignerSource == DesignerSource.StarterCode)
            TxtDesignStarter.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.Validation)
            TxtDesignValidation.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.TestingCode)
            TxtDesignTesting.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.SqlSetup)
            TxtDesignSqlSetup.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.SqlVerify)
            TxtDesignSqlVerify.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.SqlSample)
            TxtDesignSqlSample.Text = ActiveEditor.Text;
    }

    private void OnDesignerInputChanged(object sender, EventArgs e)
    {
        if (_isLoadingDesigner) return;

        if (_isSqlMode)
        {
            // sql specific fields
            _currentSqlDraft.Name = TxtDesignName.Text ?? "Neues SQL Level";
            _currentSqlDraft.Author = TxtDesignAuthor.Text ?? "Unbekannt";
            _currentSqlDraft.Description = TxtDesignDesc.Text ?? "";
            _currentSqlDraft.Materials = TxtDesignMaterials.Text ?? "";
            _currentSqlDraft.SetupScript = TxtDesignSqlSetup.Text ?? "";
            _currentSqlDraft.VerificationQuery = TxtDesignSqlVerify.Text ?? "";
            _currentSqlDraft.SampleSolution = TxtDesignSqlSample.Text ?? "";

            BtnDesignerExport.IsEnabled = false;
            TxtDesignerStatus.Text = "Entwurf geändert (Nicht verifiziert)";
            _verifiedSqlDraftState = null;

            if (_isDesignerMode && AppSettings.IsSqlAutocompleteEnabled)
            {
                string combinedText =
                    $"{TxtDesignSqlSetup.Text}\n{TxtDesignSqlSample.Text}\n{TxtDesignSqlVerify.Text}\n{SqlQueryEditor.Text}";
                _sqlAutocompleteService?.ScanTokens(combinedText);
            }
        }
        else
        {
            // c# specific fields
            _currentDraft.Name = TxtDesignName.Text ?? "Neues Level";
            _currentDraft.Author = TxtDesignAuthor.Text ?? "Unbekannt";
            _currentDraft.Description = TxtDesignDesc.Text ?? "";
            _currentDraft.Materials = TxtDesignMaterials.Text ?? "";
            _currentDraft.StarterCode = TxtDesignStarter.Text ?? "";
            _currentDraft.TestCode = TxtDesignTesting.Text ?? "";
            _currentDraft.ValidationCode = TxtDesignValidation.Text ?? "";
        }

        // trigger autosave
        _designerAutoSaveTimer.Stop();
        _designerAutoSaveTimer.Start();
    }

    private void ToggleDesignerMode(bool enable, string draftPath = "")
    {
        _isDesignerMode = enable;
        _activeDesignerSource = DesignerSource.None;

        // reset icons
        UpdateDesignerButtons();

        TabDesigner.IsVisible = enable;
        BtnExitDesigner.IsVisible = enable;
        BtnLevelSelect.IsVisible = !enable;

        if (enable)
        {
            BtnPrevLevel.IsVisible = false;
            BtnNextLevel.IsVisible = false;
        }

        BtnModeSwitch.IsVisible = !enable;
        BtnSave.IsVisible = !enable;
        BtnReset.IsVisible = !enable;
        BtnRun.IsVisible = !enable;

        PnlDesignStarter.IsVisible = !_isSqlMode;
        PnlDesignTesting.IsVisible = !_isSqlMode;
        PnlDesignValidation.IsVisible = !_isSqlMode;
        PnlDesignSqlSetup.IsVisible = _isSqlMode;
        PnlDesignSqlMode.IsVisible = _isSqlMode;
        PnlDesignExpectedTable.IsVisible = _isSqlMode;
        PnlDesignSqlVerify.IsVisible = _isSqlMode;
        PnlDesignSqlSample.IsVisible = _isSqlMode;

        if (enable)
        {
            _isLoadingDesigner = true;

            // clear designated consoles + editors
            if (_isSqlMode)
            {
                SqlQueryEditor.Text = "";
                PnlSqlOutput.Children.Clear();
                AddSqlOutput("System", "> SQL Level Designer geladen. Wähle einen Bereich zum Bearbeiten.",
                    Brushes.LightGray);
            }
            else
            {
                CodeEditor.Text = "";
                TxtConsole.Inlines?.Clear();
                AddToConsole("> C# Level Designer geladen. Wähle einen Bereich zum Bearbeiten.", Brushes.LightGray);
            }

            _originalSyntaxSetting = _isSqlMode
                ? AppSettings.IsSqlSyntaxHighlightingEnabled
                : AppSettings.IsSyntaxHighlightingEnabled;

            if (_isSqlMode)
            {
                AppSettings.IsSqlSyntaxHighlightingEnabled = true;
                ApplySqlSyntaxHighlighting();
            }
            else
            {
                AppSettings.IsSyntaxHighlightingEnabled = true;
                ApplySyntaxHighlighting();
            }

            _currentDraftPath = draftPath;
            if (!string.IsNullOrEmpty(draftPath))
            {
                if (_isSqlMode)
                    _currentSqlDraft = SqlLevelDesigner.LoadDraft(draftPath);
                else
                    _currentDraft = LevelDesigner.LoadDraft(draftPath);
            }

            if (_isSqlMode)
            {
                // populate sql data
                TxtDesignName.Text = _currentSqlDraft.Name;
                TxtDesignAuthor.Text = _currentSqlDraft.Author;
                TxtDesignDesc.Text = _currentSqlDraft.Description;
                TxtDesignMaterials.Text = _currentSqlDraft.Materials;
                TxtDesignSqlSetup.Text = _currentSqlDraft.SetupScript;
                TxtDesignSqlVerify.Text = _currentSqlDraft.VerificationQuery;
                TxtDesignSqlSample.Text = _currentSqlDraft.SampleSolution;

                TxtPrereqTitle.Text = "Vorraussetzungen / Grundlagen";
                TxtDesignPrereqInput.Watermark = "z.B. SELECT...";

                CmbSqlValidationMode.SelectedIndex = _currentSqlDraft.IsDmlMode ? 1 : 0;
                PnlDesignSqlVerify.IsVisible = _currentSqlDraft.IsDmlMode;
                RenderExpectedTable();

                _currentRelationalModel.Clear();
                if (_currentSqlDraft.InitialRelationalModel != null &&
                    _currentSqlDraft.InitialRelationalModel.Count > 0)
                {
                    string json = JsonSerializer.Serialize(_currentSqlDraft.InitialRelationalModel);
                    _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(json) ?? new List<RTable>();
                }

                if (PnlTaskRelationalModel != null)
                {
                    PnlTaskRelationalModel.Children.Clear();
                    RenderRelationalModel(PnlTaskRelationalModel, false);
                }

                if (PnlUmlRelationalModel != null) PnlUmlRelationalModel.Children.Clear();

                UpdateSqlAutocompleteSchema();
                if (AppSettings.IsSqlAutocompleteEnabled)
                {
                    // scan all relevant boxes immediately to prevent standard level memory leaks
                    string combinedText =
                        $"{TxtDesignSqlSetup.Text}\n{TxtDesignSqlSample.Text}\n{TxtDesignSqlVerify.Text}\n{SqlQueryEditor.Text}";
                    _sqlAutocompleteService?.ScanTokens(combinedText);
                }
            }
            else
            {
                // populate c# data
                TxtDesignName.Text = _currentDraft.Name;
                TxtDesignAuthor.Text = _currentDraft.Author;
                TxtDesignDesc.Text = _currentDraft.Description;
                TxtDesignMaterials.Text = _currentDraft.Materials;
                TxtDesignStarter.Text = _currentDraft.StarterCode;
                TxtDesignTesting.Text = _currentDraft.TestCode;
                TxtDesignValidation.Text = _currentDraft.ValidationCode;

                // restore c# prerequisites ui text
                TxtPrereqTitle.Text = "Voraussetzungen / Grundlagen (Suche auf Englisch)";
                TxtDesignPrereqInput.Watermark = "z.B. If statements...";
            }

            DiscordRpcManager.ResetTimer();
            DiscordRpcManager.UpdatePresence("C# Level Designer", "Creating their own level", "aec_app_icon", "Custom");

            LoadDiagramContentToUI();
            RenderDesignerPrereqList();

            // update designer diagrams tabs and preview immediately on entry
            UpdateDesignerDiagramTabs();
            UpdateDesignerPreview();

            _isLoadingDesigner = false;
        }
        else
        {
            if (_isSqlMode)
            {
                AppSettings.IsSqlSyntaxHighlightingEnabled = _originalSyntaxSetting;
                ApplySqlSyntaxHighlighting();

                MainTabs.SelectedItem = MainTabs.Items.OfType<TabItem>().First();
                BtnSave.IsVisible = true;
                BtnReset.IsVisible = true;
                BtnRun.IsVisible = true;

                currentSqlLevel = null;
                int maxId = playerData.UnlockedSqlLevelIds.Count > 0 ? playerData.UnlockedSqlLevelIds.Max() : 1;
                var startLevel = sqlLevels.FirstOrDefault(l => l.Id == maxId) ?? sqlLevels[0];
                LoadSqlLevel(startLevel);
            }
            else
            {
                AppSettings.IsSyntaxHighlightingEnabled = _originalSyntaxSetting;
                AppSettings.IsErrorHighlightingEnabled = false;
                ApplySyntaxHighlighting();

                _diagnosticTimer.Stop();
                ClearDiagnostics();
                _textMarkerService.Clear();

                MainTabs.SelectedItem = MainTabs.Items.OfType<TabItem>().First();
                BtnSave.IsVisible = true;
                BtnReset.IsVisible = true;
                BtnRun.IsVisible = true;

                currentLevel = null; // prevent writing over standard level code saves
                int maxId = playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1;
                var startLevel = levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0];
                LoadLevel(startLevel);
            }
        }
    }

    private bool HasUnsavedDesignerChanges()
    {
        UpdateDraftFromUI();
        string currentJson =
            _isSqlMode ? JsonSerializer.Serialize(_currentSqlDraft) : JsonSerializer.Serialize(_currentDraft);
        return currentJson != _lastSavedDraftJson;
    }

    private async void BtnExitDesigner_Click(object sender, RoutedEventArgs e)
    {
        if (_designerSyncTimer.IsEnabled)
        {
            _designerSyncTimer.Stop();
            SyncEditorToDesigner();
        }

        if (HasUnsavedDesignerChanges())
        {
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

            var dGrid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };
            dGrid.Children.Add(new TextBlock
            {
                Text = "Du hast ungespeicherte Änderungen im Designer. Möchtest du speichern?",
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

            btnSaveClose.Click += async (_, __) =>
            {
                dialog.Close();
                await SaveDesignerDraft();
                ToggleDesignerMode(false);
            };

            btnDiscard.Click += (_, __) =>
            {
                dialog.Close();
                ToggleDesignerMode(false);
            };

            btnCancel.Click += (_, __) => dialog.Close();

            dBtnPanel.Children.Add(btnCancel);
            dBtnPanel.Children.Add(btnDiscard);
            dBtnPanel.Children.Add(btnSaveClose);

            dGrid.Children.Add(dBtnPanel);
            dialog.Content = dGrid;

            await dialog.ShowDialog(this);
        }
        else
        {
            ToggleDesignerMode(false);
        }
    }

    private void BtnDesignerRefresh_Click(object sender, RoutedEventArgs e)
    {
        UpdateDesignerPreview();
        AddToConsole("\n> Vorschau aktualisiert.", Brushes.LightGray);
    }

    private async void BtnDesignerSave_Click(object sender, RoutedEventArgs e)
    {
        await SaveDesignerDraft();
        AddToConsole("\n> Entwurf gespeichert.", Brushes.LightGreen);
    }

    private void BtnDesignerGuide_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string url = _isSqlMode
                ? "https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_LEVEL_DESIGNER_GUIDE"
                : "https://github.com/OnlyCook/abitur-elite-code/wiki/CS_LEVEL_DESIGNER_GUIDE";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
        }
        catch (Exception ex)
        {
            AddToConsole($"\n> Fehler beim Öffnen des Guides: {ex.Message}", Brushes.Orange);
        }
    }

    private async void BtnDesignerExport_Click(object sender, RoutedEventArgs e)
    {
        if (_isSqlMode)
        {
            if ((_currentSqlDraft.Name?.Length ?? 0) < 1 || (_currentSqlDraft.Author?.Length ?? 0) < 1)
            {
                AddSqlOutput("Error", "⚠ Export abgelehnt: 'Level Name' und 'Autor' müssen gesetzt sein.",
                    Brushes.Orange);
                return;
            }

            if (_verifiedSqlDraftState == null)
            {
                AddSqlOutput("Error", "❌ Fehler: Level muss vor dem Export erfolgreich getestet werden.", Brushes.Red);
                return;
            }

            AddSqlOutput("System", "> Generiere Diagramm für finalen Export...", Brushes.LightGray);
            BtnDesignerExport.IsEnabled = false;

            bool mainSuccess2 = await GenerateDiagramByIndex(0);

            if (!mainSuccess2)
                AddSqlOutput("System", "⚠ Hinweis: Diagramm konnte nicht aktualisiert werden.", Brushes.Orange);

            _verifiedSqlDraftState.PlantUmlSource = _currentSqlDraft.PlantUmlSource;
            _verifiedSqlDraftState.PlantUmlSvgContent = _currentSqlDraft.PlantUmlSvgContent;

            SqlLevelDesigner.ExportLevel(_currentDraftPath, _verifiedSqlDraftState, _verifiedExpectedSchema,
                _verifiedExpectedResult);
            AddSqlOutput("System", "> Level erfolgreich exportiert! (.eliteslvl)", Brushes.LightGreen);
            TxtDesignerStatus.Text = "Exportiert";
            BtnDesignerExport.IsEnabled = true;
            return;
        }

        if ((_currentDraft.Name?.Length ?? 0) < 1 || (_currentDraft.Author?.Length ?? 0) < 1)
        {
            AddToConsole("\n⚠ Export abgelehnt: 'Level Name' und 'Autor' müssen gesetzt sein.", Brushes.Orange);
            return;
        }

        if (_verifiedDraftState == null)
        {
            AddToConsole("\n❌ Fehler: Level muss vor dem Export erfolgreich getestet werden.", Brushes.Red);
            return;
        }

        AddToConsole("\n> Generiere alle Diagramme für finalen Export...", Brushes.LightGray);
        BtnDesignerExport.IsEnabled = false;

        // generate main diagram (index 0)
        bool mainSuccess = await GenerateDiagramByIndex(0);

        // generate material diagrams
        for (int i = 0; i < _currentDraft.MaterialDiagrams.Count; i++)
        {
            bool matSuccess = await GenerateDiagramByIndex(i + 1);
            if (!matSuccess) mainSuccess = false;
        }

        if (!mainSuccess)
            AddToConsole("\n⚠ Hinweis: Einige Diagramme konnten nicht aktualisiert werden.", Brushes.Orange);

        _verifiedDraftState.PlantUmlSources = new List<string>(_currentDraft.PlantUmlSources);
        _verifiedDraftState.PlantUmlSvgContents = new List<string>(_currentDraft.PlantUmlSvgContents);

        _verifiedDraftState.MaterialDiagrams = new List<LevelDraft.DiagramData>();
        foreach (var md in _currentDraft.MaterialDiagrams)
            _verifiedDraftState.MaterialDiagrams.Add(new LevelDraft.DiagramData
            {
                Name = md.Name,
                PlantUmlSource = md.PlantUmlSource,
                PlantUmlSvgContent = md.PlantUmlSvgContent
            });

        LevelDesigner.ExportLevel(_currentDraftPath, _verifiedDraftState);
        AddToConsole("\n> Level erfolgreich exportiert! (.elitelvl)", Brushes.LightGreen);
        TxtDesignerStatus.Text = "Exportiert";
        BtnDesignerExport.IsEnabled = true;
    }

    private void SwitchDesignerMode(DesignerSource source, TextBox targetBox, string enterMessage)
    {
        if (_activeDesignerSource == source)
        {
            targetBox.Text = ActiveEditor.Text;
            _activeDesignerSource = DesignerSource.None;
            ActiveEditor.Text = "";

            if (_isSqlMode)
                AddSqlOutput("System", "> Editor geleert. Wähle eine Datei zum Bearbeiten.", Brushes.LightGray);
            else AddToConsole("\n> Editor geleert. Wähle eine Datei zum Bearbeiten.", Brushes.LightGray);
        }
        else
        {
            SaveActiveDesignerSource();

            _activeDesignerSource = source;
            ActiveEditor.Text = targetBox.Text;
            ActiveEditor.Focus();

            if (_isSqlMode) AddSqlOutput("System", enterMessage, Brushes.LightGray);
            else AddToConsole(enterMessage, Brushes.LightGray);
        }

        UpdateDesignerButtons();
    }

    private void SaveActiveDesignerSource()
    {
        if (_activeDesignerSource == DesignerSource.StarterCode)
            TxtDesignStarter.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.Validation)
            TxtDesignValidation.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.TestingCode)
            TxtDesignTesting.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.SqlSetup)
            TxtDesignSqlSetup.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.SqlVerify)
            TxtDesignSqlVerify.Text = ActiveEditor.Text;
        else if (_activeDesignerSource == DesignerSource.SqlSample)
            TxtDesignSqlSample.Text = ActiveEditor.Text;
    }

    private void UpdateDesignerButtons()
    {
        void SetIcon(Button btn, string iconName)
        {
            if (btn != null) btn.Content = LoadIcon($"assets/icons/{iconName}", 16);
        }

        SetIcon(BtnEditStarter, "ic_move.svg");
        SetIcon(BtnEditValidation, "ic_move.svg");
        SetIcon(BtnEditTesting, "ic_move.svg");
        SetIcon(BtnEditSqlSetup, "ic_move.svg");
        SetIcon(BtnEditSqlVerify, "ic_move.svg");
        SetIcon(BtnEditSqlSample, "ic_move.svg");

        BtnRun.IsVisible = _activeDesignerSource == DesignerSource.TestingCode ||
                           _activeDesignerSource == DesignerSource.SqlExpected ||
                           _activeDesignerSource == DesignerSource.SqlSample;

        if (_activeDesignerSource == DesignerSource.StarterCode) SetIcon(BtnEditStarter, "ic_exit.svg");
        else if (_activeDesignerSource == DesignerSource.Validation) SetIcon(BtnEditValidation, "ic_exit.svg");
        else if (_activeDesignerSource == DesignerSource.TestingCode) SetIcon(BtnEditTesting, "ic_exit.svg");
        else if (_activeDesignerSource == DesignerSource.SqlSetup) SetIcon(BtnEditSqlSetup, "ic_exit.svg");
        else if (_activeDesignerSource == DesignerSource.SqlVerify) SetIcon(BtnEditSqlVerify, "ic_exit.svg");
        else if (_activeDesignerSource == DesignerSource.SqlSample) SetIcon(BtnEditSqlSample, "ic_exit.svg");

        if (AppSettings.IsErrorHighlightingEnabled && !_isSqlMode) UpdateDiagnostics();
    }

    private async Task SaveDesignerDraft()
    {
        if (!_isDesignerMode || string.IsNullOrEmpty(_currentDraftPath)) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_designerSyncTimer.IsEnabled)
            {
                _designerSyncTimer.Stop();
                SyncEditorToDesigner();
            }

            UpdateDraftFromUI();
            TxtDesignerStatus.Text = "Speichere...";
        });

        if (_isSqlMode)
        {
            await SqlLevelDesigner.SaveDraftAsync(_currentDraftPath, _currentSqlDraft);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _lastSavedDraftJson = JsonSerializer.Serialize(_currentSqlDraft);
                TxtDesignerStatus.Text = "Gespeichert";
            });
        }
        else
        {
            await LevelDesigner.SaveDraftAsync(_currentDraftPath, _currentDraft);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _lastSavedDraftJson = JsonSerializer.Serialize(_currentDraft);
                TxtDesignerStatus.Text = "Gespeichert";
            });
        }
    }

    private void UpdateDesignerPreview()
    {
        UpdateDraftFromUI();

        PnlTask.Children.Clear();
        PnlMaterials.Children.Clear();

        string draftName = _isSqlMode ? _currentSqlDraft.Name : _currentDraft.Name;
        string draftAuthor = _isSqlMode ? _currentSqlDraft.Author : _currentDraft.Author;
        string draftDesc = _isSqlMode ? _currentSqlDraft.Description : _currentDraft.Description;
        string draftMaterials = _isSqlMode ? _currentSqlDraft.Materials : _currentDraft.Materials;
        List<string> draftPrereqs = _isSqlMode ? _currentSqlDraft.Prerequisites : _currentDraft.Prerequisites;

        PnlTask.Children.Add(new SelectableTextBlock
        {
            Text = draftName,
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = BrushTextNormal,
            Margin = new Thickness(0)
        });

        if (!string.IsNullOrWhiteSpace(draftAuthor))
        {
            PnlTask.Children.Add(new SelectableTextBlock
            {
                Text = $"von {draftAuthor}",
                FontSize = 14,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 20)
            });
        }
        else // fallback
        {
            if (PnlTask.Children.Last() is Control last) last.Margin = new Thickness(0, 0, 0, 20);
        }

        RenderRichText(PnlTask, draftDesc);

        List<string> draftSvgs = new List<string>();
        if (!_isSqlMode) draftSvgs = _currentDraft.MaterialDiagrams.Select(d => d.PlantUmlSvgContent).ToList();

        var dummyLevel = new Level
        {
            MaterialDocs = draftMaterials,
            Prerequisites = draftPrereqs
        };

        GenerateMaterials(dummyLevel, draftSvgs);

        // update diagram preview if sql mode or main diagram active
        if (_isSqlMode)
        {
            if (!string.IsNullOrEmpty(_currentSqlDraft.PlantUmlSvgContent))
            {
                ImgDiagram.Source = LoadSvgFromString(_currentSqlDraft.PlantUmlSvgContent);
                TxtNoDiagram.IsVisible = false;
            }
            else
            {
                ImgDiagram.Source = null;
                TxtNoDiagram.IsVisible = true;
            }
        }
        else
        {
            if (_currentDraft.PlantUmlSvgContents != null && _currentDraft.PlantUmlSvgContents.Count > 0 &&
                !string.IsNullOrEmpty(_currentDraft.PlantUmlSvgContents[0]))
            {
                ImgDiagram.Source = LoadSvgFromString(_currentDraft.PlantUmlSvgContents[0]);
                TxtNoDiagram.IsVisible = false;
            }
            else
            {
                ImgDiagram.Source = null;
                TxtNoDiagram.IsVisible = true;
            }
        }
    }

    private void AddDesignerPrerequisite(string topic)
    {
        var prereqs = _isSqlMode ? _currentSqlDraft.Prerequisites : _currentDraft.Prerequisites;

        if (prereqs.Count >= MaxPrerequisites)
        {
            if (_isSqlMode)
                AddSqlOutput("System", $"> Limit erreicht (Max {MaxPrerequisites} Voraussetzungen).", Brushes.Orange);
            else AddToConsole($"\n> Limit erreicht (Max {MaxPrerequisites} Voraussetzungen).", Brushes.Orange);
            return;
        }

        if (!prereqs.Contains(topic))
        {
            prereqs.Add(topic);
            RenderDesignerPrereqList();
            OnDesignerInputChanged(this, EventArgs.Empty);
        }
    }

    private void RemoveDesignerPrerequisite(string topic)
    {
        var prereqs = _isSqlMode ? _currentSqlDraft.Prerequisites : _currentDraft.Prerequisites;

        if (prereqs.Contains(topic))
        {
            prereqs.Remove(topic);
            RenderDesignerPrereqList();
            OnDesignerInputChanged(this, EventArgs.Empty);
        }
    }

    private void RenderDesignerPrereqList()
    {
        PnlDesignPrereqsList.Children.Clear();
        var prereqs = _isSqlMode ? _currentSqlDraft.Prerequisites : _currentDraft.Prerequisites;

        foreach (var item in prereqs)
        {
            var container = new Border
            {
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(2)
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto")
            };

            var txt = new TextBlock
            {
                Text = "• " + item,
                Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 5)
            };

            var btnDelete = new Button
            {
                Content = LoadIcon("assets/icons/ic_delete.svg", 14),
                Background = Brushes.Transparent,
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(4),
                Cursor = Cursor.Parse("Hand"),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnDelete, "Entfernen");

            string itemClosure = item;
            btnDelete.Click += (s, e) => RemoveDesignerPrerequisite(itemClosure);

            Grid.SetColumn(txt, 0);
            Grid.SetColumn(btnDelete, 1);

            row.Children.Add(txt);
            row.Children.Add(btnDelete);

            container.Child = row;
            PnlDesignPrereqsList.Children.Add(container);
        }
    }

    private void UpdateDesignerDiagramTabs()
    {
        PnlDesignerDiagramTabs.Children.Clear();

        Button CreateTab(string title, int index, bool isActive)
        {
            var btn = new Button
            {
                Content = title,
                Background = isActive ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#333"),
                Foreground = Brushes.White,
                FontSize = 13,
                Padding = new Thickness(10, 5),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursor.Parse("Hand")
            };
            btn.Click += (s, e) => SwitchDesignerDiagramTab(index);
            return btn;
        }

        PnlDesignerDiagramTabs.Children.Add(CreateTab("Haupt", 0, _activeDiagramIndex == 0));

        if (!_isSqlMode)
        {
            for (int i = 0; i < _currentDraft.MaterialDiagrams.Count; i++)
                PnlDesignerDiagramTabs.Children.Add(CreateTab($"Mat {i + 1}", i + 1, _activeDiagramIndex == i + 1));

            BtnAddDiagramTab.IsVisible = _currentDraft.MaterialDiagrams.Count < 3;
            BtnDeleteDiagramTab.IsVisible = _activeDiagramIndex > 0;
        }
        else
        {
            BtnAddDiagramTab.IsVisible = false;
            BtnDeleteDiagramTab.IsVisible = false;
        }
    }

    private void SwitchDesignerDiagramTab(int newIndex)
    {
        if (newIndex == _activeDiagramIndex) return;

        SaveCurrentDiagramContent();

        _activeDiagramIndex = newIndex;

        LoadDiagramContentToUI();
        UpdateDesignerDiagramTabs();
    }

    private void BtnEditSqlSetup_Click(object sender, RoutedEventArgs e)
    {
        SwitchDesignerMode(DesignerSource.SqlSetup, TxtDesignSqlSetup,
            "> Editor: Setup-Script geladen (Ausführen deaktiviert).");
    }

    private void BtnEditSqlVerify_Click(object sender, RoutedEventArgs e)
    {
        SwitchDesignerMode(DesignerSource.SqlVerify, TxtDesignSqlVerify,
            "> Editor: Verifizierungs-Abfrage geladen (Ausführen deaktiviert).");
    }

    private void InvalidateSqlExport()
    {
        _designerAutoSaveTimer.Stop();
        _designerAutoSaveTimer.Start();
        BtnDesignerExport.IsEnabled = false;
        TxtDesignerStatus.Text = "Entwurf geändert (Nicht verifiziert)";
        _verifiedSqlDraftState = null;
    }

    private void BtnEditSqlSample_Click(object sender, RoutedEventArgs e)
    {
        SwitchDesignerMode(DesignerSource.SqlSample, TxtDesignSqlSample,
            "> Editor: Musterlösung geladen. 'Ausführen' verifiziert nun das Level.");
    }

    private async Task<bool> GenerateDiagramByIndex(int index)
    {
        string source = "";
        if (_isSqlMode)
        {
            if (index == 0) source = _currentSqlDraft.PlantUmlSource;
            else return true; // sql only has single main diagram
        }
        else
        {
            if (index == 0) source = _currentDraft.PlantUmlSources.Count > 0 ? _currentDraft.PlantUmlSources[0] : "";
            else source = _currentDraft.MaterialDiagrams[index - 1].PlantUmlSource;
        }

        if (string.IsNullOrWhiteSpace(source)) return true;

        try
        {
            string prepared = PreparePlantUmlSource(source);
            string svg = await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);

            if (_isSqlMode)
            {
                _currentSqlDraft.PlantUmlSvgContent = svg;
            }
            else
            {
                if (index == 0)
                {
                    while (_currentDraft.PlantUmlSvgContents.Count <= 0) _currentDraft.PlantUmlSvgContents.Add("");
                    _currentDraft.PlantUmlSvgContents[0] = svg;
                }
                else
                {
                    _currentDraft.MaterialDiagrams[index - 1].PlantUmlSvgContent = svg;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }


    private void BtnEditStarter_Click(object sender, RoutedEventArgs e)
    {
        SwitchDesignerMode(DesignerSource.StarterCode, TxtDesignStarter,
            "\n> Editor: Starter Code geladen (Ausführen deaktiviert).");
    }

    private void BtnEditValidation_Click(object sender, RoutedEventArgs e)
    {
        SwitchDesignerMode(DesignerSource.Validation, TxtDesignValidation,
            "\n> Editor: Validierungs-Code geladen (Ausführen deaktiviert).");
    }

    private void BtnEditTesting_Click(object sender, RoutedEventArgs e)
    {
        SwitchDesignerMode(DesignerSource.TestingCode, TxtDesignTesting,
            "\n> Editor: Test-Code geladen. 'Ausführen' jetzt verfügbar.");
    }

    private void BtnResetValidation_Click(object sender, RoutedEventArgs e)
    {
        string defaultVal =
            "private static bool ValidateLevel(Assembly assembly, out string feedback)\n{\n    feedback = \"Gut gemacht!\";\n    return true;\n}";
        TxtDesignValidation.Text = defaultVal;

        if (_activeDesignerSource == DesignerSource.Validation) CodeEditor.Text = defaultVal;
        AddToConsole("\n> Validierungs-Code auf Standard zurückgesetzt.", Brushes.LightGray);
    }

    private void UpdateDraftFromUI()
    {
        if (_isSqlMode)
        {
            _currentSqlDraft.Name = TxtDesignName.Text;
            _currentSqlDraft.Author = TxtDesignAuthor.Text;
            _currentSqlDraft.Description = TxtDesignDesc.Text;
            _currentSqlDraft.Materials = TxtDesignMaterials.Text;

            SaveCurrentDiagramContent();

            if (_activeDesignerSource == DesignerSource.SqlSetup) _currentSqlDraft.SetupScript = SqlQueryEditor.Text;
            else _currentSqlDraft.SetupScript = TxtDesignSqlSetup.Text;

            if (_activeDesignerSource == DesignerSource.SqlVerify)
                _currentSqlDraft.VerificationQuery = SqlQueryEditor.Text;
            else _currentSqlDraft.VerificationQuery = TxtDesignSqlVerify.Text;

            if (_activeDesignerSource == DesignerSource.SqlSample)
                _currentSqlDraft.SampleSolution = SqlQueryEditor.Text;
            else _currentSqlDraft.SampleSolution = TxtDesignSqlSample.Text;

            // initialize relational model
            _currentSqlDraft.InitialRelationalModel = _currentRelationalModel.Select(t => new RTable
            {
                Name = t.Name,
                Columns = t.Columns.Select(c => new RColumn { Name = c.Name, IsPk = c.IsPk, IsFk = c.IsFk }).ToList()
            }).ToList();
        }
        else
        {
            _currentDraft.Name = TxtDesignName.Text;
            _currentDraft.Author = TxtDesignAuthor.Text;
            _currentDraft.Description = TxtDesignDesc.Text;
            _currentDraft.Materials = TxtDesignMaterials.Text;

            SaveCurrentDiagramContent();

            if (_activeDesignerSource == DesignerSource.StarterCode) _currentDraft.StarterCode = CodeEditor.Text;
            else _currentDraft.StarterCode = TxtDesignStarter.Text;

            if (_activeDesignerSource == DesignerSource.Validation) _currentDraft.ValidationCode = CodeEditor.Text;
            else _currentDraft.ValidationCode = TxtDesignValidation.Text;

            if (_activeDesignerSource == DesignerSource.TestingCode) _currentDraft.TestCode = CodeEditor.Text;
            else _currentDraft.TestCode = TxtDesignTesting.Text;
        }
    }

    private async void BtnGenerateUml_Click(object sender, RoutedEventArgs e)
    {
        await GeneratePlantUmlDiagram();
    }

    private void BtnWebUml_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://www.plantuml.com/plantuml/duml/SoWkIImgAStDuNBAJrBGjLDmpCbCJbMmKiX8pSd9vt98pKi1IW80";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private string PreparePlantUmlSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";

        // clipping fix
        source = Regex.Replace(source, @"(?m)^(\s*[-+#].*?)$", "$1 ");

        // static fix: replace "{static} method()" with unicode underlined text
        source = Regex.Replace(source, @"^(\s*[-+#])\s*\{static\}\s*(.+)$", m =>
        {
            string prefix = m.Groups[1].Value;
            string content = m.Groups[2].Value;
            return $"{prefix} {ConvertToUnicodeUnderline(content)}";
        }, RegexOptions.Multiline);

        // add theme attributes if missing
        if (!source.Contains("skinparam backgroundcolor transparent"))
        {
            var lines = source.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            bool inserted = false;
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("@startuml") || trimmed.StartsWith("@startchen") ||
                    trimmed.StartsWith("@starter"))
                {
                    lines.Insert(i + 1, "skinparam backgroundcolor transparent");
                    if (trimmed.StartsWith("@startuml") && !source.Contains("skinparam classAttributeIconSize 0"))
                        lines.Insert(i + 2, "skinparam classAttributeIconSize 0");
                    inserted = true;
                    break;
                }
            }

            if (!inserted) // fallback
            {
                lines.Insert(0, "@startuml");
                lines.Insert(1, "skinparam backgroundcolor transparent");
                lines.Insert(2, "skinparam classAttributeIconSize 0");
                lines.Add("@enduml");
            }

            source = string.Join("\n", lines);
        }

        return source;
    }

    private string ConvertToUnicodeUnderline(string input)
    {
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            sb.Append(c);
            sb.Append('\u0332'); // "Combining Low Line"
        }

        return sb.ToString();
    }

    private async Task<bool> GeneratePlantUmlDiagram()
    {
        string rawCode = TxtDesignPlantUml.Text;

        if (string.IsNullOrWhiteSpace(rawCode))
        {
            if (_isSqlMode)
            {
                _currentSqlDraft.PlantUmlSource = "";
                _currentSqlDraft.PlantUmlSvgContent = "";
            }
            else
            {
                if (_activeDiagramIndex == 0 && _currentDraft.PlantUmlSources.Count > 0)
                {
                    _currentDraft.PlantUmlSources[0] = "";
                    if (_currentDraft.PlantUmlSvgContents.Count > 0) _currentDraft.PlantUmlSvgContents[0] = "";
                }
            }

            ImgDiagram.Source = null;
            return true;
        }

        string preparedCode = PreparePlantUmlSource(rawCode);

        if (_isSqlMode) AddSqlOutput("System", "> Sende Anfrage an PlantUML Server...", Brushes.LightGray);
        else AddToConsole("\n> Sende Anfrage an PlantUML Server...", Brushes.LightGray);

        try
        {
            string svgContent = await PlantUmlHelper.GenerateSvgFromCodeAsync(preparedCode);

            if (_isSqlMode)
            {
                _currentSqlDraft.PlantUmlSource = rawCode;
                _currentSqlDraft.PlantUmlSvgContent = svgContent;
                ImgDiagram.Source = LoadSvgFromString(svgContent);
            }
            else
            {
                if (_activeDiagramIndex == 0)
                {
                    if (_currentDraft.PlantUmlSources.Count == 0) _currentDraft.PlantUmlSources.Add("");
                    if (_currentDraft.PlantUmlSvgContents.Count == 0) _currentDraft.PlantUmlSvgContents.Add("");

                    _currentDraft.PlantUmlSources[0] = rawCode;
                    _currentDraft.PlantUmlSvgContents[0] = svgContent;

                    ImgDiagram.Source = LoadSvgFromString(svgContent);
                }
                else
                {
                    var d = _currentDraft.MaterialDiagrams[_activeDiagramIndex - 1];
                    d.PlantUmlSource = rawCode;
                    d.PlantUmlSvgContent = svgContent;

                    UpdateDesignerPreview();
                }
            }

            if (_isSqlMode) AddSqlOutput("System", "> Diagramm erfolgreich aktualisiert.", Brushes.LightGreen);
            else AddToConsole("\n> Diagramm erfolgreich aktualisiert.", Brushes.LightGreen);

            return true;
        }
        catch (Exception ex)
        {
            if (_isSqlMode) AddSqlOutput("Error", $"> Fehler bei Diagramm-Erstellung: {ex.Message}", Brushes.Orange);
            else AddToConsole($"\n> Fehler bei Diagramm-Erstellung: {ex.Message}", Brushes.Orange);
            return false;
        }
    }

    private IImage LoadSvgFromString(string svgContent)
    {
        if (string.IsNullOrEmpty(svgContent)) return null;
        try
        {
            // clean svg header
            int svgIndex = svgContent.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (svgIndex > 0) svgContent = svgContent.Substring(svgIndex);

            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".svg");

            File.WriteAllText(tempPath, svgContent);

            var svgSource = SvgSource.Load(tempPath);

            try
            {
                File.Delete(tempPath);
            }
            catch
            {
            }

            return new SvgImage { Source = svgSource };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error parsing SVG: {ex.Message}");
            return null;
        }
    }

    private void SaveCurrentDiagramContent()
    {
        string content = TxtDesignPlantUml.Text ?? "";

        if (_isSqlMode)
        {
            _currentSqlDraft.PlantUmlSource = content;
        }
        else
        {
            if (_activeDiagramIndex == 0)
            {
                if (_currentDraft.PlantUmlSources.Count == 0) _currentDraft.PlantUmlSources.Add("");
                _currentDraft.PlantUmlSources[0] = content;
            }
            else
            {
                int listIndex = _activeDiagramIndex - 1;
                if (listIndex >= 0 && listIndex < _currentDraft.MaterialDiagrams.Count)
                    _currentDraft.MaterialDiagrams[listIndex].PlantUmlSource = content;
            }
        }
    }

    private void LoadDiagramContentToUI()
    {
        string content = "";
        if (_isSqlMode)
        {
            content = _currentSqlDraft.PlantUmlSource;
        }
        else
        {
            if (_activeDiagramIndex == 0)
            {
                if (_currentDraft.PlantUmlSources.Count > 0)
                    content = _currentDraft.PlantUmlSources[0];
            }
            else
            {
                int listIndex = _activeDiagramIndex - 1;
                if (listIndex >= 0 && listIndex < _currentDraft.MaterialDiagrams.Count)
                    content = _currentDraft.MaterialDiagrams[listIndex].PlantUmlSource;
            }
        }

        TxtDesignPlantUml.Text = content;
    }

    private void BtnAddDiagramTab_Click(object sender, RoutedEventArgs e)
    {
        if (_currentDraft.MaterialDiagrams.Count >= 3) return;

        _currentDraft.MaterialDiagrams.Add(new DiagramData());

        SwitchDesignerDiagramTab(_currentDraft.MaterialDiagrams.Count);

        OnDesignerInputChanged(this, EventArgs.Empty);
    }

    private void BtnDeleteDiagramTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDiagramIndex <= 0) return; // cannot delete main tab

        int listIndex = _activeDiagramIndex - 1;
        if (listIndex >= 0 && listIndex < _currentDraft.MaterialDiagrams.Count)
        {
            _currentDraft.MaterialDiagrams.RemoveAt(listIndex);

            _activeDiagramIndex = Math.Max(0, _activeDiagramIndex - 1);

            LoadDiagramContentToUI();
            UpdateDesignerDiagramTabs();
            OnDesignerInputChanged(this, EventArgs.Empty);
        }
    }

    private enum DesignerSource
    {
        None,
        StarterCode,
        Validation,
        TestingCode,
        SqlSetup,
        SqlExpected,
        SqlVerify,
        SqlSample
    }
}