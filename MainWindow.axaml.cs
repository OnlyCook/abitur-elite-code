using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using AvaloniaEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Svg.Transforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace AbiturEliteCode
{
    public static class AppSettings
    {
        public static bool IsVimEnabled { get; set; } = false;
        public static bool IsSyntaxHighlightingEnabled { get; set; } = false;
        public static double UiScale { get; set; } = 1.0;
        public static bool IsErrorHighlightingEnabled { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private PlayerData playerData;
        private Level currentLevel;
        private List<Level> levels;
        private System.Timers.Timer autoSaveTimer;

        private ScaleTransform ImgScale;
        private TranslateTransform ImgTranslate;
        private Point _lastMousePoint;
        private bool _isDragging = false;
        private double _currentScale = 1.0;

        private TextMarkerService _textMarkerService;
        private UnusedCodeTransformer _unusedCodeTransformer;
        private DispatcherTimer _diagnosticTimer;

        private enum VimMode { Normal, Insert, CommandPending, CommandLine, Search }
        private VimMode _vimMode = VimMode.Normal;
        private string _vimCommandBuffer = ""; // for multi char commands
        private string _vimClipboard = "";

        private SolidColorBrush BrushTextNormal = SolidColorBrush.Parse("#E6E6E6");
        private SolidColorBrush BrushTextHighlight = SolidColorBrush.Parse("#6495ED"); // blue
        private SolidColorBrush BrushTextTitle = SolidColorBrush.Parse("#32A852"); // green
        private SolidColorBrush BrushBgPanel = SolidColorBrush.Parse("#202124");

        public MainWindow()
        {
            InitializeComponent();

            var transformGroup = (TransformGroup)ImgDiagram.RenderTransform;
            ImgScale = (ScaleTransform)transformGroup.Children[0];
            ImgTranslate = (TranslateTransform)transformGroup.Children[1];

            levels = Curriculum.GetLevels();
            playerData = SaveSystem.Load();

            AppSettings.IsVimEnabled = playerData.Settings.IsVimEnabled;
            AppSettings.IsSyntaxHighlightingEnabled =
                playerData.Settings.IsSyntaxHighlightingEnabled;
            AppSettings.UiScale = playerData.Settings.UiScale;

            ApplyUiScale();
            ApplySyntaxHighlighting();
            UpdateVimState();
            BuildVimCheatSheet();

            ConfigureEditor();

            autoSaveTimer = new System.Timers.Timer(2000) { AutoReset = false };
            autoSaveTimer.Elapsed += (s, e) => Dispatcher.UIThread.InvokeAsync(SaveCurrentProgress);

            CodeEditor.TextChanged += (s, e) =>
            {
                autoSaveTimer.Stop();
                autoSaveTimer.Start();
            };

            int maxId =
                playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1;
            var startLevel = levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0];
            LoadLevel(startLevel);

            this.Opened += (s, e) => CodeEditor.Focus();

            // return focus to editor when clicking somewhere random
            this.AddHandler(
                PointerPressedEvent,
                (s, e) =>
                {
                    var source = e.Source as Control;
                    if (source is TextBox || source is Button || (source?.Parent is Button))
                        return;

                    if (source?.Name == "DiagramPanel" || source?.Name == "ImgDiagram")
                        return;

                    Dispatcher.UIThread.Post(() => CodeEditor.Focus());
                },
                RoutingStrategies.Tunnel
            );

            UpdateVimUI();
        }

        private void ConfigureEditor()
        {
            CodeEditor.Options.ConvertTabsToSpaces = true;
            CodeEditor.Options.IndentationSize = 4;
            CodeEditor.Options.ShowSpaces = false;
            CodeEditor.Options.ShowTabs = false;
            CodeEditor.Options.EnableHyperlinks = false;
            CodeEditor.Options.EnableEmailHyperlinks = false;

            CodeEditor.FontFamily = new FontFamily("Consolas, Monospace");
            CodeEditor.FontSize = 16;
            CodeEditor.Background = Brushes.Transparent;
            CodeEditor.Foreground = SolidColorBrush.Parse("#D4D4D4");

            _textMarkerService = new TextMarkerService(CodeEditor);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);

            _unusedCodeTransformer = new UnusedCodeTransformer();
            CodeEditor.TextArea.TextView.LineTransformers.Add(_unusedCodeTransformer);

            // wait 600ms after typing
            _diagnosticTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _diagnosticTimer.Tick += (s, e) =>
            {
                _diagnosticTimer.Stop();
                UpdateDiagnostics();
            };

            CodeEditor.TextArea.Caret.PositionChanged += (s, e) =>
            {
                CodeEditor.TextArea.Caret.BringCaretToView();
            };

            CodeEditor.TextArea.TextEntering += Editor_TextEntering;
            CodeEditor.AddHandler(InputElement.KeyDownEvent, CodeEditor_KeyDown, RoutingStrategies.Tunnel);

            CodeEditor.TextChanged += (s, e) =>
            {
                autoSaveTimer.Stop();
                autoSaveTimer.Start();

                if (AppSettings.IsErrorHighlightingEnabled)
                {
                    _diagnosticTimer.Stop();
                    _diagnosticTimer.Start();
                }
            };
        }

        private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
        {
            // ctrl + s => save
            if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
            {
                SaveCurrentProgress();
                TxtConsole.Text += "\n> Gespeichert.";
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
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

            if (e.Key == Key.Escape)
            {
                _vimMode = VimMode.Normal;
                _vimCommandBuffer = "";
                CodeEditor.TextArea.ClearSelection();
                UpdateVimUI();
                e.Handled = true;
                return;
            }

            if (_vimMode == VimMode.Insert)
            {
                // allow normal typing
                return;
            }
            else if (_vimMode == VimMode.CommandLine)
            {
                HandleVimCommandLine(e);
                e.Handled = true;
                return;
            }
            else if (_vimMode == VimMode.Search)
            {
                HandleVimSearch(e);
                e.Handled = true;
                return;
            }

            // normal or commandpending mode -> intercept all
            e.Handled = true;
            HandleVimNormalInput(e);
        }

        private void Editor_TextEntering(object sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            char charTyped = e.Text[0];
            TextArea textArea = (TextArea)sender;
            int offset = textArea.Caret.Offset;

            if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"')
            {
                string pair =
                    charTyped == '(' ? ")" : charTyped == '{' ? "}" : charTyped == '[' ? "]" : "\"";
                textArea.Document.Insert(offset, charTyped.ToString() + pair);
                textArea.Caret.Offset = offset + 1;
                e.Handled = true;
                return;
            }

            if (charTyped == ')' || charTyped == '}' || charTyped == ']' || charTyped == '"')
            {
                if (
                    offset < textArea.Document.TextLength
                    && textArea.Document.GetCharAt(offset) == charTyped
                )
                {
                    textArea.Caret.Offset += 1;
                    e.Handled = true;
                    return;
                }
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
                {
                    if (char.IsWhiteSpace(c))
                        indent += c;
                    else
                        break;
                }

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

        private void SaveCurrentProgress()
        {
            if (currentLevel != null)
            {
                playerData.UserCode[currentLevel.Id] = CodeEditor.Text;
                playerData.Settings.IsVimEnabled = AppSettings.IsVimEnabled;
                playerData.Settings.IsSyntaxHighlightingEnabled =
                    AppSettings.IsSyntaxHighlightingEnabled;
                playerData.Settings.UiScale = AppSettings.UiScale;

                SaveSystem.Save(playerData);
            }
        }

        private void LoadLevel(Level level)
        {
            SaveCurrentProgress();

            // reset error highlighting on every load
            AppSettings.IsErrorHighlightingEnabled = false;
            ClearDiagnostics();

            currentLevel = level;
            BtnNextLevel.IsVisible = false;

            string rawCode = playerData.UserCode.ContainsKey(level.Id)
                ? playerData.UserCode[level.Id]
                : level.StarterCode;
            CodeEditor.Text = rawCode;

            // reset uml zoom
            _currentScale = 1.0;
            if (ImgScale != null)
            {
                ImgScale.ScaleX = 1.0;
                ImgScale.ScaleY = 1.0;
            }
            if (ImgTranslate != null)
            {
                ImgTranslate.X = 0;
                ImgTranslate.Y = 0;
            }

            PnlTask.Children.Clear();

            PnlTask.Children.Add(
                new SelectableTextBlock
                {
                    Text = $"{level.Id}. {level.Title}",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 20)
                }
            );

            RenderRichText(PnlTask, level.Description);

            try
            {
                if (!string.IsNullOrEmpty(level.DiagramPath))
                {
                    string safePath = level.DiagramPath.Replace(
                        "\\",
                        Path.DirectorySeparatorChar.ToString()
                    );
                    string fullPath = Path.Combine(AppContext.BaseDirectory, safePath);
                    if (File.Exists(fullPath))
                        ImgDiagram.Source = new Bitmap(fullPath);
                    else
                        ImgDiagram.Source = null;
                }
                else
                    ImgDiagram.Source = null;
            }
            catch
            {
                ImgDiagram.Source = null;
            }

            GenerateMaterials(level);
            TxtConsole.Foreground = Brushes.LightGray;
            TxtConsole.Text =
                $"> System initialisiert.\n> Level {level.Id} (Code: {level.SkipCode}) geladen.";

            Dispatcher.UIThread.Post(() => CodeEditor.Focus());
        }

        private void RenderRichText(StackPanel panel, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            string safeText = text.Replace("|[", "\x01").Replace("|]", "\x02");
            var parts = Regex.Split(safeText, @"(\[.*?\])");

            var tb = new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                LineHeight = 24
            };

            foreach (var part in parts)
            {
                if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string content = part.Substring(1, part.Length - 2);
                    content = content.Replace("\x01", "[").Replace("\x02", "]");
                    tb.Inlines.Add(
                        new Run
                        {
                            Text = content,
                            FontWeight = FontWeight.Bold,
                            Foreground = BrushTextHighlight,
                            FontFamily = new FontFamily("Consolas")
                        }
                    );
                }
                else
                {
                    string content = part.Replace("\x01", "[").Replace("\x02", "]");
                    tb.Inlines.Add(new Run { Text = content, Foreground = BrushTextNormal });
                }
            }
            panel.Children.Add(tb);
        }

        private async void UpdateDiagnostics()
        {
            if (!AppSettings.IsErrorHighlightingEnabled || currentLevel == null)
            {
                ClearDiagnostics();
                return;
            }

            string code = CodeEditor.Text;

            // background ui thread
            var diagnostics = await System.Threading.Tasks.Task.Run(() =>
            {
                string fullCode = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" + code;

                var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);

                var references = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
                };

                var compilation = CSharpCompilation.Create("Analysis", new[] { syntaxTree }, references);
                return compilation.GetDiagnostics();
            });

            _textMarkerService.Clear();
            _unusedCodeTransformer.UnusedSegments.Clear();

            foreach (var diag in diagnostics)
            {
                int headerLength = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n".Length;
                int start = diag.Location.SourceSpan.Start - headerLength;
                int length = diag.Location.SourceSpan.Length;

                // if error is invisible header -> ignore
                if (start < 0) continue;
                if (start + length > CodeEditor.Document.TextLength) continue;

                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    _textMarkerService.Add(start, length, Colors.Red);
                }
                else if (diag.Severity == DiagnosticSeverity.Warning)
                {
                    // unused vars
                    if (diag.Id == "CS0168" || diag.Id == "CS0219" || diag.Id == "CS8019")
                    {
                        _unusedCodeTransformer.UnusedSegments.Add((start, length));
                    }
                    else
                    {
                        _textMarkerService.Add(start, length, Colors.Yellow);
                    }
                }
            }

            CodeEditor.TextArea.TextView.Redraw();
        }

        private void ClearDiagnostics()
        {
            _textMarkerService.Clear();
            _unusedCodeTransformer.UnusedSegments.Clear();
            CodeEditor.TextArea.TextView.Redraw();
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

                            // swirly line
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
                            element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Color.Parse("#808080"))); // half opacity
                        });
                    }
                }
            }
        }

        private void Diagram_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (ImgScale == null || ImgTranslate == null)
                return;

            double zoomSpeed = 0.1;

            if (e.Delta.Y > 0)
                _currentScale += zoomSpeed;
            else
                _currentScale -= zoomSpeed;
            if (_currentScale < 0.1)
                _currentScale = 0.1;
            if (_currentScale < 0.1)
                _currentScale = 0.1;
            if (_currentScale > 5.0)
                _currentScale = 5.0;

            ImgScale.ScaleX = _currentScale;
            ImgScale.ScaleY = _currentScale;

            e.Handled = true;
        }

        private void Diagram_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(sender as Control);
            if (pointer.Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _lastMousePoint = pointer.Position;
                (sender as Control).Cursor = new Cursor(StandardCursorType.SizeAll);
                e.Handled = true;
            }
        }

        private void Diagram_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isDragging || ImgTranslate == null)
                return;

            var pointer = e.GetCurrentPoint(sender as Control);
            var currentPos = pointer.Position;
            var delta = currentPos - _lastMousePoint;

            ImgTranslate.X += delta.X;
            ImgTranslate.Y += delta.Y;

            _lastMousePoint = currentPos;
            e.Handled = true;
        }

        private void Diagram_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                (sender as Control).Cursor = Cursor.Default;
                e.Handled = true;
            }
        }

        private void BtnResetDiagram_Click(object sender, RoutedEventArgs e)
        {
            if (ImgScale != null && ImgTranslate != null)
            {
                _currentScale = 1.0;
                ImgScale.ScaleX = _currentScale;
                ImgScale.ScaleY = _currentScale;
                ImgTranslate.X = 0;
                ImgTranslate.Y = 0;
            }
        }

        private void GenerateMaterials(Level level)
        {
            PnlMaterials.Children.Clear();

            if (!string.IsNullOrEmpty(level.AuxiliaryId))
            {
                string auxPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "img",
                    $"aux_{level.AuxiliaryId}.png"
                );
                if (File.Exists(auxPath))
                {
                    PnlMaterials.Children.Add(
                        new SelectableTextBlock
                        {
                            Text = "Referenz-Klassen:",
                            FontWeight = FontWeight.Bold,
                            Foreground = BrushTextTitle,
                            Margin = new Thickness(0, 0, 0, 5)
                        }
                    );
                    PnlMaterials.Children.Add(
                        new Image
                        {
                            Source = new Bitmap(auxPath),
                            Height = 150,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 0, 0, 15),
                            HorizontalAlignment = HorizontalAlignment.Left
                        }
                    );
                }
            }
            if (!string.IsNullOrEmpty(level.MaterialDocs))
                if (!string.IsNullOrEmpty(level.MaterialDocs))
                {
                    var lines = level.MaterialDocs.Split('\n');
                    foreach (var line in lines)
                    {
                        string trim = line.Trim();
                        if (trim.StartsWith("Hinweis:") || trim.StartsWith("Tipp:"))
                        {
                            string preview =
                                trim.Length > 18 ? trim.Substring(0, 15) + "..." : trim;
                            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                            var contentPanel = new Border
                            {
                                IsVisible = false,
                                Background = SolidColorBrush.Parse("#252526"),
                                Margin = new Thickness(0, 5, 0, 0),
                                CornerRadius = new CornerRadius(6),
                                Padding = new Thickness(10),
                                Child = new StackPanel()
                            };
                            RenderRichText((StackPanel)contentPanel.Child, trim);
                            var btn = new Button
                            {
                                Content = "▶ " + preview,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                Background = SolidColorBrush.Parse("#3C3C41"),
                                Foreground = Brushes.White,
                                HorizontalContentAlignment = HorizontalAlignment.Left
                            };
                            btn.Click += (s, e) =>
                            {
                                btn.IsVisible = false;
                                contentPanel.IsVisible = true;
                            };
                            stack.Children.Add(btn);
                            stack.Children.Add(contentPanel);
                            PnlMaterials.Children.Add(stack);
                        }
                        else if (!string.IsNullOrWhiteSpace(trim))
                        {
                            RenderRichText(PnlMaterials, trim);
                        }
                    }
                }
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            TxtConsole.Foreground = Brushes.LightGray;
            TxtConsole.Text = "Kompiliere...\n";
            SaveCurrentProgress();

            string fullCode =
                "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n"
                + CodeEditor.Text;

            var syntaxTree = CSharpSyntaxTree.ParseText(fullCode);
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
            };
            var compilation = CSharpCompilation.Create(
                $"Level_{currentLevel.Id}_{Guid.NewGuid()}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    TxtConsole.Foreground = Brushes.Red;
                    TxtConsole.Text = "KOMPILIERFEHLER:\n";
                    foreach (
                        var diag in result.Diagnostics.Where(
                            d => d.Severity == DiagnosticSeverity.Error
                        )
                    )
                    {
                        var lineSpan = diag.Location.GetLineSpan();
                        int userLine = lineSpan.StartLinePosition.Line - 3;
                        if (userLine < 0)
                            userLine = 0;
                        TxtConsole.Text += $"Zeile {userLine}: {diag.GetMessage()}\n";
                    }
                }
                else
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    var assembly = Assembly.Load(ms.ToArray());
                    RunTests(assembly);
                }
            }

            CodeEditor.Focus();
        }

        private void RunTests(Assembly assembly)
        {
            var result = LevelTester.Run(currentLevel.Id, assembly);
            if (result.Success)
            {
                TxtConsole.Foreground = Brushes.LightGreen;
                TxtConsole.Text = "✓ TEST BESTANDEN: " + result.Feedback + "\n\n";
                if (!playerData.CompletedLevelIds.Contains(currentLevel.Id))
                    playerData.CompletedLevelIds.Add(currentLevel.Id);
                var nextLvl = levels.FirstOrDefault(l => l.SkipCode == currentLevel.NextLevelCode);
                if (nextLvl != null && !playerData.UnlockedLevelIds.Contains(nextLvl.Id))
                {
                    playerData.UnlockedLevelIds.Add(nextLvl.Id);
                    SaveSystem.Save(playerData);
                    TxtConsole.Text += $"🔓 Level {nextLvl.Id} freigeschaltet!\n";
                    TxtConsole.Text += $"Nächstes Level Code: {nextLvl.SkipCode}\n";
                    BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                    BtnNextLevel.IsVisible = true;
                }
                else if (nextLvl != null)
                {
                    TxtConsole.Text += $"\nNächstes Level Code: {nextLvl.SkipCode}";
                    BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                    BtnNextLevel.IsVisible = true;
                }
                else
                {
                    TxtConsole.Text += "\n🎉 Das war das letzte Level dieser Sektion!";
                    BtnNextLevel.Content = "KURS ABSCHLIESSEN ✓";
                    BtnNextLevel.IsVisible = true;
                }

                SaveSystem.Save(playerData);
            }
            else
            {
                TxtConsole.Foreground = Brushes.Orange;
                string msg =
                    result.Error != null
                        ? (
                              result.Error.InnerException != null
                                  ? result.Error.InnerException.Message
                                  : result.Error.Message
                          )
                        : "Unbekannter Fehler";
                TxtConsole.Text = "❌ LAUFZEITFEHLER / LOGIK:\n" + msg;
            }
        }

        private void BtnNextLevel_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNextLevel.Content?.ToString()?.Contains("ABSCHLIESSEN") == true)
            {
                ShowCourseCompletedDialog();
                return;
            }
            var nextLvl = levels.FirstOrDefault(l => l.SkipCode == currentLevel.NextLevelCode);
            if (nextLvl != null)
                LoadLevel(nextLvl);
            CodeEditor.Focus();
        }

        private async void ShowCourseCompletedDialog()
        {
            var dialog = new Window
            {
                Title = "Kurs Abgeschlossen",
                Width = 450,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#202124"),
                CornerRadius = new CornerRadius(8)
            };
            var rootBorder = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20)
            };
            var rootGrid = new Grid { RowDefinitions = new RowDefinitions("*, Auto") };
            var contentStack = new StackPanel
            {
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentStack.Children.Add(
                new TextBlock
                {
                    Text = "🎉 Herzlichen Glückwunsch! 🎉",
                    FontSize = 22,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextTitle,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            );
            contentStack.Children.Add(
                new TextBlock
                {
                    Text =
                        "Du hast alle Levels erfolgreich abgeschlossen!\n\nDu bist nun bereit für deine Abiturprüfung in Praktischer Informatik.\nViel Erfolg!",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 24
                }
            );
            rootGrid.Children.Add(contentStack);
            var btnClose = new Button
            {
                Content = "Schließen",
                Background = BrushTextTitle,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Padding = new Thickness(30, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 20, 0, 0),
                Cursor = Cursor.Parse("Hand")
            };
            btnClose.Click += (_, __) => dialog.Close();

            Grid.SetRow(btnClose, 1);
            rootGrid.Children.Add(btnClose);

            rootBorder.Child = rootGrid;
            dialog.Content = rootBorder;

            await dialog.ShowDialog(this);
        }

        private async void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Code zurücksetzen?",
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };
            var rootGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(20)
            };
            rootGrid.Children.Add(
                new TextBlock
                {
                    Text =
                        "Möchtest du den Code wirklich zurücksetzen? Alle Änderungen gehen verloren.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 15
                }
            );
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Margin = new Thickness(0, 20, 0, 0)
            };
            Grid.SetRow(btnPanel, 1);
            var btnYes = new Button
            {
                Content = "Ja, zurücksetzen",
                Background = SolidColorBrush.Parse("#B43232"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            var btnNo = new Button
            {
                Content = "Abbrechen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            bool result = false;
            btnYes.Click += (_, __) =>
            {
                result = true;
                dialog.Close();
            };
            btnNo.Click += (_, __) =>
            {
                dialog.Close();
            };
            btnPanel.Children.Add(btnNo);
            btnPanel.Children.Add(btnYes);
            rootGrid.Children.Add(btnPanel);
            dialog.Content = rootGrid;
            await dialog.ShowDialog(this);
            if (result)
            {
                CodeEditor.Text = currentLevel.StarterCode;
                TxtConsole.Text += "\n> Code auf Standard zurückgesetzt.";
            }
            CodeEditor.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentProgress();
            TxtConsole.Text += "\n> Gespeichert.";
            CodeEditor.Focus();
        }

        private void BtnLevelSelect_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title = "Level Wählen",
                Width = 450,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushBgPanel,
                SystemDecorations = SystemDecorations.BorderOnly
            };
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
            mainGrid.Children.Add(
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 15,
                    Margin = new Thickness(0, 0, 0, 15),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Level Auswählen",
                            FontSize = 20,
                            FontWeight = FontWeight.Bold,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new Border
                        {
                            Background = SolidColorBrush.Parse("#2D2D30"),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(10, 5),
                            Child = new TextBlock
                            {
                                Text = $"{playerData.CompletedLevelIds.Count}/{levels.Count}",
                                Foreground = BrushTextTitle,
                                FontWeight = FontWeight.Bold,
                                FontSize = 14
                            }
                        }
                    }
                }
            );
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);
            var levelStack = new StackPanel { Spacing = 8 };
            var groups = levels.GroupBy(l => l.Section);
            foreach (var group in groups)
            {
                bool isSectionComplete = group.All(
                    l => playerData.CompletedLevelIds.Contains(l.Id)
                );
                var headerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };
                headerPanel.Children.Add(
                    new TextBlock
                    {
                        Text = group.Key,
                        Foreground = BrushTextTitle,
                        FontWeight = FontWeight.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                );
                if (isSectionComplete)
                {
                    headerPanel.Children.Add(LoadIcon("icons/ic_done.svg", 16));
                }
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
                    string iconPath = completed
                        ? "icons/ic_check.svg"
                        : (unlocked ? "icons/ic_lock_open.svg" : "icons/ic_lock.svg");
                    btnContent.Children.Add(LoadIcon(iconPath, 16));
                    btnContent.Children.Add(
                        new TextBlock
                        {
                            Text = $"{lvl.Id}. {lvl.Title}",
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    );
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
                var expander = new Expander
                {
                    Header = headerPanel,
                    Content = sectionContent,
                    IsExpanded = !isSectionComplete,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                levelStack.Children.Add(expander);
            }
            scroll.Content = levelStack;
            mainGrid.Children.Add(scroll);
            var closeBtn = new Button
            {
                Content = "Schließen",
                HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 15, 0, 0),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(4),
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White
            };
            Grid.SetRow(closeBtn, 2);
            closeBtn.Click += (_, __) => win.Close();
            mainGrid.Children.Add(closeBtn);
            root.Child = mainGrid;
            win.Content = root;
            win.ShowDialog(this);
            CodeEditor.Focus();
        }

        private Image LoadIcon(string path, double size)
        {
            var image = new Image { Width = size, Height = size, Stretch = Stretch.Uniform };
            string uriString = $"avares://AbiturEliteCode/{path}";
            try
            {
                var svgImage = new SvgImage();
                svgImage.Source = SvgSource.Load(uriString, null);
                image.Source = svgImage;
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"Could not load SVG: {uriString}");
            }
            return image;
        }

        private void TxtSkipCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSkipCode.Text?.Length == 3)
            {
                string code = TxtSkipCode.Text.ToUpper();
                var lvl = levels.FirstOrDefault(l => l.SkipCode == code);
                if (lvl != null)
                {
                    if (!playerData.UnlockedLevelIds.Contains(lvl.Id))
                    {
                        playerData.UnlockedLevelIds.Add(lvl.Id);
                        SaveSystem.Save(playerData);
                    }
                    LoadLevel(lvl);
                    TxtSkipCode.Text = "";
                    CodeEditor.Focus();
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            bool originalVimEnabled = AppSettings.IsVimEnabled;
            bool originalSyntaxEnabled = AppSettings.IsSyntaxHighlightingEnabled;
            bool originalErrorEnabled = AppSettings.IsErrorHighlightingEnabled;
            double originalUiScale = AppSettings.UiScale;

            var settingsWin = new Window
            {
                Title = "Einstellungen",
                Width = 600,
                Height = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = BrushBgPanel,
                SystemDecorations = SystemDecorations.BorderOnly
            };

            var rootBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = BrushBgPanel,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            var mainGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("150, *"),
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(0)
            };

            var leftPanelGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Background = SolidColorBrush.Parse("#2D2D30")
            };

            var categoriesPanel = new StackPanel();

            var btnCatEditor = new Button
            {
                Content = "Editor",
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            var btnCatDisplay = new Button
            {
                Content = "Darstellung",
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(15),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            categoriesPanel.Children.Add(
                new TextBlock
                {
                    Text = "Einstellungen",
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextTitle,
                    Margin = new Thickness(15)
                }
            );
            categoriesPanel.Children.Add(btnCatEditor);
            categoriesPanel.Children.Add(btnCatDisplay);

            leftPanelGrid.Children.Add(categoriesPanel);

            var actionButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Margin = new Thickness(10)
            };

            var btnSave = new Button
            {
                Width = 50,
                Height = 40,
                Background = SolidColorBrush.Parse("#32A852"),
                CornerRadius = new CornerRadius(5),
                IsEnabled = false,
                Opacity = 0.5
            };
            btnSave.Content = LoadIcon("icons/ic_save.svg", 20);
            ToolTip.SetTip(btnSave, "Einstellungen speichern");

            var btnReset = new Button
            {
                Width = 50,
                Height = 40,
                Background = SolidColorBrush.Parse("#B43232"),
                CornerRadius = new CornerRadius(5)
            };
            btnReset.Content = LoadIcon("icons/ic_restart.svg", 20);
            ToolTip.SetTip(btnReset, "Auf Standard zurücksetzen");

            actionButtonsPanel.Children.Add(btnSave);
            actionButtonsPanel.Children.Add(btnReset);

            Grid.SetRow(actionButtonsPanel, 1);
            leftPanelGrid.Children.Add(actionButtonsPanel);

            Grid.SetRowSpan(leftPanelGrid, 2);
            mainGrid.Children.Add(leftPanelGrid);

            var rightPanel = new Border { Padding = new Thickness(20), Background = BrushBgPanel };
            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);

            // controls creation

            // syntax highlighting
            var chkSyntax = new CheckBox
            {
                Content = "Syntax-Hervorhebung",
                IsChecked = AppSettings.IsSyntaxHighlightingEnabled,
                Foreground = Brushes.White
            };

            // error highlighting
            var chkError = new CheckBox
            {
                Content = "Error-Hervorhebung",
                IsChecked = AppSettings.IsErrorHighlightingEnabled,
                Foreground = Brushes.White
            };

            // vim controls
            var chkVim = new CheckBox
            {
                Content = "Vim Steuerung",
                IsChecked = AppSettings.IsVimEnabled,
                Foreground = Brushes.White
            };

            // ui scale
            var sliderScale = new Slider
            {
                Minimum = 0.5,
                Maximum = 2.0,
                Value = AppSettings.UiScale,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var txtScaleVal = new TextBlock
            {
                Text = $"{AppSettings.UiScale:P0}",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            void CheckChanges()
            {
                bool hasChanges =
                    (chkVim.IsChecked != originalVimEnabled) ||
                    (chkSyntax.IsChecked != originalSyntaxEnabled) ||
                    (chkError.IsChecked != originalErrorEnabled) ||
                    (Math.Abs(sliderScale.Value - originalUiScale) > 0.004);

                btnSave.IsEnabled = hasChanges;
                btnSave.Opacity = hasChanges ? 1.0 : 0.5;

                btnSave.Background = hasChanges
                    ? SolidColorBrush.Parse("#32A852")
                    : SolidColorBrush.Parse("#464646");
            }

            // event handlers

            chkSyntax.IsCheckedChanged += async (s, ev) =>
            {
                if (chkSyntax.IsChecked == true && !originalSyntaxEnabled)
                {
                    await ShowWarningDialog(
                        "Syntax Highlighting",
                        "In der Abiturprüfung stehen keine Syntax-Hervorhebungen zur Verfügung. Es wird empfohlen, ohne dieses Feature zu üben."
                    );
                }

                AppSettings.IsSyntaxHighlightingEnabled = chkSyntax.IsChecked ?? false;
                ApplySyntaxHighlighting();
                CheckChanges();
            };

            chkError.IsCheckedChanged += async (s, ev) =>
            {
                if (chkError.IsChecked == true && !originalErrorEnabled)
                {
                    await ShowWarningDialog(
                       "Error-Hervorhebung",
                       "In der Prüfung müssen Fehler selbstständig gefunden oder am Besten vermieden werden, deshalb wird strengstens empfohlen ohne dieses Feature zu üben!\n\nAchtung: Diese Funktion setzt sich nach jedem Level-Wechsel zurück."
                   );
                }

                AppSettings.IsErrorHighlightingEnabled = chkError.IsChecked ?? false;

                if (AppSettings.IsErrorHighlightingEnabled)
                    UpdateDiagnostics();
                else
                    ClearDiagnostics();

                CheckChanges();
            };

            chkVim.IsCheckedChanged += (s, ev) =>
            {
                AppSettings.IsVimEnabled = chkVim.IsChecked ?? false;
                UpdateVimState();
                CheckChanges();
            };

            sliderScale.ValueChanged += (s, ev) =>
            {
                AppSettings.UiScale = ev.NewValue;
                txtScaleVal.Text = $"{ev.NewValue:P0}";
                ApplyUiScale();
                CheckChanges();
            };

            // layout assembling

            var editorSettings = new StackPanel { Spacing = 15 };
            editorSettings.Children.Add(
                new TextBlock
                {
                    Text = "Code Editor",
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                }
            );
            editorSettings.Children.Add(chkSyntax);
            editorSettings.Children.Add(chkError);
            editorSettings.Children.Add(chkVim);

            var displaySettings = new StackPanel { Spacing = 15 };
            var scalePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            scalePanel.Children.Add(sliderScale);
            scalePanel.Children.Add(txtScaleVal);

            displaySettings.Children.Add(
                new TextBlock
                {
                    Text = "Darstellung",
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 10)
                }
            );
            displaySettings.Children.Add(
                new TextBlock { Text = "UI Skalierung (Layout)", Foreground = Brushes.LightGray }
            );
            displaySettings.Children.Add(scalePanel);

            void ShowCategory(Button activeBtn, Control content)
            {
                btnCatEditor.Background = Brushes.Transparent;
                btnCatDisplay.Background = Brushes.Transparent;
                activeBtn.Background = SolidColorBrush.Parse("#3E3E42");
                rightPanel.Child = content;
            }

            btnCatEditor.Click += (s, ev) => ShowCategory(btnCatEditor, editorSettings);
            btnCatDisplay.Click += (s, ev) => ShowCategory(btnCatDisplay, displaySettings);

            ShowCategory(btnCatEditor, editorSettings);

            var closeBtn = new Button
            {
                Content = "Schließen",
                Width = 100,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(15, 8),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(20, 10),
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White
            };

            Grid.SetRow(closeBtn, 1);
            Grid.SetColumn(closeBtn, 1);
            mainGrid.Children.Add(closeBtn);

            btnSave.Click += (s, ev) =>
            {
                playerData.Settings.IsVimEnabled = AppSettings.IsVimEnabled;
                playerData.Settings.IsSyntaxHighlightingEnabled = AppSettings.IsSyntaxHighlightingEnabled;
                playerData.Settings.UiScale = AppSettings.UiScale;

                SaveSystem.Save(playerData);

                btnSave.IsEnabled = false;
            };

            closeBtn.Click += (s, ev) =>
            {
                settingsWin.Close();
            };

            btnReset.Click += async (s, ev) =>
            {
                var confirmDialog = new Window
                {
                    Title = "Einstellungen zurücksetzen?",
                    Width = 350,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SystemDecorations = SystemDecorations.BorderOnly,
                    Background = SolidColorBrush.Parse("#252526"),
                    CornerRadius = new CornerRadius(8)
                };

                var confirmGrid = new Grid
                {
                    RowDefinitions = new RowDefinitions("*, Auto"),
                    Margin = new Thickness(20)
                };

                confirmGrid.Children.Add(
                    new TextBlock
                    {
                        Text = "Möchtest du alle Einstellungen auf die Standardwerte zurücksetzen?",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 15
                    }
                );

                var btnPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                Grid.SetRow(btnPanel, 1);

                var btnYes = new Button
                {
                    Content = "Ja, zurücksetzen",
                    Background = SolidColorBrush.Parse("#B43232"),
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(15, 8)
                };

                var btnNo = new Button
                {
                    Content = "Abbrechen",
                    Background = SolidColorBrush.Parse("#3C3C3C"),
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(15, 8)
                };

                bool resetConfirmed = false;
                btnYes.Click += (_, __) =>
                {
                    resetConfirmed = true;
                    confirmDialog.Close();
                };
                btnNo.Click += (_, __) =>
                {
                    confirmDialog.Close();
                };

                btnPanel.Children.Add(btnNo);
                btnPanel.Children.Add(btnYes);
                confirmGrid.Children.Add(btnPanel);
                confirmDialog.Content = confirmGrid;

                await confirmDialog.ShowDialog(settingsWin);

                if (resetConfirmed)
                {
                    chkVim.IsChecked = false;
                    chkSyntax.IsChecked = false;
                    chkError.IsChecked = false;
                }
            };

            // close (revert if not saved)
            settingsWin.Closing += (s, ev) =>
            {
                if (btnSave.IsEnabled)
                {
                    AppSettings.IsVimEnabled = originalVimEnabled;
                    AppSettings.IsSyntaxHighlightingEnabled = originalSyntaxEnabled;
                    AppSettings.IsErrorHighlightingEnabled = originalErrorEnabled;
                    AppSettings.UiScale = originalUiScale;

                    UpdateVimState();
                    ApplySyntaxHighlighting();
                    ApplyUiScale();
                    ClearDiagnostics();
                }
            };

            rootBorder.Child = mainGrid;
            settingsWin.Content = rootBorder;
            settingsWin.ShowDialog(this);
        }

        private async System.Threading.Tasks.Task ShowWarningDialog(string title, string message)
        {
            var dialog = new Window
            {
                Title = "Hinweis",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            var mainGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(20)
            };

            var contentPanel = new StackPanel
            {
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentPanel.Children.Add(
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextHighlight,
                    FontSize = 16
                }
            );
            contentPanel.Children.Add(
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White
                }
            );

            mainGrid.Children.Add(contentPanel);

            var btn = new Button
            {
                Content = "Verstanden",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = BrushTextTitle,
                Foreground = Brushes.White,
                Padding = new Thickness(20, 8),
                CornerRadius = new CornerRadius(4)
            };
            btn.Click += (_, __) => dialog.Close();

            Grid.SetRow(btn, 1);
            mainGrid.Children.Add(btn);

            dialog.Content = mainGrid;
            await dialog.ShowDialog(this);
        }

        private void ApplyUiScale()
        {
            var control = this.FindControl<LayoutTransformControl>("RootScaleTransform");
            if (control != null)
            {
                control.LayoutTransform = new ScaleTransform(
                    AppSettings.UiScale,
                    AppSettings.UiScale
                );
            }
        }

        private void UpdateVimState()
        {
            if (TabVim != null)
            {
                TabVim.IsVisible = AppSettings.IsVimEnabled;
                UpdateVimUI();
                if (AppSettings.IsVimEnabled)
                {
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
                    ("ESC", "Zurück zum Normal Mode")
                }
            );

            AddCategory(
                VimCol1,
                "Bewegung (Normal)",
                new[]
                {
                    ("h", "Links"),
                    ("j", "Unten"),
                    ("k", "Oben"),
                    ("l", "Rechts"),
                    ("w", "Wortanfang vorwärts"),
                    ("b", "Wortanfang rückwärts"),
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
                    ("x", "Zeichen löschen"),
                    ("dd", "Ganze Zeile löschen"),
                    ("D", "Löschen bis Zeilenende"),
                    ("dw", "Wort löschen"),
                    ("u", "Rückgängig (Undo)"),
                    ("Ctrl+r", "Wiederholen (Redo)"),
                    ("r", "Ein Zeichen ersetzen"),
                    ("yy", "Zeile kopieren (Yank)"),
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

        private void ApplySyntaxHighlighting()
        {
            if (AppSettings.IsSyntaxHighlightingEnabled)
            {
                CodeEditor.SyntaxHighlighting = GetDarkCsharpHighlighting();
            }
            else
            {
                CodeEditor.SyntaxHighlighting = null;
            }
        }

        private IHighlightingDefinition GetDarkCsharpHighlighting()
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
				<Span> <!-- preprocessor directives that allow comments -->
					<Begin fontWeight=""bold"">region</Begin>
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
        <!-- Fallback for standard types often found in Abitur code -->
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
</SyntaxDefinition>";

            using (var reader = XmlReader.Create(new StringReader(xshd)))
            {
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }

        // --- VIM LOGIC ---

        private void HandleVimNormalInput(KeyEventArgs e)
        {
            var textArea = CodeEditor.TextArea;
            string keyChar = e.KeySymbol;

            if (e.Key == Key.Up) keyChar = "k";
            else if (e.Key == Key.Down) keyChar = "j";
            else if (e.Key == Key.Left) keyChar = "h";
            else if (e.Key == Key.Right) keyChar = "l";

            if (e.Key == Key.G && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "G";
            else if (e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) keyChar = "D";

            // redo (ctrl + r)
            if (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                CodeEditor.Redo();
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

            // single key commands
            switch (keyChar)
            {
                // --- MODE SWITCHING ---
                case "i":
                    _vimMode = VimMode.Insert;
                    break;
                case "a":
                    CodeEditor.CaretOffset = Math.Min(CodeEditor.Document.TextLength, CodeEditor.CaretOffset + 1);
                    _vimMode = VimMode.Insert;
                    break;
                case "o":
                    var currentLineO = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    CodeEditor.CaretOffset = currentLineO.EndOffset;
                    textArea.PerformTextInput("\n");
                    _vimMode = VimMode.Insert;
                    break;
                case "O":
                    var currentLineBigO = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    CodeEditor.CaretOffset = currentLineBigO.Offset;
                    textArea.PerformTextInput("\n");
                    var newLine = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    if (newLine.PreviousLine != null)
                        CodeEditor.CaretOffset = newLine.PreviousLine.Offset;
                    _vimMode = VimMode.Insert;
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
                    int lineStart = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset).Offset;
                    if (CodeEditor.CaretOffset > lineStart)
                        CodeEditor.CaretOffset--;
                    break;
                case "l":
                    int lineEnd = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset).EndOffset;
                    if (CodeEditor.CaretOffset < lineEnd)
                        CodeEditor.CaretOffset++;
                    break;
                case "j":
                    if (CodeEditor.TextArea.Caret.Line < CodeEditor.Document.LineCount)
                        CodeEditor.TextArea.Caret.Line++;
                    CodeEditor.TextArea.Caret.BringCaretToView();
                    break;
                case "k":
                    if (CodeEditor.TextArea.Caret.Line > 1)
                        CodeEditor.TextArea.Caret.Line--;
                    CodeEditor.TextArea.Caret.BringCaretToView();
                    break;
                case "w": // simple word forward
                    int nextSpace = CodeEditor.Text.IndexOfAny(new[] { ' ', '\n', '\t', '.', '(', ')', ';' }, CodeEditor.CaretOffset + 1);
                    if (nextSpace != -1) CodeEditor.CaretOffset = nextSpace + 1;
                    break;
                case "b": // simple word backward
                    int prevSpace = CodeEditor.Text.LastIndexOfAny(new[] { ' ', '\n', '\t', '.', '(', ')', ';' }, Math.Max(0, CodeEditor.CaretOffset - 2));
                    if (prevSpace != -1) CodeEditor.CaretOffset = prevSpace + 1;
                    else CodeEditor.CaretOffset = 0;
                    break;
                case "0": // line start
                    var lineStart1 = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    CodeEditor.CaretOffset = lineStart1.Offset;
                    break;

                case "$": // line end
                    var lineEnd1 = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    CodeEditor.CaretOffset = lineEnd1.EndOffset;
                    break;

                // --- EDITING ---
                case "x": // delete char
                    if (CodeEditor.Document.TextLength > CodeEditor.CaretOffset)
                        CodeEditor.Document.Remove(CodeEditor.CaretOffset, 1);
                    break;
                case "u": // Undo
                    CodeEditor.Undo();
                    break;
                case "r": // replace single char
                    if (CodeEditor.Document.TextLength > CodeEditor.CaretOffset)
                    {
                        CodeEditor.Document.Remove(CodeEditor.CaretOffset, 1);
                        _vimMode = VimMode.Insert;
                    }
                    break;
                case "p":
                    if (!string.IsNullOrEmpty(_vimClipboard))
                    {
                        CodeEditor.Document.Insert(CodeEditor.CaretOffset, _vimClipboard);
                    }
                    break;

                // --- MULTI-KEY STARTERS ---
                case "g":
                case "d":
                case "y":
                    _vimCommandBuffer = keyChar;
                    _vimMode = VimMode.CommandPending;
                    break;
            }

            UpdateVimUI();
        }

        private void CompleteVimCommand(string key)
        {
            var textArea = CodeEditor.TextArea;
            string cmd = _vimCommandBuffer + key;

            // --- MOVEMENT COMMANDS ---
            if (cmd == "gg")
            {
                CodeEditor.CaretOffset = 0;
            }
            else if (cmd == "G" || (_vimCommandBuffer == "" && key == "G"))
            {
                CodeEditor.CaretOffset = CodeEditor.Document.TextLength;
            }

            // --- DELETION COMMANDS ---
            else if (cmd == "dd")
            {
                var line = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                _vimClipboard = CodeEditor.Document.GetText(line.Offset, line.TotalLength); // Save to buffer
                CodeEditor.Document.Remove(line.Offset, line.TotalLength);
            }
            else if (cmd == "dw")
            {
                int start = CodeEditor.CaretOffset;
                int nextSpace = CodeEditor.Text.IndexOfAny(new[] { ' ', '\n', '\t' }, start + 1);
                if (nextSpace == -1) nextSpace = CodeEditor.Document.TextLength;
                else nextSpace++;

                int len = nextSpace - start;
                _vimClipboard = CodeEditor.Document.GetText(start, len);
                CodeEditor.Document.Remove(start, len);
            }
            else if (cmd == "D")
            {
                var line = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                int len = (line.Offset + line.Length) - CodeEditor.CaretOffset;
                if (len > 0)
                {
                    _vimClipboard = CodeEditor.Document.GetText(CodeEditor.CaretOffset, len);
                    CodeEditor.Document.Remove(CodeEditor.CaretOffset, len);
                }
            }

            // --- YANK ---
            else if (cmd == "yy")
            {
                var line = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                _vimClipboard = CodeEditor.Document.GetText(line.Offset, line.TotalLength);
                TxtConsole.Text += "\n> Zeile kopiert.";
            }

            _vimCommandBuffer = "";
            _vimMode = VimMode.Normal;
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
                    _vimCommandBuffer = _vimCommandBuffer.Substring(0, _vimCommandBuffer.Length - 1);
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
                int index = CodeEditor.Text.IndexOf(searchTerm, CodeEditor.CaretOffset + 1, StringComparison.OrdinalIgnoreCase);
                if (index == -1) // wrap around
                    index = CodeEditor.Text.IndexOf(searchTerm, 0, StringComparison.OrdinalIgnoreCase);

                if (index != -1)
                {
                    CodeEditor.CaretOffset = index;
                    CodeEditor.TextArea.Caret.BringCaretToView();
                }

                _vimMode = VimMode.Normal;
                _vimCommandBuffer = "";
            }
            else if (e.Key == Key.Back)
            {
                if (_vimCommandBuffer.Length > 1)
                    _vimCommandBuffer = _vimCommandBuffer.Substring(0, _vimCommandBuffer.Length - 1);
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
                SaveCurrentProgress();
                TxtConsole.Text += "\n> :w (Gespeichert)";
            }
            else if (cmd.StartsWith(":q"))
            {
                TxtConsole.Text += "\n> :q (Wichtig zu testen!)";
            }
            else if (cmd.StartsWith(":wq"))
            {
                SaveCurrentProgress();
                TxtConsole.Text += "\n> :wq (Gespeichert)";
            }
            // line jump
            else if (cmd.StartsWith(":") && int.TryParse(cmd.Substring(1), out int lineNum))
            {
                if (lineNum > 0 && lineNum <= CodeEditor.Document.LineCount)
                {
                    var line = CodeEditor.Document.GetLineByNumber(lineNum);
                    CodeEditor.CaretOffset = line.Offset;
                    CodeEditor.TextArea.Caret.BringCaretToView();
                }
            }
        }

        private void UpdateVimUI()
        {
            if (!AppSettings.IsVimEnabled)
            {
                VimStatusBorder.IsVisible = false;
                CodeEditor.Cursor = Cursor.Default;
                return;
            }

            VimStatusBorder.IsVisible = true;

            switch (_vimMode)
            {
                case VimMode.Normal:
                    VimStatusBorder.Background = SolidColorBrush.Parse("#007ACC"); // blue
                    VimStatusBar.Text = "-- NORMAL --";
                    break;
                case VimMode.Insert:
                    VimStatusBorder.Background = SolidColorBrush.Parse("#28a745"); // green
                    VimStatusBar.Text = "-- INSERT --";
                    break;
                case VimMode.CommandPending:
                    VimStatusBorder.Background = SolidColorBrush.Parse("#d08770"); // orange
                    VimStatusBar.Text = _vimCommandBuffer;
                    break;
                case VimMode.CommandLine:
                case VimMode.Search:
                    VimStatusBorder.Background = SolidColorBrush.Parse("#444");
                    VimStatusBar.Text = _vimCommandBuffer;
                    break;
            }
        }
    }
}
