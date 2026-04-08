using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AbiturEliteCode.cs.MainWindow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void ConfigureSqlQueryEditor()
    {
        SqlQueryEditor.Options.ConvertTabsToSpaces = true;
        SqlQueryEditor.Options.IndentationSize = 4;
        SqlQueryEditor.Options.ShowSpaces = false;
        SqlQueryEditor.Options.EnableHyperlinks = false;
        SqlQueryEditor.Options.EnableEmailHyperlinks = false;
        SqlQueryEditor.Options.HighlightCurrentLine = true;

        SqlQueryEditor.FontFamily = new FontFamily(MonospaceFontFamily);
        SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
        SqlQueryEditor.Background = Brushes.Transparent;
        SqlQueryEditor.Foreground = SolidColorBrush.Parse("#D4D4D4");

        // add renderers (ported from C# editor)
        var bracketRenderer = new BracketHighlightRenderer(SqlQueryEditor);
        SqlQueryEditor.TextArea.TextView.BackgroundRenderers.Add(bracketRenderer);

        var ghostTransformer = new GhostCharacterTransformer(SqlQueryEditor);
        SqlQueryEditor.TextArea.TextView.LineTransformers.Add(ghostTransformer);

        _sqlAutocompleteService = new AutocompleteService(AutocompleteService.SqlKeywords);
        _sqlAutocompleteGenerator = new AutocompleteGhostGenerator(SqlQueryEditor, _sqlAutocompleteService);
        SqlQueryEditor.TextArea.TextView.ElementGenerators.Add(_sqlAutocompleteGenerator);

        var sqlSpaceTabRenderer = new SpaceTabIndicatorRenderer(SqlQueryEditor);
        SqlQueryEditor.TextArea.TextView.BackgroundRenderers.Add(sqlSpaceTabRenderer);

        _sqlBlockCaret = new VimBlockCaretRenderer(SqlQueryEditor);
        SqlQueryEditor.TextArea.TextView.BackgroundRenderers.Add(_sqlBlockCaret);

        var sqlSelectionHighlightRenderer = new SelectionHighlightRenderer(SqlQueryEditor);
        SqlQueryEditor.TextArea.TextView.BackgroundRenderers.Add(sqlSelectionHighlightRenderer);

        SqlQueryEditor.TextChanged += (s, e) =>
        {
            autoSaveTimer.Stop();
            autoSaveTimer.Start();

            if (AppSettings.IsSqlAutocompleteEnabled)
            {
                if (_suppressSqlAutocomplete)
                {
                    _suppressSqlAutocomplete = false;
                }
                else
                {
                    if (_isDesignerMode)
                    {
                        // combine designer content to fill autocomplete memory contextually
                        string combinedText =
                            $"{TxtDesignSqlSetup.Text}\n{TxtDesignSqlSample.Text}\n{TxtDesignSqlVerify.Text}\n{SqlQueryEditor.Text}";
                        _sqlAutocompleteService.ScanTokens(combinedText);
                    }
                    else
                    {
                        _sqlAutocompleteService.ScanTokens(SqlQueryEditor.Text);
                    }

                    _sqlAutocompleteService.UpdateSuggestion(SqlQueryEditor.Text, SqlQueryEditor.CaretOffset);
                    SqlQueryEditor.TextArea.TextView.Redraw();
                }
            }
        };

        SqlQueryEditor.TextArea.Caret.PositionChanged += (s, e) =>
        {
            // clamp caret in vim normal mode
            if (AppSettings.IsSqlVimEnabled && _vimMode == VimMode.Normal)
            {
                var line = SqlQueryEditor.Document.GetLineByOffset(SqlQueryEditor.CaretOffset);
                if (SqlQueryEditor.CaretOffset == line.EndOffset && line.Length > 0) SqlQueryEditor.CaretOffset--;
            }

            SqlQueryEditor.TextArea.Caret.BringCaretToView(40);
            SqlQueryEditor.TextArea.TextView.Redraw();

            if (AppSettings.IsSqlAutocompleteEnabled)
            {
                _sqlAutocompleteService.UpdateSuggestion(SqlQueryEditor.Text, SqlQueryEditor.CaretOffset);
                SqlQueryEditor.TextArea.TextView.Redraw();
            }
        };

        SqlQueryEditor.TextArea.TextEntering += SqlEditor_TextEntering;
        SqlQueryEditor.AddHandler(KeyDownEvent, SqlQueryEditor_KeyDown, RoutingStrategies.Tunnel);

        SqlQueryEditor.AddHandler(PointerWheelChangedEvent, SqlQueryEditor_PointerWheelChanged,
            RoutingStrategies.Tunnel);

        // clear relational model focus tracking when the sql editor is focused manually
        SqlQueryEditor.GotFocus += (s, e) => { UpdateFocusedColumn(null, null); };
        SqlQueryEditor.TextArea.GotFocus += (s, e) => { UpdateFocusedColumn(null, null); };

        // fix 1 pixel vertical misalignment
        foreach (var margin in SqlQueryEditor.TextArea.LeftMargins)
            if (margin is LineNumberMargin lineMargin)
                lineMargin.Margin = new Thickness(0, 1, 0, 0);
    }

    private void SqlQueryEditor_KeyDown(object sender, KeyEventArgs e)
    {
        // escape key to clear suggestions
        if (e.Key == Key.Escape && _sqlAutocompleteService.HasSuggestion)
        {
            _sqlAutocompleteService.ClearSuggestion();
            SqlQueryEditor.TextArea.TextView.Redraw();
            e.Handled = true;
            return;
        }

        // up and down arrows for autocompletion cycling
        if (AppSettings.IsSqlAutocompleteEnabled && (e.Key == Key.Up || e.Key == Key.Down) &&
            _sqlAutocompleteService.HasSuggestion)
        {
            if (e.Key == Key.Up) _sqlAutocompleteService.CyclePrevious();
            else _sqlAutocompleteService.CycleNext();

            SqlQueryEditor.TextArea.TextView.Redraw();
            e.Handled = true;
            return;
        }

        // ctrl/cmd + +/- => zoom
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                AppSettings.SqlEditorFontSize = Math.Min(48, AppSettings.SqlEditorFontSize + 1);
                SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                AppSettings.SqlEditorFontSize = Math.Max(8, AppSettings.SqlEditorFontSize - 1);
                SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
                e.Handled = true;
                return;
            }
        }

        // ctrl/cmd + shift + z => redo
        if (e.Key == Key.Z &&
            (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            SqlQueryEditor.Redo();
            e.Handled = true;
            return;
        }

        // ctrl + s => save
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            SaveCurrentProgress();
            AddSqlOutput("System", "> Query gespeichert.", Brushes.LightGray);
            e.Handled = true;
            return;
        }

        // f5 => run
        if (e.Key == Key.F5)
        {
            BtnRun_Click(this, null);
            e.Handled = true;
            return;
        }

        // ctrl + enter => run
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            BtnRun_Click(this, null);
            e.Handled = true;
            return;
        }

        // tab => confirm autocompletion
        if (AppSettings.IsSqlAutocompleteEnabled && e.Key == Key.Tab && _sqlAutocompleteService.HasSuggestion)
        {
            string fullText = _sqlAutocompleteService.CurrentSuggestionFull;
            int wordLen = _sqlAutocompleteService.CurrentWordLength;
            if (!string.IsNullOrEmpty(fullText))
            {
                int offset = SqlQueryEditor.CaretOffset;

                _sqlAutocompleteService.ClearSuggestion();

                SqlQueryEditor.Document.Replace(offset - wordLen, wordLen, fullText);
                SqlQueryEditor.CaretOffset = offset - wordLen + fullText.Length;
                e.Handled = true;
                return;
            }
        }

        // temporarily disable autocompletion if moving to the right
        if (e.Key == Key.Right && _sqlAutocompleteService.HasSuggestion)
        {
            _sqlAutocompleteService.ClearSuggestion();
            SqlQueryEditor.TextArea.TextView.Redraw();
            e.Handled = true;
        }

        if (e.Key == Key.Back)
        {
            if (AppSettings.IsSqlAutocompleteEnabled && _sqlAutocompleteService.HasSuggestion)
            {
                _sqlAutocompleteService.ClearSuggestion();
                SqlQueryEditor.TextArea.TextView.Redraw();
                _suppressSqlAutocomplete = true;
            }

            int offset = SqlQueryEditor.CaretOffset;

            // smart delete pairs
            if (offset > 0 && offset < SqlQueryEditor.Document.TextLength)
            {
                char charBefore = SqlQueryEditor.Document.GetCharAt(offset - 1);
                char charAfter = SqlQueryEditor.Document.GetCharAt(offset);

                if (
                    (charBefore == '(' && charAfter == ')')
                    || (charBefore == '"' && charAfter == '"')
                    || (charBefore == '\'' && charAfter == '\'')
                )
                {
                    SqlQueryEditor.Document.Remove(offset - 1, 2);
                    e.Handled = true;
                    return;
                }
            }

            // remove whole indentation (4 spaces)
            if (SqlQueryEditor.SelectionLength == 0 && offset >= 4)
            {
                string textToCheck = SqlQueryEditor.Document.GetText(offset - 4, 4);
                if (textToCheck == "    ")
                {
                    SqlQueryEditor.Document.Remove(offset - 4, 4);
                    e.Handled = true;
                }
            }
        }

        // -- vim logic --
        if (!AppSettings.IsSqlVimEnabled) return;

        if (e.Key == Key.Escape)
        {
            _vimMode = VimMode.Normal;
            _vimCommandBuffer = "";
            SqlQueryEditor.TextArea.ClearSelection();

            var line = SqlQueryEditor.Document.GetLineByOffset(SqlQueryEditor.CaretOffset);
            if (SqlQueryEditor.CaretOffset == line.EndOffset && line.Length > 0)
                SqlQueryEditor.CaretOffset--;

            UpdateVimUI();
            e.Handled = true;
            return;
        }

        if (_vimMode == VimMode.Insert)
            // allow normal typing
            return;

        if (_vimMode == VimMode.CommandLine)
        {
            HandleVimCommandLine(e);
            e.Handled = true;
            return;
        }

        if (_vimMode == VimMode.Search)
        {
            HandleVimSearch(e);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            if (e.Key == Key.C || e.Key == Key.V || e.Key == Key.X || e.Key == Key.A)
                return;

        // normal or commandpending mode -> intercept all
        e.Handled = true;
        HandleVimNormalInput(e);
    }

    private void SqlQueryEditor_PointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        // zoom via ctrl + mwheel
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (e.Delta.Y > 0)
                AppSettings.SqlEditorFontSize = Math.Min(48, AppSettings.SqlEditorFontSize + 1);
            else if (e.Delta.Y < 0) AppSettings.SqlEditorFontSize = Math.Max(8, AppSettings.SqlEditorFontSize - 1);
            SqlQueryEditor.FontSize = AppSettings.SqlEditorFontSize;
            e.Handled = true;
        }
    }

    private void SqlEditor_TextEntering(object sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        char charTyped = e.Text[0];
        TextArea textArea = (TextArea)sender;
        int offset = textArea.Caret.Offset;

        // skip closing pair
        if (charTyped == '"' || charTyped == '\'')
            if (offset < textArea.Document.TextLength && textArea.Document.GetCharAt(offset) == charTyped)
            {
                textArea.Caret.Offset += 1;
                e.Handled = true;
                return;
            }

        // surround selection logic
        if (textArea.Selection.Length > 0)
            if (charTyped == '(' || charTyped == '"' || charTyped == '\'')
            {
                string startChar = charTyped.ToString();
                string endChar = charTyped == '(' ? ")" : charTyped.ToString();

                string selectedText = textArea.Selection.GetText();
                int selectionStart = textArea.Selection.SurroundingSegment.Offset;

                textArea.Selection.ReplaceSelectionWithText(startChar + selectedText + endChar);
                textArea.Caret.Offset = selectionStart + selectedText.Length + 2;

                e.Handled = true;
                return;
            }

        // auto add designated pair
        if (charTyped == '(' || charTyped == '"' || charTyped == '\'')
        {
            string pair = charTyped == '(' ? ")" : charTyped.ToString();

            textArea.Document.Insert(offset, charTyped + pair);
            textArea.Caret.Offset = offset + 1;
            e.Handled = true;
            return;
        }

        // skip closing pair
        if (charTyped == ')' || charTyped == '"' || charTyped == '\'')
            if (offset < textArea.Document.TextLength && textArea.Document.GetCharAt(offset) == charTyped)
            {
                textArea.Caret.Offset += 1;
                e.Handled = true;
            }
    }

    private void UpdateSqlAutocompleteSchema()
    {
        if (_sqlAutocompleteService == null) return;
        var schema = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        // check if current level is an abitur similar level (>= 29)
        bool isAbiturLevel = currentSqlLevel != null && currentSqlLevel.Id >= 29;

        foreach (var t in _currentRelationalModel)
            schema[t.Name] = t.Columns.Select(c => c.IsFk && !isAbiturLevel ? c.Name + "_FK" : c.Name).ToList();
        _sqlAutocompleteService.SetSqlSchema(schema);
    }

    private void ApplySqlSyntaxHighlighting()
    {
        if (AppSettings.IsSqlSyntaxHighlightingEnabled)
            SqlQueryEditor.SyntaxHighlighting = SqlCodeEditor.GetDarkSqlHighlighting();
        else
            SqlQueryEditor.SyntaxHighlighting = null;
    }

    private void AddSqlOutput(string author, string text, IBrush color, bool isCode = false,
        DataTable expectedTable = null)
    {
        // remove old output if exceeds soft limit
        if (PnlSqlOutput.Children.Count > 20) PnlSqlOutput.Children.RemoveAt(0);

        StackPanel targetContainer = null;

        // grouping for system
        if (author == "System" && PnlSqlOutput.Children.Count > 0)
        {
            var lastContainer = PnlSqlOutput.Children.Last() as StackPanel;
            if (lastContainer != null && lastContainer.Children.Count >= 1)
            {
                var authorBlock = lastContainer.Children[0] as TextBlock;
                if (authorBlock != null && authorBlock.Text == "System") targetContainer = lastContainer;
            }
        }

        if (targetContainer == null)
        {
            targetContainer = new StackPanel { Spacing = 0 };
            targetContainer.Children.Add(new SelectableTextBlock
            {
                Text = author,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Gray,
                FontSize = 10
            });
            PnlSqlOutput.Children.Add(targetContainer);
        }

        if (isCode)
        {
            var codeOutput = new TextEditor
            {
                Document = new TextDocument(text),
                SyntaxHighlighting = SqlCodeEditor.GetDarkSqlHighlighting(),
                FontFamily = new FontFamily(MonospaceFontFamily),
                FontSize = 14,
                IsReadOnly = true,
                ShowLineNumbers = false,
                Background = SolidColorBrush.Parse("#1A1A1A"),
                Foreground = Brushes.White,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8),
                MinHeight = 0
            };

            codeOutput.Options.ShowSpaces = false;
            codeOutput.Options.ShowTabs = false;
            codeOutput.Options.HighlightCurrentLine = false;

            var border = new Border
            {
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Margin = new Thickness(0, 2, 0, 5),
                Child = codeOutput
            };

            targetContainer.Children.Add(border);
        }
        else
        {
            var content = new SelectableTextBlock
            {
                Text = text,
                Foreground = color,
                FontFamily = FontFamily.Default,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // append button with tooltip if applicable
            if (expectedTable != null && _consecutiveSqlFails >= 3 &&
                text.Contains("Das Ergebnis stimmt nicht mit der Erwartung überein"))
            {
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                content.Margin = new Thickness(0);
                stack.Children.Add(content);

                var btnExpected = new Border
                {
                    Background = SolidColorBrush.Parse("#3C3C3C"),
                    Padding = new Thickness(8, 2),
                    CornerRadius = new CornerRadius(10),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "?",
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                btnExpected.PointerEntered += (s, e) =>
                {
                    btnExpected.Background = SolidColorBrush.Parse("#4A4A4A");
                    ToolTip.SetIsOpen(btnExpected, true); // force open immediately
                };
                btnExpected.PointerExited += (s, e) =>
                {
                    btnExpected.Background = SolidColorBrush.Parse("#3C3C3C");
                    ToolTip.SetIsOpen(btnExpected, false); // force close immediately
                };

                var toolTipBorder = new Border
                {
                    Background = SolidColorBrush.Parse("#141414"),
                    BorderBrush = SolidColorBrush.Parse("#333333"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    ClipToBounds = true
                };
                toolTipBorder.Child = BuildTableGrid(expectedTable,
                    currentSqlLevel.ExpectedSchema?.Select(c => c.Type).ToList());

                ToolTip.SetTip(btnExpected, toolTipBorder);
                ToolTip.SetShowDelay(btnExpected, 0);

                stack.Children.Add(btnExpected);
                targetContainer.Children.Add(stack);
            }
            else
            {
                targetContainer.Children.Add(content);
            }
        }

        SqlOutputScroller.ScrollToEnd();
    }

    private void AddSqlTable(DataTable table)
    {
        if (PnlSqlOutput.Children.Count > 20) PnlSqlOutput.Children.RemoveAt(0);

        var tableContainer = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderBrush = SolidColorBrush.Parse("#333333"),
            BorderThickness = new Thickness(1),
            Background = SolidColorBrush.Parse("#141414"),
            ClipToBounds = true,
            Margin = new Thickness(0, 5, 0, 15),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        tableContainer.Child = BuildTableGrid(table);
        PnlSqlOutput.Children.Add(tableContainer);
        SqlOutputScroller.ScrollToEnd();
    }

    private string GetMySqlTypeLabel(Type type)
    {
        // map to mysql
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return "INT";
        if (type == typeof(string))
            return "VARCHAR(255)";
        if (type == typeof(double) || type == typeof(float))
            return "DOUBLE";
        if (type == typeof(decimal))
            return "DECIMAL(10,2)";
        if (type == typeof(bool))
            return "TINYINT(1)";
        if (type == typeof(DateTime))
            return "DATETIME";

        return "TEXT"; // fallback
    }

    private void CleanAndRenderExpectedTable()
    {
        if (_isCleaningTable) return;
        _isCleaningTable = true;
        bool changed = false;

        // removes in-between empty ROWS dynamically
        for (int r = _currentSqlDraft.ExpectedResult.Count - 2; r >= 0; r--)
            if (_currentSqlDraft.ExpectedResult[r].All(string.IsNullOrWhiteSpace))
            {
                _currentSqlDraft.ExpectedResult.RemoveAt(r);
                changed = true;
            }

        // removes in-between empty COLUMNS dynamically
        for (int c = _currentSqlDraft.ExpectedSchema.Count - 2; c >= 0; c--)
            if (string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema[c].Name))
            {
                bool colEmpty = true;
                foreach (var row in _currentSqlDraft.ExpectedResult)
                    if (row.Length > c && !string.IsNullOrWhiteSpace(row[c]))
                    {
                        colEmpty = false;
                        break;
                    }

                if (colEmpty)
                {
                    _currentSqlDraft.ExpectedSchema.RemoveAt(c);
                    for (int i = 0; i < _currentSqlDraft.ExpectedResult.Count; i++)
                    {
                        var list = _currentSqlDraft.ExpectedResult[i].ToList();
                        if (list.Count > c) list.RemoveAt(c);
                        _currentSqlDraft.ExpectedResult[i] = list.ToArray();
                    }

                    changed = true;
                }
            }

        if (changed) RenderExpectedTable();
        _isCleaningTable = false;
    }
}