using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

public class PlayerSettings
{
    // --- Editor ---
    [SettingKey("vim")] public bool IsVimEnabled { get; set; }
    [SettingKey("sqlvim")] public bool IsSqlVimEnabled { get; set; }
    [SettingKey("syntax")] public bool IsSyntaxHighlightingEnabled { get; set; }
    [SettingKey("sqlsyntax")] public bool IsSqlSyntaxHighlightingEnabled { get; set; }
    [SettingKey("autocomplete")] public bool IsAutocompleteEnabled { get; set; }
    [SettingKey("sqlautocomplete")] public bool IsSqlAutocompleteEnabled { get; set; }
    private double _editorFontSize = 16.0;
    [SettingKey("fontsize")]
    public double EditorFontSize
    {
        get => _editorFontSize;
        set => _editorFontSize = Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;
    }

    private double _sqlEditorFontSize = 16.0;
    [SettingKey("sqlfontsize")]
    public double SqlEditorFontSize
    {
        get => _sqlEditorFontSize;
        set => _sqlEditorFontSize = Math.Round(value * 2.0, MidpointRounding.AwayFromZero) / 2.0;
    }
    [SettingKey("wordwrap")] public bool IsWordWrapEnabled { get; set; }
    [SettingKey("sqlwordwrap")] public bool IsSqlWordWrapEnabled { get; set; }

    // --- Darstellung ---
    [SettingKey("scale")] public double UiScale { get; set; } = 1.0;
    [SettingKey("autosavelayout")] public bool IsLayoutAutoSaveEnabled { get; set; } = false;
    [SettingKey("savedlayout")] public string SavedAppLayout { get; set; } = "";

    // --- Updates ---
    [SettingKey("autoupdate")] public bool AutoCheckForUpdates { get; set; } = true;

    // --- Sonstiges ---
    [SettingKey("sqlantispoiler")] public bool IsSqlAntiSpoilerEnabled { get; set; }
    [SettingKey("discordrpc")] public bool IsDiscordRpcEnabled { get; set; }
    [SettingKey("community")] public bool IsCommunityFeaturesEnabled { get; set; } = false;
    public string GithubToken { get; set; } = "";
    [SettingKey("githubusername")] public string GithubUsername { get; set; } = "";
    [SettingKey("installkey")] public string InstallKey { get; set; } = string.Empty;

    // internal game state (not settings)
    [SettingKey("tabtips")] public int TabTipShownCount { get; set; }
    [SettingKey("vimscore")] public int VimTutorialHighscore { get; set; }
    [SettingKey("lastscreenwidth")] public double LastScreenWidth { get; set; }
    [SettingKey("lastscreenheight")] public double LastScreenHeight { get; set; }
    [SettingKey("sqlspoilerdismissed")] public bool SqlSpoilerHintDismissed { get; set; }
    [SettingKey("sqlspoilertime")] public double SqlSpoilerHintTotalSeconds { get; set; }
    [SettingKey("relationaltip")] public bool RelationalModelTipShown { get; set; }
    [SettingKey("communityhint")] public bool CommunityHintShown { get; set; }
    [SettingKey("formspreecd")] public double LastFormspreeTime { get; set; }
    [SettingKey("notispaused")] public bool AreNotificationsPaused { get; set; }
}

public class PlayerData
{
    public List<int> UnlockedLevelIds { get; set; } = new() { 1 };
    public List<int> CompletedLevelIds { get; set; } = new();

    public List<int> UnlockedSqlLevelIds { get; set; } = new() { 1 };
    public List<int> CompletedSqlLevelIds { get; set; } = new();

    public Dictionary<int, string> UserSqlCode { get; set; } = new();
    public Dictionary<int, string> UserSqlModels { get; set; } = new();
    public Dictionary<int, string> UserCode { get; set; } = new();
    public PlayerSettings Settings { get; set; } = new();
}

public class CustomPlayerData
{
    // c#
    public HashSet<string> CompletedCustomLevels { get; set; } = new();
    public Dictionary<string, string> UserCode { get; set; } = new();

