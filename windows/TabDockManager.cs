using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AbiturEliteCode;

public enum DockPosition
{
    None,
    Center,
    Left,
    Right,
    Top,
    Bottom
}

public class TabDockManager
{
    private readonly MainWindow _window;
    private readonly Panel _container;
    private readonly Canvas _ghostCanvas;
    private readonly Canvas _indicatorsCanvas;
    private readonly Border _dropPreview;
    private readonly Border _reorderIndicator;

    private TabItem? _draggedTab;
    private TabControl? _sourceTabControl;
    private Point _dragStartPoint;
    private bool _isDragging;
    private Border? _ghostElement;

    private TabControl? _targetTabControl;
    private DockPosition _dockPosition = DockPosition.None;
    private int _insertIndex = -1;
    private bool _isHardDock = false;

    private readonly Avalonia.Controls.Shapes.Path[] _dockHints = new Avalonia.Controls.Shapes.Path[4];
    private bool _isMorphAnimating = false;
    private TimeSpan? _lastFrameTime;
    private double _morphProgress = 0;
    private DockPosition _activeMorphDock = DockPosition.None;
    private DockPosition _animatingMorphDock = DockPosition.None;

    public TabDockManager(
        MainWindow window, Panel container, TabControl initialTabs,
        Canvas ghostCanvas, Canvas indicatorsCanvas, Border dropPreview, Border reorderIndicator)
    {
        _window = window;
        _container = container;
        _ghostCanvas = ghostCanvas;
        _indicatorsCanvas = indicatorsCanvas;
        _dropPreview = dropPreview;
        _reorderIndicator = reorderIndicator;

        _window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _window.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _window.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public IEnumerable<TabControl> GetAllTabControls() => GetTabControls(_container);

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            var src = e.Source as Visual;
            var tabItem = src?.GetVisualAncestors().OfType<TabItem>().FirstOrDefault() ?? src as TabItem;

            if (tabItem != null)
            {
                var tabControl = tabItem.GetVisualAncestors().OfType<TabControl>().FirstOrDefault();
                if (tabControl != null && _container.IsVisualAncestorOf(tabControl))
                {
                    // allow drag from anywhere inside the tab item header (easier to start drag)
                    if (tabItem != null)
                    {
                        _draggedTab = tabItem;
                        _sourceTabControl = tabControl;
                        _dragStartPoint = e.GetPosition(_window);
                    }
                }
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTab != null && !_isDragging)
        {
            var pos = e.GetPosition(_window);
            if (Math.Abs(pos.X - _dragStartPoint.X) > 4 || Math.Abs(pos.Y - _dragStartPoint.Y) > 4)
                StartDrag();
        }

        if (_isDragging && _ghostElement != null)
        {
            // force size all cursor during entire drag duration
            _window.Cursor = new Cursor(StandardCursorType.SizeAll);

            var pos = e.GetPosition(_window);
            Canvas.SetLeft(_ghostElement, pos.X + 10);
            Canvas.SetTop(_ghostElement, pos.Y + 10);

            UpdateDropIndicators(pos);
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            DropTab();
            e.Handled = true;
        }

        _draggedTab = null;
        _sourceTabControl = null;
        _isDragging = false;

        if (_ghostElement != null)
        {
            _ghostCanvas.Children.Clear();
            _ghostElement = null;
        }

        // transition out
        _dropPreview.Opacity = 0;
        _reorderIndicator.Opacity = 0;

        _activeMorphDock = DockPosition.None;
        _animatingMorphDock = DockPosition.None;
        _morphProgress = 0;
        _isMorphAnimating = false;

        foreach (var hint in _dockHints)
            if (hint != null)
                hint.Opacity = 0;

        _window.Cursor = Cursor.Default;
    }

