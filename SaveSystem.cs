using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PlayerSettings
{
    public bool IsVimEnabled { get; set; } = false;
    public bool IsSyntaxHighlightingEnabled { get; set; } = false;
    public double UiScale { get; set; } = 1.0;
}

public class PlayerData
{
    public List<int> UnlockedLevelIds { get; set; } = new List<int> { 1 };
    public List<int> CompletedLevelIds { get; set; } = new List<int>();
    public Dictionary<int, string> UserCode { get; set; } = new Dictionary<int, string>();
    public PlayerSettings Settings { get; set; } = new PlayerSettings();
}

public static class SaveSystem
{
    private static string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AbiturEliteCode");
    private static string path = Path.Combine(folder, "savegame.elitedata");

    public static void Save(PlayerData data)
    {
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string ids = string.Join(",", data.UnlockedLevelIds);
        string completed = string.Join(",", data.CompletedLevelIds);
        string codes = string.Join(";", data.UserCode.Select(k => $"{k.Key}:{System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(k.Value))}"));

        // Settings serialization
        string settings = $"vim:{data.Settings.IsVimEnabled};syntax:{data.Settings.IsSyntaxHighlightingEnabled};scale:{data.Settings.UiScale}";

        // format: unlocked|codes|completed|settings
        File.WriteAllText(path, $"{ids}|{codes}|{completed}|{settings}");
    }

    public static PlayerData Load()
    {
        PlayerData data = new PlayerData();
        if (!File.Exists(path)) return data;

        try
        {
            string content = File.ReadAllText(path);
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
                    else if (kv[0] == "syntax") data.Settings.IsSyntaxHighlightingEnabled = bool.Parse(kv[1]);
                    else if (kv[0] == "scale") data.Settings.UiScale = double.Parse(kv[1]);
                }
            }
        }
        catch { }
        return data;
    }
}