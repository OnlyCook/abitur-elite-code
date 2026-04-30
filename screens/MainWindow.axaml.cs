using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AbiturEliteCode.cs;
using AbiturEliteCode.cs.MainWindow;
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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Data.Sqlite;
using Timer = System.Timers.Timer;

namespace AbiturEliteCode;

public partial class MainWindow : Window
{
    private const string MonospaceFontFamily =
        "Consolas, Menlo, Monaco, DejaVu Sans Mono, Roboto Mono, Courier New, monospace";

    private const int MaxPrerequisites = 8;
    private DesignerSource _activeDesignerSource = DesignerSource.None;
    private int _activeDiagramIndex;
    private Control? _activeDraggingSplitter;
    private BracketHighlightRenderer _bracketHighlightRenderer;
    private Button _btnGlobalFk;
    private Button _btnGlobalPk;

    private CancellationTokenSource? _compilationCts;
    private int _consecutiveSqlFails;
    private AutocompleteGhostGenerator _csharpAutocompleteGenerator;
    private AutocompleteService _csharpAutocompleteService;
    private VimBlockCaretRenderer _csharpBlockCaret;
    private string _currentCustomAuthor = "";
    private List<string> _currentCustomSvgs;

    private string _currentCustomValidationCode;
    private int _currentDiagramIndex;
    private LevelDraft _currentDraft = new();
    private string _currentDraftPath = "";
    private List<RTable> _currentRelationalModel = new();
    private double _currentScale = 1.0;

    private SqlLevelDraft _currentSqlDraft = new();
    private readonly Timer _designerAutoSaveTimer;
    private readonly DispatcherTimer _designerSyncTimer;
    private DispatcherTimer _diagnosticTimer;
    private EscapeSequenceTransformer _escapeSequenceTransformer;
    private RColumn _focusedRColumn;
    private TextBox _focusedRColumnTextBox;
    private RTable _focusedRTable;
    private GhostCharacterTransformer _ghostCharTransformer;

    private bool _hasRunOnce;

    private Control? _hoveredSplitter;
    private IndentationGuideRenderer _indentationGuideRenderer;
    private string _initialRelationalModelJson = "";

    private bool _isCleaningTable;
    private bool _isCustomLevelMode;
    private bool _isDesignerMode;
    private bool _isDragging;
    private bool _isDraggingSplitter;
    private bool _isKeyboardTabSwitch;

    private bool _isLoadingDesigner;

    private bool _isSqlMode;
    private bool _isTutorialMode;
    private GridLength _lastCsharpRowHeight = new(180);
    private Point _lastMousePoint;
    private string _lastSavedDraftJson = "";
    private GridLength _lastSqlRowHeight = new(250);
    private string _latestVersion = "";

    private int _mouseTabSwitchCount;
    private string _newlyCreatedLevelPath;
    private string _nextCustomLevelPath;
    private bool _originalErrorSetting;
    private bool _originalSyntaxSetting;

    private readonly Timer _relationalAutoSaveTimer;

    private readonly DispatcherTimer _relationalTipDelayTimer;
    private readonly DispatcherTimer _relationalTipDisplayTimer;
    private Image? _relationalValidationIcon;
    private SemanticClassHighlightingTransformer _semanticClassTransformer;
    private readonly DispatcherTimer _spoilerActiveTimer;
    private bool _spoilerDelayMet;
    private readonly DispatcherTimer _tabTipDisplayTimer;

    private readonly DispatcherTimer _spoilerDelayTimer;
    private AutocompleteGhostGenerator _sqlAutocompleteGenerator;
    private AutocompleteService _sqlAutocompleteService;
    private VimBlockCaretRenderer _sqlBlockCaret;
    private bool _suppressCsharpAutocomplete;
    private bool _suppressSqlAutocomplete;
    private TextMarkerService _textMarkerService;
    private VimBlockCaretRenderer _tutorialBlockCaret;
    private int _tutorialKeystrokes;
    private int _tutorialMouseClicks;
    private int _tutorialPenalty;
    private DateTime _tutorialStart;
    private int _tutorialStep;
    private UnusedCodeTransformer _unusedCodeTransformer;

    private bool _updateAvailable;
    private string _updateDownloadUrl = "";
    private LevelDraft _verifiedDraftState;
    private List<string[]> _verifiedExpectedResult;
    private List<SqlExpectedColumn> _verifiedExpectedSchema;
    private SqlLevelDraft _verifiedSqlDraftState;
    private string _vimClipboard = "";
    private string _vimCommandBuffer = ""; // for multi char commands
    private int _vimDesiredColumn = -1;
    private VimMode _vimMode = VimMode.Normal;
    private VimMode _vimPreviousMode = VimMode.Normal;
    private int _vimVisualStartOffset = -1;
    private readonly Timer autoSaveTimer;
    private readonly SolidColorBrush BrushBgPanel = SolidColorBrush.Parse("#202124");
    private readonly SolidColorBrush BrushTextHighlight = SolidColorBrush.Parse("#6495ED"); // blue

    private readonly SolidColorBrush BrushTextNormal = SolidColorBrush.Parse("#E6E6E6");
    private readonly SolidColorBrush BrushTextTitle = SolidColorBrush.Parse("#32A852"); // green
    private Level currentLevel;
    private SqlLevel currentSqlLevel;
    private readonly CustomPlayerData customPlayerData;