    // sql
    public HashSet<string> CompletedCustomSqlLevels { get; set; } = new();
    public Dictionary<string, string> UserSqlCode { get; set; } = new();
    public Dictionary<string, string> UserSqlModels { get; set; } = new();
}

public static class SaveSystem
{
    private static readonly string appDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AbiturEliteCode");

    private static readonly string rootFolder = AppContext.BaseDirectory; // portable location

    private static string AppDataPath => Path.Combine(appDataFolder, "savegame.elitedata");
    private static string RootPath => Path.Combine(rootFolder, "savegame.elitedata");

    private static string CustomSavePath => Path.Combine(IsPortableModeEnabled() ? rootFolder : appDataFolder, "customsave.elitedata");

    private static string CommunityCachePath => Path.Combine(IsPortableModeEnabled() ? rootFolder : appDataFolder, "communitycache.elitedata");

    private static string TokenPath => Path.Combine(IsPortableModeEnabled() ? rootFolder : appDataFolder, "token(NICHT TEILEN).elitedata");
    private static string DpapiKeyPath => Path.Combine(IsPortableModeEnabled() ? rootFolder : appDataFolder, "installkey.elitedata");

    private static string GetActivePath()
    {
        // first check for existing local save
        if (File.Exists(RootPath)) return RootPath;

        // then existing appdata save
        if (File.Exists(AppDataPath)) return AppDataPath;

        // if no save file exists, determine default behavior
        bool shouldBePortable = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // check if running from usb stick
                string driveLetter = Path.GetPathRoot(rootFolder);
                var driveInfo = new DriveInfo(driveLetter);
                if (driveInfo.DriveType == DriveType.Removable)
                    shouldBePortable = true;

                // check if domain joined (highly likely a school/managed computer)
                if (Environment.UserDomainName != Environment.MachineName)
                    shouldBePortable = true;
            }
            catch { }
        }

        // test appdata write access (catches locked down school pcs)
        bool canWriteAppData = false;
        try
        {
            if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);
            string testFile = Path.Combine(appDataFolder, ".permtest");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            canWriteAppData = true;
        }
        catch { }

        if (!canWriteAppData) shouldBePortable = true;

        if (shouldBePortable && CanWriteToRoot()) return RootPath;

        return AppDataPath; // fallback appdata
    }

    public static bool IsPortableModeEnabled()
    {
        return GetActivePath() == RootPath;
    }

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
                File.Copy(AppDataPath, RootPath, true);
            else
                Save(new PlayerData(), RootPath);
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

    public static string ExportSaveString()
    {
        var dict = new Dictionary<string, string>();
        string dir = Path.GetDirectoryName(GetActivePath()) ?? string.Empty;

        // use short keys to optimize compression length
        string sPath = Path.Combine(dir, "savegame.elitedata");
        if (File.Exists(sPath)) dict["s"] = File.ReadAllText(sPath);

        string cPath = Path.Combine(dir, "customsave.elitedata");
        if (File.Exists(cPath)) dict["c"] = File.ReadAllText(cPath);

        string ccPath = Path.Combine(dir, "communitycache.elitedata");
        if (File.Exists(ccPath)) dict["cc"] = File.ReadAllText(ccPath);

        string json = System.Text.Json.JsonSerializer.Serialize(dict);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        using var ms = new MemoryStream();
        using (var bs = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionLevel.Optimal))
        {
            bs.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public static bool ImportSaveString(string base64)
    {
        try
        {
            byte[] comp = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(comp);
            using var bs = new System.IO.Compression.BrotliStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var msOut = new MemoryStream();
            bs.CopyTo(msOut);

            string json = Encoding.UTF8.GetString(msOut.ToArray());
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict == null) return false;

            BackupCurrentSave();

            string dir = Path.GetDirectoryName(GetActivePath()) ?? string.Empty;

            if (dict.TryGetValue("s", out var sData)) File.WriteAllText(Path.Combine(dir, "savegame.elitedata"), sData);
            if (dict.TryGetValue("c", out var cData)) File.WriteAllText(Path.Combine(dir, "customsave.elitedata"), cData);
            if (dict.TryGetValue("cc", out var ccData)) File.WriteAllText(Path.Combine(dir, "communitycache.elitedata"), ccData);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void BackupCurrentSave()
    {
        string dir = Path.GetDirectoryName(GetActivePath()) ?? string.Empty;
        var files = new[] { "savegame.elitedata", "customsave.elitedata", "communitycache.elitedata" };

        foreach (var f in files)
        {
            string p = Path.Combine(dir, f);
            string bp = Path.Combine(dir, "backup_" + f);
            if (File.Exists(p)) File.Copy(p, bp, true);
            else if (File.Exists(bp)) File.Delete(bp);
        }
    }

    public static void RevertSave()
    {
        string dir = Path.GetDirectoryName(GetActivePath()) ?? string.Empty;
        var files = new[] 
        {
            "savegame.elitedata",
            "customsave.elitedata",
            "communitycache.elitedata"
        };

        foreach (var f in files)
        {
            string p = Path.Combine(dir, f);
            string bp = Path.Combine(dir, "backup_" + f);

            if (File.Exists(bp))
            {
                File.Copy(bp, p, true);
            }
            else if (File.Exists(p))
            {
                // delete file if it didnt exist in the backup
                File.Delete(p);
            }
        }
    }

    public static bool HasBackup()
    {
        string dir = Path.GetDirectoryName(GetActivePath()) ?? string.Empty;
        return File.Exists(Path.Combine(dir, "backup_savegame.elitedata")) ||
               File.Exists(Path.Combine(dir, "backup_customsave.elitedata")) ||
               File.Exists(Path.Combine(dir, "backup_communitycache.elitedata"));
    }

    public static bool HasActiveSave()
    {
        string dir = Path.GetDirectoryName(GetActivePath()) ?? string.Empty;
        return File.Exists(Path.Combine(dir, "savegame.elitedata")) ||
               File.Exists(Path.Combine(dir, "customsave.elitedata")) ||
               File.Exists(Path.Combine(dir, "communitycache.elitedata"));
    }

    private static string SerializeSettings(PlayerSettings s)
    {
        var parts = new List<string>();
        foreach (var prop in typeof(PlayerSettings).GetProperties())
        {
            if (prop.Name == nameof(PlayerSettings.GithubToken)) continue;

            var attr = prop.GetCustomAttribute<SettingKeyAttribute>();
            if (attr == null) continue;

            if (prop.PropertyType == typeof(string))
            {
                // encode strings to base64
                string str = (string)prop.GetValue(s) ?? "";
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
                parts.Add($"{attr.Key}:{encoded}");
            }
            else if (prop.PropertyType == typeof(double))
                parts.Add($"{attr.Key}:{((double)prop.GetValue(s)).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            else
                parts.Add($"{attr.Key}:{prop.GetValue(s)}");
        }
        return string.Join(";", parts);
    }

    private static void DeserializeSettings(PlayerSettings s, string raw)
    {
        var lookup = typeof(PlayerSettings).GetProperties()
            .Select(p => (prop: p, attr: p.GetCustomAttribute<SettingKeyAttribute>()))
            .Where(x => x.attr != null)
            .ToDictionary(x => x.attr.Key, x => x.prop);

        foreach (var part in raw.Split(';'))
        {
            var kv = part.Split(new[] { ':' }, 2);
            if (kv.Length != 2) continue;
            if (!lookup.TryGetValue(kv[0], out var prop)) continue;

            try
            {
                object value;
                if (prop.PropertyType == typeof(bool)) value = bool.Parse(kv[1]);
                else if (prop.PropertyType == typeof(int)) value = int.Parse(kv[1]);
                else if (prop.PropertyType == typeof(double)) value = double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                else if (prop.PropertyType == typeof(string))
                {
                    // decode base64
                    value = Encoding.UTF8.GetString(Convert.FromBase64String(kv[1]));
                }
                else value = kv[1];

                prop.SetValue(s, value);
            }
            catch { }
        }
    }

    public static void Save(PlayerData data, string forcePath = null)
    {
        string targetPath = forcePath ?? GetActivePath();
        string directory = Path.GetDirectoryName(targetPath);

        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string ids = string.Join(",", data.UnlockedLevelIds);
        string completed = string.Join(",", data.CompletedLevelIds);
        string codes = string.Join(";",
            data.UserCode.Select(k => $"{k.Key}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Value))}"));

        string settings = SerializeSettings(data.Settings);

        string sqlUnlocked = string.Join(",", data.UnlockedSqlLevelIds);
        string sqlCompleted = string.Join(",", data.CompletedSqlLevelIds);
        string sqlCodes = string.Join(";",
            data.UserSqlCode.Select(k => $"{k.Key}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Value))}"));
        string sqlModels = string.Join(";",
            data.UserSqlModels.Select(k => $"{k.Key}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Value))}"));

        // format: unlocked|codes|completed|settings|sqlUnlocked|sqlCompleted|sqlCodes|sqlModels
        File.WriteAllText(targetPath,
            $"{ids}|{codes}|{completed}|{settings}|{sqlUnlocked}|{sqlCompleted}|{sqlCodes}|{sqlModels}");
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
                foreach (var item in parts[1].Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    var pair = item.Split(':');
                    if (pair.Length < 2) continue;

                    int id = int.Parse(pair[0]);
                    string code = Encoding.UTF8.GetString(Convert.FromBase64String(pair[1]));
                    if (!data.UserCode.ContainsKey(id)) data.UserCode.Add(id, code);
                }

            if (parts.Length > 2 && !string.IsNullOrEmpty(parts[2]))
                data.CompletedLevelIds = parts[2].Split(',').Select(int.Parse).ToList();

            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
                DeserializeSettings(data.Settings, parts[3]);

            if (parts.Length > 4 && !string.IsNullOrEmpty(parts[4]))
                data.UnlockedSqlLevelIds = parts[4].Split(',').Select(int.Parse).ToList();

            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
                data.CompletedSqlLevelIds = parts[5].Split(',').Select(int.Parse).ToList();

            if (parts.Length > 6 && !string.IsNullOrEmpty(parts[6]))
                foreach (var item in parts[6].Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    var pair = item.Split(':');
                    if (pair.Length < 2) continue;

                    int id = int.Parse(pair[0]);
                    string code = Encoding.UTF8.GetString(Convert.FromBase64String(pair[1]));
                    if (!data.UserSqlCode.ContainsKey(id)) data.UserSqlCode.Add(id, code);
                }

            if (parts.Length > 7 && !string.IsNullOrEmpty(parts[7]))
                foreach (var item in parts[7].Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    var pair = item.Split(':');
                    if (pair.Length < 2) continue;

                    int id = int.Parse(pair[0]);
                    string modelJson = Encoding.UTF8.GetString(Convert.FromBase64String(pair[1]));
                    if (!data.UserSqlModels.ContainsKey(id)) data.UserSqlModels.Add(id, modelJson);
                }
        }
        catch
        {
        }

        return data;
    }

    public static void SaveCustom(CustomPlayerData data)
    {
        string path = CustomSavePath;
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // c# data
        string completed = string.Join("|", data.CompletedCustomLevels);
        var codeEntries = data.UserCode.Select(k =>
            $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Key))}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Value))}");
        string codes = string.Join(";", codeEntries);

        // sql data
        string sqlCompleted = string.Join("|", data.CompletedCustomSqlLevels);
        var sqlCodeEntries = data.UserSqlCode.Select(k =>
            $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Key))}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Value))}");
        string sqlCodes = string.Join(";", sqlCodeEntries);

        var sqlModelEntries = data.UserSqlModels.Select(k =>
            $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Key))}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(k.Value))}");
        string sqlModels = string.Join(";", sqlModelEntries);

        // append sql sections with '§'
        File.WriteAllText(path, $"{completed}§{codes}§{sqlCompleted}§{sqlCodes}§{sqlModels}");
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

                // load completed c# levels
                if (sections.Length > 0 && !string.IsNullOrEmpty(sections[0]))
                {
                    var names = sections[0].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var name in names) data.CompletedCustomLevels.Add(name);
                }

                // load user code c#
                if (sections.Length > 1 && !string.IsNullOrEmpty(sections[1]))
                {
                    var entries = sections[1].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var entry in entries)
                    {
                        var parts = entry.Split(':');
                        if (parts.Length == 2)
                        {
                            string key = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                            string code = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                            if (!data.UserCode.ContainsKey(key)) data.UserCode.Add(key, code);
                        }
                    }
                }

                // load completed sql levels
                if (sections.Length > 2 && !string.IsNullOrEmpty(sections[2]))
                {
                    var names = sections[2].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var name in names) data.CompletedCustomSqlLevels.Add(name);
                }

                // load user code sql
                if (sections.Length > 3 && !string.IsNullOrEmpty(sections[3]))
                {
                    var entries = sections[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var entry in entries)
                    {
                        var parts = entry.Split(':');
                        if (parts.Length == 2)
                        {
                            string key = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                            string code = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                            if (!data.UserSqlCode.ContainsKey(key)) data.UserSqlCode.Add(key, code);
                        }
                    }
                }

                // load user models sql
                if (sections.Length > 4 && !string.IsNullOrEmpty(sections[4]))
                {
                    var entries = sections[4].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var entry in entries)
                    {
                        var parts = entry.Split(':');
                        if (parts.Length == 2)
                        {
                            string key = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                            string modelJson = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                            if (!data.UserSqlModels.ContainsKey(key)) data.UserSqlModels.Add(key, modelJson);
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return data;
    }

    public static void SaveCommunityCache(CommunityCacheData data)
    {
        try
        {
            string json = System.Text.Json.JsonSerializer.Serialize(data);
            File.WriteAllText(CommunityCachePath, json);
        }
        catch { }
    }

    public static CommunityCacheData LoadCommunityCache()
    {
        if (File.Exists(CommunityCachePath))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<CommunityCacheData>(File.ReadAllText(CommunityCachePath)) ?? new CommunityCacheData();
            }
            catch { }
        }
        return new CommunityCacheData();
    }

    public static void ClearCommunityUserState()
    {
        var cache = LoadCommunityCache();

        foreach (var discussion in cache.CsharpDiscussions.Values)
        {
            discussion.ViewerHasLiked = false;
            discussion.ViewerHasDisliked = false;
        }

        foreach (var discussion in cache.SqlDiscussions.Values)
        {
            discussion.ViewerHasLiked = false;
            discussion.ViewerHasDisliked = false;
        }

        SaveCommunityCache(cache);
    }

    private static byte[] DeriveKey(string installKey)
    {
        // derive a 256-bit key from the composite key
        using var sha = System.Security.Cryptography.SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(installKey + GetMachineKey()));
    }

    private static string EncryptToken(string token, string installKey)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        byte[] key = DeriveKey(installKey);
        byte[] nonce = new byte[System.Security.Cryptography.AesGcm.NonceByteSizes.MinSize];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonce);
        byte[] plaintext = Encoding.UTF8.GetBytes(token);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[System.Security.Cryptography.AesGcm.TagByteSizes.MinSize];
        using var aes = new System.Security.Cryptography.AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]); // store nonce + tag + ciphertext
    }

    private static string DecryptToken(string base64, string installKey)
    {
        if (string.IsNullOrEmpty(base64)) return string.Empty;
        try
        {
            byte[] key = DeriveKey(installKey);
            byte[] data = Convert.FromBase64String(base64);
            int nonceSize = System.Security.Cryptography.AesGcm.NonceByteSizes.MinSize;
            int tagSize = System.Security.Cryptography.AesGcm.TagByteSizes.MinSize;
            byte[] nonce = data[..nonceSize];
            byte[] tag = data[nonceSize..(nonceSize + tagSize)];
            byte[] ciphertext = data[(nonceSize + tagSize)..];
            byte[] plaintext = new byte[ciphertext.Length];
            using var aes = new System.Security.Cryptography.AesGcm(key, tagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch { return string.Empty; }
    }

    public static void SaveToken(string token, string installKey)
    {
        try
        {
            string path = TokenPath;
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, EncryptToken(token, installKey));
        }
        catch { }
    }

    public static string LoadToken(string installKey)
    {
        try
        {
            if (!File.Exists(TokenPath)) return string.Empty;
            return DecryptToken(File.ReadAllText(TokenPath), installKey);
        }
        catch { return string.Empty; }
    }

    public static void DeleteToken()
    {
        try { if (File.Exists(TokenPath)) File.Delete(TokenPath); }
        catch { }
    }

    private static string GetMachineKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // try to load the already protected key blob
            if (File.Exists(DpapiKeyPath))
            {
                try
                {
                    byte[] blob = File.ReadAllBytes(DpapiKeyPath);
                    byte[] keyBytes = System.Security.Cryptography.ProtectedData.Unprotect(
                        blob,
                        Encoding.UTF8.GetBytes("AbiturEliteCode"),
                        System.Security.Cryptography.DataProtectionScope.CurrentUser
                    );
                    return Convert.ToBase64String(keyBytes);
                }
                catch { }
            }

            // initial launch: generate a random machine key, protect it with dpapi, save it
            byte[] secret = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(secret);
            byte[] protected_ = System.Security.Cryptography.ProtectedData.Protect(
                secret,
                Encoding.UTF8.GetBytes("AbiturEliteCode"),
                System.Security.Cryptography.DataProtectionScope.CurrentUser
            );
            string dir = Path.GetDirectoryName(DpapiKeyPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(DpapiKeyPath, protected_);
            return Convert.ToBase64String(secret);
        }

        // linux/mac fallback
        return GetPosixMachineKey();
    }

    private static string GetPosixMachineKey()
    {
        // use "/etc/machine-id" (linux) or "IOPlatformUUID" (mac) if available
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ioreg",
                    Arguments = "-rd1 -c IOPlatformExpertDevice",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                string output = proc.StandardOutput.ReadToEnd();
                var match = System.Text.RegularExpressions.Regex.Match(output, @"IOPlatformUUID""\s*=\s*""([^""]+)""");
                if (match.Success) return match.Groups[1].Value;
            }
            catch { }
        }

        try
        {
            if (File.Exists("/etc/machine-id"))
                return File.ReadAllText("/etc/machine-id").Trim();

            if (File.Exists("/var/lib/dbus/machine-id"))
                return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
        }
        catch { }

        // last resort
        return $"{Environment.UserName}@{Environment.MachineName}";
    }
}

