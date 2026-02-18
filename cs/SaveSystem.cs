using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PlayerSettings
{
    public bool IsVimEnabled { get; set; } = false;
    public bool IsSqlVimEnabled { get; set; } = false;
    public bool IsSyntaxHighlightingEnabled { get; set; } = false;
    public bool IsSqlSyntaxHighlightingEnabled { get; set; } = false;
    public bool IsAutocompleteEnabled { get; set; } = false;
    public bool IsSqlAutocompleteEnabled { get; set; } = false;
    public double UiScale { get; set; } = 1.0;
    public int TabTipShownCount { get; set; } = 0;
}

public class PlayerData
{
    public List<int> UnlockedLevelIds { get; set; } = new List<int> { 1 };
    public List<int> CompletedLevelIds { get; set; } = new List<int>();

    public List<int> UnlockedSqlLevelIds { get; set; } = new List<int> { 1 };
    public List<int> CompletedSqlLevelIds { get; set; } = new List<int>();

    public Dictionary<int, string> UserSqlCode { get; set; } = new Dictionary<int, string>();
    public Dictionary<int, string> UserCode { get; set; } = new Dictionary<int, string>();
    public PlayerSettings Settings { get; set; } = new PlayerSettings();
}

public class CustomPlayerData
{
    public HashSet<string> CompletedCustomLevels { get; set; } = new HashSet<string>();
    public Dictionary<string, string> UserCode { get; set; } = new Dictionary<string, string>();
}

public static class SaveSystem
{
    private static string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AbiturEliteCode");
    private static string rootFolder = AppContext.BaseDirectory; // portable location

    private static string AppDataPath => Path.Combine(appDataFolder, "savegame.elitedata");
    private static string RootPath => Path.Combine(rootFolder, "savegame.elitedata");

    private static string CustomSavePath => Path.Combine(IsPortableModeEnabled() ? rootFolder : appDataFolder, "customsave.elitedata");

    private static string GetActivePath()
    {
        // first check for existing local save
        if (File.Exists(RootPath)) return RootPath;

        // then existing appdata save
        if (File.Exists(AppDataPath)) return AppDataPath;

        // if none on both -> prioritize local save
        if (CanWriteToRoot()) return RootPath;

        return AppDataPath; // fallback appdata
    }

    public static bool IsPortableModeEnabled() => File.Exists(RootPath);

