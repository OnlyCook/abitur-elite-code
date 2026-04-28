using System;
using System.Linq;
using System.Text.RegularExpressions;
using AbiturEliteCode.cs.MainWindow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void UpdateVimState()
    {
        if (TabVim != null)
        {
            bool isVimActive = _isSqlMode ? AppSettings.IsSqlVimEnabled : AppSettings.IsVimEnabled;
            TabVim.IsVisible = isVimActive;

            if (!isVimActive)
            {
                ActiveEditor.Cursor = Cursor.Default;
                if (VimStatusBorder != null) VimStatusBorder.IsVisible = false;
                if (SqlVimStatusBorder != null) SqlVimStatusBorder.IsVisible = false;
            }
            else
            {
                UpdateVimUI();
                if (PnlVimCheatSheet != null && VimCol1.Children.Count == 0)
                    BuildVimCheatSheet();
            }
        }
    }

    private void BuildVimCheatSheet()
    {
        if (VimCol1 == null || VimCol2 == null)
            return;
        VimCol1.Children.Clear();
        VimCol2.Children.Clear();

        void AddCategory(StackPanel col, string title, (string cmd, string desc)[] items)
        {
            var group = new StackPanel { Spacing = 5 };
            group.Children.Add(
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextHighlight,
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 5)
                }
            );

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("60, *") };
            int row = 0;
            foreach (var item in items)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var tCmd = new TextBlock
                {
                    Text = item.cmd,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                var tDesc = new TextBlock
                {
                    Text = item.desc,
                    Foreground = Brushes.LightGray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 2)
                };

                Grid.SetRow(tCmd, row);
                Grid.SetColumn(tCmd, 0);
                Grid.SetRow(tDesc, row);
                Grid.SetColumn(tDesc, 1);

                grid.Children.Add(tCmd);
                grid.Children.Add(tDesc);
                row++;
            }

            group.Children.Add(grid);
            col.Children.Add(group);
        }

        AddCategory(
            VimCol1,
            "Modus Wechseln",
            new[]
            {
                ("i", "Insert Modus (vor Cursor)"),
                ("a", "Insert Modus (nach Cursor)"),
                ("o", "Neue Zeile darunter + Insert"),
                ("O", "Neue Zeile darüber + Insert"),
                ("v", "Visual Modus (Zeichenweise)"),
                ("V", "Visual Modus (Zeilenweise)"),
                ("ESC", "Zurück zum Normal Mode")
            }
        );

        AddCategory(
            VimCol1,
            "Bewegung (Normal/Visual)",
            new[]
            {
                ("h", "Links"),
                ("j", "Unten"),
                ("k", "Oben"),
                ("l", "Rechts"),
                ("w", "Wortanfang vorwärts"),
                ("b", "Wortanfang rückwärts"),
                ("e", "Wortende vorwärts"),
                ("W / B", "Wort vor/zurück (nur Leer)"),
                ("0", "Zeilenanfang"),
                ("$", "Zeilenende"),
                ("gg", "Dateianfang"),
                ("G", "Dateiende")
            }
        );

        AddCategory(
            VimCol2,
            "Bearbeiten",
            new[]
            {
                ("x / d", "Zeichen / Markierung löschen"),
                ("dd", "Ganze Zeile löschen"),
                ("D", "Löschen bis Zeilenende"),
                ("dw", "Wort löschen"),
                ("c", "Markierung ersetzen (Insert)"),
                ("u", "Rückgängig (Undo)"),
                ("Ctrl+r", "Wiederholen (Redo)"),
                ("r", "Ein Zeichen ersetzen"),
                ("y / yy", "Kopieren (Yank) / Zeile"),
                ("p", "Einfügen nach Cursor")
            }
        );

        AddCategory(
            VimCol2,
            "System",
            new[]
            {
                (":w", "Speichern"),
                (":q", "Schließen (Simuliert)"),
                (":10", "Zu Zeile 10 springen"),
                ("/", "Suche (Text eingeben + Enter)")
            }
        );
    }

    private void HandleVimNormalInput(KeyEventArgs e)
    {
        var textArea = ActiveEditor.TextArea;
        string keyChar = e.KeySymbol;

        if (e.Key == Key.Up) keyChar = "k";
        else if (e.Key == Key.Down) keyChar = "j";
        else if (e.Key == Key.Left) keyChar = "h";
        else if (e.Key == Key.Right) keyChar = "l";

        if (e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "G";
        else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "D";
        else if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "V";
        else if (e.Key == Key.W && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "W";
        else if (e.Key == Key.B && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "B";
        else if (e.Key == Key.D4 && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "$"; // for linux

        // redo (ctrl + r)
        if (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ActiveEditor.Redo();
            UpdateVimUI();
            return;
        }

        if (string.IsNullOrEmpty(keyChar)) return;

        // handle multi char commands
        if (_vimMode == VimMode.CommandPending)
        {
            CompleteVimCommand(keyChar);
            return;
        }

        // reset saved pos
        if (keyChar != "j" && keyChar != "k") _vimDesiredColumn = -1;

        // single key commands
        switch (keyChar)
        {
            // --- MODE SWITCHING ---
            case "v":
                if (_vimMode == VimMode.Visual)
                {
                    _vimMode = VimMode.Normal;
                    ActiveEditor.TextArea.ClearSelection();
                }
                else
                {
                    _vimMode = VimMode.Visual;
                    _vimVisualStartOffset = ActiveEditor.CaretOffset;
                    UpdateVisualSelection();
                }

                break;
            case "V":
                if (_vimMode == VimMode.VisualLine)
                {
                    _vimMode = VimMode.Normal;
                    ActiveEditor.TextArea.ClearSelection();
                }
                else
                {
                    _vimMode = VimMode.VisualLine;
                    var lineV = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                    _vimVisualStartOffset = lineV.Offset;
                    UpdateVisualSelection();
                }

                break;
            case "i":
                _vimMode = VimMode.Insert;
                break;
            case "a":
                _vimMode = VimMode.Insert;
                var lineA = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                if (ActiveEditor.CaretOffset < lineA.EndOffset) ActiveEditor.CaretOffset++;
                break;
            case "o":
                _vimMode = VimMode.Insert;
                var currentLineO = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                ActiveEditor.CaretOffset = currentLineO.EndOffset;
                textArea.PerformTextInput("\n");
                break;
            case "O":
                _vimMode = VimMode.Insert;
                var currentLineBigO = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                ActiveEditor.CaretOffset = currentLineBigO.Offset;
                textArea.PerformTextInput("\n");
                var newLine = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                if (newLine.PreviousLine != null)
                    ActiveEditor.CaretOffset = newLine.PreviousLine.Offset;
                break;
            case ":":
                _vimMode = VimMode.CommandLine;
                _vimCommandBuffer = ":";
                break;
            case "/":
                _vimMode = VimMode.Search;
                _vimCommandBuffer = "/";
                break;

            // --- NAVIGATION ---
            case "h":
                int lineStart = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset).Offset;
                if (ActiveEditor.CaretOffset > lineStart)
                    ActiveEditor.CaretOffset--;
                _vimDesiredColumn = -1;
                break;
            case "l":
                // clear suggestion when moving right
                if (_csharpAutocompleteService.HasSuggestion)
                {
                    _csharpAutocompleteService.ClearSuggestion();
                    ActiveEditor.TextArea.TextView.Redraw();
                }

                var lineL = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                int maxOffsetL = lineL.Length > 0 ? lineL.EndOffset - 1 : lineL.EndOffset;
                if (ActiveEditor.CaretOffset < maxOffsetL)
                    ActiveEditor.CaretOffset++;
                _vimDesiredColumn = -1;
                break;
            case "j":
                var currentLineJ = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                if (currentLineJ.LineNumber < ActiveEditor.Document.LineCount)
                {
                    if (_vimDesiredColumn == -1)
                        _vimDesiredColumn = ActiveEditor.CaretOffset - currentLineJ.Offset;
                    var nextLine = ActiveEditor.Document.GetLineByNumber(currentLineJ.LineNumber + 1);
                    int maxColJ = nextLine.Length > 0 ? nextLine.Length - 1 : 0;
                    int newOffset = nextLine.Offset + Math.Min(_vimDesiredColumn, maxColJ);
                    ActiveEditor.CaretOffset = newOffset;
                }

                ActiveEditor.TextArea.Caret.BringCaretToView();
                break;
            case "k":
                var currentLineK = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                if (currentLineK.LineNumber > 1)
                {
                    if (_vimDesiredColumn == -1)
                        _vimDesiredColumn = ActiveEditor.CaretOffset - currentLineK.Offset;
                    var prevLine = ActiveEditor.Document.GetLineByNumber(currentLineK.LineNumber - 1);
                    int maxColK = prevLine.Length > 0 ? prevLine.Length - 1 : 0;
                    int newOffset = prevLine.Offset + Math.Min(_vimDesiredColumn, maxColK);
                    ActiveEditor.CaretOffset = newOffset;
                }

                ActiveEditor.TextArea.Caret.BringCaretToView();
                break;
            case "w": // word forward (stop at punctuation)
            {
                int off = ActiveEditor.CaretOffset;
                string text = ActiveEditor.Text;
                char[] delims = new[] { '.', '(', ')', ';', ',', '{', '}', '[', ']' };
                if (off < text.Length)
                {
                    bool startIsSpace = char.IsWhiteSpace(text[off]);
                    bool startIsDelim = delims.Contains(text[off]);

                    // skip current word
                    while (off < text.Length)
                    {
                        bool isSpace = char.IsWhiteSpace(text[off]);
                        bool isDelim = delims.Contains(text[off]);

                        if (startIsSpace && !isSpace) break;
                        if (startIsDelim && !isDelim) break;
                        if (!startIsSpace && !startIsDelim && (isSpace || isDelim)) break;
                        off++;
                    }

                    // skip trailing whitespaces til next word
                    while (off < text.Length && char.IsWhiteSpace(text[off])) off++;
                }

                ActiveEditor.CaretOffset = off;
            }
                break;
            case "b": // word backward (stop at punctuation)
            {
                int off = ActiveEditor.CaretOffset;
                string text = ActiveEditor.Text;
                char[] delims = new[] { '.', '(', ')', ';', ',', '{', '}', '[', ']' };
                if (off > 0)
                {
                    off--;
                    // skip previous whitespaces
                    while (off > 0 && char.IsWhiteSpace(text[off])) off--;

                    bool isDelim = delims.Contains(text[off]);
                    // jump to beginning of word
                    while (off > 0)
                    {
                        char prev = text[off - 1];
                        if (char.IsWhiteSpace(prev) || delims.Contains(prev) != isDelim) break;
                        off--;
                    }
                }

                ActiveEditor.CaretOffset = off;
            }
                break;
            case "e": // end of word
            {
                int off = ActiveEditor.CaretOffset;
                string text = ActiveEditor.Text;
                char[] delims = new[] { '.', '(', ')', ';', ',', '{', '}', '[', ']' };
                if (off < text.Length - 1)
                {
                    off++;
                    // skip whitespaces til next word
                    while (off < text.Length && char.IsWhiteSpace(text[off])) off++;

                    if (off < text.Length)
                    {
                        bool isDelim = delims.Contains(text[off]);
                        // jump to last char of word
                        while (off < text.Length - 1)
                        {
                            char next = text[off + 1];
                            if (char.IsWhiteSpace(next) || delims.Contains(next) != isDelim) break;
                            off++;
                        }
                    }
                }

                ActiveEditor.CaretOffset = Math.Min(off, text.Length > 0 ? text.Length - 1 : 0);
            }
                break;
            case "W": // word forward (ignore punctuation, only space as boundary)
            {
                int off = ActiveEditor.CaretOffset;
                string text = ActiveEditor.Text;
                if (off < text.Length)
                {
                    bool startIsSpace = char.IsWhiteSpace(text[off]);
                    while (off < text.Length && char.IsWhiteSpace(text[off]) == startIsSpace) off++;
                    while (off < text.Length && char.IsWhiteSpace(text[off])) off++;
                }

                ActiveEditor.CaretOffset = off;
            }
                break;
            case "B": // word backward (ignore punctuation, only space as boundary)
            {
                int off = ActiveEditor.CaretOffset;
                string text = ActiveEditor.Text;
                if (off > 0)
                {
                    off--;
                    while (off > 0 && char.IsWhiteSpace(text[off])) off--;
                    while (off > 0 && !char.IsWhiteSpace(text[off - 1])) off--;
                }

                ActiveEditor.CaretOffset = off;
            }
                break;
            case "0": // line start
                var line0 = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                string text0 = ActiveEditor.Document.GetText(line0);
                int indent = 0;
                while (indent < text0.Length && char.IsWhiteSpace(text0[indent]))
                    indent++;
                ActiveEditor.CaretOffset = line0.Offset + indent;
                break;

            case "$": // line end
                var lineEnd1 = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                ActiveEditor.CaretOffset = lineEnd1.Length > 0 ? lineEnd1.EndOffset - 1 : lineEnd1.EndOffset;
                break;

            case "G": // file end
                var lastLine = ActiveEditor.Document.Lines.Last();
                ActiveEditor.CaretOffset = lastLine.Length > 0 ? lastLine.EndOffset - 1 : lastLine.EndOffset;
                ActiveEditor.TextArea.Caret.BringCaretToView();
                break;
            case "D": // delete till line end
                var lineD = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
                int lenD = lineD.Offset + lineD.Length - ActiveEditor.CaretOffset;
                if (lenD > 0)
                {
                    _vimClipboard = ActiveEditor.Document.GetText(ActiveEditor.CaretOffset, lenD);
                    ActiveEditor.Document.Remove(ActiveEditor.CaretOffset, lenD);
                }

                break;

            // --- EDITING ---
            case "d":
            case "x":
                if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine ||
                    !ActiveEditor.TextArea.Selection.IsEmpty)
                {
                    DeleteVisualSelection();
                    return;
                }

                if (keyChar == "d")
                {
                    _vimCommandBuffer = keyChar;
                    _vimPreviousMode = _vimMode;
                    _vimMode = VimMode.CommandPending;
                    _vimDesiredColumn = -1;
                }
                else if (keyChar == "x")
                {
                    if (ActiveEditor.Document.TextLength > ActiveEditor.CaretOffset)
                        ActiveEditor.Document.Remove(ActiveEditor.CaretOffset, 1);
                }

                break;

            case "y":
                if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine ||
                    !ActiveEditor.TextArea.Selection.IsEmpty)
                {
                    YankVisualSelection();
                    return;
                }

                _vimCommandBuffer = keyChar;
                _vimPreviousMode = _vimMode;
                _vimMode = VimMode.CommandPending;
                _vimDesiredColumn = -1;
                break;

            case "c":
                if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine ||
                    !ActiveEditor.TextArea.Selection.IsEmpty)
                {
                    DeleteVisualSelection();
                    _vimMode = VimMode.Insert;
                    UpdateVimUI();
                    return;
                }

                break;

            case "u": // undo
                ActiveEditor.Undo();
                break;
            case "r": // replace single char
                if (ActiveEditor.Document.TextLength > ActiveEditor.CaretOffset)
                {
                    ActiveEditor.Document.Remove(ActiveEditor.CaretOffset, 1);
                    _vimMode = VimMode.Insert;
                }

                break;
            case "p":
                if (!string.IsNullOrEmpty(_vimClipboard))
                    ActiveEditor.Document.Insert(ActiveEditor.CaretOffset, _vimClipboard);
                break;

            // --- MULTI-KEY STARTERS ---
            case "g":
                _vimCommandBuffer = keyChar;
                _vimPreviousMode = _vimMode;
                _vimMode = VimMode.CommandPending;
                _vimDesiredColumn = -1;
                break;
        }

        if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine)
            UpdateVisualSelection();

        UpdateVimUI();
    }

    private void CompleteVimCommand(string key)
    {
        var textArea = ActiveEditor.TextArea;
        string cmd = _vimCommandBuffer + key;

        // --- MOVEMENT COMMANDS ---
        if (cmd == "gg")
        {
            ActiveEditor.CaretOffset = 0;
            ActiveEditor.TextArea.Caret.BringCaretToView();

            if (_vimPreviousMode == VimMode.Visual || _vimPreviousMode == VimMode.VisualLine)
            {
                _vimMode = _vimPreviousMode;
                UpdateVisualSelection();
            }
        }

        // --- DELETION COMMANDS ---
        else if (cmd == "dd")
        {
            var line = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
            _vimClipboard = ActiveEditor.Document.GetText(line.Offset, line.TotalLength); // Save to buffer
            ActiveEditor.Document.Remove(line.Offset, line.TotalLength);
        }
        else if (cmd == "dw")
        {
            int start = ActiveEditor.CaretOffset;
            int nextSpace = ActiveEditor.Text.IndexOfAny(new[] { ' ', '\n', '\t' }, start + 1);
            if (nextSpace == -1) nextSpace = ActiveEditor.Document.TextLength;
            else nextSpace++;

            int len = nextSpace - start;
            _vimClipboard = ActiveEditor.Document.GetText(start, len);
            ActiveEditor.Document.Remove(start, len);
        }

        // --- YANK ---
        else if (cmd == "yy")
        {
            var line = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
            _vimClipboard = ActiveEditor.Document.GetText(line.Offset, line.TotalLength);
            if (_isSqlMode) AddSqlOutput("System", "> Zeile kopiert.", Brushes.LightGray);
            else AddToConsole("\n> Zeile kopiert.", Brushes.LightGray);
        }

        _vimCommandBuffer = "";
        if (_vimMode == VimMode.CommandPending) _vimMode = VimMode.Normal;
        UpdateVimUI();
    }

    private void HandleVimCommandLine(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteVimCommand(_vimCommandBuffer);
            _vimMode = VimMode.Normal;
            _vimCommandBuffer = "";
        }
        else if (e.Key == Key.Back)
        {
            if (_vimCommandBuffer.Length > 1)
            {
                _vimCommandBuffer = _vimCommandBuffer.Substring(0, _vimCommandBuffer.Length - 1);
            }
            else
            {
                _vimMode = VimMode.Normal;
                _vimCommandBuffer = "";
            }
        }
        else if (!string.IsNullOrEmpty(e.KeySymbol) && !char.IsControl(e.KeySymbol[0]))
        {
            _vimCommandBuffer += e.KeySymbol.ToLower();
        }

        UpdateVimUI();
    }

    private void HandleVimSearch(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string searchTerm = _vimCommandBuffer.Substring(1); // remove '/'
            int index = ActiveEditor.Text.IndexOf(searchTerm, ActiveEditor.CaretOffset + 1,
                StringComparison.OrdinalIgnoreCase);
            if (index == -1) // wrap around
                index = ActiveEditor.Text.IndexOf(searchTerm, 0, StringComparison.OrdinalIgnoreCase);

            if (index != -1)
            {
                ActiveEditor.CaretOffset = index;
                ActiveEditor.TextArea.Caret.BringCaretToView();
            }

            _vimMode = VimMode.Normal;
            _vimCommandBuffer = "";
        }
        else if (e.Key == Key.Back)
        {
            if (_vimCommandBuffer.Length > 1)
            {
                _vimCommandBuffer = _vimCommandBuffer.Substring(0, _vimCommandBuffer.Length - 1);
            }
            else
            {
                _vimMode = VimMode.Normal;
                _vimCommandBuffer = "";
            }
        }
        else
        {
            _vimCommandBuffer += e.KeySymbol;
        }

        UpdateVimUI();
    }

    private void ExecuteVimCommand(string cmd)
    {
        if (cmd == ":w" || cmd == ":w!")
        {
            if (_isTutorialMode && _tutorialStep == 6)
            {
                _tutorialStep++;
                UpdateTutorialInstructions();
                return;
            }

            if (_isTutorialMode) return; // fix actually saving during tutorial loop

            SaveCurrentProgress();
            AddToConsole("\n> :w (Gespeichert)", Brushes.LightGreen);
        }
        else if (cmd.StartsWith(":q"))
        {
            if (_isTutorialMode && _tutorialStep == 7)
            {
                _tutorialStep++;
                UpdateTutorialInstructions();
                return;
            }

            if (_isTutorialMode) return; // fix actually quitting during tutorial loop 

            AddToConsole("\n> :q (Wichtig zu testen!)", Brushes.LightGray);
        }
        else if (cmd.StartsWith(":wq"))
        {
            SaveCurrentProgress();
            AddToConsole("\n> :wq (Gespeichert)", Brushes.LightGreen);
        }
        // line jump
        else if (cmd.StartsWith(":") && int.TryParse(cmd.Substring(1), out int lineNum))
        {
            if (lineNum > 0 && lineNum <= ActiveEditor.Document.LineCount)
            {
                var line = ActiveEditor.Document.GetLineByNumber(lineNum);
                ActiveEditor.CaretOffset = line.Offset;
                ActiveEditor.TextArea.Caret.BringCaretToView();
            }
        }
    }

    private void UpdateVimUI()
    {
        bool isVimActive = _isSqlMode ? AppSettings.IsSqlVimEnabled : AppSettings.IsVimEnabled;
        if (_isTutorialMode) isVimActive = true;

        if (!isVimActive)
        {
            if (VimStatusBorder != null) VimStatusBorder.IsVisible = false;
            if (SqlVimStatusBorder != null) SqlVimStatusBorder.IsVisible = false;
            ActiveEditor.Cursor = Cursor.Default;

            if (_csharpBlockCaret != null) _csharpBlockCaret.IsEnabled = false;
            if (_sqlBlockCaret != null) _sqlBlockCaret.IsEnabled = false;
            if (_tutorialBlockCaret != null) _tutorialBlockCaret.IsEnabled = false;

            CodeEditor.TextArea.Caret.CaretBrush = Brushes.White;
            SqlQueryEditor.TextArea.Caret.CaretBrush = Brushes.White;
            if (TutorialEditor != null) TutorialEditor.TextArea.Caret.CaretBrush = Brushes.White;

            // invalidate layer so block caret disappears immediately
            CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);
            SqlQueryEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);
            if (TutorialEditor != null) TutorialEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);

            CodeEditor.TextArea.TextView.Redraw();
            SqlQueryEditor.TextArea.TextView.Redraw();
            if (TutorialEditor != null) TutorialEditor.TextArea.TextView.Redraw();

            return;
        }

        bool isNormal = _vimMode == VimMode.Normal;
        if (_csharpBlockCaret != null) _csharpBlockCaret.IsEnabled = isNormal && AppSettings.IsVimEnabled;
        if (_sqlBlockCaret != null) _sqlBlockCaret.IsEnabled = isNormal && AppSettings.IsSqlVimEnabled;
        if (_tutorialBlockCaret != null) _tutorialBlockCaret.IsEnabled = isNormal && _isTutorialMode;

        var caretBrush = isNormal ? Brushes.Transparent : Brushes.White;
        CodeEditor.TextArea.Caret.CaretBrush = caretBrush;
        SqlQueryEditor.TextArea.Caret.CaretBrush = caretBrush;
        if (TutorialEditor != null) TutorialEditor.TextArea.Caret.CaretBrush = caretBrush;

        CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);
        SqlQueryEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);
        if (TutorialEditor != null) TutorialEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Caret);

        Border activeBorder = _isTutorialMode ? TutorialVimStatusBorder :
            _isSqlMode ? SqlVimStatusBorder : VimStatusBorder;
        TextBlock activeText = _isTutorialMode ? TutorialVimStatusBar : _isSqlMode ? SqlVimStatusBar : VimStatusBar;

        if (activeBorder == null || activeText == null) return;

        // hide the inactive ones
        if (_isTutorialMode)
        {
            if (VimStatusBorder != null) VimStatusBorder.IsVisible = false;
            if (SqlVimStatusBorder != null) SqlVimStatusBorder.IsVisible = false;
        }
        else
        {
            if (_isSqlMode && VimStatusBorder != null) VimStatusBorder.IsVisible = false;
            else if (!_isSqlMode && SqlVimStatusBorder != null) SqlVimStatusBorder.IsVisible = false;
        }

        activeBorder.IsVisible = true;

        switch (_vimMode)
        {
            case VimMode.Normal:
                activeBorder.Background = SolidColorBrush.Parse("#007ACC");
                activeText.Text = "-- NORMAL --";
                break;
            case VimMode.Insert:
                activeBorder.Background = SolidColorBrush.Parse("#28a745");
                activeText.Text = "-- INSERT --";
                break;
            case VimMode.CommandPending:
                activeBorder.Background = SolidColorBrush.Parse("#d08770");
                activeText.Text = _vimCommandBuffer;
                break;
            case VimMode.CommandLine:
            case VimMode.Search:
                activeBorder.Background = SolidColorBrush.Parse("#444");
                activeText.Text = _vimCommandBuffer;
                break;
            case VimMode.Visual:
                activeBorder.Background = SolidColorBrush.Parse("#8A2BE2");
                activeText.Text = "-- VISUAL --";
                break;
            case VimMode.VisualLine:
                activeBorder.Background = SolidColorBrush.Parse("#8A2BE2");
                activeText.Text = "-- VISUAL LINE --";
                break;
        }
    }

    private void EndVimTutorial()
    {
        _isTutorialMode = false;
        RenderTutorialTask(
            "🎉 Tutorial abgeschlossen! 🎉\nNun kannst du die Grundenlange von Vim und kannst anfangen dich an die Steuerung zu gewöhnen. Mit ein wenig Übung und Willenskraft, wirst du dadurch produktiver, aber vor allem musst du nicht mehr so oft zur Maus greifen.");

        var duration = DateTime.Now - _tutorialStart;

        int baseScore = 12000;
        int score = baseScore - (int)(duration.TotalSeconds * 30) - _tutorialMouseClicks * 2000 -
                    _tutorialKeystrokes * 20 - _tutorialPenalty;

        // score buffer for motivation (degree in psychology acquired)
        if (score < 1500) score = 1500 + new Random().Next(100, 500);

        // highscore check
        bool isNewHighscore = false;
        if (score > playerData.Settings.VimTutorialHighscore)
        {
            playerData.Settings.VimTutorialHighscore = score;
            SaveSystem.Save(playerData);
            isNewHighscore = true;
        }

        // build stats panel
        PnlTutorialStatsContent.Children.Clear();

        var statsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 20,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var timePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };
        timePanel.Children.Add(LoadIcon("assets/icons/ic_timer.svg", 20));
        timePanel.Children.Add(new TextBlock
        {
            Text = $"{duration.TotalSeconds:F1}s",
            FontSize = 16,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        statsRow.Children.Add(timePanel);

        var keyPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };
        keyPanel.Children.Add(LoadIcon("assets/icons/ic_keyboard.svg", 20));
        keyPanel.Children.Add(new TextBlock
        {
            Text = $"{_tutorialKeystrokes}",
            Foreground = Brushes.White,
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        statsRow.Children.Add(keyPanel);

        var mousePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };
        mousePanel.Children.Add(LoadIcon("assets/icons/ic_mouse.svg", 20));
        mousePanel.Children.Add(new TextBlock
        {
            Text = $"{_tutorialMouseClicks}",
            Foreground = _tutorialMouseClicks > 0 ? SolidColorBrush.Parse("#B43232") : Brushes.White,
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        });
        statsRow.Children.Add(mousePanel);

        PnlTutorialStatsContent.Children.Add(statsRow);

        var scoreRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };
        scoreRow.Children.Add(LoadIcon("assets/icons/ic_trophy.svg", 28));
        scoreRow.Children.Add(new TextBlock
        {
            Text = $"Score: {score}",
            Foreground = SolidColorBrush.Parse("#FFD700"),
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });
        PnlTutorialStatsContent.Children.Add(scoreRow);

        if (isNewHighscore)
            PnlTutorialStatsContent.Children.Add(new TextBlock
            {
                Text = "NEUER HIGHSCORE!",
                Foreground = SolidColorBrush.Parse("#32A852"),
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
        else
            PnlTutorialStatsContent.Children.Add(new TextBlock
            {
                Text = $"Bisheriger Highscore: {playerData.Settings.VimTutorialHighscore}",
                Foreground = Brushes.Gray,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            });

        PnlTutorialStats.IsVisible = true;
        UpdateVimUI();
    }

    private void TutorialEditor_KeyDown(object sender, KeyEventArgs e)
    {
        // ignore standalone modifier keys
        bool isModifier = e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                          e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                          e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                          e.Key == Key.LWin || e.Key == Key.RWin;

        if (!isModifier) _tutorialKeystrokes++;

        if (e.Key == Key.Escape)
        {
            _vimMode = VimMode.Normal;
            _vimCommandBuffer = "";
            ActiveEditor.TextArea.ClearSelection();

            var line = ActiveEditor.Document.GetLineByOffset(ActiveEditor.CaretOffset);
            if (ActiveEditor.CaretOffset == line.EndOffset && line.Length > 0)
                ActiveEditor.CaretOffset--;

            UpdateVimUI();
            e.Handled = true;
            CheckTutorialProgress();
            return;
        }

        if (_vimMode == VimMode.Insert)
        {
            CheckTutorialProgress();
            return;
        }

        if (_vimMode == VimMode.CommandLine)
        {
            HandleVimCommandLine(e);
            e.Handled = true;
            CheckTutorialProgress();
            return;
        }

        if (_vimMode == VimMode.Search)
        {
            HandleVimSearch(e);
            e.Handled = true;
            CheckTutorialProgress();
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            // zoom via ctrl + +/-
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                AppSettings.EditorFontSize = Math.Min(48, AppSettings.EditorFontSize + 1);
                TutorialEditor.FontSize = AppSettings.EditorFontSize;
                CodeEditor.FontSize = AppSettings.EditorFontSize;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                AppSettings.EditorFontSize = Math.Max(8, AppSettings.EditorFontSize - 1);
                TutorialEditor.FontSize = AppSettings.EditorFontSize;
                CodeEditor.FontSize = AppSettings.EditorFontSize;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V)
            {
                // catch those cheaters
                _tutorialPenalty += 10000;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C || e.Key == Key.X || e.Key == Key.A) return;
        }

        e.Handled = true;
        HandleVimNormalInput(e);
        CheckTutorialProgress();
    }

    private void TutorialEditor_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        _tutorialMouseClicks++;
    }

    private void TutorialEditor_PointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        // zoom via ctrl + mwheel
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (e.Delta.Y > 0)
                AppSettings.EditorFontSize = Math.Min(48, AppSettings.EditorFontSize + 1);
            else if (e.Delta.Y < 0) AppSettings.EditorFontSize = Math.Max(8, AppSettings.EditorFontSize - 1);
            TutorialEditor.FontSize = AppSettings.EditorFontSize;
            CodeEditor.FontSize = AppSettings.EditorFontSize;
            e.Handled = true;
        }
    }

    private void BtnStartTutorial_Click(object sender, RoutedEventArgs e)
    {
        PnlVimCheatSheet.IsVisible = false;
        PnlVimTutorial.IsVisible = true;
        PnlTutorialStats.IsVisible = false;
        _isTutorialMode = true;
        _tutorialStep = 1;
        _tutorialKeystrokes = 0;
        _tutorialMouseClicks = 0;
        _tutorialPenalty = 0;
        _vimClipboard = "";
        _tutorialStart = DateTime.Now;
        _vimMode = VimMode.Normal;

        // set min height (stop editor from jumping around)
        TxtTutorialTask?.MinHeight = 100;

        // lock other editors
        CodeEditor.IsReadOnly = true;
        SqlQueryEditor.IsReadOnly = true;

        TutorialEditor.Text = @"public class VimTutorial
{
    // 1. Navigation (h, j, k, l) & Modi
    string mode = ""NORMAL"";
    // change_me_to_insert

    // 2. Insert (i) vs Append (a)
    string word1 = ""ng"";
    string word2 = ""in"";

    // 3. Navigation (0, $) & Loeschen (x, dd)
    int errorMessageCode = 404X;
    char c; // loesche_nur_mich

    // 4. Zeilensprung (:line) & Kopieren (yy, p)
    // jump_here_and_copy


    // gebe lebenssignal aus
    Console.WriteLine(""Hello, World!"");

    // methode anrufen
    Methode();


    // 5. Suchen (/)
    string secret = ""Schatz"";
}
";
        TutorialEditor.CaretOffset = 0;
        UpdateTutorialInstructions();
        UpdateVimUI();

        // focus text area
        Dispatcher.UIThread.Post(() =>
        {
            TutorialEditor.Focus();
            TutorialEditor.TextArea.Focus();
        }, DispatcherPriority.Render);
    }

    private void BtnExitTutorial_Click(object sender, RoutedEventArgs e)
    {
        PnlVimTutorial.IsVisible = false;
        PnlVimCheatSheet.IsVisible = true;
        _isTutorialMode = false;
        _vimMode = VimMode.Normal;

        // unlock other editors
        CodeEditor.IsReadOnly = false;
        SqlQueryEditor.IsReadOnly = false;

        UpdateVimUI();

        if (_isSqlMode) SqlQueryEditor.Focus();
        else CodeEditor.Focus();
    }

    private void RenderTutorialTask(string text)
    {
        TxtTutorialTask.Inlines?.Clear();
        if (TxtTutorialTask.Inlines == null) TxtTutorialTask.Inlines = new InlineCollection();

        // extract ((keys)) and [code] segments
        var parts = Regex.Split(text, @"(\(\(.*?\)\)|\[.*?\])");
        foreach (var part in parts)
            if (part.StartsWith("((") && part.EndsWith("))"))
            {
                string key = part.Substring(2, part.Length - 4);

                var keyBorder = new Border
                {
                    Background = SolidColorBrush.Parse("#444"),
                    BorderBrush = SolidColorBrush.Parse("#666"),
                    BorderThickness = new Thickness(1, 1, 1, 3),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 0),
                    Margin = new Thickness(2, 0, 2, -4),
                    Child = new TextBlock
                    {
                        Text = key,
                        Foreground = Brushes.White,
                        FontFamily = new FontFamily(MonospaceFontFamily),
                        FontWeight = FontWeight.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                TxtTutorialTask.Inlines.Add(new InlineUIContainer { Child = keyBorder });
            }
            else if (part.StartsWith("[") && part.EndsWith("]"))
            {
                string content = part.Substring(1, part.Length - 2);
                TxtTutorialTask.Inlines.Add(new Run
                {
                    Text = content,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextHighlight,
                    FontFamily = new FontFamily(MonospaceFontFamily)
                });
            }
            else if (!string.IsNullOrEmpty(part))
            {
                TxtTutorialTask.Inlines.Add(new Run { Text = part });
            }
    }

    private void UpdateTutorialInstructions()
    {
        switch (_tutorialStep)
        {
            case 1:
                RenderTutorialTask(
                    "Aufgabe 1/7 (Navigation & Modi): Vim startet im NORMAL-Modus. Nutze ((h)) (links), ((j)) (runter), ((k)) (hoch), ((l)) (rechts) zur Navigation. Gehe zu [change_me_to_insert], lösche die Zeile mit ((d))((d)), drücke ((i)) (Insert), tippe [//erledigt] und beende mit ((ESC)).");
                break;
            case 2:
                RenderTutorialTask(
                    "Aufgabe 2/7 (Insert vs Append): Setze den Cursor bei [ng] auf das [n], drücke ((i)) (Insert VOR Cursor), tippe [i] und drücke ((ESC)). Gehe dann bei [in] auf das [n], drücke ((a)) (Append NACH Cursor), tippe [g] und drücke ((ESC)).");
                break;
            case 3:
                RenderTutorialTask(
                    "Aufgabe 3/7 (Schnelle Navigation & Löschen): Gehe in die [404X]-Zeile. Drücke (($)) um ans Zeilenende zu springen, lösche [X] mit ((x)). Gehe eine Zeile tiefer, drücke ((0)) für den Zeilenanfang, gehe zu [//] und lösche nur den Kommentar mit ((D)).");
                break;
            case 4:
                // dynamically find target line
                int targetLine = 1;
                var lines = TutorialEditor.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains("// jump_here_and_copy"))
                    {
                        targetLine = i + 1;
                        break;
                    }

                string lineStr = targetLine.ToString();
                string keys = string.Join("", lineStr.Select(c => $"(({c}))"));

                RenderTutorialTask(
                    $"Aufgabe 4/7 (Springen & Kopieren): Tippe ((:)){keys} und ((Enter)) um exakt zu Zeile {targetLine} zu springen. Kopiere die Zeile mit ((y))((y)) (Yank) und füge sie mit ((p)) (Paste) darunter ein.");
                break;
            case 5:
                RenderTutorialTask(
                    "Aufgabe 5/7 (Suchen): Drücke ((/)), tippe [Schatz] und drücke ((Enter)) um das Wort zu finden. Lösche es, wechsle in den Insert-Modus ((i)), tippe [Gold] und drücke ((ESC)).");
                break;
            case 6:
                RenderTutorialTask(
                    "Aufgabe 6/7 (Speichern): Fast geschafft! Speichere das Dokument im Normal-Modus, indem du ((:))((w)) (write) tippst und mit ((Enter)) bestätigst.");
                break;
            case 7:
                RenderTutorialTask(
                    "Aufgabe 7/7 (Beenden): Das Wichtigste zum Schluss: Wie verlässt man Vim? Tippe im Normal-Modus ((:))((q)) (quit) und drücke ((Enter)).\nNotiz: In dieser App ist das Verlassen von Vim nicht nötig, jedoch ist es sehr wichtig zu wissen.");
                break;
            case 8:
                EndVimTutorial();
                break;
        }
    }

    private void CheckTutorialProgress()
    {
        if (!_isTutorialMode) return;
        string t = TutorialEditor.Text;

        switch (_tutorialStep)
        {
            case 1:
                if (_vimMode == VimMode.Normal && (t.Contains("// erledigt") || t.Contains("//erledigt")) &&
                    !t.Contains("change_me_to_insert"))
                {
                    _tutorialStep++;
                    UpdateTutorialInstructions();
                }

                break;
            case 2:
                if (_vimMode == VimMode.Normal && Regex.Matches(t, "\"ing\"").Count >= 2)
                {
                    _tutorialStep++;
                    UpdateTutorialInstructions();
                }

                break;
            case 3:
                if (_vimMode == VimMode.Normal && t.Contains("404;") && !t.Contains("404X") && t.Contains("char c;") &&
                    !t.Contains("// loesche_nur_mich"))
                {
                    _tutorialStep++;
                    UpdateTutorialInstructions();
                }

                break;
            case 4:
                if (_vimMode == VimMode.Normal && Regex.Matches(t, "// jump_here_and_copy").Count >= 2)
                {
                    _tutorialStep++;
                    UpdateTutorialInstructions();
                }

                break;
            case 5:
                if (_vimMode == VimMode.Normal && t.Contains("\"Gold\"") && !t.Contains("\"Schatz\""))
                {
                    _tutorialStep++;
                    UpdateTutorialInstructions();
                }

                break;
        }
    }

    private void ConfigureTutorialEditor()
    {
        TutorialEditor.Options.ConvertTabsToSpaces = true;
        TutorialEditor.Options.IndentationSize = 4;
        TutorialEditor.Options.ShowSpaces = false;
        TutorialEditor.Options.ShowTabs = false;
        TutorialEditor.Options.HighlightCurrentLine = true;

        TutorialEditor.FontFamily = new FontFamily(MonospaceFontFamily);
        TutorialEditor.FontSize = AppSettings.EditorFontSize;
        TutorialEditor.Background = Brushes.Transparent;
        TutorialEditor.Foreground = SolidColorBrush.Parse("#D4D4D4");
        TutorialEditor.SyntaxHighlighting = CsharpCodeEditor.GetDarkCsharpHighlighting();

        _tutorialBlockCaret = new VimBlockCaretRenderer(TutorialEditor);
        TutorialEditor.TextArea.TextView.BackgroundRenderers.Add(_tutorialBlockCaret);

        TutorialEditor.AddHandler(KeyDownEvent, TutorialEditor_KeyDown, RoutingStrategies.Tunnel);
        TutorialEditor.AddHandler(PointerPressedEvent, TutorialEditor_PointerPressed, RoutingStrategies.Tunnel);
        TutorialEditor.AddHandler(PointerWheelChangedEvent, TutorialEditor_PointerWheelChanged,
            RoutingStrategies.Tunnel);
        TutorialEditor.TextArea.TextEntering += Editor_TextEntering;

        TutorialEditor.TextArea.Caret.PositionChanged += (s, e) =>
        {
            // clamp caret in vim normal mode
            if (_isTutorialMode && _vimMode == VimMode.Normal)
            {
                var line = TutorialEditor.Document.GetLineByOffset(TutorialEditor.CaretOffset);
                if (TutorialEditor.CaretOffset == line.EndOffset && line.Length > 0) TutorialEditor.CaretOffset--;
            }
        };

        TutorialEditor.TextChanged += (s, e) => { CheckTutorialProgress(); };
    }

    private void UpdateVisualSelection()
    {
        if (_vimVisualStartOffset == -1) return;

        int start = Math.Min(_vimVisualStartOffset, ActiveEditor.CaretOffset);
        int end = Math.Max(_vimVisualStartOffset, ActiveEditor.CaretOffset);

        if (_vimMode == VimMode.VisualLine)
        {
            var startLine = ActiveEditor.Document.GetLineByOffset(start);
            var endLine = ActiveEditor.Document.GetLineByOffset(end);
            start = startLine.Offset;
            end = endLine.EndOffset;
        }
        else
        {
            // visual selection includes the char under the caret
            if (end < ActiveEditor.Document.TextLength) end++;
        }

        ActiveEditor.TextArea.Selection = Selection.Create(ActiveEditor.TextArea, start, end);
    }

    private void YankVisualSelection()
    {
        if (ActiveEditor.TextArea.Selection.IsEmpty) return;
        _vimClipboard = ActiveEditor.TextArea.Selection.GetText();
        ActiveEditor.TextArea.ClearSelection();
        _vimMode = VimMode.Normal;
        if (_isSqlMode) AddSqlOutput("System", "> Text kopiert.", Brushes.LightGray);
        else AddToConsole("\n> Text kopiert.", Brushes.LightGray);
    }

    private void DeleteVisualSelection()
    {
        if (ActiveEditor.TextArea.Selection.IsEmpty) return;
        _vimClipboard = ActiveEditor.TextArea.Selection.GetText();
        int offset = ActiveEditor.TextArea.Selection.SurroundingSegment.Offset;
        ActiveEditor.TextArea.Selection.ReplaceSelectionWithText("");
        ActiveEditor.CaretOffset = offset;
        ActiveEditor.TextArea.ClearSelection();
        _vimMode = VimMode.Normal;
    }

    private enum VimMode
    {
        Normal,
        Insert,
        CommandPending,
        CommandLine,
        Search,
        Visual,
        VisualLine
    }
}