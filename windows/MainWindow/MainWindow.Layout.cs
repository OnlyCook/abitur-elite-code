using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AbiturEliteCode;

public partial class MainWindow
{
    public void TriggerLayoutAutoSave()
    {
        if (AppSettings.IsLayoutAutoSaveEnabled && !_isRestoringLayout)
        {
            _layoutAutoSaveTimer.Stop();
            _layoutAutoSaveTimer.Start();
        }
    }

    private void SaveAppLayout()
    {
        var rootControl = LeftPanelContainer.Children.FirstOrDefault(c => c is TabControl || c is Grid) as Control;
        if (rootControl == null) return;

        var state = new LayoutState
        {
            LeftPanelTree = JsonSerializer.Serialize(SerializeDockTree(rootControl))
        };

        var mainContentGrid = LeftPanelContainer.Parent as Grid;
        if (mainContentGrid != null && mainContentGrid.ColumnDefinitions.Count >= 3)
        {
            state.MainCol0 = mainContentGrid.ColumnDefinitions[0].Width.Value;
            state.MainCol2 = mainContentGrid.ColumnDefinitions[2].Width.Value;
        }

        if (UmlTabGrid != null && UmlTabGrid.RowDefinitions.Count >= 3)
            state.UmlRow0 = UmlTabGrid.RowDefinitions[0].Height.Value;

        var csharpGrid = PnlCsharpEditor.Parent as Grid;
        if (csharpGrid != null && csharpGrid.RowDefinitions.Count >= 4)
        {
            state.EditorRow1 = csharpGrid.RowDefinitions[1].Height.Value;
            state.EditorRow3 = csharpGrid.RowDefinitions[3].Height.Value; // keep for fallback

            if (_isSqlMode)
            {
                state.EditorRow3Sql = csharpGrid.RowDefinitions[3].Height.Value;
                state.EditorRow3Csharp = _lastCsharpRowHeight.Value;
            }
            else
            {
                state.EditorRow3Csharp = csharpGrid.RowDefinitions[3].Height.Value;
                state.EditorRow3Sql = _lastSqlRowHeight.Value;
            }
        }

        AppSettings.SavedAppLayout = JsonSerializer.Serialize(state);
        playerData.Settings.SavedAppLayout = AppSettings.SavedAppLayout;
        SaveSystem.Save(playerData);
    }

    private DockNode SerializeDockTree(Control c)
    {
        if (c is TabControl tc)
        {
            return new DockNode
            {
                Type = "Tab",
                Tabs = tc.Items.OfType<TabItem>().Select(t => t.Name ?? t.Header?.ToString()).ToList(),
                SelectedTab = (tc.SelectedItem as TabItem)?.Name ?? (tc.SelectedItem as TabItem)?.Header?.ToString()
            };
        }

        if (c is Grid g && g.Children.Count >= 3)
        {
            bool isHorizontal = g.ColumnDefinitions.Count > 1;
            return new DockNode
            {
                Type = isHorizontal ? "HGrid" : "VGrid",
                Child1 = SerializeDockTree((Control)g.Children[0]),
                Child2 = SerializeDockTree((Control)g.Children[2]),
                Size1 = isHorizontal ? g.ColumnDefinitions[0].Width.Value : g.RowDefinitions[0].Height.Value,
                Size2 = isHorizontal ? g.ColumnDefinitions[2].Width.Value : g.RowDefinitions[2].Height.Value
            };
        }
        return null;
    }

