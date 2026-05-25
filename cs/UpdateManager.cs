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

    public const string CurrentVersion = "1.0.0";
    private const string GithubApiUrl = "https://api.github.com/repos/OnlyCook/abitur-elite-code/releases";

    public static List<(string Version, string Body)>? CachedReleases { get; set; } = null;

    public static bool HasCheckedForUpdates { get; private set; } = false;
    public static bool IsOutdated { get; private set; } = false;
    public static bool IsMaintenanceMode { get; private set; } = false;

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

            int currentPid = Process.GetCurrentProcess().Id;
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ??
                                Path.Combine(currentAppDir, "AbiturEliteCode.exe");

            // build visible background script
            string batPath = Path.Combine(tempDir, "update.bat");
            string batContent = $@"
@echo off
title Abitur Elite Code Updater
color 0A
echo ===================================================
echo     Abitur Elite Code wird aktualisiert...
echo ===================================================
echo.
echo [1/3] Warte darauf, dass die App geschlossen wird...
:loop
tasklist /FI ""PID eq {currentPid}"" | find /i ""{currentPid}"" >nul
if not errorlevel 1 (
    timeout /t 1 >nul
    goto loop
)

echo [2/3] Installiere neue Dateien (Speicherstaende sind sicher)...
xcopy ""{sourceFolder}\*"" ""{currentAppDir}"" /E /Y /C /H >nul

echo [3/3] Raeume temporaere Dateien auf und starte neu...
start """" ""{currentExe}""
rmdir /S /Q ""{tempDir}""
del ""%~f0""
";
            File.WriteAllText(batPath, batContent);

            // start script visibly
            var psi = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };
            Process.Start(psi);

            // self destruct
            Environment.Exit(0);
            return UpdateStatus.Success;
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