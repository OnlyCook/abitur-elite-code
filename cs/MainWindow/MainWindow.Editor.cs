using System;
using System.Text.RegularExpressions;
using AbiturEliteCode.cs.MainWindow;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Editing;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private void ConfigureEditor()
    {
        CodeEditor.Options.ConvertTabsToSpaces = true;
        CodeEditor.Options.IndentationSize = 4;
        CodeEditor.Options.ShowSpaces = false;
        CodeEditor.Options.ShowTabs = false;
        CodeEditor.Options.EnableHyperlinks = false;
        CodeEditor.Options.EnableEmailHyperlinks = false;
        CodeEditor.Options.HighlightCurrentLine = true;

        CodeEditor.FontFamily = new FontFamily(MonospaceFontFamily);
        CodeEditor.FontSize = AppSettings.EditorFontSize;
        CodeEditor.Background = Brushes.Transparent;
        CodeEditor.Foreground = SolidColorBrush.Parse("#D4D4D4");

        _indentationGuideRenderer = new IndentationGuideRenderer(CodeEditor);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentationGuideRenderer);

        _bracketHighlightRenderer = new BracketHighlightRenderer(CodeEditor);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_bracketHighlightRenderer);

        _textMarkerService = new TextMarkerService(CodeEditor);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);

        _unusedCodeTransformer = new UnusedCodeTransformer(CodeEditor);
        CodeEditor.TextArea.TextView.LineTransformers.Add(_unusedCodeTransformer);

        _escapeSequenceTransformer = new EscapeSequenceTransformer();
        CodeEditor.TextArea.TextView.LineTransformers.Add(_escapeSequenceTransformer);

        _semanticClassTransformer = new SemanticClassHighlightingTransformer();
        CodeEditor.TextArea.TextView.LineTransformers.Add(_semanticClassTransformer);

        _ghostCharTransformer = new GhostCharacterTransformer(CodeEditor);
        CodeEditor.TextArea.TextView.LineTransformers.Add(_ghostCharTransformer);

        _csharpBlockCaret = new VimBlockCaretRenderer(CodeEditor);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_csharpBlockCaret);

        var csSelectionHighlightRenderer = new SelectionHighlightRenderer(CodeEditor);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(csSelectionHighlightRenderer);

        _csharpAutocompleteService = new AutocompleteService(AutocompleteService.CsharpKeywords);
        _csharpAutocompleteGenerator = new AutocompleteGhostGenerator(CodeEditor, _csharpAutocompleteService);
        CodeEditor.TextArea.TextView.ElementGenerators.Add(_csharpAutocompleteGenerator);

        // wait 600ms after typing
        _diagnosticTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _diagnosticTimer.Tick += (s, e) =>
        {
            _diagnosticTimer.Stop();
            UpdateDiagnostics();
        };

        CodeEditor.TextArea.Caret.PositionChanged += (s, e) =>
        {
            // clamp caret in vim normal mode
            if (AppSettings.IsVimEnabled && _vimMode == VimMode.Normal)
            {
                var line = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                if (CodeEditor.CaretOffset == line.EndOffset && line.Length > 0) CodeEditor.CaretOffset--;
            }

            CodeEditor.TextArea.Caret.BringCaretToView(40);
            CodeEditor.TextArea.TextView.Redraw();

            if (AppSettings.IsAutocompleteEnabled)
            {
                _csharpAutocompleteService.UpdateSuggestion(CodeEditor.Text, CodeEditor.CaretOffset);
                CodeEditor.TextArea.TextView.Redraw();
            }
        };

        CodeEditor.TextArea.TextEntering += Editor_TextEntering;
        CodeEditor.AddHandler(KeyDownEvent, CodeEditor_KeyDown, RoutingStrategies.Tunnel);

        CodeEditor.AddHandler(PointerWheelChangedEvent, CodeEditor_PointerWheelChanged, RoutingStrategies.Tunnel);

        CodeEditor.PointerMoved += CodeEditor_PointerMoved;

        CodeEditor.TextChanged += (s, e) =>
        {
            autoSaveTimer.Stop();
            autoSaveTimer.Start();

            UpdateSemanticHighlighting();

            if (AppSettings.IsErrorHighlightingEnabled)
            {
                _diagnosticTimer.Stop();
                _diagnosticTimer.Start();
            }

            if (AppSettings.IsAutocompleteEnabled)
            {
                if (_suppressCsharpAutocomplete)
                {
                    _suppressCsharpAutocomplete = false;
                }
                else
                {
                    _csharpAutocompleteService.ScanTokens(CodeEditor.Text);
                    _csharpAutocompleteService.UpdateSuggestion(CodeEditor.Text, CodeEditor.CaretOffset);
                    CodeEditor.TextArea.TextView.Redraw();
                }
            }
        };

        // fix 1 pixel vertical misalignment
        foreach (var margin in CodeEditor.TextArea.LeftMargins)
            if (margin is LineNumberMargin lineMargin)
                lineMargin.Margin = new Thickness(0, 1, 0, 0);
    }

    private void CodeEditor_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!AppSettings.IsErrorExplanationEnabled)
        {
            ToolTip.SetTip(CodeEditor, null);
            return;
        }

        var textView = CodeEditor.TextArea.TextView;
        var pos = e.GetPosition(textView);
        var posInDoc = textView.GetPosition(pos + textView.ScrollOffset);

        if (posInDoc.HasValue)
        {
            int offset = CodeEditor.Document.GetOffset(posInDoc.Value.Location);
            var marker = _textMarkerService.GetMarkerAtOffset(offset);

            if (marker != null && !string.IsNullOrEmpty(marker.Message))
            {
                ToolTip.SetTip(CodeEditor, marker.Message);
                return;
            }
        }

        ToolTip.SetTip(CodeEditor, null);
    }

    private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (_isTutorialMode)
        {
            e.Handled = true;
            return;
        }

        // escape key to clear suggestions
        if (e.Key == Key.Escape && _csharpAutocompleteService.HasSuggestion)
        {
            _csharpAutocompleteService.ClearSuggestion();
            CodeEditor.TextArea.TextView.Redraw();
            e.Handled = true;
            return;
        }

        // up and down arrows for autocompletion cycling
        if (AppSettings.IsAutocompleteEnabled && (e.Key == Key.Up || e.Key == Key.Down) &&
            _csharpAutocompleteService.HasSuggestion)
        {
            if (e.Key == Key.Up) _csharpAutocompleteService.CyclePrevious();
            else _csharpAutocompleteService.CycleNext();

            CodeEditor.TextArea.TextView.Redraw();
            e.Handled = true;
            return;
        }

        // ctrl/cmd + +/- => zoom
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (e.Key == Key.OemPlus || e.Key == Key.Add)
            {
                AppSettings.EditorFontSize = Math.Min(48, AppSettings.EditorFontSize + 1);
                CodeEditor.FontSize = AppSettings.EditorFontSize;
                TutorialEditor?.FontSize = AppSettings.EditorFontSize;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
            {
                AppSettings.EditorFontSize = Math.Max(8, AppSettings.EditorFontSize - 1);
                CodeEditor.FontSize = AppSettings.EditorFontSize;
                TutorialEditor?.FontSize = AppSettings.EditorFontSize;
                e.Handled = true;
                return;
            }
        }

        // ctrl/cmd + shift + z => redo
        if (e.Key == Key.Z &&
            (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            CodeEditor.Redo();
            e.Handled = true;
            return;
        }

        // ctrl + s => save
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            if (_isDesignerMode)
            {
                SaveDesignerDraft();
                e.Handled = true;
                return;
            }

            SaveCurrentProgress();
            AddToConsole("\n> Gespeichert.", Brushes.LightGray);
            e.Handled = true;
            return;
        }

        // f5 => run
        if (e.Key == Key.F5)
        {
            if (_isDesignerMode)
            {
                // only allow if currently editing test code
                if (_activeDesignerSource == DesignerSource.TestingCode)
                    BtnRun_Click(this, null);
                else
                    AddToConsole("\n> Ausführen im Designer nur im 'Test-Code' Editor möglich.", Brushes.LightGray);
                e.Handled = true;
                return;
            }

            BtnRun_Click(this, null);
            e.Handled = true;
            return;
        }

        // tab => confirm autocompletion
        if (AppSettings.IsAutocompleteEnabled && e.Key == Key.Tab && _csharpAutocompleteService.HasSuggestion)
            if (!AppSettings.IsVimEnabled || _vimMode == VimMode.Insert)
            {
                string suffixText = _csharpAutocompleteService.CurrentSuggestionSuffix;
                if (!string.IsNullOrEmpty(suffixText))
                {
                    int offset = CodeEditor.CaretOffset;

                    _csharpAutocompleteService.ClearSuggestion();

                    CodeEditor.Document.Insert(offset, suffixText);
                    CodeEditor.CaretOffset = offset + suffixText.Length;
                    e.Handled = true;
                    return;
                }
            }

        // temporarily disable autocompletion if moving to the right
        if (e.Key == Key.Right && _csharpAutocompleteService.HasSuggestion)
        {
            _csharpAutocompleteService.ClearSuggestion();
            CodeEditor.TextArea.TextView.Redraw();
            e.Handled = true; // handle so user not moved next line
        }

        if (e.Key == Key.Back)
        {
            if (AppSettings.IsAutocompleteEnabled && _csharpAutocompleteService.HasSuggestion)
            {
                _csharpAutocompleteService.ClearSuggestion();
                CodeEditor.TextArea.TextView.Redraw();
                _suppressCsharpAutocomplete = true;
            }

            if (AppSettings.IsVimEnabled && _vimMode == VimMode.Normal)
            {
                e.Handled = true;
                return;
            }

            int offset = CodeEditor.CaretOffset;

            // smart delete pairs
            if (offset > 0 && offset < CodeEditor.Document.TextLength)
            {
                char charBefore = CodeEditor.Document.GetCharAt(offset - 1);
                char charAfter = CodeEditor.Document.GetCharAt(offset);

                if (
                    (charBefore == '(' && charAfter == ')')
                    || (charBefore == '{' && charAfter == '}')
                    || (charBefore == '[' && charAfter == ']')
                    || (charBefore == '"' && charAfter == '"')
                    || (charBefore == '<' && charAfter == '>')
                )
                {
                    CodeEditor.Document.Remove(offset - 1, 2);
                    e.Handled = true;
                    return;
                }
            }

            // remove whole indentation (4 spaces)
            if (CodeEditor.SelectionLength == 0 && offset >= 4)
            {
                string textToCheck = CodeEditor.Document.GetText(offset - 4, 4);
                if (textToCheck == "    ")
                {
                    CodeEditor.Document.Remove(offset - 4, 4);
                    e.Handled = true;
                }
            }
        }

        // -- vim logic --

        if (!AppSettings.IsVimEnabled) return;

        if (e.Key == Key.Escape)
        {
            _vimMode = VimMode.Normal;
            _vimCommandBuffer = "";
            CodeEditor.TextArea.ClearSelection();

            var line = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
            if (CodeEditor.CaretOffset == line.EndOffset && line.Length > 0)
                CodeEditor.CaretOffset--;

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

    private void CodeEditor_PointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        // zoom via ctrl + mwheel
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            if (e.Delta.Y > 0)
                AppSettings.EditorFontSize = Math.Min(48, AppSettings.EditorFontSize + 1);
            else if (e.Delta.Y < 0) AppSettings.EditorFontSize = Math.Max(8, AppSettings.EditorFontSize - 1);
            CodeEditor.FontSize = AppSettings.EditorFontSize;
            if (TutorialEditor != null) TutorialEditor.FontSize = AppSettings.EditorFontSize;
            e.Handled = true;
        }
    }

    private void Editor_TextEntering(object sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
            return;

        char charTyped = e.Text[0];
        TextArea textArea = (TextArea)sender;
        int offset = textArea.Caret.Offset;

        // auto remove auto added chevron
        if (charTyped == ' ')
            if (offset > 0 && offset < textArea.Document.TextLength)
                if (textArea.Document.GetCharAt(offset - 1) == '<' && textArea.Document.GetCharAt(offset) == '>')
                    textArea.Document.Remove(offset, 1);

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
            if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"' || charTyped == '\'' ||
                charTyped == '<')
            {
                string startChar = charTyped.ToString();
                string endChar = charTyped == '(' ? ")"
                    : charTyped == '{' ? "}"
                    : charTyped == '[' ? "]"
                    : charTyped == '<' ? ">"
                    : charTyped.ToString();

                string selectedText = textArea.Selection.GetText();
                int selectionStart = textArea.Selection.SurroundingSegment.Offset;

                textArea.Selection.ReplaceSelectionWithText(startChar + selectedText + endChar);
                textArea.Caret.Offset = selectionStart + selectedText.Length + 2;

                e.Handled = true;
                return;
            }

        // auto add designated pair
        if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"' || charTyped == '\'' ||
            charTyped == '<')
        {
            if (charTyped == '<')
            {
                bool isGenericContext = false;
                if (offset > 0)
                {
                    char prevChar = textArea.Document.GetCharAt(offset - 1);

                    // if preceded by whitespace -> its likely an operator
                    // if preceded by a digit -> its likely an operator
                    if (!char.IsWhiteSpace(prevChar) && !char.IsDigit(prevChar))
                    {
                        // check for control structures
                        var line = textArea.Document.GetLineByOffset(offset);
                        string lineTextToCaret = textArea.Document.GetText(line.Offset, offset - line.Offset);

                        bool isInsideControlCondition = Regex.IsMatch(lineTextToCaret, @"\b(if|while|for)\s*\(");

                        if (!isInsideControlCondition) isGenericContext = true;
                    }
                }

                if (!isGenericContext) return; // let insert normally as operator
            }

            string pair =
                charTyped == '(' ? ")"
                : charTyped == '{' ? "}"
                : charTyped == '[' ? "]"
                : charTyped == '<' ? ">"
                : charTyped == '\"' ? "\""
                : "\'";

            textArea.Document.Insert(offset, charTyped + pair);
            textArea.Caret.Offset = offset + 1;
            e.Handled = true;
            return;
        }

        // skip closing pair
        if (charTyped == ')' || charTyped == '}' || charTyped == ']' || charTyped == '"' || charTyped == '\'' ||
            charTyped == '>')
            if (
                offset < textArea.Document.TextLength
                && textArea.Document.GetCharAt(offset) == charTyped
            )
            {
                textArea.Caret.Offset += 1;
                e.Handled = true;
                return;
            }

        if (e.Text == "\n" || e.Text == "\r")
        {
            char prev = offset > 0 ? textArea.Document.GetCharAt(offset - 1) : '\0';
            char next =
                offset < textArea.Document.TextLength
                    ? textArea.Document.GetCharAt(offset)
                    : '\0';

            var currentLine = textArea.Document.GetLineByOffset(offset);
            string lineText = textArea.Document.GetText(currentLine);

            string indent = "";
            foreach (char c in lineText)
                if (char.IsWhiteSpace(c))
                    indent += c;
                else
                    break;

            if (prev == '{' && next == '}')
            {
                string insertion = "\n" + indent + "    " + "\n" + indent;
                textArea.Document.Insert(offset, insertion);
                textArea.Caret.Offset = offset + indent.Length + 5;
                e.Handled = true;
                return;
            }

            string trimmed = lineText.TrimEnd();
            if (trimmed.EndsWith("{"))
                indent += "    ";

            textArea.Document.Insert(offset, "\n" + indent);
            e.Handled = true;
        }
    }
}