    private void StartDrag()
    {
        _isDragging = true;
        string headerText = "Tab";
        if (_draggedTab?.Header is string s) headerText = s;
        else if (_draggedTab?.Header is TextBlock tb) headerText = tb.Text;

        _ghostElement = new Border
        {
            Background = SolidColorBrush.Parse("#2D2D30"),
            BorderBrush = SolidColorBrush.Parse("#6495ED"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(15, 8),
            Child = new TextBlock
            {
                Text = headerText,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 14
            }
        };

        _ghostCanvas.Children.Add(_ghostElement);

        // show hard dock indicators
        for (int i = 0; i < 4; i++)
        {
            if (_dockHints[i] == null)
            {
                _dockHints[i] = new Avalonia.Controls.Shapes.Path
                {
                    Stroke = SolidColorBrush.Parse("#6495ED"),
                    StrokeThickness = 2,
                    Opacity = 0,
                    IsHitTestVisible = false,
                    ZIndex = 100
                };
                _indicatorsCanvas.Children.Add(_dockHints[i]);
            }
            _dockHints[i].Opacity = 1;
        }

        _activeMorphDock = DockPosition.None;
        _animatingMorphDock = DockPosition.None;
        _morphProgress = 0;
        UpdateDockHintsGeometry();

        _window.Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void UpdateDropIndicators(Point pointerPos)
    {
        _targetTabControl = null;
        _dockPosition = DockPosition.None;
        _insertIndex = -1;
        _isHardDock = false;

        bool showPreview = false;
        bool showReorder = false;

        // check for hard edge docking (first)
        var containerPoint = _window.TranslatePoint(pointerPos, _container);
        if (containerPoint.HasValue)
        {
            var p = containerPoint.Value;
            double cw = _container.Bounds.Width;
            double ch = _container.Bounds.Height;
            double edgeZone = 15; // edge trigger zone

            // restrict hard dock to only 20% (0.6 - 0.4 = 0.2)
            double yCenterMin = ch * 0.40;
            double yCenterMax = ch * 0.60;
            double xCenterMin = cw * 0.40;
            double xCenterMax = cw * 0.60;

            if (p.X < edgeZone && p.Y > yCenterMin && p.Y < yCenterMax)
            {
                _dockPosition = DockPosition.Left;
                _isHardDock = true;
            }
            else if (p.X > cw - edgeZone && p.Y > yCenterMin && p.Y < yCenterMax)
            {
                _dockPosition = DockPosition.Right;
                _isHardDock = true;
            }
            else if (p.Y < edgeZone && p.X > xCenterMin && p.X < xCenterMax)
            {
                _dockPosition = DockPosition.Top;
                _isHardDock = true;
            }
            else if (p.Y > ch - edgeZone && p.X > xCenterMin && p.X < xCenterMax)
            {
                _dockPosition = DockPosition.Bottom;
                _isHardDock = true;
            }

            if (_isHardDock)
            {
                if (_activeMorphDock != _dockPosition)
                {
                    _activeMorphDock = _dockPosition;
                    _animatingMorphDock = _dockPosition;
                    StartMorphAnimation();
                }

                _dropPreview.Opacity = 0; // hide regular preview since the morph handles it
                _reorderIndicator.Opacity = 0;
                return; // skip normal tab control hit testing
            }
        }

        // return morph to normal state when leaving the hard dock edge zones
        if (_activeMorphDock != DockPosition.None)
        {
            _activeMorphDock = DockPosition.None;
            StartMorphAnimation();
        }

        foreach (var tc in GetTabControls(_container))
        {
            var p = _window.TranslatePoint(pointerPos, tc);
            if (p.HasValue && new Rect(0, 0, tc.Bounds.Width, tc.Bounds.Height).Contains(p.Value))
            {
                _targetTabControl = tc;
                break;
            }
        }

        if (_targetTabControl != null)
        {
            // check if it is the only tab being dragged over its own container
            if (_sourceTabControl == _targetTabControl && _targetTabControl.Items.Count == 1)
            {
                _dockPosition = DockPosition.Center;
                showPreview = true;

                var absPos = _targetTabControl.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                if (absPos.HasValue)
                {
                    // snap to integers and use margins to eliminate right/bottom edge layout interpolation jitter
                    double startX = Math.Round(absPos.Value.X);
                    double startY = Math.Round(absPos.Value.Y);
                    double pw = Math.Round(_targetTabControl.Bounds.Width);
                    double ph = Math.Round(_targetTabControl.Bounds.Height);

                    double rightMargin = Math.Round(_indicatorsCanvas.Bounds.Width - startX - pw);
                    double bottomMargin = Math.Round(_indicatorsCanvas.Bounds.Height - startY - ph);

                    _dropPreview.Margin = new Thickness(startX, startY, rightMargin, bottomMargin);
                }

                _dropPreview.Opacity = showPreview ? 1 : 0;
                _reorderIndicator.Opacity = showReorder ? 1 : 0;
                return;
            }

            var p = _window.TranslatePoint(pointerPos, _targetTabControl).Value;
            double w = _targetTabControl.Bounds.Width;
            double h = _targetTabControl.Bounds.Height;

            // filter for visible items to prevent ghost layout snapping
            var visibleItems = _targetTabControl.Items.OfType<TabItem>().Where(t => t.IsVisible).ToList();

            // calculate dynamic header height to prevent overlapping dock areas
            double headerHeight = 45; // fallback
            if (visibleItems.Any())
            {
                var maxBottom = visibleItems.Max(t =>
                {
                    var tp = t.TranslatePoint(new Point(0, t.Bounds.Height), _targetTabControl);
                    return tp?.Y ?? 0;
                });
                if (maxBottom > 0) headerHeight = maxBottom;
            }

            // mouse over headers (re-order)
            if (p.Y <= headerHeight)
            {
                _dockPosition = DockPosition.Center;
                int visIndex = visibleItems.Count;

                // safe hit testing using abs coordinates relative to window
                for (int i = 0; i < visibleItems.Count; i++)
                {
                    var topLeft = visibleItems[i].TranslatePoint(new Point(0, 0), _window);
                    if (topLeft.HasValue)
                    {
                        double itemTop = topLeft.Value.Y;
                        double itemBottom = itemTop + visibleItems[i].Bounds.Height;

                        // check if pointer is in this row (using small vertical buffer)
                        if (pointerPos.Y <= itemBottom + 4)
                        {
                            double centerX = topLeft.Value.X + (visibleItems[i].Bounds.Width / 2);
                            if (pointerPos.X < centerX)
                            {
                                visIndex = i;
                                break;
                            }

                            // check if this is the last item in the current row
                            bool isLastInRow = (i == visibleItems.Count - 1);
                            if (!isLastInRow)
                            {
                                var nextTopLeft = visibleItems[i + 1].TranslatePoint(new Point(0, 0), _window);
                                // if the next item is a good amount lower, we are at the end of the row
                                if (nextTopLeft.HasValue && nextTopLeft.Value.Y > itemBottom - 10)
                                {
                                    isLastInRow = true;
                                }
                            }

                            // if we are past the center of the last item in this row, insert after it
                            if (isLastInRow && pointerPos.X >= centerX)
                            {
                                visIndex = i + 1;
                                break;
                            }
                        }
                    }
                }

                // check if the drop would actually change the order
                bool isSamePosition = false;
                if (_sourceTabControl == _targetTabControl && _draggedTab != null)
                {
                    int currentIndex = visibleItems.IndexOf(_draggedTab);
                    // dragging slightly left or right of itself resolves to the same position after removal
                    if (visIndex == currentIndex || visIndex == currentIndex + 1)
                    {
                        isSamePosition = true;
                    }
                }

                // map visual index to logical index for actual target insertion later
                if (visIndex < visibleItems.Count)
                {
                    _insertIndex = _targetTabControl.Items.IndexOf(visibleItems[visIndex]);
                }
                else if (visibleItems.Count > 0)
                {
                    _insertIndex = _targetTabControl.Items.IndexOf(visibleItems.Last()) + 1;
                }
                else
                {
                    _insertIndex = _targetTabControl.Items.Count;
                }

                if (!isSamePosition)
                {
                    showReorder = true;

                    // align the indicator with actual tab items
                    double indX = 0;
                    double indY = 0;
                    double indHeight = 45; // standard floating tab height (without docked margin/padding)

                    if (visIndex < visibleItems.Count)
                    {
                        var tabCanvasPos = visibleItems[visIndex].TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                        if (tabCanvasPos.HasValue)
                        {
                            indX = tabCanvasPos.Value.X;
                            indY = tabCanvasPos.Value.Y;
                        }
                    }
                    else if (visibleItems.Count > 0)
                    {
                        var lastItem = visibleItems.Last();
                        var tabCanvasPos = lastItem.TranslatePoint(new Point(lastItem.Bounds.Width, 0), _indicatorsCanvas);
                        if (tabCanvasPos.HasValue)
                        {
                            indX = tabCanvasPos.Value.X;

                            // use top left for y to stay aligned vertically
                            var lastTopLeft = lastItem.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                            indY = lastTopLeft?.Y ?? 0;
                        }
                    }
                    else
                    {
                        // empty tab control fallback
                        var tcCanvasPos = _targetTabControl.TranslatePoint(new Point(10, 5), _indicatorsCanvas);
                        if (tcCanvasPos.HasValue)
                        {
                            indX = tcCanvasPos.Value.X;
                            indY = tcCanvasPos.Value.Y;
                        }
                    }

                    Canvas.SetLeft(_reorderIndicator, indX);
                    Canvas.SetTop(_reorderIndicator, indY);
                    _reorderIndicator.Height = indHeight;
                }
            }
            else // mouse over content (dock)
            {
                if (p.X < w * 0.25) _dockPosition = DockPosition.Left;
                else if (p.X > w * 0.75) _dockPosition = DockPosition.Right;
                else if (p.Y > h * 0.75) _dockPosition = DockPosition.Bottom;
                else if (p.Y < h * 0.25) _dockPosition = DockPosition.Top;
                else _dockPosition = DockPosition.Center;

                // fallback to center merge if structural layout already satisfies this dock request
                if (IsRedundantDock(_dockPosition))
                {
                    _dockPosition = DockPosition.Center;
                }

                showPreview = true;

                if (_dockPosition != DockPosition.Center)
                {
                    var absPos = _targetTabControl.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                    if (absPos.HasValue)
                    {
                        // snap to integers to prevent sub-pixel layout rounding overshoot
                        double startX = Math.Round(absPos.Value.X);
                        double startY = Math.Round(absPos.Value.Y);
                        double totalW = Math.Round(w);
                        double totalH = Math.Round(h);
                        double pw = totalW, ph = totalH;
                        double splitterBuffer = 4; // stops grid splitter overlap

                        switch (_dockPosition)
                        {
                            case DockPosition.Left:
                                pw = Math.Round((totalW / 2) - splitterBuffer);
                                break;
                            case DockPosition.Right:
                                double offsetX = Math.Round((totalW / 2) + splitterBuffer);
                                startX += offsetX;
                                pw = totalW - offsetX; // derived via subtraction to fit
                                break;
                            case DockPosition.Top:
                                ph = Math.Round((totalH / 2) - splitterBuffer);
                                break;
                            case DockPosition.Bottom:
                                double offsetY = Math.Round((totalH / 2) + splitterBuffer);
                                startY += offsetY;
                                ph = totalH - offsetY; // derived via subtraction to fit
                                break;
                        }

                        double rightMargin = Math.Round(_indicatorsCanvas.Bounds.Width - startX - pw);
                        double bottomMargin = Math.Round(_indicatorsCanvas.Bounds.Height - startY - ph);

                        _dropPreview.Margin = new Thickness(startX, startY, rightMargin, bottomMargin);
                    }
                }
                else
                {
                    var absPos = _targetTabControl.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                    if (absPos.HasValue)
                    {
                        // snap to integers to prevent sub-pixel layout rounding overshoot
                        double startX = Math.Round(absPos.Value.X);
                        double startY = Math.Round(absPos.Value.Y);
                        double pw = Math.Round(w);
                        double ph = Math.Round(h);

                        double rightMargin = Math.Round(_indicatorsCanvas.Bounds.Width - startX - pw);
                        double bottomMargin = Math.Round(_indicatorsCanvas.Bounds.Height - startY - ph);

                        _dropPreview.Margin = new Thickness(startX, startY, rightMargin, bottomMargin);
                    }
                }
            }
        }

        _dropPreview.Opacity = showPreview ? 1 : 0;
        _reorderIndicator.Opacity = showReorder ? 1 : 0;
    }

    private bool IsRedundantDock(DockPosition pos)
    {
        // if source has more than 1 item, splitting it out is never redundant
        if (_sourceTabControl == null || _targetTabControl == null ||
            _sourceTabControl.Items.Count > 1 || _sourceTabControl == _targetTabControl)
            return false;

        var parentGrid = _sourceTabControl.Parent as Grid;

        // only applies if they are direct siblings in the same split grid
        if (parentGrid == null || parentGrid != _targetTabControl.Parent)
            return false;

        int srcCol = Grid.GetColumn(_sourceTabControl);
        int tgtCol = Grid.GetColumn(_targetTabControl);
        int srcRow = Grid.GetRow(_sourceTabControl);
        int tgtRow = Grid.GetRow(_targetTabControl);

        return pos switch
        {
            DockPosition.Left => srcCol < tgtCol,
            DockPosition.Right => srcCol > tgtCol,
            DockPosition.Top => srcRow < tgtRow,
            DockPosition.Bottom => srcRow > tgtRow,
            _ => false
        };
    }

    private void DropTab()
    {
        if (_draggedTab == null || _sourceTabControl == null || _dockPosition == DockPosition.None) return;
        if (!_isHardDock && _targetTabControl == null) return;

        if (_dockPosition == DockPosition.Center && !_isHardDock)
        {
            int adjustInsertIndex = _insertIndex;

            // adjust index for same panel drops before removing item
            if (_sourceTabControl == _targetTabControl)
            {
                int currentIndex = _sourceTabControl.Items.IndexOf(_draggedTab);
                if (currentIndex != -1 && currentIndex < adjustInsertIndex)
                {
                    adjustInsertIndex--;
                }
            }

            _sourceTabControl.Items.Remove(_draggedTab);

            if (adjustInsertIndex < 0) adjustInsertIndex = 0;
            if (adjustInsertIndex > _targetTabControl.Items.Count)
                adjustInsertIndex = _targetTabControl.Items.Count;

            _targetTabControl.Items.Insert(adjustInsertIndex, _draggedTab);
            _targetTabControl.SelectedItem = _draggedTab;
        }
        else
        {
            _sourceTabControl.Items.Remove(_draggedTab);

            var newTabControl = new TabControl { Padding = new Thickness(0) };

            // bind routing back to mainwindow event
            newTabControl.SelectionChanged += _window.OnMainTabChanged;
            newTabControl.Items.Add(_draggedTab);

            if (_isHardDock)
            {
                SplitRootContainer(newTabControl, _dockPosition);
            }
            else
            {
                SplitTabControl(_targetTabControl, newTabControl, _dockPosition);
            }
        }

        CleanupEmptyTabControls(_container);

        // force refresh on all dynamic tabs
        _window.RefreshTabStyles();

        _window.SyncRelationalModelVisibility();

        _window.TriggerLayoutAutoSave();
    }

    private void SplitRootContainer(TabControl newTabControl, DockPosition pos)
    {
        // find the actual content root, ignoring floating tooltips (borders)
        var oldRoot = _container.Children.OfType<Control>().FirstOrDefault(c => c is TabControl || c is Grid);
        if (oldRoot == null) return;

        int rootIndex = _container.Children.IndexOf(oldRoot);
        _container.Children.RemoveAt(rootIndex);

        var grid = new Grid();

        bool isVertical = pos == DockPosition.Left || pos == DockPosition.Right;

        var splitter = new GridSplitter
        {
            ResizeDirection = isVertical ? GridResizeDirection.Columns : GridResizeDirection.Rows
        };

        // assign custom style and visual indicator logic
        splitter.Classes.Add(isVertical ? "dock-vertical" : "dock-horizontal");

        // reuse main window hover logic
        splitter.PointerEntered += _window.GridSplitter_PointerEntered;
        splitter.PointerExited += _window.GridSplitter_PointerExited;

        if (pos == DockPosition.Left || pos == DockPosition.Right)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            // reset old stats (to stop issues)
            Grid.SetRow(oldRoot, 0);
            Grid.SetRowSpan(oldRoot, 1);
            Grid.SetColumnSpan(oldRoot, 1);

            Grid.SetColumn(pos == DockPosition.Left ? newTabControl : oldRoot, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(pos == DockPosition.Left ? oldRoot : newTabControl, 2);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            // reset old stats (to stop issues)
            Grid.SetColumn(oldRoot, 0);
            Grid.SetRowSpan(oldRoot, 1);
            Grid.SetColumnSpan(oldRoot, 1);

            Grid.SetRow(pos == DockPosition.Top ? newTabControl : oldRoot, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(pos == DockPosition.Top ? oldRoot : newTabControl, 2);
        }

        grid.Children.Add(oldRoot);
        grid.Children.Add(splitter);
        grid.Children.Add(newTabControl);

        // insert at the exact same position (to not cause z-index issues)
        _container.Children.Insert(rootIndex, grid);
    }

    private void SplitTabControl(TabControl target, TabControl newTabControl, DockPosition pos)
    {
        var parent = target.Parent as Panel;
        if (parent == null) return;

        // copy existing grid placement properties before removing (important)
        int gridRow = Grid.GetRow(target);
        int gridCol = Grid.GetColumn(target);
        int gridRowSpan = Grid.GetRowSpan(target);
        int gridColSpan = Grid.GetColumnSpan(target);

        var grid = new Grid();
        int targetIndex = parent.Children.IndexOf(target);
        parent.Children.RemoveAt(targetIndex);

        bool isVertical = pos == DockPosition.Left || pos == DockPosition.Right;

        var splitter = new GridSplitter
        {
            ResizeDirection = isVertical ? GridResizeDirection.Columns : GridResizeDirection.Rows
        };

        // assign custom style and visual indicator logic
        splitter.Classes.Add(isVertical ? "dock-vertical" : "dock-horizontal");

        // reuse main window hover logic
        splitter.PointerEntered += _window.GridSplitter_PointerEntered;
        splitter.PointerExited += _window.GridSplitter_PointerExited;

        if (pos == DockPosition.Left || pos == DockPosition.Right)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            Grid.SetColumn(pos == DockPosition.Left ? newTabControl : target, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(pos == DockPosition.Left ? target : newTabControl, 2);

            // reset spans/rows to stop layout issues inside new grid
            Grid.SetColumnSpan(target, 1);
            Grid.SetRowSpan(target, 1);
            Grid.SetRow(target, 0);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            Grid.SetRow(pos == DockPosition.Top ? newTabControl : target, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(pos == DockPosition.Top ? target : newTabControl, 2);

            // reset spans/columns to stop issues here also
            Grid.SetRowSpan(target, 1);
            Grid.SetColumnSpan(target, 1);
            Grid.SetColumn(target, 0);
        }

        // apply original placement properties to new wrapper grid
        Grid.SetRow(grid, gridRow);
        Grid.SetColumn(grid, gridCol);
        Grid.SetRowSpan(grid, gridRowSpan);
        Grid.SetColumnSpan(grid, gridColSpan);

        grid.Children.Add(target);
        grid.Children.Add(splitter);
        grid.Children.Add(newTabControl);
        parent.Children.Insert(targetIndex, grid);
    }

    public TabControl GetMainTabControl()
    {
        return GetTabControls(_container).FirstOrDefault() ?? _window.FindControl<TabControl>("MainTabs");
    }

    public void EnsureTabInMainSystem(TabItem tab, bool select = true)
    {
        var currentTc = tab.Parent as TabControl;

        // only move to main tab control if it currently has no parent (was hidden)
        if (currentTc == null)
        {
            var mainTc = GetMainTabControl();
            mainTc.Items.Add(tab);
            if (select) mainTc.SelectedItem = tab;
        }
        else
        {
            // if its already in a tab control (isolated or main), just select it
            if (select) currentTc.SelectedItem = tab;
        }
    }

    public void ForceCleanup()
    {
        CleanupEmptyTabControls(_container);
    }

    private void CleanupEmptyTabControls(Panel root)
    {
        var tabControls = GetTabControls(root).ToList();

        for (int i = tabControls.Count - 1; i >= 0; i--)
        {
            var tc = tabControls[i];

            // check if panel lacks any visible tabs
            bool hasEssential = tc.Items.OfType<TabItem>().Any(t => t.IsVisible || t.Name == "TabVim" || t.Name == "TabDesigner");

            if (!hasEssential && tabControls.Count > 1)
            {
                // migrate any temporary tabs to another surviving panel before destroying
                var tempTabs = tc.Items.OfType<TabItem>().ToList();
                var targetTc = tabControls.FirstOrDefault(other => other != tc);

                if (targetTc != null)
                {
                    foreach (var tempTab in tempTabs)
                    {
                        tc.Items.Remove(tempTab);
                        targetTc.Items.Add(tempTab);
                    }
                }

                RemoveTabControlAndMerge(tc);
                tabControls.RemoveAt(i);
            }
        }
    }

    private void RemoveTabControlAndMerge(TabControl emptyTc)
    {
        if (emptyTc.Parent is Grid grid && grid.Children.Count == 3)
        {
            var sibling = grid.Children.FirstOrDefault(c => c != emptyTc && !(c is GridSplitter));
            if (sibling != null)
            {
                var parentOfGrid = grid.Parent as Panel;
                if (parentOfGrid != null)
                {
                    int index = parentOfGrid.Children.IndexOf(grid);
                    grid.Children.Remove(sibling);
                    parentOfGrid.Children.RemoveAt(index);
                    parentOfGrid.Children.Insert(index, sibling);

                    Grid.SetRow((Control)sibling, Grid.GetRow(grid));
                    Grid.SetColumn((Control)sibling, Grid.GetColumn(grid));
                    Grid.SetRowSpan((Control)sibling, Grid.GetRowSpan(grid));
                    Grid.SetColumnSpan((Control)sibling, Grid.GetColumnSpan(grid));
                }
            }
        }

        _window.TriggerLayoutAutoSave();
    }

    private IEnumerable<TabControl> GetTabControls(Visual parent)
    {
        if (parent is TabControl tc) yield return tc;
        foreach (var visual in parent.GetVisualDescendants())
            if (visual is TabControl childTc) yield return childTc;
    }

    private void StartMorphAnimation()
    {
        if (!_isMorphAnimating)
        {
            _isMorphAnimating = true;
            _lastFrameTime = null;
            TopLevel.GetTopLevel(_window)?.RequestAnimationFrame(MorphAnimationFrame);
        }
    }

    private void MorphAnimationFrame(TimeSpan uptime)
    {
        if (!_isMorphAnimating) return;

        // skip first frame to just record the start time accurately
        if (_lastFrameTime == null)
        {
            _lastFrameTime = uptime;
            TopLevel.GetTopLevel(_window)?.RequestAnimationFrame(MorphAnimationFrame);
            return;
        }

        double deltaMs = (uptime - _lastFrameTime.Value).TotalMilliseconds;
        _lastFrameTime = uptime;

        // 150ms duration
        double step = deltaMs * (1.0 / 150.0);
        bool animating = false;

        if (_activeMorphDock != DockPosition.None)
        {
            _morphProgress += step;
            if (_morphProgress >= 1)
            {
                _morphProgress = 1;
            }
            else
            {
                animating = true;
            }
        }
        else
        {
            _morphProgress -= step;
            if (_morphProgress <= 0)
            {
                _morphProgress = 0;
                _animatingMorphDock = DockPosition.None;
            }
            else
            {
                animating = true;
            }
        }

        if (_ghostElement != null)
        {
            UpdateDockHintsGeometry();
        }

        if (animating)
        {
            // queue the next frame synced to the monitors refresh rate
            TopLevel.GetTopLevel(_window)?.RequestAnimationFrame(MorphAnimationFrame);
        }
        else
        {
            _isMorphAnimating = false;
        }
    }

    private void UpdateDockHintsGeometry()
    {
        var absPos1 = _container.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
        if (!absPos1.HasValue) return;

        // round boundaries to prevent subpixel layout rounding overshoot
        double cw = Math.Round(_container.Bounds.Width);
        double ch = Math.Round(_container.Bounds.Height);
        var p0 = new Point(Math.Round(absPos1.Value.X), Math.Round(absPos1.Value.Y));

        double yCenterMin = ch * 0.40;
        double xCenterMin = cw * 0.40;
        double wLen = cw * 0.20;
        double hLen = ch * 0.20;

        double d = 25;
        double hDepth = d / 2;
        double r = 4;
        double splitterBuffer = 4;
        double r2 = 8;

        double leftX = p0.X + 1;
        double leftY = p0.Y + yCenterMin;
        double rightX = p0.X + cw - hDepth - 1.5; // manually move right half a pixel
        double rightY = p0.Y + yCenterMin;
        double topX = p0.X + xCenterMin;
        double topY = p0.Y + 1;
        double bottomX = p0.X + xCenterMin;
        double bottomY = p0.Y + ch - hDepth - 1;

        Canvas.SetLeft(_dockHints[0], leftX);
        Canvas.SetTop(_dockHints[0], leftY);
        Canvas.SetLeft(_dockHints[1], rightX);
        Canvas.SetTop(_dockHints[1], rightY);
        Canvas.SetLeft(_dockHints[2], topX);
        Canvas.SetTop(_dockHints[2], topY);
        Canvas.SetLeft(_dockHints[3], bottomX);
        Canvas.SetTop(_dockHints[3], bottomY);

        Point Lerp(Point a, Point b, double tParam) => new Point(a.X + (b.X - a.X) * tParam, a.Y + (b.Y - a.Y) * tParam);

        // quadratic ease out for a smoother natural effect
        double t = 1 - Math.Pow(1 - _morphProgress, 2);

        for (int i = 0; i < 4; i++)
        {
            if (_dockHints[i] == null) continue;

            DockPosition pos = i == 0 ? DockPosition.Left : i == 1 ? DockPosition.Right : i == 2 ? DockPosition.Top : DockPosition.Bottom;

            bool isMorphed = (_animatingMorphDock == pos);
            double currentT = isMorphed ? t : 0;

            Point[] src = new Point[8];
            Point[] tgt = new Point[8];

            if (i == 0) // left
            {
                src[0] = new Point(0, 0);
                src[1] = new Point(hDepth - r, hDepth - r);
                src[2] = new Point(hDepth, hDepth);
                src[3] = new Point(hDepth, hDepth + r);
                src[4] = new Point(hDepth, hLen - hDepth - r);
                src[5] = new Point(hDepth, hLen - hDepth);
                src[6] = new Point(hDepth - r, hLen - hDepth + r);
                src[7] = new Point(0, hLen);

                double targetX = (p0.X + 1) - leftX;
                double targetY = (p0.Y + 1) - leftY;
                double pw = Math.Round(cw / 2) - splitterBuffer - 1; // 1px off the left edge
                double ph = ch - 2; // 1px off top and bottom

                tgt[0] = new Point(targetX, targetY);
                tgt[1] = new Point(targetX + pw - r2, targetY);
                tgt[2] = new Point(targetX + pw, targetY);
                tgt[3] = new Point(targetX + pw, targetY + r2);
                tgt[4] = new Point(targetX + pw, targetY + ph - r2);
                tgt[5] = new Point(targetX + pw, targetY + ph);
                tgt[6] = new Point(targetX + pw - r2, targetY + ph);
                tgt[7] = new Point(targetX, targetY + ph);
            }
            else if (i == 1) // right
            {
                src[0] = new Point(hDepth, 0);
                src[1] = new Point(r, hDepth - r);
                src[2] = new Point(0, hDepth);
                src[3] = new Point(0, hDepth + r);
                src[4] = new Point(0, hLen - hDepth - r);
                src[5] = new Point(0, hLen - hDepth);
                src[6] = new Point(r, hLen - hDepth + r);
                src[7] = new Point(hDepth, hLen);

                double offset = Math.Round(cw / 2) + splitterBuffer;
                double targetX = (p0.X + (cw / 2) + splitterBuffer) - rightX;
                double targetY = (p0.Y + 1) - rightY;
                double pw = cw - offset - 1; // 1px off the right edge (derived via subtraction)
                double ph = ch - 2; // 1px off top and bottom

                tgt[0] = new Point(targetX + pw, targetY);
                tgt[1] = new Point(targetX + r2, targetY);
                tgt[2] = new Point(targetX, targetY);
                tgt[3] = new Point(targetX, targetY + r2);
                tgt[4] = new Point(targetX, targetY + ph - r2);
                tgt[5] = new Point(targetX, targetY + ph);
                tgt[6] = new Point(targetX + r2, targetY + ph);
                tgt[7] = new Point(targetX + pw, targetY + ph);
            }
            else if (i == 2) // top
            {
                src[0] = new Point(0, 0);
                src[1] = new Point(hDepth - r, hDepth - r);
                src[2] = new Point(hDepth, hDepth);
                src[3] = new Point(hDepth + r, hDepth);
                src[4] = new Point(wLen - hDepth - r, hDepth);
                src[5] = new Point(wLen - hDepth, hDepth);
                src[6] = new Point(wLen - hDepth + r, hDepth - r);
                src[7] = new Point(wLen, 0);

                double targetX = (p0.X + 1) - topX;
                double targetY = (p0.Y + 1) - topY;
                double pw = cw - 2; // 1px off left and right
                double ph = Math.Round(ch / 2) - splitterBuffer - 1; // 1px off the top edge

                tgt[0] = new Point(targetX, targetY);
                tgt[1] = new Point(targetX, targetY + ph - r2);
                tgt[2] = new Point(targetX, targetY + ph);
                tgt[3] = new Point(targetX + r2, targetY + ph);
                tgt[4] = new Point(targetX + pw - r2, targetY + ph);
                tgt[5] = new Point(targetX + pw, targetY + ph);
                tgt[6] = new Point(targetX + pw, targetY + ph - r2);
                tgt[7] = new Point(targetX + pw, targetY);
            }
            else if (i == 3) // bottom
            {
                src[0] = new Point(0, hDepth);
                src[1] = new Point(hDepth - r, r);
                src[2] = new Point(hDepth, 0);
                src[3] = new Point(hDepth + r, 0);
                src[4] = new Point(wLen - hDepth - r, 0);
                src[5] = new Point(wLen - hDepth, 0);
                src[6] = new Point(wLen - hDepth + r, r);
                src[7] = new Point(wLen, hDepth);

                double offset = Math.Round(ch / 2) + splitterBuffer;
                double targetX = (p0.X + 1) - bottomX;
                double targetY = (p0.Y + (ch / 2) + splitterBuffer) - bottomY;
                double pw = cw - 2; // 1px off left and right
                double ph = ch - offset - 1; // 1px off the bottom edge (derived via subtraction)

                tgt[0] = new Point(targetX, targetY + ph);
                tgt[1] = new Point(targetX, targetY + r2);
                tgt[2] = new Point(targetX, targetY);
                tgt[3] = new Point(targetX + r2, targetY);
                tgt[4] = new Point(targetX + pw - r2, targetY);
                tgt[5] = new Point(targetX + pw, targetY);
                tgt[6] = new Point(targetX + pw, targetY + r2);
                tgt[7] = new Point(targetX + pw, targetY + ph);
            }

            // interpolate points smoothly towards the desired target area mapping
            Point[] p = new Point[8];
            for (int j = 0; j < 8; j++) p[j] = Lerp(src[j], tgt[j], currentT);

            _dockHints[i].Data = StreamGeometry.Parse(System.FormattableString.Invariant(
                $"M {p[0].X},{p[0].Y} L {p[1].X},{p[1].Y} Q {p[2].X},{p[2].Y} {p[3].X},{p[3].Y} L {p[4].X},{p[4].Y} Q {p[5].X},{p[5].Y} {p[6].X},{p[6].Y} L {p[7].X},{p[7].Y} Z"));

            Color baseColor = Color.Parse("#1A6495ED");
            Color activeColor = Color.Parse("#256495ED");

            byte a = (byte)(baseColor.A + (activeColor.A - baseColor.A) * currentT);
            byte rCol = (byte)(baseColor.R + (activeColor.R - baseColor.R) * currentT);
            byte g = (byte)(baseColor.G + (activeColor.G - baseColor.G) * currentT);
            byte bCol = (byte)(baseColor.B + (activeColor.B - baseColor.B) * currentT);

            _dockHints[i].Fill = new SolidColorBrush(Color.FromArgb(a, rCol, g, bCol));

            // pop the active morphing shape to the front so it overlaps others cleanly
            _dockHints[i].ZIndex = isMorphed ? 101 : 100;

            // fade out the non-active hints smoothly while morphing
            if (_animatingMorphDock != DockPosition.None && !isMorphed)
            {
                _dockHints[i].Opacity = 1.0 - t;
            }
            else
            {
                _dockHints[i].Opacity = 1.0;
            }
        }
    }
}