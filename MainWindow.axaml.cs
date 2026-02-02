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
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
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

namespace AbiturEliteCode
{
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

        private SolidColorBrush BrushTextNormal = SolidColorBrush.Parse("#E6E6E6");
        private SolidColorBrush BrushTextHighlight = SolidColorBrush.Parse("#6495ED"); // blue
        private SolidColorBrush BrushBgPanel = SolidColorBrush.Parse("#202124");

        public MainWindow()
        {
            InitializeComponent();

            var transformGroup = (TransformGroup)ImgDiagram.RenderTransform;
            ImgScale = (ScaleTransform)transformGroup.Children[0];
            ImgTranslate = (TranslateTransform)transformGroup.Children[1];

            levels = Curriculum.GetLevels();
            playerData = SaveSystem.Load();

            ConfigureEditor();

            autoSaveTimer = new System.Timers.Timer(2000)
            {
                AutoReset = false
            };
            autoSaveTimer.Elapsed += (s, e) => Dispatcher.UIThread.InvokeAsync(SaveCurrentProgress);

            CodeEditor.TextChanged += (s, e) =>
            {
                autoSaveTimer.Stop();
                autoSaveTimer.Start();
            };

            int maxId = playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1;
            var startLevel = levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0];
            LoadLevel(startLevel);

            this.Opened += (s, e) => CodeEditor.Focus();

            // return focus to editor when clicking somewhere random
            this.AddHandler(PointerPressedEvent, (s, e) =>
            {
                var source = e.Source as Control;
                if (source is TextBox || source is Button || (source?.Parent is Button)) return;

                if (source?.Name == "DiagramPanel" || source?.Name == "ImgDiagram") return;

                Dispatcher.UIThread.Post(() => CodeEditor.Focus());
            }, RoutingStrategies.Tunnel);

