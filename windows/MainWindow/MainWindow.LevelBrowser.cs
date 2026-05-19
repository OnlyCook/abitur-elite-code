using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
    private string _currentCustomDiscussionNodeId = null;
    private int _currentCustomDiscussionNumber = -1;
    private List<CommunityLevelMeta> _communityMetadataCache = new();
    private HashSet<string> _communitySelectedDifficulties = new();
    private HashSet<string> _communitySelectedTags = new();
    private int _communityVisibleCount = 20;
    private string _communitySortMode = "Beste";

    private static DateTime _lastCommunityFetchTime = DateTime.MinValue;

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
        public DateTime CreatedAt { get; set; }
        public double Score { get; set; }
    }

    private async void BtnRefreshCommunityLevels_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateManager.IsOutdated) return;

        if ((DateTime.Now - _lastCommunityFetchTime).TotalSeconds < 30)
        {
            return;
        }

        await FetchCommunityMetadataAsync(this);
    }

    private async Task FetchCommunityMetadataAsync(Window win)
    {
        if (UpdateManager.IsOutdated) return;

        if ((DateTime.Now - _lastCommunityFetchTime).TotalSeconds < 30) return;
        _lastCommunityFetchTime = DateTime.Now;

        var loadingPanel = win.FindControl<StackPanel>("CommunityLoadingPanel");
        loadingPanel.IsVisible = true;
        _communityMetadataCache.Clear();

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

            // fetch discussions using the retrieved categoryId
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
                                    upvotes: reactions(content: THUMBS_UP) { totalCount }
                                    downvotes: reactions(content: THUMBS_DOWN) { totalCount }
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
                // "Beste" algorithm
                double c = 5.0; // confidence factor
                double rating = (m.Upvotes + c * globalAvg) / (m.Upvotes + m.Downvotes + c);
                double ageHours = (DateTime.Now - m.CreatedAt.ToLocalTime()).TotalHours;
                double timeDecay = Math.Pow(Math.Max(ageHours, 0) + 2.0, 1.2);

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

        if (_communitySelectedTags.Any())
            filtered = filtered.Where(m => _communitySelectedTags.All(t => m.Tags.Contains(t)));

        if (sortMode == "Beste") filtered = filtered.OrderByDescending(m => m.Score);
        else if (sortMode == "Top") filtered = filtered.OrderByDescending(m => m.Upvotes).ThenByDescending(m => m.CreatedAt);
        else if (sortMode == "Neuste") filtered = filtered.OrderByDescending(m => m.CreatedAt);
        else if (sortMode == "Älteste") filtered = filtered.OrderBy(m => m.CreatedAt);

        var sortedList = filtered.ToList();
        var stack = new StackPanel { Spacing = 8 };

        var localCustomLevels = GetCustomLevels(); // to check if downloaded
        var completedList = _isSqlMode ? customPlayerData.CompletedCustomSqlLevels : customPlayerData.CompletedCustomLevels;

        foreach (var m in sortedList.Take(_communityVisibleCount))
        {
            var rowGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
                Margin = new Thickness(0, 0, 0, 5)
            };

            bool isDownloaded = localCustomLevels.Any(cl => cl.Name == m.Title && cl.Author == m.Author);
            bool isCompleted = completedList.Contains(m.Title);

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
                Text = "von " + m.Author,
                FontSize = 11,
                Foreground = Brushes.Gray
            });
            if (m.Tags.Any())
                subText.Children.Add(new TextBlock
                {
                    Text = " • " + string.Join(", ", m.Tags),
                    FontSize = 11,
                    Foreground = SolidColorBrush.Parse("#6495ED")
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

            btnMain.Click += async (s, e) => await DownloadAndLoadCommunityLevelAsync(m, win);
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
                metricStack.Children.Add(new TextBlock
                {
                    Text = m.CreatedAt.ToString("dd.MM.yyyy"),
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12
                });
            else
            {
                metricStack.Children.Add(LoadIcon("assets/icons/ic_upvote_filled.svg", 14));
                metricStack.Children.Add(new TextBlock
                {
                    Text = m.Upvotes.ToString(),
                    Foreground = SolidColorBrush.Parse("#6495ED"),
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

    private async Task DownloadAndLoadCommunityLevelAsync(CommunityLevelMeta meta, Window win)
    {
        var loadingPanel = win.FindControl<StackPanel>("CommunityLoadingPanel");
        loadingPanel.IsVisible = true;
        win.FindControl<ScrollViewer>("CommunityScroll").IsVisible = false;

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

            // decrypt -> inject discussion id -> re-encrypt (prevents students from modifying json directly to bypass restrictions)
            string rawJson = LevelEncryption.Decrypt(encryptedData);

            var jsonDoc = JsonDocument.Parse(rawJson);
            var mutableDict = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson);

            mutableDict["DiscussionNodeId"] = meta.NodeId;
            mutableDict["DiscussionNumber"] = meta.Number;

            string updatedJson = JsonSerializer.Serialize(mutableDict);
            string reEncrypted = LevelEncryption.Encrypt(updatedJson);

            // save to files
            string safeName = string.Join("_", meta.Title.Split(Path.GetInvalidFileNameChars()));
            string filename = $"{safeName}.{(_isSqlMode ? "eliteslvl" : "elitelvl")}";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levels", filename);

            File.WriteAllText(path, reEncrypted);

            // load the newly saved file
            LoadCustomLevelFromFile(path);
            win.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading level: {ex.Message}");
            loadingPanel.IsVisible = false;
            win.FindControl<ScrollViewer>("CommunityScroll").IsVisible = true;
        }
    }
}
