using AbiturEliteCode.cs;
using AbiturEliteCode.windows;
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
    private CancellationTokenSource? _notFoundCts;

    private CancellationTokenSource? _cooldownCts;

    private const int ApiQueueLimit = 10;

    private const int SubscriptionCountLimit = 100;

    private int _visibleCommentsCount = 20;
    private HashSet<string> _expandedCommentIds = new();
    private Control? _currentActiveTopLevelComment = null;

    private DateTime _commentsOpenedAt = DateTime.MinValue;
    private TimeSpan _accumulatedCommentsOpenTime = TimeSpan.Zero;
    private DispatcherTimer? _commentsRefreshHintTimer;

    private static string? _draftSupportMessage = string.Empty;
    private static string? _draftReportReason = string.Empty;
    private static string? _draftReportTargetUser = string.Empty;

    private static readonly uint _s1 = 0x9BE214DC;
    private static readonly uint _s2 = 0x6FA371C8;

    private DispatcherTimer? _inboxAnimationTimer;
    private int _inboxAnimationStep = 0;
    private bool _hasCompletedInitialNotificationCheck = false;
    private bool _isInitialNotificationCheckRunning = false;
    private readonly string[] _inboxFrames = {
        "ic_dot_left.svg",
        "ic_dot_left_middle.svg",
        "ic_dot_left_middle_right.svg",
        "ic_dot_middle_right.svg",
        "ic_dot_right.svg",
        "ic_dot_none.svg"
    };

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

        try
        {
            await Task.Delay(3000, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

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

    private void ShowOutdatedBanner()
    {
        BtnLike.IsEnabled = false;
        BtnDislike.IsEnabled = false;
        BtnToggleComments.IsEnabled = false;
        PnlCommentsSection.IsVisible = false;
        SetCommunitySkeletonsVisible(true);

        PnlCommunityActions.IsVisible = true;
        if (TxtCommunityOfflineStatus != null)
            TxtCommunityOfflineStatus.IsVisible = false;

        if (UpdateManager.IsMaintenanceMode)
        {
            if (TxtCommunityOutdatedStatus != null) TxtCommunityOutdatedStatus.IsVisible = false;
            if (TxtCommunityMaintenanceStatus != null) TxtCommunityMaintenanceStatus.IsVisible = true;
        }
        else
        {
            if (TxtCommunityMaintenanceStatus != null) TxtCommunityMaintenanceStatus.IsVisible = false;
            if (TxtCommunityOutdatedStatus != null) TxtCommunityOutdatedStatus.IsVisible = true;
        }
    }

    private async Task ShowCommunityNotFoundBannerAsync()
    {
        _notFoundCts?.Cancel();
        _notFoundCts?.Dispose();
        _notFoundCts = new CancellationTokenSource();
        var token = _notFoundCts.Token;

        BtnLike.IsEnabled = false;
        BtnDislike.IsEnabled = false;
        BtnToggleComments.IsEnabled = false;
        PnlCommentsSection.IsVisible = false;
        SetCommunitySkeletonsVisible(false);

        PnlCommunityActions.IsVisible = true;
        if (TxtCommunityOfflineStatus != null) TxtCommunityOfflineStatus.IsVisible = false;
        if (TxtCommunityOutdatedStatus != null) TxtCommunityOutdatedStatus.IsVisible = false;
        if (TxtCommunityMaintenanceStatus != null) TxtCommunityMaintenanceStatus.IsVisible = false;
        if (TxtCommunityFullQueueStatus != null) TxtCommunityFullQueueStatus.IsVisible = false;

        var txtNotFound = this.FindControl<TextBlock>("TxtCommunityNotFoundStatus");
        if (txtNotFound != null) txtNotFound.IsVisible = true;

        try
        {
            await Task.Delay(3000, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (txtNotFound != null) txtNotFound.IsVisible = false;
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

    private async Task UpdateCommunityUIAsync(string? levelId, bool isSql, bool forceFetch = false)
    {
        if (levelId == null) return;

        // cancel any pending banner auto-hide tasks to prevent them from bleeding into the new level
        _offlineCts?.Cancel();
        _notFoundCts?.Cancel();
        _fullQueueCts?.Cancel();

        Debug.WriteLine("[Debug] Fetching level " + levelId);

        // reset comment section and active discussion, but keep the panel visible
        if (PnlCommentsSection.IsVisible)
        {
            if (_commentsOpenedAt != DateTime.MinValue)
            {
                _accumulatedCommentsOpenTime += (DateTime.Now - _commentsOpenedAt);
                _commentsOpenedAt = DateTime.MinValue;
            }
        }

        PnlCommentsSection.IsVisible = false;
        _currentActiveDiscussionId = -1;

        if (BtnScrollTopComments != null)
            BtnScrollTopComments.IsVisible = false;

        if (IconToggleComments != null)
            IconToggleComments.Path = "/assets/icons/ic_comment.svg";

        if (!AppSettings.IsCommunityFeaturesEnabled || string.IsNullOrEmpty(AppSettings.GithubToken) || _isDesignerMode)
        {
            // only hide the panel when community is genuinely unavailable
            PnlCommunityActions.IsVisible = false;
            return;
        }

        int discussionNum = -1;

        if (_isCustomLevelMode)
        {
            if (_currentCustomDiscussionNumber > 0)
            {
                discussionNum = _currentCustomDiscussionNumber;
            }
            else
            {
                PnlCommunityActions.IsVisible = false;
                return;
            }
        }
        else
        {
            if (_discussionMappings == null)
            {
                PnlCommunityActions.IsVisible = false;
                return;
            }

            // standard levels
            string modeKey = isSql ? "SQL" : "C#";
            if (!_discussionMappings.ContainsKey(modeKey) || !_discussionMappings[modeKey].ContainsKey(levelId))
            {
                PnlCommunityActions.IsVisible = false;
                return;
            }
            discussionNum = _discussionMappings[modeKey][levelId];
        }

        if (_currentActiveDiscussionId != discussionNum)
        {
            _visibleCommentsCount = 20;
            _expandedCommentIds.Clear();
        }

        _currentActiveDiscussionId = discussionNum;

        // check version before proceeding
        if (!UpdateManager.HasCheckedForUpdates)
        {
            PnlCommunityActions.IsVisible = true;
            SetCommunitySkeletonsVisible(true);
            var result = await UpdateManager.CheckForUpdatesAsync();

            // push the badge update to the ui if the community check found it first
            if (result.UpdateAvailable)
            {
                _updateAvailable = true;
                _latestVersion = result.LatestVersion;
                _updateDownloadUrl = result.DownloadUrl;
                Dispatcher.UIThread.Post(() =>
                {
                    if (BadgeSettings != null)
                    {
                        BadgeSettings.IsVisible = true;
                        if (UpdateManager.IsMaintenanceMode)
                        {
                            BadgeSettings.Background = Scheme.BrushDiffHard;
                            BadgeSettings.BorderBrush = Scheme.BrushBadgeDefault;
                            if (!BadgeSettings.Classes.Contains("maintenance-blink"))
                                BadgeSettings.Classes.Add("maintenance-blink");
                        }
                        else
                        {
                            BadgeSettings.Background = Scheme.BrushTextTitle;
                            BadgeSettings.BorderBrush = Scheme.BrushBadgeDefault;
                            BadgeSettings.Classes.Remove("maintenance-blink");
                        }
                    }
                });
            }
        }

        if (UpdateManager.IsOutdated)
        {
            ShowOutdatedBanner();
            return;
        }

        var txtOutdated = this.FindControl<TextBlock>("TxtCommunityOutdatedStatus");
        if (txtOutdated != null) txtOutdated.IsVisible = false;

        var txtMaintenance = this.FindControl<TextBlock>("TxtCommunityMaintenanceStatus");
        if (txtMaintenance != null) txtMaintenance.IsVisible = false;

        var txtNotFound = this.FindControl<TextBlock>("TxtCommunityNotFoundStatus");
        if (txtNotFound != null) txtNotFound.IsVisible = false;

        var dict = isSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;

        // check cache first to prevent skeleton flashing
        if (!forceFetch && levelId != null && dict.TryGetValue(levelId, out var cache) && (DateTime.Now - cache.LastFetched).TotalMinutes < 5)
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

    private static string _ResolveFragment(int[] src)
    {
        var lut = _BuildLut();
        var buf = new char[src.Length];
        for (int i = 0; i < src.Length; i++)
            buf[src.Length - 1 - i] = (char)_Unmask((byte)src[i], i, lut);
        return new string(buf);
    }

    private async Task FetchCommunityDataAsync(int discussionNumber, bool isSql, string? levelId, bool fetchNextPage)
    {
        if (UpdateManager.IsOutdated) return;

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

        var dict = isSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        DiscussionCache? cache = null;
        if (levelId != null)
        {
            if (!dict.TryGetValue(levelId, out cache))
            {
                cache = new DiscussionCache();
                dict[levelId] = cache;
            }
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
                cursor = fetchNextPage ? cache?.EndCursor : null
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            requestMessage.Content = content;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var response = await _httpClient.SendAsync(requestMessage).ConfigureAwait(false);
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

                        if (discussion.ValueKind == JsonValueKind.Null)
                        {
                            await Dispatcher.UIThread.InvokeAsync(async () => {
                                await ShowCommunityNotFoundBannerAsync();
                            });
                            return;
                        }

                        if (cache == null) return;

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
                            string? commentAuthor = node.GetProperty("author").GetProperty("login").GetString();
                            string? commentBody = node.GetProperty("body").GetString();
                            bool isBotComment = false;

                            // intercept bot messages and extract the real author
                            if (commentAuthor == "aec-community-bot")
                            {
                                isBotComment = true;
                                var match = commentBody != null ? System.Text.RegularExpressions.Regex.Match(commentBody, @"^<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline) : null;
                                if (match != null && match.Success)
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
                                    string? replyAuthor = rep.GetProperty("author").GetProperty("login").GetString();
                                    string? replyBody = rep.GetProperty("body").GetString();

                                    // intercept bot messages for replies as well
                                    if (replyAuthor == "aec-community-bot")
                                    {
                                        var match = replyBody != null ? System.Text.RegularExpressions.Regex.Match(replyBody, @"^<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline) : null;
                                        if (match != null && match.Success)
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
            if (levelId != null && dictLocal.TryGetValue(levelId, out var cacheData) && string.IsNullOrEmpty(cacheData.DiscussionNodeId))
            {
                dictLocal.Remove(levelId);
            }
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                _isFetchingComments = false;
                TxtCommentsLoading.IsVisible = false;
                if (cache != null && PnlCommentsSection != null && PnlCommentsSection.IsVisible)
                {
                    BtnLoadMoreComments.IsVisible = (_visibleCommentsCount < cache.Comments.Count) || cache.HasNextPage;
                }
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

        if (_isCustomLevelMode && !string.IsNullOrEmpty(cache.DiscussionNodeId))
        {
            SetUpCustomCommunityLevelUi(cache);
        }
        else
        {
            if (BtnCommunityDiscussionMenu != null) BtnCommunityDiscussionMenu.IsVisible = false;
        }

        PnlCommunityActions.IsVisible = true;
    }

    private void SetUpCustomCommunityLevelUi(DiscussionCache cache)
    {
        if (BtnCommunityDiscussionMenu != null) BtnCommunityDiscussionMenu.IsVisible = true;

        var flyout = new Flyout();
        var flyoutStack = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(-5)
        };

        // draw from the loaded custom level fields
        string levelAuthor = _currentCustomAuthor;
        string? levelName = _isSqlMode ? currentSqlLevel?.Title : currentLevel?.Title;
        bool isAuthor = string.Equals(levelAuthor, AppSettings.GithubUsername, StringComparison.OrdinalIgnoreCase);

        if (isAuthor)
        {
            if (cache.DiscussionNodeId == null) return;
            bool isSubscribed = _communityCache.Subscriptions.ContainsKey(cache.DiscussionNodeId);
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
                CornerRadius = new CornerRadius(4)
            };

            btnSubscribe.Click += (s, e) =>
            {
                if (isSubscribed)
                {
                    _communityCache.Subscriptions.Remove(cache.DiscussionNodeId);
                    SaveSystem.SaveCommunityCache(_communityCache);
                }
                else
                {
                    AddOrUpdateSubscription(cache.DiscussionNodeId, cache.TotalComments);
                }

                if (BtnCommunityDiscussionMenu != null) BtnCommunityDiscussionMenu.Flyout?.Hide();
                ApplyCommunityUiData(cache); // lazy refresh visually
            };

            var btnDelete = new Button
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
                CornerRadius = new CornerRadius(4)
            };
            btnDelete.Click += async (s, e) =>
            {
                if (BtnCommunityDiscussionMenu != null) BtnCommunityDiscussionMenu.Flyout?.Hide();
                await ShowLevelDeletionDialogAsync(cache.DiscussionNodeId, levelName);
            };

            flyoutStack.Children.Add(btnSubscribe);
            flyoutStack.Children.Add(btnDelete);
        }
        else
        {
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
                CornerRadius = new CornerRadius(4)
            };
            btnReport.Click += (s, e) =>
            {
                if (levelName == null) return;
                BtnCommunityDiscussionMenu?.Flyout?.Hide();
                if (cache.DiscussionNodeId == null) return;
                ShowLevelReportDialog(levelName, levelAuthor, cache.DiscussionNodeId);
            };
            flyoutStack.Children.Add(btnReport);
        }

        flyout.Content = flyoutStack;
        if (BtnCommunityDiscussionMenu != null) BtnCommunityDiscussionMenu.Flyout = flyout;
    }

    private static string RenderEndpoint(int type) => _ResolveFragment(type == 0
    ? new[] { 239, 7, 115, 73, 0, 52, 22, 198, 165, 139, 236, 61, 12, 51, 160, 115, 167, 158, 206, 150, 232, 27, 92, 159, 105, 102, 59, 113, 108, 180, 50 }
    : new[] { 219, 27, 131, 205, 112, 132, 40, 222, 79, 153, 141, 55, 58, 30, 208, 103, 211, 6, 240, 166, 208, 45, 72, 237, 253, 47, 3, 205, 88, 212, 60, 186, 95, 29, 181, 229, 12, 50, 32, 99, 219, 102, 238, 151, 96, 15, 64, 159, 105, 102, 59, 113, 108, 180, 50 });

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
        if (UpdateManager.IsOutdated) return;

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

        // reset tooltip proactively
        ToolTip.SetTip(BtnToggleComments, null);

        if (!PnlCommentsSection.IsVisible && BtnScrollTopComments != null)
        {
            BtnScrollTopComments.IsVisible = false;
        }

        if (PnlCommentsSection.IsVisible)
        {
            IconToggleComments.Path = "/assets/icons/ic_comment_hide.svg";
            _commentsOpenedAt = DateTime.Now;

            if (_currentActiveDiscussionId != -1)
            {
                RenderCachedComments();

                double remainingSeconds = 20 - _accumulatedCommentsOpenTime.TotalSeconds;
                if (remainingSeconds <= 0)
                {
                    _accumulatedCommentsOpenTime = TimeSpan.Zero;
                    string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                    _ = FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);

                    // restart hint timer for the new valid viewing session
                    StartCommentsRefreshHintTimer(20);
                }
                else
                {
                    StartCommentsRefreshHintTimer(remainingSeconds);
                }
            }
        }
        else
        {
            IconToggleComments.Path = "/assets/icons/ic_comment.svg";
            _commentsRefreshHintTimer?.Stop();

            if (_commentsOpenedAt != DateTime.MinValue)
            {
                _accumulatedCommentsOpenTime += (DateTime.Now - _commentsOpenedAt);
                _commentsOpenedAt = DateTime.MinValue;
            }
        }
    }

    private async void BtnLoadMoreComments_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateManager.IsOutdated) return;

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

        string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;

        if (levelId != null && dict.TryGetValue(levelId, out var cache))
        {
            if (_visibleCommentsCount < cache.Comments.Count)
            {
                // just load 20 more from our local cache
                _visibleCommentsCount += 20;
                RenderCachedComments();
            }
            else if (cache.HasNextPage)
            {
                // we have exhausted cache, query github api
                _visibleCommentsCount += 20;
                await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, true);
            }
        }
    }

    private void TaskScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (PnlCommentsSection == null || !PnlCommentsSection.IsVisible || TaskScrollViewer == null)
        {
            if (BtnScrollTopComments != null) BtnScrollTopComments.IsVisible = false;
            return;
        }

        double scrollY = TaskScrollViewer.Offset.Y;

        if (BtnScrollTopComments != null)
        {
            double commentsTop = 0;
            var commentsTransform = TaskScrollViewer != null && TaskScrollViewer.Content != null ? PnlCommentsSection.TransformToVisual((Control)TaskScrollViewer.Content) : null;
            if (commentsTransform != null)
            {
                commentsTop = commentsTransform.Value.Transform(new Point(0, 0)).Y;
            }

            // show button only after scrolling down past the comment input
            BtnScrollTopComments.IsVisible = scrollY > (commentsTop + 200);

            if (PnlCommentsList != null && PnlCommentsList.IsVisible)
            {
                bool inReplies = false;
                _currentActiveTopLevelComment = null;

                var contentControl = TaskScrollViewer != null ? TaskScrollViewer.Content as Control : null;
                if (contentControl != null)
                {
                    // check whether we are looking at a top level comments replies
                    foreach (var child in PnlCommentsList.Children)
                    {
                        if (child is Border b && b.Child is StackPanel sp)
                        {
                            var repliesContainer = sp.Children.OfType<Border>().FirstOrDefault(c => c.Name == "RepliesContainer");
                            if (repliesContainer != null && repliesContainer.IsVisible)
                            {
                                var repliesTransform = repliesContainer.TransformToVisual(contentControl);
                                if (repliesTransform != null)
                                {
                                    var repliesBounds = new Rect(repliesTransform.Value.Transform(new Point(0, 0)), repliesContainer.Bounds.Size);

                                    // the top-level comment part is above the replies container
                                    double topLevelBottom = repliesBounds.Top;

                                    // if the whole top-level comment is above viewport and the replies are still visible
                                    if (topLevelBottom < scrollY && repliesBounds.Bottom > scrollY + 50)
                                    {
                                        inReplies = true;
                                        _currentActiveTopLevelComment = child;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                if (inReplies && _currentActiveTopLevelComment != null)
                {
                    if (IconScrollTop != null) IconScrollTop.Path = "/assets/icons/ic_arrow_up_alt.svg";
                    ToolTip.SetTip(BtnScrollTopComments, "Zurück zum Kommentar");
                }
                else
                {
                    if (IconScrollTop != null) IconScrollTop.Path = "/assets/icons/ic_arrow_up.svg";
                    ToolTip.SetTip(BtnScrollTopComments, "Zurück nach oben");
                }
            }
        }
    }

    private static byte[] _BuildLut() => new byte[]
    {
        (byte)(_s1 >> 24), (byte)(_s2 >> 16), (byte)(_s1 >>  8), (byte)(_s2),
        (byte)(_s2 >> 24), (byte)(_s1 >> 16), (byte)(_s2 >>  8), (byte)(_s1)
    };

    private void BtnScrollTopComments_Click(object sender, RoutedEventArgs e)
    {
        if (TaskScrollViewer == null) return;

        if (_currentActiveTopLevelComment != null)
        {
            // jump up to the top-level comment that owns the replies we are looking at
            var transform = TaskScrollViewer.Content != null ? _currentActiveTopLevelComment.TransformToVisual((Control)TaskScrollViewer.Content) : null;
            if (transform != null)
            {
                double y = transform.Value.Transform(new Point(0, 0)).Y;
                TaskScrollViewer.Offset = new Vector(TaskScrollViewer.Offset.X, Math.Max(0, y - 10));
            }
        }
        else
        {
            // jump back to the very top of the comment section
            var transform = TaskScrollViewer.Content != null ? PnlCommentsSection.TransformToVisual((Control)TaskScrollViewer.Content) : null;
            if (transform != null)
            {
                double y = transform.Value.Transform(new Point(0, 0)).Y;
                TaskScrollViewer.Offset = new Vector(TaskScrollViewer.Offset.X, Math.Max(0, y - 10));
            }
        }
    }

    private void RenderCachedComments()
    {
        PnlCommentsList.Children.Clear();
        string? levelKey = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;

        if (levelKey == null || !dict.TryGetValue(levelKey, out var cache)) return;

        var txtEmpty = this.FindControl<TextBlock>("TxtCommentsEmpty");
        txtEmpty?.IsVisible = cache.Comments.Count == 0;

        string sortMode = (CmbCommentSort.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Beste";
        var sortedComments = cache.Comments.ToList();

        if (sortMode == "Beste")
        {
            sortedComments = sortedComments.OrderByDescending(CalculateBestScore).ToList();
        }
        else if (sortMode == "Top")
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

        int toShow = Math.Min(_visibleCommentsCount, sortedComments.Count);
        for (int i = 0; i < toShow; i++)
        {
            if (cache.DiscussionNodeId == null) return;
            var control = CreateCommentUI(sortedComments[i], cache.DiscussionNodeId);
            if (control != null) PnlCommentsList.Children.Add(control);
        }

        bool hasMoreLocal = _visibleCommentsCount < sortedComments.Count;
        bool hasMoreRemote = cache.HasNextPage;
        BtnLoadMoreComments.IsVisible = hasMoreLocal || hasMoreRemote;
    }

    private double CalculateBestScore(GithubComment comment)
    {
        double ageInHours = (DateTime.Now - comment.CreatedAt).TotalHours;
        // add 2 hours to base to prevent division by zero or overly high scores for brand new comments
        // 1.5 acts as the gravity multiplier (higher gravity = age drags the score down faster)
        return comment.Upvotes / Math.Pow(Math.Max(ageInHours, 0) + 2.0, 1.5);
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
        if (UpdateManager.IsOutdated) return;

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

        int effectiveCount = _apiQueue.Count + _pendingDebounces.Count;
        if (effectiveCount >= ApiQueueLimit)
        {
            ShowFullQueueBannerAsync();
            return;
        }

        string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        if (levelId == null || !dict.TryGetValue(levelId, out var cache)) return;

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
        if (UpdateManager.IsOutdated) return;

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

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            requestMessage.Content = httpContent;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

            var resp = await _httpClient.SendAsync(requestMessage);
            string respBody = await resp.Content.ReadAsStringAsync();

            Debug.WriteLine("[Community] Level-Vote body: " + respBody);

            // detect github block (perma-ban)
            // github returns "FORBIDDEN" when trying to remove a reaction that doesnt exist, so we only check on 'add'
            if (add && respBody.Contains("\"FORBIDDEN\"") && respBody.Contains("does not have the correct permissions"))
            {
                playerData.Settings.IsPermaBanned = true;
                SaveSystem.Save(playerData);
                ClearApiQueue();
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
        // prevent manual '@' usage to force usage of the tag button
        if (!_isProgrammaticTextChange && TxtCommentInput.Text != null && TxtCommentInput.Text.Contains("@"))
        {
            _isProgrammaticTextChange = true;
            int caret = TxtCommentInput.CaretIndex;
            TxtCommentInput.Text = TxtCommentInput.Text.Replace("@", "");
            TxtCommentInput.CaretIndex = Math.Max(0, caret - 1);
            _isProgrammaticTextChange = false;
        }

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


    private Control? CreateCommentUI(GithubComment? comment, string? discussionId, bool isReply = false, Action<string>? onTagUser = null, GithubComment? parentComment = null)
    {
        var border = new Border
        {
            Background = isReply ? Scheme.BrushBgPanel7 : Scheme.BrushBgPanel3,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(15),
            BorderBrush = Scheme.BrushBgPanel5,
            BorderThickness = new Thickness(1),
            Margin = isReply ? new Thickness(20, 0, 0, 0) : new Thickness(0, 5, 0, 0)
        };

        // highlight visualizer for notifications
        if ((!isReply && comment != null && comment.Id != null && comment.Id == _targetHighlightCommentId) || (isReply && comment != null && comment.Id == _targetHighlightReplyId))
        {
            border.BorderBrush = Scheme.BrushTextHighlight;
            border.BorderThickness = new Thickness(2);
        }

        var mainStack = new StackPanel { Spacing = 8 };

        // remove zero-width space so mentions display normally in the ui
        string? bodyToRender = comment?.Body?.Replace("@\u200B", "@");
        string? activeTag = null;
        IBrush tagColor = Brushes.Gray;

        // dynamically highlight any mentions toward user
        if (bodyToRender == null) return null;
        bodyToRender = System.Text.RegularExpressions.Regex.Replace(bodyToRender, $@"(@{System.Text.RegularExpressions.Regex.Escape(AppSettings.GithubUsername)})", "__$1__");

        var tags = new Dictionary<string, (string Label, SolidColorBrush Color)>
        {
            { "!FEEDBACK;", ("Feedback", Scheme.BrushFeedbackPink) },
            { "!FRAGE;", ("Frage", Scheme.BrushTextHighlight2) },
            { "!TIPP;", ("Tipp", Scheme.BrushTextTitle) },
            { "!LÖSUNG;", ("Lösung", Scheme.BrushDopamineEnducingGold) }
        };

        foreach (var tag in tags)
        {
            if (bodyToRender.StartsWith(tag.Key))
            {
                activeTag = tag.Value.Label;
                tagColor = tag.Value.Color;
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
            Text = $"{comment?.Author}",
            Foreground = comment?.Author == AppSettings.GithubUsername ? Scheme.BrushTextHighlight : Brushes.Gray,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = $" • {comment?.CreatedAt:dd.MM.yyyy 'um' HH:mm}",
            Foreground = Brushes.Gray,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (activeTag != null)
        {
            headerPanel.Children.Add(new Border
            {
                Background = Scheme.BrushTextNormal3,
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

        Button? btnEdit = null;
        Button? btnDelete = null;

        var flyout = new Flyout();
        var flyoutStack = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(-5)
        };

        if (comment == null) return null;
        bool hasUserReplied = !isReply && comment.Replies.Any(r => r.Author == AppSettings.GithubUsername);
        bool isAuthor = comment.Author == AppSettings.GithubUsername;
        if (comment.Id != null && !isReply && (isAuthor || hasUserReplied))
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
                            Text = isSubscribed ? isAuthor ? "Deabonnieren" : "Deabonnieren (@)" : isAuthor ? "Abonnieren" : "Abonnieren (@)",
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 8),
                CornerRadius = new CornerRadius(4)
            };

            btnSubscribe.Click += (s, e) =>
            {
                if (isSubscribed)
                {
                    _communityCache.Subscriptions.Remove(comment.Id);
                    SaveSystem.SaveCommunityCache(_communityCache);
                }
                else
                {
                    AddOrUpdateSubscription(comment.Id, comment.Replies.Count);
                }

                btnMore.Flyout?.Hide();

                isSubscribed = !isSubscribed;
                var contentStack = (StackPanel)btnSubscribe.Content;
                var textBlock = (TextBlock)contentStack.Children[1];

                contentStack.Children[0] = LoadIcon(isSubscribed ? "assets/icons/ic_unsubscribe.svg" : "assets/icons/ic_subscribe.svg", 16);
                textBlock.Text = isSubscribed ? isAuthor ? "Deabonnieren" : "Deabonnieren (@)" : isAuthor ? "Abonnieren" : "Abonnieren (@)";
            };

            flyoutStack.Children.Add(btnSubscribe);
        }

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
                CornerRadius = new CornerRadius(4)
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
                CornerRadius = new CornerRadius(4)
            };

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
                    CornerRadius = new CornerRadius(4)
                };
                btnTag.Click += (s, e) =>
                {
                    if (comment.Author == null) return;
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
                CornerRadius = new CornerRadius(4)
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
                Background = Scheme.BrushBgPanel2,
                Foreground = Brushes.White,
                Padding = new Thickness(10, 5),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursor.Parse("Hand")
            };
            var spoilerContent = new StackPanel
            {
                IsVisible = false
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
            Color bgColor = isReply ? Scheme.BrushBgPanel7.Color : Scheme.BrushBgPanel3.Color;
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
            expandIconContainer.Children.Add(LoadIcon("assets/icons/ic_chevron_down.svg", 16));

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
                expandIconContainer.Children.Add(LoadIcon(isExpanded ? "assets/icons/ic_chevron_up.svg" : "assets/icons/ic_chevron_down.svg", 16));
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
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.White,
            BorderBrush = Scheme.BrushBgPanel5,
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
            Background = Scheme.BrushBgPanel2
        };
        var btnSaveEdit = new Button
        {
            Content = "Speichern",
            Background = Scheme.BrushTextTitle
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
            Background = comment.ViewerHasUpvoted ? Scheme.BrushUpvoteFg : Brushes.Transparent,
            BorderBrush = comment.ViewerHasUpvoted ? Scheme.BrushTextHighlight : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5)
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
            Foreground = comment.ViewerHasUpvoted ? Scheme.BrushTextHighlight : Brushes.Gray,
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

            int effectiveCount = _apiQueue.Count + _pendingDebounces.Count;
            if (effectiveCount >= ApiQueueLimit)
            {
                ShowFullQueueBannerAsync();
                return;
            }

            // optimistic ui state update
            bool targetState = !comment.ViewerHasUpvoted;
            comment.ViewerHasUpvoted = targetState;
            comment.Upvotes += targetState ? 1 : -1;

            btnUpvote.Background = targetState ? Scheme.BrushUpvoteFg : Brushes.Transparent;
            btnUpvote.BorderBrush = targetState ? Scheme.BrushTextHighlight : Brushes.Transparent;
            upvoteContent.Children.Clear();
            upvoteContent.Children.Add(LoadIcon(targetState ? "assets/icons/ic_upvote_filled.svg" : "assets/icons/ic_upvote.svg", 16));
            upvoteContent.Children.Add(new TextBlock
            {
                Text = comment.Upvotes.ToString(),
                Foreground = targetState ? Scheme.BrushTextHighlight : Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            });

            QueueApiRequestWithDebounce($"upvote_{comment.Id}",
                $"Upvote on comment by {comment.Author}",
                () => isReply ? ToggleReplyUpvoteAsync(comment.Id, targetState) : ToggleCommentUpvoteAsync(comment.Id, targetState));
        };
        actionsPanel.Children.Add(btnUpvote);

        // reply button and box setup
        Grid? replyInputGrid = null;
        if (!isReply)
        {
            var btnToggleReply = new Button
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(5),
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

            string activeReplyTag = "";

            var txtReply = new TextBox
            {
                Watermark = "Antwort verfassen...",
                Background = Scheme.BrushBgPanel3,
                Foreground = Brushes.White,
                BorderBrush = Scheme.BrushBgPanel5,
                CornerRadius = new CornerRadius(4),
                AcceptsReturn = true,
                MaxHeight = 100,
                MaxLength = ReplyCharLimit
            };
            var btnSendReply = new Button
            {
                Background = Scheme.BrushBgPanel2,
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
                Foreground = Scheme.BrushDeniedFg,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 6, 0, 0),
                IsVisible = false
            };
            CancellationTokenSource? replyCooldownCts = null;

            txtReply.TextChanged += (s, e) =>
            {
                if (_isProgrammaticTextChange) return;

                string text = txtReply.Text ?? "";
                bool textModified = false;
                int caret = txtReply.CaretIndex;

                // check if the active tag was touched, modified or prepended to
                if (!string.IsNullOrEmpty(activeReplyTag))
                {
                    if (!text.StartsWith(activeReplyTag))
                    {
                        if (text.StartsWith("@"))
                        {
                            int spaceIdx = text.IndexOf(' ');
                            if (spaceIdx != -1)
                                text = text.Substring(spaceIdx + 1).TrimStart();
                            else
                                text = "";
                        }
                        activeReplyTag = "";
                        textModified = true;
                        caret = Math.Min(caret, text.Length);
                    }
                }

                // prevent manual '@' anywhere
                int expectedAtCount = string.IsNullOrEmpty(activeReplyTag) ? 0 : 1;
                int actualAtCount = text.Count(c => c == '@');

                if (actualAtCount > expectedAtCount)
                {
                    if (!string.IsNullOrEmpty(activeReplyTag) && text.StartsWith(activeReplyTag))
                    {
                        string remainder = text.Substring(activeReplyTag.Length).Replace("@", "");
                        text = activeReplyTag + remainder;
                    }
                    else
                    {
                        text = text.Replace("@", "");
                    }
                    textModified = true;
                    caret = Math.Max(0, caret - 1);
                }

                if (textModified)
                {
                    _isProgrammaticTextChange = true;
                    txtReply.Text = text;
                    txtReply.CaretIndex = Math.Min(caret, text.Length);
                    _isProgrammaticTextChange = false;
                }

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

                activeReplyTag = "";
                txtReply.Text = "";
                replyInputGrid.IsVisible = false;
                replyContent.Children[0] = LoadIcon("assets/icons/ic_comment_add.svg", 18);
                ToolTip.SetTip(btnToggleReply, "Antwort verfassen");

                if (replySent)
                {
                    // auto subscribe to the parent comment so the background poller can look for mentions in future replies
                    AddOrUpdateSubscription(comment.Id, comment.Replies.Count + 1);

                    // keep parent replies visible after refreshing
                    if (comment.Id != null) _expandedCommentIds.Add(comment.Id);

                    string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
                    await FetchCommunityDataAsync(_currentActiveDiscussionId, _isSqlMode, levelId, false);
                }
            };

            Action<string> localTagAction = (username) =>
            {
                replyInputGrid.IsVisible = true;
                string newTag = $"@{username} ";

                string currentText = txtReply.Text ?? string.Empty;

                // remove existing tag if present to prevent stacking tags
                if (!string.IsNullOrEmpty(activeReplyTag) && currentText.StartsWith(activeReplyTag))
                {
                    currentText = currentText.Substring(activeReplyTag.Length);
                }

                activeReplyTag = newTag;

                _isProgrammaticTextChange = true;
                txtReply.Text = activeReplyTag + currentText.TrimStart();

                // enqueue the reset to focus and set caret properly
                Dispatcher.UIThread.Post(() =>
                {
                    _isProgrammaticTextChange = false;
                    txtReply.Focus();
                    txtReply.CaretIndex = activeReplyTag.Length;
                });
            };

            Grid.SetColumn(btnSendReply, 1);
            replyInputRow.Children.Add(txtReply);
            replyInputRow.Children.Add(btnSendReply);

            Grid.SetRow(replyInputRow, 0);
            Grid.SetRow(replyCooldownLabel, 1);
            replyInputGrid.Children.Add(replyInputRow);
            replyInputGrid.Children.Add(replyCooldownLabel);

            actionsPanel.Children.Add(btnToggleReply);

            Border? repliesContainer = null;
            if (comment.Replies.Count > 0)
            {
                var btnShowReplies = new Button
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(5)
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
                    Name = "RepliesContainer",
                    BorderThickness = new Thickness(2, 0, 0, 0),
                    BorderBrush = Scheme.BrushBgPanel5,
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

                    if (repliesContainer.IsVisible && comment.Id != null) _expandedCommentIds.Add(comment.Id);
                    else if (comment.Id != null) _expandedCommentIds.Remove(comment.Id);
                };

                if ((comment.Id == _targetHighlightCommentId && comment.Replies.Count > 0) || (comment.Id != null && _expandedCommentIds.Contains(comment.Id)))
                {
                    repliesContainer.IsVisible = true;
                    txtShowReplies.Text = "Antworten ausblenden";
                    showRepliesContent.Children[0] = LoadIcon("assets/icons/ic_comment_hide.svg", 16);
                    if (comment.Id != null) _expandedCommentIds.Add(comment.Id);
                }

                int maxRepliesShown = 10;
                var replyUIs = new List<Control>();

                foreach (var reply in comment.Replies.OrderBy(r => r.CreatedAt))
                {
                    var rUI = CreateCommentUI(new GithubComment
                    {
                        Id = reply.Id,
                        Author = reply.Author,
                        Body = reply.Body,
                        CreatedAt = reply.CreatedAt,
                        Upvotes = reply.Upvotes,
                        ViewerHasUpvoted = reply.ViewerHasUpvoted
                    }, discussionId, true, localTagAction, comment);

                    if (rUI != null)
                    {
                        replyUIs.Add(rUI);
                        repliesStack.Children.Add(rUI);
                    }
                }

                // add button for loading more replies if there are more than 10
                if (replyUIs.Count > 10)
                {
                    var btnLoadMoreReplies = new Button
                    {
                        Content = "Weitere Antworten laden...",
                        Background = Brushes.Transparent,
                        Foreground = Scheme.BrushTextHighlight,
                        Margin = new Thickness(20, 5, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    void UpdateRepliesVisibility()
                    {
                        for (int i = 0; i < replyUIs.Count; i++)
                        {
                            replyUIs[i].IsVisible = i < maxRepliesShown;
                        }
                        btnLoadMoreReplies.IsVisible = maxRepliesShown < replyUIs.Count;
                    }

                    btnLoadMoreReplies.Click += (s, e) =>
                    {
                        maxRepliesShown += 10;
                        UpdateRepliesVisibility();
                    };

                    repliesStack.Children.Add(btnLoadMoreReplies);
                    UpdateRepliesVisibility();
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
                string? newBody = txtEdit.Text;

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
                            string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
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

                    if (deleted)
                    {
                        // remove the deleted comment itself from subscriptions if subscribed
                        if (comment.Id != null && _communityCache.Subscriptions.ContainsKey(comment.Id))
                        {
                            _communityCache.Subscriptions.Remove(comment.Id);
                            SaveSystem.SaveCommunityCache(_communityCache);
                        }

                        if (isReply && parentComment != null && parentComment.Author != AppSettings.GithubUsername)
                        {
                            bool hasOtherReplies = parentComment.Replies.Any(r => r.Id != comment.Id && r.Author == AppSettings.GithubUsername);
                            if (!hasOtherReplies && parentComment.Id != null)
                            {
                                _communityCache.Subscriptions.Remove(parentComment.Id);
                                SaveSystem.SaveCommunityCache(_communityCache);
                            }
                        }
                    }

                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        if (deleted)
                        {
                            string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
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
                BorderBrush = Scheme.BrushBgPanel5,
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
        if (UpdateManager.IsOutdated) return;

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

        string? tagSelection = (CmbCommentTag.SelectedItem as ComboBoxItem)?.Content?.ToString();

        if (!string.IsNullOrEmpty(tagSelection) && tagSelection != "–")
        {
            // prepend the tag to the comment
            fullBody = $"!{tagSelection.ToUpper()};\n{fullBody}";
        }

        string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        var dict = _isSqlMode ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
        if (levelId != null && dict.TryGetValue(levelId, out var cache))
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

                // route to the proxy worker
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RenderEndpoint(1));
                requestMessage.Content = content;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                var resp = await _httpClient.SendAsync(requestMessage);
                string resBody = await resp.Content.ReadAsStringAsync();

                if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED" || resBody == "PERMA_BANNED")
                {
                    BtnSendComment.IsEnabled = true;
                    await Dispatcher.UIThread.InvokeAsync(resBody == "PERMA_BANNED" ? ShowPermaBanDialog : ShowBanDialog);
                    return;
                }

                // handle new soft rate limit
                if (resp.StatusCode == (System.Net.HttpStatusCode)429)
                {
                    BtnSendComment.IsEnabled = true;
                    ShowCooldownMessage(60);
                    return;
                }

                if (resp.IsSuccessStatusCode && !resBody.Contains("\"errors\":"))
                {
                    // auto subscribe to users own comment
                    try
                    {
                        using var doc = JsonDocument.Parse(resBody);
                        var id = doc.RootElement.GetProperty("data").GetProperty("addDiscussionComment").GetProperty("comment").GetProperty("id").GetString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            AddOrUpdateSubscription(id, 0);
                        }
                    }
                    catch { }

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

    private async Task<bool> SendReplyToGithubAsync(string? discussionNodeId, string? commentNodeId, string body)
    {
        if (UpdateManager.IsOutdated) return false;
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

            // route to the proxy worker
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RenderEndpoint(1));
            requestMessage.Content = content;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.SendAsync(requestMessage);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED" || resBody == "PERMA_BANNED")
            {
                BtnSendComment.IsEnabled = true;
                await Dispatcher.UIThread.InvokeAsync(resBody == "PERMA_BANNED" ? ShowPermaBanDialog : ShowBanDialog);
                return false;
            }

            // handle new soft rate limit
            if (resp.StatusCode == (System.Net.HttpStatusCode)429)
            {
                BtnSendComment.IsEnabled = true;
                ShowCooldownMessage(60);
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

    private async Task ToggleCommentUpvoteAsync(string? subjectId, bool targetState)
    {
        if (UpdateManager.IsOutdated) return;

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
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            requestMessage.Content = content;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.SendAsync(requestMessage);
            string respBody = await resp.Content.ReadAsStringAsync();

            // detect github block (perma-ban)
            if (respBody.Contains("\"FORBIDDEN\"") && respBody.Contains("does not have the correct permissions"))
            {
                playerData.Settings.IsPermaBanned = true;
                SaveSystem.Save(playerData);
                ClearApiQueue();
                await Dispatcher.UIThread.InvokeAsync(ShowPermaBanDialog);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toggle Upvote Error: {ex.Message}");
        }
    }

    private async Task<bool> UpdateCommentOrReplyAsync(string? id, string? body)
    {
        if (UpdateManager.IsOutdated) return false;

        var payload = new
        {
            commentId = id,
            body = body
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // route to the proxy worker via put
            using var requestMessage = new HttpRequestMessage(HttpMethod.Put, RenderEndpoint(1));
            requestMessage.Content = content;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.SendAsync(requestMessage);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED" || resBody == "PERMA_BANNED")
            {
                BtnSendComment.IsEnabled = true;
                await Dispatcher.UIThread.InvokeAsync(resBody == "PERMA_BANNED" ? ShowPermaBanDialog : ShowBanDialog);
                return false;
            }

            // handle new soft rate limit
            if (resp.StatusCode == (System.Net.HttpStatusCode)429)
            {
                BtnSendComment.IsEnabled = true;
                ShowCooldownMessage(60);
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

    private async Task<bool> DeleteCommentOrReplyAsync(string? id)
    {
        if (UpdateManager.IsOutdated) return false;

        try
        {
            // route to the proxy worker via delete
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{RenderEndpoint(1)}?commentId={id}");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.SendAsync(requestMessage);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && resBody == "BANNED" || resBody == "PERMA_BANNED")
            {
                BtnSendComment.IsEnabled = true;
                await Dispatcher.UIThread.InvokeAsync(resBody == "PERMA_BANNED" ? ShowPermaBanDialog : ShowBanDialog);
                return false;
            }

            // handle new soft rate limit
            if (resp.StatusCode == (System.Net.HttpStatusCode)429)
            {
                BtnSendComment.IsEnabled = true;
                ShowCooldownMessage(60);
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

    private async Task ToggleReplyUpvoteAsync(string? subjectId, bool targetState)
    {
        if (UpdateManager.IsOutdated) return;

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
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
            requestMessage.Content = content;
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
            var resp = await _httpClient.SendAsync(requestMessage);
            string respBody = await resp.Content.ReadAsStringAsync();

            Debug.WriteLine("[Community] Reply-Upvote body: " + respBody);

            // detect github block (perma-ban)
            if (respBody.Contains("\"FORBIDDEN\"") && respBody.Contains("does not have the correct permissions"))
            {
                playerData.Settings.IsPermaBanned = true;
                SaveSystem.Save(playerData);
                ClearApiQueue();
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

    public static int GetApiQueueInFlightCount() => _apiQueueInFlight;

    public static void ClearApiQueue()
    {
        _apiQueue.Clear();

        // cancel all active debounce tokens to prevent pending items from running
        foreach (var kvp in _pendingDebounces)
        {
            if (_debounceTokens.TryGetValue(kvp.Key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
        _pendingDebounces.Clear();
        _debounceTokens.Clear();
    }

    private async void ShowApiQueueDialog()
    {
        bool finished = await ApiQueueDialog.ShowAsync(this, new ApiQueueDialogConfig
        {
            SubtitleText = "Bitte warten, bis alle Community-Aktionen hochgeladen wurden, um Datenverlust zu vermeiden.",
            CancelButtonText = "Abbrechen",
            DestructiveButtonText = "Trotzdem schließen",
            GetSnapshot = GetApiQueueSnapshot,
            GetNextAvailableApiTime = GetNextAvailableApiTime,
            GetInFlightCount = () => _apiQueueInFlight,
            MonospaceFontFamily = MonospaceFontFamily
        });

        if (finished)
        {
            _isForceClosing = true;
            Close();
        }
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
        string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
        _ = UpdateCommunityUIAsync(levelId, _isSqlMode);
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
            string? levelId = (_isSqlMode ? currentSqlLevel?.Id : currentLevel?.Id).ToString();
            _ = UpdateCommunityUIAsync(levelId, _isSqlMode);
        });
    }

    private void BtnTabComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender == BtnTabEdit)
        {
            BtnTabEdit.Background = Scheme.BrushBgPanel;
            BtnTabEdit.Foreground = Scheme.BrushTextTitle;
            BtnTabEdit.FontWeight = FontWeight.SemiBold;

            BtnTabPreview.Background = Brushes.Transparent;
            BtnTabPreview.Foreground = Scheme.BrushTextNormal7;
            BtnTabPreview.FontWeight = FontWeight.Normal;

            TxtCommentInput.IsVisible = true;
            PnlCommentPreview.IsVisible = false;
            BtnMarkdownToolbar.IsVisible = true;
        }
        else
        {
            BtnTabPreview.Background = Scheme.BrushBgPanel;
            BtnTabPreview.Foreground = Scheme.BrushTextTitle;
            BtnTabPreview.FontWeight = FontWeight.SemiBold;

            BtnTabEdit.Background = Brushes.Transparent;
            BtnTabEdit.Foreground = Scheme.BrushTextNormal7;
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
                        int lineEnd = text.IndexOf('\n', start);
                        int insertPos = lineEnd == -1 ? text.Length : lineEnd;
                        bool atLineStart = start == 0 || text[start - 1] == '\n';

                        if (atLineStart)
                        {
                            TxtCommentInput.Text = text.Insert(start, "## ");
                            TxtCommentInput.SelectionStart = start + 3;
                        }
                        else
                        {
                            TxtCommentInput.Text = text.Insert(insertPos, "\n## ");
                            TxtCommentInput.SelectionStart = insertPos + 4;
                        }

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
                        if (end > start)
                        {
                            bool atLineStart = start == 0 || text[start - 1] == '\n';
                            string prefix = atLineStart ? "" : "\n";

                            string block = $"{prefix}```csharp\n{sel}\n```";
                            TxtCommentInput.Text = text.Remove(start, end - start).Insert(start, block);
                            TxtCommentInput.SelectionStart = start + block.Length;
                            TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                            break;
                        }

                        int lineEnd = text.IndexOf('\n', start);
                        int insertPos = lineEnd == -1 ? text.Length : lineEnd;
                        bool needsNewLine = !(start == 0 || text[start - 1] == '\n');

                        if (needsNewLine)
                        {
                            TxtCommentInput.Text = text.Insert(insertPos, "\n```csharp\n\n```");
                            TxtCommentInput.SelectionStart = insertPos + 11;
                        }
                        else
                        {
                            TxtCommentInput.Text = text.Insert(start, "```csharp\n\n```");
                            TxtCommentInput.SelectionStart = start + 10;
                        }

                        TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                        break;
                    }
                case "List":
                    {
                        int insertPos;
                        if (end > start)
                        {
                            string block = string.Join("\n", sel
                                .Split('\n')
                                .Select(line => $"- {line}"));
                            TxtCommentInput.Text = text.Remove(start, end - start).Insert(start, block);
                            TxtCommentInput.SelectionStart = start + block.Length;
                            TxtCommentInput.SelectionEnd = TxtCommentInput.SelectionStart;
                            break;
                        }

                        int lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
                        bool needsNewLine = lineStart < start;

                        if (needsNewLine)
                        {
                            int lineEnd = text.IndexOf('\n', start);
                            insertPos = lineEnd == -1 ? text.Length : lineEnd;
                            TxtCommentInput.Text = text.Insert(insertPos, "\n- ");
                            TxtCommentInput.SelectionStart = insertPos + 3;
                        }
                        else
                        {
                            TxtCommentInput.Text = text.Insert(lineStart, "- ");
                            TxtCommentInput.SelectionStart = lineStart + 2;
                        }

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
        if (AppSettings.IsCommunityFeaturesEnabled && !string.IsNullOrEmpty(AppSettings.GithubToken))
        {
            // start animation immediately
            _isInitialNotificationCheckRunning = true;
            Dispatcher.UIThread.InvokeAsync(UpdateInboxUI);

            Task.Run(async () =>
            {
                await Task.Delay(5000);

                // force the initial fetch regardless of queue state to ensure the animation finishes correctly
                EnqueueApiRequest("Nach neuen Benachrichtigungen suchen", PollNotificationsAsync);
            });
        }
        else
        {
            _hasCompletedInitialNotificationCheck = true;
        }
    }

    private async Task PollNotificationsAsync()
    {
        if (!_hasCompletedInitialNotificationCheck)
        {
            _isInitialNotificationCheckRunning = true;
            await Dispatcher.UIThread.InvokeAsync(UpdateInboxUI);
        }

        try
        {
            // early returns inside try-block so the finally-block is guaranteed to stop the animation
            if (UpdateManager.IsOutdated) return;
            if (!await CheckRealConnectivityAsync()) return;
            if (playerData.Settings.AreNotificationsPaused) return;

            bool hasNewNotifications = false;
            bool hasRemovedNotifications = false;

            // local helper to deeply clean up deleted custom level community data
            bool PurgeZombieCustomLevel(string discId)
            {
                bool cacheChanged = false;

                // unsubscribe from the main level
                if (_communityCache.Subscriptions.Remove(discId)) cacheChanged = true;

                // remove all associated notifications
                int removedNotis = _communityCache.Notifications.RemoveAll(n => n.TargetDiscussionId == discId);
                if (removedNotis > 0)
                {
                    cacheChanged = true;
                    hasRemovedNotifications = true;
                }

                // clean up cached discussions and their nested comment/reply subscriptions
                var matchingCs = _communityCache.CsharpDiscussions.Where(kvp => kvp.Value.DiscussionNodeId == discId).ToList();
                var matchingSql = _communityCache.SqlDiscussions.Where(kvp => kvp.Value.DiscussionNodeId == discId).ToList();

                foreach (var kvp in matchingCs)
                {
                    foreach (var comment in kvp.Value.Comments)
                    {
                        if (comment.Id != null && _communityCache.Subscriptions.Remove(comment.Id)) cacheChanged = true;
                        foreach (var reply in comment.Replies)
                        {
                            if (reply.Id != null && _communityCache.Subscriptions.Remove(reply.Id)) cacheChanged = true;
                        }
                    }
                    _communityCache.CsharpDiscussions.Remove(kvp.Key);
                    cacheChanged = true;
                }

                foreach (var kvp in matchingSql)
                {
                    foreach (var comment in kvp.Value.Comments)
                    {
                        if (comment.Id != null && _communityCache.Subscriptions.Remove(comment.Id)) cacheChanged = true;
                        foreach (var reply in comment.Replies)
                        {
                            if (reply.Id != null && _communityCache.Subscriptions.Remove(reply.Id)) cacheChanged = true;
                        }
                    }
                    _communityCache.SqlDiscussions.Remove(kvp.Key);
                    cacheChanged = true;
                }

                return cacheChanged;
            }

            try
            {
                // find all valid local custom level ids
                HashSet<string> validLocalCustomLevelIds = new();
                var customLevels = GetCustomLevels();
                foreach (var cl in customLevels)
                {
                    try
                    {
                        string json = File.ReadAllText(cl.FilePath);
                        if (!json.TrimStart().StartsWith("{")) json = LevelEncryption.Decrypt(json);
                        using var doc = JsonDocument.Parse(json);

                        int newId;
                        if (doc.RootElement.TryGetProperty("DiscussionNumber", out var dNum))
                        {
                            newId = -dNum.GetInt32();
                        }
                        else
                        {
                            newId = GetDeterministicHashCode(System.IO.Path.GetFileName(cl.FilePath));
                            if (newId > 0) newId *= -1;
                        }
                        validLocalCustomLevelIds.Add(newId.ToString());
                    }
                    catch { }
                }

                bool cachePreCleaned = false;

                // purge based on missing local file id (negative keys in cache not in validLocalCustomLevelIds)
                var zombieLevelIds = _communityCache.CsharpDiscussions.Keys
                    .Where(k => int.TryParse(k, out int id) && id < 0 && !validLocalCustomLevelIds.Contains(k))
                    .ToList();
                zombieLevelIds.AddRange(_communityCache.SqlDiscussions.Keys
                    .Where(k => int.TryParse(k, out int id) && id < 0 && !validLocalCustomLevelIds.Contains(k)));

                foreach (var zId in zombieLevelIds.Distinct())
                {
                    string? discId = null;
                    if (_communityCache.CsharpDiscussions.TryGetValue(zId, out var csCache)) discId = csCache.DiscussionNodeId;
                    if (string.IsNullOrEmpty(discId) && _communityCache.SqlDiscussions.TryGetValue(zId, out var sqlCache)) discId = sqlCache.DiscussionNodeId;

                    if (!string.IsNullOrEmpty(discId))
                    {
                        if (PurgeZombieCustomLevel(discId)) cachePreCleaned = true;
                    }
                }

                if (cachePreCleaned) SaveSystem.SaveCommunityCache(_communityCache);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Community] Zombie Pre-Cleanup Error: {ex.Message}");
            }

            // check graphql api for subscribed comment replies and bootleg mentions
            if (_communityCache.Subscriptions.Count > 0)
            {
                try
                {
                    var allIds = _communityCache.Subscriptions.Keys.ToList();
                    int chunkSize = 100;

                    for (int i = 0; i < allIds.Count; i += chunkSize)
                    {
                        var ids = allIds.Skip(i).Take(chunkSize).ToList();
                        var queryObj = new
                        {
                            query = @"query($ids: [ID!]!) {
                                nodes(ids: $ids) {
                                    ... on DiscussionComment {
                                        id
                                        discussion { 
                                            id 
                                            author { login }
                                            body
                                        }
                                        author { login }
                                        body
                                        replies(last: 15) {
                                            totalCount
                                            nodes { id author { login } body }
                                        }
                                    }
                                    ... on Discussion {
                                        id
                                        author { login }
                                        body
                                        comments(last: 15) {
                                            totalCount
                                            nodes { id author { login } body }
                                        }
                                    }
                                }
                            }",
                            variables = new { ids = ids }
                        };

                        var content = new StringContent(JsonSerializer.Serialize(queryObj), Encoding.UTF8, "application/json");
                        using var graphqlRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
                        graphqlRequest.Content = content;
                        graphqlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                        var graphqlResp = await _httpClient.SendAsync(graphqlRequest);

                        if (graphqlResp.IsSuccessStatusCode)
                        {
                            using var doc = JsonDocument.Parse(await graphqlResp.Content.ReadAsStringAsync());
                            if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("nodes", out var nodes))
                            {
                                var nodesArray = nodes.EnumerateArray().ToList();

                                for (int nodeIndex = 0; nodeIndex < nodesArray.Count; nodeIndex++)
                                {
                                    var node = nodesArray[nodeIndex];

                                    if (node.ValueKind == JsonValueKind.Null)
                                    {
                                        // comment or discussion was deleted remotely -> remove it
                                        string deletedId = ids[nodeIndex];
                                        if (_communityCache.Subscriptions.Remove(deletedId))
                                        {
                                            SaveSystem.SaveCommunityCache(_communityCache);
                                        }
                                        continue;
                                    }

                                    if (node.TryGetProperty("discussion", out var discProp))
                                    {
                                        string? commentId = node.GetProperty("id").GetString();
                                        string? discId = discProp.GetProperty("id").GetString();

                                        string? parentAuthor = node.GetProperty("author").GetProperty("login").GetString();
                                        string? parentBody = node.GetProperty("body").GetString();

                                        // intercept bot messages to get real parent author
                                        if (parentAuthor == "aec-community-bot")
                                        {
                                            var match = parentBody != null ? System.Text.RegularExpressions.Regex.Match(parentBody, @"^<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline) : null;
                                            if (match != null && match.Success)
                                            {
                                                parentAuthor = match.Groups[1].Value;
                                                parentBody = match.Groups[2].Value; // extract body to clean the html tag
                                            }
                                        }

                                        var repliesData = node.GetProperty("replies");
                                        int newTotalCount = repliesData.GetProperty("totalCount").GetInt32();

                                        if (commentId != null && _communityCache.Subscriptions.TryGetValue(commentId, out int lastKnownCount))
                                        {
                                            if (newTotalCount > lastKnownCount)
                                            {
                                                int diff = newTotalCount - lastKnownCount;
                                                var replyNodes = repliesData.GetProperty("nodes").EnumerateArray().ToList();

                                                // only evaluate the newly added replies
                                                var newReplies = replyNodes.Skip(Math.Max(0, replyNodes.Count - diff)).ToList();

                                                foreach (var replyNode in newReplies)
                                                {
                                                    string? replyAuthor = replyNode.GetProperty("author").GetProperty("login").GetString();
                                                    string? replyBody = replyNode.GetProperty("body").GetString();

                                                    // intercept bot messages for the reply author too
                                                    if (replyAuthor == "aec-community-bot")
                                                    {
                                                        var match = replyBody != null ? System.Text.RegularExpressions.Regex.Match(replyBody, @"^<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline) : null;
                                                        if (match != null && match.Success)
                                                        {
                                                            replyAuthor = match.Groups[1].Value;
                                                            replyBody = match.Groups[2].Value; // extract body to clean the html tag
                                                        }
                                                    }

                                                    // use case-insensitive check
                                                    if (string.Equals(replyAuthor, AppSettings.GithubUsername, StringComparison.OrdinalIgnoreCase)) continue;

                                                    // check for our injected zero-width space mention or a normal text mention fallback
                                                    bool isAuthor = string.Equals(parentAuthor, AppSettings.GithubUsername, StringComparison.OrdinalIgnoreCase);
                                                    bool? isMentioned = replyBody != null ? replyBody.Contains($"@\u200B{AppSettings.GithubUsername}", StringComparison.OrdinalIgnoreCase) ||
                                                                       replyBody.Contains($"@{AppSettings.GithubUsername}", StringComparison.OrdinalIgnoreCase) : null;

                                                    if (!isAuthor && isMentioned != null && (bool)isMentioned)
                                                    {
                                                        _communityCache.Notifications.Add(new AppNotification
                                                        {
                                                            Message = $"{replyAuthor} hat dich in einer Antwort erwähnt.",
                                                            Date = DateTime.Now,
                                                            IsRead = false,
                                                            TargetDiscussionId = discId,
                                                            TargetCommentId = commentId,
                                                            TargetReplyId = replyNode.GetProperty("id").GetString()
                                                        });
                                                        hasNewNotifications = true;
                                                    }
                                                    else if (isAuthor)
                                                    {
                                                        _communityCache.Notifications.Add(new AppNotification
                                                        {
                                                            Message = $"{replyAuthor} hat auf deinen Kommentar geantwortet.",
                                                            Date = DateTime.Now,
                                                            IsRead = false,
                                                            TargetDiscussionId = discId,
                                                            TargetCommentId = commentId,
                                                            TargetReplyId = replyNode.GetProperty("id").GetString()
                                                        });
                                                        hasNewNotifications = true;
                                                    }
                                                }
                                                _communityCache.Subscriptions[commentId] = newTotalCount;
                                            }
                                        }
                                    }
                                    else if (node.TryGetProperty("comments", out var commentsData))
                                    {
                                        string? discId = node.GetProperty("id").GetString();

                                        int newTotalCount = commentsData.GetProperty("totalCount").GetInt32();

                                        if (discId != null && _communityCache.Subscriptions.TryGetValue(discId, out int lastKnownCount))
                                        {
                                            if (newTotalCount > lastKnownCount)
                                            {
                                                int diff = newTotalCount - lastKnownCount;
                                                var commentNodes = commentsData.GetProperty("nodes").EnumerateArray().ToList();
                                                var newComments = commentNodes.Skip(Math.Max(0, commentNodes.Count - diff)).ToList();

                                                foreach (var cNode in newComments)
                                                {
                                                    string? cAuthor = cNode.GetProperty("author").GetProperty("login").GetString();
                                                    string? cBody = cNode.GetProperty("body").GetString();

                                                    // intercept bot messages
                                                    if (cAuthor == "aec-community-bot")
                                                    {
                                                        var match = cBody != null ? System.Text.RegularExpressions.Regex.Match(cBody, @"^<!-- aec-author:\s*(.+?)\s*-->\r?\n?(.*)", System.Text.RegularExpressions.RegexOptions.Singleline) : null;
                                                        if (match != null && match.Success)
                                                        {
                                                            cAuthor = match.Groups[1].Value;
                                                        }
                                                    }

                                                    if (string.Equals(cAuthor, AppSettings.GithubUsername, StringComparison.OrdinalIgnoreCase)) continue;

                                                    _communityCache.Notifications.Add(new AppNotification
                                                    {
                                                        Message = $"{cAuthor} hat dein Level kommentiert.",
                                                        Date = DateTime.Now,
                                                        IsRead = false,
                                                        TargetDiscussionId = discId,
                                                        TargetCommentId = cNode.GetProperty("id").GetString(),
                                                        TargetReplyId = null
                                                    });
                                                    hasNewNotifications = true;
                                                }
                                                _communityCache.Subscriptions[discId] = newTotalCount;
                                            }
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

            if (hasNewNotifications || hasRemovedNotifications)
            {
                if (_inboxFlyout != null && _inboxFlyout.IsOpen)
                {
                    // mark as read if flyout already open (only if we actually have new notifications)
                    if (hasNewNotifications)
                    {
                        foreach (var n in _communityCache.Notifications) n.IsRead = true;
                    }
                    SaveSystem.SaveCommunityCache(_communityCache);
                    await Dispatcher.UIThread.InvokeAsync(() => { ShowInboxFlyout(); });
                }
                else
                {
                    SaveSystem.SaveCommunityCache(_communityCache);
                    await Dispatcher.UIThread.InvokeAsync(UpdateInboxUI);
                }
            }
        }
        finally
        {
            if (_isInitialNotificationCheckRunning)
            {
                _isInitialNotificationCheckRunning = false;
                _hasCompletedInitialNotificationCheck = true;
                await Dispatcher.UIThread.InvokeAsync(UpdateInboxUI);
            }
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

                if (!playerData.Settings.AreNotificationsPaused && unreadCount > 0)
                {
                    StopInboxAnimation();
                    BtnInbox.Content = LoadIcon("assets/icons/ic_inbox_filled.svg", 20);
                }
                else if (_isInitialNotificationCheckRunning && unreadCount == 0)
                {
                    StartInboxAnimation();
                }
                else
                {
                    StopInboxAnimation();
                    BtnInbox.Content = LoadIcon("assets/icons/ic_inbox.svg", 20);
                }
            }
            else
            {
                StopInboxAnimation();
            }
        }
    }

    private void StartInboxAnimation()
    {
        if (_inboxAnimationTimer != null && _inboxAnimationTimer.IsEnabled) return;

        _inboxAnimationStep = 0;
        _inboxAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _inboxAnimationTimer.Tick += (s, e) =>
        {
            if (BtnInbox == null) return;

            string iconName = _inboxFrames[_inboxAnimationStep];
            BtnInbox.Content = LoadIcon($"assets/icons/{iconName}", 20);

            _inboxAnimationStep = (_inboxAnimationStep + 1) % _inboxFrames.Length;
        };

        // set initial frame immediately
        BtnInbox.Content = LoadIcon($"assets/icons/{_inboxFrames[0]}", 20);
        _inboxAnimationStep = 1;
        _inboxAnimationTimer.Start();
    }

    private void StopInboxAnimation()
    {
        if (_inboxAnimationTimer != null)
        {
            _inboxAnimationTimer.Stop();
            _inboxAnimationTimer = null;
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
        SolidColorBrush activeColor = isPaused ? Scheme.BrushDeniedBg : Scheme.BrushTextTitle;

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
            Foreground = activeColor,
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
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnRefresh, "Aktualisieren");

        var refreshContentGrid = new Grid
        {
            Width = 18,
            Height = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var refreshIcon = LoadIcon("assets/icons/ic_refresh.svg", 18);
        var cooldownArc = new Avalonia.Controls.Shapes.Arc
        {
            Width = 18,
            Height = 18,
            StartAngle = -90,
            SweepAngle = 0,
            Stroke = Scheme.BrushTextHighlight,
            StrokeThickness = 3,
            IsVisible = false
        };

        refreshContentGrid.Children.Add(refreshIcon);
        refreshContentGrid.Children.Add(cooldownArc);

        DispatcherTimer? cooldownTimer = null;

        // smooth circular cooldown loop
        void StartCooldownUI()
        {
            cooldownTimer?.Stop();
            btnRefresh.IsEnabled = false;

            // hide icon entirely while showing the arc
            refreshIcon.IsVisible = false;
            cooldownArc.IsVisible = true;

            cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            double totalCooldown = 9.5;

            // prevent 1-frame flicker by setting the sweep angle immediately
            double initialElapsed = (DateTime.Now - _lastNotificationRefreshTime).TotalSeconds;
            double initialRemaining = 10.0 - initialElapsed;
            if (initialRemaining > 0)
            {
                cooldownArc.SweepAngle = (initialRemaining / totalCooldown) * 360;
            }

            cooldownTimer.Tick += (s, e) =>
            {
                double elapsedNow = (DateTime.Now - _lastNotificationRefreshTime).TotalSeconds;
                double remaining = 10.0 - elapsedNow;

                if (remaining <= 0)
                {
                    cooldownTimer.Stop();
                    btnRefresh.IsEnabled = true;

                    // restore original state
                    refreshIcon.IsVisible = true;
                    cooldownArc.IsVisible = false;
                    cooldownArc.SweepAngle = 0;
                }
                else
                {
                    // sweep from 360 down to 0
                    cooldownArc.SweepAngle = (remaining / totalCooldown) * 360;
                }
            };
            cooldownTimer.Start();
        }

        double secondsSinceLastRefresh = (DateTime.Now - _lastNotificationRefreshTime).TotalSeconds;

        if (secondsSinceLastRefresh < 0.5)
        {
            // currently in checkmark animation phase
            btnRefresh.Content = LoadIcon("assets/icons/ic_success.svg", 18);
            btnRefresh.Background = Scheme.BrushApprovedBg;
            btnRefresh.IsEnabled = false;

            Task.Run(async () => {
                double waitTime = 0.5 - secondsSinceLastRefresh;
                if (waitTime > 0) await Task.Delay((int)(waitTime * 1000));
                await Dispatcher.UIThread.InvokeAsync(() => {
                    btnRefresh.Content = refreshContentGrid;
                    btnRefresh.Background = Brushes.Transparent;
                    StartCooldownUI();
                });
            });
        }
        else if (secondsSinceLastRefresh < 10)
        {
            btnRefresh.Content = refreshContentGrid;
            StartCooldownUI();
        }
        else
        {
            btnRefresh.Content = refreshContentGrid;
        }

        btnRefresh.Click += async (s, e) =>
        {
            if ((DateTime.Now - _lastNotificationRefreshTime).TotalSeconds < 10) return; // 10s cooldown

            _lastNotificationRefreshTime = DateTime.Now;
            btnRefresh.IsEnabled = false;

            // temporarily show success icon and color
            btnRefresh.Content = LoadIcon("assets/icons/ic_success.svg", 18);
            btnRefresh.Background = Scheme.BrushApprovedBg;

            await Task.Delay(500);

            btnRefresh.Content = refreshContentGrid;
            btnRefresh.Background = Brushes.Transparent;
            StartCooldownUI();

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
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnUnsubscribeAll, $"Alle deabonnieren ({_communityCache.Subscriptions.Count}/100)");

        var btnDeleteAll = new Button
        {
            Content = LoadIcon("assets/icons/ic_delete_all.svg", 18),
            Background = Brushes.Transparent,
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnDeleteAll, "Alle Benachrichtigungen löschen");

        var btnSupport = new Button
        {
            Content = LoadIcon("assets/icons/ic_support.svg", 18),
            Background = Brushes.Transparent,
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(4)
        };
        ToolTip.SetTip(btnSupport, "Support & Hilfe");

        btnDeleteAll.Click += async (s, e) =>
        {
            _communityCache.Notifications.Clear();
            SaveSystem.SaveCommunityCache(_communityCache);

            // temporarily show success icon and color
            btnDeleteAll.Content = LoadIcon("assets/icons/ic_success.svg", 18);
            btnDeleteAll.Background = Scheme.BrushApprovedBg;
            await Task.Delay(500);

            ShowInboxFlyout(); // re-render
        };

        btnUnsubscribeAll.Click += async (s, e) =>
        {
            _communityCache.Subscriptions.Clear();
            SaveSystem.SaveCommunityCache(_communityCache);

            // temporarily show success icon and color
            btnUnsubscribeAll.Content = LoadIcon("assets/icons/ic_success.svg", 18);
            btnUnsubscribeAll.Background = Scheme.BrushApprovedBg;
            await Task.Delay(500);

            btnUnsubscribeAll.Content = LoadIcon("assets/icons/ic_unsubscribe.svg", 18);
            btnUnsubscribeAll.Background = Brushes.Transparent;
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
                    Background = Scheme.BrushBgPanel3,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    BorderBrush = Scheme.BrushBgPanel5,
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
                    IsVisible = !_isDesignerMode
                };
                ToolTip.SetTip(btnGo, "Gehe zum betroffenen Kommentar (möglicherweise inakkurat)");
                var btnDel = new Button
                {
                    Content = LoadIcon("assets/icons/ic_delete.svg", 16),
                    Background = Brushes.Transparent
                };
                ToolTip.SetTip(btnDel, "Benachrichtigung löschen");

                btnGo.Click += async (s, e) =>
                {
                    if (_isDesignerMode) return; // locked in level designer

                    if (!await CheckRealConnectivityAsync())
                    {
                        if (!_isKnownOffline)
                        {
                            _isKnownOffline = true;
                            await Dispatcher.UIThread.InvokeAsync(async () => await ShowOfflineBannerOnceAsync());
                        }
                        return;
                    }

                    if (!string.IsNullOrEmpty(notif.TargetDiscussionId))
                    {
                        string? targetLevelId = null;
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
                            bool isCustomLevel = int.TryParse(targetLevelId, out int parsedId) && parsedId < 0;

                            _inboxFlyout?.Hide();

                            // functionally and visually switch modes if differing
                            if (_isSqlMode != isTargetSql)
                            {
                                BtnModeSwitch_Click(this, new RoutedEventArgs());
                            }

                            _targetHighlightCommentId = notif.TargetCommentId;
                            _targetHighlightReplyId = notif.TargetReplyId;
                            _isSqlMode = isTargetSql;

                            if (isCustomLevel)
                            {
                                var customLevels = GetCustomLevels();
                                var targetLvl = customLevels.FirstOrDefault(cl =>
                                {
                                    int id = GetDeterministicHashCode(System.IO.Path.GetFileName(cl.FilePath));
                                    if (id > 0) id *= -1;
                                    return id.ToString() == targetLevelId;
                                });

                                // fallback: search by discussionNodeId if hashcode doesnt match (e.g. after app restart)
                                if (targetLvl == null)
                                {
                                    foreach (var cl in customLevels)
                                    {
                                        try
                                        {
                                            string json = File.ReadAllText(cl.FilePath);
                                            if (!json.TrimStart().StartsWith("{")) json = LevelEncryption.Decrypt(json);
                                            using (var doc = JsonDocument.Parse(json))
                                            {
                                                if (doc.RootElement.TryGetProperty("DiscussionNodeId", out var dNodeId) &&
                                                    dNodeId.GetString() == notif.TargetDiscussionId)
                                                {
                                                    targetLvl = cl;
                                                    int newId;
                                                    if (doc.RootElement.TryGetProperty("DiscussionNumber", out var dNum))
                                                    {
                                                        newId = -dNum.GetInt32();
                                                    }
                                                    else
                                                    {
                                                        newId = GetDeterministicHashCode(System.IO.Path.GetFileName(cl.FilePath));
                                                        if (newId > 0) newId *= -1;
                                                    }
                                                    targetLevelId = newId.ToString();
                                                    break;
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }

                                if (targetLvl != null)
                                {
                                    LoadCustomLevelFromFile(targetLvl.FilePath);
                                }
                                else
                                {
                                    // show error temporarily if deleted or not found locally
                                    _targetHighlightCommentId = null;
                                    _targetHighlightReplyId = null;
                                    btnGo.Content = LoadIcon("assets/icons/ic_error.svg", 16);
                                    _ = Task.Run(async () =>
                                    {
                                        await Task.Delay(500);
                                        await Dispatcher.UIThread.InvokeAsync(() => btnGo.Content = LoadIcon("assets/icons/ic_comment_go.svg", 16));
                                    });
                                    return;
                                }
                            }
                            else
                            {
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
                            }

                            await UpdateCommunityUIAsync(targetLevelId, isTargetSql, true);
                            PnlCommentsSection.IsVisible = true;

                            var dict = isTargetSql ? _communityCache.SqlDiscussions : _communityCache.CsharpDiscussions;
                            if (dict.TryGetValue(targetLevelId, out var cache))
                            {
                                int targetIdx = cache.Comments.FindIndex(c => c.Id == notif.TargetCommentId);
                                while (targetIdx == -1 && cache.HasNextPage)
                                {
                                    await FetchCommunityDataAsync(_currentActiveDiscussionId, isTargetSql, targetLevelId, true);
                                    targetIdx = cache.Comments.FindIndex(c => c.Id == notif.TargetCommentId);
                                }

                                if (targetIdx >= 0 && targetIdx >= _visibleCommentsCount)
                                {
                                    _visibleCommentsCount = targetIdx + 20;
                                }
                            }

                            RenderCachedComments();

                            _targetHighlightCommentId = null;
                            _targetHighlightReplyId = null;

                            ScrollToHighlightedComment();
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
            Background = Scheme.BrushTextNormal3,
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
            Text = "Sende mir eine Nachricht bezüglich Community-Features, Fehlern oder anderen Problemen.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var txtMessage = new TextBox
        {
            Watermark = "Deine Nachricht...",
            AcceptsReturn = true,
            MaxHeight = 100,
            Height = 80,
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.White,
            BorderBrush = Scheme.BrushBgPanel5,
            CornerRadius = new CornerRadius(4),
            Text = _draftSupportMessage // restore stored message
        };
        stack.Children.Add(txtMessage);

        txtMessage.TextChanged += (s, e) => _draftSupportMessage = txtMessage.Text ?? string.Empty;

        var txtError = new TextBlock
        {
            Foreground = Scheme.BrushDeniedFg,
            FontWeight = FontWeight.SemiBold,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(txtError);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = Scheme.BrushBgPanel2
        };

        // global 30s formspree cooldown check
        double secondsSinceLast = (DateTime.Now - DateTime.FromOADate(playerData.Settings.LastFormspreeTime)).TotalSeconds;

        var btnSend = new Button
        {
            Content = secondsSinceLast < 30 ? $"Warte {(int)(30 - secondsSinceLast)}s" : "Senden",
            Background = Scheme.BrushTextHighlight2,
            IsEnabled = secondsSinceLast >= 30
        };

        btnCancel.Click += (s, e) => dialog.Close();
        btnSend.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;

            if (txtMessage.Text.Length > 5000)
            {
                dialog.Height = 280;

                txtError.Text = "Die Nachricht darf maximal 5000 Zeichen lang sein.";
                txtError.IsVisible = true;
                return;
            }

            txtError.IsVisible = false;
            btnSend.IsEnabled = false;
            btnSend.Content = "Sende...";

            _draftSupportMessage = string.Empty;

            // update cooldown timestamp
            playerData.Settings.LastFormspreeTime = DateTime.Now.ToOADate();
            SaveSystem.Save(playerData);

            var payload = new
            {
                type = "Support Request",
                user = AppSettings.GithubUsername, // treat this as hint (not proof)
                message = txtMessage.Text
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                // attach the token so the receiving end can verify the identity if needed
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RenderEndpoint(0));
                requestMessage.Content = content;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                await _httpClient.SendAsync(requestMessage);
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

    private async void ShowReportDialog(string? targetId, string? author, string? discussionId, string? body, DateTime createdAt)
    {
        bool isPrankReady = false;
        bool isPranking = false;

        var dialog = new Window
        {
            Title = "Melden",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushTextNormal3,
            CornerRadius = new CornerRadius(8)
        };

        dialog.KeyDown += (s, ev) =>
        {
            // prevent escaping during the prank
            if (ev.Key == Key.Escape && !isPranking)
                dialog.Close();
        };

        var stack = new StackPanel
        {
            Spacing = 15,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };

        var txtTitle = new TextBlock
        {
            Text = $"Kommentar von {author} melden",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.Red
        };
        stack.Children.Add(txtTitle);

        var txtWarning = new TextBlock
        {
            Text = "Möchtest du diesen Kommentar wirklich melden? Ein Missbrauch dieser Funktion kann zum Ausschluss führen.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        };
        stack.Children.Add(txtWarning);

        if (_draftReportTargetUser != author)
        {
            _draftReportReason = string.Empty;
            _draftReportTargetUser = author;
        }

        var txtReason = new TextBox
        {
            Watermark = "Grund (optional)...",
            AcceptsReturn = true,
            MaxHeight = 60,
            Height = 60,
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.White,
            BorderBrush = Scheme.BrushBgPanel5,
            CornerRadius = new CornerRadius(4),
            Text = _draftReportReason // restore stored reason
        };
        stack.Children.Add(txtReason);

        txtReason.TextChanged += (s, e) => _draftReportReason = txtReason.Text ?? string.Empty;

        var txtError = new TextBlock
        {
            Foreground = Scheme.BrushDeniedFg,
            FontWeight = FontWeight.SemiBold,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        stack.Children.Add(txtError);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = Scheme.BrushBgPanel2
        };

        // global 30s formspree cooldown check
        double secondsSinceLast = (DateTime.Now - DateTime.FromOADate(playerData.Settings.LastFormspreeTime)).TotalSeconds;

        var btnReport = new Button
        {
            Content = secondsSinceLast < 30 ? $"Warte {(int)(30 - secondsSinceLast)}s" : "Melden",
            Background = Scheme.BrushDiffHard,
            IsEnabled = secondsSinceLast >= 30
        };

        btnCancel.Click += async (s, e) =>
        {
            if (isPrankReady && !isPranking)
            {
                isPranking = true;
                btnCancel.IsEnabled = false;

                // underline "kann zum Ausschluss führen"
                txtWarning.Text = null;
                txtWarning.Inlines = new Avalonia.Controls.Documents.InlineCollection
                {
                    new Avalonia.Controls.Documents.Run
                    {
                        Text = "Möchtest du diesen Kommentar wirklich melden? Ein Missbrauch dieser Funktion "
                    },
                    new Avalonia.Controls.Documents.Run
                    {
                        Text = "kann zum Ausschluss führen.",
                        TextDecorations = TextDecorations.Underline
                    }
                };

                await Task.Delay(2000);

                // change title to target the user themself
                txtTitle.Text = $"Kommentar von {AppSettings.GithubUsername} melden";

                await Task.Delay(1500);

                // clear reason textbox if it was typed in
                if (!string.IsNullOrEmpty(txtReason.Text))
                {
                    txtReason.Text = "";
                    await Task.Delay(1000);
                }

                // type our own reason
                string prankText = "soll perma ban kassieren";
                for (int i = 1; i <= prankText.Length; i++)
                {
                    txtReason.Text = prankText.Substring(0, i);
                    txtReason.CaretIndex = i;
                    await Task.Delay(40);
                }

                await Task.Delay(1500);

                // change button appearance
                btnCancel.Content = "Melden :D";
                btnCancel.Background = Scheme.BrushDiffHard;
                btnCancel.IsEnabled = true;

                await Task.Delay(1000);

                // simulate press
                btnCancel.Background = Scheme.BrushPressedDenialBg; // darker red
                await Task.Delay(200);

                dialog.Close();
                return;
            }

            if (!isPranking)
            {
                dialog.Close();
            }
        };

        btnReport.Click += async (s, e) =>
        {
            if (txtReason.Text?.Length > 5000)
            {
                dialog.Height = 280;

                txtError.Text = "Der Grund darf maximal 5000 Zeichen lang sein.";
                txtError.IsVisible = true;
                return;
            }

            if (author == "OnlyCook" || author == "aec-community-bot")
            {
                dialog.Height = 280;

                txtError.Text = author == "OnlyCook"
                    ? "Du kannst mich nicht an mich verpetzen."
                    : "Der Bot kann sich nicht selbst bannen.";

                txtError.IsVisible = true;
                btnReport.IsVisible = false;

                btnCancel.Content = "Ok :C";
                btnCancel.Background = Scheme.BrushTextTitle;

                if (author == "OnlyCook")
                {
                    isPrankReady = true;
                }

                return;
            }

            txtError.IsVisible = false;
            btnReport.IsEnabled = false;
            btnReport.Content = "Wird gesendet...";

            _draftReportReason = string.Empty;
            _draftReportTargetUser = string.Empty;

            // update cooldown timestamp
            playerData.Settings.LastFormspreeTime = DateTime.Now.ToOADate();
            SaveSystem.Save(playerData);

            var payload = new
            {
                type = "Report",
                reporter = AppSettings.GithubUsername, // treat this as a hint (not proof)
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

                // attach the token to prevent identity spoofing
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RenderEndpoint(0));
                requestMessage.Content = content;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                await _httpClient.SendAsync(requestMessage);
            }
            catch { }

            dialog.Close();
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnReport);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private void ScrollToHighlightedComment()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (PnlCommentsList == null || TaskScrollViewer == null) return;

            Control? FindHighlighted(Controls children)
            {
                foreach (var c in children)
                {
                    if (c is Border b && b.BorderThickness == new Thickness(2) && b.BorderBrush is SolidColorBrush sb && sb.Color == Scheme.BrushTextHighlight.Color)
                        return b;

                    if (c is Panel p)
                    {
                        var res = FindHighlighted(p.Children);
                        if (res != null) return res;
                    }
                    else if (c is Border b2 && b2.Child is Panel p2)
                    {
                        var res = FindHighlighted(p2.Children);
                        if (res != null) return res;
                    }
                }
                return null;
            }

            var targetControl = FindHighlighted(PnlCommentsList.Children);

            if (targetControl != null)
            {
                var transform = TaskScrollViewer.Content != null ? targetControl.TransformToVisual((Control)TaskScrollViewer.Content) : null;
                if (transform != null)
                {
                    double y = transform.Value.Transform(new Point(0, 0)).Y;
                    TaskScrollViewer.Offset = new Vector(TaskScrollViewer.Offset.X, Math.Max(0, y - 50));
                }
            }
        }, DispatcherPriority.Loaded);
    }

    private static byte _Unmask(byte b, int i, byte[] lut)
    {
        int r = (i % 3) + 1;
        b = (byte)((b >> r) | (b << (8 - r)));
        return (byte)(b ^ lut[i % lut.Length]);
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
            Background = Scheme.BrushTextNormal3,
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
            Foreground = Scheme.BrushDeniedFg
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
            Background = Scheme.BrushBgPanel2,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
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
            Background = Scheme.BrushTextNormal3,
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
            Foreground = Scheme.BrushHardPassFg
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Dein Account wurde permanent aus der Community ausgeschlossen. Du kannst Kommentare und Bewertungen weiterhin lesen, aber keine Aktionen mehr durchführen.\n\nWende dich an den Support, wenn du dir sehr sicher bist, dass dies ein Fehler ist.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var btnOk = new Button
        {
            Content = "Verstanden",
            Background = Scheme.BrushBgPanel2,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
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
        ClearApiQueue();
        ShowPermaBanDialog();
        return true;
    }

    private void AddOrUpdateSubscription(string? commentId, int count)
    {
        if (commentId == null) return;

        if (!_communityCache.Subscriptions.ContainsKey(commentId))
        {
            if (_communityCache.Subscriptions.Count >= SubscriptionCountLimit)
            {
                var oldest = _communityCache.Subscriptions.Keys.First();
                _communityCache.Subscriptions.Remove(oldest);
            }
        }
        _communityCache.Subscriptions[commentId] = count;
        SaveSystem.SaveCommunityCache(_communityCache);
    }

    private async Task ShowLevelDeletionDialogAsync(string discussionId, string? levelName)
    {
        if (levelName == null) return;

        bool isConfirmed = false;
        var confirmDialog = new Window
        {
            Title = "Level löschen",
            Width = 400,
            Height = 205,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushTextNormal3,
            CornerRadius = new CornerRadius(8)
        };

        var dGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto, Auto"),
            Margin = new Thickness(20)
        };
        dGrid.Children.Add(new TextBlock
        {
            Text = $"Möchtest du dieses Level wirklich online löschen? Diese Aktion kann nicht rückgängig gemacht werden.\n\nBitte tippe \"loeschen\" ein, um fortzufahren.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Top
        });

        var txtConfirm = new TextBox
        {
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 15, 0, 15)
        };
        Grid.SetRow(txtConfirm, 1);
        dGrid.Children.Add(txtConfirm);

        var dBtnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Grid.SetRow(dBtnPanel, 2);

        var btnCancelDel = new Button
        {
            Content = "Abbrechen",
            Background = Scheme.BrushBgPanel2,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4)
        };
        var btnConfirmDel = new Button
        {
            Content = "Endgültig Löschen",
            Background = Scheme.BrushDiffHard,
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(4),
            IsEnabled = false
        };

        txtConfirm.TextChanged += (s, e) => btnConfirmDel.IsEnabled = txtConfirm.Text == "loeschen";

        btnCancelDel.Click += (s, e) => confirmDialog.Close();
        btnConfirmDel.Click += (s, e) =>
        {
            isConfirmed = true;
            confirmDialog.Close();
        };

        dBtnPanel.Children.Add(btnCancelDel);
        dBtnPanel.Children.Add(btnConfirmDel);
        dGrid.Children.Add(dBtnPanel);
        confirmDialog.Content = dGrid;

        await confirmDialog.ShowDialog(this);
        if (!isConfirmed) return;

        double secondsSinceLast = (DateTime.Now - _lastLevelPublishTime).TotalSeconds;
        if (secondsSinceLast < 60)
        {
            if (_isSqlMode) AddSqlOutput("System", $"> Bitte warte {(int)(60 - secondsSinceLast)} Sekunden vor der nächsten Aktion.", Brushes.Orange);
            else AddToConsole($"\n> Bitte warte {(int)(60 - secondsSinceLast)} Sekunden vor der nächsten Aktion.", Brushes.Orange);
            return;
        }

        try
        {
            string endpoint = $"{RenderEndpoint(1).TrimEnd('/')}/level?discussionId={discussionId}";
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);

            var resp = await _httpClient.SendAsync(requestMessage);
            string resBody = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden && (resBody == "BANNED" || resBody == "PERMA_BANNED"))
            {
                if (resBody == "PERMA_BANNED") ShowPermaBanDialog(); else ShowBanDialog();
                return;
            }

            if (resp.IsSuccessStatusCode)
            {
                _lastLevelPublishTime = DateTime.Now;
                if (_isSqlMode) AddSqlOutput("System", "> Level erfolgreich online gelöscht!", Brushes.LightGreen);
                else AddToConsole("\n> Level erfolgreich online gelöscht!", Brushes.LightGreen);

                BtnCommunityDiscussionMenu.IsVisible = false;
                PnlCommunityActions.IsVisible = false;
                PnlCommentsSection.IsVisible = false;
            }
            else
            {
                if (_isSqlMode) AddSqlOutput("Error", $"> Fehler beim Löschen: {resBody}", Brushes.Red);
                else AddToConsole($"\n> Fehler beim Löschen: {resBody}", Brushes.Red);
            }
        }
        catch (Exception ex)
        {
            if (_isSqlMode) AddSqlOutput("Error", $"> Netzwerkfehler: {ex.Message}", Brushes.Red);
            else AddToConsole($"\n> Netzwerkfehler: {ex.Message}", Brushes.Red);
        }
    }

    private async void ShowLevelReportDialog(string levelName, string author, string discussionId)
    {
        var dialog = new Window
        {
            Title = "Level Melden",
            Width = 400,
            Height = 270,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Scheme.BrushTextNormal3,
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
            Text = $"Level '{levelName}'\nvon {author} melden",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.Red
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Möchtest du dieses Level wirklich melden? Ein Missbrauch dieser Funktion kann zum Ausschluss führen.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray
        });

        var txtReason = new TextBox
        {
            Watermark = "Grund (optional)...",
            AcceptsReturn = true,
            MaxHeight = 60,
            Height = 60,
            Background = Scheme.BrushBgPanel3,
            Foreground = Brushes.White,
            BorderBrush = Scheme.BrushBgPanel5,
            CornerRadius = new CornerRadius(4)
        };
        stack.Children.Add(txtReason);

        var txtError = new TextBlock
        {
            Foreground = Scheme.BrushDeniedFg,
            FontWeight = FontWeight.SemiBold,
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(txtError);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new Button
        {
            Content = "Abbrechen",
            Background = Scheme.BrushBgPanel2
        };

        double secondsSinceLast = (DateTime.Now - DateTime.FromOADate(playerData.Settings.LastFormspreeTime)).TotalSeconds;
        var btnReport = new Button
        {
            Content = secondsSinceLast < 30 ? $"Warte {(int)(30 - secondsSinceLast)}s" : "Melden",
            Background = Scheme.BrushDiffHard,
            IsEnabled = secondsSinceLast >= 30
        };

        btnCancel.Click += (s, e) => dialog.Close();
        btnReport.Click += async (s, e) =>
        {
            if (txtReason.Text?.Length > 5000)
            {
                dialog.Height = 300;
                txtError.Text = "Der Grund darf maximal 5000 Zeichen lang sein.";
                txtError.IsVisible = true;
                return;
            }

            txtError.IsVisible = false;
            btnReport.IsEnabled = false;
            btnReport.Content = "Wird gesendet...";

            playerData.Settings.LastFormspreeTime = DateTime.Now.ToOADate();
            SaveSystem.Save(playerData);

            var payload = new
            {
                type = "Level Report",
                reporter = AppSettings.GithubUsername,
                reportedUser = author,
                discussionId = discussionId,
                levelName = levelName,
                reason = txtReason.Text
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, RenderEndpoint(0));
                requestMessage.Content = content;
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSettings.GithubToken);
                await _httpClient.SendAsync(requestMessage);
            }
            catch { }

            dialog.Close();
        };

        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnReport);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private void StartCommentsRefreshHintTimer(double seconds)
    {
        _commentsRefreshHintTimer?.Stop();
        _commentsRefreshHintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _commentsRefreshHintTimer.Tick += (senderTimer, args) =>
        {
            _commentsRefreshHintTimer.Stop();
            if (PnlCommentsSection != null && PnlCommentsSection.IsVisible)
            {
                if (IconToggleComments != null)
                    IconToggleComments.Path = "/assets/icons/ic_comment_hide_old.svg";
                ToolTip.SetTip(BtnToggleComments, "Schließen und erneut öffnen, um neue Kommentare zu laden");
            }
        };
        _commentsRefreshHintTimer.Start();
    }
}
