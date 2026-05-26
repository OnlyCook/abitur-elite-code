using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private static int GetDeterministicHashCode(string str)
    {
        unchecked
        {
            int hash1 = (5381 << 16) + 5381;
            int hash2 = hash1;

            for (int i = 0; i < str.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1) break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }

    private void UpdateNavigationButtonTooltips()
    {
        ToolTip.SetTip(BtnPrevLevel, "Vorheriges Level (Shift + Enter)");
        if (_isSqlMode)
        {
            bool allSolved = sqlLevels != null && sqlLevels.All(l => playerData.CompletedSqlLevelIds.Contains(l.Id));
            int totalCount = sqlLevels?.Count ?? SqlCurriculum.GetLevelCount();

            if (currentSqlLevel != null && currentSqlLevel.Id == totalCount)
            {
                if (allSolved)
                    ToolTip.SetTip(BtnNextLevel, "Kurs abschließen (Alt + Enter)");
                else
                    ToolTip.SetTip(BtnNextLevel, "Fehlende Level abschließen");
            }
            else
            {
                ToolTip.SetTip(BtnNextLevel, "Nächstes Level (Alt + Enter)");
            }
        }
        else
        {
            bool allSolved = levels != null && levels.All(l => playerData.CompletedLevelIds.Contains(l.Id));
            int totalCount = levels?.Count ?? Curriculum.GetLevelCount();

            if (currentLevel != null && currentLevel.Id == totalCount)
            {
                if (allSolved)
                    ToolTip.SetTip(BtnNextLevel, "Kurs abschließen (Alt + Enter)");
                else
                    ToolTip.SetTip(BtnNextLevel, "Fehlende Level abschließen");
            }
            else
            {
                ToolTip.SetTip(BtnNextLevel, "Nächstes Level (Alt + Enter)");
            }
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
            string? currentTitle = _isSqlMode ? currentSqlLevel?.Title : currentLevel?.Title;
            var currentInfo = allCustoms.FirstOrDefault(c => c.Name == currentTitle);

            if (currentInfo != null)
            {
                // isolate levels in the root folder so they cant navigate to other unrelated levels
                if (currentInfo.Section == "Einzelne Levels")
                {
                    BtnPrevLevel.IsEnabled = false;
                    BtnPrevLevel.Opacity = 0.5;

                    BtnNextLevel.Content = "✓";
                    BtnNextLevel.IsEnabled = true;
                    _nextCustomLevelPath = "SECTION_COMPLETE";
                }
                else
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
            int idx = sqlLevels != null ? sqlLevels.IndexOf(currentSqlLevel) : -1;
            isFirst = idx <= 0;
            isLast = idx >= sqlLevels?.Count - 1;
            isCurrentCompleted = playerData.CompletedSqlLevelIds.Contains(currentSqlLevel.Id);

            // check if next level exists and is unlocked
            if (!isLast && sqlLevels != null && idx != -1)
            {
                var next = sqlLevels[idx + 1];
                nextIsUnlocked = playerData.UnlockedSqlLevelIds.Contains(next.Id);
            }
        }
        else if (currentLevel != null && levels != null)
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
            string? currentTitle = _isSqlMode ? currentSqlLevel?.Title : currentLevel?.Title;
            var currentInfo = allCustoms.FirstOrDefault(c => c.Name == currentTitle);

            if (currentInfo != null)
            {
                // block backwards navigation for single root-level items
                if (currentInfo.Section == "Einzelne Levels") return;

                var sectionLevels = allCustoms.Where(c => c.Section == currentInfo.Section).OrderBy(c => c.Name).ToList();
                int idx = sectionLevels.FindIndex(c => c.FilePath == currentInfo.FilePath);
                if (idx > 0)
                {
                    LoadCustomLevelFromFile(sectionLevels[idx - 1].FilePath);
                    Dispatcher.UIThread.Post(() =>
                    {
                        // re-focus after button click cycle so KnownLayer.Caret renders (unfocused editor skips caret layer redraws)
                        if (_isSqlMode) { SqlQueryEditor.Focus(); SqlQueryEditor.TextArea.TextView.Redraw(); }
                        else { CodeEditor.Focus(); CodeEditor.TextArea.TextView.Redraw(); }
                        UpdateVimUI();
                    }, DispatcherPriority.Render);
                }
            }

            return;
        }

        if (_isSqlMode && currentSqlLevel != null)
        {
            int idx = sqlLevels != null ? sqlLevels.IndexOf(currentSqlLevel) : -1;
            if (idx > 0 && sqlLevels != null)
            {
                LoadSqlLevel(sqlLevels[idx - 1]);
                Dispatcher.UIThread.Post(() =>
                {
                    // re-focus after button click cycle so KnownLayer.Caret renders (unfocused editor skips caret layer redraws)
                    SqlQueryEditor.Focus();
                    UpdateVimUI();
                    SqlQueryEditor.TextArea.TextView.Redraw();
                }, DispatcherPriority.Render);
            }
        }
        else if (currentLevel != null && levels != null)
        {
            int idx = levels.IndexOf(currentLevel);
            if (idx > 0)
            {
                LoadLevel(levels[idx - 1]);
                Dispatcher.UIThread.Post(() =>
                {
                    // re-focus after button click cycle so KnownLayer.Caret renders (unfocused editor skips caret layer redraws)
                    CodeEditor.Focus();
                    UpdateVimUI();
                    CodeEditor.TextArea.TextView.Redraw();
                }, DispatcherPriority.Render);
            }
        }
    }

    private void BtnNextLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_isSqlMode)
        {
            if (currentSqlLevel != null && currentSqlLevel.Id == (sqlLevels?.Count ?? SqlCurriculum.GetLevelCount()))
            {
                // restrict course completion if not all levels are solved
                bool allSolved = sqlLevels != null && sqlLevels.All(l => playerData.CompletedSqlLevelIds.Contains(l.Id));
                if (!allSolved)
                {
                    AddSqlOutput("System", "> Du musst alle Level lösen, um den Kurs abzuschließen.", Brushes.Orange);
                    return;
                }
            }
        }
        else
        {
            if (currentLevel != null && currentLevel.Id == (levels?.Count ?? Curriculum.GetLevelCount()))
            {
                // restrict course completion if not all levels are solved
                bool allSolved = levels != null && levels.All(l => playerData.CompletedLevelIds.Contains(l.Id));
                if (!allSolved)
                {
                    AddToConsole("\n> Du musst alle Level lösen, um den Kurs abzuschließen.", Brushes.Orange);
                    return;
                }
            }
        }

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
                Dispatcher.UIThread.Post(() =>
                {
                    // re-focus after button click cycle so KnownLayer.Caret renders (unfocused editor skips caret layer redraws)
                    if (_isSqlMode) { SqlQueryEditor.Focus(); SqlQueryEditor.TextArea.TextView.Redraw(); }
                    else { CodeEditor.Focus(); CodeEditor.TextArea.TextView.Redraw(); }
                    UpdateVimUI();
                }, DispatcherPriority.Render);
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
            var nextSqlLvl = sqlLevels?.FirstOrDefault(l => l.SkipCode == currentSqlLevel?.NextLevelCode);
            if (nextSqlLvl != null)
                LoadSqlLevel(nextSqlLvl);
            Dispatcher.UIThread.Post(() =>
            {
                // re-focus after button click cycle so KnownLayer.Caret renders (unfocused editor skips caret layer redraws)
                SqlQueryEditor.Focus();
                UpdateVimUI();
                SqlQueryEditor.TextArea.TextView.Redraw();
            }, DispatcherPriority.Render);
            return;
        }

        var nextLvl = levels?.FirstOrDefault(l => l.SkipCode == currentLevel?.NextLevelCode);
        if (nextLvl != null)
            LoadLevel(nextLvl);
        Dispatcher.UIThread.Post(() =>
        {
            // re-focus after button click cycle so KnownLayer.Caret renders (unfocused editor skips caret layer redraws)
            CodeEditor.Focus();
            UpdateVimUI();
            CodeEditor.TextArea.TextView.Redraw();
        }, DispatcherPriority.Render);
    }

    private async void ShowCourseCompletedDialog()
    {
        var dialog = new Window
        {
            Title = "C# Kurs Abgeschlossen",
            Width = 500,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushBgPanel,
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
                Foreground = Scheme.BrushTextHighlight,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        );
        contentStack.Children.Add(
            new TextBlock
            {
                Text =
                    "Du hast alle C#-Levels erfolgreich abgeschlossen!\n\nDu bist nun bereit für den Programmier-Teil der Abiturprüfung in Praktischer Informatik.\nViel Erfolg!\n\nGehe aber lieber noch ein paar offizielle Abiturvorschläge ganz durch.",
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
            Background = Scheme.BrushTextHighlight,
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

    private async void ShowSqlCourseCompletedDialog()
    {
        var dialog = new Window
        {
            Title = "SQL Kurs Abgeschlossen",
            Width = 500,
            Height = 410,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushBgPanel,
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
                Foreground = Scheme.BrushDopamineEnducingGold,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        );
        contentStack.Children.Add(
            new TextBlock
            {
                Text =
                    "Du hast alle SQL-Levels erfolgreich abgeschlossen!\n\nDatenbank-Abfragen sind ein essenzieller Teil der Prüfung. Du bist nun bestens vorbereitet.\nViel Erfolg!\n\nGehe aber lieber noch ein paar offizielle Abiturvorschläge ganz durch.",
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
            Background = Scheme.BrushDopamineEnducingGold,
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

    private async void ShowCustomSectionCompletedDialog()
    {
        string? section = _isSqlMode ? currentSqlLevel?.Section : currentLevel?.Section;
        bool isSingleLevel = section == "Einzelne Levels";

        var dialog = new Window
        {
            Title = isSingleLevel ? "Level Abgeschlossen" : "Sektion Abgeschlossen",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushBgPanel,
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
            Foreground = Scheme.BrushTextTitle,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        rootStack.Children.Add(new TextBlock
        {
            // adapt text based on level type
            Text = isSingleLevel ? "Du hast dieses eigene Level erfolgreich abgeschlossen." : "Du hast alle Levels in diesem Ordner abgeschlossen.",
            FontSize = 15,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        var btnClose = new Button
        {
            Content = "Schließen",
            Background = Scheme.BrushTextTitle,
            Foreground = Brushes.White,
            Padding = new Thickness(20, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(4)
        };
        btnClose.Click += (_, __) => dialog.Close();

        rootStack.Children.Add(btnClose);
        dialog.Content = new Border { Child = rootStack };

        await dialog.ShowDialog(this);
    }

    private void BtnLevelSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_isSqlMode && sqlLevels == null) sqlLevels = SqlCurriculum.GetLevels();
        levels ??= Curriculum.GetLevels();

        var win = new LevelSelector();
        bool isCustomMode = _isCustomLevelMode;
        bool isCommunityMode = false;

        var btnToggleCommunity = win.FindControl<Button>("BtnToggleCommunity");
        var iconToggleCommunity = win.FindControl<Avalonia.Svg.Skia.Svg>("IconToggleCommunity");

        // drag and drag fields
        CustomLevelInfo? _draggedCustomLevel = null;
        Button? _draggedButton = null;
        Point _dragStartPos = default;
        bool _isDraggingLevel = false;
        Border? _dragGhost = null;
        CustomLevelInfo? _dropTargetLevel = null;
        string? _dropTargetFolder = null;
        string? _folderToFocus = null;

        var overlay = win.FindControl<Canvas>("DragOverlayCanvas");
        var indicators = win.FindControl<Canvas>("DragIndicatorsCanvas");
        var dropPreview = win.FindControl<Border>("DropPreviewOverlay");

        // register global pointer events for dragging
        win.AddHandler(InputElement.PointerMovedEvent, (s, ev) =>
        {
            if (_draggedCustomLevel != null && !_isDraggingLevel)
            {
                var pos = ev.GetPosition(win);
                if (Math.Abs(pos.X - _dragStartPos.X) > 4 || Math.Abs(pos.Y - _dragStartPos.Y) > 4)
                {
                    _isDraggingLevel = true;

                    // override the buttons cursor to ensure it persists during the captured drag pointer state
                    if (_draggedButton != null)
                    {
                        _draggedButton.Cursor = Cursor.Parse("SizeAll");
                    }

                    // enable overlay hit testing to force global cursor consistency
                    if (overlay != null)
                    {
                        overlay.IsHitTestVisible = true;
                        overlay.Background = Brushes.Transparent;
                        overlay.Cursor = Cursor.Parse("SizeAll");
                    }

                    _dragGhost = new Border
                    {
                        Background = Scheme.BrushBgPanel13,
                        BorderBrush = Scheme.BrushTextTitle,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(15, 8),
                        Child = new TextBlock
                        {
                            Text = GetCleanLevelName(_draggedCustomLevel.Name),
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.Bold,
                            FontSize = 12
                        }
                    };
                    if (overlay != null) overlay.Children.Add(_dragGhost);
                }
            }

            if (_isDraggingLevel && _dragGhost != null)
            {
                // align relative to the overlay root to fix misplacing 
                if (overlay != null)
                {
                    var overlayPos = ev.GetPosition(overlay);
                    Canvas.SetLeft(_dragGhost, overlayPos.X + 15);
                    Canvas.SetTop(_dragGhost, overlayPos.Y + 15);
                }

                _dropTargetLevel = null;
                _dropTargetFolder = null;
                Control? hoveredElement = null;

                var pos = ev.GetPosition(win);
                var visuals = win.GetVisualsAt(pos);
                foreach (var v in visuals)
                {
                    if (v is Control c)
                    {
                        // check if hovering anywhere in or on a folder
                        var expander = (c as Expander) ?? c.GetVisualAncestors().OfType<Expander>().FirstOrDefault();
                        if (expander != null && expander.Header is Border headerBorder && headerBorder.Tag is string folderName)
                        {
                            // allow hovering over own folder to prevent bleeding into root drop zone
                            _dropTargetFolder = folderName;
                            hoveredElement = expander;
                            break;
                        }

                        // check if hovering a level row directly
                        var row = (c as Grid) ?? c.GetVisualAncestors().OfType<Grid>().FirstOrDefault(g => g.Tag is CustomLevelInfo);
                        if (row != null && row.Tag is CustomLevelInfo info)
                        {
                            if (info != _draggedCustomLevel)
                            {
                                // only target specific levels when both are inside the standard root selection
                                if (info.Section == "Einzelne Levels" && _draggedCustomLevel?.Section == "Einzelne Levels")
                                {
                                    _dropTargetLevel = info;
                                    hoveredElement = row;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (hoveredElement != null && dropPreview != null && indicators != null)
                {
                    var boundsPos = hoveredElement.TranslatePoint(new Point(0, 0), indicators);
                    if (boundsPos.HasValue)
                    {
                        dropPreview.Opacity = 1;
                        dropPreview.Width = hoveredElement.Bounds.Width;
                        dropPreview.Height = hoveredElement.Bounds.Height;
                        Canvas.SetLeft(dropPreview, boundsPos.Value.X);
                        Canvas.SetTop(dropPreview, boundsPos.Value.Y);
                    }
                }
                else if (dropPreview != null && indicators != null)
                {
                    var contentScroll = win.FindControl<ScrollViewer>("ContentScroll");
                    var contentPos = contentScroll?.TranslatePoint(new Point(0, 0), indicators);

                    // root zone bounds
                    if (contentPos.HasValue && pos.Y > contentPos.Value.Y && pos.Y < contentPos.Value.Y + contentScroll?.Bounds.Height)
                    {
                        // only highlight standard root space if its not already in it
                        if (_draggedCustomLevel?.Section != "Einzelne Levels" && contentScroll != null)
                        {
                            dropPreview.Opacity = 1;

                            // expand the root indicator slightly to distinguish it from large folders
                            double rootExpansion = 4;
                            dropPreview.Width = contentScroll.Bounds.Width + (rootExpansion * 2);
                            dropPreview.Height = contentScroll.Bounds.Height + (rootExpansion * 2);
                            Canvas.SetLeft(dropPreview, contentPos.Value.X - rootExpansion);
                            Canvas.SetTop(dropPreview, contentPos.Value.Y - rootExpansion);
                        }
                        else
                        {
                            dropPreview.Opacity = 0;
                        }
                    }
                    else
                    {
                        dropPreview.Opacity = 0;
                    }
                }
                ev.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        void MoveLevelFile(string srcPath, string destDir)
        {
            if (!File.Exists(srcPath)) return;
            string fileName = Path.GetFileName(srcPath);
            string destPath = Path.Combine(destDir, fileName);
            if (File.Exists(destPath))
                destPath = Path.Combine(destDir, Guid.NewGuid().ToString().Substring(0, 4) + "_" + fileName);
            File.Move(srcPath, destPath);
        }

        win.AddHandler(InputElement.PointerReleasedEvent, (s, ev) =>
        {
            if (_isDraggingLevel)
            {
                // restore the dragged buttons original cursor
                if (_draggedButton != null && _draggedCustomLevel != null)
                {
                    _draggedButton.Cursor = _draggedCustomLevel.IsDraft ? Cursor.Default : Cursor.Parse("Hand");
                }

                // reset overlay properties
                if (overlay != null)
                {
                    overlay.IsHitTestVisible = false;
                    overlay.Background = null;
                    overlay.Cursor = Cursor.Default;
                    overlay.Children.Clear();
                }

                if (dropPreview != null) dropPreview.Opacity = 0;

                string rootPath = SaveSystem.GetLevelsDirectory();
                string prefix = _isSqlMode ? "sql_" : "cs_";

                if (_dropTargetLevel != null && _dropTargetLevel.Section == "Einzelne Levels" && _draggedCustomLevel?.Section == "Einzelne Levels")
                {
                    int folderIdx = 1;
                    string newFolderName;
                    string targetDir;
                    do
                    {
                        newFolderName = $"Ordner-{folderIdx}";
                        targetDir = Path.Combine(rootPath, prefix + newFolderName);
                        folderIdx++;
                    } while (Directory.Exists(targetDir));

                    Directory.CreateDirectory(targetDir);
                    MoveLevelFile(_draggedCustomLevel.FilePath, targetDir);
                    MoveLevelFile(_dropTargetLevel.FilePath, targetDir);

                    _folderToFocus = newFolderName;
                }
                else if (_dropTargetFolder != null && _draggedCustomLevel != null)
                {
                    // only move if dropped on a different folder
                    if (_draggedCustomLevel.Section != _dropTargetFolder)
                    {
                        // check for internal prefixed folder first, fallback to unprefixed manual folder
                        string targetDir = Path.Combine(rootPath, prefix + _dropTargetFolder);
                        if (!Directory.Exists(targetDir))
                            targetDir = Path.Combine(rootPath, _dropTargetFolder);

                        if (Directory.Exists(targetDir))
                            MoveLevelFile(_draggedCustomLevel.FilePath, targetDir);
                    }
                }
                else if (_dropTargetLevel == null && _dropTargetFolder == null && _draggedCustomLevel != null && _draggedCustomLevel.Section != "Einzelne Levels")
                {
                    MoveLevelFile(_draggedCustomLevel.FilePath, rootPath);
                }

                // cleanup any fully empty folders reliably
                foreach (var dir in Directory.GetDirectories(rootPath))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.Name.StartsWith(prefix))
                    {
                        if (!Directory.EnumerateFiles(dir).Any())
                        {
                            try { Directory.Delete(dir); } catch { }
                        }
                    }
                }

                _isDraggingLevel = false;
                _draggedCustomLevel = null;
                _draggedButton = null;
                ev.Handled = true;
                RefreshUI();
            }
            else
            {
                _draggedCustomLevel = null;
                _draggedButton = null;
            }
        }, RoutingStrategies.Tunnel);

        void RenameFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || oldName == newName) return;
            string root = SaveSystem.GetLevelsDirectory();
            string prefix = _isSqlMode ? "sql_" : "cs_";

            // resolve old path (prefixed vs manual)
            string oldPath = Path.Combine(root, prefix + oldName);
            if (!Directory.Exists(oldPath))
                oldPath = Path.Combine(root, oldName);

            // enforce prefix for newly renamed folders to keep backend organized
            string newPath = Path.Combine(root, prefix + newName);

            if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
            {
                try
                {
                    Directory.Move(oldPath, newPath);
                    RefreshUI();
                }
                catch (Exception ex)
                {
                    LogToMiniConsole($"> Fehler beim Umbenennen: {ex.Message}", Brushes.Red, true, false, null, false);
                }
            }
        }

        // contextually open community browser if the level was loaded from there previously
        if (isCustomMode && _openedViaCommunityBrowser)
        {
            if (!UpdateManager.IsOutdated && AppSettings.IsCommunityFeaturesEnabled && !string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                isCommunityMode = true;
            }
        }

        // bind static footer buttons
        win.BtnClose.Click += (_, __) => win.Close();

        win.BtnToggleMode.Click += (_, __) =>
        {
            if (isCommunityMode) isCommunityMode = false;
            isCustomMode = !isCustomMode;
            RefreshUI();
        };

        btnToggleCommunity?.Click += async (_, __) =>
        {
            isCommunityMode = !isCommunityMode;

            iconToggleCommunity?.Path = isCommunityMode ? "/assets/icons/ic_return.svg" : "/assets/icons/ic_publish.svg";
            btnToggleCommunity.Background = isCommunityMode ? Scheme.BrushBgPanel2 : Scheme.BrushGlobalBg;
            ToolTip.SetTip(btnToggleCommunity, isCommunityMode ? "Zurück zu eigenen Levels" : "Öffne Community Browser");

            RefreshUI();

            if (isCommunityMode)
            {
                bool hasInternet = await CheckRealConnectivityAsync();
                bool wasOffline = _isOffline;
                _isOffline = !hasInternet;

                DateTime lastFetch = _isSqlMode ? _lastCommunityFetchTimeSql : _lastCommunityFetchTimeCs;
                double remaining = 60 - (DateTime.Now - lastFetch).TotalSeconds;

                var txtSearchBox = win.SearchContainer.Child as TextBox;
                string? searchTxt = txtSearchBox?.Text;

                if (_isOffline)
                {
                    RenderCommunityBrowser(win, searchTxt);
                }
                else
                {
                    if (remaining > 0)
                    {
                        if (wasOffline)
                        {
                            LogToMiniConsole($"> Cooldown aktiv. Bitte warte noch {Math.Ceiling(remaining)} Sekunden.", Brushes.Orange, false, false, null, false);
                        }
                        RenderCommunityBrowser(win, searchTxt);
                    }
                    else
                    {
                        await FetchCommunityMetadataAsync(win);
                    }
                }
            }
        };

        string? currentMiniConsoleError = null;

        win.BtnMiniConsoleClose.Click += (_, __) => win.MiniConsolePanel.IsVisible = false;

        // filter flyout
        var filterFlyout = new Flyout();
        var filterStack = new StackPanel
        {
            Spacing = 10,
            Width = 250
        };
        filterStack.Children.Add(new TextBlock
        {
            Text = "Schwierigkeit",
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        });
        var diffPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        foreach (var diff in new[] { "Einfach", "Mittel", "Schwer", "Abitur" })
        {
            var cb = new CheckBox
            {
                Content = diff,
                Margin = new Thickness(0, 0, 10, 5)
            };
            cb.IsCheckedChanged += (s, ev) =>
            {
                if (cb.IsChecked == true) _communitySelectedDifficulties.Add(diff);
                else _communitySelectedDifficulties.Remove(diff);
                RenderCommunityBrowser(win);
            };
            diffPanel.Children.Add(cb);
        }
        filterStack.Children.Add(diffPanel);

        filterStack.Children.Add(new TextBlock
        {
            Text = "Tags",
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 10, 0, 0)
        });
        var tagPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        filterStack.Children.Add(tagPanel);
        filterFlyout.Content = filterStack;
        win.FindControl<Button>("BtnCommunityFilter")?.Flyout = filterFlyout;

        win.FindControl<ComboBox>("CmbCommunitySort")?.SelectionChanged += (s, ev) => RenderCommunityBrowser(win);

        win.BtnMiniConsoleClose.Click += (_, __) => win.MiniConsolePanel.IsVisible = false;
        win.BtnMiniConsoleCopy.Click += async (_, __) =>
        {
            var topLevel = GetTopLevel(win);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(currentMiniConsoleError))
            {
                await topLevel.Clipboard.SetTextAsync("Error: " + currentMiniConsoleError);
                win.BtnMiniConsoleCopy.Background = Scheme.BrushApprovedBg;
                win.BtnMiniConsoleCopy.Content = LoadIcon("assets/icons/ic_success.svg", 14);
                await Task.Delay(500);
                win.BtnMiniConsoleCopy.Background = Scheme.BrushBgPanel2;
                win.BtnMiniConsoleCopy.Content = LoadIcon("assets/icons/ic_copy.svg", 14);
            }
        };

        void LogToMiniConsole(string msg, IBrush color, bool append = true, bool isError = false, string? fullError = null, bool isQuickGen = true)
        {
            Dispatcher.UIThread.Post(() =>
            {
                win.MiniConsolePanel.IsVisible = true;
                if (!append) win.MiniConsoleText.Inlines?.Clear();
                win.MiniConsoleTitle.Text = isQuickGen ? "Quick Generate Log" : "Error Log";

                win.MiniConsoleText.Inlines ??= new Avalonia.Controls.Documents.InlineCollection();

                ProcessTextWithEmojis(msg + "\n", color, win.MiniConsoleText.Inlines);

                if (isError)
                {
                    win.BtnMiniConsoleCopy.IsVisible = true;
                    currentMiniConsoleError = fullError;
                }
                else if (!append)
                {
                    win.BtnMiniConsoleCopy.IsVisible = false;
                    currentMiniConsoleError = null;
                }
            });
        }

        // ui refresh logic
        void RefreshUI()
        {
            win.MiniConsolePanel.IsVisible = false;
            win.SearchContainer.Child = null;
            win.ContentScroll.Content = null;
            win.ContentScroll.IsVisible = !isCommunityMode;

            var toolsGrid = win.FindControl<Grid>("CommunityToolsGrid");
            var commScroll = win.FindControl<ScrollViewer>("CommunityScroll");
            if (toolsGrid != null) toolsGrid.IsVisible = false; // hiding original tools row
            commScroll?.IsVisible = isCommunityMode;

            win.HeaderRightPanel.Children.Clear();

            if (isCommunityMode)
            {
                win.BtnToggleMode.IsVisible = false;
            }
            else
            {
                win.BtnToggleMode.IsVisible = true;
                win.IconToggleMode.Path = isCustomMode ? "/assets/icons/ic_folder.svg" : "/assets/icons/ic_folder_custom.svg";
                win.TxtToggleMode.Text = isCustomMode ? "Standard Levels" : "Eigene Levels";
            }

            btnToggleCommunity?.IsVisible = !UpdateManager.IsOutdated && AppSettings.IsCommunityFeaturesEnabled && !string.IsNullOrEmpty(AppSettings.GithubToken) && isCustomMode;

            if (isCommunityMode)
            {
                win.TxtTitle.Text = "Browser";
                win.CountBadge.IsVisible = false;

                TextBox? txtSearch = null;

                var cmbSort = new ComboBox
                {
                    Background = Scheme.BrushBgPanel3,
                    Foreground = Brushes.White,
                    BorderBrush = Scheme.BrushBgPanel5,
                    CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 5, 0),
                    Height = 34
                };
                cmbSort.Items.Add(new ComboBoxItem { Content = "Beste" });
                cmbSort.Items.Add(new ComboBoxItem { Content = "Top" });
                cmbSort.Items.Add(new ComboBoxItem { Content = "Neuste" });
                cmbSort.Items.Add(new ComboBoxItem { Content = "Älteste" });

                var selectedItem = cmbSort.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Content?.ToString() == _communitySortMode);
                cmbSort.SelectedItem = selectedItem ?? cmbSort.Items.ElementAt(0);

                cmbSort.SelectionChanged += (s, ev) =>
                {
                    _communitySortMode = (cmbSort.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Beste";
                    RenderCommunityBrowser(win, txtSearch?.Text);
                };

                var btnFilter = new Button
                {
                    Background = Scheme.BrushBgPanel2,
                    Width = 34,
                    Height = 34,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                };
                UpdateFilterButtonIcon(btnFilter);

                var filterFlyout = new Flyout();
                var filterStack = new StackPanel
                {
                    Spacing = 10,
                    Width = 250
                };

                filterStack.Children.Add(new TextBlock
                {
                    Text = "Schwierigkeit",
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White
                });
                var diffPanel = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var diff in new[] { "Einfach", "Mittel", "Schwer", "Abitur" })
                {
                    var cb = MakeFilterCheckBox(
                        diff,
                        isWhitelisted: _communitySelectedDifficulties.Contains(diff),
                        isBlacklisted: _communityBlacklistDifficulties.Contains(diff),
                        onChanged: state =>
                        {
                            _communitySelectedDifficulties.Remove(diff);
                            _communityBlacklistDifficulties.Remove(diff);
                            if (state == true) _communitySelectedDifficulties.Add(diff);
                            if (state == null) _communityBlacklistDifficulties.Add(diff);
                            UpdateFilterButtonIcon(btnFilter);
                            RenderCommunityBrowser(win);
                        });
                    diffPanel.Children.Add(cb);
                }
                filterStack.Children.Add(diffPanel);

                filterStack.Children.Add(new TextBlock
                {
                    Text = "Tags",
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                var tagPanel = new WrapPanel { Orientation = Orientation.Horizontal };

                string[] currentTags = _isSqlMode ? SqlTags : CSharpTags;
                foreach (var t in currentTags)
                {
                    var cb = MakeFilterCheckBox(
                        t,
                        isWhitelisted: _communitySelectedTags.Contains(t),
                        isBlacklisted: _communityBlacklistTags.Contains(t),
                        onChanged: state =>
                        {
                            _communitySelectedTags.Remove(t);
                            _communityBlacklistTags.Remove(t);
                            if (state == true) _communitySelectedTags.Add(t);
                            if (state == null) _communityBlacklistTags.Add(t);
                            UpdateFilterButtonIcon(btnFilter);
                            RenderCommunityBrowser(win, txtSearch?.Text);
                        });
                    tagPanel.Children.Add(cb);
                }
                filterStack.Children.Add(tagPanel);

                filterFlyout.Content = filterStack;
                btnFilter.Flyout = filterFlyout;

                win.HeaderRightPanel.Children.Add(cmbSort);
                win.HeaderRightPanel.Children.Add(btnFilter);

                txtSearch = new TextBox
                {
                    Watermark = "Level/Autor suchen...",
                    MinWidth = 150,
                    Height = 34,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = Scheme.BrushBgPanel7,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Scheme.BrushBgPanel5,
                    CornerRadius = new CornerRadius(4)
                };
                txtSearch.TextChanged += (s, e) => RenderCommunityBrowser(win, txtSearch.Text);
                win.SearchContainer.Child = txtSearch;

                RenderCommunityBrowser(win, txtSearch.Text);
                return;
            }

            if (!isCustomMode)
            {
                // title and badge
                if (_isSqlMode && sqlLevels != null)
                {
                    win.TxtTitle.Text = "SQL Levels";
                    int completedCount = sqlLevels.Count(l => playerData.CompletedSqlLevelIds.Contains(l.Id));
                    win.BadgeText.Text = $"{completedCount}/{sqlLevels.Count}";
                }
                else if (levels != null)
                {
                    win.TxtTitle.Text = "C# Levels";
                    int completedCount = levels.Count(l => playerData.CompletedLevelIds.Contains(l.Id));
                    win.BadgeText.Text = $"{completedCount}/{levels.Count}";
                }

                win.CountBadge.IsVisible = true;

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
                    Watermark = "–––",
                    Width = 60,
                    MaxLength = 3,
                    Background = Scheme.BrushBgPanel7,
                    Foreground = Brushes.White,
                    BorderBrush = Scheme.BrushBgPanel5,
                    CornerRadius = new CornerRadius(4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
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
                            var lvl = sqlLevels?.FirstOrDefault(l => l.SkipCode == code);
                            if (lvl != null)
                            {
                                // actually unlock the level (permanently)
                                if (!playerData.UnlockedSqlLevelIds.Contains(lvl.Id))
                                {
                                    playerData.UnlockedSqlLevelIds.Add(lvl.Id);
                                    SaveSystem.Save(playerData);
                                }

                                LoadSqlLevel(lvl);
                                win.Close();
                            }
                        }
                        else
                        {
                            var lvl = levels?.FirstOrDefault(l => l.SkipCode == code);
                            if (lvl != null)
                            {
                                // actually unlock the level (permanently)
                                if (!playerData.UnlockedLevelIds.Contains(lvl.Id))
                                {
                                    playerData.UnlockedLevelIds.Add(lvl.Id);
                                    SaveSystem.Save(playerData);
                                }

                                LoadLevel(lvl);
                                win.Close();
                            }
                        }
                    }
                };
                codePanel.Children.Add(txtLevelCode);

                var btnLevelGuide = new Button
                {
                    Content = LoadIcon("assets/icons/ic_guide.svg", 16),
                    Background = Scheme.BrushTextHighlight2,
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTip.SetTip(btnLevelGuide, "Level Codes & Lösungen");
                btnLevelGuide.Click += (_, __) =>
                {
                    try
                    {
                        var url = $"https://github.com/OnlyCook/abitur-elite-code/blob/main/py/LEVEL_CODES.md{(_isSqlMode ? "#sql-levels" : "")}";
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
                    }
                    catch
                    {
                    }
                };
                codePanel.Children.Add(btnLevelGuide);

                win.HeaderRightPanel.Children.Add(codePanel);

                // level list
                var levelStack = new StackPanel { Spacing = 8 };

                if (_isSqlMode && sqlLevels != null)
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
                            Foreground = Scheme.BrushTextTitle,
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
                            string iconPath = completed ? "assets/icons/ic_check.svg" :
                                unlocked ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_lock.svg";
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
                                Background = unlocked
                                    ? Scheme.BrushBgPanel14
                                    : Scheme.BrushDiffFallbackBg,
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
                else if (levels != null)
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
                            Foreground = Scheme.BrushTextTitle,
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
                            string iconPath = completed ? "assets/icons/ic_check.svg" :
                                unlocked ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_lock.svg";
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
                                Background = unlocked
                                    ? Scheme.BrushBgPanel14
                                    : Scheme.BrushDiffFallbackBg,
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

                win.ContentScroll.Content = levelStack;
            }
            else
            {
                // custom levels
                win.TxtTitle.Text = "Eigene Levels";
                win.CountBadge.IsVisible = false;

                var customStack = new StackPanel { Spacing = 5 };
                var customLevels = GetCustomLevels();
                var rootLevels = customLevels.Where(x => x.Section == "Einzelne Levels").OrderBy(x => x.Name).ToList();
                var folderGroups = customLevels.Where(x => x.Section != "Einzelne Levels").GroupBy(x => x.Section)
                    .OrderBy(g => g.Key).ToList();

                // search Box
                var txtSearch = new TextBox
                {
                    Watermark = "Suchen...",
                    MinWidth = 150,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = Scheme.BrushBgPanel7,
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Scheme.BrushBgPanel5,
                    CornerRadius = new CornerRadius(4)
                };

                // search logic
                txtSearch.TextChanged += (s, e) =>
                {
                    string query = txtSearch.Text?.ToLower() ?? "";
                    foreach (var child in customStack.Children)
                        if (child is Expander exp && exp.Content is StackPanel groupPanel)
                        {
                            bool groupHasMatch = false;
                            foreach (var item in groupPanel.Children)
                                if (item is Grid row && row.Tag is CustomLevelInfo info)
                                {
                                    // use clean name for search
                                    var cleanName = GetCleanLevelName(info.Name);
                                    bool match = cleanName != null && (cleanName.ToLower().Contains(query) ||
                                                 info.Author.ToLower().Contains(query));
                                    row.IsVisible = match;
                                    if (match) groupHasMatch = true;
                                }

                            exp.IsVisible = groupHasMatch;
                            if (!string.IsNullOrEmpty(query)) exp.IsExpanded = true;
                            else exp.IsExpanded = false;
                        }
                        else if (child is Grid row && row.Tag is CustomLevelInfo info)
                        {
                            // use clean name for searchv
                            var cleanName = GetCleanLevelName(info.Name);
                            bool match =cleanName != null && (cleanName.ToLower().Contains(query) ||
                                info.Author.ToLower().Contains(query));
                            row.IsVisible = match;
                        }
                };
                win.SearchContainer.Child = txtSearch;

                // custom level header buttons
                var btnOpenFolder = new Button
                {
                    Content = LoadIcon("assets/icons/ic_folder_open.svg", 18),
                    Background = Scheme.BrushBgPanel2,
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTip.SetTip(btnOpenFolder, "Levels Ordner öffnen");
                btnOpenFolder.Click += (_, __) => OpenLevelsFolder();
                win.HeaderRightPanel.Children.Add(btnOpenFolder);

                var btnAdd = new Button
                {
                    Content = LoadIcon("assets/icons/ic_add.svg", 18),
                    Background = Scheme.BrushTextTitle,
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(4)
                };
                ToolTip.SetTip(btnAdd, "Neues Level erstellen");
                btnAdd.Click += async (_, __) =>
                {
                    string? newPath = await ShowAddLevelDialog(win);
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        _newlyCreatedLevelPath = newPath;
                        RefreshUI();
                    }
                };
                win.HeaderRightPanel.Children.Add(btnAdd);

                if (!customLevels.Any())
                {
                    customStack.Children.Add(new TextBlock
                    {
                        Text = $"Keine eigenen {(_isSqlMode ? "SQL" : "C#")} Levels gefunden.\nErstelle eins mit '+' oder \nöffne den Ordner und füge Levels hinzu.",
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
                        else if (_isSqlMode
                                     ? customPlayerData.CompletedCustomSqlLevels.Contains(cl.Name)
                                     : customPlayerData.CompletedCustomLevels.Contains(cl.Name))
                            iconPath = "assets/icons/ic_check.svg";
                        else iconPath = "assets/icons/ic_lock_open.svg";

                        var iconImage = LoadIcon(iconPath, 16);
                        iconImage.Margin = new Thickness(0, 0, 10, 0);
                        iconImage.VerticalAlignment = VerticalAlignment.Center;

                        var btnContentGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto, *")
                        };

                        Grid.SetColumn(iconImage, 0);
                        btnContentGrid.Children.Add(iconImage);

                        var textStack = new StackPanel { Spacing = 2 };
                        Grid.SetColumn(textStack, 1);

                        // use clean name for ui rendering
                        string? displayName = GetCleanLevelName(cl.Name);
                        if (_isSqlMode && AppSettings.IsSqlAntiSpoilerEnabled && cl.Section != null &&
                            displayName != null && !cl.Section.StartsWith("Sektion 7"))
                            displayName = Regex.Replace(displayName, @"\s*\(.*?\)", "").Trim();

                        // title panel to place the publish icon before the title
                        var titlePanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5
                        };

                        if (cl.HasCommunityId)
                        {
                            var commIcon = LoadIcon("assets/icons/ic_publish.svg", 16);
                            commIcon.VerticalAlignment = VerticalAlignment.Center;
                            titlePanel.Children.Add(commIcon);
                        }

                        titlePanel.Children.Add(new TextBlock
                        {
                            Text = displayName + (cl.IsDraft ? " (Entwurf)" : ""),
                            Foreground = cl.IsDraft ? Brushes.Orange : Brushes.White,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            VerticalAlignment = VerticalAlignment.Center
                        });

                        textStack.Children.Add(titlePanel);

                        var authorGrid = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto, *")
                        };

                        var vonTextBlock = new TextBlock
                        {
                            Text = "von ",
                            FontSize = 11,
                            Foreground = Brushes.Gray
                        };
                        Grid.SetColumn(vonTextBlock, 0);
                        authorGrid.Children.Add(vonTextBlock);

                        IBrush authorForeground = Brushes.Gray;
                        if (cl.HasCommunityId && !string.IsNullOrEmpty(AppSettings.GithubUsername) && cl.Author.Equals(AppSettings.GithubUsername, StringComparison.OrdinalIgnoreCase))
                        {
                            // highlight author name if community browser level and user is author
                            authorForeground = Scheme.BrushTextHighlight;
                        }

                        var authorTextBlock = new TextBlock
                        {
                            Text = cl.Author,
                            FontSize = 11,
                            Foreground = authorForeground,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };
                        Grid.SetColumn(authorTextBlock, 1);
                        authorGrid.Children.Add(authorTextBlock);

                        textStack.Children.Add(authorGrid);

                        btnContentGrid.Children.Add(textStack);

                        var btnMain = new Button
                        {
                            Content = btnContentGrid,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Background = cl.FilePath == _newlyCreatedLevelPath
                                ? Scheme.BrushApprovedBg
                                : Scheme.BrushBgPanel14,
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(10)
                        };
                        if (!_isDraggingLevel) btnMain.Cursor = cl.IsDraft ? Cursor.Default : Cursor.Parse("Hand");

                        // drag start
                        btnMain.AddHandler(InputElement.PointerPressedEvent, (s, ev) =>
                        {
                            if (!ev.GetCurrentPoint(win).Properties.IsLeftButtonPressed) return;
                            _draggedCustomLevel = cl;
                            _draggedButton = btnMain;
                            _dragStartPos = ev.GetPosition(win);
                            _isDraggingLevel = false;
                        }, RoutingStrategies.Tunnel);

                        // remove highlight after delay
                        if (cl.FilePath == _newlyCreatedLevelPath)
                        {
                            var timer = new DispatcherTimer
                            {
                                Interval = TimeSpan.FromSeconds(2)
                            };
                            timer.Tick += (s, args) =>
                            {
                                btnMain.Background = Scheme.BrushBgPanel14;
                                _newlyCreatedLevelPath = null;
                                timer.Stop();
                            };
                            timer.Start();
                        }

                        btnMain.Click += (_, __) =>
                        {
                            if (_isDraggingLevel) return;

                            if (!cl.IsDraft)
                            {
                                _openedViaCommunityBrowser = false;
                                LoadCustomLevelFromFile(cl.FilePath);
                                win.Close();
                            }
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

                                var cts = new CancellationTokenSource();
                                EventHandler<WindowClosingEventArgs> closingHandler = (sender, args) => cts.Cancel();
                                win.Closing += closingHandler;

                                try
                                {
                                    if (_isSqlMode)
                                    {
                                        LogToMiniConsole($"> Quick Export gestartet für: {GetCleanLevelName(cl.Name)}...", Brushes.LightGray, false);
                                        var draft = SqlLevelDesigner.LoadDraft(cl.FilePath);

                                        var validData = await Task.Run<(bool Success, List<SqlExpectedColumn> Schema, List<string[]> Result)>(() =>
                                        {
                                            try
                                            {
                                                using (var connection =
                                                        new SqliteConnection("Data Source=:memory:"))
                                                {
                                                    connection.Open();

                                                    using (var limitCmd = connection.CreateCommand())
                                                    {
                                                        limitCmd.CommandText = "PRAGMA hard_heap_limit = 250000000;";
                                                        limitCmd.ExecuteNonQuery();
                                                    }

                                                    using (cts.Token.Register(() =>
                                                    {
                                                        try { connection.Close(); } catch { }
                                                        try { connection.Dispose(); } catch { }
                                                    }))
                                                    {
                                                        // run setup code
                                                        using (var setupCmd = connection.CreateCommand())
                                                        {
                                                            setupCmd.CommandText = draft.SetupScript;
                                                            using (cts.Token.Register(() =>
                                                            {
                                                                try { setupCmd.Cancel(); } catch { }
                                                                try { connection.Close(); } catch { }
                                                                try { connection.Dispose(); } catch { }
                                                            }))
                                                            {
                                                                try
                                                                {
                                                                    setupCmd.ExecuteNonQuery();
                                                                }
                                                                catch (Exception) when (cts.Token.IsCancellationRequested)
                                                                {
                                                                    cts.Token.ThrowIfCancellationRequested();
                                                                }
                                                            }
                                                        }

                                                        // exclude empty input buffers
                                                        var cleanedSchema = draft.ExpectedSchema
                                                        .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                                                        .ToList();
                                                        int validCols = cleanedSchema.Count;

                                                        var cleanedResult = new List<string[]>();
                                                        foreach (var r in draft.ExpectedResult)
                                                        {
                                                            var rowData = r.Take(validCols).Select(c => c ?? "")
                                                                .ToArray();
                                                            if (rowData.Any(c => !string.IsNullOrWhiteSpace(c)))
                                                                cleanedResult.Add(rowData);
                                                        }

                                                        if (validCols == 0)
                                                            throw new Exception(
                                                                "Die Erwartungstabelle (Expected Table) darf nicht komplett leer sein.");

                                                        DataTable? actualDt = null;
                                                        string sampleSolution =
                                                            SqlLevelTester.ConvertMysqlToSqlite(connection,
                                                                draft.SampleSolution);

                                                        if (draft.IsDmlMode)
                                                        {
                                                            using (var dmlCmd = connection.CreateCommand())
                                                            {
                                                                dmlCmd.CommandText = sampleSolution;
                                                                using (cts.Token.Register(() =>
                                                                {
                                                                    try { dmlCmd.Cancel(); } catch { }
                                                                    try { connection.Close(); } catch { }
                                                                    try { connection.Dispose(); } catch { }
                                                                }))
                                                                {
                                                                    try
                                                                    {
                                                                        dmlCmd.ExecuteNonQuery();
                                                                    }
                                                                    catch (Exception) when (cts.Token.IsCancellationRequested)
                                                                    {
                                                                        cts.Token.ThrowIfCancellationRequested();
                                                                    }
                                                                }
                                                            }

                                                            if (string.IsNullOrWhiteSpace(draft.VerificationQuery))
                                                                throw new Exception(
                                                                    "Im DML Modus muss eine Verifizierungs-Abfrage angegeben werden.");

                                                            string verifyQuery =
                                                                SqlLevelTester.ConvertMysqlToSqlite(connection,
                                                                    draft.VerificationQuery, cts.Token);
                                                            actualDt = ExecuteDbQuery(connection, verifyQuery, cts.Token);
                                                        }
                                                        else
                                                        {
                                                            actualDt = ExecuteDbQuery(connection, sampleSolution, cts.Token);
                                                        }

                                                        if (actualDt.Columns.Count != validCols)
                                                            throw new Exception(
                                                                $"Spaltenanzahl stimmt nicht überein. Erwartet: {validCols}, Ist: {actualDt.Columns.Count}");

                                                        for (int i = 0; i < validCols; i++)
                                                            if (!actualDt.Columns[i].ColumnName
                                                                    .Equals(cleanedSchema[i].Name,
                                                                        StringComparison.OrdinalIgnoreCase))
                                                                throw new Exception(
                                                                    $"Spaltenname an Position {i + 1} stimmt nicht. Erwartet: '{cleanedSchema[i].Name}', Ist: '{actualDt.Columns[i].ColumnName}'");

                                                        if (actualDt.Rows.Count != cleanedResult.Count)
                                                            throw new Exception(
                                                                $"Zeilenanzahl stimmt nicht überein. Erwartet: {cleanedResult.Count}, Ist: {actualDt.Rows.Count}");

                                                        for (int r = 0; r < cleanedResult.Count; r++)
                                                            for (int c = 0; c < validCols; c++)
                                                            {
                                                                string expectedVal = cleanedResult[r][c] ?? "";
                                                                if (expectedVal == "") expectedVal = "NULL";

                                                                string actualVal = actualDt.Rows[r][c]?.ToString()
                                                                    ?.Replace(",", ".") ?? "";
                                                                if (actualDt.Rows[r][c] == DBNull.Value ||
                                                                    string.IsNullOrEmpty(actualVal)) actualVal = "NULL";

                                                                if (double.TryParse(expectedVal, NumberStyles.Any,
                                                                        CultureInfo.InvariantCulture,
                                                                        out double expNum) &&
                                                                    double.TryParse(actualVal, NumberStyles.Any,
                                                                        CultureInfo.InvariantCulture,
                                                                        out double actNum))
                                                                {
                                                                    if (Math.Abs(expNum - actNum) > 0.0001)
                                                                        throw new Exception(
                                                                            $"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                                                                }
                                                                else if (!expectedVal.Equals(actualVal,
                                                                                StringComparison.OrdinalIgnoreCase))
                                                                {
                                                                    throw new Exception(
                                                                        $"Wert in Zeile {r + 1}, Spalte {c + 1} stimmt nicht. Erwartet: '{expectedVal}', Ist: '{actualVal}'");
                                                                }
                                                            }

                                                        return (true, cleanedSchema, cleanedResult);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (ex is TargetInvocationException tie && tie.InnerException != null) throw tie.InnerException;
                                                throw;
                                            }
                                        }, cts.Token);

                                        if (!validData.Success) throw new Exception("Validierung fehlgeschlagen.");

                                        LogToMiniConsole("> Generiere Diagramme...", Brushes.LightGray, true);

                                        if (!string.IsNullOrWhiteSpace(draft.PlantUmlSource))
                                        {
                                            string prepared = PreparePlantUmlSource(draft.PlantUmlSource);
                                            draft.PlantUmlSvgContent =
                                                await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                        }

                                        SqlLevelDesigner.ExportLevel(cl.FilePath, draft, validData.Schema, validData.Result);
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_success.svg", 16);
                                        LogToMiniConsole($"@S {GetCleanLevelName(cl.Name)} erfolgreich exportiert!", Brushes.LightGreen, true);

                                        _newlyCreatedLevelPath = cl.FilePath.Replace(".eliteslvldraft", ".eliteslvl", StringComparison.OrdinalIgnoreCase);
                                        await Task.Delay(2000);
                                        RefreshUI();
                                    }
                                    else
                                    {
                                        LogToMiniConsole($"> Quick Export gestartet für: {GetCleanLevelName(cl.Name)}...", Brushes.LightGray, false);
                                        var draft = LevelDesigner.LoadDraft(cl.FilePath);

                                        bool valid = await Task.Run(async () =>
                                        {
                                            try
                                            {
                                                string fullCode =
                                                    "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\n" +
                                                    draft.TestCode;
                                                string validatorCode =
                                                    "using System;\nusing System.Reflection;\nusing System.Collections.Generic;\nusing System.Linq;\npublic static class DesignerValidator { " +
                                                    draft.ValidationCode + " }";

                                                var references = GetSafeReferences();

                                                // before compiling run security checks!
                                                var testTree = CSharpSyntaxTree.ParseText(fullCode, cancellationToken: cts.Token);
                                                var testComp = CSharpCompilation.Create("SecCheckTest", new[] { testTree }, references);
                                                var testSecurity = SandboxSecurity.AnalyzeUserCode(testTree, testComp.GetSemanticModel(testTree), false);
                                                if (!testSecurity.IsSafe) throw new Exception("Test-Code blockiert: " + testSecurity.ErrorFeedback);

                                                var valSecurityTree = CSharpSyntaxTree.ParseText(validatorCode, cancellationToken: cts.Token);
                                                var valComp = CSharpCompilation.Create("SecCheckVal", new[] { valSecurityTree }, references);
                                                var valSecurity = SandboxSecurity.AnalyzeUserCode(valSecurityTree, valComp.GetSemanticModel(valSecurityTree), true);
                                                if (!valSecurity.IsSafe) throw new Exception("Validierungs-Code blockiert: " + valSecurity.ErrorFeedback);

                                                var tree = CSharpSyntaxTree.ParseText(fullCode,
                                                    cancellationToken: cts.Token);
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
                                                        var diag = result.Diagnostics.FirstOrDefault(d =>
                                                            d.Severity == DiagnosticSeverity.Error);
                                                        throw new Exception(
                                                            $"Kompilierfehler: {diag?.GetMessage() ?? "Unbekannt"}");
                                                    }

                                                    ms.Seek(0, SeekOrigin.Begin);
                                                    var assembly = Assembly.Load(ms.ToArray());

                                                    // compile validator
                                                    var valTree = CSharpSyntaxTree.ParseText(validatorCode,
                                                        cancellationToken: cts.Token);
                                                    var valCompilation = CSharpCompilation.Create(
                                                        $"Validator_{Guid.NewGuid()}",
                                                        new[] { valTree },
                                                        references,
                                                        new CSharpCompilationOptions(
                                                            OutputKind.DynamicallyLinkedLibrary));

                                                    using (var valMs = new MemoryStream())
                                                    {
                                                        var valResult = valCompilation.Emit(valMs,
                                                            cancellationToken: cts.Token);
                                                        if (!valResult.Success)
                                                            throw new Exception("Fehler im Validierungs-Code.");

                                                        valMs.Seek(0, SeekOrigin.Begin);
                                                        var valAssembly = Assembly.Load(valMs.ToArray());
                                                        var valType = valAssembly.GetType("DesignerValidator");
                                                        var valMethod = valType?.GetMethods(BindingFlags.Public |
                                                                BindingFlags.NonPublic | BindingFlags.Static)
                                                            .FirstOrDefault(m =>
                                                                m.ReturnType == typeof(bool) &&
                                                                m.GetParameters().Length == 2);

                                                        // run validation
                                                        object?[] args = { assembly, null };
                                                        bool passed = (bool)valMethod!.Invoke(null, args)!;

                                                        if (!passed)
                                                            throw new Exception(
                                                                $"Validierung nicht bestanden: {args[1]}");
                                                        return true;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (ex is TargetInvocationException tie && tie.InnerException != null) throw tie.InnerException;
                                                throw;
                                            }
                                        }, cts.Token);

                                        if (!valid) throw new Exception("Validierung fehlgeschlagen.");

                                        // generate diagrams
                                        LogToMiniConsole("> Generiere Diagramme...", Brushes.LightGray, true);

                                        if (draft.PlantUmlSources != null && draft.PlantUmlSources.Count > 0 &&
                                            !string.IsNullOrWhiteSpace(draft.PlantUmlSources[0]))
                                        {
                                            string prepared = PreparePlantUmlSource(draft.PlantUmlSources[0]);
                                            string svgContent = await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                            if (draft.PlantUmlSvgContents == null)
                                                draft.PlantUmlSvgContents = new List<string>();
                                            if (draft.PlantUmlSvgContents.Count == 0) draft.PlantUmlSvgContents.Add("");
                                            draft.PlantUmlSvgContents[0] = svgContent;
                                        }

                                        for (int i = 0; i < draft.MaterialDiagrams.Count; i++)
                                        {
                                            if (!string.IsNullOrWhiteSpace(draft.MaterialDiagrams[i].PlantUmlSource))
                                            {
                                                string prepared =
                                                    PreparePlantUmlSource(draft.MaterialDiagrams[i].PlantUmlSource);
                                                draft.MaterialDiagrams[i].PlantUmlSvgContent =
                                                    await PlantUmlHelper.GenerateSvgFromCodeAsync(prepared);
                                            }
                                        }

                                        // export
                                        LevelDesigner.ExportLevel(cl.FilePath, draft);
                                        btnQuickExport.Content = LoadIcon("assets/icons/ic_success.svg", 16);
                                        LogToMiniConsole($"@S {GetCleanLevelName(cl.Name)} erfolgreich exportiert!", Brushes.LightGreen, true);

                                        _newlyCreatedLevelPath = cl.FilePath.Replace(".elitelvldraft", ".elitelvl", StringComparison.OrdinalIgnoreCase);
                                        await Task.Delay(2000);
                                        RefreshUI();
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                }
                                catch (Exception ex)
                                {
                                    string errorMsg = ex.Message;
                                    LogToMiniConsole($"@E Export Fehler ({GetCleanLevelName(cl.Name)}): {errorMsg}", Brushes.Red, true, true, errorMsg);

                                    btnQuickExport.Content = LoadIcon("assets/icons/ic_error.svg", 16);
                                    btnQuickExport.IsEnabled = true;
                                    btnQuickExport.Tag = "idle";
                                    await Task.Delay(2000);
                                    btnQuickExport.Content = LoadIcon("assets/icons/ic_generate.svg", 16);
                                }
                                finally
                                {
                                    win.Closing -= closingHandler;
                                    cts.Dispose();

                                    // reset button state after delay
                                    await Task.Delay(2000);
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
                                Content = LoadIcon("assets/icons/ic_edit.svg", 17),
                                Background = Brushes.Transparent,
                                Padding = new Thickness(7)
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
                            Content = LoadIcon("assets/icons/ic_delete.svg", 17),
                            Background = Brushes.Transparent,
                            Padding = new Thickness(7)
                        };
                        ToolTip.SetTip(btnDelete, "Level löschen");
                        btnDelete.Click += async (_, __) =>
                        {
                            await DeleteCustomLevel(cl, win);
                            RefreshUI();
                        };
                        actionPanel.Children.Add(btnDelete);

                        rowGrid.Children.Add(actionPanel);
                        return rowGrid;
                    }

                    // show groups
                    foreach (var group in folderGroups)
                    {
                        var groupContent = new StackPanel
                        {
                            Spacing = 5,
                            Margin = new Thickness(0, 5, 0, 0)
                        };

                        foreach (var cl in group)
                            groupContent.Children.Add(CreateLevelRow(cl));

                        var headerBorder = new Border
                        {
                            Tag = group.Key,
                            Background = Brushes.Transparent,
                            Padding = new Thickness(0, 5)
                        };

                        var headerPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 10
                        };

                        var txtFolder = new TextBox
                        {
                            Text = group.Key,
                            Foreground = Scheme.BrushTextTitle,
                            FontWeight = FontWeight.Bold,
                            Background = Brushes.Transparent,
                            BorderBrush = Brushes.Transparent,
                            VerticalAlignment = VerticalAlignment.Center,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Padding = new Thickness(5, 0),
                            Margin = new Thickness(-5, 0, 0, 0)
                        };

                        txtFolder.PointerEntered += (s, ev) =>
                        {
                            txtFolder.BorderBrush = Scheme.BrushBgPanel15;
                            txtFolder.Background = Scheme.BrushBgPanel3;
                        };
                        txtFolder.PointerExited += (s, ev) =>
                        {
                            if (!txtFolder.IsFocused)
                            {
                                txtFolder.BorderBrush = Brushes.Transparent;
                                txtFolder.Background = Brushes.Transparent;
                            }
                        };
                        txtFolder.GotFocus += (s, ev) =>
                        {
                            txtFolder.BorderBrush = Scheme.BrushBgPanel15;
                            txtFolder.Background = Scheme.BrushBgPanel3;
                        };
                        txtFolder.LostFocus += (s, ev) =>
                        {
                            txtFolder.BorderBrush = Brushes.Transparent;
                            txtFolder.Background = Brushes.Transparent;
                            RenameFolder(group.Key, txtFolder.Text);
                        };
                        txtFolder.KeyDown += (s, ev) =>
                        {
                            if (ev.Key == Key.Enter)
                            {
                                win.Focus(); // force removing focus logically which resolves rename execution
                                ev.Handled = true; // stop from bleeding to folder
                            }
                        };
                        txtFolder.MaxLength = 50;

                        headerPanel.Children.Add(txtFolder);

                        bool allComplete = group.All(l =>
                            !l.IsDraft && (_isSqlMode
                                ? customPlayerData.CompletedCustomSqlLevels.Contains(l.Name)
                                : customPlayerData.CompletedCustomLevels.Contains(l.Name)));

                        if (allComplete && group.Any())
                            headerPanel.Children.Add(LoadIcon("assets/icons/ic_done.svg", 16));

                        headerBorder.Child = headerPanel;

                        customStack.Children.Add(new Expander
                        {
                            Header = headerBorder,
                            Content = groupContent,
                            IsExpanded = false,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 0, 5)
                        });

                        // automatically focus and prep name field if it was newly created this cycle
                        if (_folderToFocus == group.Key)
                        {
                            Dispatcher.UIThread.Post(() => {
                                txtFolder.Focus();
                                txtFolder.SelectAll();
                                _folderToFocus = null;
                            });
                        }
                    }

                    foreach (var cl in rootLevels) customStack.Children.Add(CreateLevelRow(cl));
                }

                win.ContentScroll.Content = customStack;
            }
        }

        RefreshUI();

        // fetch data initially if contextual community mode was evaluated to true
        if (isCommunityMode)
        {
            iconToggleCommunity?.Path = "/assets/icons/ic_return.svg";
            btnToggleCommunity?.Background = Scheme.BrushBgPanel2;
            if (btnToggleCommunity != null) ToolTip.SetTip(btnToggleCommunity, "Zurück zu eigenen Levels");

            Dispatcher.UIThread.Post(async () =>
            {
                bool hasInternet = await CheckRealConnectivityAsync();
                bool wasOffline = _isOffline;
                _isOffline = !hasInternet;

                DateTime lastFetch = _isSqlMode ? _lastCommunityFetchTimeSql : _lastCommunityFetchTimeCs;
                double remaining = 60 - (DateTime.Now - lastFetch).TotalSeconds;

                var txtSearchBox = win.SearchContainer.Child as TextBox;
                string? searchTxt = txtSearchBox?.Text;

                if (_isOffline)
                {
                    RenderCommunityBrowser(win, searchTxt);
                }
                else
                {
                    if (remaining > 0)
                    {
                        if (wasOffline)
                        {
                            LogToMiniConsole($"> Cooldown aktiv. Bitte warte noch {Math.Ceiling(remaining)} Sekunden.", Brushes.Orange, false, false, null, false);
                        }
                        RenderCommunityBrowser(win, searchTxt);
                    }
                    else
                    {
                        await FetchCommunityMetadataAsync(win);
                    }
                }
            });
        }

        win.ShowDialog(this);
        CodeEditor.Focus();
    }

    private void LoadLevel(Level level)
    {
        SaveCurrentProgress();

        if (level.Id > 0)
        {
            _isCustomLevelMode = false;
            _currentCustomValidationCode = null;
            _currentCustomAuthor = "";
            _currentCustomSvgs = null;
            _nextCustomLevelPath = null;
            _currentCustomDiscussionNodeId = null;
            _currentCustomDiscussionNumber = -1;
        }

        // reset error highlighting on every load
        AppSettings.IsErrorHighlightingEnabled = false;
        ClearDiagnostics();

        currentLevel = level;
        _currentDiagramIndex = 0;
        UpdateNavigationButtons();

        BtnCustomLevelReturn.IsVisible = _isCustomLevelMode && !_isDesignerMode;

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
                BtnDiagram1.Background = Scheme.BrushTextHighlight2;
                BtnDiagram2.Background = Scheme.BrushBgPanel2;
                BtnDiagram3.Background = Scheme.BrushBgPanel2;

                if (level.DiagramPaths.Count >= 3) BtnDiagram3.IsVisible = true;
            }
        }
        else
        {
            ImgDiagram.Source = null;
            TxtNoDiagram.IsVisible = true;
        }

        string rawCode = level.StarterCode ?? "";
        if (_isCustomLevelMode)
        {
            // custom levels
            if (customPlayerData.UserCode.ContainsKey(level.Title!)) rawCode = customPlayerData.UserCode[level.Title!];
        }
        else
        {
            // standard levels
            if (playerData.UserCode.ContainsKey(level.Id)) rawCode = playerData.UserCode[level.Id];
        }

        // clear autocomplete and pre-reset caret to prevent ghost text layout paradox
        _csharpAutocompleteService?.ClearSuggestion();
        CodeEditor.CaretOffset = 0;

        CodeEditor.Text = rawCode;
        CodeEditor.CaretOffset = 0; // reset caret pos
        CodeEditor.TextArea.Caret.Line = 1;
        CodeEditor.TextArea.Caret.Column = 1;

        // reset vim state and clear floating carets
        CodeEditor.TextArea.ClearSelection();
        _vimMode = VimMode.Normal;
        _vimDesiredColumn = -1;

        // fresh renderer instance to flush any stale visual-line state from ghost text
        CodeEditor.TextArea.TextView.BackgroundRenderers.Remove(_csharpBlockCaret);
        _csharpBlockCaret = new VimBlockCaretRenderer(CodeEditor);
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(_csharpBlockCaret);

        UpdateVimUI();
        CodeEditor.TextArea.TextView.InvalidateVisual();

        // reset uml zoom
        if (!_isSqlMode && !level.NoUMLAutoScale)
            _currentScale = 0.5;
        else
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
                    Text = GetCleanLevelName(level.Title),
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = Scheme.BrushTextNormal,
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
                    Text = $"{level.Id}. {GetCleanLevelName(level.Title)}",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Foreground = Scheme.BrushTextNormal,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                }
            );
        }

        WrapPanel? tagsPanel = BuildTagsPanel(level.Difficulty, level.Topics, level.DiagramTags, false, _isCustomLevelMode && _currentCustomDiscussionNumber != -1);
        if (tagsPanel != null) PnlTask.Children.Add(tagsPanel);

        RenderRichText(PnlTask, level.Description);

        try
        {
            if (level.DiagramPaths != null && level.DiagramPaths.Count > 0)
            {
                ImgDiagram?.Source = LoadDiagramImage(level.DiagramPaths[0]);
                TxtNoDiagram.IsVisible = false;
            }
            else
            {
                ImgDiagram?.Source = null;
                TxtNoDiagram.IsVisible = true;
            }
        }
        catch
        {
            ImgDiagram?.Source = null;
            TxtNoDiagram.IsVisible = true;
        }

        GenerateMaterials(level, _isCustomLevelMode ? _currentCustomSvgs : null);

        if (!_isCustomLevelMode)
            AddToConsole($"> System initialisiert.\n> Level {level.Id} (Code: {level.SkipCode}) geladen.",
                Brushes.LightGray, true);
        else
            AddToConsole("> System initialisiert.", Brushes.LightGray, true);

        DiscordRpcManager.ResetTimer();
        if (_isCustomLevelMode)
            DiscordRpcManager.UpdatePresence("C# Custom Level", "Solving a custom level", "aec_app_icon", "Custom");
        else
            DiscordRpcManager.UpdatePresence($"C# Level {level.Id}", "Coding greatness", "chsarp_icon", "C#");

        UpdateSemanticHighlighting(); // init scan

        _ = UpdateCommunityUIAsync(level.Id.ToString(), false);

        // ensure editor scrolls to top
        Dispatcher.UIThread.Post(() => {
            CodeEditor.Focus();
            CodeEditor.ScrollTo(1, 1);
        });
    }

    private CheckBox MakeFilterCheckBox(string label, bool isWhitelisted, bool isBlacklisted, Action<bool?> onChanged)
    {
        var cb = new CheckBox
        {
            Content = label,
            Margin = new Thickness(0, 0, 10, 5),
            IsThreeState = true,
            IsChecked = isWhitelisted ? true : isBlacklisted ? null : false
        };

        cb.Template = BuildTriStateCheckBoxTemplate();

        cb.IsCheckedChanged += (s, ev) => onChanged(cb.IsChecked);
        return cb;
    }

    private static IControlTemplate BuildTriStateCheckBoxTemplate()
    {
        return new FuncControlTemplate<CheckBox>((checkBox, scope) =>
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("22, Auto"),
                VerticalAlignment = VerticalAlignment.Center,
                // stretch the entire row as the clickable area
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(4, 2, 8, 2),
                Cursor = Cursor.Parse("Hand")
            };

            var hitArea = new Border
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumnSpan(hitArea, 2);
            grid.Children.Add(hitArea);

            var iconCanvas = new Canvas
            {
                Width = 18,
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false
            };
            Grid.SetColumn(iconCanvas, 0);

            var border = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(2),
                BorderBrush = Scheme.BrushBgPanel12,
                Background = Brushes.Transparent
            };

            var hoverBorder = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                Background = Scheme.BrushTriCheckEnlight,
                IsVisible = false,
                IsHitTestVisible = false
            };

            var checkPath = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M 3,9 L 7,13 L 15,4"),
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                IsVisible = false,
                IsHitTestVisible = false
            };

            var crossPath = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M 4,4 L 14,14 M 14,4 L 4,14"),
                Stroke = Scheme.BrushTriCheckIgnoreFg,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                IsVisible = false,
                IsHitTestVisible = false
            };

            iconCanvas.Children.Add(border);
            iconCanvas.Children.Add(hoverBorder);
            iconCanvas.Children.Add(checkPath);
            iconCanvas.Children.Add(crossPath);
            grid.Children.Add(iconCanvas);

            var contentPresenter = new ContentPresenter
            {
                [!ContentPresenter.ContentProperty] = checkBox[!CheckBox.ContentProperty],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                IsHitTestVisible = false
            };
            Grid.SetColumn(contentPresenter, 1);
            grid.Children.Add(contentPresenter);

            void UpdateVisuals()
            {
                switch (checkBox.IsChecked)
                {
                    case true:
                        border.Background = Scheme.BrushTextHighlight;
                        border.BorderBrush = Scheme.BrushTextHighlight;
                        checkPath.IsVisible = true;
                        crossPath.IsVisible = false;
                        break;
                    case null:
                        border.Background = Scheme.BrushTriCheckIgnoreBg;
                        border.BorderBrush = Scheme.BrushTriCheckIgnoreFg;
                        checkPath.IsVisible = false;
                        crossPath.IsVisible = true;
                        break;
                    default:
                        border.Background = Brushes.Transparent;
                        border.BorderBrush = Scheme.BrushBgPanel12;
                        checkPath.IsVisible = false;
                        crossPath.IsVisible = false;
                        break;
                }
            }

            grid.PointerEntered += (_, _) =>
            {
                hoverBorder.IsVisible = true;
                border.BorderBrush = checkBox.IsChecked switch
                {
                    true => Scheme.BrushTriCheckIncludeHoverFg,
                    null => Scheme.BrushCheckIgnoreHoverFg,
                    _ => Scheme.BrushTriCheckBg
                };
            };

            grid.PointerExited += (_, _) =>
            {
                hoverBorder.IsVisible = false;
                // restore correct border color for current state
                UpdateVisuals();
            };

            checkBox.IsCheckedChanged += (_, _) => UpdateVisuals();
            checkBox.Loaded += (_, _) => UpdateVisuals();

            return grid;
        });
    }

    private void UpdateFilterButtonIcon(Button btnFilter)
    {
        bool hasFilter = _communitySelectedDifficulties.Any()
                      || _communityBlacklistDifficulties.Any()
                      || _communitySelectedTags.Any()
                      || _communityBlacklistTags.Any();

        btnFilter.Content = LoadIcon(hasFilter ? "assets/icons/ic_filter_filled.svg" : "assets/icons/ic_filter.svg", 18);
    }

    private void OpenLevelsFolder()
    {
        string path = SaveSystem.GetLevelsDirectory();
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", path);
        }
        catch (Exception ex)
        {
            AddToConsole($"\n> Fehler beim Öffnen des Ordners: {ex.Message}", Brushes.Orange);
        }
    }

    private List<CustomLevelInfo> GetCustomLevels()
    {
        var list = new List<CustomLevelInfo>();
        string rootPath = SaveSystem.GetLevelsDirectory();

        if (!Directory.Exists(rootPath)) return list;

        (string? name, string? author, bool quickGen, bool hasCommunityId) GetMetadata(string file)
        {
            try
            {
                string json = File.ReadAllText(file);

                if (!file.EndsWith("draft", StringComparison.OrdinalIgnoreCase) && !json.TrimStart().StartsWith("{"))
                    json = LevelEncryption.Decrypt(json);

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    // check for both name (c#) and title (sql) before falling back to filename
                    string? name = root.TryGetProperty("Name", out var n) ? n.GetString() :
                                  root.TryGetProperty("Title", out var t) ? t.GetString() :
                                  Path.GetFileNameWithoutExtension(file);

                    string? author = root.TryGetProperty("Author", out var a) ? a.GetString() : "Unbekannt";

                    bool quickGen = false;
                    if (root.TryGetProperty("QuickGenerate", out var qg))
                    {
                        if (qg.ValueKind == JsonValueKind.True) quickGen = true;
                        if (qg.ValueKind == JsonValueKind.String && qg.GetString()?.ToLower() == "true") quickGen = true;
                    }

                    bool hasCommId = root.TryGetProperty("DiscussionNumber", out _) || root.TryGetProperty("DiscussionNodeId", out _);

                    return (name, author, quickGen, hasCommId);
                }
            }
            catch
            {
                return (Path.GetFileNameWithoutExtension(file), "Fehler", false, false);
            }
        }

        void ScanDirectory(string dir, string sectionName)
        {
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir, _isSqlMode ? "*.eliteslvl" : "*.elitelvl"))
            {
                var meta = GetMetadata(file);
                if (meta.name != null && meta.author != null)
                {
                    list.Add(new CustomLevelInfo
                    {
                        Name = meta.name,
                        Author = meta.author,
                        FilePath = file,
                        Section = sectionName,
                        IsDraft = false,
                        HasCommunityId = meta.hasCommunityId
                    });
                }
            }

            foreach (var file in Directory.GetFiles(dir, _isSqlMode ? "*.eliteslvldraft" : "*.elitelvldraft"))
            {
                var meta = GetMetadata(file);
                if (meta.name != null && meta.author != null)
                {
                    list.Add(new CustomLevelInfo
                    {
                        Name = meta.name,
                        Author = meta.author,
                        FilePath = file,
                        Section = sectionName,
                        IsDraft = true,
                        QuickGenerate = meta.quickGen,
                        HasCommunityId = meta.hasCommunityId
                    });
                }
            }
        }

        // scan all subdirectories mapping them accurately and removing prefix for ui if present
        string prefix = _isSqlMode ? "sql_" : "cs_";
        foreach (var subdir in Directory.GetDirectories(rootPath))
        {
            string dirName = new DirectoryInfo(subdir).Name;
            string sectionName = dirName.StartsWith(prefix) ? dirName.Substring(prefix.Length) : dirName;

            ScanDirectory(subdir, sectionName);
        }

        ScanDirectory(rootPath, "Einzelne Levels");
        return list;
    }

    private async Task<string?> ShowAddLevelDialog(Window owner)
    {
        var dialog = new Window
        {
            Title = "Neues Level",
            Width = 400,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushTextNormal3,
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) dialog.Close(); };

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
            Background = Scheme.BrushBadgeDefault,
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
            Background = Scheme.BrushBadgeDefault,
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
            Background = Scheme.BrushBadgeDefault,
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
            Foreground = Scheme.BrushDeniedFg,
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
            Background = Scheme.BrushBgPanel2,
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnCopyError, "Fehler kopieren");
        btnCopyError.Click += async (s, e) =>
        {
            var topLevel = GetTopLevel(dialog);
            if (topLevel?.Clipboard != null && !string.IsNullOrEmpty(fullErrorText))
            {
                await topLevel.Clipboard.SetTextAsync(fullErrorText);
                btnCopyError.Background = Scheme.BrushApprovedBg; // flash green
                btnCopyError.Content = LoadIcon("assets/icons/ic_success.svg", 14); // temporarily change icon to success
                await Task.Delay(500);
                btnCopyError.Background = Scheme.BrushBgPanel2;
                btnCopyError.Content = LoadIcon("assets/icons/ic_copy.svg", 14);
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
            Background = Scheme.BrushBgPanel2,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnGuide, "Anleitung öffnen");
        btnGuide.Click += (_, __) =>
        {
            try
            {
                var url = _isSqlMode
                    ? "https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_AI_LEVEL_CREATION_GUIDE"
                    : "https://github.com/OnlyCook/abitur-elite-code/wiki/CS_AI_LEVEL_CREATION_GUIDE";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Process.Start("xdg-open", url);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) Process.Start("open", url);
            }
            catch
            {
            }
        };

        var btnPaste = new Button
        {
            Content = LoadIcon("assets/icons/ic_import.svg", 18),
            Background = Scheme.BrushTextHighlight2,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnPaste, "Aus Zwischenablage einfügen");
        btnPaste.Click += async (_, __) =>
        {
            var topLevel = GetTopLevel(dialog);
            if (topLevel?.Clipboard != null)
            {
#pragma warning disable CS0618
                string? text = await topLevel.Clipboard.GetTextAsync();
#pragma warning restore CS0618
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
            Foreground = Scheme.BrushAiModeFg,
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
                btnSwitchMode.Foreground = Scheme.BrushAiModeFg;
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
            Background = Scheme.BrushBgPanel2,
            Foreground = Brushes.White
        };
        var btnCreate = new Button
        {
            Content = "Erstellen",
            Background = Scheme.BrushTextTitle,
            Foreground = Brushes.White
        };

        string? resultPath = null;

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
                            throw new Exception("JSON muss 'Name' und 'Author' enthalten.");

                        string? name = nameProp.GetString();
                        string safeName = string.Join("_", (name ?? "").Split(Path.GetInvalidFileNameChars()));
                        string filename = $"{safeName}.{(_isSqlMode ? "eliteslvldraft" : "elitelvldraft")}";
                        string dir = SaveSystem.GetLevelsDirectory();
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
                    string dir = SaveSystem.GetLevelsDirectory();
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    string path = Path.Combine(dir, filename);

                    if (txtAuthor.Text != null)
                    {
                        var newDraft = new LevelDraft
                        {
                            Name = txtName.Text,
                            Author = txtAuthor.Text
                        };

                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string json = JsonSerializer.Serialize(newDraft, options);

                        File.WriteAllText(path, json);
                        resultPath = path;
                    }

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

    private async Task DeleteCustomLevel(CustomLevelInfo info, Window owner)
    {
        bool hasSubscriptions = false;

        // check if the level is connected to the community and has any active subscriptions before showing dialog
        if (info.HasCommunityId && File.Exists(info.FilePath))
        {
            try
            {
                string json = File.ReadAllText(info.FilePath);
                if (!info.IsDraft && !json.TrimStart().StartsWith("{"))
                    json = LevelEncryption.Decrypt(json);

                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("DiscussionNodeId", out var dNodeId))
                    {
                        string? nodeId = dNodeId.GetString();
                        if (!string.IsNullOrEmpty(nodeId))
                        {
                            // check if the main level thread is subscribed
                            if (_communityCache.Subscriptions.ContainsKey(nodeId))
                            {
                                hasSubscriptions = true;
                            }
                            else
                            {
                                // check deeply if any associated comments or replies are subscribed
                                var matchingCs = _communityCache.CsharpDiscussions.Where(kvp => kvp.Value.DiscussionNodeId == nodeId).ToList();
                                var matchingSql = _communityCache.SqlDiscussions.Where(kvp => kvp.Value.DiscussionNodeId == nodeId).ToList();

                                bool CheckDeepSubscriptions(List<KeyValuePair<string, DiscussionCache>> list)
                                {
                                    foreach (var kvp in list)
                                    {
                                        foreach (var comment in kvp.Value.Comments)
                                        {
                                            if (comment.Id == null) continue;
                                            if (_communityCache.Subscriptions.ContainsKey(comment.Id)) return true;
                                            foreach (var reply in comment.Replies)
                                            {
                                                if (reply.Id == null) continue;
                                                if (_communityCache.Subscriptions.ContainsKey(reply.Id)) return true;
                                            }
                                        }
                                    }
                                    return false;
                                }

                                hasSubscriptions = CheckDeepSubscriptions(matchingCs) || CheckDeepSubscriptions(matchingSql);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        var dialog = new Window
        {
            Title = "Löschen?",
            Width = 350,
            Height = hasSubscriptions ? 220 : 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushTextNormal3,
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            Margin = new Thickness(20)
        };

        string dialogText = $"Möchtest du '{GetCleanLevelName(info.Name)}' wirklich löschen?";
        if (hasSubscriptions)
        {
            dialogText += "\n\nHinweis: Durch das Löschen dieses Levels wirst du automatisch von allen zugehörigen Community-Benachrichtigungen (Level, Kommentare) deabonniert.";
        }

        grid.Children.Add(new TextBlock
        {
            Text = dialogText,
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
            Background = Scheme.BrushDiffHard,
            Foreground = Brushes.White
        };
        var btnNo = new Button
        {
            Content = "Abbrechen",
            Background = Scheme.BrushBgPanel2,
            Foreground = Brushes.White
        };

        btnNo.Click += (_, __) => dialog.Close();
        btnYes.Click += (_, __) =>
        {
            try
            {
                if (File.Exists(info.FilePath))
                {
                    // unsubscribe from community level discussion and all its comments before deleting
                    try
                    {
                        Debug.WriteLine("[Custom] Subscription Count Before: " + _communityCache.Subscriptions.Count);

                        string json = File.ReadAllText(info.FilePath);
                        if (!info.IsDraft && !json.TrimStart().StartsWith("{"))
                            json = LevelEncryption.Decrypt(json);

                        using (var doc = JsonDocument.Parse(json))
                        {
                            if (doc.RootElement.TryGetProperty("DiscussionNodeId", out var dNodeId))
                            {
                                string? nodeId = dNodeId.GetString();
                                if (!string.IsNullOrEmpty(nodeId))
                                {
                                    bool cacheChanged = false;

                                    // unsubscribe from the main level
                                    if (_communityCache.Subscriptions.Remove(nodeId))
                                    {
                                        cacheChanged = true;
                                    }

                                    // find all discussions in cache matching this nodeId to ensure we dont miss comments from duplicate/downloaded levels
                                    var matchingCsDiscussions = _communityCache.CsharpDiscussions.Where(kvp => kvp.Value.DiscussionNodeId == nodeId).ToList();
                                    var matchingSqlDiscussions = _communityCache.SqlDiscussions.Where(kvp => kvp.Value.DiscussionNodeId == nodeId).ToList();

                                    foreach (var kvp in matchingCsDiscussions)
                                    {
                                        foreach (var comment in kvp.Value.Comments)
                                        {
                                            if (comment.Id == null) continue;
                                            if (_communityCache.Subscriptions.Remove(comment.Id)) cacheChanged = true;
                                            foreach (var reply in comment.Replies)
                                            {
                                                if (reply.Id == null) continue;
                                                if (_communityCache.Subscriptions.Remove(reply.Id)) cacheChanged = true;
                                            }
                                        }
                                        // remove the cache entry itself to prevent orphaned data
                                        _communityCache.CsharpDiscussions.Remove(kvp.Key);
                                        cacheChanged = true;
                                    }

                                    foreach (var kvp in matchingSqlDiscussions)
                                    {
                                        foreach (var comment in kvp.Value.Comments)
                                        {
                                            if (comment.Id == null) continue;
                                            if (_communityCache.Subscriptions.Remove(comment.Id)) cacheChanged = true;
                                            foreach (var reply in comment.Replies)
                                            {
                                                if (reply.Id == null) continue;
                                                if (_communityCache.Subscriptions.Remove(reply.Id)) cacheChanged = true;
                                            }
                                        }
                                        // remove the cache entry itself to prevent orphaned data
                                        _communityCache.SqlDiscussions.Remove(kvp.Key);
                                        cacheChanged = true;
                                    }

                                    if (cacheChanged) SaveSystem.SaveCommunityCache(_communityCache);
                                }
                            }
                        }

                        Debug.WriteLine("[Custom] Subscription Count After: " + _communityCache.Subscriptions.Count());
                    }
                    catch { }

                    File.Delete(info.FilePath);
                }

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

    private void LoadCustomLevelFromFile(string path)
    {
        string parentDirName = new DirectoryInfo(Path.GetDirectoryName(path)!).Name;
        string sectionName = parentDirName.Equals("levels", StringComparison.OrdinalIgnoreCase) ? "Einzelne Levels" : parentDirName;

        if (path.EndsWith(".eliteslvl", StringComparison.OrdinalIgnoreCase))
        {
            string json = File.ReadAllText(path);

            if (!json.TrimStart().StartsWith("{")) json = LevelEncryption.Decrypt(json);

            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                int customId;
                if (root.TryGetProperty("DiscussionNumber", out var dNum1))
                {
                    customId = -dNum1.GetInt32();
                }
                else
                {
                    customId = GetDeterministicHashCode(Path.GetFileName(path));
                    if (customId > 0) customId *= -1;
                }

                var loadedLevel = new SqlLevel
                {
                    Id = customId,
                    Title = root.TryGetProperty("Title", out var titleProp) ? titleProp.GetString() : "Unbekannt",
                    Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                    Difficulty = root.TryGetProperty("Difficulty", out var diffProp) ? diffProp.GetString() : "",
                    MaterialDocs = root.TryGetProperty("MaterialDocs", out var matProp) ? matProp.GetString() : "",
                    SetupScript = root.TryGetProperty("SetupScript", out var setupProp) ? setupProp.GetString() : "",
                    VerificationQuery = root.TryGetProperty("VerificationQuery", out var vqProp)
                        ? vqProp.GetString()
                        : "",
                    SkipCode = "CUST",
                    Section = sectionName,
                    Prerequisites = new List<string>(),
                    ExpectedSchema = new List<SqlExpectedColumn>(),
                    ExpectedResult = new List<string[]>(),
                    DiagramPaths = new List<string>(),
                    PlantUMLSources = new List<string>()
                };

                // parse relational model properties
                if (root.TryGetProperty("IsRelationalModelReadOnly", out var rmroProp))
                    loadedLevel.IsRelationalModelReadOnly = rmroProp.GetBoolean();

                loadedLevel.InitialRelationalModel = new List<RTable>();
                if (root.TryGetProperty("InitialRelationalModel", out var irmListElem))
                {
                    foreach (var tableElem in irmListElem.EnumerateArray())
                    {
                        var t = new RTable
                        {
                            Name = tableElem.GetProperty("Name").GetString(),
                            Columns = new List<RColumn>()
                        };
                        if (tableElem.TryGetProperty("Columns", out var colsElem))
                        {
                            foreach (var colElem in colsElem.EnumerateArray())
                            {
                                t.Columns.Add(new RColumn
                                {
                                    Name = colElem.GetProperty("Name").GetString(),
                                    IsPk = colElem.GetProperty("IsPk").GetBoolean(),
                                    IsFk = colElem.GetProperty("IsFk").GetBoolean()
                                });
                            }
                        }
                        loadedLevel.InitialRelationalModel.Add(t);
                    }
                }

                if (root.TryGetProperty("Prerequisites", out var prereqElem))
                    foreach (var p in prereqElem.EnumerateArray())
                        loadedLevel.Prerequisites.Add(p.GetString()!);

                loadedLevel.Topics = new List<string>();
                if (root.TryGetProperty("Tags", out var tagsElem))
                    foreach (var t in tagsElem.EnumerateArray())
                        loadedLevel.Topics.Add(t.GetString()!);

                if (root.TryGetProperty("ExpectedSchema", out var schemaElem))
                    foreach (var col in schemaElem.EnumerateArray())
                        loadedLevel.ExpectedSchema.Add(new SqlExpectedColumn
                        {
                            Name = col.GetProperty("Name").GetString(),
                            Type = col.GetProperty("Type").GetString(),
                            StrictName = col.GetProperty("StrictName").GetBoolean()
                        });

                if (root.TryGetProperty("ExpectedResult", out var resElem))
                    foreach (var row in resElem.EnumerateArray())
                    {
                        var arr = new string[row.GetArrayLength()];
                        int i = 0;
                        // replace commas with periods cuz globalization issues
                        foreach (var cell in row.EnumerateArray()) arr[i++] = cell.GetString()?.Replace(",", ".") ?? "";
                        loadedLevel.ExpectedResult.Add(arr);
                    }

                if (root.TryGetProperty("Author", out var authorElem))
                    _currentCustomAuthor = authorElem.GetString();

                if (root.TryGetProperty("DiagramPaths", out var svgsListElem))
                {
                    int idx = 0;
                    foreach (var svgElem1 in svgsListElem.EnumerateArray())
                    {
                        string? svgContent = svgElem1.GetString();
                        if (!string.IsNullOrEmpty(svgContent))
                        {
                            string tempSvgPath = Path.Combine(Path.GetTempPath(),
                                $"elite_custom_{Math.Abs(customId)}_{idx}.svg");
                            File.WriteAllText(tempSvgPath, svgContent);
                            loadedLevel.DiagramPaths.Add(tempSvgPath);
                        }

                        idx++;
                    }
                }

                if (root.TryGetProperty("PlantUMLSources", out var srcListElem))
                    foreach (var s in srcListElem.EnumerateArray())
                        loadedLevel.PlantUMLSources.Add(s.GetString()!);

                if (root.TryGetProperty("DiscussionNodeId", out var dNodeId))
                    _currentCustomDiscussionNodeId = dNodeId.GetString();
                else
                    _currentCustomDiscussionNodeId = null;

                if (root.TryGetProperty("DiscussionNumber", out var dNum))
                {
                    _currentCustomDiscussionNumber = dNum.GetInt32();
                    // inject into mappings so ui can resolve the discussion
                    if (_discussionMappings != null)
                    {
                        if (!_discussionMappings.ContainsKey("sql")) _discussionMappings["sql"] = new Dictionary<string, int>();
                        if (!_discussionMappings.ContainsKey("SQL")) _discussionMappings["SQL"] = new Dictionary<string, int>();
                        _discussionMappings["sql"][customId.ToString()] = _currentCustomDiscussionNumber;
                        _discussionMappings["SQL"][customId.ToString()] = _currentCustomDiscussionNumber;
                    }
                }
                else
                {
                    _currentCustomDiscussionNumber = -1;
                }

                _isCustomLevelMode = true;
                _nextCustomLevelPath = null;

                LoadSqlLevel(loadedLevel);
                AddSqlOutput("System", $"> Custom Level geladen: {GetCleanLevelName(loadedLevel.Title)}", Brushes.LightGreen);
            }

            return;
        }

        string json2 = File.ReadAllText(path);

        if (!json2.TrimStart().StartsWith("{")) json2 = LevelEncryption.Decrypt(json2);

        using (var doc = JsonDocument.Parse(json2))
        {
            var root = doc.RootElement;
            int customId;
            if (root.TryGetProperty("DiscussionNumber", out var dNumCsharp1))
            {
                customId = -dNumCsharp1.GetInt32();
            }
            else
            {
                customId = GetDeterministicHashCode(Path.GetFileName(path));
                if (customId > 0) customId *= -1;
            }

            var loadedLevel = new Level
            {
                Id = customId,
                Title = root.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() :
                    root.TryGetProperty("Title", out var titleProp2) ? titleProp2.GetString() : "Unbekannt",
                Description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : "",
                Difficulty = root.TryGetProperty("Difficulty", out var diffPropCs) ? diffPropCs.GetString() : "",
                StarterCode = root.TryGetProperty("StarterCode", out var scProp) ? scProp.GetString() : "",
                MaterialDocs = root.TryGetProperty("MaterialDocs", out var matProp) ? matProp.GetString() : "",
                SkipCode = "CUST",
                Section = sectionName,
                Prerequisites = new List<string>(),
                AuxiliaryIds = new List<string>(),
                DiagramPaths = new List<string>(),
                PlantUMLSources = new List<string>()
            };

            if (root.TryGetProperty("Author", out var authorElem))
                _currentCustomAuthor = authorElem.GetString();

            if (root.TryGetProperty("Prerequisites", out var prereqElem))
                foreach (var p in prereqElem.EnumerateArray())
                    loadedLevel.Prerequisites.Add(p.GetString()!);

            loadedLevel.Topics = new List<string>();
            if (root.TryGetProperty("Tags", out var tagsElem))
                foreach (var t in tagsElem.EnumerateArray())
                    loadedLevel.Topics.Add(t.GetString()!);

            if (root.TryGetProperty("PlantUmlSvg", out var svgElem))
            {
                string? svgContent = svgElem.GetString();
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
                foreach (var s in svgsElem.EnumerateArray())
                    _currentCustomSvgs.Add(s.GetString()!);

            if (root.TryGetProperty("PlantUmlSvgs", out var svgsListElem))
            {
                int idx = 0;
                foreach (var svgElem1 in svgsListElem.EnumerateArray())
                {
                    string? svgContent = svgElem1.GetString();
                    if (!string.IsNullOrEmpty(svgContent))
                    {
                        string tempSvgPath = Path.Combine(Path.GetTempPath(),
                            $"elite_custom_{Math.Abs(customId)}_{idx}.svg");
                        File.WriteAllText(tempSvgPath, svgContent);
                        loadedLevel.DiagramPaths.Add(tempSvgPath);
                    }

                    idx++;
                }
            }
            else if (root.TryGetProperty("PlantUmlSvg", out var singleSvgElem)) // fallback
            {
                string? svgContent = singleSvgElem.GetString();
                if (!string.IsNullOrEmpty(svgContent))
                {
                    string tempSvgPath = Path.Combine(Path.GetTempPath(), $"elite_custom_{Math.Abs(customId)}.svg");
                    File.WriteAllText(tempSvgPath, svgContent);
                    loadedLevel.DiagramPaths.Add(tempSvgPath);
                }
            }

            if (root.TryGetProperty("PlantUmlSources", out var srcListElem))
                foreach (var s in srcListElem.EnumerateArray())
                    loadedLevel.PlantUMLSources.Add(s.GetString()!);
            else if (root.TryGetProperty("PlantUmlSource", out var singleSrcElem)) // fallback
                loadedLevel.PlantUMLSources.Add(singleSrcElem.GetString()!);

            _currentCustomValidationCode =
                root.TryGetProperty("ValidationCode", out var valProp) ? valProp.GetString() : "";

            if (root.TryGetProperty("DiscussionNodeId", out var dNodeIdCsharp))
                _currentCustomDiscussionNodeId = dNodeIdCsharp.GetString();
            else
                _currentCustomDiscussionNodeId = null;

            if (root.TryGetProperty("DiscussionNumber", out var dNumCsharp))
            {
                _currentCustomDiscussionNumber = dNumCsharp.GetInt32();
                // inject into mappings so ui can resolve the discussion
                if (_discussionMappings != null)
                {
                    if (!_discussionMappings.ContainsKey("csharp")) _discussionMappings["csharp"] = new Dictionary<string, int>();
                    if (!_discussionMappings.ContainsKey("C#")) _discussionMappings["C#"] = new Dictionary<string, int>();
                    if (!_discussionMappings.ContainsKey("cs")) _discussionMappings["cs"] = new Dictionary<string, int>();
                    _discussionMappings["csharp"][customId.ToString()] = _currentCustomDiscussionNumber;
                    _discussionMappings["C#"][customId.ToString()] = _currentCustomDiscussionNumber;
                    _discussionMappings["cs"][customId.ToString()] = _currentCustomDiscussionNumber;
                }
            }
            else
            {
                _currentCustomDiscussionNumber = -1;
            }

            _isCustomLevelMode = true;
            _nextCustomLevelPath = null;

            LoadLevel(loadedLevel);
            AddToConsole($"\n> Custom Level geladen: {GetCleanLevelName(loadedLevel.Title)}", Brushes.LightGreen);
        }
    }

    private void LoadSqlLevel(SqlLevel level)
    {
        SaveCurrentProgress();

        // reset custom variables if its a standard level
        if (level.Id > 0)
        {
            _isCustomLevelMode = false;
            _currentCustomAuthor = "";
            _nextCustomLevelPath = null;
            _currentCustomDiscussionNodeId = null;
            _currentCustomDiscussionNumber = -1;
        }

        // check if leaving level 4 unresolved (completes the mission to not annoy user)
        if (currentSqlLevel?.Id == 4 && !playerData.Settings.SqlSpoilerHintDismissed)
        {
            playerData.Settings.SqlSpoilerHintDismissed = true;
            SaveSystem.Save(playerData);
        }

        UpdateFocusedColumn(null, null);

        _consecutiveSqlFails = 0;

        currentSqlLevel = level;
        UpdateNavigationButtons();

        BtnCustomLevelReturn.IsVisible = _isCustomLevelMode && !_isDesignerMode;

        // clear autocomplete and pre-reset caret to prevent ghost text layout paradox
        System.Diagnostics.Debug.WriteLine($"[LoadSqlLevel] before ClearSuggestion: hasSuggestion={_sqlAutocompleteService?.HasSuggestion}");
        _sqlAutocompleteService?.ClearSuggestion();
        System.Diagnostics.Debug.WriteLine($"[LoadSqlLevel] after ClearSuggestion: hasSuggestion={_sqlAutocompleteService?.HasSuggestion}");
        SqlQueryEditor.CaretOffset = 0;

        // properly load custom or standard sql editor text
        if (_isCustomLevelMode)
        {
            if (level.Title != null && customPlayerData.UserSqlCode.ContainsKey(level.Title))
                SqlQueryEditor.Text = customPlayerData.UserSqlCode[level.Title];
            else
                SqlQueryEditor.Text = "";
        }
        else
        {
            if (playerData.UserSqlCode.ContainsKey(level.Id))
                SqlQueryEditor.Text = playerData.UserSqlCode[level.Id];
            else
                SqlQueryEditor.Text = "";
        }

        SqlQueryEditor.CaretOffset = 0; // reset caret
        SqlQueryEditor.TextArea.Caret.Line = 1;
        SqlQueryEditor.TextArea.Caret.Column = 1;

        // reset vim state and clear floating carets
        SqlQueryEditor.TextArea.ClearSelection();
        _vimMode = VimMode.Normal;
        _vimDesiredColumn = -1;

        // fresh renderer instance to flush any stale visual-line state from ghost text
        System.Diagnostics.Debug.WriteLine($"[LoadSqlLevel] recreating VimBlockCaretRenderer");
        SqlQueryEditor.TextArea.TextView.BackgroundRenderers.Remove(_sqlBlockCaret);
        _sqlBlockCaret = new VimBlockCaretRenderer(SqlQueryEditor);
        SqlQueryEditor.TextArea.TextView.BackgroundRenderers.Add(_sqlBlockCaret);

        UpdateVimUI();
        SqlQueryEditor.TextArea.TextView.InvalidateVisual();

        PnlSqlOutput.Children.Clear();
        PnlTask.Children.Clear();

        if (_isCustomLevelMode)
        {
            PnlTask.Children.Add(new SelectableTextBlock
            {
                Text = GetCleanLevelName(level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled)),
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Scheme.BrushTextNormal,
                Margin = new Thickness(0)
            });

            if (!string.IsNullOrEmpty(_currentCustomAuthor))
            {
                PnlTask.Children.Add(new SelectableTextBlock
                {
                    Text = $"von {_currentCustomAuthor}",
                    FontSize = 14,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 0, 0, 20)
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
                Text = $"S{level.Id}. {GetCleanLevelName(level.GetDisplayTitle(AppSettings.IsSqlAntiSpoilerEnabled))}",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = Scheme.BrushTextNormal,
                Margin = new Thickness(0, 0, 0, 15)
            });
        }

        PnlTaskRelationalModel.Children.Clear();
        PnlUmlRelationalModel.Children.Clear();

        _currentRelationalModel?.Clear();
        if (!level.IsRelationalModelReadOnly)
        {
            if (_isCustomLevelMode && level.Title != null && customPlayerData.UserSqlModels.ContainsKey(level.Title))
                try
                {
                    _currentRelationalModel =
                        JsonSerializer.Deserialize<List<RTable>>(customPlayerData.UserSqlModels[level.Title]) ??
                        new List<RTable>();
                }
                catch
                {
                }
            else if (!_isCustomLevelMode && playerData.UserSqlModels.ContainsKey(level.Id))
                try
                {
                    _currentRelationalModel =
                        JsonSerializer.Deserialize<List<RTable>>(playerData.UserSqlModels[level.Id]) ??
                        new List<RTable>();
                }
                catch
                {
                }

            if (_currentRelationalModel?.Count == 0 && level.InitialRelationalModel != null &&
                level.InitialRelationalModel.Count > 0)
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
        else if (MainTabs.SelectedIndex == 1)
            RenderRelationalModel(PnlUmlRelationalModel, level.IsRelationalModelReadOnly);

        WrapPanel? tagsPanel = BuildTagsPanel(level.Difficulty, level.Topics, level.DiagramTags, true, _isCustomLevelMode && _currentCustomDiscussionNumber != -1);
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
            AddSqlOutput("System", "Level geladen.\nDatenbank zurückgesetzt.", Brushes.Gray);
        else
            AddSqlOutput("System", $"Level S{level.Id} (Code: {level.SkipCode}) geladen.\nDatenbank zurückgesetzt.",
                Brushes.Gray);

        HideSpoilerHint();
        _spoilerDelayMet = false;
        _spoilerDelayTimer.Stop();

        if (!AppSettings.IsSqlAntiSpoilerEnabled && !playerData.Settings.SqlSpoilerHintDismissed)
            if (level.Id == 3 || level.Id == 4)
                _spoilerDelayTimer.Start();

        DiscordRpcManager.ResetTimer();
        if (_isCustomLevelMode)
            DiscordRpcManager.UpdatePresence("SQL Custom Level", "Solving a custom level", "aec_app_icon", "Custom");
        else
            DiscordRpcManager.UpdatePresence($"SQL Level {level.Id}", "Querying greatness", "mysql_icon", "MySQL");

        _ = UpdateCommunityUIAsync(level.Id.ToString(), true);

        // ensure editor scrolls to top
        Dispatcher.UIThread.Post(() => {
            SqlQueryEditor.ScrollTo(1, 1);
        });
    }

    private void BtnCustomLevelReturn_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentProgress();

        if (_isSqlMode && sqlLevels != null)
        {
            // return to the highest unlocked unsolved sql level (or highest overall)
            var unsolvedSqlLevels = sqlLevels.Where(l => playerData.UnlockedSqlLevelIds.Contains(l.Id) && !playerData.CompletedSqlLevelIds.Contains(l.Id)).ToList();
            var startLevel = unsolvedSqlLevels.Any()
                ? unsolvedSqlLevels.OrderByDescending(l => l.Id).First()
                : sqlLevels.FirstOrDefault(l => l.Id == (playerData.UnlockedSqlLevelIds.Count > 0 ? playerData.UnlockedSqlLevelIds.Max() : 1)) ?? sqlLevels[0];

            LoadSqlLevel(startLevel);
        }
        else if (levels != null)
        {
            // return to the highest unlocked unsolved c# level (or highest overall)
            var unsolvedLevels = levels.Where(l => playerData.UnlockedLevelIds.Contains(l.Id) && !playerData.CompletedLevelIds.Contains(l.Id)).ToList();
            var startLevel = unsolvedLevels.Any()
                ? unsolvedLevels.OrderByDescending(l => l.Id).First()
                : levels.FirstOrDefault(l => l.Id == (playerData.UnlockedLevelIds.Count > 0 ? playerData.UnlockedLevelIds.Max() : 1)) ?? levels[0];

            LoadLevel(startLevel);
        }
    }

    private string? GetCleanLevelName(string? rawName)
    {
        if (string.IsNullOrEmpty(rawName)) return rawName;
        // removes the appended, scrambled discussionId from display name safely
        return Regex.Replace(rawName, @" - [A-Za-z0-9\-_]{15,}$", "");
    }
}