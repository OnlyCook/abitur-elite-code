using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private static bool _isDownloadingCommunityLevel = false;

    private string _currentCustomDiscussionNodeId = null;
    private int _currentCustomDiscussionNumber = -1;
    private List<CommunityLevelMeta> _communityMetadataCacheCs = new();
    private List<CommunityLevelMeta> _communityMetadataCacheSql = new();
    private HashSet<string> _communitySelectedDifficulties = new();
    private HashSet<string> _communityBlacklistDifficulties = new();
    private HashSet<string> _communitySelectedTags = new();
    private HashSet<string> _communityBlacklistTags = new();
    private static DateTime _lastCommunityFetchTimeCs = DateTime.MinValue;
    private static DateTime _lastCommunityFetchTimeSql = DateTime.MinValue;

    private int _communityVisibleCount = 20;
    private string _communitySortMode = "Beste";

    private List<CommunityLevelMeta> _communityMetadataCache => _isSqlMode ? _communityMetadataCacheSql : _communityMetadataCacheCs;
    private ref DateTime _lastCommunityFetchTime => ref (_isSqlMode ? ref _lastCommunityFetchTimeSql : ref _lastCommunityFetchTimeCs);

    private class CommunityLevelMeta
    {
        public string NodeId { get; set; }
        public int Number { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Difficulty { get; set; }
        public List<string> Tags { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public bool ViewerHasLiked { get; set; }
        public DateTime CreatedAt { get; set; }
        public double Score { get; set; }
        public double Rating { get; set; }
    }

    private async void BtnRefreshCommunityLevels_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateManager.IsOutdated) return;

        if ((DateTime.Now - _lastCommunityFetchTime).TotalSeconds < 60)
        {
            return;
        }

        await FetchCommunityMetadataAsync(this);
    }

    private async Task FetchCommunityMetadataAsync(Window win)
    {
        if (UpdateManager.IsOutdated) return;

        if ((DateTime.Now - _lastCommunityFetchTime).TotalSeconds < 60) return;
        _lastCommunityFetchTime = DateTime.Now;

        var loadingPanel = win.FindControl<StackPanel>("CommunityLoadingPanel");
        loadingPanel.IsVisible = true;
        if (_isSqlMode) _communityMetadataCacheSql.Clear();
        else _communityMetadataCacheCs.Clear();

        string categoryId = _isSqlMode ? "DIC_kwDOSZnz_M4C9Il3" : "DIC_kwDOSZnz_M4C9Il2";

        try
        {
            if (categoryId == null)
            {
                // fetch category id first if not hardcoded
                var catQuery = new
                {
                    query = @"query {
                        repository(owner: ""aec-community-bot"", name: ""aec-community"") {
                            discussionCategories(first: 20) {
                                nodes { id slug }
                            }
                        }
                    }"
                };

                var catContent = new StringContent(JsonSerializer.Serialize(catQuery), System.Text.Encoding.UTF8, "application/json");
                using var catRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
                catRequest.Content = catContent;
                catRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                catRequest.Headers.UserAgent.ParseAdd("AbiturEliteCode");

                var catResponse = await _httpClient.SendAsync(catRequest);
                if (!catResponse.IsSuccessStatusCode) return;

                var respBody = await catResponse.Content.ReadAsStringAsync();
                using var catDoc = JsonDocument.Parse(respBody);

                Debug.WriteLine("[Community] Category IDs: " + respBody);

                if (catDoc.RootElement.TryGetProperty("errors", out var errors))
                {
                    Debug.WriteLine("GraphQL Category Error: " + errors.ToString());
                    return;
                }

                var catNodes = catDoc.RootElement.GetProperty("data").GetProperty("repository").GetProperty("discussionCategories").GetProperty("nodes");
                string targetSlugBase = _isSqlMode ? "sql-nutzer-level" : "c-nutzer-level";

                foreach (var node in catNodes.EnumerateArray())
                {
                    string slug = node.GetProperty("slug").GetString();
                    // safely check for both singular and plural slugs
                    if (slug != null && slug.StartsWith(targetSlugBase, StringComparison.OrdinalIgnoreCase))
                    {
                        categoryId = node.GetProperty("id").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(categoryId))
                {
                    Debug.WriteLine($"Category starting with '{targetSlugBase}' not found.");
                    return;
                }
            }

            // fetch discussions using categoryId (bulk fetch all so we can properly sort)
            string cursor = null;
            bool hasNext = true;

            while (hasNext)
            {
                var queryObj = new
                {
                    query = @"query($catId: ID!, $cursor: String) {
                        repository(owner: ""aec-community-bot"", name: ""aec-community"") {
                            discussions(categoryId: $catId, first: 100, after: $cursor, orderBy: {field: CREATED_AT, direction: DESC}) {
                                pageInfo { hasNextPage endCursor }
                                nodes {
                                    id number title createdAt
                                    author { login }
                                    upvotes: reactions(content: THUMBS_UP) { totalCount viewerHasReacted }
                                    downvotes: reactions(content: THUMBS_DOWN) { totalCount viewerHasReacted }
                                }
                            }
                        }
                    }",
                    variables = new
                    {
                        catId = categoryId,
                        cursor = cursor
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(queryObj), System.Text.Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
                request.Content = content;
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                request.Headers.UserAgent.ParseAdd("AbiturEliteCode");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) break;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                if (doc.RootElement.TryGetProperty("errors", out var queryErrors))
                {
                    Debug.WriteLine("GraphQL Query Error: " + queryErrors.ToString());
                    break;
                }

                // safely navigate the json tree
                if (!doc.RootElement.TryGetProperty("data", out var dataProp) ||
                    !dataProp.TryGetProperty("repository", out var repoProp) ||
                    !repoProp.TryGetProperty("discussions", out var discussionsProp))
                {
                    break;
                }

                foreach (var node in discussionsProp.GetProperty("nodes").EnumerateArray())
                {
                    if (node.GetProperty("author").GetProperty("login").GetString() != "aec-community-bot")
                        continue;

                    string titleRaw = node.GetProperty("title").GetString() ?? "Unbekannt";

                    // parse title: "[v1] Name by Username - Schwierigkeit | Tag1, Tag2, Tag3"
                    var titleMatch = Regex.Match(titleRaw, @"^\[v\d+\]\s*(.+?)\s+by\s+([A-Za-z0-9-]+)\s*-\s*(Einfach|Mittel|Schwer|Abitur)(?:\s*\|\s*(.*))?$");
                    string name = titleRaw;
                    string author = "Unbekannt";
                    string diff = "Einfach";
                    List<string> tags = new();

                    if (titleMatch.Success)
                    {
                        name = titleMatch.Groups[1].Value.Trim();
                        author = titleMatch.Groups[2].Value.Trim();
                        diff = titleMatch.Groups[3].Value.Trim();
                        if (titleMatch.Groups[4].Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[4].Value))
                            tags = titleMatch.Groups[4].Value.Split(',').Select(t => t.Trim()).ToList();
                    }

                    _communityMetadataCache.Add(new CommunityLevelMeta
                    {
                        NodeId = node.GetProperty("id").GetString(),
                        Number = node.GetProperty("number").GetInt32(),
                        Title = name,
                        Author = author,
                        Difficulty = diff,
                        Tags = tags,
                        Upvotes = node.GetProperty("upvotes").GetProperty("totalCount").GetInt32(),
                        Downvotes = node.GetProperty("downvotes").GetProperty("totalCount").GetInt32(),
                        ViewerHasLiked = node.GetProperty("upvotes").GetProperty("viewerHasReacted").GetBoolean(),
                        CreatedAt = node.GetProperty("createdAt").GetDateTime()
                    });
                }

                var pageInfo = discussionsProp.GetProperty("pageInfo");
                hasNext = pageInfo.GetProperty("hasNextPage").GetBoolean();
                cursor = hasNext ? pageInfo.GetProperty("endCursor").GetString() : null;
            }

            // calculate global average rating for bayesian logic
            double globalAvg = 0.5;
            if (_communityMetadataCache.Any(m => m.Upvotes > 0 || m.Downvotes > 0))
            {
                double totalUp = _communityMetadataCache.Sum(m => m.Upvotes);
                double totalVotes = _communityMetadataCache.Sum(m => m.Upvotes + m.Downvotes);
                if (totalVotes > 0) globalAvg = totalUp / totalVotes;
            }

            foreach (var m in _communityMetadataCache)
            {
                // steamdb rating scaled for smaller userbase (reaches high confidence ~50 votes)
                double totalVotes = m.Upvotes + m.Downvotes;
                double average = totalVotes > 0 ? (double)m.Upvotes / totalVotes : 0.5;
                double rating = average - (average - 0.5) * Math.Pow(2, -(totalVotes / 10.0));

                double ageHours = (DateTime.Now - m.CreatedAt.ToLocalTime()).TotalHours;
                double timeDecay = Math.Pow(Math.Max(ageHours, 0) + 2.0, 1.2);

                m.Rating = rating;
                m.Score = (m.Upvotes * rating) / timeDecay;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to fetch community meta: {ex.Message}");
        }
        finally
        {
            loadingPanel.IsVisible = false;
            RenderCommunityBrowser(win);
        }
    }

    private void RenderCommunityBrowser(Window win, string searchQuery = "")
    {
        var commScroll = win.FindControl<ScrollViewer>("CommunityScroll");
        if (!commScroll.IsVisible) return;

        string sortMode = _communitySortMode;

        var filtered = _communityMetadataCache.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
            filtered = filtered.Where(m => m.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) || m.Author.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

        if (_communitySelectedDifficulties.Any())
            filtered = filtered.Where(m => _communitySelectedDifficulties.Contains(m.Difficulty));
        if (_communityBlacklistDifficulties.Any())
            filtered = filtered.Where(m => !_communityBlacklistDifficulties.Contains(m.Difficulty));

        if (_communitySelectedTags.Any())
            filtered = filtered.Where(m => _communitySelectedTags.All(t => m.Tags.Contains(t)));
        if (_communityBlacklistTags.Any())
            filtered = filtered.Where(m => !_communityBlacklistTags.Any(t => m.Tags.Contains(t)));

        if (sortMode == "Beste") filtered = filtered.OrderByDescending(m => m.Score);
        else if (sortMode == "Top") filtered = filtered.OrderByDescending(m => m.Upvotes).ThenByDescending(m => m.CreatedAt);
        else if (sortMode == "Neuste") filtered = filtered.OrderByDescending(m => m.CreatedAt);
        else if (sortMode == "Älteste") filtered = filtered.OrderBy(m => m.CreatedAt);

        var sortedList = filtered.ToList();
        var stack = new StackPanel { Spacing = 8 };

        var localCustomLevels = GetCustomLevels(); // to check if downloaded
        var completedList = _isSqlMode ? customPlayerData.CompletedCustomSqlLevels : customPlayerData.CompletedCustomLevels;
        List<string> likedList = null; //_isSqlMode ? customPlayerData.LikedCustomSqlLevels : customPlayerData.LikedCustomLevels; // placeholder

        foreach (var m in sortedList.Take(_communityVisibleCount))
        {
            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                Margin = new Thickness(0, 0, 0, 5)
            };

            // calculate unique scrambled name to ensure backward and forward compatibility
            string scrambledId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(m.NodeId)).Replace("=", "").Replace("+", "-").Replace("/", "_");
            string uniqueName = $"{m.Title} - {scrambledId}";

            bool isDownloaded = localCustomLevels.Any(cl => (cl.Name == uniqueName || cl.Name == m.Title) && cl.Author == m.Author);
            bool isCompleted = completedList.Contains(uniqueName) || completedList.Contains(m.Title);

            string iconPath = isCompleted ? "assets/icons/ic_check.svg" : (isDownloaded ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_download.svg");
            var iconImage = LoadIcon(iconPath, 16);
            iconImage.Margin = new Thickness(0, 0, 10, 0);
            iconImage.VerticalAlignment = VerticalAlignment.Center;

            var textStack = new StackPanel { Spacing = 2 };
            textStack.Children.Add(new TextBlock
            {
                Text = m.Title,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var subText = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5
            };
            subText.Children.Add(new TextBlock
            {
                Text = "von",
                FontSize = 11,
                Foreground = Brushes.Gray
            });

            bool isOwnLevel = string.Equals(m.Author, AppSettings.GithubUsername, StringComparison.OrdinalIgnoreCase);
            subText.Children.Add(new TextBlock
            {
                Text = m.Author,
                FontSize = 11,
                Foreground = isOwnLevel ? SolidColorBrush.Parse("#6495ED") : Brushes.Gray,
                Margin = new Thickness(-2, 0)
            });

            if (m.Tags.Any())
                subText.Children.Add(new TextBlock
                {
                    Text = "• " + string.Join(", ", m.Tags),
                    FontSize = 11,
                    Foreground = SolidColorBrush.Parse("#32A852")
                });
            textStack.Children.Add(subText);

            var btnContent = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *")
            };
            btnContent.Children.Add(iconImage);
            Grid.SetColumn(textStack, 1);
            btnContent.Children.Add(textStack);

            // map difficulty to a dim color
            string bgColor = m.Difficulty switch
            {
                "Einfach" => "#1a3320",
                "Mittel" => "#33331a",
                "Schwer" => "#331a1a",
                "Abitur" => "#2d1a33",
                _ => "#191919"
            };

            var btnMain = new Button
            {
                Content = btnContent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = SolidColorBrush.Parse(bgColor),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Cursor = Cursor.Parse("Hand")
            };

            btnMain.Click += async (s, e) =>
            {
                if (isDownloaded)
                {
                    // open the level and close the browser if its already downloaded
                    var localLvl = localCustomLevels.FirstOrDefault(cl => (cl.Name == uniqueName || cl.Name == m.Title) && cl.Author == m.Author);
                    if (localLvl != null)
                    {
                        _openedViaCommunityBrowser = true;
                        LoadCustomLevelFromFile(localLvl.FilePath);
                        win.Close();
                    }
                }
                else
                {
                    if (_isDownloadingCommunityLevel) return;
                    _isDownloadingCommunityLevel = true;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var pendingIcon = LoadIcon("assets/icons/ic_pending.svg", 16);
                        pendingIcon.Margin = new Thickness(0, 0, 10, 0);
                        pendingIcon.VerticalAlignment = VerticalAlignment.Center;
                        btnContent.Children[0] = pendingIcon;
                    });

                    bool success = await DownloadAndLoadCommunityLevelAsync(m, win);

                    if (!success)
                    {
                        // show error icon briefly if download failed
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var errorIcon = LoadIcon("assets/icons/ic_error.svg", 16);
                            errorIcon.Margin = new Thickness(0, 0, 10, 0);
                            errorIcon.VerticalAlignment = VerticalAlignment.Center;
                            btnContent.Children[0] = errorIcon;
                        });

                        await Task.Delay(500);
                    }
                    else
                    {
                        // show success icon briefly otherwise
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var successIcon = LoadIcon("assets/icons/ic_success.svg", 16);
                            successIcon.Margin = new Thickness(0, 0, 10, 0);
                            successIcon.VerticalAlignment = VerticalAlignment.Center;
                            btnContent.Children[0] = successIcon;
                        });

                        await Task.Delay(500);
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var finalIconPath = success ? "assets/icons/ic_lock_open.svg" : "assets/icons/ic_download.svg";
                        var finalIcon = LoadIcon(finalIconPath, 16);
                        finalIcon.Margin = new Thickness(0, 0, 10, 0);
                        finalIcon.VerticalAlignment = VerticalAlignment.Center;
                        btnContent.Children[0] = finalIcon;

                        if (success)
                        {
                            isDownloaded = true;
                            localCustomLevels = GetCustomLevels(); // refresh local levels list silently
                        }
                    });

                    _isDownloadingCommunityLevel = false;
                }
            };
            Grid.SetColumnSpan(btnMain, 3);
            rowGrid.Children.Add(btnMain);

            var metricStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 10, 0),
                IsHitTestVisible = false
            };
            if (sortMode == "Neuste" || sortMode == "Älteste")
            {
                metricStack.Children.Add(new TextBlock
                {
                    Text = m.CreatedAt.ToString("dd.MM.yyyy"),
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12
                });
            }
            else
            {
                bool isLiked = m.ViewerHasLiked;
                string likeIcon = sortMode == "Beste" ? "assets/icons/ic_rating.svg" : isLiked ? "assets/icons/ic_like_filled.svg" : "assets/icons/ic_like.svg";
                metricStack.Children.Add(LoadIcon(likeIcon, 14));

                string metricText = sortMode == "Beste" ? (m.Rating * 100).ToString("F0") + "%" : m.Upvotes.ToString();
                metricStack.Children.Add(new TextBlock
                {
                    Text = metricText,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12
                });
            }
            Grid.SetColumn(metricStack, 2);
            rowGrid.Children.Add(metricStack);

            stack.Children.Add(rowGrid);
        }

        if (sortedList.Count > _communityVisibleCount)
        {
            var loadMore = new Button
            {
                Content = "Mehr laden...",
                Background = Brushes.Transparent,
                Foreground = SolidColorBrush.Parse("#6495ED"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            loadMore.Click += (s, e) =>
            {
                _communityVisibleCount += 20;
                RenderCommunityBrowser(win, searchQuery);
            };
            stack.Children.Add(loadMore);
        }

        if (!sortedList.Any())
            stack.Children.Add(new TextBlock
            {
                Text = "Keine Level gefunden.",
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });

        commScroll.Content = stack;
    }

    private async Task<bool> DownloadAndLoadCommunityLevelAsync(CommunityLevelMeta meta, Window win)
    {
        try
        {
            // fetch full discussion body from graphql lazily
            var queryObj = new
            {
                query = @"query($id: ID!) { node(id: $id) { ... on Discussion { body } } }",
                variables = new { id = meta.NodeId }
            };

            var content = new StringContent(JsonSerializer.Serialize(queryObj), System.Text.Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            request.Content = content;
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            request.Headers.UserAgent.ParseAdd("AbiturEliteCode");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new Exception("Netzwerkfehler");

            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string fullBody = doc.RootElement.GetProperty("data").GetProperty("node").GetProperty("body").GetString();

            // extract encrypted base64 payload directly
            string encryptedData = fullBody.Trim();
            if (string.IsNullOrEmpty(encryptedData)) throw new Exception("Ungültiges Level Format");

            string rawJson;
            try
            {
                // decrypt -> decompress
                string compressedData = LevelEncryption.Decrypt(encryptedData);
                rawJson = DecompressLevelData(compressedData);
            }
            catch
            {
                // fallback to uncompressed payload (if level was published using an older uncompressed format)
                rawJson = LevelEncryption.Decrypt(encryptedData);
            }

            var mutableDict = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson);

            // inject discussion id -> re-encrypt (prevents students from modifying json directly to bypass restrictions)
            mutableDict["DiscussionNodeId"] = meta.NodeId;
            mutableDict["DiscussionNumber"] = meta.Number;

            // generate scrambled id and append it to make level unique across downloads
            string scrambledId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(meta.NodeId)).Replace("=", "").Replace("+", "-").Replace("/", "_");
            if (mutableDict.ContainsKey("Name")) mutableDict["Name"] = $"{meta.Title} - {scrambledId}";
            if (mutableDict.ContainsKey("Title")) mutableDict["Title"] = $"{meta.Title} - {scrambledId}";

            string updatedJson = JsonSerializer.Serialize(mutableDict);
            string reEncrypted = LevelEncryption.Encrypt(updatedJson);

            // save to files (include scrambledId in filename to prevent os file conflicts)
            string safeName = string.Join("_", meta.Title.Split(Path.GetInvalidFileNameChars()));
            string filename = $"{safeName}_{scrambledId}.{(_isSqlMode ? "eliteslvl" : "elitelvl")}";

            string levelsDir = SaveSystem.GetLevelsDirectory();
            if (!Directory.Exists(levelsDir)) Directory.CreateDirectory(levelsDir);

            string path = Path.Combine(levelsDir, filename);

            File.WriteAllText(path, reEncrypted);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading level: {ex.Message}");
            return false;
        }
    }

    private string DecompressLevelData(string compressedBase64)
    {
        byte[] bytes = Convert.FromBase64String(compressedBase64);
        using var msi = new MemoryStream(bytes);
        using var mso = new MemoryStream();
        using (var gs = new System.IO.Compression.GZipStream(msi, System.IO.Compression.CompressionMode.Decompress))
        {
            gs.CopyTo(mso);
        }
        return System.Text.Encoding.UTF8.GetString(mso.ToArray());
    }
}
