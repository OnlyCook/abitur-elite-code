using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AbiturEliteCode.windows;

public sealed record ApiQueueDialogConfig
{
    public required string SubtitleText { get; init; }
    public required string CancelButtonText { get; init; }
    public string? DestructiveButtonText { get; init; }
    public required Func<List<string>> GetSnapshot { get; init; }
    public required Func<DateTime> GetNextAvailableApiTime { get; init; }
    public required Func<int> GetInFlightCount { get; init; }
    public string MonospaceFontFamily { get; init; } = "Consolas, monospace";
}

public static class ApiQueueDialog
{
    public static async Task<bool> ShowAsync(Window owner, ApiQueueDialogConfig cfg)
    {
        bool destructiveChosen = false;

        var dialog = new Window
        {
            Title = "GitHub Sync im Hintergrund",
            Width = 500,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushBgPanel,
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (_, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto"),
            Margin = new Thickness(20)
        };

        var headerStack = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(0, 0, 0, 15)
        };
        headerStack.Children.Add(new TextBlock
        {
            Text = "Synchronisiere mit GitHub...",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Scheme.BrushTextHighlight
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = cfg.SubtitleText,
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        });
        rootGrid.Children.Add(headerStack);

        var queueListPanel = new StackPanel { Spacing = 8 };
        var scrollBorder = new Border
        {
            Child = new ScrollViewer
            {
                Content = queueListPanel,
                Padding = new Thickness(10)
            },
            Background = Scheme.BrushBgPanel3,
            CornerRadius = new CornerRadius(6),
            BorderBrush = Scheme.BrushBgPanel5,
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        Grid.SetRow(scrollBorder, 1);
        rootGrid.Children.Add(scrollBorder);

        var txtTotalTime = new TextBlock
        {
            Foreground = Brushes.Orange,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var btnPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var btnCancel = new Button
        {
            Content = cfg.CancelButtonText,
            Background = Scheme.BrushBgPanel2,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        btnCancel.Click += (_, _) => dialog.Close();
        btnPanel.Children.Add(btnCancel);

        if (cfg.DestructiveButtonText is not null)
        {
            var btnDestructive = new Button
            {
                Content = cfg.DestructiveButtonText,
                Background = Scheme.BrushDiffHard,
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            btnDestructive.Click += (_, _) =>
            {
                destructiveChosen = true;
                dialog.Close();
            };
            btnPanel.Children.Add(btnDestructive);
        }

        var footerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *"),
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(footerGrid, 2);
        footerGrid.Children.Add(txtTotalTime);
        Grid.SetColumn(btnPanel, 1);
        footerGrid.Children.Add(btnPanel);
        rootGrid.Children.Add(footerGrid);

        dialog.Content = rootGrid;

        List<string> snapDescriptions = [];
        double snapFirstCooldown = 0;
        DateTime snapTakenAt = DateTime.Now;
        double totalTimeAtSnapshot = 0;
        DateTime totalTimeSnapTakenAt = DateTime.Now;

        void TakeSnapshot()
        {
            var queue = cfg.GetSnapshot();
            snapDescriptions = queue;
            snapFirstCooldown = Math.Max(0, (cfg.GetNextAvailableApiTime() - DateTime.Now).TotalSeconds);
            snapTakenAt = DateTime.Now;

            totalTimeAtSnapshot = snapFirstCooldown + Math.Max(0, queue.Count - 1) * 5.0;
            totalTimeSnapTakenAt = DateTime.Now;
        }

        var syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        syncTimer.Tick += (_, _) =>
        {
            if (cfg.GetSnapshot().Count == 0 && cfg.GetInFlightCount() == 0)
            {
                syncTimer.Stop();
                destructiveChosen = true; // queue finished naturally -> treat as "proceed"
                dialog.Close();
                return;
            }
            TakeSnapshot();
        };

        var smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        smoothTimer.Tick += (_, _) =>
        {
            if (snapDescriptions.Count == 0) return;

            double elapsed = (DateTime.Now - snapTakenAt).TotalSeconds;
            queueListPanel.Children.Clear();

            for (int i = 0; i < snapDescriptions.Count; i++)
            {
                double baseCooldown = i == 0 ? snapFirstCooldown : 5.0;
                double displayed = Math.Max(0, baseCooldown - (i == 0 ? elapsed : 0));

                var row = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 10
                };
                row.Children.Add(new TextBlock
                {
                    Text = $"{displayed:F1}s",
                    Foreground = displayed < 1.5 ? Brushes.OrangeRed : Brushes.Gray,
                    Width = 45,
                    FontFamily = new FontFamily(cfg.MonospaceFontFamily)
                });
                row.Children.Add(new TextBlock
                {
                    Text = $"– {snapDescriptions[i]}",
                    Foreground = Brushes.White
                });
                queueListPanel.Children.Add(row);
            }
        };

        var totalTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        totalTimeTimer.Tick += (_, _) =>
        {
            double remaining = Math.Max(0, totalTimeAtSnapshot - (DateTime.Now - totalTimeSnapTakenAt).TotalSeconds);
            txtTotalTime.Text = $"Gesamte Restzeit: ~{Math.Ceiling(remaining)}s";
        };

        dialog.Closed += (_, _) =>
        {
            syncTimer.Stop();
            smoothTimer.Stop();
            totalTimeTimer.Stop();
        };

        TakeSnapshot();
        syncTimer.Start();
        smoothTimer.Start();
        totalTimeTimer.Start();

        await dialog.ShowDialog(owner);
        return destructiveChosen;
    }
}
