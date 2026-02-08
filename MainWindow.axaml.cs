using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static AbiturEliteCode.cs.LevelDraft;

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

        private System.Threading.CancellationTokenSource? _compilationCts;
        private TextMarkerService _textMarkerService;
        private UnusedCodeTransformer _unusedCodeTransformer;
        private DispatcherTimer _diagnosticTimer;
        private GhostCharacterTransformer _ghostCharTransformer;
        private BracketHighlightRenderer _bracketHighlightRenderer;
        private IndentationGuideRenderer _indentationGuideRenderer;

        private bool _hasRunOnce = false;

        private const string MonospaceFontFamily = "Consolas, Menlo, Monaco, DejaVu Sans Mono, Roboto Mono, Courier New, monospace";

        private string _currentCustomValidationCode = null;
        private bool _isCustomLevelMode = false;
        private CustomPlayerData customPlayerData;

        private bool _isLoadingDesigner = false;
        private bool _isDesignerMode = false;
        private string _currentDraftPath = "";
        private LevelDraft _currentDraft = new LevelDraft();
        private System.Timers.Timer _designerAutoSaveTimer;
        private DispatcherTimer _designerSyncTimer;
        private string _lastSavedDraftJson = "";
        private bool _originalSyntaxSetting;
        private bool _originalErrorSetting;
        private LevelDraft _verifiedDraftState = null;
        private const int MaxPrerequisites = 8;
        private int _activeDiagramIndex = 0;
        private string _currentCustomAuthor = "";
        private List<string> _currentCustomSvgs = null;
        private enum DesignerSource
        {
            None,
            StarterCode,
            Validation,
            TestingCode
        }
        private DesignerSource _activeDesignerSource = DesignerSource.None;

        private enum VimMode { Normal, Insert, CommandPending, CommandLine, Search }
        private VimMode _vimMode = VimMode.Normal;
        private string _vimCommandBuffer = ""; // for multi char commands
        private string _vimClipboard = "";
        private int _vimDesiredColumn = -1;

        private SolidColorBrush BrushTextNormal = SolidColorBrush.Parse("#E6E6E6");
        private SolidColorBrush BrushTextHighlight = SolidColorBrush.Parse("#6495ED"); // blue
        private SolidColorBrush BrushTextTitle = SolidColorBrush.Parse("#32A852"); // green
        private SolidColorBrush BrushBgPanel = SolidColorBrush.Parse("#202124");

        private class CustomLevelInfo
        {
            public string Name { get; set; }
            public string Author { get; set; }
            public string FilePath { get; set; }
            public bool IsDraft { get; set; } // .elitelvldraft = true; .elitelvl = false
        }

        public MainWindow()
        {
            InitializeComponent();

            PrerequisiteSystem.Initialize();

            var transformGroup = (TransformGroup)ImgDiagram.RenderTransform;
            ImgScale = (ScaleTransform)transformGroup.Children[0];
            ImgTranslate = (TranslateTransform)transformGroup.Children[1];

            levels = Curriculum.GetLevels();
            playerData = SaveSystem.Load();
            customPlayerData = SaveSystem.LoadCustom();

            AppSettings.IsVimEnabled = playerData.Settings.IsVimEnabled;
            AppSettings.IsSyntaxHighlightingEnabled =
                playerData.Settings.IsSyntaxHighlightingEnabled;
            AppSettings.UiScale = playerData.Settings.UiScale;

            ApplyUiScale();
            ApplySyntaxHighlighting();
            UpdateVimState();
            BuildVimCheatSheet();

            ConfigureEditor();
            UpdateShortcutsAndTooltips();

            autoSaveTimer = new System.Timers.Timer(2000) { AutoReset = false };
            autoSaveTimer.Elapsed += (s, e) => Dispatcher.UIThread.InvokeAsync(SaveCurrentProgress);

            _designerSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _designerSyncTimer.Tick += (s, e) =>
            {
                _designerSyncTimer.Stop();
                SyncEditorToDesigner();
            };

            CodeEditor.TextChanged += (s, e) =>
            {
                autoSaveTimer.Stop();
                autoSaveTimer.Start();

                // trigger sync timer
                if (_isDesignerMode && _activeDesignerSource != DesignerSource.None)
                {
                    _designerSyncTimer.Stop();
                    _designerSyncTimer.Start();
                }

                if (AppSettings.IsErrorHighlightingEnabled)
                {
                    _diagnosticTimer.Stop();
                    _diagnosticTimer.Start();
                }
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

            // level designer
            _designerAutoSaveTimer = new System.Timers.Timer(2000)
            {
                AutoReset = false
            };
            _designerAutoSaveTimer.Elapsed += async (s, e) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (ChkDesignerAutoSave.IsChecked == true)
                        SaveDesignerDraft();
                });
            };

            TxtDesignName.TextChanged += OnDesignerInputChanged;
            TxtDesignAuthor.TextChanged += OnDesignerInputChanged;
            TxtDesignDesc.TextChanged += OnDesignerInputChanged;
            TxtDesignMaterials.TextChanged += OnDesignerInputChanged;
            TxtDesignStarter.TextChanged += OnDesignerInputChanged;
            TxtDesignValidation.TextChanged += OnDesignerInputChanged;
            TxtDesignTesting.TextChanged += OnDesignerInputChanged;
            TxtDesignPlantUml.TextChanged += OnDesignerInputChanged;
            TxtDesignPrereqInput.TextChanged += (s, e) =>
            {
                string query = TxtDesignPrereqInput.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(query))
                {
                    PopupPrereqSuggestions.IsVisible = false;
                    return;
                }

                var matches = PrerequisiteSystem.AllTopics
                    .Where(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)
                             && !_currentDraft.Prerequisites.Contains(t))
                    .Take(5)
                    .ToList();

                if (matches.Count > 0)
                {
                    LstPrereqSuggestions.ItemsSource = matches;
                    PopupPrereqSuggestions.IsVisible = true;
                }
                else
                {
                    PopupPrereqSuggestions.IsVisible = false;
                }
            };

            LstPrereqSuggestions.SelectionChanged += (s, e) =>
            {
                if (LstPrereqSuggestions.SelectedItem is string selected)
                {
                    AddDesignerPrerequisite(selected);
                    TxtDesignPrereqInput.Text = "";
                    PopupPrereqSuggestions.IsVisible = false;
                    LstPrereqSuggestions.SelectionChanged -= (s, e) => { };
                    LstPrereqSuggestions.SelectedItem = null;
                }
            };
        }

        private List<MetadataReference> GetSafeReferences()
        {
            var references = new List<MetadataReference>();

            // list of assemblies needed for compilation
            var assemblies = new List<Assembly>
            {
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(Enumerable).Assembly,
                Assembly.Load("System.Runtime"),
                Assembly.Load("System.Collections")
            };

            foreach (var asm in assemblies.Distinct())
            {
                try
                {
                    if (!string.IsNullOrEmpty(asm.Location))
                    {
                        references.Add(MetadataReference.CreateFromFile(asm.Location));
                    }
                    else
                    {
                        unsafe
                        {
                            if (asm.TryGetRawMetadata(out byte* blob, out int length))
                            {
                                var moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                                var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                                references.Add(assemblyMetadata.GetReference());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load reference {asm.FullName}: {ex.Message}");
                }
            }

            return references;
        }

        private void ConfigureEditor()
        {
            CodeEditor.Options.ConvertTabsToSpaces = true;
            CodeEditor.Options.IndentationSize = 4;
            CodeEditor.Options.ShowSpaces = false;
            CodeEditor.Options.ShowTabs = false;
            CodeEditor.Options.EnableHyperlinks = false;
            CodeEditor.Options.EnableEmailHyperlinks = false;

            CodeEditor.FontFamily = new FontFamily(MonospaceFontFamily);
            CodeEditor.FontSize = 16;
            CodeEditor.Background = Brushes.Transparent;
            CodeEditor.Foreground = SolidColorBrush.Parse("#D4D4D4");

            _indentationGuideRenderer = new IndentationGuideRenderer(CodeEditor);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_indentationGuideRenderer);

            _bracketHighlightRenderer = new BracketHighlightRenderer(CodeEditor);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_bracketHighlightRenderer);

            _textMarkerService = new TextMarkerService(CodeEditor);
            CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);

            _unusedCodeTransformer = new UnusedCodeTransformer();
            CodeEditor.TextArea.TextView.LineTransformers.Add(_unusedCodeTransformer);

            _ghostCharTransformer = new GhostCharacterTransformer(CodeEditor);
            CodeEditor.TextArea.TextView.LineTransformers.Add(_ghostCharTransformer);

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
                CodeEditor.TextArea.TextView.Redraw();
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

        private void UpdateShortcutsAndTooltips()
        {
            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            string ctrlKey = isMac ? "Cmd" : "Ctrl";

            // update tooltips
            ToolTip.SetTip(BtnSave, $"Code speichern ({ctrlKey} + S)");

            if (BtnSettings.Parent is Panel parentPanel)
            {
                var runBtn = parentPanel.Children.LastOrDefault() as Button;
                if (runBtn != null)
                {
                    ToolTip.SetTip(runBtn, $"Ausführen (F5)");
                }
            }
        }

        private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
        {
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
                TxtConsole.Text += "\n> Gespeichert.";
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
                    {
                        BtnRun_Click(this, new RoutedEventArgs());
                    }
                    else
                    {
                        TxtConsole.Text += "\n> Ausführen im Designer nur im 'Test-Code' Editor möglich.";
                    }
                    e.Handled = true;
                    return;
                }

                BtnRun_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back)
            {
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

            if (!AppSettings.IsVimEnabled)
            {
                return;
            }

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

            // auto remove auto added chevron
            if (charTyped == ' ')
            {
                if (offset > 0 && offset < textArea.Document.TextLength)
                {
                    if (textArea.Document.GetCharAt(offset - 1) == '<' && textArea.Document.GetCharAt(offset) == '>')
                    {
                        textArea.Document.Remove(offset, 1);
                    }
                }
            }

            // surround selection logic
            if (textArea.Selection.Length > 0)
            {
                if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"' || charTyped == '\'' || charTyped == '<')
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
            }

            // auto add designated pair
            if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"' || charTyped == '<')
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

                            if (!isInsideControlCondition)
                            {
                                isGenericContext = true;
                            }
                        }
                    }

                    if (!isGenericContext) return; // let insert normally as operator
                }

                string pair =
                    charTyped == '(' ? ")"
                    : charTyped == '{' ? "}"
                    : charTyped == '[' ? "]"
                    : charTyped == '<' ? ">"
                    : "\"";

                textArea.Document.Insert(offset, charTyped.ToString() + pair);
                textArea.Caret.Offset = offset + 1;
                e.Handled = true;
                return;
            }

            // skip closing pair
            if (charTyped == ')' || charTyped == '}' || charTyped == ']' || charTyped == '"' || charTyped == '>')
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
            if (_isDesignerMode) return;

            if (_isCustomLevelMode && currentLevel != null)
            {
                // save custom level code
                if (customPlayerData.UserCode.ContainsKey(currentLevel.Title))
                    customPlayerData.UserCode[currentLevel.Title] = CodeEditor.Text;
                else
                    customPlayerData.UserCode.Add(currentLevel.Title, CodeEditor.Text);

                SaveSystem.SaveCustom(customPlayerData);
                return;
            }

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

        private Avalonia.Media.IImage LoadDiagramImage(string relativePath)
        {
            // check if its a full file path (custom levels)
            if (File.Exists(relativePath))
            {
                try
                {
                    if (relativePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var svgSource = SvgSource.Load(relativePath, null);
                        return new SvgImage { Source = svgSource };
                    }
                    return new Bitmap(relativePath);
                }
                catch { return null; }
            }

            relativePath = relativePath.Replace("\\", "/");
            string uriString = $"avares://AbiturEliteCode/assets/{relativePath}";

            try
            {
                var uri = new Uri(uriString);
                if (AssetLoader.Exists(uri))
                {
                    if (relativePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var svgSource = SvgSource.Load(uriString, null);
                        return new SvgImage { Source = svgSource };
                    }
                    else
                    {
                        return new Bitmap(AssetLoader.Open(uri));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load asset {uriString}: {ex.Message}");
            }
            return null;
        }

        private void LoadLevel(Level level)
        {
            if (level.Id > 0)
            {
                _isCustomLevelMode = false;
                _currentCustomValidationCode = null;
                _currentCustomAuthor = "";
                _currentCustomSvgs = null;
            }

            SaveCurrentProgress();

            // reset error highlighting on every load
            AppSettings.IsErrorHighlightingEnabled = false;
            ClearDiagnostics();

            currentLevel = level;
            BtnNextLevel.IsVisible = false;

            string rawCode = level.StarterCode;
            if (_isCustomLevelMode)
            {
                // custom levels
                if (customPlayerData.UserCode.ContainsKey(level.Title))
                {
                    rawCode = customPlayerData.UserCode[level.Title];
                }
            }
            else
            {
                // standard levels
                if (playerData.UserCode.ContainsKey(level.Id))
                {
                    rawCode = playerData.UserCode[level.Id];
                }
            }
            CodeEditor.Text = rawCode;

            // reset uml zoom
            _currentScale = 0.5;
            if (ImgScale != null)
            {
                ImgScale.ScaleX = _currentScale;
                ImgScale.ScaleY = _currentScale;
            }
            if (ImgTranslate != null)
            {
                ImgTranslate.X = 0;
                ImgTranslate.Y = 0;
            }
            if (ImgDiagram != null)
            {
                ImgDiagram.HorizontalAlignment = HorizontalAlignment.Center;
                ImgDiagram.VerticalAlignment = VerticalAlignment.Center;
            }

            PnlTask.Children.Clear();

            if (_isCustomLevelMode)
            {
                // custom level header
                PnlTask.Children.Add(
                    new SelectableTextBlock
                    {
                        Text = level.Title,
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextNormal,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0)
                    }
                );

                if (!string.IsNullOrEmpty(_currentCustomAuthor))
                {
                    PnlTask.Children.Add(
                        new SelectableTextBlock
                        {
                            Text = $"von {_currentCustomAuthor}",
                            FontSize = 14,
                            Foreground = Brushes.Gray,
                            Margin = new Thickness(0, 0, 0, 20)
                        }
                    );
                }
                else
                {
                    if (PnlTask.Children.Last() is Control last) last.Margin = new Thickness(0, 0, 0, 20);
                }
            }
            else
            {
                // standard level header
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
            }

            RenderRichText(PnlTask, level.Description);

            try
            {
                if (!string.IsNullOrEmpty(level.DiagramPath))
                {
                    ImgDiagram.Source = LoadDiagramImage(level.DiagramPath);
                }
                else
                    ImgDiagram.Source = null;
            }
            catch
            {
                ImgDiagram.Source = null;
            }

            GenerateMaterials(level, _isCustomLevelMode ? _currentCustomSvgs : null);
            TxtConsole.Foreground = Brushes.LightGray;
            if (!_isCustomLevelMode)
            {
                TxtConsole.Text = $"> System initialisiert.\n> Level {level.Id} (Code: {level.SkipCode}) geladen.";
            }
            else
            {
                TxtConsole.Text = "> System initialisiert.";
            }

            Dispatcher.UIThread.Post(() => CodeEditor.Focus());
        }

        private void RenderRichText(StackPanel panel, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string safeText = text.Replace("|[", "\x01").Replace("|]", "\x02");
            var parts = Regex.Split(safeText, @"(\{\|[\s\S]*?\|\}|\[.*?\]|\*\*.*?\*\*)");

            SelectableTextBlock CreateTextBlock() => new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                LineHeight = 24,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var currentTb = CreateTextBlock();

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                // code block
                if (part.StartsWith("{|") && part.EndsWith("|}") && part.Length >= 4)
                {
                    if (currentTb.Inlines.Count > 0)
                    {
                        var lastRun = currentTb.Inlines.LastOrDefault() as Run;
                        if (lastRun != null && !string.IsNullOrEmpty(lastRun.Text))
                        {
                            lastRun.Text = lastRun.Text.TrimEnd();
                        }

                        bool hasContent = currentTb.Inlines.OfType<Run>().Any(r => !string.IsNullOrEmpty(r.Text));
                        if (hasContent)
                        {
                            currentTb.Margin = new Thickness(0, 0, 0, 2);
                            panel.Children.Add(currentTb);
                        }

                        currentTb = CreateTextBlock();
                    }

                    string codeContent = part.Substring(2, part.Length - 4).Trim();
                    codeContent = codeContent.Replace("\x01", "[").Replace("\x02", "]");

                    var codeBlockEditor = new TextEditor
                    {
                        Document = new TextDocument(codeContent),
                        SyntaxHighlighting = CsharpCodeEditor.GetDarkCsharpHighlighting(),
                        FontFamily = new FontFamily(MonospaceFontFamily),
                        FontSize = 14,
                        IsReadOnly = true,
                        ShowLineNumbers = false,
                        Background = Brushes.Transparent,
                        Foreground = Brushes.White,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Padding = new Thickness(10, 6, 10, 6),
                        MinHeight = 0
                    };

                    codeBlockEditor.Options.ShowSpaces = false;
                    codeBlockEditor.Options.ShowTabs = false;
                    codeBlockEditor.Options.HighlightCurrentLine = false;

                    var border = new Border
                    {
                        Background = SolidColorBrush.Parse("#1A1A1A"),
                        CornerRadius = new CornerRadius(6),
                        ClipToBounds = true,
                        Margin = new Thickness(0, 0, 0, 10),
                        Child = codeBlockEditor
                    };

                    panel.Children.Add(border);
                }
                // highlight
                else if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string content = part.Substring(1, part.Length - 2);
                    content = content.Replace("\x01", "[").Replace("\x02", "]");

                    currentTb.Inlines.Add(new Run
                    {
                        Text = content, 
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextHighlight,
                        FontFamily = new FontFamily(MonospaceFontFamily)
                    });
                }
                // bold text
                else if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
                {
                    string content = part.Substring(2, part.Length - 4);
                    content = content.Replace("\x01", "[").Replace("\x02", "]");

                    currentTb.Inlines.Add(new Run
                    {
                        Text = content,
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextNormal
                    });
                }
                // normal text
                else
                {
                    string content = part.Replace("\x01", "[").Replace("\x02", "]");
                    if (!string.IsNullOrEmpty(content))
                    {
                        currentTb.Inlines.Add(new Run
                        {
                            Text = content,
                            Foreground = BrushTextNormal
                        });
                    }
                }
            }

            if (currentTb.Inlines.Count > 0)
            {
                panel.Children.Add(currentTb);
            }
        }

        private async void UpdateDiagnostics()
        {
            if (!AppSettings.IsErrorHighlightingEnabled || currentLevel == null)
            {
                ClearDiagnostics();
                return;
            }

            string code = CodeEditor.Text;
            string auxId = currentLevel.AuxiliaryId;
            bool isValidationMode = _isDesignerMode && _activeDesignerSource == DesignerSource.Validation;

            var diagnostics = await System.Threading.Tasks.Task.Run(() =>
            {
                string header;
                if (isValidationMode)
                {
                    header = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Reflection;\n\npublic class DesignerValidator {\n";
                }
                else
                {
                    header = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n";
                }

                string fullCode = header + code + (isValidationMode ? "\n}" : "");

                var userTree = CSharpSyntaxTree.ParseText(fullCode);
                var trees = new List<SyntaxTree> { userTree };

                if (!string.IsNullOrEmpty(auxId))
                {
                    string auxCode = AuxiliaryImplementations.GetCode(auxId);
                    if (!string.IsNullOrEmpty(auxCode))
                    {
                        trees.Add(CSharpSyntaxTree.ParseText(auxCode));
                    }
                }

                var references = GetSafeReferences();

                var compilation = CSharpCompilation.Create(
                    "Analysis",
                    trees,
                    references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

                var diags = compilation.GetDiagnostics();
                return (diags, header.Length);
            });

            if (!AppSettings.IsErrorHighlightingEnabled)
            {
                ClearDiagnostics();
                return;
            }

            _textMarkerService.Clear();
            _unusedCodeTransformer.UnusedSegments.Clear();

            var unusedWarningIds = new HashSet<string>
            {
                "CS0168", "CS0219", "CS8019", "CS0169", "CS0414", "CS0649"
            };

            foreach (var diag in diagnostics.diags)
            {
                int start = diag.Location.SourceSpan.Start - diagnostics.Item2; // remove dynamic header
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
                    if (unusedWarningIds.Contains(diag.Id))
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

            public IndentationGuideRenderer(TextEditor editor)
            {
                _editor = editor;
                _guidePen = new Pen(new SolidColorBrush(Color.Parse("#3E3E42")), 1);
            }

            public KnownLayer Layer => KnownLayer.Background;

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (_editor.Document == null) return;

                double spaceWidth = textView.WideSpaceWidth;
                if (double.IsNaN(spaceWidth) || spaceWidth == 0) spaceWidth = _editor.FontSize / 2;

                double indentWidth = _editor.Options.IndentationSize * spaceWidth;

                int lastIndent = 0;

                foreach (var line in textView.VisualLines)
                {
                    string text = _editor.Document.GetText(line.FirstDocumentLine.Offset, line.FirstDocumentLine.Length);

                    int indentLevel = 0;

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        indentLevel = lastIndent;
                    }
                    else
                    {
                        int spaces = 0;
                        foreach (char c in text)
                        {
                            if (c == ' ') spaces++;
                            else if (c == '\t') spaces += _editor.Options.IndentationSize;
                            else break;
                        }
                        indentLevel = spaces / _editor.Options.IndentationSize;
                        lastIndent = indentLevel;
                    }

                    for (int i = 0; i < indentLevel; i++) // draw line
                    {
                        double x = (i * indentWidth) + (spaceWidth / 2) - textView.ScrollOffset.X;

                        if (x < 0) continue;

                        var top = new Point(x, line.VisualTop - textView.ScrollOffset.Y);
                        var bottom = new Point(x, line.VisualTop + line.Height - textView.ScrollOffset.Y);

                        drawingContext.DrawLine(_guidePen, top, bottom);
                    }
                }
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

        private void Diagram_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (ImgScale == null || ImgTranslate == null)
                return;

            double zoomSpeed = 0.1;

            ImgDiagram.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            if (e.Delta.Y > 0)
                _currentScale += zoomSpeed;
            else
                _currentScale -= zoomSpeed;

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
                _currentScale = 0.5;
                ImgScale.ScaleX = _currentScale;
                ImgScale.ScaleY = _currentScale;
                ImgTranslate.X = 0;
                ImgTranslate.Y = 0;

                ImgDiagram.HorizontalAlignment = HorizontalAlignment.Center;
                ImgDiagram.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private void GenerateMaterials(Level level, List<string> draftAdditionalSvgs = null)
        {
            PnlMaterials.Children.Clear();

            // load image
            if (!string.IsNullOrEmpty(level.AuxiliaryId))
            {
                string auxPath = $"img/aux_{level.AuxiliaryId}.svg";

                var auxImage = LoadDiagramImage(auxPath);

                if (auxImage != null)
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
                            Source = auxImage,
                            Height = 150,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 0, 0, 15),
                            HorizontalAlignment = HorizontalAlignment.Left
                        }
                    );
                }
            }

            List<string> svgsToRender = draftAdditionalSvgs ?? new List<string>();

            if (svgsToRender.Count > 0)
            {
                PnlMaterials.Children.Add(
                    new SelectableTextBlock
                    {
                        Text = "Referenz-Materialien:",
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextTitle,
                        Margin = new Thickness(0, 0, 0, 5)
                    }
                );

                for (int i = 0; i < svgsToRender.Count; i++)
                {
                    var svgContent = svgsToRender[i];
                    if (string.IsNullOrEmpty(svgContent)) continue;

                    var img = LoadSvgFromString(svgContent);
                    if (img != null)
                    {
                        var border = new Border
                        {
                            Background = SolidColorBrush.Parse("#1A1A1A"),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10),
                            Margin = new Thickness(0, 0, 0, 15),
                            Child = new Image
                            {
                                Source = img,
                                Height = 180,
                                Stretch = Stretch.Uniform,
                                HorizontalAlignment = HorizontalAlignment.Left
                            }
                        };
                        PnlMaterials.Children.Add(border);
                    }
                }
            }

            // hints and text
            if (!string.IsNullOrEmpty(level.MaterialDocs))
            {
                RenderMaterialText(PnlMaterials, level.MaterialDocs);
            }

            // prerequisites
            RenderPrerequisites(PnlMaterials, level.Prerequisites, level.OptionalPrerequisites);
        }

        private void RenderMaterialText(StackPanel targetPanel, string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return;

            var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var normalTextBuffer = new System.Text.StringBuilder();
            var hintBuffer = new System.Text.StringBuilder();
            bool inHintBlock = false;
            string currentHintTitle = "";

            void FlushNormalBuffer()
            {
                if (normalTextBuffer.Length > 0)
                {
                    RenderRichText(targetPanel, normalTextBuffer.ToString().TrimEnd());
                    normalTextBuffer.Clear();
                }
            }

            foreach (var line in lines)
            {
                string trim = line.Trim();

                // start of hint
                if (trim.StartsWith("start-hint:") || trim.StartsWith("start-tipp:"))
                {
                    FlushNormalBuffer();
                    inHintBlock = true;

                    // parse hint title
                    int colonIndex = trim.IndexOf(':');
                    if (colonIndex != -1 && colonIndex < trim.Length - 1)
                    {
                        if (trim.StartsWith("start-hint:"))
                            currentHintTitle = "Hinweis: " + trim.Substring(colonIndex + 1).Trim();
                        else
                            currentHintTitle = "Tipp: " + trim.Substring(colonIndex + 1).Trim();
                    }
                    else
                    {
                        currentHintTitle = trim.StartsWith("start-hint:") ? "Hinweis" : "Tipp";
                    }

                    continue;
                }

                // end of hint
                if (trim.StartsWith(":end-hint") || trim.StartsWith(":end-tipp"))
                {
                    if (inHintBlock)
                    {
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

                        var innerStack = (StackPanel)contentPanel.Child;
                        RenderRichText(innerStack, hintBuffer.ToString().Trim());

                        if (innerStack.Children.Count > 0 && innerStack.Children.Last() is Control lastChild)
                        {
                            lastChild.Margin = new Thickness(0);
                        }

                        string capturedTitle = currentHintTitle;

                        var btn = new Button
                        {
                            Content = "▶ " + capturedTitle,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Background = SolidColorBrush.Parse("#3C3C41"),
                            Foreground = Brushes.White,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Cursor = Cursor.Parse("Hand")
                        };

                        btn.Click += (s, e) =>
                        {
                            bool isExpanded = contentPanel.IsVisible;
                            contentPanel.IsVisible = !isExpanded;
                            btn.Content = (isExpanded ? "▶ " : "▼ ") + capturedTitle;
                        };

                        stack.Children.Add(btn);
                        stack.Children.Add(contentPanel);
                        targetPanel.Children.Add(stack);

                        hintBuffer.Clear();
                        inHintBlock = false;
                    }
                    continue;
                }

                if (inHintBlock)
                {
                    if (hintBuffer.Length > 0) hintBuffer.AppendLine();
                    hintBuffer.Append(line);
                }
                else
                {
                    if (normalTextBuffer.Length > 0) normalTextBuffer.AppendLine();
                    normalTextBuffer.Append(line);
                }
            }

            FlushNormalBuffer();
        }

        private void RenderPrerequisites(StackPanel targetPanel, List<string> required, List<string> optional)
        {
            bool hasRequired = required != null && required.Count > 0;
            bool hasOptional = optional != null && optional.Count > 0;

            if (!hasRequired && !hasOptional) return;

            var prereqStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };

            var contentPanel = new Border
            {
                IsVisible = false,
                Background = SolidColorBrush.Parse("#252526"),
                Margin = new Thickness(0, 5, 0, 0),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = new StackPanel()
            };

            var innerList = (StackPanel)contentPanel.Child;

            void RenderPrereqRows(List<string> topics, bool isOptional)
            {
                if (topics == null) return;

                foreach (var reqTitle in topics)
                {
                    var lesson = PrerequisiteSystem.GetLesson(reqTitle);
                    if (lesson == null) continue;

                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto"),
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    // title
                    var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

                    var txtTitle = new TextBlock
                    {
                        Text = "• " + lesson.Title,
                        Foreground = Brushes.LightGray,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    titleStack.Children.Add(txtTitle);

                    if (isOptional)
                    {
                        var badge = new Border
                        {
                            Background = SolidColorBrush.Parse("#333333"),
                            BorderBrush = Brushes.Gray,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1),
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = "Optional",
                                FontSize = 10,
                                Foreground = Brushes.Gray
                            }
                        };
                        titleStack.Children.Add(badge);
                    }

                    // dometrain
                    var btnDt = new Button
                    {
                        Content = "Kurs",
                        FontSize = 11,
                        Padding = new Thickness(8, 4),
                        Margin = new Thickness(0, 0, 5, 0),
                        Background = SolidColorBrush.Parse("#5D3FD3"),
                        Foreground = Brushes.White,
                        CornerRadius = new CornerRadius(4),
                        Cursor = Cursor.Parse("Hand")
                    };
                    ToolTip.SetTip(btnDt, "Zu Dometrain.com");
                    btnDt.Click += (s, e) => PrerequisiteSystem.OpenUrl(lesson.DometrainUrl);

                    // docs
                    var btnDoc = new Button
                    {
                        Content = "Docs",
                        FontSize = 11,
                        Padding = new Thickness(8, 4),
                        Background = SolidColorBrush.Parse("#0078D4"),
                        Foreground = Brushes.White,
                        CornerRadius = new CornerRadius(4),
                        Cursor = Cursor.Parse("Hand")
                    };
                    ToolTip.SetTip(btnDoc, "Zu Microsoft Learn");
                    btnDoc.Click += (s, e) => PrerequisiteSystem.OpenUrl(lesson.DocsUrl);

                    Grid.SetColumn(titleStack, 0);
                    Grid.SetColumn(btnDt, 1);
                    Grid.SetColumn(btnDoc, 2);

                    row.Children.Add(titleStack);
                    row.Children.Add(btnDt);
                    row.Children.Add(btnDoc);

                    innerList.Children.Add(row);
                }
            }

            RenderPrereqRows(required, false);

            if (hasRequired && hasOptional)
            {
                innerList.Children.Add(new Separator { Background = SolidColorBrush.Parse("#333"), Margin = new Thickness(0, 5, 0, 5) });
            }

            RenderPrereqRows(optional, true);

            if (innerList.Children.Count > 0)
            {
                var btnToggle = new Button
                {
                    Content = "▶ Voraussetzungen / Grundlagen",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = SolidColorBrush.Parse("#2b2b2b"),
                    Foreground = Brushes.White,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Cursor = Cursor.Parse("Hand")
                };

                btnToggle.Click += (s, e) =>
                {
                    bool isExpanded = contentPanel.IsVisible;
                    contentPanel.IsVisible = !isExpanded;
                    btnToggle.Content = (isExpanded ? "▶ " : "▼ ") + "Voraussetzungen / Grundlagen";
                    if (!isExpanded) contentPanel.CornerRadius = new CornerRadius(6);
                };

                prereqStack.Children.Add(btnToggle);
                prereqStack.Children.Add(contentPanel);
                targetPanel.Children.Add(prereqStack);
            }
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_compilationCts != null)
            {
                _compilationCts.Cancel();
                TxtConsole.Text += "\n> Vorgang wird abgebrochen...";
                BtnRun.IsEnabled = false;
                return;
            }

            _compilationCts = new System.Threading.CancellationTokenSource();
            BtnRun.Content = "■ ABBRECHEN";
            BtnRun.Width = 135;
            BtnRun.Background = SolidColorBrush.Parse("#B43232");
            ToolTip.SetTip(BtnRun, "Ausführung stoppen");

            TxtConsole.Foreground = Brushes.LightGray;
            TxtConsole.Text = "";

            if (!_hasRunOnce)
            {
                TxtConsole.Text += "> Compiler wird gestartet...\n";
                _hasRunOnce = true;
            }

            TxtConsole.Text += "> Kompiliere...\n";
            SaveCurrentProgress();

            string codeText = CodeEditor.Text;
            var levelContext = currentLevel;
            var token = _compilationCts.Token;

            // capture designer state needed for the thread
            bool runDesignerTest = _isDesignerMode;
            bool useCustomValidation = runDesignerTest || _isCustomLevelMode;
            string validationLogic = runDesignerTest ? TxtDesignValidation.Text : _currentCustomValidationCode;

            if (_isDesignerMode) UpdateDraftFromUI();

            LevelDraft pendingSnapshot = null;
            if (_isDesignerMode)
            {
                // deep copy to ensure we capture the exact state at runtime
                string json = JsonSerializer.Serialize(_currentDraft);
                pendingSnapshot = JsonSerializer.Deserialize<LevelDraft>(json);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var processingTask = System.Threading.Tasks.Task.Run<(bool Success, System.Collections.Immutable.ImmutableArray<Diagnostic>? Diagnostics, dynamic TestResult)>(() =>
                {
                    if (token.IsCancellationRequested) return (false, null, null);

                    string fullCode = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" + codeText;

                    var syntaxTree = CSharpSyntaxTree.ParseText(fullCode, cancellationToken: token);
                    var trees = new List<SyntaxTree> { syntaxTree };

                    // handle auxiliary code
                    if (!runDesignerTest && !_isCustomLevelMode && !string.IsNullOrEmpty(levelContext.AuxiliaryId))
                    {
                        string auxCode = AuxiliaryImplementations.GetCode(levelContext.AuxiliaryId);
                        if (!string.IsNullOrEmpty(auxCode))
                        {
                            trees.Add(CSharpSyntaxTree.ParseText(auxCode, cancellationToken: token));
                        }
                    }

                    var references = GetSafeReferences();

                    var compilation = CSharpCompilation.Create(
                        $"Level_{(runDesignerTest ? "Designer" : levelContext.Id.ToString())}_{Guid.NewGuid()}",
                        trees,
                        references,
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    );

                    using (var ms = new MemoryStream())
                    {
                        try
                        {
                            EmitResult emitResult = compilation.Emit(ms, cancellationToken: token);

                            if (!emitResult.Success)
                            {
                                return (Success: false, Diagnostics: (System.Collections.Immutable.ImmutableArray<Diagnostic>?)emitResult.Diagnostics, TestResult: (dynamic)null);
                            }

                            if (token.IsCancellationRequested) return (false, null, null);

                            ms.Seek(0, SeekOrigin.Begin);
                            var assembly = Assembly.Load(ms.ToArray());

                            if (runDesignerTest || useCustomValidation)
                            {
                                // --- DESIGNER CUSTOM COMPILER LOGIC ---
                                try
                                {
                                    string validatorSource = @"
                                        using System;
                                        using System.Reflection;
                                        using System.Collections.Generic;
                                        using System.Linq;

                                        public static class DesignerValidator 
                                        {
                                            " + validationLogic + @"
                                        }";

                                    var validatorTree = CSharpSyntaxTree.ParseText(validatorSource);
                                    var valCompilation = CSharpCompilation.Create(
                                        "Validator_" + Guid.NewGuid(),
                                        new[] { validatorTree },
                                        references,
                                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                    );

                                    using (var valMs = new MemoryStream())
                                    {
                                        var valEmit = valCompilation.Emit(valMs);
                                        if (!valEmit.Success)
                                        {
                                            string errorMsg = valEmit.Diagnostics.FirstOrDefault()?.GetMessage() ?? "Unbekannter Fehler";
                                            throw new Exception("Fehler im Validierungs-Code (Designer): " + errorMsg);
                                        }

                                        valMs.Seek(0, SeekOrigin.Begin);
                                        var valAssembly = Assembly.Load(valMs.ToArray());
                                        var valType = valAssembly.GetType("DesignerValidator");

                                        // dynamically find the method
                                        var valMethod = valType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                                               .FirstOrDefault(m => m.ReturnType == typeof(bool)
                                                                                && m.GetParameters().Length == 2
                                                                                && m.GetParameters()[0].ParameterType == typeof(Assembly)
                                                                                && m.GetParameters()[1].IsOut);

                                        if (valMethod == null) throw new Exception("Keine gültige Validierungsmethode gefunden. Signatur muss sein: bool Methode(Assembly a, out string f)");

                                        object[] args = new object[] { assembly, null };

                                        try
                                        {
                                            bool passed = (bool)valMethod.Invoke(null, args);
                                            string feedback = (string)args[1];

                                            return (Success: true, Diagnostics: (System.Collections.Immutable.ImmutableArray<Diagnostic>?)null,
                                                    TestResult: new TestResult { Success = passed, Feedback = feedback });
                                        }
                                        catch (TargetInvocationException tie)
                                        {
                                            throw tie.InnerException ?? tie;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    return (Success: true, Diagnostics: (System.Collections.Immutable.ImmutableArray<Diagnostic>?)null,
                                               TestResult: new TestResult { Success = false, Error = ex });
                                }
                            }
                            else
                            {
                                // normal level logic
                                var testResult = LevelTester.Run(levelContext.Id, assembly);
                                return (Success: true, Diagnostics: (System.Collections.Immutable.ImmutableArray<Diagnostic>?)null, TestResult: (dynamic)testResult);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return (false, null, null);
                        }
                    }
                }, token);

                var timeoutTask = System.Threading.Tasks.Task.Delay(60000, token);
                var completedTask = await System.Threading.Tasks.Task.WhenAny(processingTask, timeoutTask);

                if (token.IsCancellationRequested)
                {
                    TxtConsole.Foreground = Brushes.Orange;
                    TxtConsole.Text += "\n⚠ Abbruch durch Benutzer.";
                }
                else if (completedTask == timeoutTask)
                {
                    stopwatch.Stop();
                    TxtConsole.Foreground = Brushes.Red;
                    TxtConsole.Text += "\n❌ TIMEOUT: Das Programm hat das Zeitlimit von 1 Minute überschritten.";
                }
                else
                {
                    var result = await processingTask;
                    stopwatch.Stop();

                    if (!result.Success && result.Diagnostics != null)
                    {
                        TxtConsole.Foreground = Brushes.Red;
                        TxtConsole.Text += "KOMPILIERFEHLER:\n";
                        foreach (var diag in result.Diagnostics.Value.Where(d => d.Severity == DiagnosticSeverity.Error))
                        {
                            var lineSpan = diag.Location.GetLineSpan();
                            int userLine = lineSpan.StartLinePosition.Line - 3;
                            if (userLine < 0) userLine = 0;
                            TxtConsole.Text += $"Zeile {userLine}: {diag.GetMessage()}\n";
                        }
                    }
                    else if (result.Success)
                    {
                        if (runDesignerTest)
                        {
                            // handle designer result
                            if (result.TestResult.Success)
                            {
                                TxtConsole.Foreground = Brushes.LightGreen;
                                TxtConsole.Text += $"✓ DESIGNER TEST BESTANDEN: " + result.TestResult.Feedback;

                                BtnDesignerExport.IsEnabled = true;
                                _verifiedDraftState = pendingSnapshot;
                                TxtDesignerStatus.Text = "Bereit zum Export";
                            }
                            else
                            {
                                TxtConsole.Foreground = Brushes.Orange;

                                string msg;
                                if (result.TestResult.Error != null)
                                {
                                    Exception err = result.TestResult.Error;
                                    msg = $"{err.GetType().Name}: {err.Message}\n\nStack Trace:\n{err.StackTrace}";
                                }
                                else
                                {
                                    msg = "Validierung fehlgeschlagen (false zurückgegeben).";
                                }

                                TxtConsole.Text += $"❌ VALIDIERUNG FEHLGESCHLAGEN:\n{msg}";
                                BtnDesignerExport.IsEnabled = false;
                                _verifiedDraftState = null;
                            }
                        }
                        else
                        {
                            ProcessTestResult(levelContext, result.TestResult, stopwatch.Elapsed);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TxtConsole.Foreground = Brushes.Orange;
                TxtConsole.Text += "\n⚠ Abbruch durch Benutzer.";
            }
            catch (Exception ex)
            {
                TxtConsole.Text += $"\nSystem Fehler: {ex.Message}";
            }
            finally
            {
                _compilationCts?.Dispose();
                _compilationCts = null;
                BtnRun.Content = "▶ AUSFÜHREN";
                BtnRun.Width = 135;
                BtnRun.Background = SolidColorBrush.Parse("#32A852");
                BtnRun.IsEnabled = true;
                ToolTip.SetTip(BtnRun, "Ausführen (F5)");
                CodeEditor.Focus();
            }
        }

        private void ProcessTestResult(Level levelContext, dynamic result, TimeSpan duration)
        {
            TxtConsole.Text = "";

            if (result.Success)
            {
                TxtConsole.Foreground = Brushes.LightGreen;
                TxtConsole.Text += $"✓ TEST BESTANDEN ({duration.TotalSeconds:F2}s): " + result.Feedback + "\n\n";

                if (_isCustomLevelMode)
                {
                    if (!customPlayerData.CompletedCustomLevels.Contains(levelContext.Title))
                    {
                        customPlayerData.CompletedCustomLevels.Add(levelContext.Title);
                        SaveSystem.SaveCustom(customPlayerData);
                    }

                    TxtConsole.Text += "🎉 Custom Level erfolgreich abgeschlossen!";
                    return;
                }

                if (!playerData.CompletedLevelIds.Contains(levelContext.Id))
                    playerData.CompletedLevelIds.Add(levelContext.Id);

                var nextLvl = levels.FirstOrDefault(l => l.SkipCode == levelContext.NextLevelCode);

                if (nextLvl != null)
                {
                    // unlock the next level
                    if (!playerData.UnlockedLevelIds.Contains(nextLvl.Id))
                    {
                        playerData.UnlockedLevelIds.Add(nextLvl.Id);
                        TxtConsole.Text += $"🔓 Level {nextLvl.Id} freigeschaltet!\n";
                    }

                    TxtConsole.Text += $"Nächstes Level Code: {nextLvl.SkipCode}\n";

                    // check if we are switching sections
                    if (nextLvl.Section != levelContext.Section)
                    {
                        TxtConsole.Text += "\n🎉 Sektion abgeschlossen! Bereit für das nächste Thema?";
                        BtnNextLevel.Content = "NÄCHSTE SEKTION →";
                    }
                    else
                    {
                        BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                    }

                    BtnNextLevel.IsVisible = true;
                }
                else
                {
                    // no next level -> course completed
                    TxtConsole.Text += "\n🎉 Herzlichen Glückwunsch! Du hast alle Levels gemeistert.";
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

                if (nextLvl != null)
                {
                    // unlock the next level
                    if (!playerData.UnlockedLevelIds.Contains(nextLvl.Id))
                    {
                        playerData.UnlockedLevelIds.Add(nextLvl.Id);
                        TxtConsole.Text += $"🔓 Level {nextLvl.Id} freigeschaltet!\n";
                    }

                    TxtConsole.Text += $"Nächstes Level Code: {nextLvl.SkipCode}\n";

                    // check if we are switching sections
                    if (nextLvl.Section != currentLevel.Section)
                    {
                        TxtConsole.Text += "\n🎉 Sektion abgeschlossen! Bereit für das nächste Thema?";
                        BtnNextLevel.Content = "NÄCHSTE SEKTION →";
                    }
                    else
                    {
                        BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                    }

                    BtnNextLevel.IsVisible = true;
                }
                else
                {
                    // no next level -> course completed
                    TxtConsole.Text += "\n🎉 Herzlichen Glückwunsch! Du hast alle Levels gemeistert.";
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
                Width = 500,
                Height = 380,
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
                        "Du hast alle Levels erfolgreich abgeschlossen!\n\nDu bist nun bereit für den Programmier-Teil der Abiturprüfung in Praktischer Informatik.\nViel Erfolg!",
                    FontSize = 16,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 24
                }
            );
            contentStack.Children.Add(
                new TextBlock
                {
                    Text = "Rechtlicher Hinweis: Diese Software dient ausschließlich Übungszwecken. Der Entwickler übernimmt keine Gewähr für die Vollständigkeit der Inhalte oder den tatsächlichen Erfolg in der Abiturprüfung.",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 20, 10, 0),
                    FontStyle = FontStyle.Italic
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

            // header components
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            // left side
            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txtTitle = new TextBlock
            {
                Text = "Level Auswählen",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(txtTitle);

            // level count badge
            var countBadge = new Border
            {
                Background = SolidColorBrush.Parse("#2D2D30"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 5),
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
                Child = new TextBlock
                {
                    Name = "BadgeText",
                    Text = "",
                    Foreground = BrushTextTitle,
                    FontWeight = FontWeight.Bold,
                    FontSize = 14
                }
            };
            titleStack.Children.Add(countBadge);

            headerGrid.Children.Add(titleStack);

            // middle
            var searchContainer = new Border
            {
                Margin = new Thickness(15, 0)
            };
            Grid.SetColumn(searchContainer, 1);
            headerGrid.Children.Add(searchContainer);

            // right
            var headerRightPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            Grid.SetColumn(headerRightPanel, 2);
            headerGrid.Children.Add(headerRightPanel);

            // body and footer
            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var footerPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto")
            };

            mainGrid.Children.Add(headerGrid);
            Grid.SetRow(contentScroll, 1);
            mainGrid.Children.Add(contentScroll);
            Grid.SetRow(footerPanel, 2);
            mainGrid.Children.Add(footerPanel);

            root.Child = mainGrid;
            win.Content = root;

            bool isCustomMode = false;

            // ui refresh logic
            void RefreshUI()
            {
                searchContainer.Child = null;
                headerRightPanel.Children.Clear();
                contentScroll.Content = null;
                footerPanel.Children.Clear();

                // footer
                var btnClose = new Button
                {
                    Content = "Schließen",
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Padding = new Thickness(15, 10),
                    CornerRadius = new CornerRadius(4),
                    Background = SolidColorBrush.Parse("#3C3C3C"),
                    Foreground = Brushes.White,
                    Margin = new Thickness(10, 15, 0, 0)
                };
                btnClose.Click += (_, __) => win.Close();

                var btnToggleMode = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            LoadIcon(isCustomMode ? "assets/icons/ic_folder.svg" : "assets/icons/ic_folder_custom.svg", 18),
                            new TextBlock
                            {
                                Text = isCustomMode ? "Standard Levels" : "Eigene Levels",
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    },
                    Padding = new Thickness(15, 10),
                    CornerRadius = new CornerRadius(4),
                    Background = SolidColorBrush.Parse("#2D2D30"),
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 15, 10, 0)
                };

                btnToggleMode.Click += (_, __) =>
                {
                    isCustomMode = !isCustomMode;
                    RefreshUI();
                };

                Grid.SetColumn(btnToggleMode, 0);
                Grid.SetColumn(btnClose, 1);
                footerPanel.Children.Add(btnToggleMode);
                footerPanel.Children.Add(btnClose);

                if (!isCustomMode)
                {
                    // standard level view
                    txtTitle.Text = "Level Auswählen";

                    // show badge
                    countBadge.IsVisible = true;
                    ((TextBlock)countBadge.Child).Text = $"{playerData.CompletedLevelIds.Count}/{levels.Count}";

                    // show code input
                    var codePanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10
                    };
                    codePanel.Children.Add(new TextBlock 
                    {
                        Text = "Code:",
                        Foreground = Brushes.Gray,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var txtLevelCode = new TextBox
                    {
                        Width = 60,
                        MaxLength = 3,
                        Background = SolidColorBrush.Parse("#141414"),
                        Foreground = Brushes.White,
                        BorderBrush = SolidColorBrush.Parse("#333"),
                        CornerRadius = new CornerRadius(4),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        FontFamily = MonospaceFontFamily
                    };
                    txtLevelCode.TextChanged += (s, ev) =>
                    {
                        if (txtLevelCode.Text?.Length == 3)
                        {
                            string code = txtLevelCode.Text.ToUpper();
                            var lvl = levels.FirstOrDefault(l => l.SkipCode == code);
                            if (lvl != null) { LoadLevel(lvl); win.Close(); }
                        }
                    };
                    codePanel.Children.Add(txtLevelCode);
                    headerRightPanel.Children.Add(codePanel);

                    // standard level list
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
                            Foreground = BrushTextTitle,
                            FontWeight = FontWeight.Bold,
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        if (isSectionComplete) headerPanel.Children.Add(LoadIcon("assets/icons/ic_done.svg", 16));

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
                            string iconPath = completed ? "assets/icons/ic_check.svg" : (unlocked ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_lock.svg");
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

                        levelStack.Children.Add(new Expander
                        {
                            Header = headerPanel,
                            Content = sectionContent,
                            IsExpanded = !isSectionComplete,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 0, 5)
                        });
                    }
                    contentScroll.Content = levelStack;
                }
                else
                {
                    // custom levels view
                    txtTitle.Text = "Eigene Levels";
                    countBadge.IsVisible = false;

                    // custom levels list
                    var customStack = new StackPanel
                    {
                        Spacing = 5
                    };
                    var customLevels = GetCustomLevels();

                    // search bar
                    var txtSearch = new TextBox
                    {
                        Watermark = "Suchen...",
                        MinWidth = 150,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Background = SolidColorBrush.Parse("#141414"),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = SolidColorBrush.Parse("#333"),
                        CornerRadius = new CornerRadius(4)
                    };
                    txtSearch.TextChanged += (s, e) => {
                        string query = txtSearch.Text?.ToLower() ?? "";
                        foreach (var child in customStack.Children)
                        {
                            if (child is Grid row && row.Tag is CustomLevelInfo info)
                            {
                                bool match = info.Name.ToLower().Contains(query) || info.Author.ToLower().Contains(query);
                                row.IsVisible = match;
                            }
                        }
                    };

                    searchContainer.Child = txtSearch;

                    // header buttons
                    var btnOpenFolder = new Button
                    {
                        Content = LoadIcon("assets/icons/ic_folder_open.svg", 18),
                        Background = SolidColorBrush.Parse("#3C3C3C"),
                        Padding = new Thickness(8),
                        CornerRadius = new CornerRadius(4)
                    };
                    ToolTip.SetTip(btnOpenFolder, "Levels Ordner öffnen");
                    btnOpenFolder.Click += (_, __) => OpenLevelsFolder();
                    headerRightPanel.Children.Add(btnOpenFolder);

                    var btnAdd = new Button
                    {
                        Content = LoadIcon("assets/icons/ic_add.svg", 18),
                        Background = SolidColorBrush.Parse("#32A852"),
                        Padding = new Thickness(8),
                        CornerRadius = new CornerRadius(4)
                    };
                    ToolTip.SetTip(btnAdd, "Neues Level erstellen");
                    btnAdd.Click += async (_, __) =>
                    {
                        await ShowAddLevelDialog(win);
                        RefreshUI();
                    };
                    headerRightPanel.Children.Add(btnAdd);

                    if (customLevels.Count == 0)
                    {
                        customStack.Children.Add(new TextBlock
                        {
                            Text = "Keine eigenen Levels gefunden.\nErstelle eins mit '+' oder \nöffne den Ordner und füge Levels hinzu.",
                            Foreground = Brushes.Gray,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 50, 0, 0)
                        });
                    }
                    else
                    {
                        foreach (var cl in customLevels)
                        {
                            var rowGrid = new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                                Tag = cl
                            };

                            string iconPath;
                            if (cl.IsDraft) iconPath = "assets/icons/ic_lock.svg";
                            else if (customPlayerData.CompletedCustomLevels.Contains(cl.Name)) iconPath = "assets/icons/ic_check.svg";
                            else iconPath = "assets/icons/ic_lock_open.svg";

                            var iconImage = LoadIcon(iconPath, 16);
                            iconImage.Margin = new Thickness(0, 0, 10, 0);
                            iconImage.VerticalAlignment = VerticalAlignment.Center;

                            var btnContentGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *") };
                            btnContentGrid.Children.Add(iconImage); 

                            var textStack = new StackPanel { Spacing = 2 };
                            Grid.SetColumn(textStack, 1);

                            textStack.Children.Add(new TextBlock
                            {
                                Text = cl.Name + (cl.IsDraft ? " (Entwurf)" : ""),
                                Foreground = cl.IsDraft ? Brushes.Orange : Brushes.White
                            });
                            textStack.Children.Add(new TextBlock
                            {
                                Text = "von " + cl.Author,
                                FontSize = 11,
                                Foreground = Brushes.Gray
                            });
                            btnContentGrid.Children.Add(textStack);

                            var btnMain = new Button
                            {
                                Content = btnContentGrid,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                Background = SolidColorBrush.Parse("#313133"),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(10),
                                Cursor = cl.IsDraft ? Cursor.Default : Cursor.Parse("Hand")
                            };

                            btnMain.Click += (_, __) =>
                            {
                                if (!cl.IsDraft)
                                {
                                    try
                                    {
                                        string json = File.ReadAllText(cl.FilePath);
                                        using (var doc = JsonDocument.Parse(json))
                                        {
                                            var root = doc.RootElement;

                                            // generate negative id for custom levels
                                            int customId = cl.FilePath.GetHashCode();
                                            if (customId > 0) customId *= -1;

                                            var loadedLevel = new Level
                                            {
                                                Id = customId,
                                                Title = root.GetProperty("Name").GetString(),
                                                Description = root.GetProperty("Description").GetString(),
                                                StarterCode = root.GetProperty("StarterCode").GetString(),
                                                MaterialDocs = root.GetProperty("MaterialDocs").GetString(),
                                                SkipCode = "CUST", // placeholder (as not useable)
                                                Section = "Eigene Levels",
                                                Prerequisites = new List<string>(),
                                                AuxiliaryId = ""
                                            };

                                            // get author
                                            if (root.TryGetProperty("Author", out var authorElem))
                                            {
                                                _currentCustomAuthor = authorElem.GetString();
                                            }

                                            // get prerequisites
                                            if (root.TryGetProperty("Prerequisites", out var prereqElem))
                                            {
                                                foreach (var p in prereqElem.EnumerateArray())
                                                    loadedLevel.Prerequisites.Add(p.GetString());
                                            }

                                            // get main diagram
                                            if (root.TryGetProperty("PlantUmlSvg", out var svgElem))
                                            {
                                                string svgContent = svgElem.GetString();
                                                if (!string.IsNullOrEmpty(svgContent))
                                                {
                                                    string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}.svg");
                                                    File.WriteAllText(tempSvgPath, svgContent);
                                                    loadedLevel.DiagramPath = tempSvgPath;
                                                }
                                            }

                                            // get aux diagrams
                                            _currentCustomSvgs = new List<string>();
                                            if (root.TryGetProperty("MaterialDiagramSvgs", out var svgsElem))
                                            {
                                                foreach (var s in svgsElem.EnumerateArray())
                                                {
                                                    _currentCustomSvgs.Add(s.GetString());
                                                }
                                            }

                                            _currentCustomValidationCode = root.GetProperty("ValidationCode").GetString();
                                            _isCustomLevelMode = true;

                                            LoadLevel(loadedLevel);
                                            win.Close();
                                            TxtConsole.Text += $"\n> Custom Level geladen: {loadedLevel.Title}";
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TxtConsole.Text += $"\n> Fehler beim Laden: {ex.Message}";
                                    }
                                }
                            };

                            Grid.SetColumnSpan(btnMain, 3);
                            rowGrid.Children.Add(btnMain);

                            // action buttons
                            var actionPanel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 5,
                                Margin = new Thickness(0, 0, 10, 0)
                            };
                            Grid.SetColumn(actionPanel, 2);

                            // edit button
                            if (cl.IsDraft)
                            {
                                var btnEdit = new Button
                                {
                                    Content = LoadIcon("assets/icons/ic_edit.svg", 16),
                                    Background = Brushes.Transparent,
                                    Padding = new Thickness(8)
                                };
                                ToolTip.SetTip(btnEdit, "Level im Designer bearbeiten");
                                btnEdit.Click += (_, __) =>
                                {
                                    win.Close();
                                    ToggleDesignerMode(true, cl.FilePath);
                                };
                                actionPanel.Children.Add(btnEdit);
                            }

                            // delete button
                            var btnDelete = new Button
                            {
                                Content = LoadIcon("assets/icons/ic_delete.svg", 16),
                                Background = Brushes.Transparent,
                                Padding = new Thickness(8)
                            };
                            ToolTip.SetTip(btnDelete, "Level löschen");
                            btnDelete.Click += async (_, __) =>
                            {
                                await DeleteCustomLevel(cl, win);
                                RefreshUI();
                            };
                            actionPanel.Children.Add(btnDelete);

                            rowGrid.Children.Add(actionPanel);

                            customStack.Children.Add(rowGrid);
                        }
                    }
                    contentScroll.Content = customStack;
                }
            }

            RefreshUI();
            win.ShowDialog(this);
            CodeEditor.Focus();
        }

        private void OpenLevelsFolder()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", path);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", path);
                }
            }
            catch (Exception ex)
            {
                TxtConsole.Text += $"\n> Fehler beim Öffnen des Ordners: {ex.Message}";
            }
        }

        private List<CustomLevelInfo> GetCustomLevels()
        {
            var list = new List<CustomLevelInfo>();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");

            if (!Directory.Exists(path)) return list;

            (string name, string author) GetMetadata(string file)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        string name = root.TryGetProperty("Name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                        string author = root.TryGetProperty("Author", out var a) ? a.GetString() : "Unbekannt";
                        return (name, author);
                    }
                }
                catch
                {
                    return (Path.GetFileNameWithoutExtension(file), "Fehler");
                }
            }

            // regular custom levels
            foreach (var file in Directory.GetFiles(path, "*.elitelvl"))
            {
                var meta = GetMetadata(file);
                list.Add(new CustomLevelInfo
                {
                    Name = meta.name,
                    Author = meta.author,
                    FilePath = file,
                    IsDraft = false
                });
            }

            // custom level drafts
            foreach (var file in Directory.GetFiles(path, "*.elitelvldraft"))
            {
                var meta = GetMetadata(file);
                list.Add(new CustomLevelInfo
                {
                    Name = meta.name,
                    Author = meta.author,
                    FilePath = file,
                    IsDraft = true
                });
            }

            return list;
        }

        private async System.Threading.Tasks.Task ShowAddLevelDialog(Window owner)
        {
            var dialog = new Window
            {
                Title = "Neues Level",
                Width = 400,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto, *, Auto"),
                Margin = new Thickness(20)
            };

            grid.Children.Add(new TextBlock
            {
                Text = "Neues Custom Level",
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var inputStack = new StackPanel
            {
                Spacing = 10
            };
            Grid.SetRow(inputStack, 1);

            inputStack.Children.Add(new TextBlock
            {
                Text = "Name:",
                Foreground = Brushes.Gray
            });
            var txtName = new TextBox
            {
                Watermark = "Level Name",
                Background = SolidColorBrush.Parse("#1E1E1E"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8)
            };
            inputStack.Children.Add(txtName);

            inputStack.Children.Add(new TextBlock
            {
                Text = "Autor:",
                Foreground = Brushes.Gray
            });
            var txtAuthor = new TextBox
            {
                Watermark = "Autor Name",
                Background = SolidColorBrush.Parse("#1E1E1E"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8)
            };
            inputStack.Children.Add(txtAuthor);

            grid.Children.Add(inputStack);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(btnPanel, 2);

            var btnCancel = new Button
            {
                Content = "Abbrechen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White
            };
            var btnCreate = new Button
            {
                Content = "Erstellen",
                Background = SolidColorBrush.Parse("#32A852"),
                Foreground = Brushes.White
            };

            btnCancel.Click += (_, __) => dialog.Close();
            btnCreate.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) return;

                string safeName = string.Join("_", txtName.Text.Split(Path.GetInvalidFileNameChars()));
                string filename = $"{safeName}.elitelvldraft";
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, filename);

                var newDraft = new LevelDraft
                {
                    Name = txtName.Text,
                    Author = txtAuthor.Text
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(newDraft, options);

                File.WriteAllText(path, json);

                dialog.Close();
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnCreate);
            grid.Children.Add(btnPanel);

            dialog.Content = new Border { Child = grid };
            await dialog.ShowDialog(owner);
        }

        private async System.Threading.Tasks.Task DeleteCustomLevel(CustomLevelInfo info, Window owner)
        {
            var dialog = new Window
            {
                Title = "Löschen?",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(20)
            };
            grid.Children.Add(new TextBlock
            {
                Text = $"Möchtest du '{info.Name}' wirklich löschen?",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            Grid.SetRow(btnPanel, 1);

            var btnYes = new Button
            {
                Content = "Löschen",
                Background = SolidColorBrush.Parse("#B43232"),
                Foreground = Brushes.White
            };
            var btnNo = new Button
            {
                Content = "Abbrechen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White
            };

            btnNo.Click += (_, __) => dialog.Close();
            btnYes.Click += (_, __) =>
            {
                try
                {
                    if (File.Exists(info.FilePath))
                        File.Delete(info.FilePath);

                    // remove saved data for this level
                    if (!info.IsDraft)
                    {
                        bool changed = false;
                        if (customPlayerData.CompletedCustomLevels.Contains(info.Name))
                        {
                            customPlayerData.CompletedCustomLevels.Remove(info.Name);
                            changed = true;
                        }
                        if (customPlayerData.UserCode.ContainsKey(info.Name))
                        {
                            customPlayerData.UserCode.Remove(info.Name);
                            changed = true;
                        }

                        if (changed) SaveSystem.SaveCustom(customPlayerData);
                    }
                }
                catch (Exception ex)
                {
                    TxtConsole.Text += $"\n> Fehler: {ex.Message}";
                }
                dialog.Close();
            };

            btnPanel.Children.Add(btnNo);
            btnPanel.Children.Add(btnYes);
            grid.Children.Add(btnPanel);

            dialog.Content = grid;
            await dialog.ShowDialog(owner);
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

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            bool originalVimEnabled = AppSettings.IsVimEnabled;
            bool originalSyntaxEnabled = AppSettings.IsSyntaxHighlightingEnabled;
            bool originalErrorEnabled = AppSettings.IsErrorHighlightingEnabled;
            double originalUiScale = AppSettings.UiScale;
            bool isPortable = SaveSystem.IsPortableModeEnabled();
            bool originalPortableState = isPortable;

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
                ColumnDefinitions = new ColumnDefinitions("160, *"),
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(0)
            };

            var leftPanelGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Background = SolidColorBrush.Parse("#2D2D30")
            };

            var categoriesPanel = new StackPanel();

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

            Button CreateCatBtn(string text)
            {
                return new Button
                {
                    Content = text,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(15),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
            }

            var btnCatEditor = CreateCatBtn("Editor");
            var btnCatDisplay = CreateCatBtn("Darstellung");
            var btnCatData = CreateCatBtn("Daten");

            categoriesPanel.Children.Add(btnCatEditor);
            categoriesPanel.Children.Add(btnCatDisplay);
            categoriesPanel.Children.Add(btnCatData);

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
            btnSave.Content = LoadIcon("assets/icons/ic_save.svg", 20);
            ToolTip.SetTip(btnSave, "Einstellungen speichern");

            var btnReset = new Button
            {
                Width = 50,
                Height = 40,
                Background = SolidColorBrush.Parse("#B43232"),
                CornerRadius = new CornerRadius(5)
            };
            btnReset.Content = LoadIcon("assets/icons/ic_restart.svg", 20);
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

            // --- CONTROLS CREATION ---

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

            // display settings
            var sliderScale = new Slider
            {
                Minimum = 0.5,
                Maximum = 2.0,
                Value = AppSettings.UiScale,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var txtScaleVal = new TextBlock { Text = $"{AppSettings.UiScale:P0}", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };

            // data settings
            var chkPortable = new CheckBox
            {
                Content = "Portable Mode",
                IsChecked = isPortable,
                Foreground = Brushes.White
            };
            var txtPortableInfo = new TextBlock
            {
                Text = "Wenn aktiviert, wird der Speicherstand direkt neben der ausführbaren Datei gespeichert. Ideal für USB-Sticks.",
                Foreground = Brushes.Gray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(25, 0, 0, 0)
            };

            // check permissions
            bool canWriteRoot = SaveSystem.CanWriteToRoot();
            if (!canWriteRoot)
            {
                chkPortable.IsEnabled = false;
                chkPortable.Content += " (Keine Schreibrechte)";
                chkPortable.Foreground = Brushes.Gray;
                txtPortableInfo.Text = "Portable Mode ist hier nicht verfügbar, da keine Schreibrechte im Programmordner bestehen.";
                txtPortableInfo.Foreground = Brushes.Red;
            }

            void CheckChanges()
            {
                bool hasChanges =
                    (chkVim.IsChecked != originalVimEnabled) ||
                    (chkSyntax.IsChecked != originalSyntaxEnabled) ||
                    (chkError.IsChecked != originalErrorEnabled) ||
                    (chkPortable.IsChecked != isPortable) ||
                    (Math.Abs(sliderScale.Value - originalUiScale) > 0.004);

                btnSave.IsEnabled = hasChanges;
                btnSave.Opacity = hasChanges ? 1.0 : 0.5;
                btnSave.Background = hasChanges ? SolidColorBrush.Parse("#32A852") : SolidColorBrush.Parse("#464646");
            }

            // --- EVENT HANDLERS ---

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

            chkVim.IsCheckedChanged += (s, ev) => { AppSettings.IsVimEnabled = chkVim.IsChecked ?? false; UpdateVimState(); CheckChanges(); };
            chkPortable.IsCheckedChanged += (s, ev) => { CheckChanges(); };

            sliderScale.ValueChanged += (s, ev) =>
            {
                AppSettings.UiScale = ev.NewValue;
                txtScaleVal.Text = $"{ev.NewValue:P0}";
                ApplyUiScale();
                CheckChanges();
            };

            // --- LAYOUT ASSEMBLY ---

            // editor
            var editorSettings = new StackPanel { Spacing = 15 };
            editorSettings.Children.Add(new TextBlock { Text = "Code Editor", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            editorSettings.Children.Add(chkSyntax);
            editorSettings.Children.Add(chkError);
            editorSettings.Children.Add(chkVim);

            // display
            var displaySettings = new StackPanel { Spacing = 15 };
            var scalePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            scalePanel.Children.Add(sliderScale);
            scalePanel.Children.Add(txtScaleVal);
            displaySettings.Children.Add(new TextBlock { Text = "Darstellung", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            displaySettings.Children.Add(new TextBlock { Text = "UI Skalierung", Foreground = Brushes.LightGray });
            displaySettings.Children.Add(scalePanel);

            // data
            var dataSettingsPanel = new StackPanel { Spacing = 15 };
            dataSettingsPanel.Children.Add(new TextBlock { Text = "Daten & Speicher", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            dataSettingsPanel.Children.Add(chkPortable);
            dataSettingsPanel.Children.Add(txtPortableInfo);

            void ShowCategory(Button activeBtn, Control content)
            {
                btnCatEditor.Background = Brushes.Transparent;
                btnCatDisplay.Background = Brushes.Transparent;
                btnCatData.Background = Brushes.Transparent;

                activeBtn.Background = SolidColorBrush.Parse("#3E3E42");
                rightPanel.Child = content;
            }

            btnCatEditor.Click += (s, ev) => ShowCategory(btnCatEditor, editorSettings);
            btnCatDisplay.Click += (s, ev) => ShowCategory(btnCatDisplay, displaySettings);
            btnCatData.Click += (s, ev) => ShowCategory(btnCatData, dataSettingsPanel);

            ShowCategory(btnCatEditor, editorSettings);

            // --- WINDOW CONTROLS ---

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

            // save logic
            Action performSave = () =>
            {
                playerData.Settings.IsVimEnabled = AppSettings.IsVimEnabled;
                playerData.Settings.IsSyntaxHighlightingEnabled = AppSettings.IsSyntaxHighlightingEnabled;
                playerData.Settings.UiScale = AppSettings.UiScale;

                SaveSystem.Save(playerData);

                if (chkPortable.IsChecked != originalPortableState)
                {
                    try
                    {
                        SaveSystem.SetPortableMode(chkPortable.IsChecked == true);
                        isPortable = chkPortable.IsChecked == true;
                        originalPortableState = isPortable;

                        string location = isPortable ? "Programmordner" : "AppData";
                        TxtConsole.Text += $"\n> Speicherort geändert auf: {location}";
                    }
                    catch (Exception ex)
                    {
                        TxtConsole.Text += $"\n> Fehler beim Ändern des Speicherorts: {ex.Message}";
                    }
                }

                btnSave.IsEnabled = false;
                btnSave.Opacity = 0.5;
            };

            // btnSave click uses the Action
            btnSave.Click += (s, ev) => performSave();

            closeBtn.Click += async (s, ev) =>
            {
                if (btnSave.IsEnabled)
                {
                    var unsavedDialog = new Window
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
                        Text = "Du hast ungespeicherte Änderungen. Möchtest du diese speichern?",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var dBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 15, 0, 0) };
                    Grid.SetRow(dBtnPanel, 1);

                    var btnSaveClose = new Button { Content = "Speichern", Background = SolidColorBrush.Parse("#32A852"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
                    var btnDiscard = new Button { Content = "Verwerfen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
                    var btnCancel = new Button { Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };

                    btnSaveClose.Click += (_, __) =>
                    {
                        performSave();
                        unsavedDialog.Close();
                        settingsWin.Close();
                    };

                    btnDiscard.Click += (_, __) =>
                    {
                        unsavedDialog.Close();
                        settingsWin.Close();
                    };

                    btnCancel.Click += (_, __) => unsavedDialog.Close();

                    dBtnPanel.Children.Add(btnCancel);
                    dBtnPanel.Children.Add(btnDiscard);
                    dBtnPanel.Children.Add(btnSaveClose);

                    dGrid.Children.Add(dBtnPanel);
                    unsavedDialog.Content = dGrid;

                    await unsavedDialog.ShowDialog(settingsWin);
                }
                else
                {
                    settingsWin.Close();
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

                if (!AppSettings.IsVimEnabled)
                {
                    CodeEditor.Cursor = Cursor.Default;
                    VimStatusBorder.IsVisible = false;
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
                CodeEditor.SyntaxHighlighting = CsharpCodeEditor.GetDarkCsharpHighlighting();
            }
            else
            {
                CodeEditor.SyntaxHighlighting = null;
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
               
            // reset saved pos
            if (keyChar != "j" && keyChar != "k")
            {
                _vimDesiredColumn = -1;
            }

            // single key commands
            switch (keyChar)
            {
                // --- MODE SWITCHING ---
                case "i":
                    _vimMode = VimMode.Insert;
                    break;
                case "a":
                    var lineA = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    if (CodeEditor.CaretOffset < lineA.EndOffset)
                    {
                        CodeEditor.CaretOffset++;
                    }
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
                    _vimDesiredColumn = -1;
                    break;
                case "l":
                    int lineEndL = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset).EndOffset;
                    if (CodeEditor.CaretOffset < lineEndL)
                        CodeEditor.CaretOffset++;
                    _vimDesiredColumn = -1;
                    break;
                case "j":
                    var currentLineJ = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    if (currentLineJ.LineNumber < CodeEditor.Document.LineCount)
                    {   
                        if (_vimDesiredColumn == -1)
                            _vimDesiredColumn = CodeEditor.CaretOffset - currentLineJ.Offset;
                        var nextLine = CodeEditor.Document.GetLineByNumber(currentLineJ.LineNumber + 1);
                        int newOffset = nextLine.Offset + Math.Min(_vimDesiredColumn, nextLine.Length);
                        CodeEditor.CaretOffset = newOffset;
                    }
                    CodeEditor.TextArea.Caret.BringCaretToView();
                    break;
                case "k":
                    var currentLineK = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    if (currentLineK.LineNumber > 1)
                    {
                        if (_vimDesiredColumn == -1)
                            _vimDesiredColumn = CodeEditor.CaretOffset - currentLineK.Offset;
                        var prevLine = CodeEditor.Document.GetLineByNumber(currentLineK.LineNumber - 1);
                        int newOffset = prevLine.Offset + Math.Min(_vimDesiredColumn, prevLine.Length);
                        CodeEditor.CaretOffset = newOffset;
                    }
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
                    var line0 = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    string text0 = CodeEditor.Document.GetText(line0);
                    int indent = 0;
                    while (indent < text0.Length && char.IsWhiteSpace(text0[indent]))
                        indent++;
                    CodeEditor.CaretOffset = line0.Offset + indent;
                    break;

                case "$": // line end
                    var lineEnd1 = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    CodeEditor.CaretOffset = lineEnd1.EndOffset;
                    break;

                case "G": // file end
                    CodeEditor.CaretOffset = CodeEditor.Document.TextLength;
                    CodeEditor.TextArea.Caret.BringCaretToView();
                    break;
                case "D": // delete till line end
                    var lineD = CodeEditor.Document.GetLineByOffset(CodeEditor.CaretOffset);
                    int lenD = (lineD.Offset + lineD.Length) - CodeEditor.CaretOffset;
                    if (lenD > 0)
                    {
                        _vimClipboard = CodeEditor.Document.GetText(CodeEditor.CaretOffset, lenD);
                        CodeEditor.Document.Remove(CodeEditor.CaretOffset, lenD);
                    }
                    break;

                // --- EDITING ---
                case "x": // delete char
                    if (CodeEditor.Document.TextLength > CodeEditor.CaretOffset)
                        CodeEditor.Document.Remove(CodeEditor.CaretOffset, 1);
                    break;
                case "u": // undo
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
                    _vimDesiredColumn = -1;
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

        // --- LEVEL DESIGNER ---

        private void SyncEditorToDesigner()
        {
            if (!_isDesignerMode || _activeDesignerSource == DesignerSource.None) return;

            if (_activeDesignerSource == DesignerSource.StarterCode)
                TxtDesignStarter.Text = CodeEditor.Text;
            else if (_activeDesignerSource == DesignerSource.Validation)
                TxtDesignValidation.Text = CodeEditor.Text;
            else if (_activeDesignerSource == DesignerSource.TestingCode)
                TxtDesignTesting.Text = CodeEditor.Text;
        }

        private void OnDesignerInputChanged(object? sender, EventArgs e)
        {
            if (!_isDesignerMode || _isLoadingDesigner) return;

            BtnDesignerExport.IsEnabled = false;
            _verifiedDraftState = null;
            TxtDesignerStatus.Text = "Änderungen (Test erforderlich)";

            UpdateDesignerPreview();

            _designerAutoSaveTimer.Stop();

            if (ChkDesignerAutoSave.IsChecked == true)
            {
                _designerAutoSaveTimer.Start();
            }
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
            BtnNextLevel.IsVisible = !enable && (BtnNextLevel.IsVisible);

            BtnSave.IsVisible = !enable;
            BtnReset.IsVisible = !enable;
            BtnRun.IsVisible = !enable;

            if (enable)
            {
                _isLoadingDesigner = true;

                _originalSyntaxSetting = AppSettings.IsSyntaxHighlightingEnabled;
                _originalErrorSetting = AppSettings.IsErrorHighlightingEnabled;

                AppSettings.IsSyntaxHighlightingEnabled = true;
                AppSettings.IsErrorHighlightingEnabled = true;
                ApplySyntaxHighlighting();

                _currentDraftPath = draftPath;
                _currentDraft = LevelDesigner.LoadDraft(draftPath);

                // snapshot for unsaved changes check
                _lastSavedDraftJson = JsonSerializer.Serialize(_currentDraft);

                TxtDesignName.Text = _currentDraft.Name;
                TxtDesignAuthor.Text = _currentDraft.Author;
                TxtDesignDesc.Text = _currentDraft.Description;
                TxtDesignMaterials.Text = _currentDraft.Materials ?? "";

                TxtDesignStarter.Text = !string.IsNullOrEmpty(_currentDraft.StarterCode) ? _currentDraft.StarterCode : "";

                string defaultVal = "private static bool ValidateLevel(Assembly assembly, out string feedback)\n{\n    feedback = \"Gut gemacht!\";\n    return true;\n}";
                TxtDesignValidation.Text = !string.IsNullOrEmpty(_currentDraft.ValidationCode) ? _currentDraft.ValidationCode : defaultVal;

                TxtDesignTesting.Text = !string.IsNullOrEmpty(_currentDraft.TestCode) ? _currentDraft.TestCode : "";

                TxtDesignPlantUml.Text = _currentDraft.PlantUmlSource ?? "";

                ImgDiagram.Source = null;
                if (ImgScale != null) { ImgScale.ScaleX = 0.5; ImgScale.ScaleY = 0.5; }

                if (!string.IsNullOrEmpty(_currentDraft.PlantUmlSvgContent))
                {
                    ImgDiagram.Source = LoadSvgFromString(_currentDraft.PlantUmlSvgContent);
                }

                _activeDiagramIndex = 0;
                UpdateDesignerDiagramTabs();
                LoadDiagramContentToUI();

                RenderDesignerPrereqList();

                UpdateDesignerPreview();

                TxtDesignerStatus.Text = "Bereit";
                BtnDesignerExport.IsEnabled = false;
                _verifiedDraftState = null;

                CodeEditor.Text = "";
                TxtConsole.Text = $"> Level Designer geladen: '{Path.GetFileName(draftPath)}'\n> Wähle 'Im Editor bearbeiten' bei einem Code-Feld.";

                MainTabs.SelectedItem = TabDesigner;

                _isLoadingDesigner = false;
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

        private bool HasUnsavedDesignerChanges()
        {
            UpdateDraftFromUI();
            string currentJson = JsonSerializer.Serialize(_currentDraft);
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

                var dBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 15, 0, 0) };
                Grid.SetRow(dBtnPanel, 1);

                var btnSaveClose = new Button { Content = "Speichern", Background = SolidColorBrush.Parse("#32A852"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
                var btnDiscard = new Button { Content = "Verwerfen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
                var btnCancel = new Button { Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };

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
            TxtConsole.Text += "\n> Vorschau aktualisiert.";
        }

        private async void BtnDesignerSave_Click(object sender, RoutedEventArgs e)
        {
            await SaveDesignerDraft();
            TxtConsole.Text += "\n> Entwurf gespeichert.";
        }

        private void BtnDesignerGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = "https://github.com/OnlyCook/abitur-elite-code";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch (Exception ex)
            {
                TxtConsole.Text += $"\n> Fehler beim Öffnen des Guides: {ex.Message}";
            }
        }

        private async void BtnDesignerExport_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentDraft.Name?.Length ?? 0) < 1 || (_currentDraft.Author?.Length ?? 0) < 1)
            {
                TxtConsole.Foreground = Brushes.Orange;
                TxtConsole.Text += "\n⚠ Export abgelehnt: 'Level Name' und 'Autor' müssen gesetzt sein.";
                return;
            }

            if (_verifiedDraftState == null)
            {
                TxtConsole.Text += "\n❌ Fehler: Level muss vor dem Export erfolgreich getestet werden.";
                return;
            }

            TxtConsole.Foreground = Brushes.LightGray;
            TxtConsole.Text += "\n> Generiere alle Diagramme für finalen Export...";
            BtnDesignerExport.IsEnabled = false;

            // generate main diagram
            bool mainSuccess = await GenerateDiagramByIndex(0);

            // generate material diagrams
            for (int i = 0; i < _currentDraft.MaterialDiagrams.Count; i++)
            {
                bool matSuccess = await GenerateDiagramByIndex(i + 1);
                if (!matSuccess) mainSuccess = false;
            }

            if (!mainSuccess)
            {
                TxtConsole.Foreground = Brushes.Orange;
                TxtConsole.Text += "\n⚠ Hinweis: Einige Diagramme konnten nicht aktualisiert werden.";
            }

            _verifiedDraftState.PlantUmlSource = _currentDraft.PlantUmlSource;
            _verifiedDraftState.PlantUmlSvgContent = _currentDraft.PlantUmlSvgContent;
            _verifiedDraftState.MaterialDiagrams = new List<DiagramData>();
            foreach (var md in _currentDraft.MaterialDiagrams)
            {
                _verifiedDraftState.MaterialDiagrams.Add(new DiagramData
                {
                    Name = md.Name,
                    PlantUmlSource = md.PlantUmlSource,
                    PlantUmlSvgContent = md.PlantUmlSvgContent
                });
            }

            LevelDesigner.ExportLevel(_currentDraftPath, _verifiedDraftState);
            TxtConsole.Text += "\n> Level erfolgreich exportiert! (.elitelvl)";
            TxtDesignerStatus.Text = "Exportiert";
            BtnDesignerExport.IsEnabled = true;
        }

        private async Task<bool> GenerateDiagramByIndex(int index)
        {
            string source = "";
            if (index == 0) source = _currentDraft.PlantUmlSource;
            else source = _currentDraft.MaterialDiagrams[index - 1].PlantUmlSource;

            if (string.IsNullOrWhiteSpace(source)) return true;

            try
            {
                string prepared = PreparePlantUmlSource(source);
                string svg = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);

                if (index == 0) _currentDraft.PlantUmlSvgContent = svg;
                else _currentDraft.MaterialDiagrams[index - 1].PlantUmlSvgContent = svg;

                return true;
            }
            catch { return false; }
        }

        private void SwitchDesignerMode(DesignerSource source, TextBox targetBox, string enterMessage)
        {
            if (_activeDesignerSource == source)
            {
                // exit mode
                targetBox.Text = CodeEditor.Text;
                _activeDesignerSource = DesignerSource.None;
                CodeEditor.Text = "";
                TxtConsole.Text += "\n> Editor geleert. Wähle eine Datei zum Bearbeiten.";
            }
            else
            {
                SaveActiveDesignerSource();

                _activeDesignerSource = source;
                CodeEditor.Text = targetBox.Text;
                CodeEditor.Focus();
                TxtConsole.Text += enterMessage;
            }

            UpdateDesignerButtons();
        }


        private void BtnEditStarter_Click(object sender, RoutedEventArgs e)
        {
            SwitchDesignerMode(DesignerSource.StarterCode, TxtDesignStarter, "\n> Editor: Starter Code geladen (Ausführen deaktiviert).");
        }

        private void BtnEditValidation_Click(object sender, RoutedEventArgs e)
        {
            SwitchDesignerMode(DesignerSource.Validation, TxtDesignValidation, "\n> Editor: Validierungs-Code geladen (Ausführen deaktiviert).");
        }

        private void BtnEditTesting_Click(object sender, RoutedEventArgs e)
        {
            SwitchDesignerMode(DesignerSource.TestingCode, TxtDesignTesting, "\n> Editor: Test-Code geladen. 'Ausführen' jetzt verfügbar.");
        }

        private void SaveActiveDesignerSource()
        {
            if (_activeDesignerSource == DesignerSource.StarterCode)
                TxtDesignStarter.Text = CodeEditor.Text;
            else if (_activeDesignerSource == DesignerSource.Validation)
                TxtDesignValidation.Text = CodeEditor.Text;
            else if (_activeDesignerSource == DesignerSource.TestingCode)
                TxtDesignTesting.Text = CodeEditor.Text;
        }

        private void BtnResetValidation_Click(object sender, RoutedEventArgs e)
        {
            string defaultVal = "private static bool ValidateLevel(Assembly assembly, out string feedback)\n{\n    feedback = \"Gut gemacht!\";\n    return true;\n}";
            TxtDesignValidation.Text = defaultVal;

            if (_activeDesignerSource == DesignerSource.Validation)
            {
                CodeEditor.Text = defaultVal;
            }
            TxtConsole.Text += "\n> Validierungs-Code auf Standard zurückgesetzt.";
        }

        private void UpdateDesignerButtons()
        {
            // helper to switch icons based on active source
            void SetIcon(Button btn, string iconName)
            {
                btn.Content = LoadIcon($"assets/icons/{iconName}", 16);
            }

            // reset all to move icon
            SetIcon(BtnEditStarter, "ic_move.svg");
            ToolTip.SetTip(BtnEditStarter, "Im Editor bearbeiten");
            SetIcon(BtnEditValidation, "ic_move.svg");
            ToolTip.SetTip(BtnEditValidation, "Im Editor bearbeiten");
            SetIcon(BtnEditTesting, "ic_move.svg");
            ToolTip.SetTip(BtnEditTesting, "Im Editor testen");

            BtnRun.IsVisible = (_activeDesignerSource == DesignerSource.TestingCode);
    
            // exit button for active source
            if (_activeDesignerSource == DesignerSource.StarterCode)
            {
                SetIcon(BtnEditStarter, "ic_exit.svg");
                ToolTip.SetTip(BtnEditStarter, "Editor verlassen");
            }
            else if (_activeDesignerSource == DesignerSource.Validation)
            {
                SetIcon(BtnEditValidation, "ic_exit.svg");
                ToolTip.SetTip(BtnEditValidation, "Editor verlassen");
            }
            else if (_activeDesignerSource == DesignerSource.TestingCode)
            {
                SetIcon(BtnEditTesting, "ic_exit.svg");
                ToolTip.SetTip(BtnEditTesting, "Editor verlassen");
            }

            if (AppSettings.IsErrorHighlightingEnabled)
            {
                UpdateDiagnostics();
            }
        }

        private async System.Threading.Tasks.Task SaveDesignerDraft()
        {
            if (!_isDesignerMode) return;

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

            await LevelDesigner.SaveDraftAsync(_currentDraftPath, _currentDraft);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _lastSavedDraftJson = JsonSerializer.Serialize(_currentDraft);
                TxtDesignerStatus.Text = "Gespeichert";
            });
        }

        private void UpdateDraftFromUI()
        {
            _currentDraft.Name = TxtDesignName.Text;
            _currentDraft.Author = TxtDesignAuthor.Text;
            _currentDraft.Description = TxtDesignDesc.Text;
            _currentDraft.Materials = TxtDesignMaterials.Text;

            SaveCurrentDiagramContent();

            if (_activeDesignerSource == DesignerSource.StarterCode)
                _currentDraft.StarterCode = CodeEditor.Text;
            else
                _currentDraft.StarterCode = TxtDesignStarter.Text;

            if (_activeDesignerSource == DesignerSource.Validation)
                _currentDraft.ValidationCode = CodeEditor.Text;
            else
                _currentDraft.ValidationCode = TxtDesignValidation.Text;

            if (_activeDesignerSource == DesignerSource.TestingCode)
                _currentDraft.TestCode = CodeEditor.Text;
            else
                _currentDraft.TestCode = TxtDesignTesting.Text;
        }

        private void UpdateDesignerPreview()
        {
            UpdateDraftFromUI();

            PnlTask.Children.Clear();
            PnlMaterials.Children.Clear();

            PnlTask.Children.Add(new SelectableTextBlock
            {
                Text = _currentDraft.Name,
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextNormal,
                Margin = new Thickness(0)
            });

            if (!string.IsNullOrWhiteSpace(_currentDraft.Author))
            {
                PnlTask.Children.Add(new SelectableTextBlock
                {
                    Text = $"von {_currentDraft.Author}",
                    FontSize = 14,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 20)
                });
            }
            else // fallback
            {
                if (PnlTask.Children.Last() is Control last) last.Margin = new Thickness(0, 0, 0, 20);
            }

            RenderRichText(PnlTask, _currentDraft.Description);

            var draftSvgs = _currentDraft.MaterialDiagrams.Select(d => d.PlantUmlSvgContent).ToList();

            var dummyLevel = new Level
            {
                MaterialDocs = _currentDraft.Materials,
                Prerequisites = _currentDraft.Prerequisites
            };

            GenerateMaterials(dummyLevel, draftSvgs);
        }

        private void AddDesignerPrerequisite(string topic)
        {
            if (_currentDraft.Prerequisites.Count >= MaxPrerequisites)
            {
                TxtConsole.Text += $"\n> Limit erreicht (Max {MaxPrerequisites} Voraussetzungen).";
                return;
            }

            if (!_currentDraft.Prerequisites.Contains(topic))
            {
                _currentDraft.Prerequisites.Add(topic);
                RenderDesignerPrereqList();
                OnDesignerInputChanged(this, EventArgs.Empty);
            }
        }

        private void RemoveDesignerPrerequisite(string topic)
        {
            if (_currentDraft.Prerequisites.Contains(topic))
            {
                _currentDraft.Prerequisites.Remove(topic);
                RenderDesignerPrereqList();
                OnDesignerInputChanged(this, EventArgs.Empty);
            }
        }

        private void RenderDesignerPrereqList()
        {
            PnlDesignPrereqsList.Children.Clear();

            foreach (var item in _currentDraft.Prerequisites)
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

        private async void BtnGenerateUml_Click(object sender, RoutedEventArgs e)
        {
            await GeneratePlantUmlDiagram();
        }

        private void BtnWebUml_Click(object sender, RoutedEventArgs e)
        {
            var url = "https://www.plantuml.com/plantuml/duml/SoWkIImgAStDuNBAJrBGjLDmpCbCJbMmKiX8pSd9vt98pKi1IW80";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private string PreparePlantUmlSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "";

            // clipping fix
            source = Regex.Replace(source, @"(?m)^(\s*[-+#].*?)$", "$1 ");

            // add transparency and no icons if missing
            if (!source.Contains("skinparam backgroundcolor transparent") && !source.Contains("skinparam classAttributeIconSize 0"))
            {
                var lines = source.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                bool inserted = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Trim().StartsWith("@startuml"))
                    {
                        lines.Insert(i + 1, "skinparam backgroundcolor transparent");
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

        private async Task<bool> GeneratePlantUmlDiagram()
        {
            string rawCode = TxtDesignPlantUml.Text;

            if (string.IsNullOrWhiteSpace(rawCode))
            {
                _currentDraft.PlantUmlSource = "";
                _currentDraft.PlantUmlSvgContent = "";
                ImgDiagram.Source = null;
                return true;
            }

            string preparedCode = PreparePlantUmlSource(rawCode);

            TxtConsole.Text += "\n> Sende Anfrage an PlantUML Server...";

            try
            {
                string svgContent = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(preparedCode);

                if (_activeDiagramIndex == 0)
                {
                    _currentDraft.PlantUmlSource = rawCode;
                    _currentDraft.PlantUmlSvgContent = svgContent;

                    ImgDiagram.Source = LoadSvgFromString(svgContent);
                }
                else
                {
                    var d = _currentDraft.MaterialDiagrams[_activeDiagramIndex - 1];
                    d.PlantUmlSource = rawCode;
                    d.PlantUmlSvgContent = svgContent;

                    UpdateDesignerPreview();
                }

                TxtConsole.Text += "\n> Diagramm erfolgreich aktualisiert.";
                return true;
            }
            catch (Exception ex)
            {
                TxtConsole.Foreground = Brushes.Orange;
                TxtConsole.Text += $"\n> Fehler bei Diagramm-Erstellung: {ex.Message}";
                return false;
            }
        }

        private Avalonia.Media.IImage LoadSvgFromString(string svgContent)
        {
            if (string.IsNullOrEmpty(svgContent)) return null;
            try
            {
                // clean svg header
                int svgIndex = svgContent.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
                if (svgIndex > 0)
                {
                    svgContent = svgContent.Substring(svgIndex);
                }

                string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".svg");

                File.WriteAllText(tempPath, svgContent);

                var svgSource = SvgSource.Load(tempPath, null);

                try { File.Delete(tempPath); } catch { }

                return new SvgImage { Source = svgSource };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing SVG: {ex.Message}");
                return null;
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

            // main tab
            PnlDesignerDiagramTabs.Children.Add(CreateTab("Haupt", 0, _activeDiagramIndex == 0));

            // material tab
            for (int i = 0; i < _currentDraft.MaterialDiagrams.Count; i++)
            {
                PnlDesignerDiagramTabs.Children.Add(CreateTab($"Mat {i + 1}", i + 1, _activeDiagramIndex == i + 1));
            }

            BtnAddDiagramTab.IsVisible = _currentDraft.MaterialDiagrams.Count < 3;

            BtnDeleteDiagramTab.IsVisible = _activeDiagramIndex > 0;
        }

        private void SwitchDesignerDiagramTab(int newIndex)
        {
            if (newIndex == _activeDiagramIndex) return;

            SaveCurrentDiagramContent();

            _activeDiagramIndex = newIndex;

            LoadDiagramContentToUI();
            UpdateDesignerDiagramTabs();
        }

        private void SaveCurrentDiagramContent()
        {
            string content = TxtDesignPlantUml.Text ?? "";

            if (_activeDiagramIndex == 0)
            {
                _currentDraft.PlantUmlSource = content;
            }
            else
            {
                int listIndex = _activeDiagramIndex - 1;
                if (listIndex >= 0 && listIndex < _currentDraft.MaterialDiagrams.Count)
                {
                    _currentDraft.MaterialDiagrams[listIndex].PlantUmlSource = content;
                }
            }
        }

        private void LoadDiagramContentToUI()
        {
            string content = "";
            if (_activeDiagramIndex == 0)
            {
                content = _currentDraft.PlantUmlSource;
            }
            else
            {
                int listIndex = _activeDiagramIndex - 1;
                if (listIndex >= 0 && listIndex < _currentDraft.MaterialDiagrams.Count)
                {
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
    }
}
