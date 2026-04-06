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
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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
        public static bool IsSqlVimEnabled { get; set; } = false;
        public static bool IsSyntaxHighlightingEnabled { get; set; } = false;
        public static bool IsSqlSyntaxHighlightingEnabled { get; set; } = false;
        public static double EditorFontSize { get; set; } = 16.0;
        public static double SqlEditorFontSize { get; set; } = 16.0;
        public static double UiScale { get; set; } = 1.0;
        public static bool IsAutocompleteEnabled { get; set; } = false;
        public static bool IsSqlAutocompleteEnabled { get; set; } = false;
        public static bool IsErrorHighlightingEnabled { get; set; } = false;
        public static bool IsErrorExplanationEnabled { get; set; } = false;
        public static bool AutoCheckForUpdates { get; set; } = true;
        public static bool IsSqlAntiSpoilerEnabled { get; set; } = false;
    }

    public static class CodeGuard
    {
        public static System.Threading.CancellationToken Token { get; set; }
        public static void Check()
        {
            if (Token.IsCancellationRequested)
            {
                throw new Exception("Ausführung durch Timeout oder Benutzer abgebrochen.");
            }
        }
    }

    public partial class MainWindow : Window
    {
        private PlayerData playerData;
        private Level currentLevel;
        private List<Level> levels;
        private System.Timers.Timer autoSaveTimer;

        private System.Timers.Timer _relationalAutoSaveTimer;
        private List<RTable> _currentRelationalModel = new List<RTable>();
        private RColumn _focusedRColumn;
        private RTable _focusedRTable;
        private TextBox _focusedRColumnTextBox;
        private Button _btnGlobalPk;
        private Button _btnGlobalFk;
        private Image? _relationalValidationIcon;

        private Control? _hoveredSplitter;
        private Control? _activeDraggingSplitter;
        private bool _isDraggingSplitter = false;

        private ScaleTransform ImgScale;
        private TranslateTransform ImgTranslate;
        private Point _lastMousePoint;
        private bool _isDragging = false;
        private double _currentScale = 1.0;
        private int _currentDiagramIndex = 0;
        private GridLength _lastCsharpRowHeight = new GridLength(180);
        private GridLength _lastSqlRowHeight = new GridLength(250);

        private System.Threading.CancellationTokenSource? _compilationCts;
        private TextMarkerService _textMarkerService;
        private UnusedCodeTransformer _unusedCodeTransformer;
        private EscapeSequenceTransformer _escapeSequenceTransformer;
        private SemanticClassHighlightingTransformer _semanticClassTransformer;
        private DispatcherTimer _diagnosticTimer;
        private GhostCharacterTransformer _ghostCharTransformer;
        private BracketHighlightRenderer _bracketHighlightRenderer;
        private IndentationGuideRenderer _indentationGuideRenderer;
        private AutocompleteService _csharpAutocompleteService;
        private AutocompleteGhostGenerator _csharpAutocompleteGenerator;
        private AutocompleteService _sqlAutocompleteService;
        private AutocompleteGhostGenerator _sqlAutocompleteGenerator;
        private bool _suppressCsharpAutocomplete = false;
        private bool _suppressSqlAutocomplete = false;

        private const string MonospaceFontFamily = "Consolas, Menlo, Monaco, DejaVu Sans Mono, Roboto Mono, Courier New, monospace";

        private bool _hasRunOnce = false;

        private int _mouseTabSwitchCount = 0;
        private bool _isKeyboardTabSwitch = false;

        private DispatcherTimer _spoilerDelayTimer;
        private DispatcherTimer _spoilerActiveTimer;
        private bool _spoilerDelayMet = false;

        private DispatcherTimer _relationalTipDelayTimer;
        private DispatcherTimer _relationalTipDisplayTimer;
        private string _initialRelationalModelJson = "";

        private string _currentCustomValidationCode = null;
        private bool _isCustomLevelMode = false;
        private CustomPlayerData customPlayerData;

        private bool _isSqlMode = false;
        private List<SqlLevel> sqlLevels;
        private SqlLevel currentSqlLevel;
        private int _consecutiveSqlFails = 0;

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
        private string _nextCustomLevelPath = null;
        private string _newlyCreatedLevelPath = null;
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
        private DesignerSource _activeDesignerSource = DesignerSource.None;

        private SqlLevelDraft _currentSqlDraft = new SqlLevelDraft();
        private SqlLevelDraft _verifiedSqlDraftState = null;
        private List<SqlExpectedColumn> _verifiedExpectedSchema = null;
        private List<string[]> _verifiedExpectedResult = null;

        private enum VimMode { Normal, Insert, CommandPending, CommandLine, Search, Visual, VisualLine }
        private VimMode _vimMode = VimMode.Normal;
        private VimMode _vimPreviousMode = VimMode.Normal;
        private string _vimCommandBuffer = ""; // for multi char commands
        private string _vimClipboard = "";
        private int _vimDesiredColumn = -1;
        private int _vimVisualStartOffset = -1;
        private VimBlockCaretRenderer _csharpBlockCaret;
        private VimBlockCaretRenderer _sqlBlockCaret;
        private VimBlockCaretRenderer _tutorialBlockCaret;
        private bool _isTutorialMode = false;
        private int _tutorialStep = 0;
        private DateTime _tutorialStart;
        private int _tutorialKeystrokes = 0;
        private int _tutorialMouseClicks = 0;
        private int _tutorialPenalty = 0;

        private SolidColorBrush BrushTextNormal = SolidColorBrush.Parse("#E6E6E6");
        private SolidColorBrush BrushTextHighlight = SolidColorBrush.Parse("#6495ED"); // blue
        private SolidColorBrush BrushTextTitle = SolidColorBrush.Parse("#32A852"); // green
        private SolidColorBrush BrushBgPanel = SolidColorBrush.Parse("#202124");

        private class CustomLevelInfo
        {
            public string Name { get; set; }
            public string Author { get; set; }
            public string FilePath { get; set; }
            public string Section { get; set; }
            public bool IsDraft { get; set; } // .elitelvldraft = true; .elitelvl = false
            public bool QuickGenerate { get; set; }
        }

        private bool _updateAvailable = false;
        private string _latestVersion = "";
        private string _updateDownloadUrl = "";

        public MainWindow()
        {
            InitializeComponent();

            PrerequisiteSystem.Initialize();
            SqlPrerequisiteSystem.Initialize();

            var transformGroup = (TransformGroup)ImgDiagram.RenderTransform;
            ImgScale = (ScaleTransform)transformGroup.Children[0];
            ImgTranslate = (TranslateTransform)transformGroup.Children[1];

            levels = Curriculum.GetLevels();
            playerData = SaveSystem.Load();
            customPlayerData = SaveSystem.LoadCustom();

            AppSettings.IsVimEnabled = playerData.Settings.IsVimEnabled;
            AppSettings.IsSqlVimEnabled = playerData.Settings.IsSqlVimEnabled;
            AppSettings.IsSyntaxHighlightingEnabled = playerData.Settings.IsSyntaxHighlightingEnabled;
            AppSettings.IsSqlSyntaxHighlightingEnabled = playerData.Settings.IsSqlSyntaxHighlightingEnabled;
            AppSettings.IsAutocompleteEnabled = playerData.Settings.IsAutocompleteEnabled;
            AppSettings.IsSqlAutocompleteEnabled = playerData.Settings.IsSqlAutocompleteEnabled;
            AppSettings.EditorFontSize = playerData.Settings.EditorFontSize;
            AppSettings.SqlEditorFontSize = playerData.Settings.SqlEditorFontSize;
            AppSettings.UiScale = playerData.Settings.UiScale;
            AppSettings.AutoCheckForUpdates = playerData.Settings.AutoCheckForUpdates;
            AppSettings.IsSqlAntiSpoilerEnabled = playerData.Settings.IsSqlAntiSpoilerEnabled;

            // check if display is too small and scale down automatically
            var screen = this.Screens?.Primary;
            if (screen != null)
            {
                double screenWidth = screen.WorkingArea.Width;
                double screenHeight = screen.WorkingArea.Height;

                double baseWidth = 1250.0;
                double baseHeight = 850.0;

                bool resolutionChanged = Math.Abs(playerData.Settings.LastScreenWidth - screenWidth) > 1 || Math.Abs(playerData.Settings.LastScreenHeight - screenHeight) > 1;

                if (resolutionChanged)
                {
                    playerData.Settings.LastScreenWidth = screenWidth;
                    playerData.Settings.LastScreenHeight = screenHeight;
                }

                // only resize window if screen is actually smaller than base design size
                if (screenWidth < baseWidth || screenHeight < baseHeight)
                {
                    double scaleX = screenWidth / baseWidth;
                    double scaleY = screenHeight / baseHeight;
                    double targetScale = Math.Min(scaleX, scaleY);

                    // scale ui correspondingly (if resolution changed)
                    if (resolutionChanged && AppSettings.UiScale > targetScale)
                    {
                        AppSettings.UiScale = Math.Max(0.5, Math.Round(targetScale, 1));
                    }

                    this.Width = screenWidth;
                    this.Height = screenHeight - 50; // buffer for taskbar/header
                }
                else // standard behavior (monitor size >= 1300x900)
                {
                    this.Width = baseWidth;
                    this.Height = baseHeight;
                }
            }
            else // fallback (no screen detected)
            {
                this.Width = 1250;
                this.Height = 850;
            }

            if (AppSettings.AutoCheckForUpdates)
            {
                CheckForUpdatesBackground();
            }

            ApplyUiScale();
            ApplySyntaxHighlighting();
            ApplySqlSyntaxHighlighting();
            UpdateVimState();
            BuildVimCheatSheet();

            ConfigureEditor();
            ConfigureSqlQueryEditor();
            ConfigureTutorialEditor();
            UpdateShortcutsAndTooltips();

            autoSaveTimer = new System.Timers.Timer(2000)
            {
                AutoReset = false
            };
            autoSaveTimer.Elapsed += (s, e) => Dispatcher.UIThread.InvokeAsync(SaveCurrentProgress);

            _relationalAutoSaveTimer = new System.Timers.Timer(2000)
            {
                AutoReset = false
            };
            _relationalAutoSaveTimer.Elapsed += (s, e) => Dispatcher.UIThread.InvokeAsync(SaveCurrentProgress);

            _designerSyncTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _designerSyncTimer.Tick += (s, e) =>
            {
                _designerSyncTimer.Stop();
                SyncEditorToDesigner();
            };

            _relationalTipDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2),
            };
            _relationalTipDelayTimer.Tick += (s, e) =>
            {
                _relationalTipDelayTimer.Stop();
                if (MainTabs.SelectedIndex == 0 || MainTabs.SelectedIndex == 1)
                {
                    ShowRelationalTip();
                }
            };

            _relationalTipDisplayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(12)
            };
            _relationalTipDisplayTimer.Tick += (s, e) =>
            {
                _relationalTipDisplayTimer.Stop();
                PnlRelationalTip.IsVisible = false;
            };

            BtnCloseTip.Click += (s, e) => PnlTabTip.IsVisible = false;
            BtnCloseRelationalTip.Click += (s, e) =>
            {
                _relationalTipDisplayTimer.Stop();
                PnlRelationalTip.IsVisible = false;
            };

            BtnCloseTip.Click += (s, e) => PnlTabTip.IsVisible = false;
            MainTabs.SelectionChanged += OnMainTabChanged;
            MainTabs.LayoutUpdated += (s, e) => UpdateTabStyles();

            _spoilerDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _spoilerDelayTimer.Tick += (s, e) =>
            {
                _spoilerDelayTimer.Stop();
                _spoilerDelayMet = true;
                EvaluateSpoilerHintVisibility();
            };

            _spoilerActiveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _spoilerActiveTimer.Tick += (s, e) =>
            {
                if (playerData.Settings.SqlSpoilerHintDismissed) return;

                playerData.Settings.SqlSpoilerHintTotalSeconds += 0.5;
                if (playerData.Settings.SqlSpoilerHintTotalSeconds >= 4.0)
                {
                    playerData.Settings.SqlSpoilerHintDismissed = true;
                    SaveSystem.Save(playerData);
                }
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

            // fix 1 pixel vertical misalignment
            foreach (var margin in CodeEditor.TextArea.LeftMargins)
            {
                if (margin is LineNumberMargin lineMargin)
                {
                    lineMargin.Margin = new Thickness(0, 1, 0, 0);
                }
            }

            int maxId = playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1;
            var startLevel = levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0];
            LoadLevel(startLevel);

            this.Opened += (s, e) =>
            {
                CodeEditor.Focus();
                // fix unscaled window for certain aspect ratios
                Dispatcher.UIThread.Post(() =>
                {
                    this.InvalidateMeasure();
                    RootScaleTransform?.InvalidateMeasure();
                }, DispatcherPriority.Render);
            };

            // global shortcuts
            this.AddHandler(KeyDownEvent, (s, e) =>
            {
                // global ui scaling via ctrl +/- (only outside of editors)
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
                {
                    bool isEditorFocused = (CodeEditor?.IsFocused == true || CodeEditor?.TextArea?.IsFocused == true) ||
                                           (SqlQueryEditor?.IsFocused == true || SqlQueryEditor?.TextArea?.IsFocused == true) ||
                                           (TutorialEditor?.IsFocused == true || TutorialEditor?.TextArea?.IsFocused == true);

                    if (!isEditorFocused)
                    {
                        if (e.Key == Key.OemPlus || e.Key == Key.Add)
                        {
                            AppSettings.UiScale = Math.Min(2.0, Math.Round(AppSettings.UiScale + 0.1, 1));
                            ApplyUiScale();
                            e.Handled = true;
                            return;
                        }
                        else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                        {
                            AppSettings.UiScale = Math.Max(0.5, Math.Round(AppSettings.UiScale - 0.1, 1));
                            ApplyUiScale();
                            e.Handled = true;
                            return;
                        }
                    }
                }

                if (e.Key == Key.F1)
                {
                    _isKeyboardTabSwitch = true;
                    MainTabs.SelectedIndex = 0;
                    e.Handled = true;
                }
                else if (e.Key == Key.F2)
                {
                    // cycle diagrams if already on tab and multiple diagrams exist
                    if (MainTabs.SelectedIndex == 1 && !_isSqlMode && currentLevel?.DiagramPaths?.Count > 1)
                    {
                        _currentDiagramIndex++;
                        if (_currentDiagramIndex >= currentLevel.DiagramPaths.Count)
                            _currentDiagramIndex = 0;

                        ImgDiagram.Source = LoadDiagramImage(currentLevel.DiagramPaths[_currentDiagramIndex]);

                        BtnDiagram1.Background = _currentDiagramIndex == 0 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                        BtnDiagram2.Background = _currentDiagramIndex == 1 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                        BtnDiagram3.Background = _currentDiagramIndex == 2 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                    }

                    _isKeyboardTabSwitch = true;
                    MainTabs.SelectedIndex = 1;
                    e.Handled = true;
                }
                else if (e.Key == Key.F3)
                {
                    _isKeyboardTabSwitch = true;
                    MainTabs.SelectedIndex = 2;
                    e.Handled = true;
                }
                else if (e.Key == Key.F4)
                {
                    if (_isDesignerMode)
                    {
                        _isKeyboardTabSwitch = true;
                        MainTabs.SelectedIndex = 3;
                        e.Handled = true;
                    }
                }
            }, RoutingStrategies.Tunnel);

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

            this.AddHandler(PointerPressedEvent, (s, e) =>
            {
                Control current = e.Source as Control;
                GridSplitter splitter = null;

                // get gridsplitter
                while (current != null)
                {
                    if (current is GridSplitter gs)
                    {
                        splitter = gs;
                        break;
                    }
                    current = current.Parent as Control ?? current.TemplatedParent as Control;
                }

                if (splitter != null)
                {
                    _isDraggingSplitter = true;
                    _activeDraggingSplitter = splitter;

                    splitter.Classes.Add("dragging");
                    splitter.Classes.Add("hover-active");
                }
            }, RoutingStrategies.Tunnel);

            this.AddHandler(PointerReleasedEvent, (s, e) =>
            {
                if (_isDraggingSplitter && _activeDraggingSplitter != null)
                {
                    _isDraggingSplitter = false;
                    _activeDraggingSplitter.Classes.Remove("dragging");

                    // remove hover if mouse isnt focusing
                    if (!_activeDraggingSplitter.IsPointerOver)
                    {
                        _activeDraggingSplitter.Classes.Remove("hover-active");
                        if (_hoveredSplitter == _activeDraggingSplitter)
                            _hoveredSplitter = null;
                    }

                    _activeDraggingSplitter = null;
                }
                else if (!_isDraggingSplitter && _hoveredSplitter != null && !_hoveredSplitter.IsPointerOver)
                {
                    _hoveredSplitter.Classes.Remove("hover-active");
                    _hoveredSplitter = null;
                }
            }, RoutingStrategies.Tunnel);

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
            TxtDesignSqlSetup.TextChanged += OnDesignerInputChanged;
            TxtDesignSqlVerify.TextChanged += OnDesignerInputChanged;
            TxtDesignPrereqInput.TextChanged += (s, e) =>
            {
                string query = TxtDesignPrereqInput.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(query))
                {
                    PopupPrereqSuggestions.IsVisible = false;
                    return;
                }

                var sourceList = _isSqlMode ? SqlPrerequisiteSystem.AllTopics : PrerequisiteSystem.AllTopics;
                var matches = sourceList
                    .Where(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)
                             && (_isSqlMode ? !_currentSqlDraft.Prerequisites.Contains(t) : !_currentDraft.Prerequisites.Contains(t)))
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

            TxtDesignSqlSample.TextChanged += OnDesignerInputChanged;

            CmbSqlValidationMode.SelectionChanged += (s, e) => {
                if (_isLoadingDesigner) return;
                _currentSqlDraft.IsDmlMode = CmbSqlValidationMode.SelectedIndex == 1;
                PnlDesignSqlVerify.IsVisible = _currentSqlDraft.IsDmlMode;
                _designerAutoSaveTimer.Stop(); _designerAutoSaveTimer.Start();
            };
        }

        private void UpdateTabStyles()
        {
            var tabItems = MainTabs.Items.OfType<TabItem>().Where(t => t.IsVisible).ToList();
            if (tabItems.Count == 0) return;

            // find bottom most row
            double maxY = tabItems.Max(t => t.Bounds.Y);

            foreach (var tab in tabItems)
            {
                if (Math.Abs(tab.Bounds.Y - maxY) < 2.0) // small buffer
                {
                    if (!tab.Classes.Contains("latch"))
                        tab.Classes.Add("latch");
                }
                else
                {
                    tab.Classes.Remove("latch");
                }
            }
        }

        private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != MainTabs) return;

            bool wasQueryEditorFocused = SqlQueryEditor?.IsFocused == true || SqlQueryEditor?.TextArea?.IsFocused == true;

            // immediately drop focus linkage so it doesnt steal back
            if (wasQueryEditorFocused)
            {
                UpdateFocusedColumn(null, null);
            }

            if (_isSqlMode)
            {
                bool isReadOnly = false;
                if (!_isDesignerMode && currentSqlLevel != null)
                    isReadOnly = currentSqlLevel.IsRelationalModelReadOnly;

                if (currentSqlLevel != null || _isDesignerMode)
                {
                    if (MainTabs.SelectedIndex == 0 && PnlTaskRelationalModel != null)
                        RenderRelationalModel(PnlTaskRelationalModel, isReadOnly);
                    else if (MainTabs.SelectedIndex == 1 && PnlUmlRelationalModel != null)
                        RenderRelationalModel(PnlUmlRelationalModel, isReadOnly);
                }
            }

            if (wasQueryEditorFocused)
            {
                Dispatcher.UIThread.Post(() => SqlQueryEditor.Focus());
            }

            // live preview update on tab switch
            if (_isDesignerMode && (MainTabs.SelectedIndex == 0 || MainTabs.SelectedIndex == 2))
            {
                UpdateDesignerPreview();
            }

            if (_isKeyboardTabSwitch)
            {
                _isKeyboardTabSwitch = false;
                _mouseTabSwitchCount = 0;
                return;
            }

            _mouseTabSwitchCount++;

            int shownCount = playerData.Settings.TabTipShownCount;

            if ((shownCount == 0 && _mouseTabSwitchCount >= 5) ||
                (shownCount == 1 && _mouseTabSwitchCount >= 20))
            {
                ShowTabTip();
            }

            EvaluateSpoilerHintVisibility();
        }

        private void UpdateSqlAutocompleteSchema()
        {
            if (_sqlAutocompleteService == null) return;
            var schema = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            // check if current level is an abitur similar level (>= 29)
            bool isAbiturLevel = currentSqlLevel != null && currentSqlLevel.Id >= 29;

            foreach (var t in _currentRelationalModel)
            {
                schema[t.Name] = t.Columns.Select(c => (c.IsFk && !isAbiturLevel) ? c.Name + "_FK" : c.Name).ToList();
            }
            _sqlAutocompleteService.SetSqlSchema(schema);
        }

        private void ShowTabTip()
        {
            if (PnlTabTip.IsVisible) return;

            PnlTabTip.IsVisible = true;

            playerData.Settings.TabTipShownCount++;
            SaveSystem.Save(playerData);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                PnlTabTip.IsVisible = false;
                timer.Stop();
            };
            timer.Start();
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
                Assembly.Load("System.Collections"),
                typeof(MainWindow).Assembly
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

        private TextEditor ActiveEditor => _isTutorialMode ? TutorialEditor : (_isSqlMode ? SqlQueryEditor : CodeEditor);

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
                            string combinedText = $"{TxtDesignSqlSetup.Text}\n{TxtDesignSqlSample.Text}\n{TxtDesignSqlVerify.Text}\n{SqlQueryEditor.Text}";
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
                    if (SqlQueryEditor.CaretOffset == line.EndOffset && line.Length > 0)
                    {
                        SqlQueryEditor.CaretOffset--;
                    }
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
            SqlQueryEditor.AddHandler(InputElement.KeyDownEvent, SqlQueryEditor_KeyDown, RoutingStrategies.Tunnel);

            SqlQueryEditor.AddHandler(InputElement.PointerWheelChangedEvent, SqlQueryEditor_PointerWheelChanged, RoutingStrategies.Tunnel);

            // clear relational model focus tracking when the sql editor is focused manually
            SqlQueryEditor.GotFocus += (s, e) =>
            {
                UpdateFocusedColumn(null, null);
            };
            SqlQueryEditor.TextArea.GotFocus += (s, e) =>
            {
                UpdateFocusedColumn(null, null);
            };

            // fix 1 pixel vertical misalignment
            foreach (var margin in SqlQueryEditor.TextArea.LeftMargins)
            {
                if (margin is LineNumberMargin lineMargin)
                {
                    lineMargin.Margin = new Thickness(0, 1, 0, 0);
                }
            }
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
            if (AppSettings.IsSqlAutocompleteEnabled && (e.Key == Key.Up || e.Key == Key.Down) && _sqlAutocompleteService.HasSuggestion)
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
            if (e.Key == Key.Z && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
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
            if (!AppSettings.IsSqlVimEnabled)
            {
                return;
            }

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

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                if (e.Key == Key.C || e.Key == Key.V || e.Key == Key.X || e.Key == Key.A)
                {
                    return;
                }
            }

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
                {
                    AppSettings.SqlEditorFontSize = Math.Min(48, AppSettings.SqlEditorFontSize + 1);
                }
                else if (e.Delta.Y < 0)
                {
                    AppSettings.SqlEditorFontSize = Math.Max(8, AppSettings.SqlEditorFontSize - 1);
                }
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
            {
                if (offset < textArea.Document.TextLength && textArea.Document.GetCharAt(offset) == charTyped)
                {
                    textArea.Caret.Offset += 1;
                    e.Handled = true;
                    return;
                }
            }

            // surround selection logic
            if (textArea.Selection.Length > 0)
            {
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
            }

            // auto add designated pair
            if (charTyped == '(' || charTyped == '"' || charTyped == '\'')
            {
                string pair = charTyped == '(' ? ")" : charTyped.ToString();

                textArea.Document.Insert(offset, charTyped.ToString() + pair);
                textArea.Caret.Offset = offset + 1;
                e.Handled = true;
                return;
            }

            // skip closing pair
            if (charTyped == ')' || charTyped == '"' || charTyped == '\'')
            {
                if (offset < textArea.Document.TextLength && textArea.Document.GetCharAt(offset) == charTyped)
                {
                    textArea.Caret.Offset += 1;
                    e.Handled = true;
                    return;
                }
            }
        }

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
                    if (CodeEditor.CaretOffset == line.EndOffset && line.Length > 0)
                    {
                        CodeEditor.CaretOffset--;
                    }
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
            CodeEditor.AddHandler(InputElement.KeyDownEvent, CodeEditor_KeyDown, RoutingStrategies.Tunnel);

            CodeEditor.AddHandler(InputElement.PointerWheelChangedEvent, CodeEditor_PointerWheelChanged, RoutingStrategies.Tunnel);

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
            {
                if (margin is LineNumberMargin lineMargin)
                {
                    lineMargin.Margin = new Thickness(0, 1, 0, 0);
                }
            }
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

        private void UpdateShortcutsAndTooltips()
        {
            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            string ctrlKey = isMac ? "Cmd" : "Ctrl";

            // update tooltips
            ToolTip.SetTip(BtnSave, $"{(_isSqlMode ? "Query" : "Code")} speichern ({ctrlKey} + S)");

            if (BtnSettings.Parent is Panel parentPanel)
            {
                var runBtn = parentPanel.Children.LastOrDefault() as Button;
                if (runBtn != null)
                {
                    ToolTip.SetTip(runBtn, $"Ausführen (F5)");
                }
            }
        }

        private void AddToConsole(string text, IBrush color)
        {
            Dispatcher.UIThread.Post(() =>
            {
                TxtConsole.Inlines ??= new Avalonia.Controls.Documents.InlineCollection();

                // split text to use explicit linebreaks
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i > 0)
                    {
                        TxtConsole.Inlines.Add(new LineBreak());
                    }

                    if (!string.IsNullOrEmpty(lines[i]))
                    {
                        TxtConsole.Inlines.Add(new Run
                        {
                            Text = lines[i],
                            Foreground = color,
                            FontFamily = new FontFamily(MonospaceFontFamily)
                        });
                    }
                }

                ConsoleScroller?.ScrollToEnd();
            });
        }

        private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
        {
            // escape key to clear suggestions
            if (e.Key == Key.Escape && _csharpAutocompleteService.HasSuggestion)
            {
                _csharpAutocompleteService.ClearSuggestion();
                CodeEditor.TextArea.TextView.Redraw();
                e.Handled = true;
                return;
            }

            // up and down arrows for autocompletion cycling
            if (AppSettings.IsAutocompleteEnabled && (e.Key == Key.Up || e.Key == Key.Down) && _csharpAutocompleteService.HasSuggestion)
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
            if (e.Key == Key.Z && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
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
                    {
                        BtnRun_Click(this, null);
                    }
                    else
                    {
                        AddToConsole("\n> Ausführen im Designer nur im 'Test-Code' Editor möglich.", Brushes.LightGray);
                    }
                    e.Handled = true;
                    return;
                }

                BtnRun_Click(this, null);
                e.Handled = true;
                return;
            }

            // tab => confirm autocompletion
            if (AppSettings.IsAutocompleteEnabled && e.Key == Key.Tab && _csharpAutocompleteService.HasSuggestion)
            {
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

            if (!AppSettings.IsVimEnabled)
            {
                return;
            }

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

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                if (e.Key == Key.C || e.Key == Key.V || e.Key == Key.X || e.Key == Key.A)
                {
                    return;
                }
            }

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
                {
                    AppSettings.EditorFontSize = Math.Min(48, AppSettings.EditorFontSize + 1);
                }
                else if (e.Delta.Y < 0)
                {
                    AppSettings.EditorFontSize = Math.Max(8, AppSettings.EditorFontSize - 1);
                }
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
            {
                if (offset > 0 && offset < textArea.Document.TextLength)
                {
                    if (textArea.Document.GetCharAt(offset - 1) == '<' && textArea.Document.GetCharAt(offset) == '>')
                    {
                        textArea.Document.Remove(offset, 1);
                    }
                }
            }

            // skip closing pair
            if (charTyped == '"' || charTyped == '\'')
            {
                if (offset < textArea.Document.TextLength && textArea.Document.GetCharAt(offset) == charTyped)
                {
                    textArea.Caret.Offset += 1;
                    e.Handled = true;
                    return;
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
            if (charTyped == '(' || charTyped == '{' || charTyped == '[' || charTyped == '"' ||  charTyped == '\'' || charTyped == '<')
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
                    : charTyped == '\"' ? "\""
                    : "\'";

                textArea.Document.Insert(offset, charTyped.ToString() + pair);
                textArea.Caret.Offset = offset + 1;
                e.Handled = true;
                return;
            }

            // skip closing pair
            if (charTyped == ')' || charTyped == '}' || charTyped == ']' || charTyped == '"' || charTyped == '\'' || charTyped == '>')
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

            if (_isSqlMode && currentSqlLevel != null)
            {
                string codeToSave = SqlQueryEditor.Text;

                if (_isCustomLevelMode)
                {
                    if (customPlayerData.UserSqlCode.ContainsKey(currentSqlLevel.Title))
                        customPlayerData.UserSqlCode[currentSqlLevel.Title] = codeToSave;
                    else
                        customPlayerData.UserSqlCode.Add(currentSqlLevel.Title, codeToSave);

                    // only save relational model if it is not read only
                    if (!currentSqlLevel.IsRelationalModelReadOnly)
                    {
                        string modelJson = JsonSerializer.Serialize(_currentRelationalModel);

                        if (customPlayerData.UserSqlModels.ContainsKey(currentSqlLevel.Title))
                            customPlayerData.UserSqlModels[currentSqlLevel.Title] = modelJson;
                        else
                            customPlayerData.UserSqlModels.Add(currentSqlLevel.Title, modelJson);
                    }

                    SaveSystem.SaveCustom(customPlayerData);
                    return;
                }

                if (playerData.UserSqlCode.ContainsKey(currentSqlLevel.Id))
                    playerData.UserSqlCode[currentSqlLevel.Id] = codeToSave;
                else
                    playerData.UserSqlCode.Add(currentSqlLevel.Id, codeToSave);

                // only save relational model if it is not read only
                if (!currentSqlLevel.IsRelationalModelReadOnly)
                {
                    string modelJson = JsonSerializer.Serialize(_currentRelationalModel);

                    // sync model across the whole section if set
                    if (currentSqlLevel.IsRelationalModelSectionShared && sqlLevels != null)
                    {
                        var levelsInSection = sqlLevels.Where(l => l.Section == currentSqlLevel.Section);
                        foreach (var lvl in levelsInSection)
                        {
                            if (playerData.UserSqlModels.ContainsKey(lvl.Id))
                                playerData.UserSqlModels[lvl.Id] = modelJson;
                            else
                                playerData.UserSqlModels.Add(lvl.Id, modelJson);
                        }
                    }
                    else
                    {
                        if (playerData.UserSqlModels.ContainsKey(currentSqlLevel.Id))
                            playerData.UserSqlModels[currentSqlLevel.Id] = modelJson;
                        else
                            playerData.UserSqlModels.Add(currentSqlLevel.Id, modelJson);
                    }
                }

                SaveSystem.Save(playerData);
                return;
            }

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
                playerData.Settings.IsSqlVimEnabled = AppSettings.IsSqlVimEnabled;
                playerData.Settings.IsSyntaxHighlightingEnabled = AppSettings.IsSyntaxHighlightingEnabled;
                playerData.Settings.IsAutocompleteEnabled = AppSettings.IsAutocompleteEnabled;
                playerData.Settings.IsSqlAutocompleteEnabled = AppSettings.IsSqlAutocompleteEnabled;
                playerData.Settings.EditorFontSize = AppSettings.EditorFontSize;
                playerData.Settings.SqlEditorFontSize = AppSettings.SqlEditorFontSize;
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
                _nextCustomLevelPath = null;
            }

            SaveCurrentProgress();

            // reset error highlighting on every load
            AppSettings.IsErrorHighlightingEnabled = false;
            ClearDiagnostics();

            currentLevel = level;
            _currentDiagramIndex = 0;
            UpdateNavigationButtons();

            PnlDiagramSwitch.IsVisible = false;
            BtnDiagram1.IsVisible = false;
            BtnDiagram2.IsVisible = false;
            BtnDiagram3.IsVisible = false;

            if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
            {
                ImgDiagram.Source = LoadDiagramImage(level.DiagramPaths[0]);
                TxtNoDiagram.IsVisible = false;

                if (level.DiagramPaths.Count > 1)
                {
                    PnlDiagramSwitch.IsVisible = true;
                    BtnDiagram1.IsVisible = true;
                    BtnDiagram2.IsVisible = true;

                    // highlight first button
                    BtnDiagram1.Background = SolidColorBrush.Parse("#007ACC");
                    BtnDiagram2.Background = SolidColorBrush.Parse("#3C3C3C");
                    BtnDiagram3.Background = SolidColorBrush.Parse("#3C3C3C");

                    if (level.DiagramPaths.Count >= 3)
                    {
                        BtnDiagram3.IsVisible = true;
                    }
                }
            }
            else
            {
                ImgDiagram.Source = null;
                TxtNoDiagram.IsVisible = true;
            }

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
            if (!_isSqlMode && !level.NoUMLAutoScale)
            {
                _currentScale = 0.5;
            }
            else
            {
                _currentScale = 1.0;
            }
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
                        Margin = new Thickness(0, 0, 0, 15)
                    }
                );
            }

            WrapPanel tagsPanel = BuildTagsPanel(level.Difficulty, level.Topics, level.DiagramTags, false);
            if (tagsPanel != null) PnlTask.Children.Add(tagsPanel);

            RenderRichText(PnlTask, level.Description);

            try
            {
                if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
                {
                    ImgDiagram.Source = LoadDiagramImage(level.DiagramPaths[0]);
                    TxtNoDiagram.IsVisible = false;
                }
                else
                {
                    ImgDiagram.Source = null;
                    TxtNoDiagram.IsVisible = true;
                }
            }
            catch
            {
                ImgDiagram.Source = null;
                TxtNoDiagram.IsVisible = true;
            }

            GenerateMaterials(level, _isCustomLevelMode ? _currentCustomSvgs : null);

            TxtConsole.Inlines?.Clear();

            if (!_isCustomLevelMode)
            {
                AddToConsole($"> System initialisiert.\n> Level {level.Id} (Code: {level.SkipCode}) geladen.", Brushes.LightGray);
            }
            else
            {
                AddToConsole("> System initialisiert.", Brushes.LightGray);
            }

            UpdateSemanticHighlighting(); // init scan

            Dispatcher.UIThread.Post(() => CodeEditor.Focus());
        }


        private WrapPanel BuildTagsPanel(string difficulty, List<string> topics, List<string> diagrams, bool isSql)
        {
            if (difficulty == "" && (topics == null || topics.Count == 0)  && (diagrams == null || diagrams.Count == 0))
            {
                return null;
            }

            var panel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, -10, 0, 15)
            };

            // difficulty tag
            if (!string.IsNullOrEmpty(difficulty))
            {
                IBrush diffColor = Brushes.Gray;
                string tooltip = "";

                switch (difficulty.ToLower())
                {
                    case "einfach":
                        diffColor = SolidColorBrush.Parse("#28a745");
                        tooltip = isSql ? "Einfache SELECT-Abfragen, meist auf einer einzelnen Tabelle." : "Grundlegende Programmierkonzepte, wenig Vernetzung.";
                        break;
                    case "mittel":
                        diffColor = SolidColorBrush.Parse("#d08770");
                        tooltip = isSql ? "Abfragen mit JOINs, GROUP BY und einfachen Unterabfragen." : "Komplexere Logik, erste Objektinteraktionen und Datenstrukturen.";
                        break;
                    case "schwer":
                        diffColor = SolidColorBrush.Parse("#B43232");
                        tooltip = isSql ? "Komplexe Unterabfragen, aggregierte Joins und Verschachtelungen." : "Komplexe Algorithmen, Datenstrukturen (Listen/Arrays) und Architektur.";
                        break;
                    case "abitur":
                        diffColor = SolidColorBrush.Parse("#8A2BE2");
                        tooltip = isSql ? "Auf Abitur-Niveau: Komplexe Auswertungen über viele Relationen." : "Auf Abitur-Niveau: Netzwerkkommunikation, Parsing, komplexe Objektgeflechte.";
                        break;
                }

                var diffBorder = CreateTagBorder(difficulty.ToUpper(), diffColor);
                ToolTip.SetTip(diffBorder, tooltip);
                panel.Children.Add(diffBorder);
            }

            // topic tags (max of 3, currently c# only)
            if (!isSql && topics != null)
            {
                foreach (var topic in topics.Take(3))
                {
                    panel.Children.Add(CreateTagBorder(topic, SolidColorBrush.Parse("#007ACC")));
                }
            }

            // diagram tags (max of 3)
            if (diagrams != null)
            {
                foreach (var diag in diagrams.Take(3))
                {
                    panel.Children.Add(CreateTagBorder(diag, SolidColorBrush.Parse("#555555")));
                }
            }

            return panel;
        }

        private Border CreateTagBorder(string text, IBrush bgColor)
        {
            return new Border
            {
                Background = bgColor,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                Margin = new Thickness(0, 0, 5, 5),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void RenderRichText(StackPanel panel, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string safeText = text.Replace("|[", "\x01").Replace("|]", "\x02");
            var parts = Regex.Split(safeText, @"(\{\|[\s\S]*?\|\}|\[.*?\]|\*\*.*?\*\*|(?<!\w)__.*?__(?!\w))");

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

                    IHighlightingDefinition highlighting = _isSqlMode
                        ? SqlCodeEditor.GetDarkSqlHighlighting()
                        : CsharpCodeEditor.GetDarkCsharpHighlighting();

                    var codeBlockEditor = new TextEditor
                    {
                        Document = new TextDocument(codeContent),
                        SyntaxHighlighting = highlighting,
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
                // underline text
                else if (part.StartsWith("__") && part.EndsWith("__") && part.Length >= 4)
                {
                    string content = part.Substring(2, part.Length - 4);
                    content = content.Replace("\x01", "[").Replace("\x02", "]");

                    currentTb.Inlines.Add(new Run
                    {
                        Text = content,
                        Foreground = BrushTextNormal,
                        TextDecorations = TextDecorations.Underline
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
                    if (currentLevel != null && currentLevel.Id == 26)
                    {
                        header += "public partial class FlughafenVerwaltung {\n";
                    }
                }

                string fullCode = header + code + (isValidationMode ? "\n}" : "");
                if (!isValidationMode && currentLevel != null && currentLevel.Id == 26)
                {
                    fullCode += "\n}";
                }

                var userTree = CSharpSyntaxTree.ParseText(fullCode);
                var trees = new List<SyntaxTree> { userTree };

                if (currentLevel.AuxiliaryIds != null)
                {
                    foreach (var auxId in currentLevel.AuxiliaryIds)
                    {
                        string auxCode = AuxiliaryImplementations.GetCode(auxId, code);
                        if (!string.IsNullOrEmpty(auxCode))
                        {
                            trees.Add(CSharpSyntaxTree.ParseText(auxCode));
                        }
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
                    _textMarkerService.Add(start, length, Colors.Red, diag.GetMessage());
                }
                else if (diag.Severity == DiagnosticSeverity.Warning)
                {
                    // unused vars
                    if (unusedWarningIds.Contains(diag.Id))
                    {
                        _unusedCodeTransformer.UnusedSegments.Add(new TextSegment { StartOffset = start, Length = length });
                    }
                    else
                    {
                        _textMarkerService.Add(start, length, Colors.Yellow, diag.GetMessage());
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

        private void Diagram_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (ImgScale == null || ImgTranslate == null || sender is not Control panel)
                return;

            double zoomSpeed = 0.1;
            double oldScale = _currentScale;

            ImgDiagram.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            if (e.Delta.Y > 0)
                _currentScale += zoomSpeed;
            else
                _currentScale -= zoomSpeed;

            if (_currentScale < 0.1) _currentScale = 0.1;
            if (_currentScale > 5.0) _currentScale = 5.0;

            var pointerPos = e.GetCurrentPoint(panel).Position;

            double imgCenterX = panel.Bounds.Width / 2 + ImgTranslate.X;
            double imgCenterY = panel.Bounds.Height / 2 + ImgTranslate.Y;

            double scaleFactor = _currentScale / oldScale;

            ImgTranslate.X -= (pointerPos.X - imgCenterX) * (scaleFactor - 1);
            ImgTranslate.Y -= (pointerPos.Y - imgCenterY) * (scaleFactor - 1);

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

        private void BtnDiagramSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (currentLevel == null || currentLevel.DiagramPaths == null) return;
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int index))
            {
                if (index >= 0 && index < currentLevel.DiagramPaths.Count)
                {
                    _currentDiagramIndex = index;
                    ImgDiagram.Source = LoadDiagramImage(currentLevel.DiagramPaths[index]);

                    BtnDiagram1.Background = index == 0 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                    BtnDiagram2.Background = index == 1 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                    BtnDiagram3.Background = index == 2 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                }
            }
        }

        private void BtnResetDiagram_Click(object sender, RoutedEventArgs e)
        {
            if (ImgScale != null && ImgTranslate != null)
            {
                if (_isSqlMode || currentLevel.NoUMLAutoScale)
                {
                    _currentScale = 1.0;
                }
                else
                {
                    _currentScale = 0.5;
                }
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
            if (level.AuxiliaryIds != null && level.AuxiliaryIds.Count > 0)
            {
                bool headerAdded = false;
                WrapPanel auxWrapPanel = null;

                foreach (var auxId in level.AuxiliaryIds)
                {
                    if (string.IsNullOrEmpty(auxId)) continue;

                    string auxPath = $"img/aux_{auxId}.svg";
                    var auxImage = LoadDiagramImage(auxPath);

                    if (auxImage != null)
                    {
                        if (!headerAdded)
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
                            auxWrapPanel = new WrapPanel
                            {
                                Orientation = Orientation.Horizontal
                            };
                            PnlMaterials.Children.Add(auxWrapPanel);
                            headerAdded = true;
                        }

                        auxWrapPanel.Children.Add(
                            new Image
                            {
                                Source = auxImage,
                                Height = 150,
                                Stretch = Stretch.Uniform,
                                Margin = new Thickness(0, 0, 15, 15),
                                HorizontalAlignment = HorizontalAlignment.Left
                            }
                        );
                    }
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

                var matWrapPanel = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };
                PnlMaterials.Children.Add(matWrapPanel);

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
                            Margin = new Thickness(0, 0, 15, 15),
                            Child = new Image
                            {
                                Source = img,
                                Height = 180,
                                Stretch = Stretch.Uniform,
                                HorizontalAlignment = HorizontalAlignment.Left
                            }
                        };
                        matWrapPanel.Children.Add(border);
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

                        var btnText = new TextBlock
                        {
                            Text = "▶ " + capturedTitle,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        var btn = new Button
                        {
                            Content = btnText,
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
                            btnText.Text = (isExpanded ? "▶ " : "▼ ") + capturedTitle;
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
                    string lessonTitle = "";
                    string vidBtnText = "";
                    string vidUrl = "";
                    string docUrl = "";
                    string vidTooltip = "";
                    string docTooltip = "";
                    Avalonia.Media.IBrush vidColor = null;

                    if (_isSqlMode)
                    {
                        var sqlLesson = SqlPrerequisiteSystem.GetLesson(reqTitle);
                        if (sqlLesson == null) continue;

                        lessonTitle = sqlLesson.Title;
                        vidBtnText = "Video";
                        vidUrl = sqlLesson.YoutubeUrl;
                        docUrl = sqlLesson.DocsUrl;
                        vidTooltip = "Zu YouTube";
                        docTooltip = "Zu MySQL Docs";
                        vidColor = Avalonia.Media.SolidColorBrush.Parse("#b00000");
                    }
                    else
                    {
                        var lesson = PrerequisiteSystem.GetLesson(reqTitle);
                        if (lesson == null) continue;

                        lessonTitle = lesson.Title;
                        vidBtnText = "Kurs";
                        vidUrl = lesson.DometrainUrl;
                        docUrl = lesson.DocsUrl;
                        vidTooltip = "Zu Dometrain.com";
                        docTooltip = "Zu Microsoft Learn";
                        vidColor = Avalonia.Media.SolidColorBrush.Parse("#5D3FD3");
                    }

                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto"),
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    // title
                    var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

                    var txtTitle = new TextBlock
                    {
                        Text = "• " + lessonTitle,
                        Foreground = Avalonia.Media.Brushes.LightGray,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };
                    titleStack.Children.Add(txtTitle);

                    if (isOptional)
                    {
                        var badge = new Border
                        {
                            Background = Avalonia.Media.SolidColorBrush.Parse("#333333"),
                            BorderBrush = Avalonia.Media.Brushes.Gray,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(4, 1),
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock
                            {
                                Text = "Optional",
                                FontSize = 10,
                                Foreground = Avalonia.Media.Brushes.Gray
                            }
                        };
                        titleStack.Children.Add(badge);
                    }

                    // video/course
                    var btnVid = new Button
                    {
                        Content = vidBtnText,
                        FontSize = 11,
                        Padding = new Thickness(8, 4),
                        Margin = new Thickness(0, 0, 5, 0),
                        Background = vidColor,
                        Foreground = Avalonia.Media.Brushes.White,
                        CornerRadius = new CornerRadius(4),
                        Cursor = Avalonia.Input.Cursor.Parse("Hand"),
                        IsVisible = !string.IsNullOrEmpty(vidUrl)
                    };
                    ToolTip.SetTip(btnVid, vidTooltip);
                    btnVid.Click += (s, e) => {
                        if (_isSqlMode) SqlPrerequisiteSystem.OpenUrl(vidUrl);
                        else PrerequisiteSystem.OpenUrl(vidUrl);
                    };

                    // docs
                    var btnDoc = new Button
                    {
                        Content = "Docs",
                        FontSize = 11,
                        Padding = new Thickness(8, 4),
                        Background = Avalonia.Media.SolidColorBrush.Parse("#0078D4"),
                        Foreground = Avalonia.Media.Brushes.White,
                        CornerRadius = new CornerRadius(4),
                        Cursor = Avalonia.Input.Cursor.Parse("Hand"),
                        IsVisible = !string.IsNullOrEmpty(docUrl)
                    };
                    ToolTip.SetTip(btnDoc, docTooltip);
                    btnDoc.Click += (s, e) => {
                        if (_isSqlMode) SqlPrerequisiteSystem.OpenUrl(docUrl);
                        else PrerequisiteSystem.OpenUrl(docUrl);
                    };

                    Grid.SetColumn(titleStack, 0);
                    Grid.SetColumn(btnVid, 1);
                    Grid.SetColumn(btnDoc, 2);

                    row.Children.Add(titleStack);
                    row.Children.Add(btnVid);
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
                    var cleanedSchema = _currentSqlDraft.ExpectedSchema.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
                    int validCols = cleanedSchema.Count;

                    var cleanedResult = new List<string[]>();
                    foreach (var r in _currentSqlDraft.ExpectedResult)
                    {
                        var rowData = r.Take(validCols).Select(c => c ?? "").ToArray();
                        if (rowData.Any(c => !string.IsNullOrWhiteSpace(c)))
                        {
                            cleanedResult.Add(rowData);
                        }
                    }

                    if (validCols == 0) throw new Exception("Die Erwartungstabelle (Expected Table) darf nicht komplett leer sein.");

                    // run verification query depending on mode
                    DataTable actualDt = null;
                    string sampleSolution = SqlLevelTester.ConvertMysqlToSqlite(connection, _currentSqlDraft.SampleSolution);

                    if (_currentSqlDraft.IsDmlMode)
                    {
                        using (var dmlCmd = connection.CreateCommand())
                        {
                            dmlCmd.CommandText = sampleSolution;
                            dmlCmd.ExecuteNonQuery();
                        }

                        if (string.IsNullOrWhiteSpace(_currentSqlDraft.VerificationQuery))
                            throw new Exception("Im DML Modus muss eine Verifizierungs-Abfrage angegeben werden.");

                        string verifyQuery = SqlLevelTester.ConvertMysqlToSqlite(connection, _currentSqlDraft.VerificationQuery);
                        actualDt = ExecuteDbQuery(connection, verifyQuery);
                    }
                    else
                    {
                        actualDt = ExecuteDbQuery(connection, sampleSolution);
                    }

                    // always output table
                    if (actualDt != null)
                    {
                        AddSqlTable(actualDt);
                    }

                    // compare columns (count and name)
                    if (actualDt.Columns.Count != validCols)
                        throw new Exception($"Spaltenanzahl stimmt nicht überein. Erwartet: {validCols}, Ist: {actualDt.Columns.Count}");

                    for (int i = 0; i < validCols; i++)
                    {
                        if (!actualDt.Columns[i].ColumnName.Equals(cleanedSchema[i].Name, StringComparison.OrdinalIgnoreCase))
                            throw new Exception($"Spaltenname an Position {i + 1} stimmt nicht. Erwartet: '{cleanedSchema[i].Name}', Ist: '{actualDt.Columns[i].ColumnName}'");
                    }

                    // compare row count
                    if (actualDt.Rows.Count != cleanedResult.Count)
                        throw new Exception($"Zeilenanzahl stimmt nicht überein. Erwartet: {cleanedResult.Count}, Ist: {actualDt.Rows.Count}");

                    // deep compare values
                    for (int r = 0; r < cleanedResult.Count; r++)
                    {
                        for (int c = 0; c < validCols; c++)
                        {
                            string expectedVal = cleanedResult[r][c] ?? "";
                            if (expectedVal == "") expectedVal = "NULL"; // map empty cell to "NULL"

                            string actualVal = actualDt.Rows[r][c]?.ToString()?.Replace(",", ".") ?? "";
                            if (actualDt.Rows[r][c] == DBNull.Value || string.IsNullOrEmpty(actualVal)) actualVal = "NULL";

                            if (double.TryParse(expectedVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double expNum) &&
                                double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double actNum))
                            {
                                if (Math.Abs(expNum - actNum) > 0.0001)
                                    throw new Exception($"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                            }
                            else if (!expectedVal.Equals(actualVal, StringComparison.OrdinalIgnoreCase))
                            {
                                throw new Exception($"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                            }
                        }
                    }

                    // success (expected matched sample solution identically)
                    string json = JsonSerializer.Serialize(_currentSqlDraft);
                    _verifiedSqlDraftState = JsonSerializer.Deserialize<SqlLevelDraft>(json);
                    _verifiedExpectedSchema = cleanedSchema;
                    _verifiedExpectedResult = cleanedResult;

                    BtnDesignerExport.IsEnabled = true;
                    TxtDesignerStatus.Text = "Bereit zum Export";
                    AddSqlOutput("System", "✓ DESIGNER TEST BESTANDEN! Die Musterlösung erzeugt exakt das erwartete Ergebnis.", Brushes.LightGreen);

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

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_isSqlMode)
            {
                if (_isDesignerMode)
                {
                    UpdateDraftFromUI();
                    RunSqlDesignerTest();
                    return;
                }
                RunSqlQuery();
                return;
            }

            if (_compilationCts != null)
            {
                _compilationCts.Cancel();
                AddToConsole("\n> Vorgang wird abgebrochen...", Brushes.LightGray);
                BtnRun.IsEnabled = false;
                return;
            }

            _compilationCts = new System.Threading.CancellationTokenSource();
            BtnRun.Content = "■ ABBRECHEN";
            BtnRun.Width = 135;
            BtnRun.Background = SolidColorBrush.Parse("#B43232");
            ToolTip.SetTip(BtnRun, "Ausführung stoppen");

            TxtConsole.Inlines?.Clear();

            if (!_hasRunOnce)
            {
                AddToConsole("> Compiler wird gestartet...\n", Brushes.LightGray);
                _hasRunOnce = true;
            }

            AddToConsole("> Kompiliere...\n", Brushes.LightGray);
            SaveCurrentProgress();

            string codeText = CodeEditor.Text;
            var levelContext = currentLevel;
            var token = _compilationCts.Token;
            CodeGuard.Token = token;

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

            int headerLineCount = 4;
            if (!runDesignerTest && levelContext != null && levelContext.Id == 26)
            {
                headerLineCount = 5;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var processingTask = System.Threading.Tasks.Task.Run<(bool Success, System.Collections.Immutable.ImmutableArray<Diagnostic>? Diagnostics, dynamic TestResult)>(() =>
                {
                    if (token.IsCancellationRequested) return (false, null, null);

                    var originalConsoleOut = Console.Out;
                    var customWriter = new ConsoleRedirectionWriter((str) =>
                    {
                        AddToConsole(str, Brushes.Cyan);
                    });
                    Console.SetOut(customWriter);

                    string header = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n";
                    if (!runDesignerTest && levelContext != null && levelContext.Id == 26)
                    {
                        header += "public partial class FlughafenVerwaltung {\n";
                    }

                    string fullCode = header + codeText;
                    if (!runDesignerTest && levelContext != null && levelContext.Id == 26)
                    {
                        fullCode += "\n}";
                    }

                    var syntaxTree = CSharpSyntaxTree.ParseText(fullCode, cancellationToken: token);

                    // inject global loop breaker to catch infinite loops and avoid frying any cpus
                    var root = syntaxTree.GetRoot(token);
                    var rewriter = new LoopGuardRewriter();
                    syntaxTree = rewriter.Visit(root).SyntaxTree;

                    var trees = new List<SyntaxTree> { syntaxTree };

                    // handle auxiliary code
                    if (!runDesignerTest && !_isCustomLevelMode && levelContext.AuxiliaryIds != null)
                    {
                        foreach (var auxId in levelContext.AuxiliaryIds)
                        {
                            string auxCode = AuxiliaryImplementations.GetCode(auxId, codeText);
                            if (!string.IsNullOrEmpty(auxCode))
                            {
                                trees.Add(CSharpSyntaxTree.ParseText(auxCode, cancellationToken: token));
                            }
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
                                var testResult = LevelTester.Run(levelContext.Id, assembly, codeText);
                                return (Success: true, Diagnostics: (System.Collections.Immutable.ImmutableArray<Diagnostic>?)null, TestResult: (dynamic)testResult);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return (false, null, null);
                        }
                    }
                }, token);

                var timeoutTask = System.Threading.Tasks.Task.Delay(12000, token);
                var completedTask = await System.Threading.Tasks.Task.WhenAny(processingTask, timeoutTask);

                if (token.IsCancellationRequested)
                {
                    AddToConsole("\n⚠ Abbruch durch Benutzer.", Brushes.Orange);
                }
                else if (completedTask == timeoutTask)
                {
                    _compilationCts.Cancel();
                    stopwatch.Stop();
                    AddToConsole("\n❌ TIMEOUT: Das Programm hat das Zeitlimit von 12 Sekunden überschritten.", Brushes.Red);
                }
                else
                {
                    var result = await processingTask;
                    stopwatch.Stop();

                    if (!result.Success && result.Diagnostics != null)
                    {
                        AddToConsole("KOMPILIERFEHLER:\n", Brushes.Red);
                        foreach (var diag in result.Diagnostics.Value.Where(d => d.Severity == DiagnosticSeverity.Error))
                        {
                            var lineSpan = diag.Location.GetLineSpan();
                            int userLine = lineSpan.StartLinePosition.Line - (headerLineCount - 1);
                            if (userLine < 0) userLine = 0;
                            AddToConsole($"Zeile {userLine}: {diag.GetMessage()}\n", Brushes.Red);
                        }
                    }
                    else if (result.Success)
                    {
                        if (runDesignerTest)
                        {
                            // handle designer result
                            if (result.TestResult.Success)
                            {
                                AddToConsole($"✓ DESIGNER TEST BESTANDEN: " + result.TestResult.Feedback, Brushes.LightGreen);
                                BtnDesignerExport.IsEnabled = true;
                                _verifiedDraftState = pendingSnapshot;
                                TxtDesignerStatus.Text = "Bereit zum Export";
                            }
                            else
                            {
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

                                AddToConsole($"❌ VALIDIERUNG FEHLGESCHLAGEN:\n{msg}", Brushes.Orange);
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
                AddToConsole("\n⚠ Abbruch durch Benutzer.", Brushes.Orange);
            }
            catch (Exception ex)
            {
                AddToConsole($"\nSystem Fehler: {ex.Message}", Brushes.Red);
            }
            finally
            {
                _compilationCts?.Cancel();
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

        private class LoopGuardRewriter : CSharpSyntaxRewriter
        {
            private StatementSyntax GetCheckStatement()
            {
                return SyntaxFactory.ParseStatement("AbiturEliteCode.CodeGuard.Check();\n");
            }

            private BlockSyntax EnsureBlock(StatementSyntax statement)
            {
                if (statement is BlockSyntax block)
                {
                    return block.WithStatements(block.Statements.Insert(0, GetCheckStatement()));
                }
                return SyntaxFactory.Block(GetCheckStatement(), statement);
            }

            public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
            {
                var visitedNode = (WhileStatementSyntax)base.VisitWhileStatement(node);

                var method = visitedNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (method != null && (method.Identifier.Text.Equals("Run", StringComparison.OrdinalIgnoreCase) ||
                                       method.Identifier.Text.Equals("RunServer", StringComparison.OrdinalIgnoreCase)))
                {
                    // only inject return if it is explicitly an infinite loop
                    if (visitedNode.Condition is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.TrueLiteralExpression))
                    {
                        var returnStatement = SyntaxFactory.ParseStatement("return;\n");
                        var block = visitedNode.Statement is BlockSyntax b ? b : SyntaxFactory.Block(visitedNode.Statement);
                        block = block.AddStatements(returnStatement);
                        return visitedNode.WithStatement(EnsureBlock(block));
                    }
                }
                return visitedNode.WithStatement(EnsureBlock(visitedNode.Statement));
            }

            public override SyntaxNode VisitForStatement(ForStatementSyntax node)
            {
                var visitedNode = (ForStatementSyntax)base.VisitForStatement(node);

                var method = visitedNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (method != null && (method.Identifier.Text.Equals("Run", StringComparison.OrdinalIgnoreCase) ||
                                       method.Identifier.Text.Equals("RunServer", StringComparison.OrdinalIgnoreCase)))
                {
                    // only inject return if it is an infinite for loop
                    if (visitedNode.Condition == null)
                    {
                        var returnStatement = SyntaxFactory.ParseStatement("return;\n");
                        var block = visitedNode.Statement is BlockSyntax b ? b : SyntaxFactory.Block(visitedNode.Statement);
                        block = block.AddStatements(returnStatement);
                        return visitedNode.WithStatement(EnsureBlock(block));
                    }
                }
                return visitedNode.WithStatement(EnsureBlock(visitedNode.Statement));
            }

            public override SyntaxNode VisitDoStatement(DoStatementSyntax node)
            {
                var visitedNode = (DoStatementSyntax)base.VisitDoStatement(node);
                return visitedNode.WithStatement(EnsureBlock(visitedNode.Statement));
            }
        }

        private void ProcessTestResult(Level levelContext, dynamic result, TimeSpan duration)
        {
            if (result.Success)
            {
                AddToConsole($"✓ TEST BESTANDEN ({duration.TotalSeconds:F2}s): " + result.Feedback + "\n\n", Brushes.LightGreen);

                if (_isCustomLevelMode)
                {
                    if (!customPlayerData.CompletedCustomLevels.Contains(levelContext.Title))
                    {
                        customPlayerData.CompletedCustomLevels.Add(levelContext.Title);
                        SaveSystem.SaveCustom(customPlayerData);
                    }

                    AddToConsole("🎉 Custom Level erfolgreich abgeschlossen!", Brushes.LightGreen);

                    UpdateNavigationButtons();
                    if (_nextCustomLevelPath != "SECTION_COMPLETE" && !string.IsNullOrEmpty(_nextCustomLevelPath))
                    {
                        AddToConsole($"\n> Nächstes Level verfügbar.", Brushes.LightGray);
                    }
                    else if (_nextCustomLevelPath == "SECTION_COMPLETE")
                    {
                        BtnNextLevel.Content = "SEKTION ABSCHLIESSEN ✓";
                        BtnNextLevel.IsVisible = true;
                    }

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
                        AddToConsole($"🔓 Level {nextLvl.Id} freigeschaltet!\n", Brushes.LightGreen);
                    }

                    AddToConsole($"Nächstes Level Code: {nextLvl.SkipCode}\n", Brushes.LightGray);

                    // check if we are switching sections
                    if (nextLvl.Section != levelContext.Section)
                    {
                        AddToConsole("\n🎉 Sektion abgeschlossen! Bereit für das nächste Thema?", Brushes.LightGreen);
                        BtnNextLevel.Content = "NÄCHSTE SEKTION →";
                    }
                    else
                    {
                        BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                    }

                    BtnNextLevel.IsEnabled = true;
                }
                else
                {
                    // no next level -> course completed
                    AddToConsole("\n🎉 Herzlichen Glückwunsch! Du hast alle Levels gemeistert.", Brushes.LightGreen);
                    BtnNextLevel.Content = "KURS ABSCHLIESSEN ✓";
                    BtnNextLevel.IsEnabled = true;
                    BtnNextLevel.Opacity = 1.0;
                }

                SaveSystem.Save(playerData);
            }
            else
            {
                string msg = result.Error != null
                        ? (result.Error.InnerException != null ? result.Error.InnerException.Message : result.Error.Message)
                        : "Unbekannter Fehler";
                AddToConsole("❌ LAUFZEITFEHLER / LOGIK:\n" + msg, Brushes.Orange);
            }
        }

        private void UpdateNavigationButtons()
        {
            if (_isDesignerMode)
            {
                BtnPrevLevel.IsVisible = false;
                BtnNextLevel.IsVisible = false;
                return;
            }

            if (_isCustomLevelMode)
            {
                BtnPrevLevel.IsVisible = true;
                BtnNextLevel.IsVisible = true;

                // get relevant custom levels for current mode, excluding drafts
                var allCustoms = GetCustomLevels().Where(c => !c.IsDraft).ToList();
                string currentTitle = _isSqlMode ? currentSqlLevel?.Title : currentLevel?.Title;
                var currentInfo = allCustoms.FirstOrDefault(c => c.Name == currentTitle);

                if (currentInfo != null)
                {
                    // group by section and order alphabetically
                    var sectionLevels = allCustoms.Where(c => c.Section == currentInfo.Section).OrderBy(c => c.Name).ToList();
                    int idx = sectionLevels.FindIndex(c => c.FilePath == currentInfo.FilePath);

                    bool isFirst1 = idx <= 0;
                    bool isLast1 = idx >= sectionLevels.Count - 1;

                    BtnPrevLevel.IsEnabled = !isFirst1;
                    BtnPrevLevel.Opacity = isFirst1 ? 0.5 : 1.0;

                    if (isLast1)
                    {
                        BtnNextLevel.Content = "✓";
                        BtnNextLevel.IsEnabled = true;
                        _nextCustomLevelPath = "SECTION_COMPLETE";
                    }
                    else
                    {
                        BtnNextLevel.Content = "→";
                        BtnNextLevel.IsEnabled = true;
                        _nextCustomLevelPath = sectionLevels[idx + 1].FilePath;
                    }
                }
                return;
            }

            BtnPrevLevel.IsVisible = true;
            BtnNextLevel.IsVisible = true;

            bool isFirst = false;
            bool isLast = false;
            bool nextIsUnlocked = false;
            bool isCurrentCompleted = false;

            if (_isCustomLevelMode)
            {
                BtnPrevLevel.IsEnabled = false;
                BtnNextLevel.Content = "→";
                return;
            }

            if (_isSqlMode && currentSqlLevel != null)
            {
                int idx = sqlLevels.IndexOf(currentSqlLevel);
                isFirst = idx <= 0;
                isLast = idx >= sqlLevels.Count - 1;
                isCurrentCompleted = playerData.CompletedSqlLevelIds.Contains(currentSqlLevel.Id);

                // check if next level exists and is unlocked
                if (!isLast)
                {
                    var next = sqlLevels[idx + 1];
                    nextIsUnlocked = playerData.UnlockedSqlLevelIds.Contains(next.Id);
                }
            }
            else if (currentLevel != null)
            {
                int idx = levels.IndexOf(currentLevel);
                isFirst = idx <= 0;
                isLast = idx >= levels.Count - 1;
                isCurrentCompleted = playerData.CompletedLevelIds.Contains(currentLevel.Id);

                if (!isLast)
                {
                    var next = levels[idx + 1];
                    nextIsUnlocked = playerData.UnlockedLevelIds.Contains(next.Id);
                }
            }

            BtnPrevLevel.IsEnabled = !isFirst;
            BtnPrevLevel.Opacity = isFirst ? 0.5 : 1.0;

            if (isLast)
            {
                BtnNextLevel.Content = "✓";
                BtnNextLevel.IsEnabled = isCurrentCompleted;
            }
            else
            {
                BtnNextLevel.Content = "→";
                BtnNextLevel.IsEnabled = nextIsUnlocked;
            }
        }

        private void BtnPrevLevel_Click(object sender, RoutedEventArgs e)
        {
            if (_isCustomLevelMode)
            {
                var allCustoms = GetCustomLevels().Where(c => !c.IsDraft).ToList();
                string currentTitle = _isSqlMode ? currentSqlLevel?.Title : currentLevel?.Title;
                var currentInfo = allCustoms.FirstOrDefault(c => c.Name == currentTitle);

                if (currentInfo != null)
                {
                    var sectionLevels = allCustoms.Where(c => c.Section == currentInfo.Section).OrderBy(c => c.Name).ToList();
                    int idx = sectionLevels.FindIndex(c => c.FilePath == currentInfo.FilePath);
                    if (idx > 0)
                    {
                        LoadCustomLevelFromFile(sectionLevels[idx - 1].FilePath);
                        if (_isSqlMode) SqlQueryEditor.Focus(); else CodeEditor.Focus();
                    }
                }
                return;
            }

            if (_isSqlMode && currentSqlLevel != null)
            {
                int idx = sqlLevels.IndexOf(currentSqlLevel);
                if (idx > 0)
                {
                    LoadSqlLevel(sqlLevels[idx - 1]);
                }
            }
            else if (currentLevel != null)
            {
                int idx = levels.IndexOf(currentLevel);
                if (idx > 0)
                {
                    LoadLevel(levels[idx - 1]);
                }
            }
        }

        private void BtnNextLevel_Click(object sender, RoutedEventArgs e)
        {
            if (BtnNextLevel.Content?.ToString() == "✓" || BtnNextLevel.Content?.ToString()?.Contains("ABSCHLIESSEN") == true)
            {
                if (_isCustomLevelMode && _nextCustomLevelPath == "SECTION_COMPLETE")
                {
                    ShowCustomSectionCompletedDialog();
                }
                else if (_isSqlMode)
                {
                    ShowSqlCourseCompletedDialog();
                }
                else
                {
                    ShowCourseCompletedDialog();
                }
                return;
            }

            if (_isCustomLevelMode && !string.IsNullOrEmpty(_nextCustomLevelPath))
            {
                try
                {
                    LoadCustomLevelFromFile(_nextCustomLevelPath);
                    if (_isSqlMode) SqlQueryEditor.Focus(); else CodeEditor.Focus();
                }
                catch (Exception ex)
                {
                    if (_isSqlMode) AddSqlOutput("Error", $"> Fehler beim Laden des nächsten Levels: {ex.Message}", Brushes.Red);
                    else AddToConsole($"\n> Fehler beim Laden des nächsten Levels: {ex.Message}", Brushes.Red);
                    BtnNextLevel.IsVisible = false;
                }
                return;
            }

            if (_isSqlMode)
            {
                var nextSqlLvl = sqlLevels.FirstOrDefault(l => l.SkipCode == currentSqlLevel.NextLevelCode);
                if (nextSqlLvl != null)
                    LoadSqlLevel(nextSqlLvl);
                SqlQueryEditor.Focus();
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
                Margin = new Thickness(0, 20, 0, 0)
            };
            btnClose.Click += (_, __) => dialog.Close();

            Grid.SetRow(btnClose, 1);
            rootGrid.Children.Add(btnClose);

            rootBorder.Child = rootGrid;
            dialog.Content = rootBorder;

            await dialog.ShowDialog(this);
        }

        private async void ShowSqlCourseCompletedDialog()
        {
            var dialog = new Window
            {
                Title = "SQL Kurs Abgeschlossen",
                Width = 500,
                Height = 350,
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
                    Foreground = SolidColorBrush.Parse("#FFD700"),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            );
            contentStack.Children.Add(
                new TextBlock
                {
                    Text =
                        "Du hast alle SQL-Levels erfolgreich abgeschlossen!\n\nDatenbank-Abfragen sind ein essenzieller Teil der Prüfung. Du bist nun bestens vorbereitet.",
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
                Background = SolidColorBrush.Parse("#FFD700"),
                Foreground = Brushes.Black,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Padding = new Thickness(30, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 20, 0, 0)
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
            if (_isSqlMode)
            {
                // using document replace so that the action is added to the undo stack (instead of being fully cleared)
                SqlQueryEditor.Document.Replace(0, SqlQueryEditor.Document.TextLength, "");
                AddSqlOutput("System", "> Query Editor geleert.", Brushes.LightGray);
                SqlQueryEditor.Focus();
                return;
            }

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
                AddToConsole("\n> Code auf Standard zurückgesetzt.", Brushes.LightGray);
            }
            CodeEditor.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentProgress();

            if (_isSqlMode)
            {
                AddSqlOutput("System", "> Query gespeichert.", Brushes.LightGray);
                SqlQueryEditor.Focus();
            }
            else
            {
                AddToConsole("\n> Gespeichert.", Brushes.LightGray);
                CodeEditor.Focus();
            }
        }

        private void BtnModeSwitch_Click(object sender, RoutedEventArgs e)
        {
            _isSqlMode = !_isSqlMode;

            BtnNextLevel.IsVisible = false;

            var rightGrid = this.FindControl<Grid>("RootGrid").Children
                .OfType<Grid>().FirstOrDefault(g => g.ColumnDefinitions.Count == 3)
                ?.Children.OfType<Grid>().FirstOrDefault(g => g.GetValue(Grid.ColumnProperty) == 2);

            RowDefinition bottomRow = null;
            if (rightGrid != null && rightGrid.RowDefinitions.Count > 3)
                bottomRow = rightGrid.RowDefinitions[3];

            if (_isSqlMode)
            {
                PnlTaskRelationalModel.IsVisible = true;
                UmlRelationalSplitter.IsVisible = true;
                UmlRelationalBorder.IsVisible = true;

                if (bottomRow != null)
                {
                    _lastCsharpRowHeight = bottomRow.Height;
                    bottomRow.Height = _lastSqlRowHeight;
                }

                BtnModeSwitch.Content = "MODUS: SQL";
                BtnModeSwitch.Foreground = SolidColorBrush.Parse("#FFD700");
                BtnModeSwitch.BorderBrush = SolidColorBrush.Parse("#FFD700");

                PnlCsharpEditor.IsVisible = false;
                PnlSqlOutputContainer.IsVisible = true;

                PnlCsharpConsole.IsVisible = false;
                PnlSqlQueryEditor.IsVisible = true;

                var oldSqlPanel = this.FindControl<Grid>("PnlSqlEditor");
                if (oldSqlPanel != null) oldSqlPanel.IsVisible = false;

                BtnSave.IsVisible = true;
                BtnReset.IsVisible = true;
                ToolTip.SetTip(BtnReset, "Query leeren (Undo möglich)");
                UpdateShortcutsAndTooltips();

                ApplySqlSyntaxHighlighting();

                if (sqlLevels == null) sqlLevels = SqlCurriculum.GetLevels();
                int maxId = playerData.UnlockedSqlLevelIds.Count > 0 ? playerData.UnlockedSqlLevelIds.Max() : 1;
                var startLevel = sqlLevels.FirstOrDefault(l => l.Id == maxId) ?? sqlLevels[0];
                LoadSqlLevel(startLevel);
                UpdateVimState();
            }
            else
            {
                PnlTaskRelationalModel.IsVisible = false;
                UmlRelationalSplitter.IsVisible = false;
                UmlRelationalBorder.IsVisible = false;

                if (UmlTabGrid != null && UmlTabGrid.RowDefinitions.Count > 2)
                {
                    UmlTabGrid.RowDefinitions[2].Height = GridLength.Auto;
                }

                if (bottomRow != null)
                {
                    _lastSqlRowHeight = bottomRow.Height;
                    bottomRow.Height = _lastCsharpRowHeight;
                }

                BtnModeSwitch.Content = "MODUS: C#";
                BtnModeSwitch.Foreground = SolidColorBrush.Parse("#6495ED");
                BtnModeSwitch.BorderBrush = SolidColorBrush.Parse("#6495ED");

                PnlCsharpEditor.IsVisible = true;
                PnlSqlOutputContainer.IsVisible = false;

                PnlCsharpConsole.IsVisible = true;
                PnlSqlQueryEditor.IsVisible = false;

                BtnSave.IsVisible = true;
                BtnReset.IsVisible = true;
                ToolTip.SetTip(BtnReset, "Code zurücksetzen");
                UpdateShortcutsAndTooltips();

                ApplySyntaxHighlighting();

                LoadLevel(currentLevel);
                UpdateVimState();
            }
        }

        private void BtnLevelSelect_Click(object sender, RoutedEventArgs e)
        {
            if (_isSqlMode && sqlLevels == null)
            {
                sqlLevels = SqlCurriculum.GetLevels();
            }
            levels ??= Curriculum.GetLevels();

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

            // header
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                Margin = new Thickness(0, 0, 0, 15)
            };

            // title
            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txtTitle = new TextBlock
            {
                Text = _isSqlMode ? "SQL Levels" : "C# Levels",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(txtTitle);

            // badge level counter
            var countBadge = new Border
            {
                Background = SolidColorBrush.Parse("#2D2D30"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 5),
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = true,
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

            // search / code input container
            var searchContainer = new Border
            {
                Margin = new Thickness(15, 0)
            };
            Grid.SetColumn(searchContainer, 1);
            headerGrid.Children.Add(searchContainer);

            // right header panel
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

                // footer buttons
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

                // toggle custom levels button
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
                    // title and badge
                    if (_isSqlMode)
                    {
                        txtTitle.Text = "SQL Levels";
                        int completedCount = sqlLevels.Count(l => playerData.CompletedSqlLevelIds.Contains(l.Id));
                        ((TextBlock)countBadge.Child).Text = $"{completedCount}/{sqlLevels.Count}";
                    }
                    else
                    {
                        txtTitle.Text = "C# Levels";
                        int completedCount = levels.Count(l => playerData.CompletedLevelIds.Contains(l.Id));
                        ((TextBlock)countBadge.Child).Text = $"{completedCount}/{levels.Count}";
                    }
                    countBadge.IsVisible = true;

                    // code input field
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

                    // code input logic
                    txtLevelCode.TextChanged += (s, ev) =>
                    {
                        if (txtLevelCode.Text?.Length == 3)
                        {
                            string code = txtLevelCode.Text.ToUpper();
                            if (_isSqlMode)
                            {
                                var lvl = sqlLevels.FirstOrDefault(l => l.SkipCode == code);
                                if (lvl != null) { LoadSqlLevel(lvl); win.Close(); }
                            }
                            else
                            {
                                var lvl = levels.FirstOrDefault(l => l.SkipCode == code);
                                if (lvl != null) { LoadLevel(lvl); win.Close(); }
                            }
                        }
                    };
                    codePanel.Children.Add(txtLevelCode);

                    var btnLevelGuide = new Button
                    {
                        Content = LoadIcon("assets/icons/ic_guide.svg", 16),
                        Background = SolidColorBrush.Parse("#007ACC"),
                        Padding = new Thickness(8),
                        CornerRadius = new CornerRadius(4)
                    };
                    ToolTip.SetTip(btnLevelGuide, "Level Codes & Lösungen");
                    btnLevelGuide.Click += (_, __) =>
                    {
                        try
                        {
                            var url = $"https://github.com/OnlyCook/abitur-elite-code/blob/main/py/LEVEL_CODES.md{(_isSqlMode ? "#sql-levels" : "")}";
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
                            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
                        }
                        catch { }
                    };
                    codePanel.Children.Add(btnLevelGuide);

                    headerRightPanel.Children.Add(codePanel);


                    // level list
                    var levelStack = new StackPanel { Spacing = 8 };

                    if (_isSqlMode)
                    {
                        // sql levels
                        var groups = sqlLevels.GroupBy(l => l.Section);
                        foreach (var group in groups)
                        {
                            bool isSectionComplete = group.All(l => playerData.CompletedSqlLevelIds.Contains(l.Id));

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
                                bool unlocked = playerData.UnlockedSqlLevelIds.Contains(lvl.Id);
                                bool completed = playerData.CompletedSqlLevelIds.Contains(lvl.Id);

                                var btnContent = new StackPanel
                                {
                                    Orientation = Orientation.Horizontal,
                                    Spacing = 10
                                };
                                string iconPath = completed ? "assets/icons/ic_check.svg" : (unlocked ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_lock.svg");
                                btnContent.Children.Add(LoadIcon(iconPath, 16));
                                btnContent.Children.Add(new TextBlock
                                {
                                    Text = $"S{lvl.Id}. {lvl.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)}",
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
                                btn.Click += (_, __) =>
                                {
                                    LoadSqlLevel(lvl);
                                    win.Close();
                                };
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
                    }
                    else
                    {
                        // c# levels
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
                                btn.Click += (_, __) =>
                                {
                                    LoadLevel(lvl);
                                    win.Close();
                                };
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
                    }

                    contentScroll.Content = levelStack;
                }
                else
                {
                    // custom levels (for now only c#)
                    txtTitle.Text = "Eigene Levels";
                    countBadge.IsVisible = false;

                    var customStack = new StackPanel { Spacing = 5 };
                    var customLevels = GetCustomLevels();
                    var rootLevels = customLevels.Where(x => x.Section == "Einzelne Levels").OrderBy(x => x.Name).ToList();
                    var folderGroups = customLevels.Where(x => x.Section != "Einzelne Levels").GroupBy(x => x.Section).OrderBy(g => g.Key).ToList();

                    // search Box
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

                    // search logic
                    txtSearch.TextChanged += (s, e) => {
                        string query = txtSearch.Text?.ToLower() ?? "";
                        foreach (var child in customStack.Children)
                        {
                            if (child is Expander exp && exp.Content is StackPanel groupPanel)
                            {
                                bool groupHasMatch = false;
                                foreach (var item in groupPanel.Children)
                                {
                                    if (item is Grid row && row.Tag is CustomLevelInfo info)
                                    {
                                        bool match = info.Name.ToLower().Contains(query) || info.Author.ToLower().Contains(query);
                                        row.IsVisible = match;
                                        if (match) groupHasMatch = true;
                                    }
                                }
                                exp.IsVisible = groupHasMatch;
                                if (!string.IsNullOrEmpty(query)) exp.IsExpanded = true; else exp.IsExpanded = false;
                            }
                            else if (child is Grid row && row.Tag is CustomLevelInfo info)
                            {
                                bool match = info.Name.ToLower().Contains(query) || info.Author.ToLower().Contains(query);
                                row.IsVisible = match;
                            }
                        }
                    };
                    searchContainer.Child = txtSearch;

                    // custom level header buttons
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
                        string newPath = await ShowAddLevelDialog(win);
                        if (!string.IsNullOrEmpty(newPath))
                        {
                            _newlyCreatedLevelPath = newPath;
                            RefreshUI();
                        }
                    };
                    headerRightPanel.Children.Add(btnAdd);

                    if (!customLevels.Any())
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
                        Grid CreateLevelRow(CustomLevelInfo cl)
                        {
                            var rowGrid = new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                                Tag = cl,
                                Margin = new Thickness(0, 0, 0, 5),
                                HorizontalAlignment = HorizontalAlignment.Stretch
                            };

                            string iconPath;
                            if (cl.IsDraft) iconPath = "assets/icons/ic_lock.svg";
                            else if (_isSqlMode ? customPlayerData.CompletedCustomSqlLevels.Contains(cl.Name) : customPlayerData.CompletedCustomLevels.Contains(cl.Name)) iconPath = "assets/icons/ic_check.svg";
                            else iconPath = "assets/icons/ic_lock_open.svg";

                            var iconImage = LoadIcon(iconPath, 16);
                            iconImage.Margin = new Thickness(0, 0, 10, 0);
                            iconImage.VerticalAlignment = VerticalAlignment.Center;

                            var btnContentGrid = new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto, *")
                            };
                            btnContentGrid.Children.Add(iconImage);

                            var textStack = new StackPanel { Spacing = 2 };
                            Grid.SetColumn(textStack, 1);

                            string displayName = cl.Name;
                            if (_isSqlMode && AppSettings.IsSqlAntiSpoilerEnabled && cl.Section != null && !cl.Section.StartsWith("Sektion 7"))
                            {
                                displayName = Regex.Replace(cl.Name, @"\s*\(.*?\)", "").Trim();
                            }

                            textStack.Children.Add(new TextBlock
                            {
                                Text = displayName + (cl.IsDraft ? " (Entwurf)" : ""),
                                Foreground = cl.IsDraft ? Brushes.Orange : Brushes.White,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            });
                            textStack.Children.Add(new TextBlock
                            {
                                Text = "von " + cl.Author,
                                FontSize = 11,
                                Foreground = Brushes.Gray,
                                TextTrimming = TextTrimming.CharacterEllipsis
                            });
                            btnContentGrid.Children.Add(textStack);

                            var btnMain = new Button
                            {
                                Content = btnContentGrid,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                Background = (cl.FilePath == _newlyCreatedLevelPath) ? SolidColorBrush.Parse("#2E8B57") : SolidColorBrush.Parse("#313133"),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(10),
                                Cursor = cl.IsDraft ? Cursor.Default : Cursor.Parse("Hand")
                            };

                            // remove highlight after delay
                            if (cl.FilePath == _newlyCreatedLevelPath)
                            {
                                var timer = new DispatcherTimer
                                {
                                    Interval = TimeSpan.FromSeconds(2)
                                };
                                timer.Tick += (s, args) =>
                                {
                                    btnMain.Background = SolidColorBrush.Parse("#313133");
                                    _newlyCreatedLevelPath = null;
                                    timer.Stop();
                                };
                                timer.Start();
                            }

                            btnMain.Click += (_, __) =>
                            {
                                if (!cl.IsDraft) { LoadCustomLevelFromFile(cl.FilePath); win.Close(); }
                            };

                            Grid.SetColumnSpan(btnMain, 3);
                            rowGrid.Children.Add(btnMain);

                            // action buttons
                            var actionPanel = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 5,
                                Margin = new Thickness(0, 0, 10, 0),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                Background = Brushes.Transparent
                            };
                            Grid.SetColumn(actionPanel, 2);

                            if (cl.IsDraft && cl.QuickGenerate)
                            {
                                var btnQuickExport = new Button
                                {
                                    Content = LoadIcon("assets/icons/ic_generate.svg", 16),
                                    Background = Brushes.Transparent,
                                    Padding = new Thickness(8),
                                    Tag = "idle"
                                };
                                ToolTip.SetTip(btnQuickExport, "Quick Export (Automatisch)");

                                btnQuickExport.Click += async (s, e) =>
                                {
                                    if (btnQuickExport.Tag.ToString() != "idle") return;
                                    btnQuickExport.Tag = "pending";
                                    btnQuickExport.Content = LoadIcon("assets/icons/ic_pending.svg", 16);
                                    btnQuickExport.IsEnabled = false;

                                    var cts = new System.Threading.CancellationTokenSource();
                                    EventHandler<WindowClosingEventArgs> closingHandler = (sender, args) => cts.Cancel();
                                    win.Closing += closingHandler;

                                    try
                                    {
                                        if (_isSqlMode)
                                        {
                                            AddSqlOutput("System", $"> Quick Export gestartet für: {cl.Name}...", Brushes.LightGray);
                                            var draft = SqlLevelDesigner.LoadDraft(cl.FilePath);

                                            var validData = await System.Threading.Tasks.Task.Run<(bool Success, List<SqlExpectedColumn> Schema, List<string[]> Result)>(() =>
                                            {
                                                try
                                                {
                                                    using (var connection = new SqliteConnection("Data Source=:memory:"))
                                                    {
                                                        connection.Open();

                                                        // run setup code
                                                        using (var setupCmd = connection.CreateCommand())
                                                        {
                                                            setupCmd.CommandText = draft.SetupScript;
                                                            setupCmd.ExecuteNonQuery();
                                                        }

                                                        // exclude empty input buffers
                                                        var cleanedSchema = draft.ExpectedSchema.Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();
                                                        int validCols = cleanedSchema.Count;

                                                        var cleanedResult = new List<string[]>();
                                                        foreach (var r in draft.ExpectedResult)
                                                        {
                                                            var rowData = r.Take(validCols).Select(c => c ?? "").ToArray();
                                                            if (rowData.Any(c => !string.IsNullOrWhiteSpace(c)))
                                                            {
                                                                cleanedResult.Add(rowData);
                                                            }
                                                        }

                                                        if (validCols == 0) throw new Exception("Die Erwartungstabelle (Expected Table) darf nicht komplett leer sein.");

                                                        DataTable actualDt = null;
                                                        string sampleSolution = SqlLevelTester.ConvertMysqlToSqlite(connection, draft.SampleSolution);

                                                        if (draft.IsDmlMode)
                                                        {
                                                            using (var dmlCmd = connection.CreateCommand())
                                                            {
                                                                dmlCmd.CommandText = sampleSolution;
                                                                dmlCmd.ExecuteNonQuery();
                                                            }

                                                            if (string.IsNullOrWhiteSpace(draft.VerificationQuery))
                                                                throw new Exception("Im DML Modus muss eine Verifizierungs-Abfrage angegeben werden.");

                                                            string verifyQuery = SqlLevelTester.ConvertMysqlToSqlite(connection, draft.VerificationQuery);
                                                            actualDt = ExecuteDbQuery(connection, verifyQuery);
                                                        }
                                                        else
                                                        {
                                                            actualDt = ExecuteDbQuery(connection, sampleSolution);
                                                        }

                                                        if (actualDt.Columns.Count != validCols)
                                                            throw new Exception($"Spaltenanzahl stimmt nicht überein. Erwartet: {validCols}, Ist: {actualDt.Columns.Count}");

                                                        for (int i = 0; i < validCols; i++)
                                                        {
                                                            if (!actualDt.Columns[i].ColumnName.Equals(cleanedSchema[i].Name, StringComparison.OrdinalIgnoreCase))
                                                                throw new Exception($"Spaltenname an Position {i + 1} stimmt nicht. Erwartet: '{cleanedSchema[i].Name}', Ist: '{actualDt.Columns[i].ColumnName}'");
                                                        }

                                                        if (actualDt.Rows.Count != cleanedResult.Count)
                                                            throw new Exception($"Zeilenanzahl stimmt nicht überein. Erwartet: {cleanedResult.Count}, Ist: {actualDt.Rows.Count}");

                                                        for (int r = 0; r < cleanedResult.Count; r++)
                                                        {
                                                            for (int c = 0; c < validCols; c++)
                                                            {
                                                                string expectedVal = cleanedResult[r][c] ?? "";
                                                                if (expectedVal == "") expectedVal = "NULL";

                                                                string actualVal = actualDt.Rows[r][c]?.ToString()?.Replace(",", ".") ?? "";
                                                                if (actualDt.Rows[r][c] == DBNull.Value || string.IsNullOrEmpty(actualVal)) actualVal = "NULL";

                                                                if (double.TryParse(expectedVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double expNum) &&
                                                                    double.TryParse(actualVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double actNum))
                                                                {
                                                                    if (Math.Abs(expNum - actNum) > 0.0001)
                                                                        throw new Exception($"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                                                                }
                                                                else if (!expectedVal.Equals(actualVal, StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    throw new Exception($"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                                                                }
                                                            }
                                                        }

                                                        return (true, cleanedSchema, cleanedResult);
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    if (!cts.Token.IsCancellationRequested)
                                                        Dispatcher.UIThread.InvokeAsync(() => AddSqlOutput("Error", $"❌ Export Fehler ({cl.Name}): {ex.Message}", Brushes.Red));
                                                    return (false, null, null);
                                                }
                                            }, cts.Token);

                                            if (!validData.Success) throw new Exception("Validierung fehlgeschlagen.");

                                            AddSqlOutput("System", "> Generiere Diagramme...", Brushes.LightGray);

                                            if (!string.IsNullOrWhiteSpace(draft.PlantUmlSource))
                                            {
                                                string prepared = PreparePlantUmlSource(draft.PlantUmlSource);
                                                draft.PlantUmlSvgContent = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                            }

                                            SqlLevelDesigner.ExportLevel(cl.FilePath, draft, validData.Schema, validData.Result);
                                            btnQuickExport.Content = LoadIcon("assets/icons/ic_success.svg", 16);
                                            AddSqlOutput("System", $"> {cl.Name} erfolgreich exportiert!", Brushes.LightGreen);

                                            await System.Threading.Tasks.Task.Delay(2000);
                                            RefreshUI();
                                        }
                                        else
                                        {
                                            AddToConsole($"\n> Quick Export gestartet für: {cl.Name}...", Brushes.LightGray);
                                            var draft = LevelDesigner.LoadDraft(cl.FilePath);

                                            bool valid = await System.Threading.Tasks.Task.Run(async () =>
                                            {
                                                try
                                                {
                                                    string fullCode = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" + draft.TestCode;
                                                    string validatorCode = "using System;\nusing System.Reflection;\nusing System.Collections.Generic;\nusing System.Linq;\npublic static class DesignerValidator { " + draft.ValidationCode + " }";

                                                    var references = GetSafeReferences();

                                                    var tree = CSharpSyntaxTree.ParseText(fullCode, cancellationToken: cts.Token);
                                                    var compilation = CSharpCompilation.Create(
                                                        $"QuickExport_{Guid.NewGuid()}",
                                                        new[] { tree },
                                                        references,
                                                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                                                    using (var ms = new MemoryStream())
                                                    {
                                                        var result = compilation.Emit(ms, cancellationToken: cts.Token);
                                                        if (!result.Success)
                                                        {
                                                            var diag = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
                                                            throw new Exception($"Kompilierfehler: {diag?.GetMessage() ?? "Unbekannt"}");
                                                        }

                                                        ms.Seek(0, SeekOrigin.Begin);
                                                        var assembly = Assembly.Load(ms.ToArray());

                                                        // compile validator
                                                        var valTree = CSharpSyntaxTree.ParseText(validatorCode, cancellationToken: cts.Token);
                                                        var valCompilation = CSharpCompilation.Create(
                                                            $"Validator_{Guid.NewGuid()}",
                                                            new[] { valTree },
                                                            references,
                                                            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                                                        using (var valMs = new MemoryStream())
                                                        {
                                                            var valResult = valCompilation.Emit(valMs, cancellationToken: cts.Token);
                                                            if (!valResult.Success) throw new Exception("Fehler im Validierungs-Code.");

                                                            valMs.Seek(0, SeekOrigin.Begin);
                                                            var valAssembly = Assembly.Load(valMs.ToArray());
                                                            var valType = valAssembly.GetType("DesignerValidator");
                                                            var valMethod = valType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                                                .FirstOrDefault(m => m.ReturnType == typeof(bool) && m.GetParameters().Length == 2);

                                                            // run validation
                                                            object[] args = new object[] { assembly, null };
                                                            bool passed = (bool)valMethod.Invoke(null, args);

                                                            if (!passed) throw new Exception($"Validierung nicht bestanden: {args[1]}");
                                                            return true;
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    if (!cts.Token.IsCancellationRequested)
                                                        await Dispatcher.UIThread.InvokeAsync(() => AddToConsole($"\n❌ Export Fehler ({cl.Name}): {ex.Message}", Brushes.Red));
                                                    return false;
                                                }
                                            }, cts.Token);

                                            if (!valid) throw new Exception("Validierung fehlgeschlagen.");

                                            // generate diagrams
                                            AddToConsole("\n> Generiere Diagramme...", Brushes.LightGray);

                                            if (draft.PlantUmlSources != null && draft.PlantUmlSources.Count > 0 && !string.IsNullOrWhiteSpace(draft.PlantUmlSources[0]))
                                            {
                                                string prepared = PreparePlantUmlSource(draft.PlantUmlSources[0]);
                                                string svgContent = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                                if (draft.PlantUmlSvgContents == null) draft.PlantUmlSvgContents = new List<string>();
                                                if (draft.PlantUmlSvgContents.Count == 0) draft.PlantUmlSvgContents.Add("");
                                                draft.PlantUmlSvgContents[0] = svgContent;
                                            }

                                            for (int i = 0; i < draft.MaterialDiagrams.Count; i++)
                                            {
                                                if (!string.IsNullOrWhiteSpace(draft.MaterialDiagrams[i].PlantUmlSource))
                                                {
                                                    string prepared = PreparePlantUmlSource(draft.MaterialDiagrams[i].PlantUmlSource);
                                                    draft.MaterialDiagrams[i].PlantUmlSvgContent = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                                }
                                            }

                                            // export
                                            LevelDesigner.ExportLevel(cl.FilePath, draft);
                                            btnQuickExport.Content = LoadIcon("assets/icons/ic_success.svg", 16);
                                            AddToConsole($"\n> {cl.Name} erfolgreich exportiert!", Brushes.LightGreen);

                                            await System.Threading.Tasks.Task.Delay(2000);
                                            RefreshUI();
                                        }
                                    }
                                    catch (OperationCanceledException) { }
                                    catch (Exception)
                                    {
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_error.svg", 16);
                                        btnQuickExport.IsEnabled = true;
                                        btnQuickExport.Tag = "idle";
                                        await System.Threading.Tasks.Task.Delay(2000);
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_generate.svg", 16);
                                    }
                                    finally
                                    {
                                        win.Closing -= closingHandler;
                                        cts.Dispose();

                                        // reset button state after delay
                                        await System.Threading.Tasks.Task.Delay(2000);
                                        if (btnQuickExport.Content != null)
                                        {
                                            btnQuickExport.Content = LoadIcon("assets/icons/ic_generate.svg", 16);
                                            btnQuickExport.IsEnabled = true;
                                            btnQuickExport.Tag = "idle";
                                        }
                                    }
                                };
                                actionPanel.Children.Add(btnQuickExport);
                            }

                            if (cl.IsDraft)
                            {
                                var btnEdit = new Button
                                {
                                    Content = LoadIcon("assets/icons/ic_edit.svg", 16),
                                    Background = Brushes.Transparent,
                                    Padding = new Thickness(8)
                                };
                                ToolTip.SetTip(btnEdit, "Level im Designer bearbeiten");
                                btnEdit.Click += (_, __) => { win.Close(); ToggleDesignerMode(true, cl.FilePath); };
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
                            btnDelete.Click += async (_, __) => { await DeleteCustomLevel(cl, win); RefreshUI(); };
                            actionPanel.Children.Add(btnDelete);

                            rowGrid.Children.Add(actionPanel);
                            return rowGrid;
                        }

                        // show groups
                        foreach (var group in folderGroups)
                        {
                            var groupContent = new StackPanel { Spacing = 5, Margin = new Thickness(0, 5, 0, 0) };
                            foreach (var cl in group) groupContent.Children.Add(CreateLevelRow(cl));

                            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                            headerPanel.Children.Add(new TextBlock
                            {
                                Text = group.Key,
                                Foreground = BrushTextTitle,
                                FontWeight = FontWeight.Bold,
                                VerticalAlignment = VerticalAlignment.Center
                            });

                            bool allComplete = group.All(l => !l.IsDraft && (_isSqlMode ? customPlayerData.CompletedCustomSqlLevels.Contains(l.Name) : customPlayerData.CompletedCustomLevels.Contains(l.Name)));
                            if (allComplete && group.Any()) headerPanel.Children.Add(LoadIcon("assets/icons/ic_done.svg", 16));

                            customStack.Children.Add(new Expander
                            {
                                Header = headerPanel,
                                Content = groupContent,
                                IsExpanded = false,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                CornerRadius = new CornerRadius(4),
                                Margin = new Thickness(0, 0, 0, 5)
                            });
                        }

                        foreach (var cl in rootLevels) customStack.Children.Add(CreateLevelRow(cl));
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
                AddToConsole($"\n> Fehler beim Öffnen des Ordners: {ex.Message}", Brushes.Orange);
            }
        }

        private List<CustomLevelInfo> GetCustomLevels()
        {
            var list = new List<CustomLevelInfo>();
            string rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");

            if (!Directory.Exists(rootPath)) return list;

            (string name, string author, bool quickGen) GetMetadata(string file)
            {
                try
                {
                    string json = File.ReadAllText(file);

                    if (!file.EndsWith("draft", StringComparison.OrdinalIgnoreCase) && !json.TrimStart().StartsWith("{"))
                    {
                        json = LevelEncryption.Decrypt(json);
                    }

                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        string name = root.TryGetProperty("Name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                        string author = root.TryGetProperty("Author", out var a) ? a.GetString() : "Unbekannt";

                        bool quickGen = false;
                        if (root.TryGetProperty("QuickGenerate", out var qg))
                        {
                            if (qg.ValueKind == JsonValueKind.True) quickGen = true;
                            if (qg.ValueKind == JsonValueKind.String && qg.GetString().ToLower() == "true") quickGen = true;
                        }

                        return (name, author, quickGen);
                    }
                }
                catch
                {
                    return (Path.GetFileNameWithoutExtension(file), "Fehler", false);
                }
            }

            void ScanDirectory(string dir, string sectionName)
            {
                if (!Directory.Exists(dir)) return;

                // regular custom levels
                foreach (var file in Directory.GetFiles(dir, _isSqlMode ? "*.eliteslvl" : "*.elitelvl"))
                {
                    var meta = GetMetadata(file);
                    list.Add(new CustomLevelInfo
                    {
                        Name = meta.name,
                        Author = meta.author,
                        FilePath = file,
                        Section = sectionName,
                        IsDraft = false
                    });
                }

                // custom level drafts
                foreach (var file in Directory.GetFiles(dir, _isSqlMode ? "*.eliteslvldraft" : "*.elitelvldraft"))
                {
                    var meta = GetMetadata(file);
                    list.Add(new CustomLevelInfo
                    {
                        Name = meta.name,
                        Author = meta.author,
                        FilePath = file,
                        Section = sectionName,
                        IsDraft = true,
                        QuickGenerate = meta.quickGen
                    });
                }
            }

            // scan subdirectories (groups)
            foreach (var subdir in Directory.GetDirectories(rootPath))
            {
                string dirName = new DirectoryInfo(subdir).Name;
                ScanDirectory(subdir, dirName);
            }

            // ccan root directory
            ScanDirectory(rootPath, "Einzelne Levels");

            return list;
        }

        private async System.Threading.Tasks.Task<string> ShowAddLevelDialog(Window owner)
        {
            var dialog = new Window
            {
                Title = "Neues Level",
                Width = 400,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            var rootGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto, *, Auto"),
                Margin = new Thickness(20)
            };

            // header
            rootGrid.Children.Add(new TextBlock
            {
                Text = "Neues Custom Level",
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var contentPanel = new Panel();
            Grid.SetRow(contentPanel, 1);

            // manual mode ui
            var panelManual = new StackPanel
            {
                Spacing = 15,
                IsVisible = true
            };

            panelManual.Children.Add(new TextBlock
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
            panelManual.Children.Add(txtName);

            panelManual.Children.Add(new TextBlock
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
            panelManual.Children.Add(txtAuthor);

            contentPanel.Children.Add(panelManual);

            // ai mode ui
            var panelAi = new Grid
            {
                IsVisible = false,
                RowDefinitions = new RowDefinitions("*, Auto")
            };

            var txtJson = new TextBox
            {
                Watermark = "Füge hier den JSON-Code der KI ein...",
                Background = SolidColorBrush.Parse("#1E1E1E"),
                Foreground = Brushes.Gray,
                BorderThickness = new Thickness(1),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily(MonospaceFontFamily),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                MinHeight = 120
            };
            panelAi.Children.Add(txtJson);

            // ai tools row
            var aiRowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(aiRowGrid, 1);

            // error container
            var errorStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                IsVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            var txtErrorMsg = new TextBlock
            {
                Text = "Ungültiges JSON", // placeholder
                Foreground = SolidColorBrush.Parse("#FF5555"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };
            ToolTip.SetTip(txtErrorMsg, "Fehler im JSON Format");

            string fullErrorText = ""; // store full error for copy

            var btnCopyError = new Button
            {
                Content = LoadIcon("assets/icons/ic_copy.svg", 14),
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Padding = new Thickness(6),
                CornerRadius = new CornerRadius(4)
            };
            ToolTip.SetTip(btnCopyError, "Fehler kopieren");
            btnCopyError.Click += async (s, e) =>
            {
                var topLevel = TopLevel.GetTopLevel(dialog);
                if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(fullErrorText))
                {
                    await topLevel.Clipboard.SetTextAsync(fullErrorText);
                    btnCopyError.Background = SolidColorBrush.Parse("#2E8B57"); // flash green
                    await System.Threading.Tasks.Task.Delay(500);
                    btnCopyError.Background = SolidColorBrush.Parse("#3C3C3C");
                }
            };

            errorStack.Children.Add(txtErrorMsg);
            errorStack.Children.Add(btnCopyError);
            aiRowGrid.Children.Add(errorStack);

            // tools container
            var aiToolsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            Grid.SetColumn(aiToolsPanel, 1);

            var btnGuide = new Button
            {
                Content = LoadIcon("assets/icons/ic_guide.svg", 18),
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4)
            };
            ToolTip.SetTip(btnGuide, "Anleitung öffnen");
            btnGuide.Click += (_, __) =>
            {
                try
                {
                    var url = _isSqlMode ? "https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_AI_LEVEL_CREATION_GUIDE" : "https://github.com/OnlyCook/abitur-elite-code/wiki/CS_AI_LEVEL_CREATION_GUIDE";

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
                }
                catch { }
            };

            var btnPaste = new Button
            {
                Content = LoadIcon("assets/icons/ic_import.svg", 18),
                Background = SolidColorBrush.Parse("#007ACC"),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4)
            };
            ToolTip.SetTip(btnPaste, "Aus Zwischenablage einfügen");
            btnPaste.Click += async (_, __) =>
            {
                var topLevel = TopLevel.GetTopLevel(dialog);
                if (topLevel?.Clipboard != null)
                {
                    string text = await topLevel.Clipboard.GetTextAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        txtJson.Text = text;
                        errorStack.IsVisible = false; // reset error
                    }
                }
            };

            aiToolsPanel.Children.Add(btnGuide);
            aiToolsPanel.Children.Add(btnPaste);
            aiRowGrid.Children.Add(aiToolsPanel);

            panelAi.Children.Add(aiRowGrid);
            contentPanel.Children.Add(panelAi);
            rootGrid.Children.Add(contentPanel);

            // footer grid
            var footerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *"),
                Margin = new Thickness(0, 20, 0, 0)
            };
            Grid.SetRow(footerGrid, 2);

            // mode switching button
            var btnSwitchMode = new Button
            {
                Content = "KI Import Modus",
                Background = Brushes.Transparent,
                Foreground = SolidColorBrush.Parse("#0088e3"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursor.Parse("Hand"),
                Padding = new Thickness(5, 5, 5, 5)
            };

            bool isAiMode = false;
            btnSwitchMode.Click += (_, __) =>
            {
                isAiMode = !isAiMode;
                panelManual.IsVisible = !isAiMode;
                panelAi.IsVisible = isAiMode;
                errorStack.IsVisible = false;

                if (isAiMode)
                {
                    btnSwitchMode.Content = "Manueller Modus";
                    btnSwitchMode.Foreground = Brushes.Gray;
                    txtJson.Focus();
                }
                else
                {
                    btnSwitchMode.Content = "KI Import Modus";
                    btnSwitchMode.Foreground = SolidColorBrush.Parse("#0088e3");
                    txtName.Focus();
                }
            };
            footerGrid.Children.Add(btnSwitchMode);

            // action buttons
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };
            Grid.SetColumn(actionPanel, 1);

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

            string resultPath = null;

            btnCancel.Click += (_, __) => dialog.Close();
            btnCreate.Click += (_, __) =>
            {
                try
                {
                    if (isAiMode)
                    {
                        if (string.IsNullOrWhiteSpace(txtJson.Text)) return;

                        try
                        {
                            var doc = JsonDocument.Parse(txtJson.Text);

                            if (!doc.RootElement.TryGetProperty("Name", out var nameProp) ||
                                !doc.RootElement.TryGetProperty("Author", out var authProp))
                            {
                                throw new Exception("JSON muss 'Name' und 'Author' enthalten.");
                            }

                            string name = nameProp.GetString();
                            string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                            string filename = $"{safeName}.{(_isSqlMode ? "eliteslvldraft" : "elitelvldraft")}";
                            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels");
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                            string path = Path.Combine(dir, filename);
                            File.WriteAllText(path, txtJson.Text);

                            resultPath = path;
                            dialog.Close();
                        }
                        catch (Exception ex)
                        {
                            fullErrorText = ex.Message;
                            txtErrorMsg.Text = "Fehler: " + ex.Message;
                            ToolTip.SetTip(txtErrorMsg, ex.Message); // show full error on hover
                            errorStack.IsVisible = true;
                        }
                    }
                    else // manual mode
                    {
                        if (string.IsNullOrWhiteSpace(txtName.Text)) return;

                        string safeName = string.Join("_", txtName.Text.Split(Path.GetInvalidFileNameChars()));
                        string filename = $"{safeName}.{(_isSqlMode ? "eliteslvldraft" : "elitelvldraft")}";
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
                        resultPath = path;

                        dialog.Close();
                    }
                }
                catch (Exception ex)
                {
                    AddToConsole($"\n> Fehler beim Erstellen: {ex.Message}", Brushes.Red);
                }
            };

            actionPanel.Children.Add(btnCancel);
            actionPanel.Children.Add(btnCreate);
            footerGrid.Children.Add(actionPanel);

            rootGrid.Children.Add(footerGrid);

            dialog.Content = new Border { Child = rootGrid };
            await dialog.ShowDialog(owner);

            return resultPath;
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
                        if (_isSqlMode)
                        {
                            if (customPlayerData.CompletedCustomSqlLevels.Contains(info.Name))
                            {
                                customPlayerData.CompletedCustomSqlLevels.Remove(info.Name);
                                changed = true;
                            }
                            if (customPlayerData.UserSqlCode.ContainsKey(info.Name))
                            {
                                customPlayerData.UserSqlCode.Remove(info.Name);
                                changed = true;
                            }
                            if (customPlayerData.UserSqlModels.ContainsKey(info.Name))
                            {
                                customPlayerData.UserSqlModels.Remove(info.Name);
                                changed = true;
                            }
                        }
                        else
                        {
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
                        }

                        if (changed) SaveSystem.SaveCustom(customPlayerData);
                    }
                }
                catch (Exception ex)
                {
                    AddToConsole($"\n> Fehler: {ex.Message}", Brushes.Red);
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
            bool originalSqlVimEnabled = AppSettings.IsSqlVimEnabled;
            bool originalSyntaxEnabled = AppSettings.IsSyntaxHighlightingEnabled;
            bool originalSqlSyntaxEnabled = AppSettings.IsSqlSyntaxHighlightingEnabled;
            bool originalAutocompleteEnabled = AppSettings.IsAutocompleteEnabled;
            bool originalSqlAutocompleteEnabled = AppSettings.IsSqlAutocompleteEnabled;
            bool originalErrorEnabled = AppSettings.IsErrorHighlightingEnabled;
            bool originalErrorExplanation = AppSettings.IsErrorExplanationEnabled;
            double originalEditorFontSize = AppSettings.EditorFontSize;
            double originalSqlFontSize = AppSettings.SqlEditorFontSize;
            double originalUiScale = AppSettings.UiScale;
            bool originalAutoUpdateEnabled = AppSettings.AutoCheckForUpdates;
            bool isPortable = SaveSystem.IsPortableModeEnabled();
            bool originalPortableState = isPortable;
            bool originalSqlAntiSpoilerEnabled = AppSettings.IsSqlAntiSpoilerEnabled;

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

            Button CreateCatBtn(string text, bool showBadge = false)
            {
                var btn = new Button
                {
                    Background = Brushes.Transparent,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(15),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };

                if (showBadge)
                {
                    var grid = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*, Auto")
                    };
                    grid.Children.Add(new TextBlock
                    {
                        Text = text,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    var badge = new Border
                    {
                        Background = SolidColorBrush.Parse("#B43232"),
                        Width = 8,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    Grid.SetColumn(badge, 1);
                    grid.Children.Add(badge);
                    btn.Content = grid;
                }
                else
                {
                    btn.Content = text;
                }
                return btn;
            }

            var btnCatEditor = CreateCatBtn("Editor");
            var btnCatDisplay = CreateCatBtn("Darstellung");
            var btnCatData = CreateCatBtn("Daten");
            var btnCatUpdates = CreateCatBtn("Updates", _updateAvailable);
            var btnCatMisc = CreateCatBtn("Sonstiges");

            categoriesPanel.Children.Add(btnCatEditor);
            categoriesPanel.Children.Add(btnCatDisplay);
            categoriesPanel.Children.Add(btnCatData);
            categoriesPanel.Children.Add(btnCatUpdates);
            categoriesPanel.Children.Add(btnCatMisc);

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

            var rightPanel = new Border
            {
                Padding = new Thickness(20),
                Background = BrushBgPanel
            };
            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);

            // --- CONTROLS CREATION ---

            // syntax highlighting
            var chkSyntax = new CheckBox
            {
                Content = "Syntax-Hervorhebung",
                IsChecked = _isSqlMode ? AppSettings.IsSqlSyntaxHighlightingEnabled : AppSettings.IsSyntaxHighlightingEnabled,
                Foreground = Brushes.White
            };

            // autocompletion
            var chkAutocomplete = new CheckBox
            {
                Content = "Autovervollständigung",
                IsChecked = _isSqlMode ? AppSettings.IsSqlAutocompleteEnabled : AppSettings.IsAutocompleteEnabled,
                Foreground = Brushes.White
            };

            // error highlighting (c# only, for now)
            var chkError = new CheckBox
            {
                Content = "Error-Hervorhebung",
                IsChecked = AppSettings.IsErrorHighlightingEnabled,
                Foreground = Brushes.White,
                IsVisible = !_isSqlMode
            };

            var chkErrorExplain = new CheckBox
            {
                Content = "Error-Erklärungen",
                IsChecked = AppSettings.IsErrorExplanationEnabled,
                IsEnabled = AppSettings.IsErrorHighlightingEnabled,
                Foreground = Brushes.White,
                IsVisible = !_isSqlMode,
                Margin = new Thickness(20, 0, 0, 0) // indent
            };

            // vim controls (c# only)
            var chkVim = new CheckBox
            {
                Content = "Vim Steuerung",
                IsChecked = _isSqlMode ? AppSettings.IsSqlVimEnabled : AppSettings.IsVimEnabled,
                Foreground = Brushes.White
            };

            // lock vim toggle during tutorial
            if (_isTutorialMode)
            {
                chkVim.IsEnabled = false;
                ToolTip.SetTip(chkVim, "Während des Tutorials nicht änderbar");
            }

            // display settings
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

            // editor font size
            var sliderFontSize = new Slider
            {
                Minimum = 8,
                Maximum = 48,
                Value = AppSettings.EditorFontSize,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var txtFontSizeVal = new TextBlock
            {
                Text = $"{AppSettings.EditorFontSize:F0}px",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            // sql font size
            var sliderSqlFontSize = new Slider
            {
                Minimum = 8,
                Maximum = 48,
                Value = AppSettings.SqlEditorFontSize,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var txtSqlFontSizeVal = new TextBlock
            {
                Text = $"{AppSettings.SqlEditorFontSize:F0}px",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

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
            
            // update settings panel
            var updatesSettingsPanel = new StackPanel { Spacing = 15 };
            updatesSettingsPanel.Children.Add(new TextBlock
            {
                Text = "Updates",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var chkAutoUpdate = new CheckBox
            {
                Content = "Beim Start automatisch nach Updates suchen",
                IsChecked = AppSettings.AutoCheckForUpdates,
                Foreground = Brushes.White
            };

            var txtVersionInfo = new TextBlock
            {
                Text = _updateAvailable
                    ? $"Eine neue Version ist verfügbar: {_latestVersion}\nAktuelle Version: {UpdateManager.CurrentVersion}"
                    : $"Aktuelle Version: {UpdateManager.CurrentVersion}",
                Foreground = _updateAvailable ? SolidColorBrush.Parse("#32A852") : Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 10)
            };

            var btnCheckUpdate = new Button
            {
                Content = "Nach Updates suchen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                Padding = new Thickness(15, 8),
                CornerRadius = new CornerRadius(4),
                MinWidth = 180,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            var btnUpdateApp = new Button
            {
                Content = "App aktualisieren",
                Background = SolidColorBrush.Parse("#007ACC"),
                Foreground = Brushes.White,
                Padding = new Thickness(15, 8),
                CornerRadius = new CornerRadius(4),
                IsEnabled = _updateAvailable
            };

            var updateProgressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Foreground = SolidColorBrush.Parse("#32A852"),
                Background = SolidColorBrush.Parse("#1A1A1A"),
                IsVisible = false,
                Margin = new Thickness(0, 5, 0, 15)
            };

            var updatesActionRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 10
            };
            updatesActionRow.Children.Add(btnCheckUpdate);
            updatesActionRow.Children.Add(btnUpdateApp);

            updatesSettingsPanel.Children.Add(chkAutoUpdate);
            updatesSettingsPanel.Children.Add(txtVersionInfo);
            updatesSettingsPanel.Children.Add(updateProgressBar);
            updatesSettingsPanel.Children.Add(updatesActionRow);

            btnCheckUpdate.Click += async (s, ev) =>
            {
                btnCheckUpdate.Content = "Suche...";
                btnCheckUpdate.IsEnabled = false;

                var result = await UpdateManager.CheckForUpdatesAsync();
                if (result.UpdateAvailable)
                {
                    _updateAvailable = true;
                    _latestVersion = result.LatestVersion;
                    _updateDownloadUrl = result.DownloadUrl;
                    BadgeSettings.IsVisible = true;

                    txtVersionInfo.Text = $"Eine neue Version ist verfügbar: {_latestVersion}\nAktuelle Version: {UpdateManager.CurrentVersion}";
                    txtVersionInfo.Foreground = SolidColorBrush.Parse("#32A852");
                    btnUpdateApp.IsEnabled = true;

                    // add badge to category button dynamically if it doesnt have it
                    if (btnCatUpdates.Content is string)
                    {
                        var grid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*, Auto")
                        };
                        grid.Children.Add(new TextBlock
                        {
                            Text = "Updates",
                            VerticalAlignment = VerticalAlignment.Center
                        });
                        var badge = new Border
                        {
                            Background = SolidColorBrush.Parse("#B43232"),
                            Width = 8,
                            Height = 8,
                            CornerRadius = new CornerRadius(4),
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(5, 0, 0, 0)
                        };
                        Grid.SetColumn(badge, 1);
                        grid.Children.Add(badge);
                        btnCatUpdates.Content = grid;
                    }
                }
                else
                {
                    txtVersionInfo.Text = $"Du bist auf dem neusten Stand.\nAktuelle Version: {UpdateManager.CurrentVersion}";
                    txtVersionInfo.Foreground = Brushes.Gray;
                }

                btnCheckUpdate.Content = "Nach Updates suchen";
                btnCheckUpdate.IsEnabled = true;
            };

            btnUpdateApp.Click += async (s, ev) =>
            {
                btnUpdateApp.Content = "Bereite Update vor...";
                btnUpdateApp.IsEnabled = false;
                btnCheckUpdate.IsEnabled = false;
                updateProgressBar.IsVisible = true;
                updateProgressBar.Value = 0;

                // progress reporter to get update state
                var progress = new Progress<(string message, double percentage)>(p =>
                {
                    btnUpdateApp.Content = p.message;
                    updateProgressBar.Value = p.percentage;
                });

                var updateResult = await UpdateManager.PerformUpdateAsync(_updateDownloadUrl, progress);

                if (updateResult != UpdateManager.UpdateStatus.Success)
                {
                    // reset ui
                    btnUpdateApp.Content = "App aktualisieren";
                    btnUpdateApp.IsEnabled = true;
                    btnCheckUpdate.IsEnabled = true;
                    updateProgressBar.IsVisible = false;

                    // show manual update dialog
                    await ShowManualUpdateDialog(updateResult, _updateDownloadUrl, settingsWin);
                }
            };

            // misc
            var miscSettingsPanel = new StackPanel { Spacing = 15 };
            miscSettingsPanel.Children.Add(new TextBlock
            {
                Text = "Sonstiges",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var chkSqlAntiSpoiler = new CheckBox
            {
                Content = "SQL Anti-Spoiler Modus",
                IsChecked = AppSettings.IsSqlAntiSpoilerEnabled,
                Foreground = Brushes.White
            };
            ToolTip.SetTip(chkSqlAntiSpoiler, "Mögliche Lösungsansätze aus den Levelnamen verbergen");

            chkSqlAntiSpoiler.IsCheckedChanged += (s, ev) =>
            {
                AppSettings.IsSqlAntiSpoilerEnabled = chkSqlAntiSpoiler.IsChecked ?? false;
                CheckChanges();
            };

            miscSettingsPanel.Children.Add(chkSqlAntiSpoiler);

            void CheckChanges()
            {
                bool hasChanges =
                    (!_isSqlMode && chkVim.IsChecked != originalVimEnabled) ||
                    (_isSqlMode && chkVim.IsChecked != originalSqlVimEnabled) ||
                    (!_isSqlMode && chkSyntax.IsChecked != originalSyntaxEnabled) ||
                    (_isSqlMode && chkSyntax.IsChecked != originalSqlSyntaxEnabled) ||
                    (!_isSqlMode && chkAutocomplete.IsChecked != originalAutocompleteEnabled) ||
                    (_isSqlMode && chkAutocomplete.IsChecked != originalSqlAutocompleteEnabled) ||
                    (chkError.IsChecked != originalErrorEnabled) ||
                    (chkErrorExplain.IsChecked != originalErrorExplanation) ||
                    (Math.Abs(sliderFontSize.Value - originalEditorFontSize) > 0.004) ||
                    (Math.Abs(sliderSqlFontSize.Value - originalSqlFontSize) > 0.004) ||
                    (chkPortable.IsChecked != isPortable) ||
                    (chkAutoUpdate.IsChecked != originalAutoUpdateEnabled) ||
                    (chkSqlAntiSpoiler.IsChecked != originalSqlAntiSpoilerEnabled) ||
                    (Math.Abs(sliderScale.Value - originalUiScale) > 0.004);

                btnSave.IsEnabled = hasChanges;
                btnSave.Opacity = hasChanges ? 1.0 : 0.5;
                btnSave.Background = hasChanges ? SolidColorBrush.Parse("#32A852") : SolidColorBrush.Parse("#464646");
            }

            // reset button click
            btnReset.Click += async (s, ev) =>
            {
                var confirmDialog = new Window
                {
                    Title = "Reset",
                    Width = 350,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SystemDecorations = SystemDecorations.BorderOnly,
                    Background = SolidColorBrush.Parse("#252526"),
                    CornerRadius = new CornerRadius(8)
                };

                var cGrid = new Grid { RowDefinitions = new RowDefinitions("*, Auto"), Margin = new Thickness(20) };
                cGrid.Children.Add(new TextBlock
                {
                    Text = "Einstellungen wirklich auf Standard zurücksetzen?",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var cBtnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
                Grid.SetRow(cBtnPanel, 1);

                var btnYes = new Button { Content = "Ja, zurücksetzen", Background = SolidColorBrush.Parse("#B43232"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };
                var btnNo = new Button { Content = "Abbrechen", Background = SolidColorBrush.Parse("#3C3C3C"), Foreground = Brushes.White, CornerRadius = new CornerRadius(4) };

                btnYes.Click += (_, __) =>
                {
                    // reset values based on mode
                    if (_isSqlMode)
                    {
                        chkSyntax.IsChecked = false;
                        chkVim.IsChecked = false;
                        chkAutocomplete.IsChecked = false;
                    }
                    else
                    {
                        chkSyntax.IsChecked = false;
                        chkError.IsChecked = false;
                        chkErrorExplain.IsChecked = false;
                        chkVim.IsChecked = false;
                        chkAutocomplete.IsChecked = false;
                    }

                    sliderFontSize.Value = 16.0;
                    sliderSqlFontSize.Value = 16.0;
                    chkPortable.IsChecked = false;
                    sliderScale.Value = 1.0;
                    chkAutoUpdate.IsChecked = false;

                    confirmDialog.Close();
                };
                btnNo.Click += (_, __) => confirmDialog.Close();

                cBtnPanel.Children.Add(btnNo);
                cBtnPanel.Children.Add(btnYes);
                cGrid.Children.Add(cBtnPanel);

                confirmDialog.Content = cGrid;
                await confirmDialog.ShowDialog(settingsWin);
            };

            // --- EVENT HANDLERS ---

            chkSyntax.IsCheckedChanged += (s, ev) =>
            {
                if (_isSqlMode)
                {
                    AppSettings.IsSqlSyntaxHighlightingEnabled = chkSyntax.IsChecked ?? false;
                    ApplySqlSyntaxHighlighting();
                }
                else
                {
                    AppSettings.IsSyntaxHighlightingEnabled = chkSyntax.IsChecked ?? false;
                    ApplySyntaxHighlighting();
                }
                CheckChanges();
            };

            chkError.IsCheckedChanged += async (s, ev) =>
            {
                if (chkError.IsChecked == true && !originalErrorEnabled)
                {
                    await ShowWarningDialog(
                       "Error-Hervorhebung",
                       "In der Prüfung müssen Fehler selbstständig gefunden werden. Es wird empfohlen ohne dieses Feature zu üben!\n\nAchtung: Diese Funktion setzt sich nach jedem Level-Wechsel zurück."
                    );
                }

                AppSettings.IsErrorHighlightingEnabled = chkError.IsChecked ?? false;

                if (AppSettings.IsErrorHighlightingEnabled == false)
                {
                    chkErrorExplain.IsChecked = false;
                    chkErrorExplain.IsEnabled = false;
                }
                else
                {
                    chkErrorExplain.IsEnabled = true;
                }

                if (AppSettings.IsErrorHighlightingEnabled)
                    UpdateDiagnostics();
                else
                    ClearDiagnostics();

                CheckChanges();
            };

            chkErrorExplain.IsCheckedChanged += async (s, ev) =>
            {
                if (chkErrorExplain.IsChecked == true && !originalErrorExplanation)
                {
                    await ShowWarningDialog(
                      "Error-Erklärungen",
                      "Detaillierte Fehlerbeschreibungen stehen in der Prüfung nicht zur Verfügung. Nutze dies nur, wenn du absolut nicht weiterkommst."
                    );
                }
                AppSettings.IsErrorExplanationEnabled = chkErrorExplain.IsChecked ?? false;
                CheckChanges();
            };

            chkAutocomplete.IsCheckedChanged += (s, ev) =>
            {
                if (_isSqlMode)
                {
                    AppSettings.IsSqlAutocompleteEnabled = chkAutocomplete.IsChecked ?? false;
                    if (AppSettings.IsSqlAutocompleteEnabled)
                    {
                        _sqlAutocompleteService?.ScanTokens(SqlQueryEditor.Text);
                    }
                    else
                    {
                        _sqlAutocompleteService?.ClearSuggestion();
                    }
                }
                else
                {
                    AppSettings.IsAutocompleteEnabled = chkAutocomplete.IsChecked ?? false;
                    if (AppSettings.IsAutocompleteEnabled)
                    {
                        _csharpAutocompleteService?.ScanTokens(CodeEditor.Text);
                    }
                    else
                    {
                        _csharpAutocompleteService?.ClearSuggestion();
                    }
                }

                CheckChanges();
            };

            chkVim.IsCheckedChanged += (s, ev) =>
            {
                if (_isSqlMode) AppSettings.IsSqlVimEnabled = chkVim.IsChecked ?? false;
                else AppSettings.IsVimEnabled = chkVim.IsChecked ?? false;
                UpdateVimState();
                CheckChanges();
            };
            chkPortable.IsCheckedChanged += (s, ev) =>
            {
                CheckChanges();
            };

            sliderScale.ValueChanged += (s, ev) =>
            {
                AppSettings.UiScale = ev.NewValue;
                txtScaleVal.Text = $"{ev.NewValue:P0}";
                ApplyUiScale();
                CheckChanges();
            };

            sliderFontSize.ValueChanged += (s, ev) =>
            {
                AppSettings.EditorFontSize = ev.NewValue;
                txtFontSizeVal.Text = $"{ev.NewValue:F0}px";
                CodeEditor.FontSize = ev.NewValue;
                TutorialEditor.FontSize = ev.NewValue;
                CheckChanges();
            };

            sliderSqlFontSize.ValueChanged += (s, ev) =>
            {
                AppSettings.SqlEditorFontSize = ev.NewValue;
                txtSqlFontSizeVal.Text = $"{ev.NewValue:F0}px";
                SqlQueryEditor.FontSize = ev.NewValue;
                CheckChanges();
            };

            chkAutoUpdate.IsCheckedChanged += (s, ev) =>
            {
                AppSettings.AutoCheckForUpdates = chkAutoUpdate.IsChecked ?? false;
                CheckChanges();
            };

            // --- LAYOUT ASSEMBLY ---

            // editor
            var editorSettings = new StackPanel { Spacing = 15 };
            string editorTitle = _isSqlMode ? "SQL Query Editor" : "C# Code Editor";
            editorSettings.Children.Add(new TextBlock
            {
                Text = editorTitle,
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });
            editorSettings.Children.Add(chkSyntax);
            editorSettings.Children.Add(chkAutocomplete);
            editorSettings.Children.Add(chkError);
            editorSettings.Children.Add(chkErrorExplain);
            editorSettings.Children.Add(chkVim);

            // display
            var displaySettings = new StackPanel { Spacing = 15 };
            var scalePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };
            var fontPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };
            fontPanel.Children.Add(sliderFontSize);
            fontPanel.Children.Add(txtFontSizeVal);
            var sqlFontPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };
            sqlFontPanel.Children.Add(sliderSqlFontSize);
            sqlFontPanel.Children.Add(txtSqlFontSizeVal);

            scalePanel.Children.Add(sliderScale);
            scalePanel.Children.Add(txtScaleVal);
            displaySettings.Children.Add(new TextBlock
            {
                Text = "Darstellung",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });
            displaySettings.Children.Add(new TextBlock
            {
                Text = "UI Skalierung",
                Foreground = Brushes.LightGray
            });
            displaySettings.Children.Add(scalePanel);
            displaySettings.Children.Add(new TextBlock
            {
                Text = "C# Editor Schriftgröße",
                Foreground = Brushes.LightGray
            });
            displaySettings.Children.Add(fontPanel);
            displaySettings.Children.Add(new TextBlock
            {
                Text = "SQL Editor Schriftgröße",
                Foreground = Brushes.LightGray
            });
            displaySettings.Children.Add(sqlFontPanel);

            // data
            var dataSettingsPanel = new StackPanel { Spacing = 15 };
            dataSettingsPanel.Children.Add(new TextBlock
            {
                Text = "Daten & Speicher",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10)
            });
            dataSettingsPanel.Children.Add(chkPortable);
            dataSettingsPanel.Children.Add(txtPortableInfo);

            void ShowCategory(Button activeBtn, Control content)
            {
                btnCatEditor.Background = Brushes.Transparent;
                btnCatDisplay.Background = Brushes.Transparent;
                btnCatData.Background = Brushes.Transparent;
                btnCatUpdates.Background = Brushes.Transparent;
                btnCatMisc.Background = Brushes.Transparent;

                activeBtn.Background = SolidColorBrush.Parse("#3E3E42");
                rightPanel.Child = content;
            }

            btnCatEditor.Click += (s, ev) => ShowCategory(btnCatEditor, editorSettings);
            btnCatDisplay.Click += (s, ev) => ShowCategory(btnCatDisplay, displaySettings);
            btnCatData.Click += (s, ev) => ShowCategory(btnCatData, dataSettingsPanel);
            btnCatUpdates.Click += (s, ev) => ShowCategory(btnCatUpdates, updatesSettingsPanel);
            btnCatMisc.Click += (s, ev) => ShowCategory(btnCatMisc, miscSettingsPanel);

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
                playerData.Settings.IsSqlVimEnabled = AppSettings.IsSqlVimEnabled;
                playerData.Settings.IsSyntaxHighlightingEnabled = AppSettings.IsSyntaxHighlightingEnabled;
                playerData.Settings.IsSqlSyntaxHighlightingEnabled = AppSettings.IsSqlSyntaxHighlightingEnabled;
                playerData.Settings.IsAutocompleteEnabled = AppSettings.IsAutocompleteEnabled;
                playerData.Settings.IsSqlAutocompleteEnabled = AppSettings.IsSqlAutocompleteEnabled;
                playerData.Settings.EditorFontSize = AppSettings.EditorFontSize;
                playerData.Settings.SqlEditorFontSize = AppSettings.SqlEditorFontSize;
                playerData.Settings.UiScale = AppSettings.UiScale;
                playerData.Settings.AutoCheckForUpdates = AppSettings.AutoCheckForUpdates;
                playerData.Settings.IsSqlAntiSpoilerEnabled = AppSettings.IsSqlAntiSpoilerEnabled;

                SaveSystem.Save(playerData);

                if (chkPortable.IsChecked != originalPortableState)
                {
                    try
                    {
                        SaveSystem.SetPortableMode(chkPortable.IsChecked == true);
                        isPortable = chkPortable.IsChecked == true;
                        originalPortableState = isPortable;

                        string location = isPortable ? "Programmordner" : "AppData";
                        AddToConsole($"\n> Speicherort geändert auf: {location}", Brushes.LightGray);
                    }
                    catch (Exception ex)
                    {
                        AddToConsole($"\n> Fehler beim Ändern des Speicherorts: {ex.Message}", Brushes.Red);
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
                    AppSettings.IsSqlVimEnabled = originalSqlVimEnabled;
                    AppSettings.IsSyntaxHighlightingEnabled = originalSyntaxEnabled;
                    AppSettings.IsSqlSyntaxHighlightingEnabled = originalSqlSyntaxEnabled;
                    AppSettings.IsAutocompleteEnabled = originalAutocompleteEnabled;
                    AppSettings.IsSqlAutocompleteEnabled = originalSqlAutocompleteEnabled;
                    AppSettings.IsErrorHighlightingEnabled = originalErrorEnabled;
                    AppSettings.IsErrorExplanationEnabled = originalErrorExplanation;
                    AppSettings.EditorFontSize = originalEditorFontSize;
                    AppSettings.SqlEditorFontSize = originalSqlFontSize;
                    CodeEditor.FontSize = originalEditorFontSize;
                    SqlQueryEditor.FontSize = originalSqlFontSize;
                    AppSettings.UiScale = originalUiScale;
                    AppSettings.AutoCheckForUpdates = originalAutoUpdateEnabled;
                    AppSettings.IsSqlAntiSpoilerEnabled = originalSqlAntiSpoilerEnabled;

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

        private void UpdateSemanticHighlighting()
        {
            var classes = new HashSet<string>();

            // extract from aux code
            if (currentLevel?.AuxiliaryIds != null)
            {
                foreach (var auxId in currentLevel.AuxiliaryIds)
                {
                    string auxCode = AuxiliaryImplementations.GetCode(auxId, CodeEditor.Text);
                    if (!string.IsNullOrEmpty(auxCode))
                    {
                        ExtractTypesFromCode(auxCode, classes);
                    }
                }
            }

            // extract from user code
            ExtractTypesFromCode(CodeEditor.Text, classes);

            classes.ExceptWith(AutocompleteService.CsharpKeywords); // filter out c# keywords (safety)

            _semanticClassTransformer.KnownClasses = classes;
            CodeEditor.TextArea.TextView.Redraw();
        }

        private void ExtractTypesFromCode(string code, HashSet<string> types)
        {
            if (string.IsNullOrEmpty(code)) return;

            // finds class, struct, record, interface, enum names (automatically handles constructors/destructors)
            var matches = Regex.Matches(code, @"\b(?:class|struct|record|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)\b");
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    types.Add(match.Groups[1].Value);
                }
            }
        }

        private void ApplySqlSyntaxHighlighting()
        {
            if (AppSettings.IsSqlSyntaxHighlightingEnabled)
            {
                SqlQueryEditor.SyntaxHighlighting = SqlCodeEditor.GetDarkSqlHighlighting();
            }
            else
            {
                SqlQueryEditor.SyntaxHighlighting = null;
            }
        }

        // --- VIM LOGIC ---

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
            if (keyChar != "j" && keyChar != "k")
            {
                _vimDesiredColumn = -1;
            }

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
                    if (ActiveEditor.CaretOffset < lineA.EndOffset)
                    {
                        ActiveEditor.CaretOffset++;
                    }
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
                    int lenD = (lineD.Offset + lineD.Length) - ActiveEditor.CaretOffset;
                    if (lenD > 0)
                    {
                        _vimClipboard = ActiveEditor.Document.GetText(ActiveEditor.CaretOffset, lenD);
                        ActiveEditor.Document.Remove(ActiveEditor.CaretOffset, lenD);
                    }
                    break;

                // --- EDITING ---
                case "d":
                case "x":
                    if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine || !ActiveEditor.TextArea.Selection.IsEmpty)
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
                    if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine || !ActiveEditor.TextArea.Selection.IsEmpty)
                    {
                        YankVisualSelection();
                        return;
                    }
                    _vimCommandBuffer = keyChar; _vimPreviousMode = _vimMode; _vimMode = VimMode.CommandPending; _vimDesiredColumn = -1;
                    break;

                case "c":
                    if (_vimMode == VimMode.Visual || _vimMode == VimMode.VisualLine || !ActiveEditor.TextArea.Selection.IsEmpty)
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
                    {
                        ActiveEditor.Document.Insert(ActiveEditor.CaretOffset, _vimClipboard);
                    }
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
                int index = ActiveEditor.Text.IndexOf(searchTerm, ActiveEditor.CaretOffset + 1, StringComparison.OrdinalIgnoreCase);
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
                if (end < ActiveEditor.Document.TextLength)
                {
                    end++;
                }
            }

            ActiveEditor.TextArea.Selection = AvaloniaEdit.Editing.Selection.Create(ActiveEditor.TextArea, start, end);
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

            Border activeBorder = _isTutorialMode ? TutorialVimStatusBorder : (_isSqlMode ? SqlVimStatusBorder : VimStatusBorder);
            TextBlock activeText = _isTutorialMode ? TutorialVimStatusBar : (_isSqlMode ? SqlVimStatusBar : VimStatusBar);

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

            TutorialEditor.AddHandler(InputElement.KeyDownEvent, TutorialEditor_KeyDown, RoutingStrategies.Tunnel);
            TutorialEditor.AddHandler(InputElement.PointerPressedEvent, TutorialEditor_PointerPressed, RoutingStrategies.Tunnel);
            TutorialEditor.AddHandler(InputElement.PointerWheelChangedEvent, TutorialEditor_PointerWheelChanged, RoutingStrategies.Tunnel);
            TutorialEditor.TextArea.TextEntering += Editor_TextEntering;

            TutorialEditor.TextArea.Caret.PositionChanged += (s, e) =>
            {
                // clamp caret in vim normal mode
                if (_isTutorialMode && _vimMode == VimMode.Normal)
                {
                    var line = TutorialEditor.Document.GetLineByOffset(TutorialEditor.CaretOffset);
                    if (TutorialEditor.CaretOffset == line.EndOffset && line.Length > 0)
                    {
                        TutorialEditor.CaretOffset--;
                    }
                }
            };

            TutorialEditor.TextChanged += (s, e) => {
                CheckTutorialProgress();
            };
        }

        private void TutorialEditor_KeyDown(object sender, KeyEventArgs e)
        {
            // ignore standalone modifier keys
            bool isModifier = e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                              e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                              e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                              e.Key == Key.LWin || e.Key == Key.RWin;

            if (!isModifier)
            {
                _tutorialKeystrokes++;
            }

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
            else if (_vimMode == VimMode.CommandLine)
            {
                HandleVimCommandLine(e);
                e.Handled = true;
                CheckTutorialProgress();
                return;
            }
            else if (_vimMode == VimMode.Search)
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

                if (e.Key == Key.C || e.Key == Key.X || e.Key == Key.A)
                {
                    return;
                }
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
                {
                    AppSettings.EditorFontSize = Math.Min(48, AppSettings.EditorFontSize + 1);
                }
                else if (e.Delta.Y < 0)
                {
                    AppSettings.EditorFontSize = Math.Max(8, AppSettings.EditorFontSize - 1);
                }
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
            Dispatcher.UIThread.Post(() => {
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

            if (_isSqlMode) SqlQueryEditor.Focus(); else CodeEditor.Focus();
        }

        private void RenderTutorialTask(string text)
        {
            TxtTutorialTask.Inlines?.Clear();
            if (TxtTutorialTask.Inlines == null) TxtTutorialTask.Inlines = new Avalonia.Controls.Documents.InlineCollection();

            // extract ((keys)) and [code] segments
            var parts = Regex.Split(text, @"(\(\(.*?\)\)|\[.*?\])");
            foreach (var part in parts)
            {
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

                    TxtTutorialTask.Inlines.Add(new Avalonia.Controls.Documents.InlineUIContainer { Child = keyBorder });
                }
                else if (part.StartsWith("[") && part.EndsWith("]"))
                {
                    string content = part.Substring(1, part.Length - 2);
                    TxtTutorialTask.Inlines.Add(new Avalonia.Controls.Documents.Run
                    {
                        Text = content,
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextHighlight,
                        FontFamily = new FontFamily(MonospaceFontFamily)
                    });
                }
                else if (!string.IsNullOrEmpty(part))
                {
                    TxtTutorialTask.Inlines.Add(new Avalonia.Controls.Documents.Run { Text = part });
                }
            }
        }

        private void UpdateTutorialInstructions()
        {
            switch (_tutorialStep)
            {
                case 1:
                    RenderTutorialTask("Aufgabe 1/7 (Navigation & Modi): Vim startet im NORMAL-Modus. Nutze ((h)) (links), ((j)) (runter), ((k)) (hoch), ((l)) (rechts) zur Navigation. Gehe zu [change_me_to_insert], lösche die Zeile mit ((d))((d)), drücke ((i)) (Insert), tippe [//erledigt] und beende mit ((ESC)).");
                    break;
                case 2:
                    RenderTutorialTask("Aufgabe 2/7 (Insert vs Append): Setze den Cursor bei [ng] auf das [n], drücke ((i)) (Insert VOR Cursor), tippe [i] und drücke ((ESC)). Gehe dann bei [in] auf das [n], drücke ((a)) (Append NACH Cursor), tippe [g] und drücke ((ESC)).");
                    break;
                case 3:
                    RenderTutorialTask("Aufgabe 3/7 (Schnelle Navigation & Löschen): Gehe in die [404X]-Zeile. Drücke (($)) um ans Zeilenende zu springen, lösche [X] mit ((x)). Gehe eine Zeile tiefer, drücke ((0)) für den Zeilenanfang, gehe zu [//] und lösche nur den Kommentar mit ((D)).");
                    break;
                case 4:
                    // dynamically find target line
                    int targetLine = 1;
                    var lines = TutorialEditor.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains("// jump_here_and_copy"))
                        {
                            targetLine = i + 1;
                            break;
                        }
                    }
                    string lineStr = targetLine.ToString();
                    string keys = string.Join("", lineStr.Select(c => $"(({c}))"));

                    RenderTutorialTask($"Aufgabe 4/7 (Springen & Kopieren): Tippe ((:)){keys} und ((Enter)) um exakt zu Zeile {targetLine} zu springen. Kopiere die Zeile mit ((y))((y)) (Yank) und füge sie mit ((p)) (Paste) darunter ein.");
                    break;
                case 5:
                    RenderTutorialTask("Aufgabe 5/7 (Suchen): Drücke ((/)), tippe [Schatz] und drücke ((Enter)) um das Wort zu finden. Lösche es, wechsle in den Insert-Modus ((i)), tippe [Gold] und drücke ((ESC)).");
                    break;
                case 6:
                    RenderTutorialTask("Aufgabe 6/7 (Speichern): Fast geschafft! Speichere das Dokument im Normal-Modus, indem du ((:))((w)) (write) tippst und mit ((Enter)) bestätigst.");
                    break;
                case 7:
                    RenderTutorialTask("Aufgabe 7/7 (Beenden): Das Wichtigste zum Schluss: Wie verlässt man Vim? Tippe im Normal-Modus ((:))((q)) (quit) und drücke ((Enter)).\nNotiz: In dieser App ist das Verlassen von Vim nicht nötig, jedoch ist es sehr wichtig zu wissen.");
                    break;
                case 8:
                    EndVimTutorial();
                    break;
                default:
                    break;
            }
        }

        private void EndVimTutorial()
        {
            _isTutorialMode = false;
            RenderTutorialTask("🎉 Tutorial abgeschlossen! 🎉\nNun kannst du die Grundenlange von Vim und kannst anfangen dich an die Steuerung zu gewöhnen. Mit ein wenig Übung und Willenskraft, wirst du dadurch produktiver, aber vor allem musst du nicht mehr so oft zur Maus greifen.");

            var duration = DateTime.Now - _tutorialStart;

            int baseScore = 12000;
            int score = baseScore - (int)(duration.TotalSeconds * 30) - (_tutorialMouseClicks * 2000) - (_tutorialKeystrokes * 20) - _tutorialPenalty;

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
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var timePanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
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
                Orientation = Avalonia.Layout.Orientation.Horizontal,
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
                Orientation = Avalonia.Layout.Orientation.Horizontal,
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
                Orientation = Avalonia.Layout.Orientation.Horizontal,
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
            {
                PnlTutorialStatsContent.Children.Add(new TextBlock
                {
                    Text = "NEUER HIGHSCORE!",
                    Foreground = SolidColorBrush.Parse("#32A852"),
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }
            else
            {
                PnlTutorialStatsContent.Children.Add(new TextBlock
                {
                    Text = $"Bisheriger Highscore: {playerData.Settings.VimTutorialHighscore}",
                    Foreground = Brushes.Gray,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            PnlTutorialStats.IsVisible = true;
            UpdateVimUI();
        }

        private void CheckTutorialProgress()
        {
            if (!_isTutorialMode) return;
            string t = TutorialEditor.Text;

            switch (_tutorialStep)
            {
                case 1:
                    if (_vimMode == VimMode.Normal && (t.Contains("// erledigt") || t.Contains("//erledigt")) && !t.Contains("change_me_to_insert"))
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
                    if (_vimMode == VimMode.Normal && t.Contains("404;") && !t.Contains("404X") && t.Contains("char c;") && !t.Contains("// loesche_nur_mich"))
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
                default:
                    break;
            }
        }

        // --- LEVEL DESIGNER ---

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
                    string combinedText = $"{TxtDesignSqlSetup.Text}\n{TxtDesignSqlSample.Text}\n{TxtDesignSqlVerify.Text}\n{SqlQueryEditor.Text}";
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
                    AddSqlOutput("System", "> SQL Level Designer geladen. Wähle einen Bereich zum Bearbeiten.", Brushes.LightGray);
                }
                else
                {
                    CodeEditor.Text = "";
                    TxtConsole.Inlines?.Clear();
                    AddToConsole("> C# Level Designer geladen. Wähle einen Bereich zum Bearbeiten.", Brushes.LightGray);
                }

                _originalSyntaxSetting = _isSqlMode ? AppSettings.IsSqlSyntaxHighlightingEnabled : AppSettings.IsSyntaxHighlightingEnabled;

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
                    if (_currentSqlDraft.InitialRelationalModel != null && _currentSqlDraft.InitialRelationalModel.Count > 0)
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
                        string combinedText = $"{TxtDesignSqlSetup.Text}\n{TxtDesignSqlSample.Text}\n{TxtDesignSqlVerify.Text}\n{SqlQueryEditor.Text}";
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
            string currentJson = _isSqlMode ? JsonSerializer.Serialize(_currentSqlDraft) : JsonSerializer.Serialize(_currentDraft);
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
                string url = _isSqlMode ? "https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_LEVEL_DESIGNER_GUIDE" : "https://github.com/OnlyCook/abitur-elite-code/wiki/CS_LEVEL_DESIGNER_GUIDE";
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
                AddToConsole($"\n> Fehler beim Öffnen des Guides: {ex.Message}", Brushes.Orange);
            }
        }

        private async void BtnDesignerExport_Click(object sender, RoutedEventArgs e)
        {
            if (_isSqlMode)
            {
                if ((_currentSqlDraft.Name?.Length ?? 0) < 1 || (_currentSqlDraft.Author?.Length ?? 0) < 1)
                {
                    AddSqlOutput("Error", "⚠ Export abgelehnt: 'Level Name' und 'Autor' müssen gesetzt sein.", Brushes.Orange);
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
                {
                    AddSqlOutput("System", "⚠ Hinweis: Diagramm konnte nicht aktualisiert werden.", Brushes.Orange);
                }

                _verifiedSqlDraftState.PlantUmlSource = _currentSqlDraft.PlantUmlSource;
                _verifiedSqlDraftState.PlantUmlSvgContent = _currentSqlDraft.PlantUmlSvgContent;

                SqlLevelDesigner.ExportLevel(_currentDraftPath, _verifiedSqlDraftState, _verifiedExpectedSchema, _verifiedExpectedResult);
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
            {
                AddToConsole("\n⚠ Hinweis: Einige Diagramme konnten nicht aktualisiert werden.", Brushes.Orange);
            }

            _verifiedDraftState.PlantUmlSources = new List<string>(_currentDraft.PlantUmlSources);
            _verifiedDraftState.PlantUmlSvgContents = new List<string>(_currentDraft.PlantUmlSvgContents);

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
            AddToConsole("\n> Level erfolgreich exportiert! (.elitelvl)", Brushes.LightGreen);
            TxtDesignerStatus.Text = "Exportiert";
            BtnDesignerExport.IsEnabled = true;
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
                string svg = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);

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
            catch { return false; }
        }

        private void SwitchDesignerMode(DesignerSource source, TextBox targetBox, string enterMessage)
        {
            if (_activeDesignerSource == source)
            {
                targetBox.Text = ActiveEditor.Text;
                _activeDesignerSource = DesignerSource.None;
                ActiveEditor.Text = "";

                if (_isSqlMode) AddSqlOutput("System", "> Editor geleert. Wähle eine Datei zum Bearbeiten.", Brushes.LightGray);
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

        private void BtnEditSqlSetup_Click(object sender, RoutedEventArgs e)
        {
            SwitchDesignerMode(DesignerSource.SqlSetup, TxtDesignSqlSetup, "> Editor: Setup-Script geladen (Ausführen deaktiviert).");
        }

        private void BtnEditSqlVerify_Click(object sender, RoutedEventArgs e)
        {
            SwitchDesignerMode(DesignerSource.SqlVerify, TxtDesignSqlVerify, "> Editor: Verifizierungs-Abfrage geladen (Ausführen deaktiviert).");
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

        private void BtnResetValidation_Click(object sender, RoutedEventArgs e)
        {
            string defaultVal = "private static bool ValidateLevel(Assembly assembly, out string feedback)\n{\n    feedback = \"Gut gemacht!\";\n    return true;\n}";
            TxtDesignValidation.Text = defaultVal;

            if (_activeDesignerSource == DesignerSource.Validation)
            {
                CodeEditor.Text = defaultVal;
            }
            AddToConsole("\n> Validierungs-Code auf Standard zurückgesetzt.", Brushes.LightGray);
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

            BtnRun.IsVisible = (_activeDesignerSource == DesignerSource.TestingCode || _activeDesignerSource == DesignerSource.SqlExpected || _activeDesignerSource == DesignerSource.SqlSample);

            if (_activeDesignerSource == DesignerSource.StarterCode) SetIcon(BtnEditStarter, "ic_exit.svg");
            else if (_activeDesignerSource == DesignerSource.Validation) SetIcon(BtnEditValidation, "ic_exit.svg");
            else if (_activeDesignerSource == DesignerSource.TestingCode) SetIcon(BtnEditTesting, "ic_exit.svg");
            else if (_activeDesignerSource == DesignerSource.SqlSetup) SetIcon(BtnEditSqlSetup, "ic_exit.svg");
            else if (_activeDesignerSource == DesignerSource.SqlVerify) SetIcon(BtnEditSqlVerify, "ic_exit.svg");
            else if (_activeDesignerSource == DesignerSource.SqlSample) SetIcon(BtnEditSqlSample, "ic_exit.svg");

            if (AppSettings.IsErrorHighlightingEnabled && !_isSqlMode)
            {
                UpdateDiagnostics();
            }
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

                if (_activeDesignerSource == DesignerSource.SqlVerify) _currentSqlDraft.VerificationQuery = SqlQueryEditor.Text;
                else _currentSqlDraft.VerificationQuery = TxtDesignSqlVerify.Text;

                if (_activeDesignerSource == DesignerSource.SqlSample) _currentSqlDraft.SampleSolution = SqlQueryEditor.Text;
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
            if (!_isSqlMode)
            {
                draftSvgs = _currentDraft.MaterialDiagrams.Select(d => d.PlantUmlSvgContent).ToList();
            }

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
                if (_currentDraft.PlantUmlSvgContents != null && _currentDraft.PlantUmlSvgContents.Count > 0 && !string.IsNullOrEmpty(_currentDraft.PlantUmlSvgContents[0]))
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
                if (_isSqlMode) AddSqlOutput("System", $"> Limit erreicht (Max {MaxPrerequisites} Voraussetzungen).", Brushes.Orange);
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
                    if (trimmed.StartsWith("@startuml") || trimmed.StartsWith("@startchen") || trimmed.StartsWith("@starter"))
                    {
                        lines.Insert(i + 1, "skinparam backgroundcolor transparent");
                        if (trimmed.StartsWith("@startuml") && !source.Contains("skinparam classAttributeIconSize 0"))
                        {
                            lines.Insert(i + 2, "skinparam classAttributeIconSize 0");
                        }
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
            var sb = new System.Text.StringBuilder();
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
                string svgContent = await AbiturEliteCode.cs.PlantUmlHelper.GenerateSvgFromCodeAsync(preparedCode);

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

            PnlDesignerDiagramTabs.Children.Add(CreateTab("Haupt", 0, _activeDiagramIndex == 0));

            if (!_isSqlMode)
            {
                for (int i = 0; i < _currentDraft.MaterialDiagrams.Count; i++)
                {
                    PnlDesignerDiagramTabs.Children.Add(CreateTab($"Mat {i + 1}", i + 1, _activeDiagramIndex == i + 1));
                }

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
                    {
                        _currentDraft.MaterialDiagrams[listIndex].PlantUmlSource = content;
                    }
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
                    {
                        content = _currentDraft.MaterialDiagrams[listIndex].PlantUmlSource;
                    }
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

        private void LoadCustomLevelFromFile(string path)
        {
            if (path.EndsWith(".eliteslvl", StringComparison.OrdinalIgnoreCase))
            {
                string json = File.ReadAllText(path);

                if (!json.TrimStart().StartsWith("{")) json = LevelEncryption.Decrypt(json);

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    int customId = path.GetHashCode();
                    if (customId > 0) customId *= -1;

                    var loadedLevel = new SqlLevel
                    {
                        Id = customId,
                        Title = root.TryGetProperty("Title", out var titleProp) ? titleProp.GetString() : "Unbekannt",
                        Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                        MaterialDocs = root.TryGetProperty("MaterialDocs", out var matProp) ? matProp.GetString() : "",
                        SetupScript = root.TryGetProperty("SetupScript", out var setupProp) ? setupProp.GetString() : "",
                        VerificationQuery = root.TryGetProperty("VerificationQuery", out var vqProp) ? vqProp.GetString() : "",
                        SkipCode = "CUST",
                        Section = "Eigene Levels",
                        Prerequisites = new List<string>(),
                        ExpectedSchema = new List<SqlExpectedColumn>(),
                        ExpectedResult = new List<string[]>(),
                        DiagramPaths = new List<string>(),
                        PlantUMLSources = new List<string>()
                    };

                    if (root.TryGetProperty("Prerequisites", out var prereqElem))
                        foreach (var p in prereqElem.EnumerateArray()) loadedLevel.Prerequisites.Add(p.GetString());

                    if (root.TryGetProperty("ExpectedSchema", out var schemaElem))
                    {
                        foreach (var col in schemaElem.EnumerateArray())
                        {
                            loadedLevel.ExpectedSchema.Add(new SqlExpectedColumn
                            {
                                Name = col.GetProperty("Name").GetString(),
                                Type = col.GetProperty("Type").GetString(),
                                StrictName = col.GetProperty("StrictName").GetBoolean()
                            });
                        }
                    }

                    if (root.TryGetProperty("ExpectedResult", out var resElem))
                    {
                        foreach (var row in resElem.EnumerateArray())
                        {
                            var arr = new string[row.GetArrayLength()];
                            int i = 0;
                            // replace commas with periods cuz globalization issues
                            foreach (var cell in row.EnumerateArray())
                            {
                                arr[i++] = cell.GetString()?.Replace(",", ".");
                            }
                            loadedLevel.ExpectedResult.Add(arr);
                        }
                    }

                    if (root.TryGetProperty("Author", out var authorElem))
                        _currentCustomAuthor = authorElem.GetString();

                    if (root.TryGetProperty("DiagramPaths", out var svgsListElem))
                    {
                        int idx = 0;
                        foreach (var svgElem1 in svgsListElem.EnumerateArray())
                        {
                            string svgContent = svgElem1.GetString();
                            if (!string.IsNullOrEmpty(svgContent))
                            {
                                string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}_{idx}.svg");
                                File.WriteAllText(tempSvgPath, svgContent);
                                loadedLevel.DiagramPaths.Add(tempSvgPath);
                            }
                            idx++;
                        }
                    }

                    if (root.TryGetProperty("PlantUMLSources", out var srcListElem))
                    {
                        foreach (var s in srcListElem.EnumerateArray()) loadedLevel.PlantUMLSources.Add(s.GetString());
                    }

                    _isCustomLevelMode = true;
                    _nextCustomLevelPath = null;

                    LoadSqlLevel(loadedLevel);
                    AddSqlOutput("System", $"> Custom Level geladen: {loadedLevel.Title}", Brushes.LightGreen);
                }
                return;
            }

            string json2 = File.ReadAllText(path);

            if (!json2.TrimStart().StartsWith("{")) json2 = LevelEncryption.Decrypt(json2);

            using (var doc = JsonDocument.Parse(json2))
            {
                var root = doc.RootElement;
                int customId = path.GetHashCode();
                if (customId > 0) customId *= -1;

                var loadedLevel = new Level
                {
                    Id = customId,
                    Title = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : (root.TryGetProperty("Title", out var titleProp2) ? titleProp2.GetString() : "Unbekannt"),
                    Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                    StarterCode = root.TryGetProperty("StarterCode", out var scProp) ? scProp.GetString() : "",
                    MaterialDocs = root.TryGetProperty("MaterialDocs", out var matProp) ? matProp.GetString() : "",
                    SkipCode = "CUST",
                    Section = "Eigene Levels",
                    Prerequisites = new List<string>(),
                    AuxiliaryIds = new List<string>(),
                    DiagramPaths = new List<string>(),
                    PlantUMLSources = new List<string>()
                };

                if (root.TryGetProperty("Author", out var authorElem))
                    _currentCustomAuthor = authorElem.GetString();

                if (root.TryGetProperty("Prerequisites", out var prereqElem))
                    foreach (var p in prereqElem.EnumerateArray())
                        loadedLevel.Prerequisites.Add(p.GetString());

                if (root.TryGetProperty("PlantUmlSvg", out var svgElem))
                {
                    string svgContent = svgElem.GetString();
                    if (!string.IsNullOrEmpty(svgContent))
                    {
                        string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}.svg");
                        File.WriteAllText(tempSvgPath, svgContent);

                        if (loadedLevel.DiagramPaths == null) loadedLevel.DiagramPaths = new List<string>();
                        loadedLevel.DiagramPaths.Add(tempSvgPath);
                    }
                }

                _currentCustomSvgs = new List<string>();
                if (root.TryGetProperty("MaterialDiagramSvgs", out var svgsElem))
                {
                    foreach (var s in svgsElem.EnumerateArray())
                        _currentCustomSvgs.Add(s.GetString());
                }

                if (root.TryGetProperty("PlantUmlSvgs", out var svgsListElem))
                {
                    int idx = 0;
                    foreach (var svgElem1 in svgsListElem.EnumerateArray())
                    {
                        string svgContent = svgElem1.GetString();
                        if (!string.IsNullOrEmpty(svgContent))
                        {
                            string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}_{idx}.svg");
                            File.WriteAllText(tempSvgPath, svgContent);
                            loadedLevel.DiagramPaths.Add(tempSvgPath);
                        }
                        idx++;
                    }
                }
                else if (root.TryGetProperty("PlantUmlSvg", out var singleSvgElem)) // fallback
                {
                    string svgContent = singleSvgElem.GetString();
                    if (!string.IsNullOrEmpty(svgContent))
                    {
                        string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}.svg");
                        File.WriteAllText(tempSvgPath, svgContent);
                        loadedLevel.DiagramPaths.Add(tempSvgPath);
                    }
                }

                if (root.TryGetProperty("PlantUmlSources", out var srcListElem))
                {
                    foreach (var s in srcListElem.EnumerateArray()) loadedLevel.PlantUMLSources.Add(s.GetString());
                }
                else if (root.TryGetProperty("PlantUmlSource", out var singleSrcElem)) // fallback
                {
                    loadedLevel.PlantUMLSources.Add(singleSrcElem.GetString());
                }

                _currentCustomValidationCode = root.TryGetProperty("ValidationCode", out var valProp) ? valProp.GetString() : "";
                _isCustomLevelMode = true;
                _nextCustomLevelPath = null;

                LoadLevel(loadedLevel);
                AddToConsole($"\n> Custom Level geladen: {loadedLevel.Title}", Brushes.LightGreen);
            }
        }

        private async void ShowCustomSectionCompletedDialog()
        {
            var dialog = new Window
            {
                Title = "Sektion Abgeschlossen",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#202124"),
                CornerRadius = new CornerRadius(8)
            };

            var rootStack = new StackPanel { Spacing = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20) };

            rootStack.Children.Add(new TextBlock
            {
                Text = "🎉 Gut gemacht!",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextTitle,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            rootStack.Children.Add(new TextBlock
            {
                Text = "Du hast alle Levels in diesem Ordner abgeschlossen.",
                FontSize = 15,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            var btnClose = new Button
            {
                Content = "Schließen",
                Background = BrushTextTitle,
                Foreground = Brushes.White,
                Padding = new Thickness(20, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(4)
            };
            btnClose.Click += (_, __) => dialog.Close();

            rootStack.Children.Add(btnClose);
            dialog.Content = new Border { Child = rootStack };

            BtnNextLevel.IsVisible = false;
            await dialog.ShowDialog(this);
        }

        // --- SQL LOGIC ---

        private void LoadSqlLevel(SqlLevel level)
        {
            // check if leaving level 4 unresolved (completes the mission to not annoy user)
            if (currentSqlLevel?.Id == 4 && !playerData.Settings.SqlSpoilerHintDismissed)
            {
                playerData.Settings.SqlSpoilerHintDismissed = true;
                SaveSystem.Save(playerData);
            }

            SaveCurrentProgress();

            UpdateFocusedColumn(null, null);

            _consecutiveSqlFails = 0;

            currentSqlLevel = level;
            UpdateNavigationButtons();

            if (_isCustomLevelMode)
            {
                if (customPlayerData.UserSqlCode.ContainsKey(level.Title))
                {
                    SqlQueryEditor.Text = customPlayerData.UserSqlCode[level.Title];
                }
                else
                {
                    SqlQueryEditor.Text = "";
                }
            }
            else
            {
                if (playerData.UserSqlCode.ContainsKey(level.Id))
                {
                    SqlQueryEditor.Text = playerData.UserSqlCode[level.Id];
                }
                else
                {
                    SqlQueryEditor.Text = "";
                }
            }
            PnlSqlOutput.Children.Clear();

            PnlTask.Children.Clear();

            if (_isCustomLevelMode)
            {
                PnlTask.Children.Add(new SelectableTextBlock
                {
                    Text = level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled),
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    Margin = new Thickness(0)
                });

                if (!string.IsNullOrEmpty(_currentCustomAuthor))
                {
                    PnlTask.Children.Add(new SelectableTextBlock
                    {
                        Text = $"S{level.Id}. {level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)}",
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        Foreground = BrushTextNormal,
                        Margin = new Thickness(0, 0, 0, 15)
                    });
                }
                else
                {
                    if (PnlTask.Children.Last() is Control last) last.Margin = new Thickness(0, 0, 0, 20);
                }
            }
            else
            {
                // standard level header
                PnlTask.Children.Add(new SelectableTextBlock
                {
                    Text = $"S{level.Id}. {level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)}",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    Margin = new Thickness(0, 0, 0, 15)
                });
            }

            PnlTaskRelationalModel.Children.Clear();
            PnlUmlRelationalModel.Children.Clear();

            _currentRelationalModel.Clear();
            if (!level.IsRelationalModelReadOnly)
            {
                if (_isCustomLevelMode && customPlayerData.UserSqlModels.ContainsKey(level.Title))
                {
                    try
                    {
                        _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(customPlayerData.UserSqlModels[level.Title]) ?? new List<RTable>();
                    }
                    catch { }
                }
                else if (!_isCustomLevelMode && playerData.UserSqlModels.ContainsKey(level.Id))
                {
                    try
                    {
                        _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(playerData.UserSqlModels[level.Id]) ?? new List<RTable>();
                    }
                    catch { }
                }

                if (_currentRelationalModel.Count == 0 && level.InitialRelationalModel != null && level.InitialRelationalModel.Count > 0)
                {
                    string json = JsonSerializer.Serialize(level.InitialRelationalModel);
                    _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(json);
                }
            }
            else if (level.InitialRelationalModel != null && level.InitialRelationalModel.Count > 0)
            {
                string json = JsonSerializer.Serialize(level.InitialRelationalModel);
                _currentRelationalModel = JsonSerializer.Deserialize<List<RTable>>(json);
            }

            _initialRelationalModelJson = JsonSerializer.Serialize(_currentRelationalModel);

            UpdateSqlAutocompleteSchema();

            // initial rendering for active tab
            if (MainTabs.SelectedIndex == 0) RenderRelationalModel(PnlTaskRelationalModel, level.IsRelationalModelReadOnly);
            else if (MainTabs.SelectedIndex == 1) RenderRelationalModel(PnlUmlRelationalModel, level.IsRelationalModelReadOnly);

            WrapPanel tagsPanel = BuildTagsPanel(level.Difficulty, null, level.DiagramTags, true);
            if (tagsPanel != null) PnlTask.Children.Add(tagsPanel);

            RenderRichText(PnlTask, level.Description);

            // materials
            GenerateMaterials(new Level
            {
                MaterialDocs = level.MaterialDocs,
                AuxiliaryIds = level.AuxiliaryIds,
                Prerequisites = level.Prerequisites,
                OptionalPrerequisites = level.OptionalPrerequisites
            });

            // diagrams
            PnlDiagramSwitch.IsVisible = false;

            ImgDiagram.Source = null; // reset first
            bool diagramLoaded = false;

            if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
            {
                var loadedImage = LoadDiagramImage(level.DiagramPaths[0]);
                if (loadedImage != null)
                {
                    ImgDiagram.Source = loadedImage;
                    diagramLoaded = true;
                }
            }

            _currentScale = 1.0;
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

            TxtNoDiagram.IsVisible = !diagramLoaded;

            if (_isCustomLevelMode)
            {
                AddSqlOutput("System", $"Level geladen.\nDatenbank zurückgesetzt.", Brushes.Gray);
            }
            else
            {
                AddSqlOutput("System", $"Level S{level.Id} (Code: {level.SkipCode}) geladen.\nDatenbank zurückgesetzt.", Brushes.Gray);
            }

            HideSpoilerHint();
            _spoilerDelayMet = false;
            _spoilerDelayTimer.Stop();

            if (!AppSettings.IsSqlAntiSpoilerEnabled && !playerData.Settings.SqlSpoilerHintDismissed)
            {
                if (level.Id == 3 || level.Id == 4)
                {
                    _spoilerDelayTimer.Start();
                }
            }
        }

        private void RunSqlQuery()
        {
            string userQuery = SqlQueryEditor.Text.Trim();
            if (string.IsNullOrEmpty(userQuery)) return;

            AddSqlOutput("Nutzer", userQuery, Brushes.White, true);

            var result = SqlLevelTester.Run(currentSqlLevel, userQuery);

            if (result.ResultTable != null)
            {
                AddSqlTable(result.ResultTable);
            }

            if (result.Success)
            {
                _consecutiveSqlFails = 0;
                AddSqlOutput("System", result.Feedback, Brushes.LightGreen);

                if (_isCustomLevelMode)
                {
                    if (!customPlayerData.CompletedCustomSqlLevels.Contains(currentSqlLevel.Title))
                    {
                        customPlayerData.CompletedCustomSqlLevels.Add(currentSqlLevel.Title);
                        SaveSystem.SaveCustom(customPlayerData);
                    }

                    AddSqlOutput("System", "🎉 Custom Level erfolgreich abgeschlossen!", Brushes.LightGreen);

                    UpdateNavigationButtons();
                    if (_nextCustomLevelPath != "SECTION_COMPLETE" && !string.IsNullOrEmpty(_nextCustomLevelPath))
                    {
                        AddSqlOutput("System", "> Nächstes Level verfügbar.", Brushes.LightGray);
                    }
                    else if (_nextCustomLevelPath == "SECTION_COMPLETE")
                    {
                        BtnNextLevel.Content = "SEKTION ABSCHLIESSEN ✓";
                        BtnNextLevel.IsVisible = true;
                    }

                    return;
                }

                if (!playerData.CompletedSqlLevelIds.Contains(currentSqlLevel.Id))
                    playerData.CompletedSqlLevelIds.Add(currentSqlLevel.Id);

                var nextLvl = sqlLevels.FirstOrDefault(l => l.SkipCode == currentSqlLevel.NextLevelCode);

                if (nextLvl != null)
                {
                    if (!playerData.UnlockedSqlLevelIds.Contains(nextLvl.Id))
                    {
                        playerData.UnlockedSqlLevelIds.Add(nextLvl.Id);
                        AddSqlOutput("System", $"🔓 Level S{nextLvl.Id} freigeschaltet!", Brushes.LightGreen);
                    }

                    if (nextLvl.Section != currentSqlLevel.Section)
                    {
                        AddSqlOutput("System", "🎉 Sektion abgeschlossen!", Brushes.LightGreen);
                        BtnNextLevel.Content = "NÄCHSTE SEKTION →";
                    }
                    else
                    {
                        BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                    }
                }
                else
                {
                    // no next level
                    AddSqlOutput("System", "🎉 Kurs abgeschlossen!", Brushes.LightGreen);
                    BtnNextLevel.Content = "KURS ABSCHLIESSEN ✓";
                }

                BtnNextLevel.IsVisible = true;
                BtnNextLevel.IsEnabled = true;
                SaveSystem.Save(playerData);
            }
            else
            {
                if (result.Feedback != null && result.Feedback.Contains("Das Ergebnis stimmt nicht mit der Erwartung überein"))
                {
                    _consecutiveSqlFails++;
                }
                else
                {
                    _consecutiveSqlFails = 0;
                }

                // format error
                string displayFeedback = result.Feedback ?? "Unbekannter Fehler.";
                if (Regex.IsMatch(displayFeedback, @"^(?:SQLite Error|SQL Fehler)\s*\d+:", RegexOptions.IgnoreCase))
                {
                    // strip unnecessary prefix
                    displayFeedback = Regex.Replace(displayFeedback, @"^(?:SQLite Error|SQL Fehler)\s*\d+:\s*", "", RegexOptions.IgnoreCase);

                    // attempt to map the error to a line number by locating the problematic token
                    var match = Regex.Match(displayFeedback, @"(?:column:|table:|near)\s*['""]?([a-zA-Z0-9_]+)['""]?", RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        string token = match.Groups[1].Value;
                        int index = userQuery.IndexOf(token, StringComparison.OrdinalIgnoreCase);

                        if (index != -1)
                        {
                            int lineNumber = userQuery.Substring(0, index).Count(c => c == '\n') + 1;

                            // capitalize first letter if it is a letter
                            if (displayFeedback.Length > 0 && char.IsLetter(displayFeedback[0]))
                                displayFeedback = char.ToUpper(displayFeedback[0]) + displayFeedback.Substring(1);

                            displayFeedback = $"Zeile {lineNumber}: {displayFeedback}";
                        }
                    }
                }

                DataTable expectedData = null;
                if (currentSqlLevel.ExpectedResult != null && currentSqlLevel.ExpectedResult.Count > 0)
                {
                    expectedData = new DataTable();

                    if (currentSqlLevel.ExpectedSchema != null && currentSqlLevel.ExpectedSchema.Count > 0)
                    {
                        foreach (var col in currentSqlLevel.ExpectedSchema)
                            expectedData.Columns.Add(col.Name, typeof(string));
                    }
                    else
                    {
                        for (int i = 0; i < currentSqlLevel.ExpectedResult[0].Length; i++)
                            expectedData.Columns.Add($"Spalte {i + 1}", typeof(string));
                    }

                    foreach (var row in currentSqlLevel.ExpectedResult)
                    {
                        expectedData.Rows.Add(row);
                    }
                }

                AddSqlOutput("Error", displayFeedback, Brushes.Orange, false, expectedData);
            }
        }

        private DataTable ExecuteDbQuery(SqliteConnection conn, string sql)
        {
            var dt = new DataTable();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }

        private void AddSqlOutput(string author, string text, IBrush color, bool isCode = false, DataTable expectedTable = null)
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
                    if (authorBlock != null && authorBlock.Text == "System")
                    {
                        targetContainer = lastContainer;
                    }
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
                if (expectedTable != null && _consecutiveSqlFails >= 3 && text.Contains("Das Ergebnis stimmt nicht mit der Erwartung überein"))
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

                    btnExpected.PointerEntered += (s, e) => {
                        btnExpected.Background = SolidColorBrush.Parse("#4A4A4A");
                        ToolTip.SetIsOpen(btnExpected, true); // force open immediately
                    };
                    btnExpected.PointerExited += (s, e) => {
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
                    toolTipBorder.Child = BuildTableGrid(expectedTable, currentSqlLevel.ExpectedSchema?.Select(c => c.Type).ToList());

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

        private Grid BuildTableGrid(DataTable table, List<string> manualTypes = null)
        {
            var grid = new Grid();

            // create columns
            for (int i = 0; i < table.Columns.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            }

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            // --- HEADERS ---
            for (int col = 0; col < table.Columns.Count; col++)
            {
                string mysqlType = "VARCHAR(255)";

                if (manualTypes != null && col < manualTypes.Count)
                {
                    mysqlType = manualTypes[col].ToUpper();
                }
                else
                {
                    var colType = table.Columns[col].DataType;
                    if (colType == typeof(string) && table.Rows.Count > 0)
                    {
                        bool isDouble = false;
                        bool isInt = true;
                        foreach (DataRow row in table.Rows)
                        {
                            string val = row[col]?.ToString()?.Replace(",", ".");
                            if (string.IsNullOrEmpty(val)) continue;
                            if (!int.TryParse(val, out _)) isInt = false;
                            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) isDouble = true;
                        }

                        if (isInt && !isDouble) colType = typeof(int);
                        else if (isDouble) colType = typeof(double);
                    }
                    mysqlType = GetMySqlTypeLabel(colType);
                }

                var headerBorder = new Border
                {
                    Background = SolidColorBrush.Parse("#252526"),
                    Padding = new Thickness(12, 8),
                    BorderBrush = SolidColorBrush.Parse("#333333"),
                    BorderThickness = new Thickness(0, 0, col == table.Columns.Count - 1 ? 0 : 1, 1)
                };

                var headerStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 2
                };

                // column name
                headerStack.Children.Add(new SelectableTextBlock
                {
                    Text = table.Columns[col].ColumnName,
                    FontWeight = FontWeight.Bold,
                    Foreground = BrushTextNormal,
                    FontSize = 13
                });

                // mysql data type
                headerStack.Children.Add(new TextBlock
                {
                    Text = mysqlType,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    FontFamily = new FontFamily(MonospaceFontFamily)
                });

                headerBorder.Child = headerStack;

                Grid.SetRow(headerBorder, 0);
                Grid.SetColumn(headerBorder, col);
                grid.Children.Add(headerBorder);
            }

            // --- DATA ROWS ---
            for (int i = 0; i < table.Rows.Count; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                // alternating bg color
                var rowBackground = (i % 2 == 0) ? (IBrush)Brushes.Transparent : SolidColorBrush.Parse("#1A1A1A");

                for (int col = 0; col < table.Columns.Count; col++)
                {
                    var cellBorder = new Border
                    {
                        Background = rowBackground,
                        Padding = new Thickness(12, 8),
                        BorderBrush = SolidColorBrush.Parse("#2A2A2A"),
                        BorderThickness = new Thickness(0, 0, col == table.Columns.Count - 1 ? 0 : 1, 0)
                    };

                    var cellText = new SelectableTextBlock
                    {
                        Text = table.Rows[i][col].ToString(),
                        Foreground = Brushes.White,
                        FontSize = 14,
                        FontFamily = new FontFamily(MonospaceFontFamily)
                    };

                    // handle null values visually
                    if (table.Rows[i][col] == DBNull.Value || table.Rows[i][col]?.ToString() == "NULL")
                    {
                        cellText.Text = "NULL";
                        cellText.Foreground = Brushes.Gray;
                        cellText.FontStyle = FontStyle.Italic;
                    }

                    cellBorder.Child = cellText;

                    Grid.SetRow(cellBorder, i + 1);
                    Grid.SetColumn(cellBorder, col);
                    grid.Children.Add(cellBorder);
                }
            }

            return grid;
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

        private async void GridSplitter_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control splitter)
            {
                _hoveredSplitter = splitter;

                // dont do nothing while dragging
                if (_isDraggingSplitter)
                    return;

                // delay to prevent regular mouse movement triggering
                await Task.Delay(150);

                if (splitter.IsPointerOver && !_isDraggingSplitter)
                    splitter.Classes.Add("hover-active");
            }
        }

        private void GridSplitter_PointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Control splitter)
            {
                // freeze hover state while dragging
                if (_isDraggingSplitter && _activeDraggingSplitter == splitter)
                    return;

                splitter.Classes.Remove("hover-active");

                if (_hoveredSplitter == splitter)
                    _hoveredSplitter = null;
            }
        }

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
                {
                    if (!PnlRelationalTip.IsVisible)
                    {
                        _relationalTipDelayTimer.Stop();
                        _relationalTipDelayTimer.Start();
                    }
                }
            }
        }

        private bool CheckRelationalModel(int levelId)
        {
            if (levelId < 13 || levelId > 27) return false;

            // normalize strings (ignore case and whitespaces)
            string Normalize(string s) => s?.Trim().ToLower() ?? "";

            var currentModel = _currentRelationalModel.Select(t => new {
                Name = Normalize(t.Name),
                Columns = t.Columns.Select(c => new {
                    Name = Normalize(c.Name),
                    c.IsPk,
                    c.IsFk
                }).OrderBy(c => c.Name).ToList()
            }).OrderBy(t => t.Name).ToList();

            var expectedTables = new List<(string Name, (string ColName, bool IsPk, bool IsFk)[] Cols)>();

            switch (levelId)
            {
                case 13:
                    expectedTables.Add(("Schueler", new[] { ("id", true, false), ("name", false, false), ("klasse", false, false) }));
                    expectedTables.Add(("Buch", new[] { ("id", true, false), ("titel", false, false) }));
                    expectedTables.Add(("ausleihe", new[] { ("schuelerid", true, true), ("buchid", true, true), ("datum", false, false) }));
                    break;
                case 14:
                case 15:
                    expectedTables.Add(("Vip", new[] { ("id", true, false), ("name", false, false) }));
                    expectedTables.Add(("Reservierung", new[] { ("vipid", true, true), ("tischnr", false, false) }));
                    break;
                case 16:
                    expectedTables.Add(("Gast", new[] { ("id", true, false), ("name", false, false), ("stadt", false, false) }));
                    expectedTables.Add(("Ticket", new[] { ("id", true, false), ("gastid", false, true), ("bereich", false, false) }));
                    break;
                case 17:
                    expectedTables.Add(("Vip", new[] { ("id", true, false), ("name", false, false) }));
                    expectedTables.Add(("Reservierung", new[] { ("vipid", true, true), ("bereich", false, false), ("tischnr", false, false) }));
                    break;
                case 18:
                    expectedTables.Add(("Produkt", new[] { ("id", true, false), ("bezeichnung", false, false), ("preis", false, false) }));
                    expectedTables.Add(("Position", new[] { ("id", true, false), ("produktid", false, true), ("menge", false, false) }));
                    break;
                case 19:
                    expectedTables.Add(("Bestellung", new[] { ("id", true, false), ("datum", false, false) }));
                    expectedTables.Add(("Position", new[] { ("id", true, false), ("preis", false, false), ("menge", false, false), ("bestellungid", false, true) }));
                    break;
                case 20:
                    expectedTables.Add(("Produkt", new[] { ("id", true, false), ("name", false, false) }));
                    expectedTables.Add(("Position", new[] { ("id", true, false), ("menge", false, false), ("produktid", false, true) }));
                    break;
                case 21:
                    expectedTables.Add(("Produkt", new[] { ("id", true, false), ("kategorie", false, false) }));
                    expectedTables.Add(("Position", new[] { ("id", true, false), ("menge", false, false), ("produktid", false, true) }));
                    break;
                case 22:
                    expectedTables.Add(("Produkt", new[] { ("id", true, false), ("kategorie", false, false), ("preis", false, false) }));
                    expectedTables.Add(("Position", new[] { ("id", true, false), ("menge", false, false), ("produktid", false, true) }));
                    break;
                case 23:
                case 24:
                case 25:
                    expectedTables.Add(("Gast", new[] { ("id", true, false), ("name", false, false) }));
                    expectedTables.Add(("Buchung", new[] { ("id", true, false), ("anreise", false, false), ("abreise", false, false), ("gastid", false, true) }));
                    break;
                case 26:
                    expectedTables.Add(("Buchung", new[] { ("id", true, false), ("anreise", false, false), ("abreise", false, false) }));
                    break;
                case 27:
                    expectedTables.Add(("Gast", new[] { ("id", true, false), ("name", false, false) }));
                    expectedTables.Add(("Buchung", new[] { ("id", true, false), ("anreise", false, false), ("abreise", false, false), ("gastid", false, true), ("zimmerid", false, true) }));
                    expectedTables.Add(("Zimmer", new[] { ("id", true, false), ("nummer", false, false) }));
                    break;
                default:
                    return false;
            }

            if (currentModel.Count != expectedTables.Count) return false;

            var expectedModel = expectedTables.Select(t => new {
                Name = Normalize(t.Name),
                Columns = t.Cols.Select(c => new {
                    Name = Normalize(c.ColName),
                    IsPk = c.IsPk,
                    IsFk = c.IsFk
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
                svgImage.Source = SvgSource.Load($"avares://AbiturEliteCode/{iconPath}", null);
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
            if (_isDesignerMode && _currentSqlDraft != null)
            {
                showEditControls = _currentSqlDraft.IsRelationalModelReadOnly;
            }

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
                    Content = LoadIcon(_currentSqlDraft.IsRelationalModelReadOnly ? "assets/icons/ic_lock.svg" : "assets/icons/ic_lock_open.svg", 16),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(6),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursor.Parse("Hand"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                ToolTip.SetTip(btnLock, _currentSqlDraft.IsRelationalModelReadOnly ? "Relationales Modell für Spieler freigeben (Bearbeitbar)" : "Relationales Modell für Spieler sperren (Read-Only)");
                btnLock.Click += (s, e) =>
                {
                    _currentSqlDraft.IsRelationalModelReadOnly = !_currentSqlDraft.IsRelationalModelReadOnly;
                    btnLock.Content = LoadIcon(_currentSqlDraft.IsRelationalModelReadOnly ? "assets/icons/ic_lock.svg" : "assets/icons/ic_lock_open.svg", 16);
                    ToolTip.SetTip(btnLock, _currentSqlDraft.IsRelationalModelReadOnly ? "Relationales Modell für Spieler freigeben (Bearbeitbar)" : "Relationales Modell für Spieler sperren (Read-Only)");
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
                var sb = new System.Text.StringBuilder();
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

                var topLevel = TopLevel.GetTopLevel(this);
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
                _btnGlobalPk.Click += (s, e) => {
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
                _btnGlobalFk.Click += (s, e) => {
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
                    tb.Inlines.Add(new Run { Text = table.Name, Foreground = BrushTextHighlight, FontWeight = FontWeight.Bold });
                    tb.Inlines.Add(new Run { Text = " (", Foreground = BrushTextNormal });

                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        var col = table.Columns[i];
                        var run = new Run { Text = col.Name + (col.IsFk ? "#" : ""), Foreground = BrushTextNormal };
                        if (col.IsPk) run.TextDecorations = TextDecorations.Underline;
                        tb.Inlines.Add(run);
                        if (i < table.Columns.Count - 1) tb.Inlines.Add(new Run { Text = ", ", Foreground = BrushTextNormal });
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
                txtTableName.TextChanged += (s, e) => {
                    string filtered = new string(txtTableName.Text?.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray() ?? Array.Empty<char>());
                    if (txtTableName.Text != filtered)
                    {
                        txtTableName.Text = filtered;
                        txtTableName.CaretIndex = txtTableName.Text.Length;
                    }
                    table.Name = txtTableName.Text;
                    TriggerRelationalAutoSave();
                };

                txtTableName.GotFocus += (s, e) => {
                    UpdateFocusedColumn(null, null);
                    _focusedRTable = table;
                };

                // handle shortcut to jump to first column or create one
                txtTableName.KeyDown += (s, e) => {
                    if (e.KeySymbol == "(" || (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D8) || e.Key == Key.OemOpenBrackets)
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

                if (_focusedRTable == table) Dispatcher.UIThread.Post(() => {
                    txtTableName.Focus();
                    txtTableName.CaretIndex = txtTableName.Text?.Length ?? 0;
                });

                rowPanel.Children.Add(txtTableName);
                rowPanel.Children.Add(new TextBlock { Text = " (", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15 });

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

                btnAddCol.Click += (s, e) => {
                    if (ToolTip.GetTip(btnAddCol)?.ToString() == "Spalte löschen")
                    {
                        if (table.Columns.Count > 0)
                        {
                            var lastCol = table.Columns.Last();
                            if (_focusedRColumn == lastCol) UpdateFocusedColumn(null, null);
                            table.Columns.Remove(lastCol);

                            // focus previous element
                            if (table.Columns.Count > 0)
                            {
                                UpdateFocusedColumn(table.Columns.Last(), null);
                            }
                            else
                            {
                                _focusedRTable = table;
                            }

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
                        VerticalContentAlignment = VerticalAlignment.Center,
                    };

                    // wrap in border to fake underline
                    var pkUnderlineBorder = new Border
                    {
                        BorderThickness = col.IsPk ? new Thickness(0, 0, 0, 1) : new Thickness(0),
                        BorderBrush = BrushTextNormal,
                        Child = txtCol
                    };

                    txtCol.TextChanged += (s, e) => {
                        string filtered = new string(txtCol.Text?.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray() ?? Array.Empty<char>());
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
                    txtCol.KeyDown += (s, e) => {
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
                        else if (e.KeySymbol == ")" || (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D9) || e.Key == Key.OemCloseBrackets)
                        {
                            e.Handled = true;
                            int tableIndex = _currentRelationalModel.IndexOf(table);
                            if (tableIndex == _currentRelationalModel.Count - 1)
                            {
                                var newTable = new RTable { Name = "", Columns = new List<RColumn> { new RColumn { Name = "id", IsPk = true } } };
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
                    if (_focusedRColumn == col) Dispatcher.UIThread.Post(() => {
                        txtCol.Focus();
                        txtCol.CaretIndex = txtCol.Text?.Length ?? 0;
                    });

                    colStack.Children.Add(pkUnderlineBorder);

                    if (col.IsFk)
                    {
                        colStack.Children.Add(new TextBlock { Text = "#", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15 });
                    }

                    if (i < table.Columns.Count - 1)
                    {
                        colStack.Children.Add(new TextBlock { Text = ", ", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15 });
                    }

                    rowPanel.Children.Add(colStack);
                }

                rowPanel.Children.Add(btnAddCol);
                rowPanel.Children.Add(new TextBlock { Text = ")", Foreground = BrushTextNormal, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily(MonospaceFontFamily), FontSize = 15 });

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
                btnDelTable.Click += (s, e) => {
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
            btnAddTable.Click += (s, e) => {
                var newTable = new RTable
                {
                    Name = "",
                    Columns = new List<RColumn>
                    {
                        new RColumn
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

        private void UpdateFocusedColumn(RColumn col, TextBox tb)
        {
            _focusedRColumn = col;
            _focusedRColumnTextBox = tb;
            _focusedRTable = null;
            UpdateGlobalKeyButtons();
        }

        private void UpdateGlobalKeyButtons()
        {
            if (_btnGlobalPk != null)
            {
                _btnGlobalPk.IsEnabled = _focusedRColumn != null;
                _btnGlobalPk.Background = (_focusedRColumn != null && _focusedRColumn.IsPk) ? SolidColorBrush.Parse("#D08770") : SolidColorBrush.Parse("#3C3C3C");
            }
            if (_btnGlobalFk != null)
            {
                _btnGlobalFk.IsEnabled = _focusedRColumn != null;
                _btnGlobalFk.Background = (_focusedRColumn != null && _focusedRColumn.IsFk) ? SolidColorBrush.Parse("#B48EAD") : SolidColorBrush.Parse("#3C3C3C");
            }
        }

        private void RenderExpectedTable(int focusRow = -1, int focusCol = -1, bool isCell = false)
        {
            GridExpectedTable.Children.Clear();
            GridExpectedTable.RowDefinitions.Clear();
            GridExpectedTable.ColumnDefinitions.Clear();

            if (_currentSqlDraft.ExpectedSchema == null) _currentSqlDraft.ExpectedSchema = new List<SqlExpectedColumn>();
            if (_currentSqlDraft.ExpectedResult == null) _currentSqlDraft.ExpectedResult = new List<string[]>();

            // always make sure one empty row and col is at the end (input buffer)
            if (_currentSqlDraft.ExpectedSchema.Count == 0 || !string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema.Last().Name))
            {
                _currentSqlDraft.ExpectedSchema.Add(new SqlExpectedColumn { Name = "", Type = "VARCHAR(255)" });
                for (int i = 0; i < _currentSqlDraft.ExpectedResult.Count; i++)
                {
                    var list = _currentSqlDraft.ExpectedResult[i].ToList();
                    list.Add("");
                    _currentSqlDraft.ExpectedResult[i] = list.ToArray();
                }
            }
            if (_currentSqlDraft.ExpectedResult.Count == 0 || _currentSqlDraft.ExpectedResult.Last().Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                _currentSqlDraft.ExpectedResult.Add(new string[_currentSqlDraft.ExpectedSchema.Count]);
            }

            int cols = _currentSqlDraft.ExpectedSchema.Count;
            int rows = _currentSqlDraft.ExpectedResult.Count;

            for (int c = 0; c < cols; c++) GridExpectedTable.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            GridExpectedTable.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Control elementToFocus = null;

            // headers
            for (int c = 0; c < cols; c++)
            {
                int colIndex = c;
                var headerStack = new StackPanel
                {
                    Spacing = 2,
                    Margin = new Thickness(2)
                };

                var txtName = new TextBox
                {
                    // wrap the strict name in apostrophes visually
                    Text = _currentSqlDraft.ExpectedSchema[c].StrictName ? $"'{_currentSqlDraft.ExpectedSchema[c].Name}'" : _currentSqlDraft.ExpectedSchema[c].Name,
                    Watermark = "LEER",
                    Width = 120,
                    FontSize = 12,
                    Padding = new Thickness(4, 2),
                    Background = SolidColorBrush.Parse("#141414"),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = SolidColorBrush.Parse("#333"),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center
                };

                txtName.TextChanged += (s, e) => {
                    if (colIndex < _currentSqlDraft.ExpectedSchema.Count)
                    {
                        string raw = txtName.Text ?? "";
                        int quoteCount = raw.Count(ch => ch == '\'' || ch == '"');

                        // only allow valid mysql characters (letters, digits, underscores)
                        string filtered = new string(raw.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
                        bool strict = _currentSqlDraft.ExpectedSchema[colIndex].StrictName;

                        // determine strictness based on the current quote state compared to the previous state
                        if (quoteCount > 0)
                        {
                            if (strict && quoteCount < 2) strict = false;
                            else if (!strict) strict = true;
                        }
                        else
                        {
                            strict = false;
                        }

                        // format correctly via apostrophes
                        string newText = strict ? $"'{filtered}'" : filtered;

                        if (txtName.Text != newText)
                        {
                            txtName.Text = newText;
                            txtName.CaretIndex = txtName.Text.Length;
                        }

                        _currentSqlDraft.ExpectedSchema[colIndex].Name = filtered;
                        _currentSqlDraft.ExpectedSchema[colIndex].StrictName = strict;

                        InvalidateSqlExport();
                        if (colIndex == cols - 1 && !string.IsNullOrWhiteSpace(filtered)) RenderExpectedTable(-1, colIndex, false);
                    }
                };
                txtName.LostFocus += (s, e) => CleanAndRenderExpectedTable();
                if (!isCell && focusCol == c) elementToFocus = txtName;

                var cmbType = new ComboBox
                {
                    Width = 120,
                    FontSize = 10,
                    Background = SolidColorBrush.Parse("#141414"),
                    Foreground = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    BorderBrush = SolidColorBrush.Parse("#333")
                };
                cmbType.Items.Add("VARCHAR(255)");
                cmbType.Items.Add("INT");
                cmbType.Items.Add("DOUBLE");
                cmbType.Items.Add("DATE");
                cmbType.SelectedItem = _currentSqlDraft.ExpectedSchema[c].Type ?? "VARCHAR(255)";
                cmbType.SelectionChanged += (s, e) => {
                    if (colIndex < _currentSqlDraft.ExpectedSchema.Count)
                    {
                        _currentSqlDraft.ExpectedSchema[colIndex].Type = cmbType.SelectedItem as string;
                        InvalidateSqlExport();
                    }
                };

                headerStack.Children.Add(txtName);
                headerStack.Children.Add(cmbType);
                Grid.SetRow(headerStack, 0);
                Grid.SetColumn(headerStack, c);
                GridExpectedTable.Children.Add(headerStack);
            }

            // cells
            for (int r = 0; r < rows; r++)
            {
                GridExpectedTable.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                int rowIndex = r;
                for (int c = 0; c < cols; c++)
                {
                    int colIndex = c;

                    // check if current column lacks name
                    bool isColBuffer = string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema[c].Name);
                    bool isRowBuffer = (r == rows - 1);

                    var txtCell = new TextBox
                    {
                        Text = _currentSqlDraft.ExpectedResult[r].Length > c ? _currentSqlDraft.ExpectedResult[r][c] : "",
                        Watermark = (isColBuffer || isRowBuffer) ? "LEER" : "NULL",
                        IsEnabled = !isColBuffer,
                        Width = 120,
                        FontSize = 12,
                        Margin = new Thickness(2),
                        Padding = new Thickness(4, 2),
                        Background = SolidColorBrush.Parse("#141414"),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = SolidColorBrush.Parse("#333"),
                        VerticalContentAlignment = VerticalAlignment.Center,
                        HorizontalContentAlignment = HorizontalAlignment.Center
                    };

                    txtCell.TextChanged += (s, e) => {
                        // force period instead of comma
                        if (txtCell.Text != null && txtCell.Text.Contains(","))
                        {
                            int caret = txtCell.CaretIndex;
                            txtCell.Text = txtCell.Text.Replace(",", ".");
                            txtCell.CaretIndex = caret;
                        }

                        if (rowIndex < _currentSqlDraft.ExpectedResult.Count && colIndex < _currentSqlDraft.ExpectedResult[rowIndex].Length)
                        {
                            if (_currentSqlDraft.ExpectedResult[rowIndex].Length > colIndex)
                                _currentSqlDraft.ExpectedResult[rowIndex][colIndex] = txtCell.Text;

                            InvalidateSqlExport();

                            if (colIndex == cols - 1 && !string.IsNullOrWhiteSpace(txtCell.Text) && colIndex < _currentSqlDraft.ExpectedSchema.Count && string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema[colIndex].Name))
                            {
                                _currentSqlDraft.ExpectedSchema[colIndex].Name = $"Spalte{colIndex + 1}";
                            }

                            if ((rowIndex == rows - 1 || colIndex == cols - 1) && !string.IsNullOrWhiteSpace(txtCell.Text))
                            {
                                RenderExpectedTable(rowIndex, colIndex, true);
                            }
                        }
                    };
                    txtCell.LostFocus += (s, e) => CleanAndRenderExpectedTable();
                    if (isCell && focusRow == r && focusCol == c) elementToFocus = txtCell;

                    Grid.SetRow(txtCell, r + 1);
                    Grid.SetColumn(txtCell, c);
                    GridExpectedTable.Children.Add(txtCell);
                }
            }

            if (elementToFocus != null)
            {
                Dispatcher.UIThread.Post(() => {
                    elementToFocus.Focus();
                    if (elementToFocus is TextBox tb) tb.CaretIndex = tb.Text?.Length ?? 0;
                });
            }
        }

        private void InvalidateSqlExport()
        {
            _designerAutoSaveTimer.Stop(); _designerAutoSaveTimer.Start();
            BtnDesignerExport.IsEnabled = false;
            TxtDesignerStatus.Text = "Entwurf geändert (Nicht verifiziert)";
            _verifiedSqlDraftState = null;
        }

        private bool _isCleaningTable = false;
        private void CleanAndRenderExpectedTable()
        {
            if (_isCleaningTable) return;
            _isCleaningTable = true;
            bool changed = false;

            // removes in-between empty ROWS dynamically
            for (int r = _currentSqlDraft.ExpectedResult.Count - 2; r >= 0; r--)
            {
                if (_currentSqlDraft.ExpectedResult[r].All(string.IsNullOrWhiteSpace))
                {
                    _currentSqlDraft.ExpectedResult.RemoveAt(r);
                    changed = true;
                }
            }
            // removes in-between empty COLUMNS dynamically
            for (int c = _currentSqlDraft.ExpectedSchema.Count - 2; c >= 0; c--)
            {
                if (string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema[c].Name))
                {
                    bool colEmpty = true;
                    foreach (var row in _currentSqlDraft.ExpectedResult)
                    {
                        if (row.Length > c && !string.IsNullOrWhiteSpace(row[c])) { colEmpty = false; break; }
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
            }

            if (changed) RenderExpectedTable();
            _isCleaningTable = false;
        }

        private void BtnEditSqlSample_Click(object sender, RoutedEventArgs e)
        {
            SwitchDesignerMode(DesignerSource.SqlSample, TxtDesignSqlSample, "> Editor: Musterlösung geladen. 'Ausführen' verifiziert nun das Level.");
        }

        private async void CheckForUpdatesBackground()
        {
            var result = await UpdateManager.CheckForUpdatesAsync();
            if (result.UpdateAvailable)
            {
                _updateAvailable = true;
                _latestVersion = result.LatestVersion;
                _updateDownloadUrl = result.DownloadUrl;
                Dispatcher.UIThread.Post(() => BadgeSettings.IsVisible = true);
            }
        }

        private async Task ShowManualUpdateDialog(UpdateManager.UpdateStatus status, string downloadUrl, Window owner)
        {
            var dialog = new Window
            {
                Title = "Manuelles Update erforderlich",
                Width = 520,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = SolidColorBrush.Parse("#252526"),
                CornerRadius = new CornerRadius(8)
            };

            string msg = status switch
            {
                UpdateManager.UpdateStatus.UnsupportedOS => "Auto-Updates werden auf macOS und Linux nicht unterstützt.\n\nBitte lade die neue Version manuell herunter und lösche die alten Dateien gegebenenfalls.",
                UpdateManager.UpdateStatus.NoWritePermission => "Abitur Elite Code hat keine Schreibrechte in diesem Ordner.\n\nBitte lade das Update manuell herunter oder verschiebe das Programm in einen anderen Ordner.",
                UpdateManager.UpdateStatus.NetworkError => "Fehler beim Herunterladen des Updates.\n\nMöglicherweise ist GitHub nicht erreichbar oder deine Internetverbindung ist unterbrochen.",
                _ => "",
            };
            var rootGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(20)
            };

            var contentPanel = new StackPanel
            {
                Spacing = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentPanel.Children.Add(new TextBlock
            {
                Text = "Manuelles Update erforderlich",
                FontWeight = FontWeight.Bold,
                Foreground = BrushTextHighlight,
                FontSize = 18
            });
            contentPanel.Children.Add(new TextBlock
            {
                Text = msg,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                LineHeight = 20
            });
            rootGrid.Children.Add(contentPanel);

            var btnPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10,
                Margin = new Thickness(0, 20, 0, 0)
            };
            Grid.SetRow(btnPanel, 1);

            var btnGuide = new Button
            {
                Content = "Guide öffnen",
                Background = SolidColorBrush.Parse("#007ACC"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            btnGuide.Click += (_, __) =>
            {
                string url = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "https://github.com/OnlyCook/abitur-elite-code?tab=readme-ov-file#----windows-auto-update"
                    : "https://github.com/OnlyCook/abitur-elite-code?tab=readme-ov-file#------linux--macos";
                UpdateManager.OpenBrowser(url);
            };

            var btnDownload = new Button
            {
                Content = "Im Browser herunterladen",
                Background = SolidColorBrush.Parse("#32A852"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            btnDownload.Click += (_, __) => UpdateManager.OpenBrowser(downloadUrl);

            var btnClose = new Button
            {
                Content = "Schließen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            btnClose.Click += (_, __) => dialog.Close();

            btnPanel.Children.Add(btnGuide);
            btnPanel.Children.Add(btnDownload);
            btnPanel.Children.Add(btnClose);

            rootGrid.Children.Add(btnPanel);
            dialog.Content = rootGrid;

            await dialog.ShowDialog(owner);
        }

        private void EvaluateSpoilerHintVisibility()
        {
            if (!_isSqlMode || currentSqlLevel == null || AppSettings.IsSqlAntiSpoilerEnabled)
            {
                HideSpoilerHint();
                return;
            }

            // only show in level 3 or 4 if tab is on "Aufgabe" tab and 4 seconds passed
            if ((currentSqlLevel.Id == 3 || currentSqlLevel.Id == 4) && _spoilerDelayMet && MainTabs.SelectedIndex == 0)
            {
                PnlSpoilerTip.IsVisible = true;
                if (!_spoilerActiveTimer.IsEnabled) _spoilerActiveTimer.Start();
            }
            else
            {
                HideSpoilerHint();
            }
        }

        private void HideSpoilerHint()
        {
            PnlSpoilerTip?.IsVisible = false;
            _spoilerActiveTimer?.Stop();
        }

        private void BtnEnableSpoilerMode_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.IsSqlAntiSpoilerEnabled = true;
            playerData.Settings.SqlSpoilerHintDismissed = true;
            SaveSystem.Save(playerData);

            HideSpoilerHint();

            if (currentSqlLevel != null)
            {
                LoadSqlLevel(currentSqlLevel);
            }
        }

        private void BtnCloseSpoilerTip_Click(object sender, RoutedEventArgs e)
        {
            playerData.Settings.SqlSpoilerHintDismissed = true;
            SaveSystem.Save(playerData);
            _spoilerDelayMet = false;
            HideSpoilerHint();
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
}