            // handle tab switching focus
            var tabs = this.FindControl<TabControl>("MainTabs");
            if (tabs != null)
            {
                tabs.SelectionChanged += (sender, args) =>
                {
                    if (sender == tabs && args.AddedItems.Count > 0 && args.AddedItems[0] is TabItem)
                    {
                        Dispatcher.UIThread.Post(() => CodeEditor.Focus(), DispatcherPriority.Input);
                    }
                };
            }
        }

        private Image LoadIcon(string path, double size)
        {
            var image = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform
            };

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

        private void ConfigureEditor()
        {
            CodeEditor.SyntaxHighlighting = null;
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

            CodeEditor.TextArea.Caret.PositionChanged += (s, e) =>
            {
                CodeEditor.TextArea.Caret.BringCaretToView();
            };

            CodeEditor.TextArea.TextEntering += Editor_TextEntering;
            CodeEditor.AddHandler(InputElement.KeyDownEvent, CodeEditor_KeyDown, RoutingStrategies.Tunnel);
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

                    if ((charBefore == '(' && charAfter == ')') ||
                        (charBefore == '{' && charAfter == '}') ||
                        (charBefore == '[' && charAfter == ']') ||
                        (charBefore == '"' && charAfter == '"'))
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
        }

        private void Editor_TextEntering(object sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            char charTyped = e.Text[0];
            TextArea textArea = (TextArea)sender;
            int offset = textArea.Caret.Offset;

            if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"')
            {
                string pair = charTyped == '(' ? ")" :
                              charTyped == '{' ? "}" :
                              charTyped == '[' ? "]" : "\"";

                textArea.Document.Insert(offset, charTyped.ToString() + pair);
                textArea.Caret.Offset = offset + 1;
                e.Handled = true;
                return;
            }

            if (charTyped == ')' || charTyped == '}' || charTyped == ']' || charTyped == '"')
            {
                if (offset < textArea.Document.TextLength &&
                    textArea.Document.GetCharAt(offset) == charTyped)
                {
                    textArea.Caret.Offset += 1;
                    e.Handled = true;
                    return;
                }
            }

            if (e.Text == "\n" || e.Text == "\r")
            {
                char prev = offset > 0 ? textArea.Document.GetCharAt(offset - 1) : '\0';
                char next = offset < textArea.Document.TextLength ? textArea.Document.GetCharAt(offset) : '\0';

                var currentLine = textArea.Document.GetLineByOffset(offset);
                string lineText = textArea.Document.GetText(currentLine);

                string indent = "";
                foreach (char c in lineText)
                {
                    if (char.IsWhiteSpace(c)) indent += c;
                    else break;
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
                {
                    indent += "    ";
                }

                textArea.Document.Insert(offset, "\n" + indent);
                e.Handled = true;
            }
        }

        private void SaveCurrentProgress()
        {
            if (currentLevel != null)
            {
                playerData.UserCode[currentLevel.Id] = CodeEditor.Text;
                SaveSystem.Save(playerData);
            }
        }

        private void LoadLevel(Level level)
        {
            SaveCurrentProgress();
            currentLevel = level;
            BtnNextLevel.IsVisible = false;

            string rawCode = playerData.UserCode.ContainsKey(level.Id) ? playerData.UserCode[level.Id] : level.StarterCode;
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

            PnlTask.Children.Add(new SelectableTextBlock
            {
                Text = $"{level.Id}. {level.Title}",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextNormal,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            RenderRichText(PnlTask, level.Description);

            try
            {
                if (!string.IsNullOrEmpty(level.DiagramPath))
                {
                    string safePath = level.DiagramPath.Replace("\\", Path.DirectorySeparatorChar.ToString());
                    string fullPath = Path.Combine(AppContext.BaseDirectory, safePath);

                    if (File.Exists(fullPath))
                        ImgDiagram.Source = new Bitmap(fullPath);
                    else
                        ImgDiagram.Source = null;
                }
                else ImgDiagram.Source = null;
            }
            catch { ImgDiagram.Source = null; }

            GenerateMaterials(level);

            TxtConsole.Foreground = Brushes.LightGray;
            TxtConsole.Text = $"> System initialisiert.\n> Level {level.Id} (Code: {level.SkipCode}) geladen.";

            Dispatcher.UIThread.Post(() => CodeEditor.Focus());
        }

        private void RenderRichText(StackPanel panel, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
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
                    tb.Inlines.Add(new Run
                    {
                        Text = content,
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextHighlight,
                        FontFamily = new FontFamily("Consolas")
                    });
                }
                else
                {
                    string content = part.Replace("\x01", "[").Replace("\x02", "]");
                    tb.Inlines.Add(new Run
                    {
                        Text = content,
                        Foreground = BrushTextNormal
                    });
                }
            }
            panel.Children.Add(tb);
        }


        private void Diagram_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (ImgScale == null || ImgTranslate == null) return;

            double zoomSpeed = 0.1;
            double oldScale = _currentScale;

            if (e.Delta.Y > 0) _currentScale += zoomSpeed;
            else _currentScale -= zoomSpeed;

            if (_currentScale < 0.1) _currentScale = 0.1;
            if (_currentScale > 5.0) _currentScale = 5.0;

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
            if (!_isDragging || ImgTranslate == null) return;

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
                string auxPath = Path.Combine(AppContext.BaseDirectory, "img", $"aux_{level.AuxiliaryId}.png");
                if (File.Exists(auxPath))
                {
                    PnlMaterials.Children.Add(new SelectableTextBlock
                    {
                        Text = "Referenz-Klassen:",
                        FontWeight = FontWeight.Bold,
                        Foreground = SolidColorBrush.Parse("#32A852"),
                        Margin = new Thickness(0, 0, 0, 5)
                    });
                    PnlMaterials.Children.Add(new Image
                    {
                        Source = new Bitmap(auxPath),
                        Height = 150,
                        Stretch = Stretch.Uniform,
                        Margin = new Thickness(0, 0, 0, 15),
                        HorizontalAlignment = HorizontalAlignment.Left
                    });
                }
            }

            if (!string.IsNullOrEmpty(level.MaterialDocs))
            {
                var lines = level.MaterialDocs.Split('\n');
                foreach (var line in lines)
                {
                    string trim = line.Trim();
                    if (trim.StartsWith("Hinweis:") || trim.StartsWith("Tipp:"))
                    {
                        string preview = trim.Length > 18 ? trim.Substring(0, 15) + "..." : trim;
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

            string fullCode = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" + CodeEditor.Text;

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
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    TxtConsole.Foreground = Brushes.Red;
                    TxtConsole.Text = "KOMPILIERFEHLER:\n";
                    foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var lineSpan = diag.Location.GetLineSpan();
                        int userLine = lineSpan.StartLinePosition.Line - 3;
                        if (userLine < 0) userLine = 0;
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
            bool success = false;
            string feedback = "";

            try
            {
                // --- LEVEL 1 ---
                if (currentLevel.Id == 1)
                {
                    Type tierType = assembly.GetType("Tier");
                    if (tierType == null) throw new Exception("Klasse 'Tier' nicht gefunden. Stelle sicher, dass du 'public class Tier' geschrieben hast.");

                    ConstructorInfo ctor = tierType.GetConstructor(new[] { typeof(string), typeof(int) });
                    if (ctor == null) throw new Exception("Konstruktor Tier(string, int) fehlt. Füge einen Konstruktor mit zwei Parametern hinzu: public Tier(string name, int alter)");

                    object tier = ctor.Invoke(new object[] { "Löwe", 5 });
                    FieldInfo fName = tierType.GetField("name", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo fAlter = tierType.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fName == null) throw new Exception("Feld 'name' fehlt oder ist nicht private. Füge hinzu: private string name;");
                    if (fAlter == null) throw new Exception("Feld 'alter' fehlt oder ist nicht private. Füge hinzu: private int alter;");

                    string actualName = (string)fName.GetValue(tier);
                    int actualAlter = (int)fAlter.GetValue(tier);

                    if (actualName == "Löwe" && actualAlter == 5)
                    {
                        success = true;
                        feedback = "Klasse Tier korrekt implementiert! Felder und Konstruktor funktionieren.";
                    }
                    else
                    {
                        throw new Exception("Konstruktor setzt die Werte nicht korrekt. Im Konstruktor: this.name = name; und this.alter = alter;");
                    }
                }
                // --- LEVEL 2 ---
                else if (currentLevel.Id == 2)
                {
                    Type t = assembly.GetType("Tier");
                    if (t == null) throw new Exception("Klasse 'Tier' nicht gefunden. Hast du sie gelöscht?");

                    object obj = Activator.CreateInstance(t);

                    MethodInfo mSet = t.GetMethod("SetAlter");
                    MethodInfo mGet = t.GetMethod("GetAlter");
                    FieldInfo fAlter = t.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (mSet == null) throw new Exception("Methode SetAlter fehlt. Erstelle: public void SetAlter(int neuesAlter)");
                    if (mGet == null) throw new Exception("Methode GetAlter fehlt. Erstelle: public int GetAlter()");

                    // initial value check
                    fAlter.SetValue(obj, 10);

                    // test 1: invalid (lower)
                    mSet.Invoke(obj, new object[] { 5 });
                    int val1 = (int)mGet.Invoke(obj, null);
                    if (val1 != 10) throw new Exception("Fehler: Alter wurde trotz kleinerem Wert geändert! SetAlter muss prüfen: if (neuesAlter > alter)");

                    // test 2: valid (higher)
                    mSet.Invoke(obj, new object[] { 12 });
                    int val2 = (int)mGet.Invoke(obj, null);
                    if (val2 != 12) throw new Exception("Fehler: Alter wurde trotz gültigem Wert nicht geändert. Setze alter = neuesAlter wenn die Bedingung erfüllt ist.");

                    success = true;
                    feedback = "Kapselung und Validierung erfolgreich implementiert!";
                }
                // --- LEVEL 3 ---
                else if (currentLevel.Id == 3)
                {
                    Type tTier = assembly.GetType("Tier");
                    Type tLoewe = assembly.GetType("Loewe");

                    if (tTier == null) throw new Exception("Klasse Tier fehlt. Erstelle: public abstract class Tier");
                    if (tLoewe == null) throw new Exception("Klasse Loewe fehlt. Erstelle: public class Loewe : Tier");

                    if (!tTier.IsAbstract) throw new Exception("Klasse Tier muss 'abstract' sein. Schreibe: public abstract class Tier");
                    if (!tLoewe.IsSubclassOf(tTier)) throw new Exception("Loewe erbt nicht von Tier. Füge hinzu: public class Loewe : Tier");

                    // check constructor chaining
                    ConstructorInfo ctor = tLoewe.GetConstructor(new[] { typeof(string), typeof(int) });
                    if (ctor == null) throw new Exception("Konstruktor Loewe(string, int) fehlt. Erstelle: public Loewe(string name, int laenge) : base(name)");

                    // we cannot instantiate Tier, but we can instantiate Loewe
                    object leo = ctor.Invoke(new object[] { "Simba", 50 });

                    // check Bruellen
                    MethodInfo mB = tLoewe.GetMethod("Bruellen");
                    if (mB == null) throw new Exception("Methode Bruellen fehlt. Erstelle: public string Bruellen()");

                    string sound = (string)mB.Invoke(leo, null);
                    if (string.IsNullOrEmpty(sound)) throw new Exception("Bruellen gibt nichts zurück. Die Methode sollte einen String zurückgeben.");

                    success = true;
                    feedback = "Vererbung und Abstraktion korrekt implementiert!";
                }
                // --- LEVEL 4 ---
                else if (currentLevel.Id == 4)
                {
                    Type tG = assembly.GetType("Gehege");
                    Type tTier = assembly.GetType("Tier");

                    if (tG == null) throw new Exception("Klasse 'Gehege' nicht gefunden.");
                    if (tTier == null) throw new Exception("Klasse 'Tier' nicht gefunden.");

                    // check if Tier is abstract
                    if (tTier.IsAbstract)
                    {
                        throw new Exception("Für dieses Level muss Klasse 'Tier' konkret (nicht abstract) sein.");
                    }

                    // create Gehege instance
                    object g;
                    try
                    {
                        g = Activator.CreateInstance(tG);
                    }
                    catch
                    {
                        throw new Exception("Gehege konnte nicht instanziiert werden. Prüfe den Konstruktor.");
                    }

                    if (g == null) throw new Exception("Gehege-Instanz ist null.");

                    // find methods
                    MethodInfo mAdd = tG.GetMethod("Hinzufuegen");
                    MethodInfo mCount = tG.GetMethod("AnzahlTiere");

                    if (mAdd == null) throw new Exception("Methode 'Hinzufuegen' nicht gefunden.");
                    if (mCount == null) throw new Exception("Methode 'AnzahlTiere' nicht gefunden.");

                    // check method signatures
                    var addParams = mAdd.GetParameters();
                    if (addParams.Length != 1 || addParams[0].ParameterType != tTier)
                    {
                        throw new Exception("Methode Hinzufuegen muss Parameter vom Typ Tier haben.");
                    }

                    if (mCount.ReturnType != typeof(int))
                    {
                        throw new Exception("Methode AnzahlTiere muss int zurückgeben.");
                    }

                    // create Tier instance
                    object animal;
                    try
                    {
                        animal = Activator.CreateInstance(tTier);
                    }
                    catch
                    {
                        throw new Exception("Tier konnte nicht instanziiert werden. Tier braucht einen Konstruktor ohne Parameter.");
                    }

                    if (animal == null) throw new Exception("Tier-Instanz ist null.");

                    // test initial count
                    int initialCount;
                    try
                    {
                        initialCount = (int)mCount.Invoke(g, null);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Fehler bei AnzahlTiere: {ex.Message}");
                    }

                    if (initialCount != 0)
                    {
                        throw new Exception($"AnzahlTiere sollte initial 0 sein, ist aber {initialCount}. Initialisiere die Liste im Konstruktor.");
                    }

                    // add one animal
                    try
                    {
                        mAdd.Invoke(g, new object[] { animal });
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Fehler bei Hinzufuegen: {ex.Message}");
                    }

                    // test count after adding
                    int countAfterAdd;
                    try
                    {
                        countAfterAdd = (int)mCount.Invoke(g, null);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Fehler bei AnzahlTiere: {ex.Message}");
                    }

                    if (countAfterAdd == 1)
                    {
                        success = true;
                        feedback = "Gehege und List<Tier> korrekt implementiert!";
                    }
                    else
                    {
                        throw new Exception($"AnzahlTiere gibt {countAfterAdd} statt 1 zurück. Prüfe die Liste.");
                    }
                }
                // --- LEVEL 5 ---
                else if (currentLevel.Id == 5)
                {
                    Type tG = assembly.GetType("Gehege");
                    if (tG == null) throw new Exception("Klasse 'Gehege' fehlt.");

                    Type tT = assembly.GetType("Tier");
                    if (tT == null) throw new Exception("Klasse 'Tier' fehlt.");

                    object g = Activator.CreateInstance(tG);

                    object CreateTier(int age)
                    {
                        return Activator.CreateInstance(tT, new object[] { age });
                    }

                    object t1 = CreateTier(5);
                    object t2 = CreateTier(20);
                    object t3 = CreateTier(10);

                    FieldInfo fList = tG.GetField("bewohner");
                    if (fList == null) throw new Exception("Feld 'bewohner' fehlt.");

                    MethodInfo mGetAlter = tT.GetMethod("GetAlter");
                    if (mGetAlter == null) throw new Exception("Methode 'GetAlter()' fehlt.");

                    var listInstance = fList.GetValue(g);
                    if (listInstance == null) throw new Exception("Liste 'bewohner' ist null.");

                    MethodInfo listAdd = listInstance.GetType().GetMethod("Add");

                    listAdd.Invoke(listInstance, new object[] { t1 });
                    listAdd.Invoke(listInstance, new object[] { t2 });
                    listAdd.Invoke(listInstance, new object[] { t3 });

                    MethodInfo mAlgo = tG.GetMethod("ErmittleAeltestes");
                    if (mAlgo == null) throw new Exception("Methode 'ErmittleAeltestes' fehlt.");

                    if (mAlgo.ReturnType != tT) throw new Exception("Rückgabetyp muss 'Tier' sein.");

                    object result = mAlgo.Invoke(g, null);

                    if (result == null) throw new Exception("Methode gibt null zurück.");

                    int resultAge = (int)mGetAlter.Invoke(result, null);

                    if (result == t2 && resultAge == 20)
                    {
                        success = true;
                        feedback = "Mini-Prüfung bestanden! Algorithmus korrekt.";
                    }
                    else
                    {
                        throw new Exception($"Falsches Tier zurückgegeben (Alter: {resultAge}, erwartet: 20).");
                    }

                    // test empty list
                    var emptyGehege = Activator.CreateInstance(tG);
                    object emptyResult = mAlgo.Invoke(emptyGehege, null);
                    if (emptyResult != null)
                    {
                        throw new Exception("Bei leerer Liste muss null zurückgegeben werden.");
                    }
                }

                if (success)
                {
                    TxtConsole.Foreground = Brushes.LightGreen;
                    TxtConsole.Text = "✓ TEST BESTANDEN: " + feedback + "\n\n";

                    // unlock logic

                    if (!playerData.CompletedLevelIds.Contains(currentLevel.Id))
                    {
                        playerData.CompletedLevelIds.Add(currentLevel.Id);
                    }

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
                    else // final level
                    {
                        TxtConsole.Text += "\n🎉 Das war das letzte Level dieser Sektion!";
                        BtnNextLevel.Content = "KURS ABSCHLIESSEN ✓";
                        BtnNextLevel.IsVisible = true;
                    }

                    SaveSystem.Save(playerData);
                }
            }
            catch (Exception ex)
            {
                TxtConsole.Foreground = Brushes.Orange;
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                TxtConsole.Text = "❌ LAUFZEITFEHLER / LOGIK:\n" + msg;
            }
        }

        private void BtnNextLevel_Click(object sender, RoutedEventArgs e)
        {
            // check if button is in finish mode
            if (BtnNextLevel.Content?.ToString()?.Contains("ABSCHLIESSEN") == true)
            {
                ShowCourseCompletedDialog();
                return;
            }

            var nextLvl = levels.FirstOrDefault(l => l.SkipCode == currentLevel.NextLevelCode);
            if (nextLvl != null)
            {
                LoadLevel(nextLvl);
            }

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

            var rootGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto")
            };

            var contentStack = new StackPanel
            {
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            contentStack.Children.Add(new TextBlock
            {
                Text = "🎉 Herzlichen Glückwunsch! 🎉",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = SolidColorBrush.Parse("#32A852"),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            contentStack.Children.Add(new TextBlock
            {
                Text = "Du hast alle Levels erfolgreich abgeschlossen!\n\nDu bist nun bereit für deine Abiturprüfung in Praktischer Informatik.\nViel Erfolg!",
                FontSize = 16,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24
            });

            rootGrid.Children.Add(contentStack);

            var btnClose = new Button
            {
                Content = "Schließen",
                Background = SolidColorBrush.Parse("#32A852"),
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

            rootGrid.Children.Add(new TextBlock
            {
                Text = "Möchtest du den Code wirklich zurücksetzen? Alle Änderungen gehen verloren.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Margin = new Thickness(0, 20, 0, 0)
            };
            Grid.SetRow(btnPanel, 1);

            var btnYes = new Button { Content = "Ja, zurücksetzen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
            var btnNo = new Button { Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };

            bool result = false;
            btnYes.Click += (_, __) => { result = true; dialog.Close(); };
            btnNo.Click += (_, __) => { dialog.Close(); };

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

            var root = new Border { CornerRadius = new CornerRadius(8), Background = BrushBgPanel, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1) };

            var mainGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto, *, Auto"),
                Margin = new Thickness(15)
            };

            mainGrid.Children.Add(new StackPanel
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
                            Foreground = SolidColorBrush.Parse("#32A852"),
                            FontWeight = FontWeight.Bold,
                            FontSize = 14
                        }
                    }
                }
            });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);
            var levelStack = new StackPanel
            {
                Spacing = 8
            };

            var groups = levels.GroupBy(l => l.Section);

            foreach (var group in groups)
            {
                bool isSectionComplete = group.All(l => playerData.CompletedLevelIds.Contains(l.Id));

                var headerPanel = new StackPanel 
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };
                headerPanel.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    Foreground = SolidColorBrush.Parse("#32A852"),
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                });

                if (isSectionComplete)
                {
                    headerPanel.Children.Add(LoadIcon("icons/ic_done.svg", 16));
                }

                var sectionContent = new StackPanel
                { Spacing = 5,
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

                    string iconPath = completed ? "icons/ic_check.svg" : (unlocked ? "icons/ic_lock_open.svg" : "icons/ic_lock.svg");
                    btnContent.Children.Add(LoadIcon(iconPath, 16));

                    btnContent.Children.Add(new TextBlock
                    {
                        Text = $"{lvl.Id}. {lvl.Title}",
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var btn = new Button
                    {
                        Content = btnContent,
                        IsEnabled = unlocked,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(10, 10),
                        Background = unlocked ? SolidColorBrush.Parse("#313133") : SolidColorBrush.Parse("#191919"),
                        Foreground = unlocked ? Brushes.White : Brushes.Gray,
                        CornerRadius = new CornerRadius(4)
                    };
                    btn.Click += (_, __) => { LoadLevel(lvl); win.Close(); };
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
    }
}