    public static bool CanWriteToRoot()
    {
        try
        {
            // permission check
            string testFile = Path.Combine(rootFolder, ".permtest");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetPortableMode(bool enabled)
    {
        if (enabled)
        {
            // switch to portable mode
            if (File.Exists(AppDataPath))
            {
                File.Copy(AppDataPath, RootPath, true);
            }
            else
            {
                Save(new PlayerData(), forcePath: RootPath);
            }
        }
        else
        {
            // switch to appdata
            if (File.Exists(RootPath))
            {
                if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
                File.Copy(RootPath, AppDataPath, true);
                File.Delete(RootPath);
            }
        }
    }

    public static void Save(PlayerData data, string forcePath = null)
    {
        string targetPath = forcePath ?? GetActivePath();
        string directory = Path.GetDirectoryName(targetPath);

        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string ids = string.Join(",", data.UnlockedLevelIds);
        string completed = string.Join(",", data.CompletedLevelIds);
        string codes = string.Join(";", data.UserCode.Select(k => $"{k.Key}:{System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(k.Value))}"));
        string settings = $"vim:{data.Settings.IsVimEnabled};sqlvim:{data.Settings.IsSqlVimEnabled};syntax:{data.Settings.IsSyntaxHighlightingEnabled};sqlsyntax:{data.Settings.IsSqlSyntaxHighlightingEnabled};autocomplete:{data.Settings.IsAutocompleteEnabled};sqlautocomplete:{data.Settings.IsSqlAutocompleteEnabled};scale:{data.Settings.UiScale};tabtips:{data.Settings.TabTipShownCount}";

        string sqlUnlocked = string.Join(",", data.UnlockedSqlLevelIds);
        string sqlCompleted = string.Join(",", data.CompletedSqlLevelIds);
        string sqlCodes = string.Join(";", data.UserSqlCode.Select(k => $"{k.Key}:{System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(k.Value))}"));

        // format: unlocked|codes|completed|settings|sqlUnlocked|sqlCompleted|sqlCodes
        File.WriteAllText(targetPath, $"{ids}|{codes}|{completed}|{settings}|{sqlUnlocked}|{sqlCompleted}|{sqlCodes}");
    }

    public static PlayerData Load()
    {
        string targetPath = GetActivePath();
        PlayerData data = new PlayerData();

        if (!File.Exists(targetPath)) return data;

        try
        {
            string content = File.ReadAllText(targetPath);
            string[] parts = content.Split('|');

            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                data.UnlockedLevelIds = parts[0].Split(',').Select(int.Parse).ToList();

            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                foreach (var item in parts[1].Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    var pair = item.Split(':');
                    if (pair.Length < 2) continue;

                    int id = int.Parse(pair[0]);
                    string code = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(pair[1]));
                    if (!data.UserCode.ContainsKey(id)) data.UserCode.Add(id, code);
                }
            }

            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
            {
                data.CompletedLevelIds = parts[2].Split(',').Select(int.Parse).ToList();
            }

            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
            {
                var settingsParts = parts[3].Split(';');
                foreach (var s in settingsParts)
                {
                    var kv = s.Split(':');
                    if (kv.Length != 2) continue;

                    if (kv[0] == "vim") data.Settings.IsVimEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "sqlvim") data.Settings.IsSqlVimEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "syntax") data.Settings.IsSyntaxHighlightingEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "sqlsyntax") data.Settings.IsSqlSyntaxHighlightingEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "sqlautocomplete") data.Settings.IsSqlAutocompleteEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "autocomplete") data.Settings.IsAutocompleteEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "scale") data.Settings.UiScale = double.Parse(kv[1]);
                    else if (kv[0] == "tabtips") data.Settings.TabTipShownCount = int.Parse(kv[1]);
                }
            }

            if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4]))
                data.UnlockedSqlLevelIds = parts[4].Split(',').Select(int.Parse).ToList();

            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
                data.CompletedSqlLevelIds = parts[5].Split(',').Select(int.Parse).ToList();

            if (parts.Length > 6 && !string.IsNullOrEmpty(parts[6]))
            {
                foreach (var item in parts[6].Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    var pair = item.Split(':');
                    if (pair.Length < 2) continue;

                    int id = int.Parse(pair[0]);
                    string code = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(pair[1]));
                    if (!data.UserSqlCode.ContainsKey(id)) data.UserSqlCode.Add(id, code);
                }
            }
        }
        catch { }
        return data;
    }

    public static void SaveCustom(CustomPlayerData data)
    {
        string path = CustomSavePath;
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string completed = string.Join("|", data.CompletedCustomLevels);

        var codeEntries = data.UserCode.Select(k =>
            $"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(k.Key))}:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(k.Value))}");
        string codes = string.Join(";", codeEntries);

        File.WriteAllText(path, $"{completed}§{codes}");
    }

    public static CustomPlayerData LoadCustom()
    {
        var data = new CustomPlayerData();
        string path = CustomSavePath;

        if (!File.Exists(path)) return data;

        try
        {
            string content = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(content))
            {
                string[] sections = content.Split('§');

                // load completed levels
                if (sections.Length > 0 && !string.IsNullOrEmpty(sections[0]))
                {
                    var names = sections[0].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var name in names) data.CompletedCustomLevels.Add(name);
                }

                // load user code
                if (sections.Length > 1 && !string.IsNullOrEmpty(sections[1]))
                {
                    var entries = sections[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var entry in entries)
                    {
                        var parts = entry.Split(':');
                        if (parts.Length == 2)
                        {
                            string key = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                            string code = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                            if (!data.UserCode.ContainsKey(key)) data.UserCode.Add(key, code);
                        }
                    }
                }
            }
        }
        catch { }
        return data;
    }
}