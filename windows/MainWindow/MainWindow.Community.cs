using AbiturEliteCode.cs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode;

public partial class MainWindow
{
    private static readonly System.Collections.Concurrent.ConcurrentQueue<(string Description, Func<Task> Action)> _apiQueue = new();
    private static bool _isApiQueueRunning = false;
    private static int _apiQueueInFlight = 0;
    private static DateTime _nextAvailableApiTime = DateTime.MinValue;
    private static readonly Dictionary<string, CancellationTokenSource> _debounceTokens = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Description, Func<Task> Action)> _pendingDebounces = new();
    private CancellationTokenSource? _offlineCts;
    private bool _isKnownOffline = false;
    private CancellationTokenSource? _fullQueueCts;

    private TextBlock? _cooldownLabel;
    private CancellationTokenSource? _cooldownCts;

    private const int ApiQueueLimit = 10;

    public static List<string> GetApiQueueSnapshot() => _apiQueue.Select(x => x.Description).ToList();
    public static DateTime GetNextAvailableApiTime() => _nextAvailableApiTime;

    private bool TryEnqueueApiRequest(string description, Func<Task> action)
    {
        int effectiveCount = _apiQueue.Count + _pendingDebounces.Count;
        if (effectiveCount >= ApiQueueLimit)
        {
            Debug.WriteLine($"[Queue] FULL ({effectiveCount}/{ApiQueueLimit}), rejected: \"{description}\"");
            ShowFullQueueBannerAsync();
            return false;
        }
        EnqueueApiRequest(description, action);
        return true;
    }

    private async void ShowFullQueueBannerAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _fullQueueCts?.Cancel();
            _fullQueueCts?.Dispose();
            _fullQueueCts = new CancellationTokenSource();
        });

        var token = _fullQueueCts!.Token;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PnlCommunityActions.IsVisible = true;
            if (TxtCommunityFullQueueStatus != null)
                TxtCommunityFullQueueStatus.IsVisible = true;
        });

        try { await Task.Delay(3000, token); }
        catch (TaskCanceledException) { return; }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (TxtCommunityFullQueueStatus != null)
                TxtCommunityFullQueueStatus.IsVisible = false;
        });
    }

    private async Task ShowOfflineBannerOnceAsync()
    {
        _offlineCts?.Cancel();
        _offlineCts?.Dispose();
        _offlineCts = new CancellationTokenSource();
        var token = _offlineCts.Token;

        // disable all interaction and reveal bar in bare bones state
        BtnLike.IsEnabled = false;
        BtnDislike.IsEnabled = false;
        BtnToggleComments.IsEnabled = false;
        PnlCommentsSection.IsVisible = false;
        SetCommunitySkeletonsVisible(false);

        PnlCommunityActions.IsVisible = true;
        if (TxtCommunityOfflineStatus != null)
            TxtCommunityOfflineStatus.IsVisible = true;

        try
        {
            await Task.Delay(3000, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (TxtCommunityOfflineStatus != null)
            TxtCommunityOfflineStatus.IsVisible = false;
        PnlCommunityActions.IsVisible = false;
    }

    private void FlushPendingDebounces()
    {
        foreach (var kvp in _pendingDebounces)
        {
            if (_debounceTokens.TryGetValue(kvp.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
            // bypass limit check on flush
            EnqueueApiRequest(kvp.Value.Description, kvp.Value.Action);
        }
        _pendingDebounces.Clear();
        _debounceTokens.Clear();
    }

    private static void EnqueueApiRequest(string description, Func<Task> action)
    {
        _apiQueue.Enqueue((description, action));
        Debug.WriteLine($"[Queue] Enqueued: \"{description}\" | size: {_apiQueue.Count}");
        if (!_isApiQueueRunning)
        {
            _isApiQueueRunning = true;
            Task.Run(async () =>
            {
                while (_apiQueue.TryDequeue(out var req))
                {
                    Interlocked.Increment(ref _apiQueueInFlight);
                    Debug.WriteLine($"[Queue] Running: \"{req.Description}\" | remaining: {_apiQueue.Count}");
                    try
                    {
                        await req.Action();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Queue] Error in \"{req.Description}\": {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _apiQueueInFlight);
                    }
                    _nextAvailableApiTime = DateTime.Now.AddSeconds(5);
                    await Task.Delay(5000); // 5s cooldown to not overwhelm api
                }
                Debug.WriteLine("[Queue] Runner finished, queue empty.");
                _isApiQueueRunning = false;
            });
        }
    }

    private bool QueueApiRequestWithDebounce(string debounceKey, string description, Func<Task> action)
    {
        int effectiveCount = _apiQueue.Count + _pendingDebounces.Count;
        if (effectiveCount >= ApiQueueLimit)
        {
            Debug.WriteLine($"[Queue] FULL ({effectiveCount}/{ApiQueueLimit}), debounce rejected: \"{description}\"");
            ShowFullQueueBannerAsync();
            return false;
        }

        if (_debounceTokens.TryGetValue(debounceKey, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        cts = new CancellationTokenSource();
        _debounceTokens[debounceKey] = cts;
        var token = cts.Token;

        _pendingDebounces[debounceKey] = (description, action);
        Debug.WriteLine($"[Queue] Debounce registered: \"{description}\" (key: {debounceKey}) | effective size: {effectiveCount + 1}");

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000, token); // 1 second wait to check for misclicks
                if (!token.IsCancellationRequested)
                {
                    if (_pendingDebounces.TryRemove(debounceKey, out var pending))
                    {
                        EnqueueApiRequest(pending.Description, pending.Action);
                    }
                }
                else
                {
                    Debug.WriteLine($"[Queue] Debounce cancelled: \"{description}\"");
                }
            }
            catch (TaskCanceledException) { }
        });

        return true;
    }

    private void SetCommunitySkeletonsVisible(bool isVisible)
    {
        if (SkeletonLike != null) SkeletonLike.IsVisible = isVisible;
        if (SkeletonDislike != null) SkeletonDislike.IsVisible = isVisible;
        if (SkeletonComment != null) SkeletonComment.IsVisible = isVisible;

        if (TxtLikeCount != null) TxtLikeCount.IsVisible = !isVisible;
        if (TxtDislikeCount != null) TxtDislikeCount.IsVisible = !isVisible;
        if (TxtCommentCount != null) TxtCommentCount.IsVisible = !isVisible;
    }

    private static async Task<bool> CheckRealConnectivityAsync()
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 2000);
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private async void UpdateCommunityUIAsync(string levelId, bool isSql)
    {
        Debug.WriteLine("[Debug] Fetching level " + levelId);

        // reset comment section and active discussion, but keep the panel visible
        PnlCommentsSection.IsVisible = false;
        _currentActiveDiscussionId = -1;

        if (IconToggleComments != null)
            IconToggleComments.Path = "/assets/icons/ic_comment.svg";

        if (!AppSettings.IsCommunityFeaturesEnabled || string.IsNullOrEmpty(AppSettings.GithubToken) || _isCustomLevelMode || _discussionMappings == null)
        {
            // only hide the panel when community is genuinely unavailable
            PnlCommunityActions.IsVisible = false;
            return;
        }

        string modeKey = isSql ? "SQL" : "C#";
        if (!_discussionMappings.ContainsKey(modeKey) || !_discussionMappings[modeKey].ContainsKey(levelId))
            return; // no discussion mapped

        int discussionNum = _discussionMappings[modeKey][levelId];
        _currentActiveDiscussionId = discussionNum;

        var dict = isSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;

        // check cache first to prevent skeleton flashing
        if (dict.TryGetValue(levelId, out var cache) && (DateTime.Now - cache.LastFetched).TotalMinutes < 5)
        {
            PnlCommunityActions.IsVisible = true;
            SetCommunitySkeletonsVisible(false);
            ApplyCommunityUiData(cache);
            return; // skip ping and fetch, use fresh cache synchronously
        }

        // no valid cache -> show skeletons immediately and ping network
        PnlCommunityActions.IsVisible = true;
        SetCommunitySkeletonsVisible(true);

        bool isOnline = await CheckRealConnectivityAsync();

        if (!isOnline)
        {
            if (!_isKnownOffline)
            {
                // first time we notice being offline: show the banner once, then stay silent
                _isKnownOffline = true;
                await ShowOfflineBannerOnceAsync();
            }
            // already known offline: just do nothing, community stays hidden
            return;
        }

        // internet connection spotted
        _isKnownOffline = false;
        await FetchCommunityDataAsync(discussionNum, isSql, levelId, false);
    }

    private async Task FetchCommunityDataAsync(int discussionNumber, bool isSql, string levelId, bool fetchNextPage)
    {
        if (!await CheckRealConnectivityAsync())
        {
            if (!_isKnownOffline)
            {
                _isKnownOffline = true;
                await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
            }
            return;
        }

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
                repository(owner: ""aec-community-bot"", name: ""aec-community"") {
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
                            string commentAuthor = node.GetProperty("author").GetProperty("login").GetString();
                            string commentBody = node.GetProperty("body").GetString();
                            bool isBotComment = false;

                            // intercept bot messages and extract the real author
                            if (commentAuthor == "aec-community-bot")
                            {
                                isBotComment = true;
                                var match = System.Text.RegularExpressions.Regex.Match(commentBody, @"<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline);
                                if (match.Success)
                                {
                                    commentAuthor = match.Groups[1].Value;
                                    commentBody = match.Groups[2].Value;
                                }
                            }

                            int rawUpvotes = node.GetProperty("upvoteCount").GetInt32();

                            var newComment = new GithubComment
                            {
                                Id = node.GetProperty("id").GetString(),
                                Author = commentAuthor,
                                Body = commentBody,
                                CreatedAt = ConvertToGermanTime(node.GetProperty("createdAt").GetDateTime().ToUniversalTime()),
                                // deduct 1 upvote if posted by the bot to remove its own auto upvote (no ones gonna know)
                                Upvotes = isBotComment ? Math.Max(0, rawUpvotes - 1) : rawUpvotes,
                                ViewerHasUpvoted = node.GetProperty("viewerHasUpvoted").GetBoolean()
                            };

                            if (node.TryGetProperty("replies", out var repliesProp) && repliesProp.TryGetProperty("nodes", out var repNodes))
                            {
                                foreach (var rep in repNodes.EnumerateArray())
                                {
                                    string replyAuthor = rep.GetProperty("author").GetProperty("login").GetString();
                                    string replyBody = rep.GetProperty("body").GetString();

                                    // intercept bot messages for replies as well
                                    if (replyAuthor == "aec-community-bot")
                                    {
                                        var match = System.Text.RegularExpressions.Regex.Match(replyBody, @"<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline);
                                        if (match.Success)
                                        {
                                            replyAuthor = match.Groups[1].Value;
                                            replyBody = match.Groups[2].Value;
                                        }
                                    }

                                    newComment.Replies.Add(new GithubReply
                                    {
                                        Id = rep.GetProperty("id").GetString(),
                                        Author = replyAuthor,
                                        Body = replyBody,
                                        CreatedAt = ConvertToGermanTime(rep.GetProperty("createdAt").GetDateTime().ToUniversalTime()),
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
            if (!_isKnownOffline)
            {
                _isKnownOffline = true;
                await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
            }

            var dictLocal = isSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
            if (dictLocal.TryGetValue(levelId, out var cacheData) && string.IsNullOrEmpty(cacheData.DiscussionNodeId))
            {
                dictLocal.Remove(levelId);
            }
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                _isFetchingComments = false;
                TxtCommentsLoading.IsVisible = false;
                if (cache != null && cache.HasNextPage && PnlCommentsSection.IsVisible) BtnLoadMoreComments.IsVisible = true;
            });
        }
    }

    private DateTime ConvertToGermanTime(DateTime utcTime)
    {
        try
        {
            string tzId = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? "W. Europe Standard Time"
                : "Europe/Berlin";
            TimeZoneInfo cetZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, cetZone);
        }
        catch
        {
            return utcTime.ToLocalTime(); // fallback
        }
    }

    private void ApplyCommunityUiData(DiscussionCache cache)
    {
        SetCommunitySkeletonsVisible(false);

        // re-enable controls (in case hidden cuz offline before)
        BtnLike.IsEnabled = true;
        BtnDislike.IsEnabled = true;
        BtnToggleComments.IsEnabled = true;

        TxtLikeCount.Text = cache.Likes.ToString();
        TxtDislikeCount.Text = cache.Dislikes.ToString();
        TxtCommentCount.Text = cache.TotalComments.ToString();

        if (IconLike != null)
            IconLike.Path = cache.ViewerHasLiked ? "/assets/icons/ic_like_filled.svg" : "/assets/icons/ic_like.svg";

        if (IconDislike != null)
            IconDislike.Path = cache.ViewerHasDisliked ? "/assets/icons/ic_dislike_filled.svg" : "/assets/icons/ic_dislike.svg";

        // re-enable buttons in case they were disabled by previously being offline
        BtnLike.IsEnabled = true;
        BtnDislike.IsEnabled = true;
        BtnToggleComments.IsEnabled = true;

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

            Dispatcher.UIThread.InvokeAsync(InitializeNotificationPoller);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Failed to load mappings resource: {ex.Message}");
            _discussionMappings = new(); // prevent null refs
        }
    }

    private async void BtnToggleComments_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckRealConnectivityAsync())
        {
            if (!_isKnownOffline)
            {
                _isKnownOffline = true;
                await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
            }
            return;
        }

        PnlCommentsSection.IsVisible = !PnlCommentsSection.IsVisible;
        IconToggleComments.Path = PnlCommentsSection.IsVisible ? "/assets/icons/ic_comment_hide.svg" : "/assets/icons/ic_comment.svg";

        if (PnlCommentsSection.IsVisible && _currentActiveDiscussionId != -1)
        {
            RenderCachedComments();
        }
    }

    private async void BtnLoadMoreComments_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckRealConnectivityAsync())
        {
            if (!_isKnownOffline)
            {
                _isKnownOffline = true;
                await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
            }
            return;
        }

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

    private async void ToggleReaction(bool isLike)
    {
        if (!await CheckRealConnectivityAsync())
        {
            if (!_isKnownOffline)
            {
                _isKnownOffline = true;
                await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
            }
            return;
        }

        if (string.IsNullOrEmpty(AppSettings.GithubToken) || _currentActiveDiscussionId == -1) return;

        if (CheckAndHandlePermaBan()) return;

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

        QueueApiRequestWithDebounce($"level_react_{cache.DiscussionNodeId}", $"Reaction for {(_isSqlMode ? "SQL" : "C#")} level {levelId}", () => SyncReactionToGithubAsync(cache));
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
            string respBody = await resp.Content.ReadAsStringAsync();

            // detect github block (perma-ban)
            if (respBody.Contains("\"FORBIDDEN\"") && respBody.Contains("does not have the correct permissions"))
            {
                playerData.Settings.IsPermaBanned = true;
                SaveSystem.Save(playerData);
                await Dispatcher.UIThread.InvokeAsync(ShowPermaBanDialog);
            }
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

    private Control CreateCommentUI(GithubComment comment, string discussionId, bool isReply = false, Action<string> onTagUser = null)
    {
        var border = new Border
        {
            Background = SolidColorBrush.Parse(isReply ? "#141414" : "#1A1A1A"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(15),
            BorderBrush = SolidColorBrush.Parse("#333"),
            BorderThickness = new Thickness(1),
            Margin = isReply ? new Thickness(20, 0, 0, 0) : new Thickness(0, 5, 0, 0)
        };

        var mainStack = new StackPanel { Spacing = 8 };

        // remove zero-width space so mentions display normally in the ui
        string bodyToRender = comment.Body.Replace("@\u200B", "@");
        string activeTag = null;
        IBrush tagColor = Brushes.Gray;

        // dynamically highlight any mentions toward user
        bodyToRender = System.Text.RegularExpressions.Regex.Replace(bodyToRender, $@"(@{System.Text.RegularExpressions.Regex.Escape(AppSettings.GithubUsername)})", "__$1__");

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
            Content = LoadIcon("assets/icons/ic_more.svg", 16),
            Cursor = Cursor.Parse("Hand")
        };
        Grid.SetColumn(btnMore, 1);
        headerGrid.Children.Add(btnMore);

        Button btnEdit = null;
        Button btnDelete = null;

        var flyout = new Flyout();
        var flyoutStack = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(-5)
        };

        if (comment.Author == AppSettings.GithubUsername)
        {
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
                Cursor = Cursor.Parse("Hand")
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
                Cursor = Cursor.Parse("Hand")
            };

            if (!isReply) // allow subscribing only to comments
            {
                var isSubscribed = _communityCache.Subscriptions.ContainsKey(comment.Id);
                var btnSubscribe = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            LoadIcon(isSubscribed ? "assets/icons/ic_unsubscribe.svg" : "assets/icons/ic_subscribe.svg", 16),
                            new TextBlock
                            {
                                Text = isSubscribed ? "Deabonnieren" : "Abonnieren",
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    },
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 8),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursor.Parse("Hand")
                };

                btnSubscribe.Click += (s, e) =>
                {
                    if (isSubscribed)
                    {
                        _communityCache.Subscriptions.Remove(comment.Id);
                    }
                    else
                    {
                        _communityCache.Subscriptions.Add(comment.Id, comment.Replies.Count);
                    }

                    SaveSystem.SaveCommunityCache(_communityCache);
                    btnMore.Flyout?.Hide();

                    // update local ui without refreshing the whole section
                    isSubscribed = !isSubscribed;
                    var contentStack = (StackPanel)btnSubscribe.Content;
                    var iconContainer = contentStack.Children[0];
                    var textBlock = (TextBlock)contentStack.Children[1];

                    contentStack.Children[0] = LoadIcon(isSubscribed ? "assets/icons/ic_unsubscribe.svg" : "assets/icons/ic_subscribe.svg", 16);
                    textBlock.Text = isSubscribed ? "Deabonnieren" : "Abonnieren";
                };

                flyoutStack.Children.Add(btnSubscribe);
            }

            flyoutStack.Children.Add(btnEdit);
            flyoutStack.Children.Add(btnDelete);
        }
        else
        {
            if (isReply)
            {
                var btnTag = new Button
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            LoadIcon("assets/icons/ic_tag.svg", 16),
                            new TextBlock
                            {
                                Text = "Markieren",
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    },
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 8),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursor.Parse("Hand")
                };
                btnTag.Click += (s, e) =>
                {
                    btnMore.Flyout?.Hide();
                    onTagUser?.Invoke(comment.Author);
                };
                flyoutStack.Children.Add(btnTag);
            }

            var btnReport = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        LoadIcon("assets/icons/ic_report.svg", 16),
                        new TextBlock
                        {
                            Text = "Melden",
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
                Cursor = Cursor.Parse("Hand")
            };
            btnReport.Click += (s, e) =>
            {
                btnMore.Flyout?.Hide();
                ShowReportDialog(comment.Id, comment.Author, discussionId, comment.Body, comment.CreatedAt);
            };
            flyoutStack.Children.Add(btnReport);
        }

        flyout.Content = flyoutStack;
        btnMore.Flyout = flyout;

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
                Cursor = Cursor.Parse("Hand")
            };
            var spoilerContent = new StackPanel
            {
                IsVisible = false,
                Margin = new Thickness(0, 10, 0, 0)
            };

            MarkdownRenderer.RenderMarkdownToPanel(spoilerContent, bodyToRender, _isSqlMode, true);

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
            MarkdownRenderer.RenderMarkdownToPanel(bodyContainer, bodyToRender, _isSqlMode, true);
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
                Cursor = Cursor.Parse("Hand")
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
            Cursor = Cursor.Parse("Hand")
        };
        var btnSaveEdit = new Button
        {
            Content = "Speichern",
            Background = SolidColorBrush.Parse("#32A852"),
            Cursor = Cursor.Parse("Hand")
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
            Cursor = Cursor.Parse("Hand")
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

        btnUpvote.Click += async (s, e) =>
        {
            if (!await CheckRealConnectivityAsync())
            {
                if (!_isKnownOffline)
                {
                    _isKnownOffline = true;
                    await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
                }
                return;
            }

            if (CheckAndHandlePermaBan()) return;

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

            QueueApiRequestWithDebounce($"upvote_{comment.Id}",
                $"Upvote on comment by {comment.Author}",
                () => isReply ? ToggleReplyUpvoteAsync(comment.Id, targetState) : ToggleCommentUpvoteAsync(comment.Id, targetState));
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
                Cursor = Cursor.Parse("Hand")
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
                RowDefinitions = new RowDefinitions("*, Auto"),
                Margin = new Thickness(0, 10, 0, 0),
                IsVisible = false
            };
            const int ReplyCharLimit = 1000;

            var replyInputRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*, Auto")
            };

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
                Cursor = Cursor.Parse("Hand"),
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(8, 6),
                VerticalAlignment = VerticalAlignment.Bottom,
                IsEnabled = false,
                Opacity = 0.5
            };
            var iconSendReply = LoadIcon("assets/icons/ic_send_disabled.svg", 18);
            btnSendReply.Content = iconSendReply;

            var replyCooldownLabel = new TextBlock
            {
                Foreground = SolidColorBrush.Parse("#FF6B6B"),
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = false
            };
            CancellationTokenSource? replyCooldownCts = null;

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
                if (!await CheckRealConnectivityAsync())
                {
                    if (!_isKnownOffline)
                    {
                        _isKnownOffline = true;
                        await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
                    }
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtReply.Text)) return;

                // per reply 20s cooldown
                double secondsSinceLast = (DateTime.Now - _lastCommentTime).TotalSeconds;
                if (secondsSinceLast < 20)
                {
                    int waitTime = (int)(20 - secondsSinceLast);
                    replyCooldownCts?.Cancel();
                    replyCooldownCts = new CancellationTokenSource();
                    var rToken = replyCooldownCts.Token;
                    replyCooldownLabel.IsVisible = true;
                    try
                    {
                        for (int i = waitTime; i > 0; i--)
                        {
                            replyCooldownLabel.Text = $"Warte {i}s";
                            await Task.Delay(1000, rToken);
                        }
                        replyCooldownLabel.IsVisible = false;
                    }
                    catch (TaskCanceledException) { }
                    return;
                }

                btnSendReply.IsEnabled = false;
                btnSendReply.Opacity = 0.5;
                btnSendReply.Content = LoadIcon("assets/icons/ic_send_disabled.svg", 18);

                _lastCommentTime = DateTime.Now;

                // inject zero-width space to bypass githubs native mention system
                string replyText = System.Text.RegularExpressions.Regex.Replace(txtReply.Text, @"@([A-Za-z0-9-]+)", "@\u200B$1");

                Debug.WriteLine($"[Debug] Sent Reply to comment.Id=[{comment.Id}] in discussionId=[{discussionId}]...");
                bool replySent = await SendReplyToGithubAsync(discussionId, comment.Id, replyText);

                txtReply.Text = "";
                replyInputGrid.IsVisible = false;
                replyContent.Children[0] = LoadIcon("assets/icons/ic_comment_add.svg", 18);
                ToolTip.SetTip(btnToggleReply, "Antwort verfassen");

                if (replySent)
                {
                    // auto subscribe to the parent comment so the background poller can look for mentions in future replies
                    _communityCache.Subscriptions[comment.Id] = comment.Replies.Count + 1;
                    SaveSystem.SaveCommunityCache(_communityCache);

                    string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                    await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
                }
            };

            Action<string> localTagAction = (username) =>
            {
                replyInputGrid.IsVisible = true;
                string tag = $"@{username} ";

                int insertPos = txtReply.CaretIndex;
                if (insertPos < 0) insertPos = txtReply.Text?.Length ?? 0;

                string currentText = txtReply.Text ?? string.Empty;
                txtReply.Text = currentText.Insert(insertPos, tag);

                txtReply.Focus();
                txtReply.CaretIndex = insertPos + tag.Length;
            };

            Grid.SetColumn(btnSendReply, 1);
            replyInputRow.Children.Add(txtReply);
            replyInputRow.Children.Add(btnSendReply);

            Grid.SetRow(replyInputRow, 0);
            Grid.SetRow(replyCooldownLabel, 1);
            replyInputGrid.Children.Add(replyInputRow);
            replyInputGrid.Children.Add(replyCooldownLabel);

            actionsPanel.Children.Add(btnToggleReply);

            Border repliesContainer = null;
            if (comment.Replies.Count > 0)
            {
                var btnShowReplies = new Button
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(5),
                    Cursor = Cursor.Parse("Hand")
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
                    Padding = new Thickness(0, 0, 0, 0),
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
                    }, discussionId, true, localTagAction));
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
                if (!await CheckRealConnectivityAsync())
                {
                    if (!_isKnownOffline)
                    {
                        _isKnownOffline = true;
                        await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
                    }
                    return;
                }

                btnSaveEdit.IsEnabled = false;
                if (CheckAndHandlePermaBan())
                {
                    btnSaveEdit.IsEnabled = true;
                    return;
                }
                string newBody = txtEdit.Text;

                // optimistic ui update
                editGrid.IsVisible = false;
                bodyWrapper.IsVisible = true;
                actionsPanel.IsVisible = true;
                
                // prevent re-editing the old text before fetch finishes
                comment.Body = newBody; 

                EnqueueApiRequest("Kommentar bearbeiten", async () => 
                {
                    bool updated = await UpdateCommentOrReplyAsync(comment.Id, newBody);

                    await Dispatcher.UIThread.InvokeAsync(async () => 
                    {
                        if (updated)
                        {
                            string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                            await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
                        }
                        else
                        {
                            // if it failed, re-enable the edit button if user opens the edit view again
                            btnSaveEdit.IsEnabled = true; 
                        }
                    });
                });
            };

            btnDelete.Click += async (s, e) =>
            {
                if (!await CheckRealConnectivityAsync())
                {
                    if (!_isKnownOffline)
                    {
                        _isKnownOffline = true;
                        await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
                    }
                    return;
                }

                btnMore.Flyout?.Hide();
                btnDelete.IsEnabled = false;

                if (CheckAndHandlePermaBan())
                {
                    btnDelete.IsEnabled = true;
                    return;
                }

                // visually hide the comment instantly (optimistic)
                border.IsVisible = false;

                EnqueueApiRequest("Kommentar löschen", async () => 
                {
                    bool deleted = await DeleteCommentOrReplyAsync(comment.Id);

                    await Dispatcher.UIThread.InvokeAsync(async () => 
                    {
                        if (deleted)
                        {
                            string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                            await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
                        }
                        else
                        {
                            // if it failed, show it again and re-enable the button
                            border.IsVisible = true;
                            btnDelete.IsEnabled = true; 
                        }
                    });
                });
            };
        }

        border.Child = mainStack;

        if (isReply)
        {
            var replyWrapper = new Grid
            {
                Margin = new Thickness(0, 5, 0, 0)
            };

            // reddit curve pointing to username
            var branchLine = new Border
            {
                BorderBrush = SolidColorBrush.Parse("#333"),
                BorderThickness = new Thickness(2, 0, 0, 2),
                CornerRadius = new CornerRadius(0, 0, 0, 10),
                Width = 25,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-2, 0, 0, 0),
                ZIndex = -1
            };

            replyWrapper.Children.Add(branchLine);
            replyWrapper.Children.Add(border);

            return replyWrapper;
        }

        return border;
    }

    private async void BtnSendComment_Click(object sender, RoutedEventArgs e)
    {
        if (!await CheckRealConnectivityAsync())
        {
            if (!_isKnownOffline)
            {
                _isKnownOffline = true;
                await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtCommentInput.Text)) return;

        if (CheckAndHandlePermaBan()) return;

        // global 20s cooldown verification upon click
        double secondsSinceLastComment = (DateTime.Now - _lastCommentTime).TotalSeconds;
        if (secondsSinceLastComment < 20)
        {
            ShowCooldownMessage((int)(20 - secondsSinceLastComment));
            return;
        }

        BtnSendComment.IsEnabled = false;

        // inject tag dynamically at send time
        string fullBody = TxtCommentInput.Text;
        // inject zero-width space to bypass githubs native mention system
        fullBody = System.Text.RegularExpressions.Regex.Replace(fullBody, @"@([A-Za-z0-9-]+)", "@\u200B$1");

        string tagSelection = (CmbCommentTag.SelectedItem as ComboBoxItem)?.Content?.ToString();

        if (!string.IsNullOrEmpty(tagSelection) && tagSelection != "–")
        {
            // prepend the tag to the comment
            fullBody = $"!{tagSelection.ToUpper()};\n{fullBody}";
        }

        string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        if (dict.TryGetValue(levelId, out var cache))
        {
            // in case the node id didnt cache properly
            if (string.IsNullOrEmpty(cache.DiscussionNodeId))
            {
                await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
            }

            _lastCommentTime = DateTime.Now;

            // send payload to cloudflare worker (instead of github directly)
            var payload = new
            {
                discussionId = cache.DiscussionNodeId,
                body = fullBody
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

                // route to the proxy worker
                var resp = await _httpClient.PostAsync("https://aec-community-proxy.theactualcooker.workers.dev", content);
                string resBody = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED")
                {
                    BtnSendComment.IsEnabled = true;
                    await Dispatcher.UIThread.InvokeAsync(ShowBanDialog);
                    return;
                }

                if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
                {
                    // auto subscribe if question
                    if (tagSelection == "Frage")
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(resBody);
                            var id = doc.RootElement.GetProperty("data").GetProperty("addDiscussionComment").GetProperty("comment").GetProperty("id").GetString();
                            if (!string.IsNullOrEmpty(id))
                            {
                                _communityCache.Subscriptions.Add(id, 0);
                                SaveSystem.SaveCommunityCache(_communityCache);
                            }
                        }
                        catch { }
                    }

                    TxtCommentInput.Text = "";
                    CmbCommentTag.SelectedIndex = 0; // reset tag selection upon success

                    // refetch immediately to show the new comment
                    await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
                }
                else
                {
                    Debug.WriteLine($"Worker Proxy Error: {resBody}");
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

    private async Task<bool> SendReplyToGithubAsync(string discussionNodeId, string commentNodeId, string body)
    {
        if (CheckAndHandlePermaBan()) return false;

        // send payload to cloudflare worker (instead of github directly)
        var payload = new
        {
            discussionId = discussionNodeId,
            replyToId = commentNodeId,
            body = body
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

            // route to the proxy worker
            var resp = await _httpClient.PostAsync("https://aec-community-proxy.theactualcooker.workers.dev", content);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED")
            {
                await Dispatcher.UIThread.InvokeAsync(ShowBanDialog);
                return false;
            }

            if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
            {
                return true;
            }
            else
            {
                Debug.WriteLine($"Worker Proxy Reply Error: {resBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Reply Submit Error: {ex.Message}");
            return false;
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
            string respBody = await resp.Content.ReadAsStringAsync();

            // detect github block (perma-ban)
            if (respBody.Contains("\"FORBIDDEN\"") && respBody.Contains("does not have the correct permissions"))
            {
                playerData.Settings.IsPermaBanned = true;
                SaveSystem.Save(playerData);
                await Dispatcher.UIThread.InvokeAsync(ShowPermaBanDialog);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toggle Upvote Error: {ex.Message}");
        }
    }

    private async Task<bool> UpdateCommentOrReplyAsync(string id, string body)
    {
        var payload = new
        {
            commentId = id,
            body = body
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

            // route to the proxy worker via put
            var resp = await _httpClient.PutAsync("https://aec-community-proxy.theactualcooker.workers.dev", content);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED")
            {
                await Dispatcher.UIThread.InvokeAsync(ShowBanDialog);
                return false;
            }

            if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
            {
                return true;
            }
            else
            {
                Debug.WriteLine($"[Community] Worker Proxy Update Error: {resBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Update Comment Error: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> DeleteCommentOrReplyAsync(string id)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

            // route to the proxy worker via delete
            var resp = await _httpClient.DeleteAsync($"https://aec-community-proxy.theactualcooker.workers.dev?commentId={id}");
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED")
            {
                await Dispatcher.UIThread.InvokeAsync(ShowBanDialog);
                return false;
            }

            if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
            {
                return true;
            }
            else
            {
                Debug.WriteLine($"[Community] Worker Proxy Delete Error: {resBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Delete Comment Error: {ex.Message}");
            return false;
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
            string respBody = await resp.Content.ReadAsStringAsync();

            Debug.WriteLine("[Community] Reply-Upvote body: " + respBody);

            // detect github block (perma-ban)
            if ((!resp.IsSuccessStatusCode || respBody.Contains("\"FORBIDDEN\"")) && respBody.Contains("blocked"))
            {
                playerData.Settings.IsPermaBanned = true;
                SaveSystem.Save(playerData);
                await Dispatcher.UIThread.InvokeAsync(ShowPermaBanDialog);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Community] Toggle Reply Upvote Error: {ex.Message}");
        }
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

    private async void ShowApiQueueDialog()
    {
        var dialog = new Window
        {
            Title = "GitHub Sync im Hintergrund",
            Width = 500,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#202124"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto"),
            Margin = new Thickness(20)
        };

        var headerStack = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(0, 0, 0, 15)
        };
        headerStack.Children.Add(new TextBlock
        {
            Text = "Synchronisiere mit GitHub...",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#6495ED")
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = "Bitte warten, bis alle Community-Aktionen hochgeladen wurden, um Datenverlust zu vermeiden.",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        });
        rootGrid.Children.Add(headerStack);

        var queueListPanel = new StackPanel { Spacing = 8 };
        var scrollViewer = new ScrollViewer
        {
            Content = queueListPanel,
            Padding = new Thickness(10)
        };
        var scrollBorder = new Border
        {
            Child = scrollViewer,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            CornerRadius = new CornerRadius(6),
            BorderBrush = SolidColorBrush.Parse("#333"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        Grid.SetRow(scrollBorder, 1);
        rootGrid.Children.Add(scrollBorder);

        var footerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *"),
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(footerGrid, 2);

        var txtTotalTime = new TextBlock
        {
            Foreground = Brushes.Orange,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        footerGrid.Children.Add(txtTotalTime);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(btnPanel, 1);

        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnForceClose = new Button
        {
            Content = "Trotzdem schließen",
            Background = SolidColorBrush.Parse("#B43232"),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };

        btnCancel.Click += (_, __) => dialog.Close();
        btnForceClose.Click += (_, __) =>
        {
            _isForceClosing = true;
            dialog.Close();
            Close();
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnForceClose);
        footerGrid.Children.Add(btnPanel);

        rootGrid.Children.Add(footerGrid);
        dialog.Content = rootGrid;

        // snapshots updated every 500ms from the real queue
        List<string> _snapDescriptions = new();
        double _snapFirstCooldown = 0;
        DateTime _snapTakenAt = DateTime.Now;
        double _totalTimeAtSnapshot = 0;
        DateTime _totalTimeSnapTakenAt = DateTime.Now;

        void TakeSnapshot()
        {
            var queue = GetApiQueueSnapshot();
            _snapDescriptions = queue;
            _snapFirstCooldown = Math.Max(0, (GetNextAvailableApiTime() - DateTime.Now).TotalSeconds);
            _snapTakenAt = DateTime.Now;

            double total = _snapFirstCooldown + Math.Max(0, queue.Count - 1) * 5.0;
            _totalTimeAtSnapshot = total;
            _totalTimeSnapTakenAt = DateTime.Now;
        }

        // 500ms state sync timer
        var syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        syncTimer.Tick += (s, ev) =>
        {
            var queue = GetApiQueueSnapshot();
            if (queue.Count == 0 && _apiQueueInFlight == 0)
            {
                syncTimer.Stop();
                _isForceClosing = true;
                dialog.Close();
                Close();
                return;
            }
            TakeSnapshot();
        };

        var smoothTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        smoothTimer.Tick += (s, ev) =>
        {
            if (_snapDescriptions.Count == 0) return;

            double elapsed = (DateTime.Now - _snapTakenAt).TotalSeconds;

            // rebuild list rows with interpolated per item cooldowns
            queueListPanel.Children.Clear();
            for (int i = 0; i < _snapDescriptions.Count; i++)
            {
                double baseCooldown = (i == 0) ? _snapFirstCooldown : 5.0;
                double displayed = Math.Max(0, baseCooldown - (i == 0 ? elapsed : 0));

                var itemStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10
                };
                var txtCountdown = new TextBlock
                {
                    Text = $"{displayed:F1}s",
                    Foreground = displayed < 1.5 ? Brushes.OrangeRed : Brushes.Gray,
                    Width = 45,
                    FontFamily = new FontFamily(MonospaceFontFamily)
                };
                var txtDesc = new TextBlock
                {
                    Text = $"– {_snapDescriptions[i]}",
                    Foreground = Brushes.White
                };
                itemStack.Children.Add(txtCountdown);
                itemStack.Children.Add(txtDesc);
                queueListPanel.Children.Add(itemStack);
            }
        };

        var totalTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        totalTimeTimer.Tick += (s, ev) =>
        {
            double elapsedSinceSnap = (DateTime.Now - _totalTimeSnapTakenAt).TotalSeconds;
            double displayed = Math.Max(0, _totalTimeAtSnapshot - elapsedSinceSnap);
            txtTotalTime.Text = $"Gesamte Restzeit: ~{Math.Ceiling(displayed)}s";
        };

        TakeSnapshot(); // initial snapshot before timers start
        syncTimer.Start();
        smoothTimer.Start();
        totalTimeTimer.Start();

        dialog.Closed += (s, ev) =>
        {
            syncTimer.Stop();
            smoothTimer.Stop();
            totalTimeTimer.Stop();
        };

        await dialog.ShowDialog(this);
    }

    private async void BtnCommunityLoggedOutStatus_Click(object sender, RoutedEventArgs e)
    {
        await OpenSettingsWindow(true);
    }

    private async void ShowCommunityHint()
    {
        if (playerData.Settings.CommunityHintShown || !string.IsNullOrEmpty(AppSettings.GithubToken)) return;

        playerData.Settings.CommunityHintShown = true;
        SaveSystem.Save(playerData);

        await Task.Delay(500);

        PnlCommunityActions.IsVisible = true;
        BtnCommunityLoggedOutStatus.IsVisible = true;

        // force skeletons to show for the hint effect
        SkeletonLike.IsVisible = true;
        SkeletonDislike.IsVisible = true;
        SkeletonComment.IsVisible = true;

        var txtLikeCount = this.FindControl<TextBlock>("TxtLikeCount");
        if (txtLikeCount != null) txtLikeCount.IsVisible = false;

        var txtDislikeCount = this.FindControl<TextBlock>("TxtDislikeCount");
        if (txtDislikeCount != null) txtDislikeCount.IsVisible = false;

        var txtCommentCount = this.FindControl<TextBlock>("TxtCommentCount");
        if (txtCommentCount != null) txtCommentCount.IsVisible = false;

        // show for 6 seconds, pause while hovered
        int elapsed = 0;
        while (elapsed < 6000)
        {
            await Task.Delay(100);

            // exit early if user logged in during the hint window
            if (!string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                BtnCommunityLoggedOutStatus.IsVisible = false;
                return;
            }

            if (BtnCommunityLoggedOutStatus.IsPointerOver)
            {
                continue; // pause the timer
            }

            elapsed += 100;
        }

        // hide the button automatically after timer finishes
        BtnCommunityLoggedOutStatus.IsVisible = false;

        // restore normal community ui state based on current login/settings
        string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        UpdateCommunityUIAsync(levelId, _isSqlMode);
    }

    public void RefreshCommunityUI()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateInboxUI();
            if (!string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                if (BtnCommunityLoggedOutStatus != null)
                    BtnCommunityLoggedOutStatus.IsVisible = false;
            }
            string levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
            UpdateCommunityUIAsync(levelId, _isSqlMode);
        });
    }

    private void BtnTabComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender == BtnTabEdit)
        {
            BtnTabEdit.Background = SolidColorBrush.Parse("#202124");
            BtnTabEdit.Foreground = SolidColorBrush.Parse("#32A852");
            BtnTabEdit.FontWeight = FontWeight.SemiBold;

            BtnTabPreview.Background = Brushes.Transparent;
            BtnTabPreview.Foreground = SolidColorBrush.Parse("#CCCCCC");
            BtnTabPreview.FontWeight = FontWeight.Normal;

            TxtCommentInput.IsVisible = true;
            PnlCommentPreview.IsVisible = false;
            BtnMarkdownToolbar.IsVisible = true;
        }
        else
        {
            BtnTabPreview.Background = SolidColorBrush.Parse("#202124");
            BtnTabPreview.Foreground = SolidColorBrush.Parse("#32A852");
            BtnTabPreview.FontWeight = FontWeight.SemiBold;

            BtnTabEdit.Background = Brushes.Transparent;
            BtnTabEdit.Foreground = SolidColorBrush.Parse("#CCCCCC");
            BtnTabEdit.FontWeight = FontWeight.Normal;

            TxtCommentInput.IsVisible = false;
            PnlCommentPreview.IsVisible = true;
            BtnMarkdownToolbar.IsVisible = false;

            PnlCommentPreviewContent.Children.Clear();
            if (string.IsNullOrWhiteSpace(TxtCommentInput.Text))
            {
                PnlCommentPreviewContent.Children.Add(new TextBlock
                {
                    Text = "Nichts zum Anzeigen.",
                    Foreground = Brushes.Gray,
                    FontStyle = Avalonia.Media.FontStyle.Italic
                });
            }
            else
            {
                MarkdownRenderer.RenderMarkdownToPanel(PnlCommentPreviewContent, TxtCommentInput.Text, _isSqlMode, true);
            }
        }
    }

    private void BtnInsertMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string format)
        {
            string text = TxtCommentInput.Text ?? "";
            int start = TxtCommentInput.SelectionStart;
            int end = TxtCommentInput.SelectionEnd;
            string sel = text.Substring(start, end - start);

            switch (format)
            {
                case "Heading":
                    {
                        bool needsNewLine = start > 0 && text[start - 1] != '\n';
                        string prefix = needsNewLine ? "\n" : "";
                        int offset = needsNewLine ? 1 : 0;

                        TxtCommentInput.Text = text.Insert(start, $"{prefix}## ");
                        TxtCommentInput.SelectionStart = start + 3 + offset;
                        TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                        break;
                    }
                case "Bold":
                    TxtCommentInput.Text = text.Remove(start, end - start).Insert(start, $"**{sel}**");
                    TxtCommentInput.SelectionStart = end == start ? start + 2 : start + sel.Length + 4;
                    TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                    break;
                case "Italic":
                    TxtCommentInput.Text = text.Remove(start, end - start).Insert(start, $"_{sel}_");
                    TxtCommentInput.SelectionStart = end == start ? start + 1 : start + sel.Length + 2;
                    TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                    break;
                case "InlineCode":
                    TxtCommentInput.Text = text.Remove(start, end - start).Insert(start, $"`{sel}`");
                    TxtCommentInput.SelectionStart = end == start ? start + 1 : start + sel.Length + 2;
                    TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                    break;
                case "CodeBlock":
                    {
                        bool needsNewLine = start > 0 && text[start - 1] != '\n';
                        string prefix = needsNewLine ? "\n" : "";
                        int offset = needsNewLine ? 1 : 0;

                        string block = $"{prefix}```csharp\n{sel}\n```";
                        TxtCommentInput.Text = text.Remove(start, end - start).Insert(start, block);
                        TxtCommentInput.SelectionStart = end == start ? start + 10 + offset : start + block.Length;
                        TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                        break;
                    }
                case "List":
                    {
                        bool needsNewLine = start > 0 && text[start - 1] != '\n';
                        string prefix = needsNewLine ? "\n" : "";
                        int offset = needsNewLine ? 1 : 0;

                        TxtCommentInput.Text = text.Insert(start, $"{prefix}- ");
                        TxtCommentInput.SelectionStart = start + 2 + offset;
                        TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                        break;
                    }
            }

            BtnMarkdownToolbar.Flyout?.Hide();
            TxtCommentInput.Focus();
        }
    }

    private void InitializeNotificationPoller()
    {
        _notificationPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _notificationPollTimer.Tick += (s, e) =>
        {
            if (AppSettings.IsCommunityFeaturesEnabled && !string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                // only poll if the queue is completely idle
                if (_apiQueue.Count == 0 && _apiQueueInFlight == 0)
                {
                    EnqueueApiRequest("Nach neuen Benachrichtigungen suchen", PollNotificationsAsync);
                }
            }
        };
        _notificationPollTimer.Start();

        // trigger initial poll shortly after startup
        Task.Run(async () =>
        {
            await Task.Delay(5000);
            if (AppSettings.IsCommunityFeaturesEnabled && !string.IsNullOrEmpty(AppSettings.GithubToken))
            {
                if (_apiQueue.Count == 0 && _apiQueueInFlight == 0)
                {
                    EnqueueApiRequest("Nach neuen Benachrichtigungen suchen", PollNotificationsAsync);
                }
            }
        });
    }

    private async Task PollNotificationsAsync()
    {
        if (!await CheckRealConnectivityAsync()) return;
        if (playerData.Settings.AreNotificationsPaused) return;

        bool hasNewNotifications = false;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

        // check graphql api for subscribed comment replies and bootleg mentions
        if (_communityCache.Subscriptions.Count > 0)
        {
            try
            {
                var allIds = _communityCache.Subscriptions.Keys.ToList();
                int chunkSize = 50; // chunk to prevent graphql complexity limits for heavy users

                for (int i = 0; i < allIds.Count; i += chunkSize)
                {
                    var ids = allIds.Skip(i).Take(chunkSize).ToList();
                    var queryObj = new
                    {
                        query = @"query($ids: [ID!]!) {
                            nodes(ids: $ids) {
                                ... on DiscussionComment {
                                    id
                                    author { login }
                                    replies(last: 5) {
                                        totalCount
                                        nodes { id author { login } body }
                                    }
                                }
                            }
                        }",
                        variables = new { ids = ids }
                    };

                    var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
                    var graphqlResp = await _httpClient.PostAsync("https://api.github.com/graphql", content);

                    if (graphqlResp.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(await graphqlResp.Content.ReadAsStringAsync());
                        if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("nodes", out var nodes))
                        {
                            foreach (var node in nodes.EnumerateArray())
                            {
                                if (node.ValueKind == JsonValueKind.Null) continue;

                                string commentId = node.GetProperty("id").GetString();
                                string parentAuthor = node.GetProperty("author").GetProperty("login").GetString();
                                var repliesData = node.GetProperty("replies");
                                int newTotalCount = repliesData.GetProperty("totalCount").GetInt32();

                                if (_communityCache.Subscriptions.TryGetValue(commentId, out int lastKnownCount))
                                {
                                    if (newTotalCount > lastKnownCount)
                                    {
                                        int diff = newTotalCount - lastKnownCount;
                                        var replyNodes = repliesData.GetProperty("nodes").EnumerateArray().ToList();

                                        // only evaluate the newly added replies
                                        var newReplies = replyNodes.Skip(Math.Max(0, replyNodes.Count - diff)).ToList();

                                        foreach (var replyNode in newReplies)
                                        {
                                            string replyAuthor = replyNode.GetProperty("author").GetProperty("login").GetString();
                                            if (replyAuthor == AppSettings.GithubUsername) continue;

                                            string replyBody = replyNode.GetProperty("body").GetString();

                                            string foundDiscId = null;
                                            foreach (var cache in _communityCache.CsharpDiscussions.Values.Concat(_communityCache.SqlDiscussions.Values))
                                            {
                                                if (cache.Comments.Any(c => c.Id == commentId || c.Replies.Any(r => r.Id == commentId)))
                                                {
                                                    foundDiscId = cache.DiscussionNodeId;
                                                    break;
                                                }
                                            }

                                            // check for our injected zero-width space mention or a normal text mention fallback
                                            bool isMentioned = replyBody.Contains($"@\u200B{AppSettings.GithubUsername}") ||
                                                               replyBody.Contains($"@{AppSettings.GithubUsername}");

                                            if (isMentioned)
                                            {
                                                _communityCache.Notifications.Add(new AppNotification
                                                {
                                                    Message = $"{replyAuthor} hat dich in einer Antwort erwähnt (@).",
                                                    Date = DateTime.Now,
                                                    IsRead = false,
                                                    TargetDiscussionId = foundDiscId,
                                                    TargetCommentId = commentId
                                                });
                                                hasNewNotifications = true;
                                            }
                                            else if (parentAuthor == AppSettings.GithubUsername)
                                            {
                                                // only trigger standard reply notification if they explicitly own the parent comment
                                                _communityCache.Notifications.Add(new AppNotification
                                                {
                                                    Message = $"{replyAuthor} hat auf deinen abonnierten Kommentar geantwortet.",
                                                    Date = DateTime.Now,
                                                    IsRead = false,
                                                    TargetDiscussionId = foundDiscId,
                                                    TargetCommentId = commentId
                                                });
                                                hasNewNotifications = true;
                                            }
                                        }

                                        _communityCache.Subscriptions[commentId] = newTotalCount;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Community] Polling Subscriptions Error: {ex.Message}");
            }
        }

        if (hasNewNotifications)
        {
            SaveSystem.SaveCommunityCache(_communityCache);
            await Dispatcher.UIThread.InvokeAsync(UpdateInboxUI);
        }
    }

    private void UpdateInboxUI()
    {
        bool isCommunityEnabled = AppSettings.IsCommunityFeaturesEnabled;
        bool isLoggedIn = !string.IsNullOrEmpty(AppSettings.GithubToken);

        if (BtnInbox != null)
        {
            BtnInbox.IsVisible = isCommunityEnabled && isLoggedIn;

            if (BtnInbox.IsVisible)
            {
                int unreadCount = _communityCache.Notifications.Count(n => !n.IsRead);

                // hide indicator badge if notifications are paused
                if (BadgeInbox != null)
                    BadgeInbox.IsVisible = !playerData.Settings.AreNotificationsPaused && unreadCount > 0;

                var icon = (!playerData.Settings.AreNotificationsPaused && unreadCount > 0) ? "ic_inbox_filled.svg" : "ic_inbox.svg";
                BtnInbox.Content = LoadIcon($"assets/icons/{icon}", 20);
            }
        }
    }

    private void BtnInbox_Click(object sender, RoutedEventArgs e)
    {
        // mark all as read
        bool changed = false;
        foreach (var n in _communityCache.Notifications)
        {
            if (!n.IsRead)
            {
                n.IsRead = true;
                changed = true;
            }
        }

        if (changed)
        {
            SaveSystem.SaveCommunityCache(_communityCache);
            UpdateInboxUI();
        }

        ShowInboxFlyout();
    }

    private void ShowInboxFlyout()
    {
        if (BtnInbox == null) return;

        var rootStack = new StackPanel
        {
            Spacing = 10,
            Width = 380
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *")
        };

        bool isPaused = playerData.Settings.AreNotificationsPaused;
        string activeColor = isPaused ? "#d44c0d" : "#32A852";

        var btnToggleNotis = new Button
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(btnToggleNotis, isPaused ? "Benachrichtigungen aktivieren" : "Benachrichtigungen pausieren");

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(4)
        };
        titleStack.Children.Add(LoadIcon(isPaused ? "assets/icons/ic_notis_paused.svg" : "assets/icons/ic_notis_active.svg", 20));
        titleStack.Children.Add(new TextBlock
        {
            Text = "Posteingang",
            FontSize = 18,
            Foreground = SolidColorBrush.Parse(activeColor),
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });
        btnToggleNotis.Content = titleStack;

        btnToggleNotis.Click += (s, e) =>
        {
            playerData.Settings.AreNotificationsPaused = !playerData.Settings.AreNotificationsPaused;
            SaveSystem.Save(playerData);
            UpdateInboxUI();
            ShowInboxFlyout();
        };

        Grid.SetColumn(btnToggleNotis, 0);
        headerGrid.Children.Add(btnToggleNotis);

        var headerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(headerActions, 1);

        var btnRefresh = new Button
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(btnRefresh, "Aktualisieren");

        double secondsSinceLastRefresh = (DateTime.Now - _lastNotificationRefreshTime).TotalSeconds;

        // visual cooldown loop
        async void StartCooldownUI(int startElapsed)
        {
            btnRefresh.IsEnabled = false;
            btnRefresh.Opacity = 0.5;

            string[] steps = { "12-5", "25", "37-5", "62-5", "75", "87-5" };
            for (int i = startElapsed; i < 6; i++)
            {
                btnRefresh.Content = LoadIcon($"assets/icons/ic_clock_loader_{steps[i]}.svg", 18);
                Debug.WriteLine("[Debug] Looped: " + i + "; Showing: " + steps[i]);
                await Task.Delay(1667);
            }

            btnRefresh.Content = LoadIcon("assets/icons/ic_refresh.svg", 18);
            btnRefresh.IsEnabled = true;
            btnRefresh.Opacity = 1.0;
        }

        if (secondsSinceLastRefresh < 10)
        {
            int frame = (int)(secondsSinceLastRefresh / (10.0 / 6.0));
            StartCooldownUI(frame);
        }
        else
        {
            btnRefresh.Content = LoadIcon("assets/icons/ic_refresh.svg", 18);
        }

        btnRefresh.Click += (s, e) =>
        {
            if ((DateTime.Now - _lastNotificationRefreshTime).TotalSeconds < 10) return; // 10s cooldown

            _lastNotificationRefreshTime = DateTime.Now;
            StartCooldownUI(0);

            EnqueueApiRequest("Manuelle Benachrichtigungsprüfung", async () =>
            {
                await PollNotificationsAsync();
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (_inboxFlyout != null && _inboxFlyout.IsOpen)
                        ShowInboxFlyout();
                });
            });
        };

        var btnUnsubscribeAll = new Button
        {
            Content = LoadIcon("assets/icons/ic_unsubscribe.svg", 18),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(btnUnsubscribeAll, "Alle deabonnieren");

        var btnDeleteAll = new Button
        {
            Content = LoadIcon("assets/icons/ic_delete_all.svg", 18),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(btnDeleteAll, "Alle Benachrichtigungen löschen");

        var btnSupport = new Button
        {
            Content = LoadIcon("assets/icons/ic_support.svg", 18),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(btnSupport, "Support & Hilfe");

        btnDeleteAll.Click += (s, e) =>
        {
            _communityCache.Notifications.Clear();
            SaveSystem.SaveCommunityCache(_communityCache);
            ShowInboxFlyout(); // re-render
        };

        btnUnsubscribeAll.Click += (s, e) =>
        {
            _communityCache.Subscriptions.Clear();
            SaveSystem.SaveCommunityCache(_communityCache);
        };

        btnSupport.Click += (s, e) =>
        {
            _inboxFlyout?.Hide();
            ShowSupportDialog();
        };

        headerActions.Children.Add(btnRefresh);
        headerActions.Children.Add(btnUnsubscribeAll);
        headerActions.Children.Add(btnDeleteAll);
        headerActions.Children.Add(btnSupport);
        headerGrid.Children.Add(headerActions);
        rootStack.Children.Add(headerGrid);

        // notifications list
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var listStack = new StackPanel { Spacing = 8 };

        if (_communityCache.Notifications.Count == 0)
        {
            listStack.Children.Add(new TextBlock
            {
                Text = "Keine Benachrichtigungen.",
                Foreground = Brushes.Gray,
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            });
        }
        else
        {
            foreach (var notif in _communityCache.Notifications.OrderByDescending(n => n.Date))
            {
                // wrap notification in its own contained styled border
                var itemBorder = new Border
                {
                    Background = SolidColorBrush.Parse("#1A1A1A"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    BorderBrush = SolidColorBrush.Parse("#333"),
                    BorderThickness = new Thickness(1)
                };

                var itemGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*, Auto")
                };

                var infoStack = new StackPanel { Spacing = 4 };
                infoStack.Children.Add(new TextBlock
                {
                    Text = notif.Message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White
                });
                infoStack.Children.Add(new TextBlock
                {
                    Text = notif.Date.ToString("dd.MM.yyyy HH:mm"),
                    FontSize = 11,
                    Foreground = Brushes.Gray
                });

                Grid.SetColumn(infoStack, 0);
                itemGrid.Children.Add(infoStack);

                var actionsStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var btnGo = new Button
                {
                    Content = LoadIcon("assets/icons/ic_comment_go.svg", 16),
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                var btnDel = new Button
                {
                    Content = LoadIcon("assets/icons/ic_delete.svg", 16),
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                btnGo.Click += (s, e) => 
                {
                    _inboxFlyout?.Hide();
                    if (!string.IsNullOrEmpty(notif.TargetDiscussionId))
                    {
                        string targetLevelId = null;
                        bool isTargetSql = false;

                        foreach (var kvp in _communityCache.CsharpDiscussions)
                        {
                            if (kvp.Value.DiscussionNodeId == notif.TargetDiscussionId)
                            {
                                targetLevelId = kvp.Key;
                                isTargetSql = false;
                                break;
                            }
                        }
                        if (targetLevelId == null)
                        {
                            foreach (var kvp in _communityCache.SqlDiscussions)
                            {
                                if (kvp.Value.DiscussionNodeId == notif.TargetDiscussionId)
                                {
                                    targetLevelId = kvp.Key;
                                    isTargetSql = true;
                                    break;
                                }
                            }
                        }

                        if (targetLevelId != null)
                        {
                            _isSqlMode = isTargetSql;

                            // switch the actual open level view inside the editor
                            if (isTargetSql)
                            {
                                if (sqlLevels == null) sqlLevels = SqlCurriculum.GetLevels();
                                var lvl = sqlLevels.FirstOrDefault(l => l.Id.ToString() == targetLevelId);
                                if (lvl != null) LoadSqlLevel(lvl);
                            }
                            else
                            {
                                if (levels == null) levels = Curriculum.GetLevels();
                                var lvl = levels.FirstOrDefault(l => l.Id.ToString() == targetLevelId);
                                if (lvl != null) LoadLevel(lvl);
                            }

                            UpdateCommunityUIAsync(targetLevelId, isTargetSql);
                            PnlCommentsSection.IsVisible = true;
                            RenderCachedComments();
                        }
                    }
                };

                btnDel.Click += (s, e) =>
                {
                    _communityCache.Notifications.Remove(notif);
                    SaveSystem.SaveCommunityCache(_communityCache);
                    ShowInboxFlyout();
                };

                // only show jump button if context data was retrieved
                if (!string.IsNullOrEmpty(notif.TargetDiscussionId)) actionsStack.Children.Add(btnGo);
                actionsStack.Children.Add(btnDel);

                Grid.SetColumn(actionsStack, 1);
                itemGrid.Children.Add(actionsStack);

                itemBorder.Child = itemGrid;
                listStack.Children.Add(itemBorder);
            }
        }

        scrollViewer.Content = listStack;
        rootStack.Children.Add(scrollViewer);

        if (_inboxFlyout == null) _inboxFlyout = new Flyout();
        _inboxFlyout.Content = rootStack;
        BtnInbox.Flyout = _inboxFlyout;
        _inboxFlyout.ShowAt(BtnInbox);
    }

    private async void ShowSupportDialog()
    {
        var dialog = new Window
        {
            Title = "Support",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) =>
        {
            if (ev.Key == Key.Escape) dialog.Close();
        };

        var stack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(new TextBlock
        {
            Text = "Support & Hilfe",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Sende mir eine Nachricht bzgl. Community-Features, Fehlern oder anderen Problemen.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var txtMessage = new TextBox
        {
            Watermark = "Deine Nachricht...",
            AcceptsReturn = true,
            MaxHeight = 100,
            Height = 80,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = Brushes.White,
            BorderBrush = SolidColorBrush.Parse("#333"),
            CornerRadius = new CornerRadius(4)
        };
        stack.Children.Add(txtMessage);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C")
        };

        // global 30s formspree cooldown check
        double secondsSinceLast = (DateTime.Now - DateTime.FromOADate(playerData.Settings.LastFormspreeTime)).TotalSeconds;

        var btnSend = new Button
        {
            Content = secondsSinceLast < 30 ? $"Warte {(int)(30 - secondsSinceLast)}s" : "Senden",
            Background = SolidColorBrush.Parse("#007ACC"),
            IsEnabled = secondsSinceLast >= 30
        };

        btnCancel.Click += (s, e) => dialog.Close();
        btnSend.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;
            btnSend.IsEnabled = false;
            btnSend.Content = "Sende...";

            // update cooldown timestamp
            playerData.Settings.LastFormspreeTime = DateTime.Now.ToOADate();
            SaveSystem.Save(playerData);

            var payload = new
            {
                type = "Support Request",
                user = AppSettings.GithubUsername,
                message = txtMessage.Text
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                string fId = "do" + "lzbl"; // very secure stuff right here
                await _httpClient.PostAsync($"https://formspree.io/f/mz{fId}", content);
            }
            catch { }

            dialog.Close();
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnSend);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private async void ShowReportDialog(string targetId, string author, string discussionId, string body, DateTime createdAt)
    {
        var dialog = new Window
        {
            Title = "Melden",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };
        dialog.KeyDown += (s, ev) =>
        {
            if (ev.Key == Key.Escape)
                dialog.Close();
        };

        var stack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(new TextBlock
        {
            Text = $"Kommentar von {author} melden",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.Red
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Möchtest du diesen Kommentar wirklich melden? Ein Missbrauch dieser Funktion kann zum Ausschluss führen.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var txtReason = new TextBox
        {
            Watermark = "Grund (optional)...",
            AcceptsReturn = true,
            MaxHeight = 60,
            Height = 60,
            Background = SolidColorBrush.Parse("#1A1A1A"),
            Foreground = Brushes.White,
            BorderBrush = SolidColorBrush.Parse("#333"),
            CornerRadius = new CornerRadius(4)
        };
        stack.Children.Add(txtReason);

        var txtError = new TextBlock
        {
            Foreground = SolidColorBrush.Parse("#FF5555"),
            FontWeight = FontWeight.SemiBold,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = SolidColorBrush.Parse("#3C3C3C")
        };

        // global 30s formspree cooldown check
        double secondsSinceLast = (DateTime.Now - DateTime.FromOADate(playerData.Settings.LastFormspreeTime)).TotalSeconds;

        var btnReport = new Button
        {
            Content = secondsSinceLast < 30 ? $"Warte {(int)(30 - secondsSinceLast)}s" : "Melden",
            Background = SolidColorBrush.Parse("#B43232"),
            IsEnabled = secondsSinceLast >= 30
        };

        btnCancel.Click += (s, e) => dialog.Close();
        btnReport.Click += async (s, e) =>
        {
            if (author == "OnlyCook")
            {
                txtError.Text = "Du kannst mich nicht an mich verpetzen.";
                txtError.IsVisible = true;
                btnReport.IsVisible = false;
                btnCancel.Content = "Ok :C";
                btnCancel.Background = SolidColorBrush.Parse("#32a852");
                return;
            }

            txtError.IsVisible = false;
            btnReport.IsEnabled = false;
            btnReport.Content = "Wird gesendet...";

            // update cooldown timestamp
            playerData.Settings.LastFormspreeTime = DateTime.Now.ToOADate();
            SaveSystem.Save(playerData);

            var payload = new
            {
                type = "Report",
                reporter = AppSettings.GithubUsername,
                reportedUser = author,
                commentId = targetId,
                discussionId = discussionId,
                commentBody = body,
                postedAt = createdAt.ToString("o"),
                reason = txtReason.Text
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                string fId = "do" + "lzbl";
                await _httpClient.PostAsync($"https://formspree.io/f/mz{fId}", content);
            }
            catch { }

            dialog.Close();
        };

        btnPanel.Children.Add(txtError);
        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnReport);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private async void ShowBanDialog()
    {
        // soft ban: blocked by cloudflare proxy (spam/rate limit)
        var dialog = new Window
        {
            Title = "Gesperrt",
            Width = 400,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };

        var stack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Account temporär gesperrt",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#FF5555")
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Dein Account wurde aufgrund von Spam oder unangemessenem Verhalten vorübergehend für Kommentare und Antworten gesperrt.\n\nBitte kontaktiere den Support, falls dies ein Irrtum ist.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var btnOk = new Button
        {
            Content = "Verstanden",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = Cursor.Parse("Hand")
        };
        btnOk.Click += (s, e) => dialog.Close();

        stack.Children.Add(btnOk);
        dialog.Content = stack;

        await dialog.ShowDialog(this);
    }

    private async void ShowPermaBanDialog()
    {
        // permanent ban: user is blocked on github by aec-community-bot
        var dialog = new Window
        {
            Title = "Permanent gesperrt",
            Width = 420,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = SolidColorBrush.Parse("#252526"),
            CornerRadius = new CornerRadius(8)
        };

        var stack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = "⛔ Account permanent gesperrt",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = SolidColorBrush.Parse("#FF2222")
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Dein Account wurde permanent aus der Community ausgeschlossen. Du kannst Kommentare und Bewertungen weiterhin lesen, aber keine Aktionen mehr durchführen.\n\nWende dich an den Support, wenn du dir extremst sicher bist, dass dies ein Fehler ist.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var btnOk = new Button
        {
            Content = "Verstanden",
            Background = SolidColorBrush.Parse("#3C3C3C"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = Cursor.Parse("Hand")
        };
        btnOk.Click += (s, e) => dialog.Close();

        stack.Children.Add(btnOk);
        dialog.Content = stack;

        await dialog.ShowDialog(this);
    }

    private bool CheckAndHandlePermaBan()
    {
        // returns true if the user is perma-banned, shows dialog and blocks the action
        if (!playerData.Settings.IsPermaBanned) return false;
        ShowPermaBanDialog();
        return true;
    }
}
