using AbiturEliteCode.cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public static class UpdateManager
{
    public enum UpdateStatus
    {
        Success,
        UnsupportedOS,
        NoWritePermission,
        NetworkError,
        RateLimitExceeded
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    public const string CurrentVersion = "1.0.0";
    private const string GithubApiUrl = "https://api.github.com/repos/OnlyCook/abitur-elite-code/releases";

    public static List<(string Version, string Body)>? CachedReleases { get; set; } = null;

    public static bool HasCheckedForUpdates { get; private set; } = false;
    public static bool IsOutdated { get; private set; } = false;
    public static bool IsMaintenanceMode { get; private set; } = false;

    public static void ProcessCommandLineArgs(string[] args)
    {
        if (args == null || args.Length == 0) return;

        if (args.Length >= 4 && args[0] == "--apply-update")
        {
            if (int.TryParse(args[1], out int targetPid))
            {
                string targetDir = args[2];
                string sourceDir = args[3];

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        AllocConsole();
                        // explicitly redirect output so a ui app can write to the console
                        var stdOut = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                        Console.SetOut(stdOut);
                        Console.SetError(stdOut);
                    }
                    catch { }
                }

                Console.WriteLine("==================================================");
                Console.WriteLine("      Abitur Elite Code wird aktualisiert...");
                Console.WriteLine("==================================================");
                Console.WriteLine();

                ApplyUpdateAndRestart(targetPid, targetDir, sourceDir);
            }
        }
        else if (args.Length >= 2 && args[0] == "--cleanup-update")
        {
            string tempDir = args[1];
            Task.Run(() =>
            {
                try
                {
                    // wait a bit to ensure the updater process fully released locks
                    System.Threading.Thread.Sleep(3000);
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            });
        }
    }

    private static void ApplyUpdateAndRestart(int targetPid, string targetDir, string sourceDir)
    {
        try
        {
            Console.WriteLine("[1/3] Warte darauf, dass die App geschlossen wird...");
            try
            {
                var oldProcess = Process.GetProcessById(targetPid);
                if (!oldProcess.HasExited)
                {
                    oldProcess.WaitForExit(5000); // wait up to 5 seconds
                }

                // force kill if the process is stubbornly hanging in the background
                if (!oldProcess.HasExited)
                {
                    Console.WriteLine("Schliesse alte Applikation erzwingend...");
                    oldProcess.Kill();
                }
            }
            catch { }

            // give the os a moment to fully release file locks
            System.Threading.Thread.Sleep(1000);

            Console.WriteLine("[2/3] Installiere neue Dateien (Speicherstaende sind sicher)...");

            // safely recreate the directory structure
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(sourceDir, dirPath);
                Directory.CreateDirectory(Path.Combine(targetDir, relPath));
            }

            // safely copy files with retry logic
            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(sourceDir, newPath);
                string destPath = Path.Combine(targetDir, relPath);

                bool copied = false;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        File.Copy(newPath, destPath, true);
                        copied = true;
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(500);
                    }
                }

                if (!copied)
                {
                    Console.WriteLine($"Warnung: '{relPath}' konnte nicht ueberschrieben werden (Gesperrt?).");
                }
            }

            Console.WriteLine("[3/3] Raeume temporaere Dateien auf und starte neu...");
            string targetExe = Path.Combine(targetDir, "AbiturEliteCode.exe");
            if (!File.Exists(targetExe))
            {
                string friendlyName = AppDomain.CurrentDomain.FriendlyName;
                if (!friendlyName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    friendlyName += ".exe";
                targetExe = Path.Combine(targetDir, friendlyName);
            }

            string rootTempDir = sourceDir;
            int idx = rootTempDir.IndexOf("AbiturEliteCodeUpdate", StringComparison.OrdinalIgnoreCase);
            if (idx != -1)
            {
                rootTempDir = rootTempDir.Substring(0, idx + "AbiturEliteCodeUpdate".Length);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = $"--cleanup-update \"{rootTempDir}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("\nEin Fehler ist aufgetreten: " + ex.Message);
            Console.WriteLine("Starte Ziel-Applikation als Fallback in 4 Sekunden...");

            // pause so the user can read the error message in the console
            System.Threading.Thread.Sleep(4000);

            try
            {
                string targetExe = Path.Combine(targetDir, "AbiturEliteCode.exe");
                Process.Start(new ProcessStartInfo { FileName = targetExe, UseShellExecute = true });
            }
            catch { }
        }

        Environment.Exit(0);
    }

    public static async Task<(UpdateStatus Status, bool UpdateAvailable, string LatestVersion, string DownloadUrl)> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AbiturEliteCode-Updater");

            // append token if available (unauthorized = 60/h rate limit; rather cooked for a classroom)
            if (!string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            }

            var response = await client.GetAsync(GithubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                HasCheckedForUpdates = true;

                // check for rate limit
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    if (errorJson.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                    {
                        return (UpdateStatus.RateLimitExceeded, false, "", "");
                    }
                }

                return (UpdateStatus.NetworkError, false, "", "");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // github /releases returns an array; index 0 is the newest release
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                // cache all releases
                CachedReleases = new();
                foreach (var release in root.EnumerateArray())
                {
                    string rTag = release.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                    string rBody = release.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                    CachedReleases.Add((rTag, rBody));
                }

                var latestRelease = root[0];

                if (latestRelease.TryGetProperty("tag_name", out var tagElement))
                {
                    string tag = tagElement.GetString()?.Trim() ?? "";

                    bool currentIsMaintenance = CurrentVersion.EndsWith("m");
                    bool latestIsMaintenance = tag.EndsWith("m");

                    string currentClean = CurrentVersion.Replace("m", "");
                    currentClean = currentClean.Contains('-') ? currentClean[..currentClean.IndexOf('-')] : currentClean;

                    string latestClean = tag.Replace("m", "");
                    latestClean = latestClean.Contains('-') ? latestClean[..latestClean.IndexOf('-')] : latestClean;

                    bool currentIsPreRelease = CurrentVersion.Contains('-');
                    bool latestIsPreRelease = tag.Contains('-');

                    if (Version.TryParse(currentClean, out var current) && Version.TryParse(latestClean, out var latest))
                    {
                        bool isNewer = false;

                        if (latest > current)
                        {
                            isNewer = true;
                        }
                        else if (latest == current)
                        {
                            if (currentIsPreRelease && !latestIsPreRelease) isNewer = true;
                            else if (!currentIsPreRelease && !latestIsPreRelease)
                            {
                                if (!currentIsMaintenance && latestIsMaintenance) isNewer = true;
                            }
                        }

                        if (isNewer)
                        {
                            string targetAsset = "AbiturEliteCode-win.zip";
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                targetAsset = "AbiturEliteCode-linux.zip";
                            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                targetAsset = "AbiturEliteCode-mac.zip";

                            string downloadUrl = "";
                            bool hasZipAsset = false;

                            if (latestRelease.TryGetProperty("assets", out var assetsElement))
                            {
                                foreach (var asset in assetsElement.EnumerateArray())
                                {
                                    if (asset.TryGetProperty("name", out var nameElement) && nameElement.GetString() == targetAsset)
                                    {
                                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                                        hasZipAsset = true;
                                        break;
                                    }
                                }
                            }

                            // flag as maintenance if tag explicitly ends with 'm' or it has no valid zip asset
                            IsMaintenanceMode = latestIsMaintenance || !hasZipAsset;

                            IsOutdated = true;
                            HasCheckedForUpdates = true;
                            return (UpdateStatus.Success, true, tag, downloadUrl);
                        }
                    }
                }
            }

            IsMaintenanceMode = false;
            IsOutdated = false;
            HasCheckedForUpdates = true;
            return (UpdateStatus.Success, false, "", "");
        }
        catch
        {
            HasCheckedForUpdates = true;
            return (UpdateStatus.NetworkError, false, "", "");
        }
    }

    public static async Task<List<(string Version, string Body)>> GetAllReleasesAsync()
    {
        var releases = new List<(string Version, string Body)>();
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AbiturEliteCode-Updater");

            // append token if available
            if (!string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            }

            var response = await client.GetAsync("https://api.github.com/repos/OnlyCook/abitur-elite-code/releases");
            if (!response.IsSuccessStatusCode) return releases;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                string tag = release.GetProperty("tag_name").GetString() ?? "";
                string body = release.GetProperty("body").GetString() ?? "";
                releases.Add((tag, body));
            }
        }
        catch
        {
            // failure (return empty list)
        }

        return releases;
    }

    public static async Task<UpdateStatus> PerformUpdateAsync(string downloadUrl,
        IProgress<(string message, double percentage)> progress)
    {
        if (string.IsNullOrEmpty(downloadUrl)) return UpdateStatus.NetworkError;

        string currentAppDir = AppDomain.CurrentDomain.BaseDirectory;

        // fallback for mac/linux
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return UpdateStatus.UnsupportedOS;

        // fallback if windows lacks write permissions
        if (!HasWritePermission(currentAppDir)) return UpdateStatus.NoWritePermission;

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "AbiturEliteCodeUpdate");
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");

            // download
            progress?.Report(("Lade herunter...", 0));
            await DownloadFileAsync(downloadUrl, zipPath, progress);

            // extract
            progress?.Report(("Entpacke Dateien...", 100));
            string extractPath = Path.Combine(tempDir, "extracted");
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            string sourceFolder = Path.Combine(extractPath, "AbiturEliteCode");
            if (!Directory.Exists(sourceFolder)) sourceFolder = extractPath;

            // ensure no ".elitedata" files exist in the extracted update folder (extra safety)
            var tempSaveFiles = Directory.GetFiles(sourceFolder, "*.elitedata", SearchOption.AllDirectories);
            foreach (var file in tempSaveFiles) File.Delete(file);

            progress?.Report(("Starte Installer...", 100));

            // we clone ourselves to act as the updater to guarantee the update logic exists
            string updaterDir = Path.Combine(tempDir, "updater");
            Directory.CreateDirectory(updaterDir);

            foreach (string dirPath in Directory.GetDirectories(currentAppDir, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(currentAppDir, dirPath);
                Directory.CreateDirectory(Path.Combine(updaterDir, relPath));
            }
            foreach (string filePath in Directory.GetFiles(currentAppDir, "*.*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(currentAppDir, filePath);
                File.Copy(filePath, Path.Combine(updaterDir, relPath), true);
            }

            int currentPid = Process.GetCurrentProcess().Id;
            string currentExeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "AbiturEliteCode.exe");
            string updaterExePath = Path.Combine(updaterDir, currentExeName);

            if (File.Exists(updaterExePath))
            {
                // launch the clone we just created and feed it the arguments it needs
                var psi = new ProcessStartInfo
                {
                    FileName = updaterExePath,
                    Arguments = $"--apply-update {currentPid} \"{currentAppDir.TrimEnd('\\')}\" \"{sourceFolder.TrimEnd('\\')}\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                Process.Start(psi);

                // self destruct the old instance
                Environment.Exit(0);
                return UpdateStatus.Success;
            }
            else
            {
                return UpdateStatus.NetworkError;
            }
        }
        catch (Exception)
        {
            return UpdateStatus.NetworkError;
        }
    }

    private static async Task DownloadFileAsync(string url, string destination,
        IProgress<(string message, double percentage)>? progress)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AbiturEliteCode-Updater");

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var canReportProgress = totalBytes != -1;

        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream =
            new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;
        int lastReportedPercentage = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;

            if (canReportProgress)
            {
                int currentPercentage = (int)((double)totalRead / totalBytes * 100);
                // only update ui if actually changes (stop ui spam)
                if (currentPercentage > lastReportedPercentage)
                {
                    lastReportedPercentage = currentPercentage;
                    progress?.Report(($"Lade herunter... {currentPercentage}%", currentPercentage));
                }
            }
        }
    }

    private static bool HasWritePermission(string directoryPath)
    {
        try
        {
            string testFile = Path.Combine(directoryPath, "update_test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
        }
        catch
        {
        }
    }
}