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

public enum DockPosition { None, Center, Left, Right, Top, Bottom }

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
                    // only begin drag if clicked on header part (finally)
                    var border = src?.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Name == "PART_LayoutRoot");
                    if (border != null || src is TextBlock || src is Avalonia.Svg.Skia.Svg)
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

        _dropPreview.IsVisible = false;
        _reorderIndicator.IsVisible = false;
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
        _window.Cursor = new Cursor(StandardCursorType.DragMove);
    }

    private void UpdateDropIndicators(Point pointerPos)
    {
        _targetTabControl = null;
        _dockPosition = DockPosition.None;
        _insertIndex = -1;

        _dropPreview.IsVisible = false;
        _reorderIndicator.IsVisible = false;

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
            var p = _window.TranslatePoint(pointerPos, _targetTabControl).Value;
            double w = _targetTabControl.Bounds.Width;
            double h = _targetTabControl.Bounds.Height;

            // mouse over headers (re-order)
            if (p.Y <= 45)
            {
                _dockPosition = DockPosition.Center;
                var items = _targetTabControl.Items.OfType<TabItem>().ToList();
                _insertIndex = items.Count;

                for (int i = 0; i < items.Count; i++)
                {
                    var tp = _window.TranslatePoint(pointerPos, items[i]);
                    if (tp.HasValue && tp.Value.X < items[i].Bounds.Width / 2)
                    {
                        _insertIndex = i;
                        break;
                    }
                }

                // check if the drop would actually change the order
                bool isSamePosition = false;
                if (_sourceTabControl == _targetTabControl && _draggedTab != null)
                {
                    int currentIndex = items.IndexOf(_draggedTab);
                    // dragging slightly left or right of itself resolves to the same position after removal
                    if (_insertIndex == currentIndex || _insertIndex == currentIndex + 1)
                    {
                        isSamePosition = true;
                    }
                }

                if (isSamePosition)
                {
                    _reorderIndicator.IsVisible = false;
                }
                else
                {
                    _reorderIndicator.IsVisible = true;
                    var absPos = _targetTabControl.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                    if (absPos.HasValue)
                    {
                        double indX = absPos.Value.X;
                        if (_insertIndex < items.Count)
                        {
                            var tabPos = items[_insertIndex].TranslatePoint(new Point(0, 0), _targetTabControl);
                            if (tabPos.HasValue) indX += tabPos.Value.X;
                        }
                        else if (items.Count > 0)
                        {
                            var tabPos = items.Last().TranslatePoint(new Point(items.Last().Bounds.Width, 0), _targetTabControl);
                            if (tabPos.HasValue) indX += tabPos.Value.X;
                        }
                        else indX += 10;

                        Canvas.SetLeft(_reorderIndicator, indX);
                        Canvas.SetTop(_reorderIndicator, absPos.Value.Y + 5);
                        _reorderIndicator.Height = 35;
                    }
                }
            }
            else // mouse over content (dock, more like a placeholder right now, real bugged)
            {
                if (p.X < w * 0.25) _dockPosition = DockPosition.Left;
                else if (p.X > w * 0.75) _dockPosition = DockPosition.Right;
                else if (p.Y > h * 0.75) _dockPosition = DockPosition.Bottom;
                else if (p.Y < h * 0.25) _dockPosition = DockPosition.Top;
                else _dockPosition = DockPosition.Center;

                if (_dockPosition != DockPosition.Center)
                {
                    _dropPreview.IsVisible = true;
                    var absPos = _targetTabControl.TranslatePoint(new Point(0, 0), _indicatorsCanvas);
                    if (absPos.HasValue)
                    {
                        double startX = absPos.Value.X;
                        double startY = absPos.Value.Y;
                        double pw = w, ph = h;

                        switch (_dockPosition)
                        {
                            case DockPosition.Left: pw = w / 2; break;
                            case DockPosition.Right: startX += w / 2; pw = w / 2; break;
                            case DockPosition.Top: ph = h / 2; break;
                            case DockPosition.Bottom: startY += h / 2; ph = h / 2; break;
                        }

                        Canvas.SetLeft(_dropPreview, startX);
                        Canvas.SetTop(_dropPreview, startY);
                        _dropPreview.Width = pw;
                        _dropPreview.Height = ph;
                    }
                }
                else
                {
                    _dropPreview.IsVisible = true;
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
    }

    private void DropTab()
    {
        if (_draggedTab == null || _targetTabControl == null || _sourceTabControl == null || _dockPosition == DockPosition.None) return;

        if (_dockPosition == DockPosition.Center)
        {
            int adjustInsertIndex = _insertIndex;

            // adjust index for same mpanel drops before removing item
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
            SplitTabControl(_targetTabControl, newTabControl, _dockPosition);
        }

        CleanupEmptyTabControls(_container);

        // force refresh on all dynamic tabs
        _window.RefreshTabStyles();
    }

    private void SplitTabControl(TabControl target, TabControl newTabControl, DockPosition pos)
    {
        var parent = target.Parent as Panel;
        if (parent == null) return;

        var grid = new Grid();
        int targetIndex = parent.Children.IndexOf(target);
        parent.Children.RemoveAt(targetIndex);

        // placeholder for now
        var splitter = new GridSplitter
        {
            Background = Brushes.Transparent,
            ResizeDirection = pos == DockPosition.Left || pos == DockPosition.Right ? GridResizeDirection.Columns : GridResizeDirection.Rows,
            Width = pos == DockPosition.Left || pos == DockPosition.Right ? 8 : double.NaN,
            Height = pos == DockPosition.Top || pos == DockPosition.Bottom ? 8 : double.NaN,
            Cursor = pos == DockPosition.Left || pos == DockPosition.Right ? new Cursor(StandardCursorType.SizeWestEast) : new Cursor(StandardCursorType.SizeNorthSouth)
        };

        splitter.PointerEntered += (s, e) => {
            if (s is TemplatedControl ctrl && !ctrl.Classes.Contains("dragging"))
                ctrl.Background = SolidColorBrush.Parse("#32A852");
        };

        splitter.PointerExited += (s, e) => {
            if (s is TemplatedControl ctrl && !ctrl.Classes.Contains("dragging"))
                ctrl.Background = Brushes.Transparent;
        };

        splitter.PointerPressed += (s, e) => {
            if (s is Control ctrl) ctrl.Classes.Add("dragging");
        };

        splitter.PointerReleased += (s, e) => {
            if (s is TemplatedControl ctrl)
            {
                ctrl.Classes.Remove("dragging");
                if (!ctrl.IsPointerOver) ctrl.Background = Brushes.Transparent;
            }
        };

        if (pos == DockPosition.Left || pos == DockPosition.Right)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            Grid.SetColumn(pos == DockPosition.Left ? newTabControl : target, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(pos == DockPosition.Left ? target : newTabControl, 2);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));

            Grid.SetRow(pos == DockPosition.Top ? newTabControl : target, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(pos == DockPosition.Top ? target : newTabControl, 2);
        }

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