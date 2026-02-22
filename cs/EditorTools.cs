using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace AbiturEliteCode
{
    public class ConsoleRedirectionWriter : StringWriter
    {
        private Action<string> _onWrite;
        public ConsoleRedirectionWriter(Action<string> onWrite)
        {
            _onWrite = onWrite;
        }

        public override void Write(string value) => _onWrite?.Invoke(value);
        public override void WriteLine(string value) => _onWrite?.Invoke(value + "\n");
        public override void Write(char value) => _onWrite?.Invoke(value.ToString());
        public override void Write(char[] buffer, int index, int count) => _onWrite?.Invoke(new string(buffer, index, count));
    }

    public class TextMarkerService : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly TextSegmentCollection<TextMarker> _markers;

        public TextMarkerService(TextEditor editor)
        {
            _editor = editor;
            _markers = new TextSegmentCollection<TextMarker>(editor.Document);
        }

        public TextMarker GetMarkerAtOffset(int offset)
        {
            if (_markers == null) return null;
            return _markers.FindSegmentsContaining(offset).FirstOrDefault();
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_markers == null || !textView.VisualLinesValid) return;

            foreach (VisualLine line in textView.VisualLines)
            {
                foreach (TextMarker marker in _markers.FindOverlappingSegments(line.FirstDocumentLine.Offset, line.LastDocumentLine.EndOffset))
                {
                    foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                    {
                        var startPoint = rect.BottomLeft;
                        var endPoint = rect.BottomRight;

                        var pen = new Pen(new SolidColorBrush(marker.MarkerColor), 1);

                        // draw swirly line
                        var geometry = new StreamGeometry();
                        using (var ctx = geometry.Open())
                        {
                            ctx.BeginFigure(startPoint, false);
                            double x = startPoint.X;
                            double y = startPoint.Y;
                            double squiggleHeight = 2.5;

                            while (x < endPoint.X)
                            {
                                x += 2;
                                ctx.LineTo(new Point(x, y - squiggleHeight));
                                x += 2;
                                ctx.LineTo(new Point(x, y));
                            }
                        }
                        drawingContext.DrawGeometry(null, pen, geometry);
                    }
                }
            }
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Add(int offset, int length, Color color, string message = null)
        {
            _markers.Add(new TextMarker(offset, length) { MarkerColor = color, Message = message });
        }

        public void Clear()
        {
            var oldMarkers = _markers.ToList();
            foreach (var m in oldMarkers) _markers.Remove(m);
        }

        public class TextMarker : TextSegment
        {
            public TextMarker(int startOffset, int length)
            {
                StartOffset = startOffset;
                Length = length;
            }
            public Color MarkerColor { get; set; }
            public string Message { get; set; }
        }
    }

    public class UnusedCodeTransformer : DocumentColorizingTransformer
    {
        public List<(int Start, int Length)> UnusedSegments { get; set; } = new();

        protected override void ColorizeLine(DocumentLine line)
        {
            if (UnusedSegments.Count == 0) return;

            int lineStart = line.Offset;
            int lineEnd = lineStart + line.Length;

            foreach (var segment in UnusedSegments)
            {
                if (segment.Start < lineEnd && segment.Start + segment.Length > lineStart)
                {
                    int start = Math.Max(lineStart, segment.Start);
                    int end = Math.Min(lineEnd, segment.Start + segment.Length);

                    ChangeLinePart(start, end, element =>
                    {
                        element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Color.Parse("#60D4D4D4")));
                    });
                }
            }
        }
    }

    public class EscapeSequenceTransformer : DocumentColorizingTransformer
    {
        private static readonly Regex EscapePatternInString = new Regex(@"\\(u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|[0-7]{1,3}|['\\\0abfnrtvN])", RegexOptions.Compiled);
        private static readonly Regex EscapePatternInChar = new Regex(@"\\(u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|[0-7]{1,3}|[""\\\0abfnrtvN])", RegexOptions.Compiled);

        private static readonly SolidColorBrush EscapeBrush = new SolidColorBrush(Color.Parse("#D7BA7D"));

        protected override void ColorizeLine(DocumentLine line)
        {
            string text = CurrentContext.Document.GetText(line);
            if (string.IsNullOrEmpty(text)) return;

            int lineStart = line.Offset;

            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inVerbatim = false; // @"..."
            bool inChar = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // block comment
                if (inBlockComment)
                {
                    if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    { inBlockComment = false; i++; }
                    continue;
                }

                // line comment
                if (inLineComment) continue;

                // inside a verbatim string: only "" is an escape
                if (inVerbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { i++; } // "" → skip pair
                        else { inVerbatim = false; }
                    }
                    continue; // no \ escapes in verbatim strings
                }

                // inside a regular string
                if (inString)
                {
                    if (c == '\\')
                    {
                        // try to match an escape sequence starting at position i
                        var m = EscapePatternInString.Match(text, i);
                        if (m.Success && m.Index == i)
                        {
                            int absStart = lineStart + i;
                            int absEnd = absStart + m.Length;
                            ChangeLinePart(absStart, absEnd, el =>
                                el.TextRunProperties.SetForegroundBrush(EscapeBrush));
                            i += m.Length - 1;
                        }
                        else
                        {
                            i++; // skip the next char (unknown escape)
                        }
                        continue;
                    }
                    if (c == '"') { inString = false; }
                    continue;
                }

                // inside a char literal
                if (inChar)
                {
                    if (c == '\\')
                    {
                        var m = EscapePatternInChar.Match(text, i);
                        if (m.Success && m.Index == i)
                        {
                            int absStart = lineStart + i;
                            int absEnd = absStart + m.Length;
                            ChangeLinePart(absStart, absEnd, el =>
                                el.TextRunProperties.SetForegroundBrush(EscapeBrush));
                            i += m.Length - 1;
                        }
                        else { i++; }
                        continue;
                    }
                    if (c == '\'') { inChar = false; }
                    continue;
                }

                // not inside any literal
                if (c == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] == '/') { inLineComment = true; i++; continue; }
                    if (text[i + 1] == '*') { inBlockComment = true; i++; continue; }
                }

                if (c == '@' && i + 1 < text.Length && text[i + 1] == '"')
                { inVerbatim = true; i++; continue; }

                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
            }
        }
    }

    public class GhostCharacterTransformer : DocumentColorizingTransformer
    {
        private readonly TextEditor _editor;

        public GhostCharacterTransformer(TextEditor editor)
        {
            _editor = editor;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            int offset = _editor.CaretOffset;

            // only check if caret is on this line and not at the very end of document
            if (offset >= line.Offset && offset < line.EndOffset)
            {
                char nextChar = _editor.Document.GetCharAt(offset);

                // check for closing pairs
                if (nextChar == ')' || nextChar == '}' || nextChar == ']' || nextChar == '"' || nextChar == '\'' || nextChar == '>')
                {
                    // apply opacity to the single character at the caret position
                    ChangeLinePart(offset, offset + 1, element =>
                    {
                        // skip autocompletion
                        if (element is GhostTextElement || element.DocumentLength == 0) return;

                        element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Color.Parse("#66D4D4D4")));
                    });
                }
            }
        }
    }

    public class IndentationGuideRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly Pen _guidePen;
        private readonly Pen _activeGuidePen;

        public IndentationGuideRenderer(TextEditor editor)
        {
            _editor = editor;

            _guidePen = new Pen(new SolidColorBrush(Color.Parse("#3E3E42")), 1);
            _activeGuidePen = new Pen(new SolidColorBrush(Color.Parse("#A0A0A0")), 1);
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor.Document == null) return;

            double spaceWidth = textView.WideSpaceWidth;
            if (double.IsNaN(spaceWidth) || spaceWidth == 0) spaceWidth = _editor.FontSize / 2;
            double indentWidth = _editor.Options.IndentationSize * spaceWidth;

            int caretLineNum = _editor.TextArea.Caret.Line;
            int caretIndent = 0;
            int activeGuideIndex = -1;
            int scopeStart = 0;
            int scopeEnd = _editor.Document.LineCount + 1;

            if (caretLineNum > 0 && caretLineNum <= _editor.Document.LineCount)
            {
                var caretLine = _editor.Document.GetLineByNumber(caretLineNum);
                caretIndent = GetIndentLevel(_editor.Document, caretLine);

                string lineText = _editor.Document.GetText(caretLine).Trim();
                bool isOpeningBlock = lineText.Contains("{");
                bool isClosingBlock = lineText.Contains("}");
                bool isLineEmpty = caretLine.Length == 0 || string.IsNullOrWhiteSpace(_editor.Document.GetText(caretLine));

                bool prevLineEndedWithOpenBrace = false;
                if (caretLineNum > 1)
                {
                    for (int i = caretLineNum - 1; i >= 1; i--)
                    {
                        var l = _editor.Document.GetLineByNumber(i);
                        string txt = _editor.Document.GetText(l).Trim();
                        if (!string.IsNullOrEmpty(txt))
                        {
                            if (txt.EndsWith("{")) prevLineEndedWithOpenBrace = true;
                            break;
                        }
                    }
                }

                if (isLineEmpty)
                {
                    caretIndent = GetContextIndent(_editor.Document, caretLineNum);
                }

                // if caret is on a line with braces -> use that indentation level otherwise go one level deeper
                if (caretIndent > 0 || isOpeningBlock || isClosingBlock || prevLineEndedWithOpenBrace)
                {
                    // if opening or closing block -> use current indent else use parent
                    bool useInnerScope = isOpeningBlock || isClosingBlock;
                    activeGuideIndex = useInnerScope ? caretIndent : caretIndent - 1;

                    // find start of scope (scan up)
                    for (int i = caretLineNum - 1; i >= 1; i--)
                    {
                        var l = _editor.Document.GetLineByNumber(i);
                        // skip empty lines for scope detection to avoid breaking early
                        if (string.IsNullOrWhiteSpace(_editor.Document.GetText(l))) continue;

                        if (GetIndentLevel(_editor.Document, l) <= activeGuideIndex)
                        {
                            scopeStart = i;
                            break;
                        }
                    }

                    // find end of scope (scan down)
                    for (int i = caretLineNum + 1; i <= _editor.Document.LineCount; i++)
                    {
                        var l = _editor.Document.GetLineByNumber(i);
                        if (string.IsNullOrWhiteSpace(_editor.Document.GetText(l))) continue;
                        if (GetIndentLevel(_editor.Document, l) <= activeGuideIndex)
                        {
                            scopeEnd = i;
                            break;
                        }
                    }
                }
            }

            foreach (var line in textView.VisualLines)
            {
                int lineNum = line.FirstDocumentLine.LineNumber;
                int indentLevel = GetIndentLevel(_editor.Document, line.FirstDocumentLine);

                string text = _editor.Document.GetText(line.FirstDocumentLine);
                if (string.IsNullOrWhiteSpace(text))
                {
                    indentLevel = GetContextIndent(_editor.Document, lineNum);
                }

                for (int i = 0; i < indentLevel; i++)
                {
                    double x = (i * indentWidth) + (spaceWidth / 2) - textView.ScrollOffset.X;
                    if (x < 0) continue;

                    var top = new Point(x, line.VisualTop - textView.ScrollOffset.Y);
                    var bottom = new Point(x, line.VisualTop + line.Height - textView.ScrollOffset.Y);

                    bool isActive = (i == activeGuideIndex) && (lineNum > scopeStart) && (lineNum < scopeEnd);

                    drawingContext.DrawLine(isActive ? _activeGuidePen : _guidePen, top, bottom);
                }
            }
        }

        private int GetIndentLevel(TextDocument doc, DocumentLine line)
        {
            string text = doc.GetText(line.Offset, line.Length);
            int spaces = 0;
            foreach (char c in text)
            {
                if (c == ' ') spaces++;
                else if (c == '\t') spaces += _editor.Options.IndentationSize;
                else break;
            }
            return spaces / _editor.Options.IndentationSize;
        }

        private int GetContextIndent(TextDocument doc, int lineNum)
        {
            int effectivePrev = 0;
            for (int i = lineNum - 1; i >= 1; i--)
            {
                var l = doc.GetLineByNumber(i);
                string text = doc.GetText(l).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    int indent = GetIndentLevel(doc, l);
                    effectivePrev = text.EndsWith("{") ? indent + 1 : indent;
                    break;
                }
            }

            int effectiveNext = effectivePrev; // fallback if no next line
            for (int i = lineNum + 1; i <= doc.LineCount; i++)
            {
                var l = doc.GetLineByNumber(i);
                string text = doc.GetText(l).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    int indent = GetIndentLevel(doc, l);
                    effectiveNext = text.StartsWith("}") ? indent + 1 : indent;
                    break;
                }
            }

            return Math.Min(effectivePrev, effectiveNext);
        }
    }

    public class BracketHighlightRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;
        private readonly SolidColorBrush _highlightBrush;
        private readonly Pen _highlightPen;

        public BracketHighlightRenderer(TextEditor editor)
        {
            _editor = editor;
            _highlightBrush = new SolidColorBrush(Color.Parse("#33007ACC")); // faint blue background
            _highlightPen = new Pen(new SolidColorBrush(Color.Parse("#007ACC")), 1); // blue border
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            int offset = _editor.CaretOffset;
            if (offset == 0 && offset == _editor.Document.TextLength) return;

            var result = FindMatchingBracket(offset);

            if (result.HasValue)
            {
                DrawHighlight(textView, drawingContext, result.Value.Open);
                DrawHighlight(textView, drawingContext, result.Value.Close);
            }
        }

        private void DrawHighlight(TextView textView, DrawingContext ctx, int offset)
        {
            if (offset < 0 || offset >= _editor.Document.TextLength) return; // safety

            var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });

            double charWidth = textView.WideSpaceWidth;
            if (double.IsNaN(charWidth) || charWidth == 0) charWidth = _editor.FontSize / 2;

            foreach (var rect in rects)
            {
                var adjustedRect = rect;

                // check if the rectangle is wider than a single character due to ghost text
                if (rect.Width > charWidth * 1.5)
                {
                    if (offset == _editor.CaretOffset) // before
                    {
                        adjustedRect = new Rect(rect.Right - charWidth, rect.Y, charWidth, rect.Height);
                    }
                    else if (offset + 1 == _editor.CaretOffset) // after
                    {
                        adjustedRect = new Rect(rect.X, rect.Y, charWidth, rect.Height);
                    }
                    else // fallback
                    {
                        adjustedRect = new Rect(rect.X, rect.Y, charWidth, rect.Height);
                    }
                }

                ctx.DrawRectangle(_highlightBrush, _highlightPen, adjustedRect);
            }
        }

        private (int Open, int Close)? FindMatchingBracket(int caretOffset)
        {
            if (_editor.Document.TextLength == 0) return null;

            char cLeft = caretOffset > 0 ? _editor.Document.GetCharAt(caretOffset - 1) : '\0';
            char cRight = caretOffset < _editor.Document.TextLength ? _editor.Document.GetCharAt(caretOffset) : '\0';

            int searchStart = -1;
            bool searchForward = true;
            char openChar = '\0';
            char closeChar = '\0';

            // check char to the right
            if (IsStartBracket(cRight) && IsValidBracket(caretOffset))
            {
                searchStart = caretOffset;
                searchForward = true;
                openChar = cRight;
                closeChar = GetMatching(cRight);
            }
            else if (IsEndBracket(cRight) && IsValidBracket(caretOffset))
            {
                searchStart = caretOffset;
                searchForward = false;
                closeChar = cRight;
                openChar = GetMatching(cRight);
            }
            // check char to the left
            else if (IsStartBracket(cLeft) && IsValidBracket(caretOffset - 1))
            {
                searchStart = caretOffset - 1;
                searchForward = true;
                openChar = cLeft;
                closeChar = GetMatching(cLeft);
            }
            else if (IsEndBracket(cLeft) && IsValidBracket(caretOffset - 1))
            {
                searchStart = caretOffset - 1;
                searchForward = false;
                closeChar = cLeft;
                openChar = GetMatching(cLeft);
            }

            if (searchStart == -1) return null;

            int match = ScanForMatch(searchStart, searchForward, openChar, closeChar);
            if (match != -1)
            {
                return searchForward ? (searchStart, match) : (match, searchStart);
            }

            return null;
        }

        private int ScanForMatch(int start, bool forward, char open, char close)
        {
            int balance = 0;
            int docLength = _editor.Document.TextLength;
            int step = forward ? 1 : -1;

            bool isChevron = (open == '<' || close == '<');

            for (int i = start; i >= 0 && i < docLength; i += step)
            {
                char c = _editor.Document.GetCharAt(i);

                if (!IsValidBracket(i)) continue;

                if (c == open) balance++;
                else if (c == close) balance--;

                if (balance == 0) return i;

                // abort if we hit invalid generic characters
                if (isChevron && balance != 0)
                {
                    // if we are "inside" the brackets -> ensure the content is valid for generics
                    if (!IsValidGenericContentChar(c))
                    {
                        return -1; // not a generic pair
                    }
                }

                if (Math.Abs(i - start) > 20000) break;
            }
            return -1;
        }

        // determine if a character is allowed inside a generic declaration <T>
        private bool IsValidGenericContentChar(char c)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == ',' || c == '.' || c == '?' || c == '[' || c == ']' || c == '<' || c == '>')
                return true;

            return false;
        }

        private bool IsValidBracket(int offset) // filter edge cases
        {
            char c = _editor.Document.GetCharAt(offset);
            if (c != '<' && c != '>') return true;

            if (c == '<')
            {
                if (offset + 1 < _editor.Document.TextLength)
                {
                    char next = _editor.Document.GetCharAt(offset + 1);
                    if (next == '=' || next == '<') return false;
                }
                if (offset > 0 && _editor.Document.GetCharAt(offset - 1) == '<') return false;
            }
            else if (c == '>')
            {
                if (offset > 0)
                {
                    char prev = _editor.Document.GetCharAt(offset - 1);
                    if (prev == '=' || prev == '>') return false; // arrows => or >>
                    if (prev == '-') return false; // arrow ->
                }
                if (offset + 1 < _editor.Document.TextLength && _editor.Document.GetCharAt(offset + 1) == '>') return false;
            }

            var line = _editor.Document.GetLineByOffset(offset);
            string lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);
            if (lineText.Contains("//") || lineText.Contains("/*")) return false;

            return true;
        }

        private bool IsStartBracket(char c) => c == '(' || c == '{' || c == '[' || c == '<';
        private bool IsEndBracket(char c) => c == ')' || c == '}' || c == ']' || c == '>';

        private char GetMatching(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '{': return '}';
                case '}': return '{';
                case '[': return ']';
                case ']': return '[';
                case '<': return '>';
                case '>': return '<';
                default: return '\0';
            }
        }
    }

    public class AutocompleteService
    {
        private List<string> _currentSuggestions = new List<string>();
        private int _suggestionIndex = 0;
        private string _currentSuggestionSuffix = null;

        private HashSet<string> _keywords;

        // semantic context
        private Dictionary<string, HashSet<string>> _instanceMembers = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<string>> _staticMembers = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, string> _variableTypes = new Dictionary<string, string>();
        private HashSet<string> _allClasses = new HashSet<string>();
        private HashSet<string> _allLocals = new HashSet<string>();

        // c# keywords to ignore
        public static readonly HashSet<string> CsharpKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
            "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while", "var", "List", "Dictionary", "Console", "Math"
        };

        // sql keywords to ignore
        public static readonly HashSet<string> SqlKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "GROUP", "BY", "HAVING", "ORDER", "LIMIT", "OFFSET", "INSERT", "INTO",
            "VALUES", "UPDATE", "SET", "DELETE", "JOIN", "INNER", "LEFT", "RIGHT", "OUTER", "CROSS", "ON",
            "AS", "DISTINCT", "ALL", "UNION", "AND", "OR", "NOT", "NULL", "IS", "IN", "BETWEEN", "LIKE",
            "EXISTS", "CREATE", "TABLE", "DROP", "ALTER", "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "DEFAULT",
            "AUTO_INCREMENT", "ASC", "DESC", "USING", "INT", "INTEGER", "VARCHAR", "TEXT", "CHAR", "DATE",
            "DATETIME", "TIMESTAMP", "FLOAT", "DOUBLE", "DECIMAL", "BOOLEAN", "COUNT", "SUM", "AVG", "MIN",
            "MAX", "UPPER", "LOWER", "LENGTH", "CONCAT", "NOW"
        };

        public AutocompleteService(HashSet<string> keywords)
        {
            _keywords = keywords ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool HasSuggestion => _currentSuggestions.Count > 0;
        public string CurrentSuggestionSuffix => HasSuggestion ? _currentSuggestions[_suggestionIndex] : null;

        public void CycleNext()
        {
            if (HasSuggestion && _suggestionIndex < _currentSuggestions.Count - 1)
            {
                _suggestionIndex++;
            }
        }

        public void CyclePrevious()
        {
            if (HasSuggestion && _suggestionIndex > 0)
            {
                _suggestionIndex--;
            }
        }

        public void ScanTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // sql mode fallback
            if (_keywords == SqlKeywords)
            {
                var matches = Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
                _allLocals.Clear();
                foreach (Match match in matches)
                {
                    string token = match.Value;
                    if (token.Length > 2 && !_keywords.Contains(token)) _allLocals.Add(token);
                }
                return;
            }

            // c# mode semantic extraction
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            _instanceMembers.Clear();
            _staticMembers.Clear();
            _variableTypes.Clear();
            _allClasses.Clear();
            _allLocals.Clear();

            // extract explicitly declared classes and members
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var cls in classes)
            {
                string className = cls.Identifier.Text;
                _allClasses.Add(className);

                if (!_instanceMembers.ContainsKey(className)) _instanceMembers[className] = new HashSet<string>();
                if (!_staticMembers.ContainsKey(className)) _staticMembers[className] = new HashSet<string>();

                foreach (var member in cls.Members)
                {
                    bool isStatic = member.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
                    string memberName = null;

                    if (member is MethodDeclarationSyntax method) memberName = method.Identifier.Text;
                    else if (member is PropertyDeclarationSyntax prop) memberName = prop.Identifier.Text;
                    else if (member is FieldDeclarationSyntax field) memberName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text;

                    if (memberName != null)
                    {
                        if (isStatic) _staticMembers[className].Add(memberName);
                        else _instanceMembers[className].Add(memberName);
                    }
                }
            }

            // extract parameters to trust user defined types
            var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
            foreach (var param in parameters)
            {
                if (param.Type != null)
                {
                    string typeName = param.Type.ToString();
                    string varName = param.Identifier.Text;
                    _allLocals.Add(varName);

                    if (typeName != "var" && !CsharpKeywords.Contains(typeName))
                    {
                        _variableTypes[varName] = typeName;
                        _allClasses.Add(typeName);
                    }
                }
            }

            // extract local variables and fields to trust user defined types
            var variables = root.DescendantNodes().OfType<VariableDeclarationSyntax>();
            foreach (var varDecl in variables)
            {
                string typeName = varDecl.Type.ToString();

                if (typeName != "var" && !CsharpKeywords.Contains(typeName))
                {
                    _allClasses.Add(typeName);
                }

                foreach (var v in varDecl.Variables)
                {
                    string varName = v.Identifier.Text;
                    _allLocals.Add(varName);

                    if (typeName != "var" && !CsharpKeywords.Contains(typeName))
                    {
                        _variableTypes[varName] = typeName;
                    }
                }
            }

            // infer members from usage
            var memberAccesses = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            foreach (var access in memberAccesses)
            {
                if (access.Expression is IdentifierNameSyntax callerObj)
                {
                    string callerName = callerObj.Identifier.Text;
                    string memberName = access.Name.Identifier.Text;

                    // is it a static call?
                    if (_allClasses.Contains(callerName))
                    {
                        if (!_staticMembers.ContainsKey(callerName)) _staticMembers[callerName] = new HashSet<string>();
                        _staticMembers[callerName].Add(memberName);
                    }
                    // is it an instance call?
                    else if (_variableTypes.ContainsKey(callerName))
                    {
                        string typeName = _variableTypes[callerName];
                        if (!_instanceMembers.ContainsKey(typeName)) _instanceMembers[typeName] = new HashSet<string>();
                        _instanceMembers[typeName].Add(memberName);
                    }
                }
            }
        }

        public void UpdateSuggestion(string text, int caretOffset)
        {
            _currentSuggestions.Clear();
            _suggestionIndex = 0;
            _currentSuggestionSuffix = null;

            if (caretOffset == 0 || caretOffset > text.Length) return;

            // skip inside strings or comments in c#
            if (_keywords != SqlKeywords && IsInsideStringOrComment(text, caretOffset)) return;

            // do not suggest if caret is directly before a word character
            if (caretOffset < text.Length)
            {
                char nextChar = text[caretOffset];
                if (char.IsLetterOrDigit(nextChar) || nextChar == '_') return;
            }

            // find word boundary before caret
            int start = caretOffset - 1;
            while (start >= 0)
            {
                char c = text[start];
                if (!char.IsLetterOrDigit(c) && c != '_') break;
                start--;
            }
            start++;

            int length = caretOffset - start;
            string currentWord = length > 0 ? text.Substring(start, length) : "";

            // check if there is a dot before the word (caller)
            int beforeWord = start - 1;
            while (beforeWord >= 0 && char.IsWhiteSpace(text[beforeWord])) beforeWord--;

            bool hasDot = beforeWord >= 0 && text[beforeWord] == '.';
            string caller = "";

            if (hasDot)
            {
                int callerEnd = beforeWord - 1;
                while (callerEnd >= 0 && char.IsWhiteSpace(text[callerEnd])) callerEnd--;

                int callerStart = callerEnd;
                while (callerStart >= 0)
                {
                    char c = text[callerStart];
                    if (!char.IsLetterOrDigit(c) && c != '_') break;
                    callerStart--;
                }
                callerStart++;

                if (callerEnd >= callerStart)
                {
                    caller = text.Substring(callerStart, callerEnd - callerStart + 1);
                }
            }

            HashSet<string> possibleMatches = new HashSet<string>();

            if (_keywords == SqlKeywords)
            {
                // sql fallback
                foreach (var t in _allLocals) possibleMatches.Add(t);
            }
            else
            {
                // c# contextual completion
                if (hasDot && !string.IsNullOrEmpty(caller))
                {
                    // static members of a class
                    if (_allClasses.Contains(caller) && _staticMembers.ContainsKey(caller))
                    {
                        foreach (var m in _staticMembers[caller]) possibleMatches.Add(m);
                    }
                    // instance members of a variable
                    else if (_variableTypes.ContainsKey(caller))
                    {
                        string type = _variableTypes[caller];
                        if (_instanceMembers.ContainsKey(type))
                        {
                            foreach (var m in _instanceMembers[type]) possibleMatches.Add(m);
                        }
                    }
                }
                else if (!hasDot && length > 0)
                {
                    // current scope members, locals, classes
                    var tree = CSharpSyntaxTree.ParseText(text);
                    var root = tree.GetRoot();
                    var nodeAtCaret = root.FindToken(caretOffset).Parent;
                    var parentClass = nodeAtCaret?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

                    if (parentClass != null)
                    {
                        string cName = parentClass.Identifier.Text;
                        if (_staticMembers.ContainsKey(cName)) foreach (var m in _staticMembers[cName]) possibleMatches.Add(m);
                        if (_instanceMembers.ContainsKey(cName)) foreach (var m in _instanceMembers[cName]) possibleMatches.Add(m);
                    }

                    foreach (var c in _allClasses) possibleMatches.Add(c);
                    foreach (var l in _allLocals) possibleMatches.Add(l);
                }
            }

            if (possibleMatches.Count > 0 && currentWord.Length > 0)
            {
                _currentSuggestions = possibleMatches
                    .Where(t => t.StartsWith(currentWord) && t.Length > currentWord.Length)
                    .Select(t => t.Substring(currentWord.Length))
                    .OrderBy(t => t)
                    .ToList();
            }

            if (_currentSuggestions.Count > 0)
            {
                _currentSuggestionSuffix = _currentSuggestions[0];
            }
        }

        private bool IsInsideStringOrComment(string text, int offset)
        {
            bool inString = false;
            bool inChar = false;
            bool inLineComment = false;
            bool inBlockComment = false;

            for (int i = 0; i < offset; i++)
            {
                char c = text[i];
                if (inBlockComment) { if (c == '*' && i + 1 < text.Length && text[i + 1] == '/') { inBlockComment = false; i++; } continue; }
                if (inLineComment) { if (c == '\n') inLineComment = false; continue; }
                if (inString) { if (c == '\\') { i++; continue; } if (c == '"') inString = false; continue; }
                if (inChar) { if (c == '\\') { i++; continue; } if (c == '\'') inChar = false; continue; }

                if (c == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] == '/') { inLineComment = true; i++; continue; }
                    if (text[i + 1] == '*') { inBlockComment = true; i++; continue; }
                }
                if (c == '"') { inString = true; continue; }
                if (c == '\'') { inChar = true; continue; }
            }

            return inString || inChar || inLineComment || inBlockComment;
        }

        public void ClearSuggestion()
        {
            _currentSuggestions.Clear();
            _suggestionIndex = 0;
            _currentSuggestionSuffix = null;
        }
    }

    public class AutocompleteGhostGenerator : VisualLineElementGenerator
    {
        private readonly TextEditor _editor;
        private readonly AutocompleteService _service;

        public AutocompleteGhostGenerator(TextEditor editor, AutocompleteService service)
        {
            _editor = editor;
            _service = service;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (!AppSettings.IsAutocompleteEnabled && !AppSettings.IsSqlAutocompleteEnabled) return -1;
            if (!_service.HasSuggestion) return -1;

            int caretOffset = _editor.CaretOffset;

            if (caretOffset >= startOffset) // only generate if the caret is within the current visual line segment
            {
                return caretOffset;
            }

            return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            if (_service.HasSuggestion && offset == _editor.CaretOffset)
            {
                // returns a custom element that renders the ghost text
                return new GhostTextElement(_service.CurrentSuggestionSuffix, _editor);
            }
            return null;
        }
    }

    public class GhostTextElement : VisualLineElement
    {
        private readonly string _text;
        private readonly TextEditor _editor;

        // base(visualLength, documentLength)
        public GhostTextElement(string text, TextEditor editor) : base(text.Length, 0)
        {
            _text = text;
            _editor = editor;
        }

        public override TextRun CreateTextRun(int visualColumn, ITextRunConstructionContext context)
        {
            this.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Color.Parse("#808080")));
            this.TextRunProperties.SetTypeface(new Typeface(_editor.FontFamily, FontStyle.Italic, _editor.FontWeight));

            return new TextCharacters(_text, this.TextRunProperties);
        }
    }

    internal class CsharpCodeEditor
    {
        public static IHighlightingDefinition GetDarkCsharpHighlighting()
        {
            string xshd =
                @"
<SyntaxDefinition name=""C# Dark"" extensions="".cs"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
	<Color name=""Comment"" foreground=""#6A9955"" exampleText=""// comment"" />
	<Color name=""String"" foreground=""#CE9178"" exampleText=""string text = &quot;Hello&quot;"" />
    <Color name=""Char"" foreground=""#CE9178"" exampleText=""char linefeed = '\n';"" />
	<Color name=""Preprocessor"" foreground=""#9B9B9B"" exampleText=""#region Title"" />
	<Color name=""Punctuation"" foreground=""#D4D4D4"" exampleText=""a(b.c);"" />
	<Color name=""ValueTypeKeywords"" foreground=""#569CD6"" exampleText=""bool b = true;"" />
	<Color name=""ReferenceTypeKeywords"" foreground=""#569CD6"" exampleText=""object o;"" />
	<Color name=""MethodCall"" foreground=""#DCDCAA"" exampleText=""o.ToString();""/>
	<Color name=""NumberLiteral"" foreground=""#B5CEA8"" exampleText=""3.1415f""/>
	<Color name=""ThisOrBaseReference"" foreground=""#569CD6"" exampleText=""this.Do(); base.Do();""/>
	<Color name=""NullOrValueKeywords"" foreground=""#569CD6"" exampleText=""if (value == null)""/>
	<Color name=""Keywords"" foreground=""#C586C0"" exampleText=""if (a) {} else {}""/>
	<Color name=""GotoKeywords"" foreground=""#C586C0"" exampleText=""continue; return;""/>
	<Color name=""ContextKeywords"" foreground=""#569CD6"" exampleText=""var a = from x in y select z;""/>
	<Color name=""ExceptionKeywords"" foreground=""#C586C0"" exampleText=""try {} catch {} finally {}""/>
	<Color name=""CheckedKeyword"" foreground=""#569CD6"" exampleText=""checked {}""/>
	<Color name=""UnsafeKeywords"" foreground=""#569CD6"" exampleText=""unsafe { fixed (..) {} }"" />
	<Color name=""OperatorKeywords"" foreground=""#569CD6"" exampleText=""public static implicit operator..."" />
	<Color name=""ParameterModifiers"" foreground=""#569CD6"" exampleText=""(ref int a, params int[] b)"" />
	<Color name=""Modifiers"" foreground=""#569CD6"" exampleText=""public static override"" />
	<Color name=""Visibility"" foreground=""#569CD6"" exampleText=""public internal"" />
	<Color name=""NamespaceKeywords"" foreground=""#569CD6"" exampleText=""namespace A.B { using System; }"" />
	<Color name=""GetSetAddRemove"" foreground=""#569CD6"" exampleText=""int Prop { get; set; }"" />
	<Color name=""TrueFalse"" foreground=""#569CD6"" exampleText=""b = false; a = true;"" />
	<Color name=""TypeKeywords"" foreground=""#569CD6"" exampleText=""if (x is int) { a = x as int; type = typeof(int); size = sizeof(int); c = new object(); }"" />
    <Color name=""SemanticType"" foreground=""#4EC9B0"" exampleText=""List&lt;int&gt; list;"" />

	<RuleSet name=""CommentMarkerSet"">
		<Keywords fontWeight=""bold"" foreground=""#969696"">
			<Word>TODO</Word>
			<Word>FIXME</Word>
		</Keywords>
		<Keywords fontWeight=""bold"" foreground=""#969696"">
			<Word>HACK</Word>
			<Word>UNDONE</Word>
		</Keywords>
	</RuleSet>

    <RuleSet name=""CSharpCode"">
        <Span color=""Comment"">
            <Begin>//</Begin>
            <RuleSet>
                <Import ruleSet=""CommentMarkerSet""/>
            </RuleSet>
        </Span>
        <Span color=""Comment"" multiline=""true"">
            <Begin>/\*</Begin>
            <End>\*/</End>
            <RuleSet>
                <Import ruleSet=""CommentMarkerSet""/>
            </RuleSet>
        </Span>
        <Span color=""String"">
            <Begin>""</Begin>
            <End>""</End>
        </Span>
		<Span color=""Char"">
			<Begin>'</Begin>
			<End>'</End>
		</Span>
		<Span color=""Preprocessor"">
			<Begin>\#</Begin>
			<RuleSet name=""PreprocessorSet"">
				<Span> <Begin fontWeight=""bold"">region</Begin>
					<RuleSet>
						<Span color=""Comment"">
							<Begin>//</Begin>
							<RuleSet>
								<Import ruleSet=""CommentMarkerSet""/>
							</RuleSet>
						</Span>
						<Span color=""Comment"" multiline=""true"">
							<Begin>/\*</Begin>
							<End>\*/</End>
							<RuleSet>
								<Import ruleSet=""CommentMarkerSet""/>
							</RuleSet>
						</Span>
					</RuleSet>
				</Span>
			</RuleSet>
		</Span>
		<Keywords color=""TrueFalse"">
			<Word>true</Word>
			<Word>false</Word>
		</Keywords>
		<Keywords color=""Keywords"">
			<Word>else</Word>
			<Word>if</Word>
			<Word>switch</Word>
			<Word>case</Word>
			<Word>default</Word>
			<Word>do</Word>
			<Word>for</Word>
			<Word>foreach</Word>
			<Word>in</Word>
			<Word>while</Word>
			<Word>lock</Word>
		</Keywords>
		<Keywords color=""GotoKeywords"">
			<Word>break</Word>
			<Word>continue</Word>
			<Word>goto</Word>
			<Word>return</Word>
		</Keywords>
		<Keywords color=""ContextKeywords"">
			<Word>yield</Word>
			<Word>partial</Word>
			<Word>global</Word>
			<Word>where</Word>
			<Word>select</Word>
			<Word>group</Word>
			<Word>by</Word>
			<Word>into</Word>
			<Word>from</Word>
			<Word>ascending</Word>
			<Word>descending</Word>
			<Word>orderby</Word>
			<Word>let</Word>
			<Word>join</Word>
			<Word>on</Word>
			<Word>equals</Word>
		</Keywords>
		<Keywords color=""ExceptionKeywords"">
			<Word>try</Word>
			<Word>throw</Word>
			<Word>catch</Word>
			<Word>finally</Word>
		</Keywords>
		<Keywords color=""CheckedKeyword"">
			<Word>checked</Word>
			<Word>unchecked</Word>
		</Keywords>
		<Keywords color=""UnsafeKeywords"">
			<Word>fixed</Word>
			<Word>unsafe</Word>
		</Keywords>
		<Keywords color=""ValueTypeKeywords"">
			<Word>bool</Word>
			<Word>byte</Word>
			<Word>char</Word>
			<Word>decimal</Word>
			<Word>double</Word>
			<Word>enum</Word>
			<Word>float</Word>
			<Word>int</Word>
			<Word>long</Word>
			<Word>sbyte</Word>
			<Word>short</Word>
			<Word>struct</Word>
			<Word>uint</Word>
			<Word>ushort</Word>
			<Word>ulong</Word>
            <Word>var</Word>
		</Keywords>
		<Keywords color=""ReferenceTypeKeywords"">
			<Word>class</Word>
			<Word>interface</Word>
			<Word>delegate</Word>
			<Word>object</Word>
			<Word>string</Word>
			<Word>void</Word>
		</Keywords>
		<Keywords color=""OperatorKeywords"">
			<Word>explicit</Word>
			<Word>implicit</Word>
			<Word>operator</Word>
		</Keywords>
		<Keywords color=""ParameterModifiers"">
			<Word>params</Word>
			<Word>ref</Word>
			<Word>out</Word>
		</Keywords>
		<Keywords color=""Modifiers"">
			<Word>abstract</Word>
			<Word>const</Word>
			<Word>event</Word>
			<Word>extern</Word>
			<Word>override</Word>
			<Word>readonly</Word>
			<Word>sealed</Word>
			<Word>static</Word>
			<Word>virtual</Word>
			<Word>volatile</Word>
			<Word>async</Word>
		</Keywords>
		<Keywords color=""Visibility"">
			<Word>public</Word>
			<Word>protected</Word>
			<Word>private</Word>
			<Word>internal</Word>
		</Keywords>
		<Keywords color=""NamespaceKeywords"">
			<Word>namespace</Word>
			<Word>using</Word>
		</Keywords>
		<Keywords color=""GetSetAddRemove"">
			<Word>get</Word>
			<Word>set</Word>
			<Word>add</Word>
			<Word>remove</Word>
		</Keywords>
		<Keywords color=""NullOrValueKeywords"">
			<Word>null</Word>
			<Word>value</Word>
		</Keywords>
		<Keywords color=""TypeKeywords"">
			<Word>as</Word>
			<Word>is</Word>
			<Word>new</Word>
			<Word>sizeof</Word>
			<Word>typeof</Word>
			<Word>stackalloc</Word>
		</Keywords>
		<Keywords color=""ThisOrBaseReference"">
			<Word>this</Word>
			<Word>base</Word>
		</Keywords>
        <Keywords color=""SemanticType"">
             <Word>List</Word>
             <Word>Dictionary</Word>
             <Word>Console</Word>
             <Word>Math</Word>
             <Word>Convert</Word>
             <Word>Array</Word>
             <Word>DateTime</Word>
             <Word>Int32</Word>
        </Keywords>
		<Rule color=""MethodCall"">
			\b
			[\d\w_]+  # an identifier
			(?=\s*\() # followed by (
		</Rule>
		<Rule color=""NumberLiteral"">
			\b0[xX][0-9a-fA-F]+  # hex number
		|	
			(	\b\d+(\.[0-9]+)?   #number with optional floating point
			|	\.[0-9]+           #or just starting with floating point
			)
			([eE][+-]?[0-9]+)? # optional exponent
		</Rule>
    </RuleSet>

	<RuleSet>
        <Span color=""String"" multiline=""true"">
            <Begin>\$""</Begin>
            <End>""</End>
            <RuleSet>
                <Span begin=""\{\{"" end=""""/>
                <Span begin=""}}"" end=""""/>
                <Span color=""Punctuation"">
                    <Begin>\{</Begin>
                    <End>}</End>
                    <RuleSet>
                        <Import ruleSet=""CSharpCode""/>
                    </RuleSet>
                </Span>
            </RuleSet>
        </Span>
        <Import ruleSet=""CSharpCode""/>
	</RuleSet>
</SyntaxDefinition>";

            using (var reader = XmlReader.Create(new StringReader(xshd)))
            {
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
    }

    public class SemanticClassHighlightingTransformer : DocumentColorizingTransformer
    {
        public HashSet<string> KnownClasses { get; set; } = new HashSet<string>();

        protected override void ColorizeLine(DocumentLine line)
        {
            if (KnownClasses.Count == 0) return;

            string text = CurrentContext.Document.GetText(line);
            if (string.IsNullOrWhiteSpace(text)) return;

            int lineStart = line.Offset;

            var safeRanges = new List<(int start, int end)>();
            bool inString = false;
            bool inInterpolated = false;
            bool inChar = false;
            int interpolationDepth = 0;
            int safeStart = 0;

            for (int i = 0; i < text.Length; i++)
            {
                // handle interpolation
                if (inInterpolated && text[i] == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{') { i++; continue; } // Skip {{
                    interpolationDepth++;
                    safeStart = i + 1;
                    continue;
                }
                if (inInterpolated && interpolationDepth > 0 && text[i] == '}')
                {
                    if (i + 1 < text.Length && text[i + 1] == '}') { i++; continue; } // Skip }}
                    safeRanges.Add((safeStart, i));
                    interpolationDepth--;
                    continue;
                }

                if (interpolationDepth > 0) continue;

                if (inString || inInterpolated)
                {
                    if (text[i] == '"' && (i == 0 || text[i - 1] != '\\'))
                    {
                        inString = false;
                        inInterpolated = false;
                        safeStart = i + 1;
                    }
                    continue;
                }

                if (inChar)
                {
                    if (text[i] == '\'' && (i == 0 || text[i - 1] != '\\'))
                    {
                        inChar = false;
                        safeStart = i + 1;
                    }
                    continue;
                }

                // line comments
                if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    safeRanges.Add((safeStart, i));
                    safeStart = text.Length;
                    break;
                }

                // string starts
                if (text[i] == '$' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    safeRanges.Add((safeStart, i));
                    inInterpolated = true;
                    i++; // skip "
                }
                else if (text[i] == '"')
                {
                    safeRanges.Add((safeStart, i));
                    inString = true;
                }
                else if (text[i] == '\'')
                {
                    safeRanges.Add((safeStart, i));
                    inChar = true;
                }
            }

            if (safeStart < text.Length) safeRanges.Add((safeStart, text.Length));

            // finds class, struct, record, interface, enum names
            var matches = Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
            foreach (Match match in matches)
            {
                if (KnownClasses.Contains(match.Value))
                {
                    bool isSafe = false;
                    foreach (var range in safeRanges)
                    {
                        if (match.Index >= range.start && (match.Index + match.Length) <= range.end)
                        {
                            isSafe = true;
                            break;
                        }
                    }

                    if (isSafe)
                    {
                        ChangeLinePart(lineStart + match.Index, lineStart + match.Index + match.Length, element =>
                        {
                            element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Color.Parse("#4EC9B0")));
                        });
                    }
                }
            }
        }
    }
}