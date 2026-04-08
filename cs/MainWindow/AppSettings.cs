namespace AbiturEliteCode.cs.MainWindow;

public static class AppSettings
{
    public static bool IsVimEnabled { get; set; }
    public static bool IsSqlVimEnabled { get; set; }
    public static bool IsSyntaxHighlightingEnabled { get; set; }
    public static bool IsSqlSyntaxHighlightingEnabled { get; set; }
    public static double EditorFontSize { get; set; } = 16.0;
    public static double SqlEditorFontSize { get; set; } = 16.0;
    public static double UiScale { get; set; } = 1.0;
    public static bool IsAutocompleteEnabled { get; set; }
    public static bool IsSqlAutocompleteEnabled { get; set; }
    public static bool IsErrorHighlightingEnabled { get; set; }
    public static bool IsErrorExplanationEnabled { get; set; }
    public static bool AutoCheckForUpdates { get; set; } = true;
    public static bool IsSqlAntiSpoilerEnabled { get; set; }
    public static bool IsDiscordRpcEnabled { get; set; }
}