using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private static readonly System.Collections.Concurrent.ConcurrentQueue<Func<Task>> _apiQueue = new();
    private static bool _isApiQueueRunning = false;
    private static readonly Dictionary<string, CancellationTokenSource> _debounceTokens = new();

    private TextBlock? _cooldownLabel;
    private CancellationTokenSource? _cooldownCts;

    private static void EnqueueApiRequest(Func<Task> action)
    {
        _apiQueue.Enqueue(action);
        if (!_isApiQueueRunning)
        {
            _isApiQueueRunning = true;
            Task.Run(async () =>
            {
                while (_apiQueue.TryDequeue(out var req))
                {
                    try { await req(); } catch { }
                    await Task.Delay(5000); // 5s cooldown to not overwhelm api
                }
                _isApiQueueRunning = false;
            });
        }
    }

    private void QueueApiRequestWithDebounce(string debounceKey, Func<Task> action)
    {
        if (_debounceTokens.TryGetValue(debounceKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        cts = new CancellationTokenSource();
        _debounceTokens[debounceKey] = cts;
        var token = cts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token); // 1 second wait to check for misclicks
                if (!token.IsCancellationRequested)
                {
                    EnqueueApiRequest(action);
                }
            }
            catch (TaskCanceledException) { }
        });
    }

    private async void UpdateCommunityUIAsync(string levelId, bool isSql)
    {
        Debug.WriteLine("[Debug] Fetching level " + levelId);

        PnlCommunityActions.IsVisible = false;
        PnlCommentsSection.IsVisible = false;
        _currentActiveDiscussionId = -1;

        if (IconToggleComments != null)
            IconToggleComments.Path = "/assets/icons/ic_comment.svg";

        Debug.WriteLine($"[Debug] Discussion Mappings == null?: {_discussionMappings == null}");

        if (!AppSettings.IsCommunityFeaturesEnabled || string.IsNullOrEmpty(AppSettings.GithubToken) || _isCustomLevelMode || !NetworkInterface.GetIsNetworkAvailable() || _discussionMappings == null)
            return;

        string modeKey = isSql ? "SQL" : "C#";
        if (!_discussionMappings.ContainsKey(modeKey) || !_discussionMappings[modeKey].ContainsKey(levelId))
            return; // no discussion mapped

        int discussionNum = _discussionMappings[modeKey][levelId];
        _currentActiveDiscussionId = discussionNum;

        var dict = isSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;

        // use cache if younger than 5 minutes
        if (dict.TryGetValue(levelId, out var cache) && (DateTime.Now - cache.LastFetched).TotalMinutes < 5)
        {
            ApplyCommunityUiData(cache);
        }
        else
        {
            // fetch fresh data
            await FetchCommunityDataAsync(discussionNum, isSql, levelId, false);
        }
    }

    private async Task FetchCommunityDataAsync(int discussionNumber, bool isSql, string levelId, bool fetchNextPage)
    {
        _isFetchingComments = true;
        TxtCommentsLoading.IsVisible = true;

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

        var dict = isSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        if (!dict.TryGetValue(levelId, out var cache))
        {
            cache = new DiscussionCache();
            dict[levelId] = cache;
        }

        var queryObj = new
        {
            query = @"query($num: Int!, $cursor: String) {
                repository(owner: ""OnlyCook"", name: ""aec-community"") {
                    discussion(number: $num) {
                        id
                        upvotes: reactions(content: THUMBS_UP) { totalCount viewerHasReacted }
                        downvotes: reactions(content: THUMBS_DOWN) { totalCount viewerHasReacted }
                        comments(first: 20, after: $cursor) {
                            totalCount
                            pageInfo { endCursor hasNextPage }
                            nodes {
                                id
                                author { login }
                                body
                                createdAt
                                upvoteCount
                                viewerHasUpvoted
                                replies(first: 20) {
                                    nodes {
                                        id
                                        author { login }
                                        body
                                        createdAt
                                        reactions(content: THUMBS_UP) { totalCount viewerHasReacted }
                                    }
                                }
                            }
                        }
                    }
                }
            }",
            variables = new
            {
                num = discussionNumber,
                cursor = fetchNextPage ? cache.EndCursor : null
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.github.com/graphql", content).ConfigureAwait(false);
            string jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                using (var doc = JsonDocument.Parse(jsonString))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("repository", out var repo) &&
                        repo.ValueKind != JsonValueKind.Null)
                    {
                        var discussion = repo.GetProperty("discussion");

                        if (!fetchNextPage)
                        {
                            cache.DiscussionNodeId = discussion.GetProperty("id").GetString();
                            cache.Likes = discussion.GetProperty("upvotes").GetProperty("totalCount").GetInt32();
                            cache.ViewerHasLiked = discussion.GetProperty("upvotes").GetProperty("viewerHasReacted").GetBoolean();
                            cache.Dislikes = discussion.GetProperty("downvotes").GetProperty("totalCount").GetInt32();
                            cache.ViewerHasDisliked = discussion.GetProperty("downvotes").GetProperty("viewerHasReacted").GetBoolean();
                            cache.Comments.Clear();
                        }

                        var commentsData = discussion.GetProperty("comments");
                        cache.TotalComments = commentsData.GetProperty("totalCount").GetInt32();

                        var pageInfo = commentsData.GetProperty("pageInfo");
                        cache.EndCursor = pageInfo.GetProperty("endCursor").ValueKind != JsonValueKind.Null ? pageInfo.GetProperty("endCursor").GetString() : null;
                        cache.HasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();

                        foreach (var node in commentsData.GetProperty("nodes").EnumerateArray())
                        {
                            var newComment = new GithubComment
                            {
                                Id = node.GetProperty("id").GetString(),
                                Author = node.GetProperty("author").GetProperty("login").GetString(),
                                Body = node.GetProperty("body").GetString(),
                                CreatedAt = node.GetProperty("createdAt").GetDateTime(),
                                Upvotes = node.GetProperty("upvoteCount").GetInt32(),
                                ViewerHasUpvoted = node.GetProperty("viewerHasUpvoted").GetBoolean()
                            };

                            if (node.TryGetProperty("replies", out var repliesProp) && repliesProp.TryGetProperty("nodes", out var repNodes))
                            {
                                foreach (var rep in repNodes.EnumerateArray())
                                {
                                    newComment.Replies.Add(new GithubReply
                                    {
                                        Id = rep.GetProperty("id").GetString(),
                                        Author = rep.GetProperty("author").GetProperty("login").GetString(),
                                        Body = rep.GetProperty("body").GetString(),
                                        CreatedAt = rep.GetProperty("createdAt").GetDateTime(),
                                        Upvotes = rep.GetProperty("reactions").GetProperty("totalCount").GetInt32(),
                                        ViewerHasUpvoted = rep.GetProperty("reactions").GetProperty("viewerHasReacted").GetBoolean()
                                    });
                                }
                            }
                            cache.Comments.Add(newComment);
                        }

                        cache.LastFetched = DateTime.Now;
                        SaveSystem.SaveCommunityCache(_communityCache);

                        await Dispatcher.UIThread.InvokeAsync(() => {
                            ApplyCommunityUiData(cache);
                            TxtCommentsLocked.IsVisible = false;
                            if (PnlCommentsSection.IsVisible) RenderCachedComments();
                        });
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => TxtCommentsLocked.IsVisible = true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Fetch Error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                _isFetchingComments = false;
                TxtCommentsLoading.IsVisible = false;
                if (cache.HasNextPage && PnlCommentsSection.IsVisible) BtnLoadMoreComments.IsVisible = true;
            });
        }
    }

    private void ApplyCommunityUiData(DiscussionCache cache)
    {
        TxtLikeCount.Text = cache.Likes.ToString();
        TxtDislikeCount.Text = cache.Dislikes.ToString();
        TxtCommentCount.Text = cache.TotalComments.ToString();

        if (IconLike != null)
            IconLike.Path = cache.ViewerHasLiked ? "/assets/icons/ic_like_filled.svg" : "/assets/icons/ic_like.svg";

        if (IconDislike != null)
            IconDislike.Path = cache.ViewerHasDisliked ? "/assets/icons/ic_dislike_filled.svg" : "/assets/icons/ic_dislike.svg";

        PnlCommunityActions.IsVisible = true;
    }

    private void LoadDiscussionMappings()
    {
        try
        {
            var asset = AssetLoader.Open(new Uri("avares://AbiturEliteCode/assets/aecc-discussion-mappings.json"));
            using var reader = new StreamReader(asset);
            string json = reader.ReadToEnd();

            _discussionMappings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json);
            Debug.WriteLine("[Community] Mappings loaded successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Failed to load mappings resource: {ex.Message}");
            _discussionMappings = new(); // prevent null refs
        }
    }

    private void BtnToggleComments_Click(object sender, RoutedEventArgs e)
    {
        PnlCommentsSection.IsVisible = !PnlCommentsSection.IsVisible;
        IconToggleComments.Path = PnlCommentsSection.IsVisible ? "/assets/icons/ic_comment_hide.svg" : "/assets/icons/ic_comment.svg";

        if (PnlCommentsSection.IsVisible && _currentActiveDiscussionId != -1)
        {
            RenderCachedComments();
        }
    }

    private async void BtnLoadMoreComments_Click(object sender, RoutedEventArgs e)
    {
        if (_isFetchingComments || _currentActiveDiscussionId == -1) return;

        string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, true);
    }

    private void TaskScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // dynamic loading when scrolled to bottom
        if (!PnlCommentsSection.IsVisible || _isFetchingComments || _currentActiveDiscussionId == -1) return;

        var scrollViewer = sender as ScrollViewer;
        if (scrollViewer != null)
        {
            if (scrollViewer.Offset.Y >= scrollViewer.Extent.Height - scrollViewer.Viewport.Height - 50)
            {
                var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
                string levelKey = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();

                if (dict.TryGetValue(levelKey, out var cache) && cache.HasNextPage)
                {
                    BtnLoadMoreComments_Click(null, null);
                }
            }
        }
    }

    private void RenderCachedComments()
    {
        PnlCommentsList.Children.Clear();
        string levelKey = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;

        if (!dict.TryGetValue(levelKey, out var cache)) return;

        var txtEmpty = this.FindControl<TextBlock>("TxtCommentsEmpty");
        if (txtEmpty != null)
        {
            txtEmpty.IsVisible = cache.Comments.Count == 0;
        }

        string sortMode = (CmbCommentSort.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Top";
        var sortedComments = cache.Comments.ToList();

        if (sortMode == "Top")
        {
            sortedComments = sortedComments.OrderByDescending(c => c.Upvotes).ThenBy(c => c.CreatedAt).ToList();
        }
        else if (sortMode == "Neuste")
        {
            sortedComments = sortedComments.OrderByDescending(c => c.CreatedAt).ToList();
        }
        else
        {
            sortedComments = sortedComments.OrderBy(c => c.CreatedAt).ToList();
        }

        foreach (var comment in sortedComments)
        {
            PnlCommentsList.Children.Add(CreateCommentUI(comment, cache.DiscussionNodeId));
        }

        BtnLoadMoreComments.IsVisible = cache.HasNextPage;
    }

    public void BtnLike_Click(object sender, RoutedEventArgs e)
    {
        ToggleReaction(true);
    }

    public void BtnDislike_Click(object sender, RoutedEventArgs e)
    {
        ToggleReaction(false);
    }

    private void ToggleReaction(bool isLike)
    {
        if (string.IsNullOrEmpty(AppSettings.GithubToken) || _currentActiveDiscussionId == -1) return;

        string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        if (!dict.TryGetValue(levelId, out var cache)) return;

        // mutually exclusive local state updates
        if (isLike)
        {
            if (cache.ViewerHasLiked)
            {
                cache.ViewerHasLiked = false;
                cache.Likes--;
            }
            else
            {
                cache.ViewerHasLiked = true;
                cache.Likes++;
                if (cache.ViewerHasDisliked)
                {
                    cache.ViewerHasDisliked = false;
                    cache.Dislikes--;
                }
            }
        }
        else
        {
            if (cache.ViewerHasDisliked)
            {
                cache.ViewerHasDisliked = false;
                cache.Dislikes--;
            }
            else
            {
                cache.ViewerHasDisliked = true;
                cache.Dislikes++;
                if (cache.ViewerHasLiked)
                {
                    cache.ViewerHasLiked = false;
                    cache.Likes--;
                }
            }
        }

        ApplyCommunityUiData(cache);

        QueueApiRequestWithDebounce($"level_react_{cache.DiscussionNodeId}", () => SyncReactionToGithubAsync(cache));
    }


    private async Task SyncReactionToGithubAsync(DiscussionCache cache)
    {
        if (string.IsNullOrEmpty(cache.DiscussionNodeId) || string.IsNullOrEmpty(AppSettings.GithubToken)) return;

        // local function to dispatch mutations to graphql
        async Task MutateReaction(string content, bool add)
        {
            string mutation = add ? "addReaction" : "removeReaction";
            var queryObj = new
            {
                query = $@"mutation($subjectId: ID!, $content: ReactionContent!) {{
                    {mutation}(input: {{subjectId: $subjectId, content: $content}}) {{
                        reaction {{ content }}
                    }}
                }}",
                variables = new
                {
                    subjectId = cache.DiscussionNodeId,
                    content = content
                }
            };

            var httpContent = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", httpContent);
            await resp.Content.ReadAsStringAsync();
        }

        try
        {
            if (cache.ViewerHasLiked)
            {
                await MutateReaction("THUMBS_UP", true);
                await MutateReaction("THUMBS_DOWN", false);
            }
            else if (cache.ViewerHasDisliked)
            {
                await MutateReaction("THUMBS_DOWN", true);
                await MutateReaction("THUMBS_UP", false);
            }
            else
            {
                await MutateReaction("THUMBS_UP", false);
                await MutateReaction("THUMBS_DOWN", false);
            }

            // keep local memory up to date
            cache.LastFetched = DateTime.Now;
            SaveSystem.SaveCommunityCache(_communityCache);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Reaction Sync Error: {ex.Message}");
        }
    }

    private void CmbCommentSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PnlCommentsSection != null && PnlCommentsSection.IsVisible && _currentActiveDiscussionId != -1)
        {
            RenderCachedComments();
        }
    }

    private void TxtCommentInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        int length = TxtCommentInput.Text?.Length ?? 0;
        TxtCommentCharCount.Text = $"{length} / 5000";

        if (length > 5000)
        {
            TxtCommentCharCount.Foreground = Brushes.Red;
        }
        else
        {
            TxtCommentCharCount.Foreground = Brushes.Gray;
        }

        bool canSend = length > 0 && length <= 5000;
        BtnSendComment.IsEnabled = canSend;
        BtnSendComment.Opacity = canSend ? 1.0 : 0.5;
        IconSendComment.Path = canSend ? "/assets/icons/ic_send.svg" : "/assets/icons/ic_send_disabled.svg";
    }

    private Control CreateCommentUI(GithubComment comment, string discussionId, bool isReply = false)
    {
        var border = new Border
        {
            Background = SolidColorBrush.Parse(isReply ? "#141414" : "#1A1A1A"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(15),
            BorderBrush = SolidColorBrush.Parse("#333"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 5, 0, 0)
        };

        var mainStack = new StackPanel { Spacing = 8 };

        string bodyToRender = comment.Body;
        string activeTag = null;
        IBrush tagColor = Brushes.Gray;

        var tags = new Dictionary<string, (string Label, string Color)>
        {
            { "!FEEDBACK;", ("Feedback", "#A870A8") },
            { "!FRAGE;", ("Frage", "#007ACC") },
            { "!TIPP;", ("Tipp", "#32A852") },
            { "!LÖSUNG;", ("Lösung", "#FFD700") }
        };

        foreach (var tag in tags)
        {
            if (bodyToRender.StartsWith(tag.Key))
            {
                activeTag = tag.Value.Label;
                tagColor = SolidColorBrush.Parse(tag.Value.Color);
                bodyToRender = bodyToRender.Substring(tag.Key.Length).TrimStart();
                break;
            }
        }

        // header
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            Margin = new Thickness(0, 0, 0, -5)
        };
        headerPanel.Children.Add(new TextBlock
        {
            Text = $"{comment.Author}",
            Foreground = comment.Author == AppSettings.GithubUsername ? Brush.Parse("#6495ED") : Brushes.Gray,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = $" • {comment.CreatedAt:dd.MM.yyyy 'um' HH:mm}",
            Foreground = Brushes.Gray,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (activeTag != null)
        {
            headerPanel.Children.Add(new Border
            {
                Background = SolidColorBrush.Parse("#252526"),
                BorderBrush = tagColor,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = activeTag,
                    FontSize = 10,
                    Foreground = tagColor,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
        }
        headerGrid.Children.Add(headerPanel);

        var btnMore = new Button
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(5),
            Content = LoadIcon("assets/icons/ic_more.svg", 16)
        };
        Grid.SetColumn(btnMore, 1);
        headerGrid.Children.Add(btnMore);
        Button btnEdit = null;
        Button btnDelete = null;

        if (comment.Author == AppSettings.GithubUsername)
        {
            btnMore.Cursor = Avalonia.Input.Cursor.Parse("Hand");

            var flyout = new Flyout();
            var flyoutStack = new StackPanel
            {
                Spacing = 5,
                Margin = new Thickness(-5)
            };

            btnEdit = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        LoadIcon("assets/icons/ic_edit.svg", 16),
                        new TextBlock
                        {
                            Text = "Bearbeiten",
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 8),
                CornerRadius = new CornerRadius(4),
                Cursor = Avalonia.Input.Cursor.Parse("Hand")
            };

            btnDelete = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        LoadIcon("assets/icons/ic_delete.svg", 16),
                        new TextBlock
                        {
                            Text = "Löschen",
                            Foreground = Brushes.Red,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 8),
                CornerRadius = new CornerRadius(4),
                Cursor = Avalonia.Input.Cursor.Parse("Hand")
            };

            flyoutStack.Children.Add(btnEdit);
            flyoutStack.Children.Add(btnDelete);
            flyout.Content = flyoutStack;
            btnMore.Flyout = flyout;
        }
        else
        {
            // non-user comment/reply -> make button invisible decoy
            btnMore.Opacity = 0;
            btnMore.IsHitTestVisible = false;
        }

        mainStack.Children.Add(headerGrid);

        // body (with spoiler handling and collapse layout for long comments)
        var bodyWrapper = new Grid();
        var bodyContainer = new StackPanel
        {
            Spacing = 5,
            ClipToBounds = true
        };

        if (activeTag == "Lösung")
        {
            var spoilerBtn = new Button
            {
                Content = "Lösung anzeigen",
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Foreground = Brushes.White,
                Padding = new Thickness(10, 5),
                CornerRadius = new CornerRadius(4),
                Cursor = Avalonia.Input.Cursor.Parse("Hand")
            };
            var spoilerContent = new StackPanel
            {
                IsVisible = false,
                Margin = new Thickness(0, 10, 0, 0)
            };

            RenderMarkdownToPanel(spoilerContent, bodyToRender);

            spoilerBtn.Click += (s, e) =>
            {
                spoilerBtn.IsVisible = false;
                spoilerContent.IsVisible = true;
            };
            bodyContainer.Children.Add(spoilerBtn);
            bodyContainer.Children.Add(spoilerContent);
        }
        else
        {
            RenderMarkdownToPanel(bodyContainer, bodyToRender);
        }

        bool isLong = bodyToRender.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length > 5;
        if (isLong && activeTag != "Lösung")
        {
            bodyContainer.MaxHeight = 120;
            bodyContainer.Margin = new Thickness(0, 0, 0, 30);

            // fog effect
            Color bgColor = isReply ? Color.Parse("#141414") : Color.Parse("#1A1A1A");
            var fogBorder = new Border
            {
                Height = 50,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 30),
                IsHitTestVisible = false,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop
                        {
                            Color = Color.FromArgb(0, bgColor.R, bgColor.G, bgColor.B),
                            Offset = 0.0
                        },
                        new GradientStop
                        {
                            Color = bgColor,
                            Offset = 1.0
                        }
                    }
                }
            };

            var btnToggleExpand = new Button
            {
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(5),
                Margin = new Thickness(0),
                Cursor = Avalonia.Input.Cursor.Parse("Hand")
            };

            var expandContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5
            };
            var expandIconContainer = new Panel();
            expandIconContainer.Children.Add(LoadIcon("assets/icons/ic_expand.svg", 16));

            var expandText = new TextBlock
            {
                Text = "Mehr anzeigen",
                Foreground = Brushes.Gray,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            expandContent.Children.Add(expandIconContainer);
            expandContent.Children.Add(expandText);
            btnToggleExpand.Content = expandContent;

            bool isExpanded = false;
            btnToggleExpand.Click += (s, e) =>
            {
                isExpanded = !isExpanded;
                bodyContainer.MaxHeight = isExpanded ? double.PositiveInfinity : 120;
                fogBorder.IsVisible = !isExpanded;

                expandIconContainer.Children.Clear();
                expandIconContainer.Children.Add(LoadIcon(isExpanded ? "assets/icons/ic_collapse.svg" : "assets/icons/ic_expand.svg", 16));
                expandText.Text = isExpanded ? "Weniger anzeigen" : "Mehr anzeigen";
            };

            // important z-index order
            bodyWrapper.Children.Add(bodyContainer);
            bodyWrapper.Children.Add(fogBorder);
            bodyWrapper.Children.Add(btnToggleExpand);
        }
        else
        {
            bodyWrapper.Children.Add(bodyContainer);
        }

        mainStack.Children.Add(bodyWrapper);

        var editGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto"),
            IsVisible = false,
            Margin = new Thickness(0, 5, 0, 5)
        };
        var txtEdit = new TextBox
        {
            Text = comment.Body,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = Brushes.White,
            BorderBrush = SolidColorBrush.Parse("#333"),
            CornerRadius = new CornerRadius(4),
            MaxHeight = 150
        };
        var editActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var btnCancelEdit = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Cursor = Avalonia.Input.Cursor.Parse("Hand")
        };
        var btnSaveEdit = new Button
        {
            Content = "Speichern",
            Background = SolidColorBrush.Parse("#32A852"),
            Cursor = Avalonia.Input.Cursor.Parse("Hand")
        };

        editActions.Children.Add(btnCancelEdit);
        editActions.Children.Add(btnSaveEdit);
        Grid.SetRow(editActions, 1);
        editGrid.Children.Add(txtEdit);
        editGrid.Children.Add(editActions);

        mainStack.Children.Add(editGrid);

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15,
            Margin = new Thickness(0, isReply ? 7 : 5, 0, 0)
        };

        // upvote
        var btnUpvote = new Button
        {
            Background = comment.ViewerHasUpvoted ? SolidColorBrush.Parse("#256495ED") : Brushes.Transparent,
            BorderBrush = comment.ViewerHasUpvoted ? SolidColorBrush.Parse("#6495ED") : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5),
            Cursor = Avalonia.Input.Cursor.Parse("Hand")
        };
        var upvoteContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };
        upvoteContent.Children.Add(LoadIcon(comment.ViewerHasUpvoted ? "assets/icons/ic_upvote_filled.svg" : "assets/icons/ic_upvote.svg", 16));
        upvoteContent.Children.Add(new TextBlock
        {
            Text = comment.Upvotes.ToString(),
            Foreground = comment.ViewerHasUpvoted ? SolidColorBrush.Parse("#6495ED") : Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center
        });
        btnUpvote.Content = upvoteContent;

        btnUpvote.Click += (s, e) =>
        {
            // optimistic ui state update
            bool targetState = !comment.ViewerHasUpvoted;
            comment.ViewerHasUpvoted = targetState;
            comment.Upvotes += targetState ? 1 : -1;

            btnUpvote.Background = targetState ? SolidColorBrush.Parse("#256495ED") : Brushes.Transparent;
            btnUpvote.BorderBrush = targetState ? SolidColorBrush.Parse("#6495ED") : Brushes.Transparent;

            upvoteContent.Children.Clear();
            upvoteContent.Children.Add(LoadIcon(targetState ? "assets/icons/ic_upvote_filled.svg" : "assets/icons/ic_upvote.svg", 16));
            upvoteContent.Children.Add(new TextBlock
            {
                Text = comment.Upvotes.ToString(),
                Foreground = targetState ? SolidColorBrush.Parse("#6495ED") : Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            });

            // queue upvote
            QueueApiRequestWithDebounce($"upvote_{comment.Id}", () =>
                isReply
                    ? ToggleReplyUpvoteAsync(comment.Id, targetState)
                    : ToggleCommentUpvoteAsync(comment.Id, targetState));
        };
        actionsPanel.Children.Add(btnUpvote);

        // reply button and box setup
        Grid replyInputGrid = null;
        if (!isReply)
        {
            var btnToggleReply = new Button
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(5),
                Cursor = Avalonia.Input.Cursor.Parse("Hand")
            };
            var replyContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5
            };
            replyContent.Children.Add(LoadIcon("assets/icons/ic_comment_add.svg", 18));
            btnToggleReply.Content = replyContent;
            ToolTip.SetTip(btnToggleReply, "Antwort verfassen");

            replyInputGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                Margin = new Thickness(0, 10, 0, 0),
                IsVisible = false
            };
            const int ReplyCharLimit = 1000;

            var txtReply = new TextBox
            {
                Watermark = "Antwort verfassen...",
                Background = SolidColorBrush.Parse("#1A1A1A"),
                Foreground = Brushes.White,
                BorderBrush = SolidColorBrush.Parse("#333"),
                CornerRadius = new CornerRadius(4),
                AcceptsReturn = true,
                MaxHeight = 100,
                MaxLength = ReplyCharLimit
            };
            var btnSendReply = new Button
            {
                Background = SolidColorBrush.Parse("#3C3C3C"),
                Cursor = Avalonia.Input.Cursor.Parse("Hand"),
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 6),
                VerticalAlignment = VerticalAlignment.Bottom,
                IsEnabled = false,
                Opacity = 0.5
            };
            var iconSendReply = LoadIcon("assets/icons/ic_send_disabled.svg", 18);
            btnSendReply.Content = iconSendReply;

            txtReply.TextChanged += (s, e) =>
            {
                bool canSend = !string.IsNullOrWhiteSpace(txtReply.Text);
                btnSendReply.IsEnabled = canSend;
                btnSendReply.Opacity = canSend ? 1.0 : 0.5;
                btnSendReply.Content = LoadIcon(canSend ? "assets/icons/ic_send.svg" : "assets/icons/ic_send_disabled.svg", 18);
            };

            btnToggleReply.Click += (s, e) =>
            {
                replyInputGrid.IsVisible = !replyInputGrid.IsVisible;
                replyContent.Children[0] = LoadIcon(
                    replyInputGrid.IsVisible ? "assets/icons/ic_comment_add_hide.svg" : "assets/icons/ic_comment_add.svg", 18);
                ToolTip.SetTip(btnToggleReply, replyInputGrid.IsVisible ? "Antwort verbergen" : "Antwort verfassen");
            };

            btnSendReply.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtReply.Text)) return;
                btnSendReply.IsEnabled = false;
                btnSendReply.Opacity = 0.5;
                btnSendReply.Content = LoadIcon("assets/icons/ic_send_disabled.svg", 18);

                Debug.WriteLine($"[Debug] Sent Reply to comment.Id=[{comment.Id}] in discussionId=[{discussionId}]...");
                await SendReplyToGithubAsync(discussionId, comment.Id, txtReply.Text);

                txtReply.Text = "";
                replyInputGrid.IsVisible = false;
                replyContent.Children[0] = LoadIcon("assets/icons/ic_comment_add.svg", 18);
                ToolTip.SetTip(btnToggleReply, "Antwort verfassen");

                // unsubscribe natively without risking api queue stall
                _ = Task.Run(async () => {
                    await Task.Delay(5000);
                    await UnsubscribeFromDiscussionAsync(discussionId);
                });
            };

            Grid.SetColumn(btnSendReply, 1);
            replyInputGrid.Children.Add(txtReply);
            replyInputGrid.Children.Add(btnSendReply);

            actionsPanel.Children.Add(btnToggleReply);

            Border repliesContainer = null;
            if (comment.Replies.Count > 0)
            {
                var btnShowReplies = new Button
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(5),
                    Cursor = Avalonia.Input.Cursor.Parse("Hand")
                };
                var showRepliesContent = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5
                };
                showRepliesContent.Children.Add(LoadIcon("assets/icons/ic_comment.svg", 16));
                var txtShowReplies = new TextBlock
                {
                    Text = comment.Replies.Count == 1 ? "1 Antwort" : $"{comment.Replies.Count} Antworten",
                    Foreground = Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                showRepliesContent.Children.Add(txtShowReplies);
                btnShowReplies.Content = showRepliesContent;

                actionsPanel.Children.Add(btnShowReplies);

                repliesContainer = new Border
                {
                    BorderThickness = new Thickness(2, 0, 0, 0),
                    BorderBrush = SolidColorBrush.Parse("#333"),
                    Margin = new Thickness(15, 10, 0, 0),
                    Padding = new Thickness(15, 0, 0, 0),
                    IsVisible = false
                };
                var repliesStack = new StackPanel
                {
                    Spacing = 8
                };
                repliesContainer.Child = repliesStack;

                btnShowReplies.Click += (s, e) =>
                {
                    repliesContainer.IsVisible = !repliesContainer.IsVisible;
                    txtShowReplies.Text = repliesContainer.IsVisible ? "Antworten ausblenden" : comment.Replies.Count == 1 ? "1 Antwort" : $"{comment.Replies.Count} Antworten";
                    showRepliesContent.Children[0] = LoadIcon(
                        repliesContainer.IsVisible ? "assets/icons/ic_comment_hide.svg" : "assets/icons/ic_comment.svg", 16);
                };

                foreach (var reply in comment.Replies.OrderBy(r => r.CreatedAt))
                {
                    repliesStack.Children.Add(CreateCommentUI(new GithubComment
                    {
                        Id = reply.Id,
                        Author = reply.Author,
                        Body = reply.Body,
                        CreatedAt = reply.CreatedAt,
                        Upvotes = reply.Upvotes,
                        ViewerHasUpvoted = reply.ViewerHasUpvoted
                    }, discussionId, true));
                }
            }

            mainStack.Children.Add(actionsPanel);
            if (replyInputGrid != null) mainStack.Children.Add(replyInputGrid);
            if (repliesContainer != null) mainStack.Children.Add(repliesContainer);
        }
        else
        {
            mainStack.Children.Add(actionsPanel);
        }

        if (btnMore != null && btnEdit != null && btnDelete != null)
        {
            btnEdit.Click += (s, e) =>
            {
                btnMore.Flyout?.Hide();
                bodyWrapper.IsVisible = false;
                actionsPanel.IsVisible = false;
                if (!isReply && replyInputGrid != null) replyInputGrid.IsVisible = false;
                editGrid.IsVisible = true;
            };

            btnCancelEdit.Click += (s, e) =>
            {
                editGrid.IsVisible = false;
                bodyWrapper.IsVisible = true;
                actionsPanel.IsVisible = true;
                txtEdit.Text = comment.Body;
            };

            btnSaveEdit.Click += async (s, e) =>
            {
                btnSaveEdit.IsEnabled = false;
                await UpdateCommentOrReplyAsync(comment.Id, txtEdit.Text);
                string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
            };

            btnDelete.Click += async (s, e) =>
            {
                btnMore.Flyout?.Hide();
                btnDelete.IsEnabled = false;
                await DeleteCommentOrReplyAsync(comment.Id);
                string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
            };
        }

        border.Child = mainStack;
        return border;
    }

    private void RenderMarkdownToPanel(StackPanel panel, string text)
    {
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        bool inCodeBlock = false;
        StringBuilder codeBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    inCodeBlock = false;
                    var codeContent = codeBuilder.ToString().TrimEnd('\r', '\n');

                    var codeBlockEditor = new TextEditor
                    {
                        Document = new TextDocument(codeContent),
                        SyntaxHighlighting = _isSqlMode ? SqlCodeEditor.GetDarkSqlHighlighting() : CsharpCodeEditor.GetDarkCsharpHighlighting(),
                        FontFamily = new FontFamily(MonospaceFontFamily),
                        FontSize = 13,
                        IsReadOnly = true,
                        ShowLineNumbers = false,
                        Background = Brushes.Transparent,
                        Foreground = Brushes.White,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Padding = new Thickness(10, 6, 10, 6),
                        MinHeight = 0
                    };
                    codeBlockEditor.Options.ShowSpaces = false;
                    codeBlockEditor.Options.ShowTabs = false;
                    codeBlockEditor.Options.HighlightCurrentLine = false;

                    var border = new Border
                    {
                        Background = SolidColorBrush.Parse("#1A1A1A"),
                        CornerRadius = new CornerRadius(6),
                        ClipToBounds = true,
                        Margin = new Thickness(0, 5, 0, 5),
                        BorderBrush = SolidColorBrush.Parse("#333"),
                        BorderThickness = new Thickness(1),
                        Child = codeBlockEditor
                    };
                    panel.Children.Add(border);
                    codeBuilder.Clear();
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBuilder.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                panel.Children.Add(new Control { Height = 8 });
                continue;
            }

            var textBlock = new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 2)
            };
            int currentIndex = 0;

            foreach (Match match in MarkdownInlineRegex.Matches(line))
            {
                if (match.Index > currentIndex)
                    textBlock.Inlines.Add(new Run(line.Substring(currentIndex, match.Index - currentIndex)));

                if (match.Groups["bold"].Success)
                {
                    var bold = new Bold();
                    bold.Inlines.Add(new Run(match.Groups["boldtext"].Value));
                    textBlock.Inlines.Add(bold);
                }
                else if (match.Groups["kbd"].Success)
                {
                    textBlock.Inlines.Add(new InlineUIContainer(new Border
                    {
                        Background = SolidColorBrush.Parse("#3C3C3C"),
                        BorderBrush = SolidColorBrush.Parse("#555555"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1),
                        Margin = new Thickness(2, 0),
                        Child = new TextBlock
                        {
                            Text = match.Groups["kbdtext"].Value,
                            FontSize = 11,
                            FontFamily = FontFamily.Parse(MonospaceFontFamily),
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    })
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                }
                else if (match.Groups["code"].Success)
                {
                    textBlock.Inlines.Add(new InlineUIContainer(new Border
                    {
                        Background = SolidColorBrush.Parse("#2D2D30"),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1),
                        Margin = new Thickness(2, 0),
                        Child = new TextBlock
                        {
                            Text = match.Groups["codetext"].Value,
                            FontSize = 12,
                            FontFamily = FontFamily.Parse(MonospaceFontFamily),
                            Foreground = SolidColorBrush.Parse("#DCDCAA"),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    })
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    });
                }

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < line.Length)
                textBlock.Inlines.Add(new Run(line.Substring(currentIndex)));

            panel.Children.Add(textBlock);
        }

        // safety check for unclosed blocks
        if (inCodeBlock && codeBuilder.Length > 0)
        {
            var codeContent = codeBuilder.ToString().TrimEnd('\r', '\n');
            var codeBlockEditor = new TextEditor
            {
                Document = new TextDocument(codeContent),
                SyntaxHighlighting = _isSqlMode ? SqlCodeEditor.GetDarkSqlHighlighting() : CsharpCodeEditor.GetDarkCsharpHighlighting(),
                FontFamily = new FontFamily(MonospaceFontFamily),
                FontSize = 13,
                IsReadOnly = true,
                ShowLineNumbers = false,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(10, 6, 10, 6),
                MinHeight = 0
            };
            codeBlockEditor.Options.ShowSpaces = false;
            codeBlockEditor.Options.ShowTabs = false;
            codeBlockEditor.Options.HighlightCurrentLine = false;

            var border = new Border
            {
                Background = SolidColorBrush.Parse("#1A1A1A"),
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Margin = new Thickness(0, 5, 0, 5),
                BorderBrush = SolidColorBrush.Parse("#333"),
                BorderThickness = new Thickness(1),
                Child = codeBlockEditor
            };
            panel.Children.Add(border);
        }
    }

    private async void BtnSendComment_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCommentInput.Text)) return;

        // global 20s cooldown verification upon click
        double secondsSinceLastComment = (DateTime.Now - _lastCommentTime).TotalSeconds;
        if (secondsSinceLastComment < 20)
        {
            ShowCooldownMessage((int)(20 - secondsSinceLastComment));
            return;
        }

        BtnSendComment.IsEnabled = false;

        string tagPrefix = "";
        string tagSelection = (CmbCommentTag.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (tagSelection != null && tagSelection != "–")
        {
            tagPrefix = $"!{tagSelection.ToUpper()}; ";
        }

        string fullBody = tagPrefix + TxtCommentInput.Text;

        string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        if (dict.TryGetValue(levelId, out var cache))
        {
            // Safety net: in case the node ID didn't cache properly
            if (string.IsNullOrEmpty(cache.DiscussionNodeId))
            {
                await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
            }

            _lastCommentTime = DateTime.Now;

            string mutation = @"mutation($discussionId: ID!, $body: String!) { addDiscussionComment(input: {discussionId: $discussionId, body: $body}) { comment { id } } }";
            var queryObj = new
            {
                query = mutation,
                variables = new
                {
                    discussionId = cache.DiscussionNodeId,
                    body = fullBody
                }
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
                string resBody = await resp.Content.ReadAsStringAsync(); // consume response to free socket connection

                if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
                {
                    TxtCommentInput.Text = "";

                    // unsubscribe user automatically outside of the queue logic
                    _ = Task.Run(async () => {
                        await Task.Delay(5000); // wait 5 seconds so github subscribes user first
                        await UnsubscribeFromDiscussionAsync(cache.DiscussionNodeId);
                    });

                    // refetch immediately to show the new comment
                    await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
                }
                else
                {
                    Debug.WriteLine($"GraphQL Error: {resBody}");
                    BtnSendComment.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Comment Submit Error: {ex.Message}");
                BtnSendComment.IsEnabled = true;
            }
        }
        else
        {
            BtnSendComment.IsEnabled = true;
        }
    }

    private async Task SendReplyToGithubAsync(string discussionNodeId, string commentNodeId, string body)
    {
        string mutation = @"mutation($discussionId: ID!, $replyToId: ID!, $body: String!) { addDiscussionComment(input: {discussionId: $discussionId, replyToId: $replyToId, body: $body}) { comment { id } } }";
        var queryObj = new
        {
            query = mutation,
            variables = new
            {
                discussionId = discussionNodeId,
                replyToId = commentNodeId,
                body = body
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
            {
                string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
            }
            else
            {
                Debug.WriteLine($"GraphQL Reply Error: {resBody}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Reply Submit Error: {ex.Message}");
        }
    }

    private async Task ToggleCommentUpvoteAsync(string subjectId, bool targetState)
    {
        string op = targetState ? "addUpvote" : "removeUpvote";
        string mutation = $@"mutation($subjectId: ID!) {{ {op}(input: {{subjectId: $subjectId}}) {{ clientMutationId }} }}";
        var queryObj = new
        {
            query = mutation,
            variables = new
            {
                subjectId = subjectId
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
            await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toggle Upvote Error: {ex.Message}");
        }
    }

    private async Task UpdateCommentOrReplyAsync(string id, string body)
    {
        string mutation = @"mutation($id: ID!, $body: String!) { updateDiscussionComment(input: {commentId: $id, body: $body}) { comment { id } } }";
        var queryObj = new
        {
            query = mutation,
            variables = new
            {
                id = id,
                body = body
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
            await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Update Comment Error: {ex.Message}");
        }
    }

    private async Task DeleteCommentOrReplyAsync(string id)
    {
        string mutation = @"mutation($id: ID!) { deleteDiscussionComment(input: {id: $id}) { comment { id } } }";
        var queryObj = new
        {
            query = mutation,
            variables = new
            {
                id = id
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
            await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Delete Comment Error: {ex.Message}");
        }
    }

    private async Task ToggleReplyUpvoteAsync(string subjectId, bool targetState)
    {
        string op = targetState ? "addReaction" : "removeReaction";
        string mutation = $@"mutation($subjectId: ID!) {{ {op}(input: {{subjectId: $subjectId, content: THUMBS_UP}}) {{ clientMutationId }} }}";
        var queryObj = new
        {
            query = mutation,
            variables = new
            {
                subjectId = subjectId
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
            await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Toggle Reply Upvote Error: {ex.Message}");
        }
    }

    private async Task UnsubscribeFromDiscussionAsync(string discussionNodeId)
    {
        if (string.IsNullOrEmpty(discussionNodeId)) return;

        string mutation = @"mutation($id: ID!) { updateSubscription(input: {subscribableId: $id, state: IGNORED}) { subscribable { viewerSubscription } } }";
        var queryObj = new
        {
            query = mutation,
            variables = new
            {
                id = discussionNodeId
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.PostAsync("https://api.github.com/graphql", content);
            string resBody = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode || resBody.Contains("\"errors\":"))
            {
                Debug.WriteLine($"GraphQL Unsubscribe Error: {resBody}");
            }
        }
        catch { }
    }

    private async void ShowCooldownMessage(int waitTime)
    {
        if (TxtCooldownMessage == null) return;

        _cooldownCts?.Cancel();
        _cooldownCts = new CancellationTokenSource();
        var token = _cooldownCts.Token;

        TxtCooldownMessage.IsVisible = true;
        try
        {
            for (int i = waitTime; i > 0; i--)
            {
                TxtCooldownMessage.Text = $"Warte {i}s";
                await Task.Delay(1000, token);
            }
            TxtCooldownMessage.IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }
}