public class GithubComment
{
    public string Id { get; set; }
    public string Author { get; set; }
    public string Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Upvotes { get; set; }
    public bool ViewerHasUpvoted { get; set; }
    public List<GithubReply> Replies { get; set; } = new();
}

public class GithubReply
{
    public string Id { get; set; }
    public string Author { get; set; }
    public string Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Upvotes { get; set; }
    public bool ViewerHasUpvoted { get; set; }
}

public class DiscussionCache
{
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    public int TotalComments { get; set; }

    public bool ViewerHasLiked { get; set; }
    public bool ViewerHasDisliked { get; set; }
    public string DiscussionNodeId { get; set; }

    public List<GithubComment> Comments { get; set; } = new();
    public string EndCursor { get; set; }
    public bool HasNextPage { get; set; }
    public DateTime LastFetched { get; set; }
}

public class CommunityCacheData
{
    public Dictionary<string, DiscussionCache> CsharpDiscussions { get; set; } = new();
    public Dictionary<string, DiscussionCache> SqlDiscussions { get; set; } = new();

    public List<AppNotification> Notifications { get; set; } = new();
    public Dictionary<string, int> Subscriptions { get; set; } = new();
}

public class AppNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; }
    public DateTime Date { get; set; }
    public bool IsRead { get; set; }
    public string TargetDiscussionId { get; set; }
    public string TargetCommentId { get; set; }
}