using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;

namespace AbiturEliteCode.screens;

public class SettingsWindowContext
{
    // --- status flags ---
    public required bool IsSqlMode { get; init; }
    public required bool IsTutorialMode { get; init; }
    public required bool IsDesignerMode { get; init; }

    // --- update status ---
    public bool UpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string UpdateDownloadUrl { get; set; } = string.Empty;

    // --- user data ---
    public required PlayerData PlayerData { get; init; }
    public int? CurrentLevelId { get; init; }
    public int? CurrentSqlLevelId { get; init; }

    // --- editors ---
    public required TextEditor CodeEditor { get; init; }
    public required TextEditor SqlQueryEditor { get; init; }
    public required TextEditor TutorialEditor { get; init; }

    // --- mainwindow callbacks ---
    public required Func<string, double, Control> LoadIcon { get; init; }
    public required Action ApplyUiScale { get; init; }
    public required Action ApplySyntaxHighlighting { get; init; }
    public required Action ApplySqlSyntaxHighlighting { get; init; }
    public required Action UpdateVimState { get; init; }
    public required Action ClearDiagnostics { get; init; }
    public required Action UpdateDiagnostics { get; init; }
    public required Action<string, IBrush> AddToConsole { get; init; }
    public required Action<MainWindow.VimMode> SetVimMode { get; init; }
    public required Action ShowUpdateBadge { get; init; }
    public required Func<UpdateManager.UpdateStatus, string, Window, Task> ShowManualUpdateDialog { get; init; }

    // --- autocompletion-callbacks ---
    public Action<string>? ScanSqlTokens { get; init; }
    public Action? ClearSqlSuggestion { get; init; }
    public Action<string>? ScanCsharpTokens { get; init; }
    public Action? ClearCsharpSuggestion { get; init; }
}