    private readonly ScaleTransform ImgScale;
    private readonly TranslateTransform ImgTranslate;
    private List<Level> levels;
    private readonly PlayerData playerData;
    private List<SqlLevel> sqlLevels;

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
        AppSettings.IsDiscordRpcEnabled = playerData.Settings.IsDiscordRpcEnabled;

        // check if display is too small and scale down automatically
        var screen = Screens?.Primary;
        if (screen != null)
        {
            double screenWidth = screen.WorkingArea.Width;
            double screenHeight = screen.WorkingArea.Height;

            double baseWidth = 1250.0;
            double baseHeight = 850.0;

            bool resolutionChanged = Math.Abs(playerData.Settings.LastScreenWidth - screenWidth) > 1 ||
                                     Math.Abs(playerData.Settings.LastScreenHeight - screenHeight) > 1;

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
                    AppSettings.UiScale = Math.Max(0.5, Math.Round(targetScale, 1));

                Width = screenWidth;
                Height = screenHeight - 50; // buffer for taskbar/header
            }
            else // standard behavior (monitor size >= 1300x900)
            {
                Width = baseWidth;
                Height = baseHeight;
            }
        }
        else // fallback (no screen detected)
        {
            Width = 1250;
            Height = 850;
        }

        if (AppSettings.AutoCheckForUpdates) CheckForUpdatesBackground();

        if (AppSettings.IsDiscordRpcEnabled) DiscordRpcManager.Initialize();

        ApplyUiScale();
        ApplySyntaxHighlighting();
        ApplySqlSyntaxHighlighting();
        UpdateVimState();
        BuildVimCheatSheet();

        ConfigureEditor();
        ConfigureSqlQueryEditor();
        ConfigureTutorialEditor();
        UpdateShortcutsAndTooltips();

        autoSaveTimer = new Timer(2000)
        {
            AutoReset = false
        };
        autoSaveTimer.Elapsed += (s, e) => Dispatcher.UIThread.InvokeAsync(SaveCurrentProgress);

        _relationalAutoSaveTimer = new Timer(2000)
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
            Interval = TimeSpan.FromSeconds(2)
        };
        _relationalTipDelayTimer.Tick += (s, e) =>
        {
            _relationalTipDelayTimer.Stop();
            if (MainTabs.SelectedIndex == 0 || MainTabs.SelectedIndex == 1) ShowRelationalTip();
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

        _tabTipDisplayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _tabTipDisplayTimer.Tick += (s, e) =>
        {
            _tabTipDisplayTimer.Stop();
            PnlTabTip.IsVisible = false;
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
            if (margin is LineNumberMargin lineMargin)
                lineMargin.Margin = new Thickness(0, 1, 0, 0);

        int maxId = playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1;
        var startLevel = levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0];
        LoadLevel(startLevel);

        Opened += (s, e) =>
        {
            CodeEditor.Focus();
            // fix unscaled window for certain aspect ratios
            Dispatcher.UIThread.Post(() =>
            {
                InvalidateMeasure();
                RootScaleTransform?.InvalidateMeasure();
            }, DispatcherPriority.Render);
        };

        // global shortcuts
        AddHandler(KeyDownEvent, (s, e) =>
        {
            // level navigation
            if (e.Key == Key.Enter)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && BtnPrevLevel.IsVisible && BtnPrevLevel.IsEnabled)
                {
                    BtnPrevLevel_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && BtnNextLevel.IsVisible && BtnNextLevel.IsEnabled)
                {
                    BtnNextLevel_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return;
                }
            }

            // global ui scaling via ctrl +/- (only outside of editors)
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                bool isEditorFocused = CodeEditor?.IsFocused == true || CodeEditor?.TextArea?.IsFocused == true ||
                                       SqlQueryEditor?.IsFocused == true ||
                                       SqlQueryEditor?.TextArea?.IsFocused == true ||
                                       TutorialEditor?.IsFocused == true || TutorialEditor?.TextArea?.IsFocused == true;

                if (!isEditorFocused)
                {
                    if (e.Key == Key.OemPlus || e.Key == Key.Add)
                    {
                        AppSettings.UiScale = Math.Min(2.0, Math.Round(AppSettings.UiScale + 0.1, 1));
                        ApplyUiScale();
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
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

                    BtnDiagram1.Background = _currentDiagramIndex == 0
                        ? SolidColorBrush.Parse("#007ACC")
                        : SolidColorBrush.Parse("#3C3C3C");
                    BtnDiagram2.Background = _currentDiagramIndex == 1
                        ? SolidColorBrush.Parse("#007ACC")
                        : SolidColorBrush.Parse("#3C3C3C");
                    BtnDiagram3.Background = _currentDiagramIndex == 2
                        ? SolidColorBrush.Parse("#007ACC")
                        : SolidColorBrush.Parse("#3C3C3C");
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
        AddHandler(
            PointerPressedEvent,
            (s, e) =>
            {
                var source = e.Source as Control;
                if (source is TextBox || source is Button || source?.Parent is Button)
                    return;

                if (source?.Name == "DiagramPanel" || source?.Name == "ImgDiagram")
                    return;

                Dispatcher.UIThread.Post(() => CodeEditor.Focus());
            },
            RoutingStrategies.Tunnel
        );

        AddHandler(PointerPressedEvent, (s, e) =>
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

        AddHandler(PointerReleasedEvent, (s, e) =>
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
        _designerAutoSaveTimer = new Timer(2000)
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
                            && (_isSqlMode
                                ? !_currentSqlDraft.Prerequisites.Contains(t)
                                : !_currentDraft.Prerequisites.Contains(t)))
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

        CmbSqlValidationMode.SelectionChanged += (s, e) =>
        {
            if (_isLoadingDesigner) return;
            _currentSqlDraft.IsDmlMode = CmbSqlValidationMode.SelectedIndex == 1;
            PnlDesignSqlVerify.IsVisible = _currentSqlDraft.IsDmlMode;
            _designerAutoSaveTimer.Stop();
            _designerAutoSaveTimer.Start();
        };

        Closed += (s, e) => DiscordRpcManager.Deinitialize();
    }

    protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs e)
    {
        // save current progress before closing the app
        if (_isDesignerMode)
        {
            SaveDesignerDraft();
        }
        else
        {
            SaveCurrentProgress();
        }

        base.OnClosing(e);
    }

    private TextEditor ActiveEditor => _isTutorialMode ? TutorialEditor : _isSqlMode ? SqlQueryEditor : CodeEditor;

    private void UpdateTabStyles()
    {
        var tabItems = MainTabs.Items.OfType<TabItem>().Where(t => t.IsVisible).ToList();
        if (tabItems.Count == 0) return;

        // find bottom most row
        double maxY = tabItems.Max(t => t.Bounds.Y);

        foreach (var tab in tabItems)
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

    private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabs) return;

        bool wasQueryEditorFocused = SqlQueryEditor?.IsFocused == true || SqlQueryEditor?.TextArea?.IsFocused == true;

        // immediately drop focus linkage so it doesnt steal back
        if (wasQueryEditorFocused) UpdateFocusedColumn(null, null);

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

        if (wasQueryEditorFocused) Dispatcher.UIThread.Post(() => SqlQueryEditor.Focus());

        // live preview update on tab switch
        if (_isDesignerMode && (MainTabs.SelectedIndex == 0 || MainTabs.SelectedIndex == 2)) UpdateDesignerPreview();

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
            ShowTabTip();

        EvaluateSpoilerHintVisibility();
    }

    private void ShowTabTip()
    {
        if (PnlTabTip.IsVisible) return;

        PnlTabTip.IsVisible = true;

        playerData.Settings.TabTipShownCount++;
        SaveSystem.Save(playerData);

        _tabTipDisplayTimer.Start();
    }

    private void PnlTabTip_PointerEntered(object sender, PointerEventArgs e)
    {
        _tabTipDisplayTimer.Stop();
    }

    private void PnlTabTip_PointerExited(object sender, PointerEventArgs e)
    {
        _tabTipDisplayTimer.Start();
    }

    private void PnlRelationalTip_PointerEntered(object sender, PointerEventArgs e)
    {
        _relationalTipDisplayTimer.Stop();
    }

    private void PnlRelationalTip_PointerExited(object sender, PointerEventArgs e)
    {
        _relationalTipDisplayTimer.Start();
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
            try
            {
                if (!string.IsNullOrEmpty(asm.Location))
                    references.Add(MetadataReference.CreateFromFile(asm.Location));
                else
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load reference {asm.FullName}: {ex.Message}");
            }

        return references;
    }

    private void UpdateNavigationButtonTooltips()
    {
        ToolTip.SetTip(BtnPrevLevel, "Vorheriges Level (Shift + Enter)");
        if (_isSqlMode)
        {
            if (currentSqlLevel != null && currentSqlLevel.Id == SqlCurriculum.GetLevelCount())
                ToolTip.SetTip(BtnNextLevel, "Kurs abschließen (Alt + Enter)");
            else
                ToolTip.SetTip(BtnNextLevel, "Nächstes Level (Alt + Enter)");
        }
        else
        {
            if (currentLevel != null && currentLevel.Id == Curriculum.GetLevelCount())
                ToolTip.SetTip(BtnNextLevel, "Kurs abschließen (Alt + Enter)");
            else
                ToolTip.SetTip(BtnNextLevel, "Nächstes Level (Alt + Enter)");
        }
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
            if (runBtn != null) ToolTip.SetTip(runBtn, "Ausführen (F5)");
        }
    }

    private void AddToConsole(string text, IBrush color)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TxtConsole.Inlines ??= new InlineCollection();

            // split text to use explicit linebreaks
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) TxtConsole.Inlines.Add(new LineBreak());

                if (!string.IsNullOrEmpty(lines[i]))
                    TxtConsole.Inlines.Add(new Run
                    {
                        Text = lines[i],
                        Foreground = color,
                        FontFamily = new FontFamily(MonospaceFontFamily)
                    });
            }

            ConsoleScroller?.ScrollToEnd();
        });
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
                        if (playerData.UserSqlModels.ContainsKey(lvl.Id))
                            playerData.UserSqlModels[lvl.Id] = modelJson;
                        else
                            playerData.UserSqlModels.Add(lvl.Id, modelJson);
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

    private IImage LoadDiagramImage(string relativePath)
    {
        // check if its a full file path (custom levels)
        if (File.Exists(relativePath))
            try
            {
                if (relativePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var svgSource = SvgSource.Load(relativePath);
                    return new SvgImage { Source = svgSource };
                }

                return new Bitmap(relativePath);
            }
            catch
            {
                return null;
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
                    var svgSource = SvgSource.Load(uriString);
                    return new SvgImage { Source = svgSource };
                }

                return new Bitmap(AssetLoader.Open(uri));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load asset {uriString}: {ex.Message}");
        }

        return null;
    }


    private WrapPanel BuildTagsPanel(string difficulty, List<string> topics, List<string> diagrams, bool isSql)
    {
        if (difficulty == "" && (topics == null || topics.Count == 0) &&
            (diagrams == null || diagrams.Count == 0)) return null;

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
                    tooltip = isSql
                        ? "Einfache SELECT-Abfragen, meist auf einer einzelnen Tabelle."
                        : "Grundlegende Programmierkonzepte, wenig Vernetzung.";
                    break;
                case "mittel":
                    diffColor = SolidColorBrush.Parse("#d08770");
                    tooltip = isSql
                        ? "Abfragen mit JOINs, GROUP BY und einfachen Unterabfragen."
                        : "Komplexere Logik, erste Objektinteraktionen und Datenstrukturen.";
                    break;
                case "schwer":
                    diffColor = SolidColorBrush.Parse("#B43232");
                    tooltip = isSql
                        ? "Komplexe Unterabfragen, aggregierte Joins und Verschachtelungen."
                        : "Komplexe Algorithmen, Datenstrukturen (Listen/Arrays) und Architektur.";
                    break;
                case "abitur":
                    diffColor = SolidColorBrush.Parse("#8A2BE2");
                    tooltip = isSql
                        ? "Auf Abitur-Niveau: Komplexe Auswertungen über viele Relationen."
                        : "Auf Abitur-Niveau: Netzwerkkommunikation, Parsing, komplexe Objektgeflechte.";
                    break;
            }

            var diffBorder = CreateTagBorder(difficulty.ToUpper(), diffColor);
            ToolTip.SetTip(diffBorder, tooltip);
            panel.Children.Add(diffBorder);
        }

        // topic tags (max of 3, currently c# only)
        if (!isSql && topics != null)
            foreach (var topic in topics.Take(3))
                panel.Children.Add(CreateTagBorder(topic, SolidColorBrush.Parse("#007ACC")));

        // diagram tags (max of 3)
        if (diagrams != null)
            foreach (var diag in diagrams.Take(3))
                panel.Children.Add(CreateTagBorder(diag, SolidColorBrush.Parse("#555555")));

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

        SelectableTextBlock CreateTextBlock()
        {
            return new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                LineHeight = 24,
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

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
                    if (lastRun != null && !string.IsNullOrEmpty(lastRun.Text)) lastRun.Text = lastRun.Text.TrimEnd();

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
                    currentTb.Inlines.Add(new Run
                    {
                        Text = content,
                        Foreground = BrushTextNormal
                    });
            }
        }

        if (currentTb.Inlines.Count > 0) panel.Children.Add(currentTb);
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

        var diagnostics = await Task.Run(() =>
        {
            string header;
            if (isValidationMode)
            {
                header =
                    "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Reflection;\n\npublic class DesignerValidator {\n";
            }
            else
            {
                header = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n";
                if (currentLevel != null && currentLevel.Id == 26)
                    header += "public partial class FlughafenVerwaltung {\n";
            }

            string fullCode = header + code + (isValidationMode ? "\n}" : "");
            if (!isValidationMode && currentLevel != null && currentLevel.Id == 26) fullCode += "\n}";

            var userTree = CSharpSyntaxTree.ParseText(fullCode);
            var trees = new List<SyntaxTree> { userTree };

            if (currentLevel.AuxiliaryIds != null)
                foreach (var auxId in currentLevel.AuxiliaryIds)
                {
                    string auxCode = AuxiliaryImplementations.GetCode(auxId, code);
                    if (!string.IsNullOrEmpty(auxCode)) trees.Add(CSharpSyntaxTree.ParseText(auxCode));
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
                    _unusedCodeTransformer.UnusedSegments.Add(new TextSegment { StartOffset = start, Length = length });
                else
                    _textMarkerService.Add(start, length, Colors.Yellow, diag.GetMessage());
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
            if (index >= 0 && index < currentLevel.DiagramPaths.Count)
            {
                _currentDiagramIndex = index;
                ImgDiagram.Source = LoadDiagramImage(currentLevel.DiagramPaths[index]);

                BtnDiagram1.Background =
                    index == 0 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                BtnDiagram2.Background =
                    index == 1 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
                BtnDiagram3.Background =
                    index == 2 ? SolidColorBrush.Parse("#007ACC") : SolidColorBrush.Parse("#3C3C3C");
            }
    }

    private void BtnResetDiagram_Click(object sender, RoutedEventArgs e)
    {
        if (ImgScale != null && ImgTranslate != null)
        {
            if (_isSqlMode || currentLevel.NoUMLAutoScale)
                _currentScale = 1.0;
            else
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
        if (!string.IsNullOrEmpty(level.MaterialDocs)) RenderMaterialText(PnlMaterials, level.MaterialDocs);

        // prerequisites
        RenderPrerequisites(PnlMaterials, level.Prerequisites, level.OptionalPrerequisites);
    }

    private void RenderMaterialText(StackPanel targetPanel, string rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return;

        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        var normalTextBuffer = new StringBuilder();
        var hintBuffer = new StringBuilder();
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
                        lastChild.Margin = new Thickness(0);

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
                IBrush vidColor = null;

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
                    vidColor = SolidColorBrush.Parse("#b00000");
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
                    vidColor = SolidColorBrush.Parse("#5D3FD3");
                }

                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto"),
                    Margin = new Thickness(0, 2, 0, 2)
                };

                // title
                var titleStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var txtTitle = new TextBlock
                {
                    Text = "• " + lessonTitle,
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

                // video/course
                var btnVid = new Button
                {
                    Content = vidBtnText,
                    FontSize = 11,
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 0, 5, 0),
                    Background = vidColor,
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursor.Parse("Hand"),
                    IsVisible = !string.IsNullOrEmpty(vidUrl)
                };
                ToolTip.SetTip(btnVid, vidTooltip);
                btnVid.Click += (s, e) =>
                {
                    if (_isSqlMode) SqlPrerequisiteSystem.OpenUrl(vidUrl);
                    else PrerequisiteSystem.OpenUrl(vidUrl);
                };

                // docs
                var btnDoc = new Button
                {
                    Content = "Docs",
                    FontSize = 11,
                    Padding = new Thickness(8, 4),
                    Background = SolidColorBrush.Parse("#0078D4"),
                    Foreground = Brushes.White,
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursor.Parse("Hand"),
                    IsVisible = !string.IsNullOrEmpty(docUrl)
                };
                ToolTip.SetTip(btnDoc, docTooltip);
                btnDoc.Click += (s, e) =>
                {
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
            innerList.Children.Add(new Separator
                { Background = SolidColorBrush.Parse("#333"), Margin = new Thickness(0, 5, 0, 5) });

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

        _compilationCts = new CancellationTokenSource();
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
        if (!runDesignerTest && levelContext != null && levelContext.Id == 26) headerLineCount = 5;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var processingTask = Task.Run<(bool Success, ImmutableArray<Diagnostic>? Diagnostics, dynamic TestResult)>(
                () =>
                {
                    if (token.IsCancellationRequested) return (false, null, null);

                    var originalConsoleOut = Console.Out;
                    var customWriter = new ConsoleRedirectionWriter(str => { AddToConsole(str, Brushes.Cyan); });
                    Console.SetOut(customWriter);

                    string header = "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n";
                    if (!runDesignerTest && levelContext != null && levelContext.Id == 26)
                        header += "public partial class FlughafenVerwaltung {\n";

                    string fullCode = header + codeText;
                    if (!runDesignerTest && levelContext != null && levelContext.Id == 26) fullCode += "\n}";

                    var syntaxTree = CSharpSyntaxTree.ParseText(fullCode, cancellationToken: token);

                    // inject global loop breaker to catch infinite loops and avoid frying any cpus
                    var root = syntaxTree.GetRoot(token);
                    var rewriter = new LoopGuardRewriter();
                    syntaxTree = rewriter.Visit(root).SyntaxTree;

                    var trees = new List<SyntaxTree> { syntaxTree };

                    // handle auxiliary code
                    if (!runDesignerTest && !_isCustomLevelMode && levelContext.AuxiliaryIds != null)
                        foreach (var auxId in levelContext.AuxiliaryIds)
                        {
                            string auxCode = AuxiliaryImplementations.GetCode(auxId, codeText);
                            if (!string.IsNullOrEmpty(auxCode))
                                trees.Add(CSharpSyntaxTree.ParseText(auxCode, cancellationToken: token));
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
                                return (Success: false, Diagnostics: emitResult.Diagnostics, TestResult: (dynamic)null);

                            if (token.IsCancellationRequested) return (false, null, null);

                            ms.Seek(0, SeekOrigin.Begin);
                            var assembly = Assembly.Load(ms.ToArray());

                            if (runDesignerTest || useCustomValidation)
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
                                            string errorMsg = valEmit.Diagnostics.FirstOrDefault()?.GetMessage() ??
                                                              "Unbekannter Fehler";
                                            throw new Exception("Fehler im Validierungs-Code (Designer): " + errorMsg);
                                        }

                                        valMs.Seek(0, SeekOrigin.Begin);
                                        var valAssembly = Assembly.Load(valMs.ToArray());
                                        var valType = valAssembly.GetType("DesignerValidator");

                                        // dynamically find the method
                                        var valMethod = valType.GetMethods(BindingFlags.Public |
                                                                           BindingFlags.NonPublic | BindingFlags.Static)
                                            .FirstOrDefault(m => m.ReturnType == typeof(bool)
                                                                 && m.GetParameters().Length == 2
                                                                 && m.GetParameters()[0].ParameterType ==
                                                                 typeof(Assembly)
                                                                 && m.GetParameters()[1].IsOut);

                                        if (valMethod == null)
                                            throw new Exception(
                                                "Keine gültige Validierungsmethode gefunden. Signatur muss sein: bool Methode(Assembly a, out string f)");

                                        object[] args = new object[] { assembly, null };

                                        try
                                        {
                                            bool passed = (bool)valMethod.Invoke(null, args);
                                            string feedback = (string)args[1];

                                            return (Success: true, Diagnostics: null,
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
                                    return (Success: true, Diagnostics: null,
                                        TestResult: new TestResult { Success = false, Error = ex });
                                }

                            // normal level logic
                            var testResult = LevelTester.Run(levelContext.Id, assembly, codeText);
                            return (Success: true, Diagnostics: null, TestResult: testResult);
                        }
                        catch (OperationCanceledException)
                        {
                            return (false, null, null);
                        }
                    }
                }, token);

            var timeoutTask = Task.Delay(12000, token);
            var completedTask = await Task.WhenAny(processingTask, timeoutTask);

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
                            AddToConsole("✓ DESIGNER TEST BESTANDEN: " + result.TestResult.Feedback,
                                Brushes.LightGreen);
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

    private void ProcessTestResult(Level levelContext, dynamic result, TimeSpan duration)
    {
        if (result.Success)
        {
            AddToConsole($"✓ TEST BESTANDEN ({duration.TotalSeconds:F2}s): " + result.Feedback + "\n\n",
                Brushes.LightGreen);

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
                    AddToConsole("\n> Nächstes Level verfügbar.", Brushes.LightGray);
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
                // check if we are switching sections
                if (nextLvl.Section != levelContext.Section)
                {
                    AddToConsole("\n🎉 Sektion abgeschlossen! Bereit für das nächste Thema?\n", Brushes.LightGreen);
                    BtnNextLevel.Content = "NÄCHSTE SEKTION →";
                }
                else
                {
                    BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                }

                // unlock the next level
                if (!playerData.UnlockedLevelIds.Contains(nextLvl.Id))
                {
                    playerData.UnlockedLevelIds.Add(nextLvl.Id);
                    AddToConsole($"🔓 Level {nextLvl.Id} freigeschaltet!\n", Brushes.LightGreen);
                }

                AddToConsole($"Nächstes Level Code: {nextLvl.SkipCode}\n", Brushes.LightGray);

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
                ? result.Error.InnerException != null ? result.Error.InnerException.Message : result.Error.Message
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
                var sectionLevels = allCustoms.Where(c => c.Section == currentInfo.Section).OrderBy(c => c.Name)
                    .ToList();
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

        UpdateNavigationButtonTooltips();
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
                var sectionLevels = allCustoms.Where(c => c.Section == currentInfo.Section).OrderBy(c => c.Name)
                    .ToList();
                int idx = sectionLevels.FindIndex(c => c.FilePath == currentInfo.FilePath);
                if (idx > 0)
                {
                    LoadCustomLevelFromFile(sectionLevels[idx - 1].FilePath);
                    if (_isSqlMode) SqlQueryEditor.Focus();
                    else CodeEditor.Focus();
                }
            }

            return;
        }

        if (_isSqlMode && currentSqlLevel != null)
        {
            int idx = sqlLevels.IndexOf(currentSqlLevel);
            if (idx > 0) LoadSqlLevel(sqlLevels[idx - 1]);
        }
        else if (currentLevel != null)
        {
            int idx = levels.IndexOf(currentLevel);
            if (idx > 0) LoadLevel(levels[idx - 1]);
        }
    }

    private void BtnNextLevel_Click(object sender, RoutedEventArgs e)
    {
        if (BtnNextLevel.Content?.ToString() == "✓" ||
            BtnNextLevel.Content?.ToString()?.Contains("ABSCHLIESSEN") == true)
        {
            if (_isCustomLevelMode && _nextCustomLevelPath == "SECTION_COMPLETE")
                ShowCustomSectionCompletedDialog();
            else if (_isSqlMode)
                ShowSqlCourseCompletedDialog();
            else
                ShowCourseCompletedDialog();
            return;
        }

        if (_isCustomLevelMode && !string.IsNullOrEmpty(_nextCustomLevelPath))
        {
            try
            {
                LoadCustomLevelFromFile(_nextCustomLevelPath);
                if (_isSqlMode) SqlQueryEditor.Focus();
                else CodeEditor.Focus();
            }
            catch (Exception ex)
            {
                if (_isSqlMode)
                    AddSqlOutput("Error", $"> Fehler beim Laden des nächsten Levels: {ex.Message}", Brushes.Red);
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
            Title = "C# Kurs Abgeschlossen",
            Width = 500,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#202124"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };
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
                Text =
                    "Rechtlicher Hinweis: Diese Software dient ausschließlich Übungszwecken. Der Entwickler übernimmt keine Gewähr für die Vollständigkeit der Inhalte oder den tatsächlichen Erfolg in der Abiturprüfung.",
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
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };
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
                Text =
                    "Rechtlicher Hinweis: Diese Software dient ausschließlich Übungszwecken. Der Entwickler übernimmt keine Gewähr für die Vollständigkeit der Inhalte oder den tatsächlichen Erfolg in der Abiturprüfung.",
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
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };
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
        btnNo.Click += (_, __) => { dialog.Close(); };
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
        _isCustomLevelMode = false;

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

            sqlLevels ??= SqlCurriculum.GetLevels();
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
                UmlTabGrid.RowDefinitions[2].Height = GridLength.Auto;

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

            levels ??= Curriculum.GetLevels();
            if (currentLevel == null || currentLevel.Id < 0)
            {
                int maxId = playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1;
                currentLevel = levels.FirstOrDefault(l => l.Id == maxId) ?? levels[0];
            }

            LoadLevel(currentLevel);
            UpdateVimState();
        }
    }

    private Image LoadIcon(string path, double size)
    {
        var image = new Image { Width = size, Height = size, Stretch = Stretch.Uniform };
        string uriString = $"avares://AbiturEliteCode/{path}";
        try
        {
            var svgImage = new SvgImage();
            svgImage.Source = SvgSource.Load(uriString);
            image.Source = svgImage;
        }
        catch
        {
            Debug.WriteLine($"Could not load SVG: {uriString}");
        }

        return image;
    }

    private void ApplyUiScale()
    {
        var control = this.FindControl<LayoutTransformControl>("RootScaleTransform");
        if (control != null)
            control.LayoutTransform = new ScaleTransform(
                AppSettings.UiScale,
                AppSettings.UiScale
            );
    }

    private void ApplySyntaxHighlighting()
    {
        if (AppSettings.IsSyntaxHighlightingEnabled)
            CodeEditor.SyntaxHighlighting = CsharpCodeEditor.GetDarkCsharpHighlighting();
        else
            CodeEditor.SyntaxHighlighting = null;
    }

    private void UpdateSemanticHighlighting()
    {
        var classes = new HashSet<string>();

        // extract from aux code
        if (currentLevel?.AuxiliaryIds != null)
            foreach (var auxId in currentLevel.AuxiliaryIds)
            {
                string auxCode = AuxiliaryImplementations.GetCode(auxId, CodeEditor.Text);
                if (!string.IsNullOrEmpty(auxCode)) ExtractTypesFromCode(auxCode, classes);
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
            if (match.Groups.Count > 1)
                types.Add(match.Groups[1].Value);
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
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        var rootStack = new StackPanel
            { Spacing = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20) };

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

    private void RunSqlQuery()
    {
        string userQuery = SqlQueryEditor.Text.Trim();
        if (string.IsNullOrEmpty(userQuery)) return;

        AddSqlOutput("Nutzer", userQuery, Brushes.White, true);

        var result = SqlLevelTester.Run(currentSqlLevel, userQuery);

        if (result.ResultTable != null) AddSqlTable(result.ResultTable);

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
                if (nextLvl.Section != currentSqlLevel.Section)
                {
                    AddSqlOutput("System", "🎉 Sektion abgeschlossen!", Brushes.LightGreen);
                    BtnNextLevel.Content = "NÄCHSTE SEKTION →";
                }
                else
                {
                    BtnNextLevel.Content = "NÄCHSTES LEVEL →";
                }

                if (!playerData.UnlockedSqlLevelIds.Contains(nextLvl.Id))
                {
                    playerData.UnlockedSqlLevelIds.Add(nextLvl.Id);
                    AddSqlOutput("System", $"🔓 Level S{nextLvl.Id} freigeschaltet!", Brushes.LightGreen);
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
            if (result.Feedback != null &&
                result.Feedback.Contains("Das Ergebnis stimmt nicht mit der Erwartung überein"))
                _consecutiveSqlFails++;
            else
                _consecutiveSqlFails = 0;

            // format error
            string displayFeedback = result.Feedback ?? "Unbekannter Fehler.";
            if (Regex.IsMatch(displayFeedback, @"^(?:SQLite Error|SQL Fehler)\s*\d+:", RegexOptions.IgnoreCase))
            {
                // strip unnecessary prefix
                displayFeedback = Regex.Replace(displayFeedback, @"^(?:SQLite Error|SQL Fehler)\s*\d+:\s*", "",
                    RegexOptions.IgnoreCase);

                // attempt to map the error to a line number by locating the problematic token
                var match = Regex.Match(displayFeedback, @"(?:column:|table:|near)\s*['""]?([a-zA-Z0-9_]+)['""]?",
                    RegexOptions.IgnoreCase);
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
                    foreach (var col in currentSqlLevel.ExpectedSchema)
                        expectedData.Columns.Add(col.Name, typeof(string));
                else
                    for (int i = 0; i < currentSqlLevel.ExpectedResult[0].Length; i++)
                        expectedData.Columns.Add($"Spalte {i + 1}", typeof(string));

                foreach (var row in currentSqlLevel.ExpectedResult) expectedData.Rows.Add(row);
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

    private Grid BuildTableGrid(DataTable table, List<string> manualTypes = null)
    {
        var grid = new Grid();

        // create columns
        for (int i = 0; i < table.Columns.Count; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

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
                        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                            isDouble = true;
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
            var rowBackground = i % 2 == 0 ? (IBrush)Brushes.Transparent : SolidColorBrush.Parse("#1A1A1A");

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
            _btnGlobalPk.Background = _focusedRColumn != null && _focusedRColumn.IsPk
                ? SolidColorBrush.Parse("#D08770")
                : SolidColorBrush.Parse("#3C3C3C");
        }

        if (_btnGlobalFk != null)
        {
            _btnGlobalFk.IsEnabled = _focusedRColumn != null;
            _btnGlobalFk.Background = _focusedRColumn != null && _focusedRColumn.IsFk
                ? SolidColorBrush.Parse("#B48EAD")
                : SolidColorBrush.Parse("#3C3C3C");
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
        if (_currentSqlDraft.ExpectedSchema.Count == 0 ||
            !string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema.Last().Name))
        {
            _currentSqlDraft.ExpectedSchema.Add(new SqlExpectedColumn { Name = "", Type = "VARCHAR(255)" });
            for (int i = 0; i < _currentSqlDraft.ExpectedResult.Count; i++)
            {
                var list = _currentSqlDraft.ExpectedResult[i].ToList();
                list.Add("");
                _currentSqlDraft.ExpectedResult[i] = list.ToArray();
            }
        }

        if (_currentSqlDraft.ExpectedResult.Count == 0 ||
            _currentSqlDraft.ExpectedResult.Last().Any(cell => !string.IsNullOrWhiteSpace(cell)))
            _currentSqlDraft.ExpectedResult.Add(new string[_currentSqlDraft.ExpectedSchema.Count]);

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
                Text = _currentSqlDraft.ExpectedSchema[c].StrictName
                    ? $"'{_currentSqlDraft.ExpectedSchema[c].Name}'"
                    : _currentSqlDraft.ExpectedSchema[c].Name,
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

            txtName.TextChanged += (s, e) =>
            {
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
                    if (colIndex == cols - 1 && !string.IsNullOrWhiteSpace(filtered)) RenderExpectedTable(-1, colIndex);
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
            cmbType.SelectionChanged += (s, e) =>
            {
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
                bool isRowBuffer = r == rows - 1;

                var txtCell = new TextBox
                {
                    Text = _currentSqlDraft.ExpectedResult[r].Length > c ? _currentSqlDraft.ExpectedResult[r][c] : "",
                    Watermark = isColBuffer || isRowBuffer ? "LEER" : "NULL",
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

                txtCell.TextChanged += (s, e) =>
                {
                    // force period instead of comma
                    if (txtCell.Text != null && txtCell.Text.Contains(","))
                    {
                        int caret = txtCell.CaretIndex;
                        txtCell.Text = txtCell.Text.Replace(",", ".");
                        txtCell.CaretIndex = caret;
                    }

                    if (rowIndex < _currentSqlDraft.ExpectedResult.Count &&
                        colIndex < _currentSqlDraft.ExpectedResult[rowIndex].Length)
                    {
                        if (_currentSqlDraft.ExpectedResult[rowIndex].Length > colIndex)
                            _currentSqlDraft.ExpectedResult[rowIndex][colIndex] = txtCell.Text;

                        InvalidateSqlExport();

                        if (colIndex == cols - 1 && !string.IsNullOrWhiteSpace(txtCell.Text) &&
                            colIndex < _currentSqlDraft.ExpectedSchema.Count &&
                            string.IsNullOrWhiteSpace(_currentSqlDraft.ExpectedSchema[colIndex].Name))
                            _currentSqlDraft.ExpectedSchema[colIndex].Name = $"Spalte{colIndex + 1}";

                        if ((rowIndex == rows - 1 || colIndex == cols - 1) && !string.IsNullOrWhiteSpace(txtCell.Text))
                            RenderExpectedTable(rowIndex, colIndex, true);
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
            Dispatcher.UIThread.Post(() =>
            {
                elementToFocus.Focus();
                if (elementToFocus is TextBox tb) tb.CaretIndex = tb.Text?.Length ?? 0;
            });
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
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

        string msg = status switch
        {
            UpdateManager.UpdateStatus.UnsupportedOS =>
                "Auto-Updates werden auf macOS und Linux nicht unterstützt.\n\nBitte lade die neue Version manuell herunter und lösche die alten Dateien gegebenenfalls.",
            UpdateManager.UpdateStatus.NoWritePermission =>
                "Abitur Elite Code hat keine Schreibrechte in diesem Ordner.\n\nBitte lade das Update manuell herunter oder verschiebe das Programm in einen anderen Ordner.",
            UpdateManager.UpdateStatus.NetworkError =>
                "Fehler beim Herunterladen des Updates.\n\nMöglicherweise ist GitHub nicht erreichbar oder deine Internetverbindung ist unterbrochen.",
            _ => ""
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
            Orientation = Orientation.Horizontal,
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

        if (currentSqlLevel != null) LoadSqlLevel(currentSqlLevel);
    }

    private void BtnCloseSpoilerTip_Click(object sender, RoutedEventArgs e)
    {
        playerData.Settings.SqlSpoilerHintDismissed = true;
        SaveSystem.Save(playerData);
        _spoilerDelayMet = false;
        HideSpoilerHint();
    }
}

public static class CodeGuard
{
    public static CancellationToken Token { get; set; }

    public static void Check()
    {
        if (Token.IsCancellationRequested) throw new Exception("Ausführung durch Timeout oder Benutzer abgebrochen.");
    }
}