    private void LoadAppLayout()
    {
        if (string.IsNullOrEmpty(AppSettings.SavedAppLayout)) return;
        try
        {
            _isRestoringLayout = true;
            var state = JsonSerializer.Deserialize<LayoutState>(AppSettings.SavedAppLayout);
            if (state == null) return;

            var mainContentGrid = LeftPanelContainer.Parent as Grid;
            if (mainContentGrid != null && mainContentGrid.ColumnDefinitions.Count >= 3)
            {
                mainContentGrid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(state.MainCol0, GridUnitType.Star));
                mainContentGrid.ColumnDefinitions[2] = new ColumnDefinition(new GridLength(state.MainCol2, GridUnitType.Star));
            }

            if (UmlTabGrid != null && UmlTabGrid.RowDefinitions.Count >= 3)
                UmlTabGrid.RowDefinitions[0] = new RowDefinition(new GridLength(state.UmlRow0, GridUnitType.Star));

            var csharpGrid = PnlCsharpEditor.Parent as Grid;
            if (csharpGrid != null && csharpGrid.RowDefinitions.Count >= 4)
            {
                csharpGrid.RowDefinitions[1] = new RowDefinition(new GridLength(state.EditorRow1, GridUnitType.Star));

                // fallback
                if (state.EditorRow3Csharp == 180 && state.EditorRow3Sql == 250 && state.EditorRow3 != 1)
                {
                    if (_isSqlMode) state.EditorRow3Sql = state.EditorRow3;
                    else state.EditorRow3Csharp = state.EditorRow3;
                }

                _lastCsharpRowHeight = new GridLength(state.EditorRow3Csharp, GridUnitType.Pixel);
                _lastSqlRowHeight = new GridLength(state.EditorRow3Sql, GridUnitType.Pixel);

                csharpGrid.RowDefinitions[3] = new RowDefinition(new GridLength(_isSqlMode ? state.EditorRow3Sql : state.EditorRow3Csharp, GridUnitType.Pixel));
            }

            var node = JsonSerializer.Deserialize<DockNode>(state.LeftPanelTree);
            if (node != null)
            {
                var allTabs = _tabDockManager.GetAllTabControls().SelectMany(tc => tc.Items.OfType<TabItem>()).ToList();

                // ensure MainTabs is detached before clear to prevent visual tree errors
                if (MainTabs.Parent is Panel p)
                    p.Children.Remove(MainTabs);

                LeftPanelContainer.Children.Clear();

                bool mainTabsUsed = false;
                var newRoot = BuildDockTree(node, allTabs, ref mainTabsUsed);
                if (newRoot != null)
                    LeftPanelContainer.Children.Add(newRoot);

                // dump leftover tabs into first found main tabcontrol
                var mainTc = _tabDockManager.GetMainTabControl();
                foreach (var tab in allTabs)
                    if (tab.Parent == null) mainTc.Items.Add(tab);
            }
            UpdateTabStyles();
        }
        catch { }
        finally
        {
            _isRestoringLayout = false;
        }
    }

    private Control BuildDockTree(DockNode node, List<TabItem> availableTabs, ref bool mainTabsUsed)
    {
        if (node.Type == "Tab")
        {
            TabControl tc;

            // reuse mainTabs for the first Tab Control structure to maintain references
            if (!mainTabsUsed)
            {
                tc = MainTabs;
                if (tc.Parent is Panel p) p.Children.Remove(tc);
                tc.Items.Clear();
                mainTabsUsed = true;
            }
            else
            {
                tc = new TabControl
                {
                    Padding = new Thickness(0)
                };
                tc.SelectionChanged += OnMainTabChanged;
            }

            foreach (var tabName in node.Tabs)
            {
                var tab = availableTabs.FirstOrDefault(t => (t.Name ?? t.Header?.ToString()) == tabName);
                if (tab != null)
                {
                    if (tab.Parent is TabControl oldParent) oldParent.Items.Remove(tab);
                    tc.Items.Add(tab);
                }
            }
            var selected = tc.Items.OfType<TabItem>().FirstOrDefault(t => (t.Name ?? t.Header?.ToString()) == node.SelectedTab);
            if (selected != null) tc.SelectedItem = selected;
            return tc;
        }

        if (node.Type == "HGrid" || node.Type == "VGrid")
        {
            var grid = new Grid();
            bool isH = node.Type == "HGrid";

            var splitter = new GridSplitter
            {
                ResizeDirection = isH ? GridResizeDirection.Columns : GridResizeDirection.Rows
            };
            splitter.Classes.Add(isH ? "dock-vertical" : "dock-horizontal");
            splitter.PointerEntered += GridSplitter_PointerEntered;
            splitter.PointerExited += GridSplitter_PointerExited;

            if (isH)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(node.Size1, GridUnitType.Star)));
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(node.Size2, GridUnitType.Star)));

                // pass the reference down recursively (sorry nasa)
                var c1 = BuildDockTree(node.Child1, availableTabs, ref mainTabsUsed);
                var c2 = BuildDockTree(node.Child2, availableTabs, ref mainTabsUsed);

                if (c1 != null)
                {
                    Grid.SetColumn(c1, 0);
                    grid.Children.Add(c1);
                }
                Grid.SetColumn(splitter, 1);
                grid.Children.Add(splitter);
                if (c2 != null)
                {
                    Grid.SetColumn(c2, 2);
                    grid.Children.Add(c2);
                }
            }
            else
            {
                grid.RowDefinitions.Add(new RowDefinition(new GridLength(node.Size1, GridUnitType.Star)));
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                grid.RowDefinitions.Add(new RowDefinition(new GridLength(node.Size2, GridUnitType.Star)));

                // pass the ref down recursively
                var c1 = BuildDockTree(node.Child1, availableTabs, ref mainTabsUsed);
                var c2 = BuildDockTree(node.Child2, availableTabs, ref mainTabsUsed);

                if (c1 != null)
                {
                    Grid.SetRow(c1, 0);
                    grid.Children.Add(c1);
                }
                Grid.SetRow(splitter, 1);
                grid.Children.Add(splitter);
                if (c2 != null)
                {
                    Grid.SetRow(c2, 2);
                    grid.Children.Add(c2);
                }
            }
            return grid;
        }
        return null;
    }

    public void ResetLayoutState()
    {
        _isRestoringLayout = true;

        var tabControls = _tabDockManager.GetAllTabControls().ToList();
        var allTabs = tabControls.SelectMany(tc => tc.Items.OfType<TabItem>()).ToList();

        // detach all tabs from their current parents to prevent visual parent exceptions
        foreach (var tc in tabControls)
        {
            tc.Items.Clear();
        }

        LeftPanelContainer.Children.Clear();

        MainTabs.Items.Clear();

        // explicitly split visual ties for main tabs before it can be appended later
        if (MainTabs.Parent is Panel p)
        {
            p.Children.Remove(MainTabs);
        }

        var defaultOrder = new[] { "Aufgabe", "UML/Diagramme", "Materialien", "Level Designer", "TabDesigner", "Vim Hilfe", "TabVim" };
        foreach (var name in defaultOrder)
        {
            var tab = allTabs.FirstOrDefault(t => t.Header?.ToString() == name || t.Name == name);
            if (tab != null)
            {
                MainTabs.Items.Add(tab);
                allTabs.Remove(tab);
            }
        }
        foreach (var tab in allTabs)
            MainTabs.Items.Add(tab);

        LeftPanelContainer.Children.Add(MainTabs);
        MainTabs.SelectedIndex = 0;

        var mainContentGrid = LeftPanelContainer.Parent as Grid;
        if (mainContentGrid != null && mainContentGrid.ColumnDefinitions.Count >= 3)
        {
            mainContentGrid.ColumnDefinitions[0] = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
            mainContentGrid.ColumnDefinitions[2] = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
        }

        if (UmlTabGrid != null && UmlTabGrid.RowDefinitions.Count >= 3)
            UmlTabGrid.RowDefinitions[0] = new RowDefinition(new GridLength(1, GridUnitType.Star));

        var csharpGrid = PnlCsharpEditor.Parent as Grid;
        if (csharpGrid != null && csharpGrid.RowDefinitions.Count >= 4)
        {
            csharpGrid.RowDefinitions[1] = new RowDefinition(new GridLength(1, GridUnitType.Star));

            // set default pixel heights based on mode to stop console overscaling 
            _lastCsharpRowHeight = new GridLength(180, GridUnitType.Pixel);
            _lastSqlRowHeight = new GridLength(250, GridUnitType.Pixel);
            csharpGrid.RowDefinitions[3] = new RowDefinition(new GridLength(_isSqlMode ? 250 : 180, GridUnitType.Pixel));
        }

        UpdateTabStyles();
        _isRestoringLayout = false;
    }
}

public class LayoutState
{
    public string LeftPanelTree { get; set; } = "";
    public double MainCol0 { get; set; } = 1;
    public double MainCol2 { get; set; } = 1;
    public double UmlRow0 { get; set; } = 1;
    public double EditorRow1 { get; set; } = 1;
    public double EditorRow3 { get; set; } = 1;
    public double EditorRow3Csharp { get; set; } = 180;
    public double EditorRow3Sql { get; set; } = 250;
}
public class DockNode
{
    public string Type { get; set; } = "";
    public List<string> Tabs { get; set; } = new();
    public string SelectedTab { get; set; } = "";
    public DockNode Child1 { get; set; } = null;
    public DockNode Child2 { get; set; } = null;
    public double Size1 { get; set; }
    public double Size2 { get; set; }
}