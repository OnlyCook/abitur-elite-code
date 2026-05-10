using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                showPreview = true;
                var absPos = _container.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                if (absPos.HasValue)
                {
                    double startX = absPos.Value.X;
                    double startY = absPos.Value.Y;
                    double pw = cw, ph = ch;
                    double splitterBuffer = 4; // stops grid splitter overlap

                    switch (_dockPosition)
                    {
                        case DockPosition.Left:
                            pw = (cw / 2) - splitterBuffer;
                            break;
                        case DockPosition.Right:
                            startX += (cw / 2) + splitterBuffer;
                            pw = (cw / 2) - splitterBuffer;
                            break;
                        case DockPosition.Top:
                            ph = (ch / 2) - splitterBuffer;
                            break;
                        case DockPosition.Bottom:
                            startY += (ch / 2) + splitterBuffer;
                            ph = (ch / 2) - splitterBuffer;
                            break;
                    }

                    Canvas.SetLeft(_dropPreview, startX);
                    Canvas.SetTop(_dropPreview, startY);
                    _dropPreview.Width = pw;
                    _dropPreview.Height = ph;
                }

                _dropPreview.Opacity = showPreview ? 1 : 0;
                _reorderIndicator.Opacity = showReorder ? 1 : 0;
                return; // skip normal tab control hit testing
            }
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
                    Canvas.SetLeft(_dropPreview, absPos.Value.X);
                    Canvas.SetTop(_dropPreview, absPos.Value.Y);
                    _dropPreview.Width = _targetTabControl.Bounds.Width;
                    _dropPreview.Height = _targetTabControl.Bounds.Height;
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
                        double startX = absPos.Value.X;
                        double startY = absPos.Value.Y;
                        double pw = w, ph = h;
                        double splitterBuffer = 4; // stops grid splitter overlap

                        switch (_dockPosition)
                        {
                            case DockPosition.Left:
                                pw = (w / 2) - splitterBuffer;
                                break;
                            case DockPosition.Right:
                                startX += (w / 2) + splitterBuffer;
                                pw = (w / 2) - splitterBuffer;
                                break;
                            case DockPosition.Top:
                                ph = (h / 2) - splitterBuffer;
                                break;
                            case DockPosition.Bottom:
                                startY += (h / 2) + splitterBuffer;
                                ph = (h / 2) - splitterBuffer;
                                break;
                        }

                        Canvas.SetLeft(_dropPreview, startX);
                        Canvas.SetTop(_dropPreview, startY);
                        _dropPreview.Width = pw;
                        _dropPreview.Height = ph;
                    }
                }
                else
                {
                    var absPos = _targetTabControl.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                    if (absPos.HasValue)
                    {
                        Canvas.SetLeft(_dropPreview, absPos.Value.X);
                        Canvas.SetTop(_dropPreview, absPos.Value.Y);
                        _dropPreview.Width = w;
                        _dropPreview.Height = h;
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
        var mainTc = GetMainTabControl();
        var currentTc = tab.Parent as TabControl;

        if (currentTc != mainTc)
        {
            if (currentTc != null) currentTc.Items.Remove(tab);
            mainTc.Items.Add(tab);
        }

        if (select) mainTc.SelectedItem = tab;
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
            bool hasEssential = tc.Items.OfType<TabItem>().Any(t => t.IsVisible);

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
    }

    private IEnumerable<TabControl> GetTabControls(Visual parent)
    {
        if (parent is TabControl tc) yield return tc;
        foreach (var visual in parent.GetVisualDescendants())
            if (visual is TabControl childTc) yield return childTc;
    }
}