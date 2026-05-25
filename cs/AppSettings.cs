using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbiturEliteCode.cs;

public static class AppSettings
{
    // --- Editor ---
    [SettingKey("vim")] public static bool IsVimEnabled { get; set; }
    [SettingKey("sqlvim")] public static bool IsSqlVimEnabled { get; set; }
    [SettingKey("syntax")] public static bool IsSyntaxHighlightingEnabled { get; set; }
    [SettingKey("sqlsyntax")] public static bool IsSqlSyntaxHighlightingEnabled { get; set; }
    [SettingKey("fontsize")]
    public static double EditorFontSize
    {
        get;
        set => field = System.Math.Round(value * 2.0, System.MidpointRounding.AwayFromZero) / 2.0;
    } = 16.0;
    [SettingKey("sqlfontsize")]
    public static double SqlEditorFontSize
    {
        get;
        set => field = System.Math.Round(value * 2.0, System.MidpointRounding.AwayFromZero) / 2.0;
    } = 16.0;
    [SettingKey("autocomplete")] public static bool IsAutocompleteEnabled { get; set; }
    [SettingKey("sqlautocomplete")] public static bool IsSqlAutocompleteEnabled { get; set; }
    [SettingKey("wordwrap")] public static bool IsWordWrapEnabled { get; set; }
    [SettingKey("sqlwordwrap")] public static bool IsSqlWordWrapEnabled { get; set; }

    public static bool IsErrorHighlightingEnabled { get; set; }
    public static bool IsErrorExplanationEnabled { get; set; }

    // --- Darstellung ---
    [SettingKey("scale")] public static double UiScale { get; set; } = 1.0;
    [SettingKey("autosavelayout")] public static bool IsLayoutAutoSaveEnabled { get; set; } = false;
    [SettingKey("savedlayout")] public static string SavedAppLayout { get; set; } = "";

    // --- Updates ---
    [SettingKey("autoupdate")] public static bool AutoCheckForUpdates { get; set; } = true;

    // --- Sonstiges ---
    [SettingKey("sqlantispoiler")] public static bool IsSqlAntiSpoilerEnabled { get; set; }
    [SettingKey("discordrpc")] public static bool IsDiscordRpcEnabled { get; set; }
    [SettingKey("community")] public static bool IsCommunityFeaturesEnabled { get; set; } = false;
    public static string GithubToken { get; set; } = string.Empty;
    [SettingKey("githubusername")] public static string GithubUsername { get; set; } = string.Empty;
    [SettingKey("installkey")] public static string InstallKey { get; set; } = string.Empty;

    // ---

    public static Dictionary<string, object> TakeSnapshot()
    {
        var snap = new Dictionary<string, object>();
        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Static))
            snap[prop.Name] = prop.GetValue(null)!;
        return snap;
    }

    public static bool HasChangedFrom(Dictionary<string, object> snapshot)
    {
        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (!snapshot.TryGetValue(prop.Name, out var original)) continue;
            if (!Equals(original, prop.GetValue(null))) return true;
        }
        return false;
    }

    public static void RestoreSnapshot(Dictionary<string, object> snapshot)
    {
        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Static))
            if (snapshot.TryGetValue(prop.Name, out var value))
                prop.SetValue(null, value);
    }

    public static void LoadFrom(PlayerSettings source)
    {
        var sourceType = typeof(PlayerSettings);
        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var match = sourceType.GetProperty(prop.Name);
            if (match != null) prop.SetValue(null, match.GetValue(source));
        }
    }

    public static void ApplyTo(PlayerSettings target)
    {
        var targetType = typeof(PlayerSettings);
        foreach (var prop in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            var match = targetType.GetProperty(prop.Name);
            if (match != null && match.CanWrite) match.SetValue(target, prop.GetValue(null));
        }
    }
}