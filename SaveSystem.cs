using System.IO;
using System.Linq;

public class PlayerData
{
    public List<int> UnlockedLevelIds { get; set; } = new List<int> { 1 };
    public Dictionary<int, string> UserCode { get; set; } = new Dictionary<int, string>();
}

public static class SaveSystem
{
    private static string path = "savegame.elitedata";

    public static void Save(PlayerData data)
    {
        string ids = string.Join(",", data.UnlockedLevelIds);
        string codes = string.Join(";", data.UserCode.Select(k => $"{k.Key}:{System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(k.Value))}"));
        File.WriteAllText(path, $"{ids}|{codes}");
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
                    int id = int.Parse(pair[0]);
                    string code = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(pair[1]));
                    if (!data.UserCode.ContainsKey(id)) data.UserCode.Add(id, code);
                }
            }
        }
        catch { }
        return data;
    }
}