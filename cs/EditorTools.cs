using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public void Add(int offset, int length, Color color)
        {
            _markers.Add(new TextMarker(offset, length) { MarkerColor = color });
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
                if (nextChar == ')' || nextChar == '}' || nextChar == ']' || nextChar == '"' || nextChar == '>')
                {
                    // apply opacity to the single character at the caret position
                    ChangeLinePart(offset, offset + 1, element =>
                    {
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

                if (caretLine.Length == 0 || string.IsNullOrWhiteSpace(_editor.Document.GetText(caretLine)))
                {
                    caretIndent = GetContextIndent(_editor.Document, caretLineNum);
                }

                if (caretIndent > 0 || isOpeningBlock)
                {
                    activeGuideIndex = isOpeningBlock ? caretIndent : caretIndent - 1;

                    // find start of scope (scan up)
                    for (int i = caretLineNum - 1; i >= 1; i--)
                    {
                        var l = _editor.Document.GetLineByNumber(i);
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
            // look up (prev)
            int prevIndent = 0;
            for (int i = lineNum - 1; i >= 1; i--)
            {
                var l = doc.GetLineByNumber(i);
                if (!string.IsNullOrWhiteSpace(doc.GetText(l)))
                {
                    prevIndent = GetIndentLevel(doc, l);
                    break;
                }
            }

            // look down (next)
            int nextIndent = 0;
            for (int i = lineNum + 1; i <= doc.LineCount; i++)
            {
                var l = doc.GetLineByNumber(i);
                if (!string.IsNullOrWhiteSpace(doc.GetText(l)))
                {
                    nextIndent = GetIndentLevel(doc, l);
                    break;
                }
            }

            return Math.Max(prevIndent, nextIndent);
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
            foreach (var rect in rects)
            {
                ctx.DrawRectangle(_highlightBrush, _highlightPen, rect);
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

    internal class CsharpCodeEditor
    {
        public static IHighlightingDefinition GetDarkCsharpHighlighting()
        {
            string xshd =
                @"
<SyntaxDefinition name=""C# Dark"" extensions="".cs"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
	<Color name=""Comment"" foreground=""#6A9955"" exampleText=""// comment"" />
	<Color name=""String"" foreground=""#CE9178"" exampleText=""string text = &quot;Hello&quot;"" />
	<Color name=""Char"" foreground=""#D7BA7D"" exampleText=""char linefeed = '\n';"" />
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
            <RuleSet>
                <Span begin=""\\"" end="".""/>
            </RuleSet>
        </Span>
		<Span color=""Char"">
			<Begin>'</Begin>
			<End>'</End>
			<RuleSet>
				<Span begin=""\\"" end="".""/>
			</RuleSet>
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
                <Span begin=""\\"" end="".""/>
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
}