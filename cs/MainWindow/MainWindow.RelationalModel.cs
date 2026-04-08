using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Avalonia.Threading;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void TriggerRelationalAutoSave()
    {
        UpdateSqlAutocompleteSchema();
        UpdateRelationalValidationIcon();
        _relationalAutoSaveTimer.Stop();
        _relationalAutoSaveTimer.Start();

        // show tip for sql level 13 (if unseen)
        if (_isSqlMode && currentSqlLevel?.Id == 13 && !playerData.Settings.RelationalModelTipShown)
        {
            string currentJson = JsonSerializer.Serialize(_currentRelationalModel);

            if (currentJson != _initialRelationalModelJson)
                if (!PnlRelationalTip.IsVisible)
                {
                    _relationalTipDelayTimer.Stop();
                    _relationalTipDelayTimer.Start();
                }
        }
    }

    private bool CheckRelationalModel(int levelId)
    {
        if (levelId < 13 || levelId > 27) return false;

        // normalize strings (ignore case and whitespaces)
        string Normalize(string s)
        {
            return s?.Trim().ToLower() ?? "";
        }

        var currentModel = _currentRelationalModel.Select(t => new
        {
            Name = Normalize(t.Name),
            Columns = t.Columns.Select(c => new
            {
                Name = Normalize(c.Name),
                c.IsPk,
                c.IsFk
            }).OrderBy(c => c.Name).ToList()
        }).OrderBy(t => t.Name).ToList();

        var expectedTables = new List<(string Name, (string ColName, bool IsPk, bool IsFk)[] Cols)>();

        switch (levelId)
        {
            case 13:
                expectedTables.Add(("Schueler",
                    new[] { ("id", true, false), ("name", false, false), ("klasse", false, false) }));
                expectedTables.Add(("Buch", new[] { ("id", true, false), ("titel", false, false) }));
                expectedTables.Add(("ausleihe",
                    new[] { ("schuelerid", true, true), ("buchid", true, true), ("datum", false, false) }));
                break;
            case 14:
            case 15:
                expectedTables.Add(("Vip", new[] { ("id", true, false), ("name", false, false) }));
                expectedTables.Add(("Reservierung", new[] { ("vipid", true, true), ("tischnr", false, false) }));
                break;
            case 16:
                expectedTables.Add(("Gast",
                    new[] { ("id", true, false), ("name", false, false), ("stadt", false, false) }));
                expectedTables.Add(("Ticket",
                    new[] { ("id", true, false), ("gastid", false, true), ("bereich", false, false) }));
                break;
            case 17:
                expectedTables.Add(("Vip", new[] { ("id", true, false), ("name", false, false) }));
                expectedTables.Add(("Reservierung",
                    new[] { ("vipid", true, true), ("bereich", false, false), ("tischnr", false, false) }));
                break;
            case 18:
                expectedTables.Add(("Produkt",
                    new[] { ("id", true, false), ("bezeichnung", false, false), ("preis", false, false) }));
                expectedTables.Add(("Position",
                    new[] { ("id", true, false), ("produktid", false, true), ("menge", false, false) }));
                break;
            case 19:
                expectedTables.Add(("Bestellung", new[] { ("id", true, false), ("datum", false, false) }));
                expectedTables.Add(("Position",
                    new[]
                    {
                        ("id", true, false), ("preis", false, false), ("menge", false, false),
                        ("bestellungid", false, true)
                    }));
                break;
            case 20:
                expectedTables.Add(("Produkt", new[] { ("id", true, false), ("name", false, false) }));
                expectedTables.Add(("Position",
                    new[] { ("id", true, false), ("menge", false, false), ("produktid", false, true) }));
                break;
            case 21:
                expectedTables.Add(("Produkt", new[] { ("id", true, false), ("kategorie", false, false) }));
                expectedTables.Add(("Position",
                    new[] { ("id", true, false), ("menge", false, false), ("produktid", false, true) }));
                break;
            case 22:
                expectedTables.Add(("Produkt",
                    new[] { ("id", true, false), ("kategorie", false, false), ("preis", false, false) }));
                expectedTables.Add(("Position",
                    new[] { ("id", true, false), ("menge", false, false), ("produktid", false, true) }));
                break;
            case 23:
            case 24:
            case 25:
                expectedTables.Add(("Gast", new[] { ("id", true, false), ("name", false, false) }));
                expectedTables.Add(("Buchung",
                    new[]
                    {
                        ("id", true, false), ("anreise", false, false), ("abreise", false, false),
                        ("gastid", false, true)
                    }));
                break;
            case 26:
                expectedTables.Add(("Buchung",
                    new[] { ("id", true, false), ("anreise", false, false), ("abreise", false, false) }));
                break;
            case 27:
                expectedTables.Add(("Gast", new[] { ("id", true, false), ("name", false, false) }));
                expectedTables.Add(("Buchung",
                    new[]
                    {
                        ("id", true, false), ("anreise", false, false), ("abreise", false, false),
                        ("gastid", false, true), ("zimmerid", false, true)
                    }));
                expectedTables.Add(("Zimmer", new[] { ("id", true, false), ("nummer", false, false) }));
                break;
            default:
                return false;
        }

        if (currentModel.Count != expectedTables.Count) return false;

        var expectedModel = expectedTables.Select(t => new
        {
            Name = Normalize(t.Name),
            Columns = t.Cols.Select(c => new
            {
                Name = Normalize(c.ColName),
                c.IsPk,
                c.IsFk
            }).OrderBy(c => c.Name).ToList()
        }).OrderBy(t => t.Name).ToList();

        for (int i = 0; i < expectedModel.Count; i++)
        {
            if (currentModel[i].Name != expectedModel[i].Name) return false;
            if (currentModel[i].Columns.Count != expectedModel[i].Columns.Count) return false;

            for (int j = 0; j < expectedModel[i].Columns.Count; j++)
            {
                var curCol = currentModel[i].Columns[j];
                var expCol = expectedModel[i].Columns[j];

                if (curCol.Name != expCol.Name || curCol.IsPk != expCol.IsPk || curCol.IsFk != expCol.IsFk)
                    return false;
            }
        }

        return true;
    }

    private void UpdateRelationalValidationIcon()
    {
        if (_relationalValidationIcon == null || currentSqlLevel == null) return;

        if (currentSqlLevel.Id >= 13 && currentSqlLevel.Id <= 27)
        {
            bool isCorrect = CheckRelationalModel(currentSqlLevel.Id);
            string iconPath = isCorrect ? "assets/icons/ic_correct.svg" : "assets/icons/ic_not_correct.svg";

            var svgImage = new SvgImage();
            svgImage.Source = SvgSource.Load($"avares://AbiturEliteCode/{iconPath}");
            _relationalValidationIcon.Source = svgImage;

            ToolTip.SetTip(_relationalValidationIcon, isCorrect ? "Korrekt umgesetzt" : "Noch nicht korrekt umgesetzt");
            _relationalValidationIcon.IsVisible = true;
        }
        else
        {
            _relationalValidationIcon.IsVisible = false;
        }
    }

    private void RenderRelationalModel(StackPanel targetPanel, bool isReadOnly)
    {
        targetPanel.Children.Clear();

        bool showEditControls = !isReadOnly;
        if (_isDesignerMode && _currentSqlDraft != null) showEditControls = _currentSqlDraft.IsRelationalModelReadOnly;

        // prevent method being cancelled early by designer
        if (!_isDesignerMode && !showEditControls && _currentRelationalModel.Count == 0) return;

        // header with global buttons
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        titleStack.Children.Add(new TextBlock
        {
            Text = "Relationales Modell (Schema)",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = BrushTextTitle,
            VerticalAlignment = VerticalAlignment.Center
        });

        // validation icon
        if (currentSqlLevel != null && currentSqlLevel.Id >= 13 && currentSqlLevel.Id <= 27)
        {
            _relationalValidationIcon = LoadIcon("assets/icons/ic_not_correct.svg", 16);
            _relationalValidationIcon.Margin = new Thickness(5, 0, 5, 0);
            _relationalValidationIcon.VerticalAlignment = VerticalAlignment.Center;

            titleStack.Children.Add(_relationalValidationIcon);
            UpdateRelationalValidationIcon();
        }
        else
        {
            _relationalValidationIcon = null;
        }

        if (_isDesignerMode && _currentSqlDraft != null)
        {
            var btnLock = new Button
            {
                Content = LoadIcon(
                    _currentSqlDraft.IsRelationalModelReadOnly
                        ? "assets/icons/ic_lock.svg"
                        : "assets/icons/ic_lock_open.svg", 16),
                Background = Brushes.Transparent,
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursor.Parse("Hand"),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnLock,
                _currentSqlDraft.IsRelationalModelReadOnly
                    ? "Relationales Modell für Spieler freigeben (Bearbeitbar)"
                    : "Relationales Modell für Spieler sperren (Read-Only)");
            btnLock.Click += (s, e) =>
            {
                _currentSqlDraft.IsRelationalModelReadOnly = !_currentSqlDraft.IsRelationalModelReadOnly;
                btnLock.Content =
                    LoadIcon(
                        _currentSqlDraft.IsRelationalModelReadOnly
                            ? "assets/icons/ic_lock.svg"
                            : "assets/icons/ic_lock_open.svg", 16);
                ToolTip.SetTip(btnLock,
                    _currentSqlDraft.IsRelationalModelReadOnly
                        ? "Relationales Modell für Spieler freigeben (Bearbeitbar)"
                        : "Relationales Modell für Spieler sperren (Read-Only)");
                TriggerRelationalAutoSave();
                RenderRelationalModel(targetPanel, isReadOnly);
            };
            titleStack.Children.Add(btnLock);
        }

        var btnCopyModel = new Button
        {
            Content = LoadIcon("assets/icons/ic_copy.svg", 16),
            Background = Brushes.Transparent,
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursor.Parse("Hand"),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = showEditControls
        };
        ToolTip.SetTip(btnCopyModel, "Modell kopieren");
        btnCopyModel.Click += async (s, e) =>
        {
            var sb = new StringBuilder();
            foreach (var table in _currentRelationalModel)
            {
                sb.Append(table.Name).Append(" (");
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var col = table.Columns[i];
                    sb.Append(col.Name);
                    if (col.IsFk) sb.Append("#");
                    if (col.IsPk) sb.Append("[PK]");
                    if (i < table.Columns.Count - 1) sb.Append(", ");
                }

                sb.AppendLine(")");
            }

            var topLevel = GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(sb.ToString().TrimEnd());
                btnCopyModel.Background = SolidColorBrush.Parse("#2E8B57");
                await Task.Delay(500);
                btnCopyModel.Background = Brushes.Transparent;
            }
        };
        titleStack.Children.Add(btnCopyModel);

        headerGrid.Children.Add(titleStack);

        if (showEditControls)
        {
            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            _btnGlobalPk = new Button
            {
                Content = "PK",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                Cursor = Cursor.Parse("Hand"),
                IsEnabled = false,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4)
            };
            ToolTip.SetTip(_btnGlobalPk, "Primärschlüssel (Unterstreichen)");
            _btnGlobalPk.Click += (s, e) =>
            {
                if (_focusedRColumn != null)
                {
                    _focusedRColumn.IsPk = !_focusedRColumn.IsPk;
                    TriggerRelationalAutoSave();
                    RenderRelationalModel(targetPanel, false);
                }
            };

            _btnGlobalFk = new Button
            {
                Content = "FK",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                Cursor = Cursor.Parse("Hand"),
                IsEnabled = false,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 4)
            };
            ToolTip.SetTip(_btnGlobalFk, "Fremdschlüssel (Raute anhängen)");
            _btnGlobalFk.Click += (s, e) =>
            {
                if (_focusedRColumn != null)
                {
                    _focusedRColumn.IsFk = !_focusedRColumn.IsFk;
                    TriggerRelationalAutoSave();
                    RenderRelationalModel(targetPanel, false);
                }
            };

            btnStack.Children.Add(_btnGlobalPk);
            btnStack.Children.Add(_btnGlobalFk);
            Grid.SetColumn(btnStack, 1);
            headerGrid.Children.Add(btnStack);
        }

        targetPanel.Children.Add(headerGrid);

        // prevent empty not showing relational model
        if (_isDesignerMode && !showEditControls && _currentRelationalModel.Count == 0) return;

        // --- READ ONLY MODE ---
        if (!showEditControls)
        {
            if (_isDesignerMode)
            {
                var tbInfo = new TextBlock
                {
                    Text = "Spieler startet mit einem leeren Modell.",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyle.Italic,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                targetPanel.Children.Add(tbInfo);
                return;
            }

            foreach (var table in _currentRelationalModel)
            {
                var tb = new SelectableTextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5),
                    FontFamily = new FontFamily(MonospaceFontFamily),
                    FontSize = 15
                };
                tb.Inlines.Add(new Run
                    { Text = table.Name, Foreground = BrushTextHighlight, FontWeight = FontWeight.Bold });
                tb.Inlines.Add(new Run { Text = " (", Foreground = BrushTextNormal });

                for (int i = 0; i < table.Columns.Count; i++)
                {
                    var col = table.Columns[i];
                    var run = new Run { Text = col.Name + (col.IsFk ? "#" : ""), Foreground = BrushTextNormal };
                    if (col.IsPk) run.TextDecorations = TextDecorations.Underline;
                    tb.Inlines.Add(run);
                    if (i < table.Columns.Count - 1)
                        tb.Inlines.Add(new Run { Text = ", ", Foreground = BrushTextNormal });
                }

                tb.Inlines.Add(new Run { Text = ")", Foreground = BrushTextNormal });
                targetPanel.Children.Add(tb);
            }

            return;
        }

        // --- EDIT MODE ---
        foreach (var table in _currentRelationalModel)
        {
            var rowPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // table name
            var txtTableName = new TextBox
            {
                Text = table.Name,
                Foreground = BrushTextHighlight,
                FontFamily = new FontFamily(MonospaceFontFamily),
                FontWeight = FontWeight.Bold,
                FontSize = 15,
                LetterSpacing = 0.5,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2),
                MinHeight = 0,
                MinWidth = 40,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            // only valid sql characters allowed
            txtTableName.TextChanged += (s, e) =>
            {
                string filtered =
                    new string(txtTableName.Text?.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray() ??
                               Array.Empty<char>());
                if (txtTableName.Text != filtered)
                {
                    txtTableName.Text = filtered;
                    txtTableName.CaretIndex = txtTableName.Text.Length;
                }

                table.Name = txtTableName.Text;
                TriggerRelationalAutoSave();
            };

            txtTableName.GotFocus += (s, e) =>
            {
                UpdateFocusedColumn(null, null);
                _focusedRTable = table;
            };

            // handle shortcut to jump to first column or create one
            txtTableName.KeyDown += (s, e) =>
            {
                if (e.KeySymbol == "(" || (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D8) ||
                    e.Key == Key.OemOpenBrackets)
                {
                    e.Handled = true;
                    if (table.Columns.Count == 0)
                    {
                        var newCol = new RColumn { Name = "" };
                        table.Columns.Add(newCol);
                        UpdateFocusedColumn(newCol, null);
                        RenderRelationalModel(targetPanel, false);
                        TriggerRelationalAutoSave();
                    }
                    else
                    {
                        UpdateFocusedColumn(table.Columns.First(), null);
                        RenderRelationalModel(targetPanel, false);
                    }
                }
            };

            if (_focusedRTable == table)
                Dispatcher.UIThread.Post(() =>
                {
                    txtTableName.Focus();
                    txtTableName.CaretIndex = txtTableName.Text?.Length ?? 0;
                });

            rowPanel.Children.Add(txtTableName);
            rowPanel.Children.Add(new TextBlock
            {
                Text = " (", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15
            });

            // add/delete column button
            var btnAddCol = new Button
            {
                Content = LoadIcon("assets/icons/ic_plus.svg", 16),
                Background = Brushes.Transparent,
                Padding = new Thickness(4),
                Margin = new Thickness(2, 0, 0, 0),
                Cursor = Cursor.Parse("Hand"),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnAddCol, "Spalte hinzufügen");

            btnAddCol.Click += (s, e) =>
            {
                if (ToolTip.GetTip(btnAddCol)?.ToString() == "Spalte löschen")
                {
                    if (table.Columns.Count > 0)
                    {
                        var lastCol = table.Columns.Last();
                        if (_focusedRColumn == lastCol) UpdateFocusedColumn(null, null);
                        table.Columns.Remove(lastCol);

                        // focus previous element
                        if (table.Columns.Count > 0)
                            UpdateFocusedColumn(table.Columns.Last(), null);
                        else
                            _focusedRTable = table;

                        RenderRelationalModel(targetPanel, false);
                        TriggerRelationalAutoSave();
                    }
                }
                else
                {
                    var newCol = new RColumn { Name = "" };
                    table.Columns.Add(newCol);
                    UpdateFocusedColumn(newCol, null);
                    RenderRelationalModel(targetPanel, false);
                    TriggerRelationalAutoSave();
                }
            };

            // columns
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                int capturedIndex = i;
                var colStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var txtCol = new TextBox
                {
                    Text = col.Name,
                    Foreground = BrushTextNormal,
                    FontFamily = new FontFamily(MonospaceFontFamily),
                    FontSize = 15,
                    LetterSpacing = 0.5,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(4, 2),
                    MinHeight = 0,
                    MinWidth = 20,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                // wrap in border to fake underline
                var pkUnderlineBorder = new Border
                {
                    BorderThickness = col.IsPk ? new Thickness(0, 0, 0, 1) : new Thickness(0),
                    BorderBrush = BrushTextNormal,
                    Child = txtCol
                };

                txtCol.TextChanged += (s, e) =>
                {
                    string filtered =
                        new string(txtCol.Text?.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray() ??
                                   Array.Empty<char>());
                    bool stateChanged = false;

                    // check for '_FK' suffix and strip it
                    if (filtered.EndsWith("_FK", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Substring(0, filtered.Length - 3);
                        if (!col.IsFk)
                        {
                            col.IsFk = true;
                            stateChanged = true;
                        }
                    }

                    // check for '_PK' suffix and strip it
                    if (filtered.EndsWith("_PK", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Substring(0, filtered.Length - 3);
                        if (!col.IsPk)
                        {
                            col.IsPk = true;
                            stateChanged = true;
                        }
                    }

                    if (txtCol.Text != filtered)
                    {
                        txtCol.Text = filtered;
                        txtCol.CaretIndex = txtCol.Text.Length;
                    }

                    col.Name = txtCol.Text;
                    TriggerRelationalAutoSave();

                    // render if it was newly marked as fk or pk
                    if (stateChanged)
                    {
                        RenderRelationalModel(targetPanel, false);
                        return;
                    }

                    // turn into delete column button if last column name is empty
                    if (capturedIndex == table.Columns.Count - 1)
                    {
                        if (string.IsNullOrEmpty(col.Name))
                        {
                            btnAddCol.Content = LoadIcon("assets/icons/ic_cross.svg", 16);
                            ToolTip.SetTip(btnAddCol, "Spalte löschen");
                        }
                        else
                        {
                            btnAddCol.Content = LoadIcon("assets/icons/ic_plus.svg", 16);
                            ToolTip.SetTip(btnAddCol, "Spalte hinzufügen");
                        }
                    }
                };

                // handle shortcuts to jump to next column/table or create them
                txtCol.KeyDown += (s, e) =>
                {
                    if (e.KeySymbol == "," || e.Key == Key.OemComma)
                    {
                        e.Handled = true;

                        // skip if current column textbox is empty
                        if (table.Columns[capturedIndex] != null && table.Columns[capturedIndex].Name.Length == 0)
                            return;

                        if (capturedIndex == table.Columns.Count - 1)
                        {
                            var newCol = new RColumn { Name = "" };
                            table.Columns.Add(newCol);
                            UpdateFocusedColumn(newCol, null);
                            RenderRelationalModel(targetPanel, false);
                            TriggerRelationalAutoSave();
                        }
                        else
                        {
                            UpdateFocusedColumn(table.Columns[capturedIndex + 1], null);
                            RenderRelationalModel(targetPanel, false);
                        }
                    }
                    else if (e.KeySymbol == ")" || (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D9) ||
                             e.Key == Key.OemCloseBrackets)
                    {
                        e.Handled = true;
                        int tableIndex = _currentRelationalModel.IndexOf(table);
                        if (tableIndex == _currentRelationalModel.Count - 1)
                        {
                            var newTable = new RTable
                                { Name = "", Columns = new List<RColumn> { new() { Name = "id", IsPk = true } } };
                            _currentRelationalModel.Add(newTable);
                            _focusedRTable = newTable;
                            RenderRelationalModel(targetPanel, false);
                            TriggerRelationalAutoSave();
                        }
                        else
                        {
                            _focusedRTable = _currentRelationalModel[tableIndex + 1];
                            RenderRelationalModel(targetPanel, false);
                        }
                    }
                };

                if (i == table.Columns.Count - 1 && string.IsNullOrEmpty(col.Name))
                {
                    btnAddCol.Content = LoadIcon("assets/icons/ic_cross.svg", 16);
                    ToolTip.SetTip(btnAddCol, "Spalte löschen");
                }

                txtCol.GotFocus += (s, e) => UpdateFocusedColumn(col, txtCol);
                if (_focusedRColumn == col)
                    Dispatcher.UIThread.Post(() =>
                    {
                        txtCol.Focus();
                        txtCol.CaretIndex = txtCol.Text?.Length ?? 0;
                    });

                colStack.Children.Add(pkUnderlineBorder);

                if (col.IsFk)
                    colStack.Children.Add(new TextBlock
                    {
                        Text = "#", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15
                    });

                if (i < table.Columns.Count - 1)
                    colStack.Children.Add(new TextBlock
                    {
                        Text = ", ", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15
                    });

                rowPanel.Children.Add(colStack);
            }

            rowPanel.Children.Add(btnAddCol);
            rowPanel.Children.Add(new TextBlock
            {
                Text = ")", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15
            });

            var btnDelTable = new Button
            {
                Content = LoadIcon("assets/icons/ic_cross.svg", 16),
                Background = Brushes.Transparent,
                Padding = new Thickness(4),
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursor.Parse("Hand"),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(btnDelTable, "Tabelle löschen");
            btnDelTable.Click += (s, e) =>
            {
                _currentRelationalModel.Remove(table);
                if (table.Columns.Contains(_focusedRColumn) || _focusedRTable == table) UpdateFocusedColumn(null, null);
                RenderRelationalModel(targetPanel, false);
                TriggerRelationalAutoSave();
            };
            rowPanel.Children.Add(btnDelTable);

            targetPanel.Children.Add(rowPanel);
        }

        var btnAddTable = new Button
        {
            Content = "+ Tabelle",
            Background = SolidColorBrush.Parse("#2D2D30"),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4),
            Cursor = Cursor.Parse("Hand"),
            Margin = new Thickness(0, 10, 0, 0)
        };
        btnAddTable.Click += (s, e) =>
        {
            var newTable = new RTable
            {
                Name = "",
                Columns = new List<RColumn>
                {
                    new()
                    {
                        Name = "id",
                        IsPk = true
                    }
                }
            };
            _currentRelationalModel.Add(newTable);

            // set focus to the newly created table
            _focusedRTable = newTable;

            RenderRelationalModel(targetPanel, false);
            TriggerRelationalAutoSave();
        };
        targetPanel.Children.Add(btnAddTable);

        UpdateGlobalKeyButtons();
    }

    private void ShowRelationalTip()
    {
        PnlRelationalTip.IsVisible = true;
        _relationalTipDisplayTimer.Stop();
        _relationalTipDisplayTimer.Start();

        playerData.Settings.RelationalModelTipShown = true;
        SaveSystem.Save(playerData);
    }
}