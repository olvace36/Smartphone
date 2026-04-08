using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    public sealed class StardewConnectComment
    {
        public string Id { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public bool AuthorIsPlayer { get; set; }
        public string Text { get; set; } = "";
        public string Season { get; set; } = "spring";
        public int Day { get; set; } = 1;
        public int Year { get; set; } = 1;
        public int TimeOfDay { get; set; } = 600;
    }

    public sealed class StardewConnectPost
    {
        public string Id { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public bool AuthorIsPlayer { get; set; }
        public string Text { get; set; } = "";
        public string AttachedImageFile { get; set; } = "";
        public bool AttachmentFromPlayerFolder { get; set; }
        public List<StardewConnectPostAttachment> Attachments { get; set; } = new();
        public string Season { get; set; } = "spring";
        public int Day { get; set; } = 1;
        public int Year { get; set; } = 1;
        public int TimeOfDay { get; set; } = 600;
        public List<string> LikedBy { get; set; } = new();
        public List<StardewConnectComment> Comments { get; set; } = new();
        public int PlayerReadCommentCount { get; set; }
        public string PostTag { get; set; } = "";
    }

    public sealed class StardewConnectPostAttachment
    {
        public string ImageFile { get; set; } = "";
        public bool FromPlayerFolder { get; set; }
        public string ImageTag { get; set; } = "";
    }

    public sealed class StardewConnectVisitSnapshot
    {
        public string Season { get; set; } = "spring";
        public int Day { get; set; } = 1;
        public int Year { get; set; } = 1;
        public int TimeOfDay { get; set; } = 600;
    }

    public sealed class StardewConnectProfileStats
    {
        public string ActorName { get; set; } = "";
        public bool ActorIsPlayer { get; set; }
        public int TotalPosts { get; set; }
        public int TotalLikesReceived { get; set; }
        public int TotalCommentsReceived { get; set; }
        public int TotalLikesGiven { get; set; }
        public int TotalCommentsGiven { get; set; }
        public Dictionary<string, int> InteractionsFrom { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> InteractionsTo { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class StardewConnectProfileInteraction
    {
        public string ActorName { get; set; } = "";
        public bool ActorIsPlayer { get; set; }
        public int Count { get; set; }
    }

    public static class StardewConnectManager
    {
        private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;
        private const int MaxAttachmentsPerPost = 3;
        private const int DaysPerSeason = 28;
        private const int SeasonsPerYear = 4;
        private const int DaySortKeyMultiplier = 2000;
        private const string LastSocialVisitDataFileName = "stardewConnectLastVisit";

        private static IModHelper Helper => ModEntry.SHelper;

        public static List<StardewConnectPost> Posts { get; private set; } = new();
        public static Dictionary<string, StardewConnectProfileStats> ProfileStats { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        private static StardewConnectVisitSnapshot LastSocialVisitSnapshot { get; set; } = new();

        public static List<StardewConnectPost> GetPostsSnapshot()
        {
            return Posts.ToList();
        }

        public static StardewConnectVisitSnapshot GetLastSocialVisitSnapshot()
        {
            return new StardewConnectVisitSnapshot
            {
                Season = LastSocialVisitSnapshot.Season,
                Day = LastSocialVisitSnapshot.Day,
                Year = LastSocialVisitSnapshot.Year,
                TimeOfDay = LastSocialVisitSnapshot.TimeOfDay
            };
        }

        public static void MarkSocialAppVisitedNow()
        {
            LastSocialVisitSnapshot = CreateCurrentVisitSnapshot();
            SaveLastSocialVisitSnapshot();
        }

        public static bool IsPostOnOrAfterLastSocialVisit(StardewConnectPost post)
        {
            if (post == null)
                return false;

            return IsOnOrAfterLastSocialVisit(post.Season, post.Day, post.Year, post.TimeOfDay);
        }

        public static bool IsCommentOnOrAfterLastSocialVisit(StardewConnectComment comment)
        {
            if (comment == null)
                return false;

            return IsOnOrAfterLastSocialVisit(comment.Season, comment.Day, comment.Year, comment.TimeOfDay);
        }

        public static bool IsOnOrAfterLastSocialVisit(string season, int day, int year, int timeOfDay)
        {
            long compareKey = BuildChronologicalSortKey(season, day, year, timeOfDay);
            long visitKey = BuildChronologicalSortKey(
                LastSocialVisitSnapshot.Season,
                LastSocialVisitSnapshot.Day,
                LastSocialVisitSnapshot.Year,
                LastSocialVisitSnapshot.TimeOfDay);

            return compareKey > visitKey;
        }

        public static StardewConnectPost? GetPost(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return null;

            return Posts.FirstOrDefault(post => string.Equals(post.Id, postId, StringComparison.OrdinalIgnoreCase));
        }

        public static List<StardewConnectPost> GetPostsByAuthor(string actorName, bool actorIsPlayer)
        {
            string resolvedActorName = ResolveActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return new List<StardewConnectPost>();

            var result = new List<StardewConnectPost>();
            for (int i = Posts.Count - 1; i >= 0; i--)
            {
                StardewConnectPost post = Posts[i];
                if (post.AuthorIsPlayer != actorIsPlayer)
                    continue;

                if (!NameComparer.Equals(post.AuthorName, resolvedActorName))
                    continue;

                result.Add(post);
            }

            return result;
        }

        public static StardewConnectProfileStats GetProfileStatsSnapshot(string actorName, bool actorIsPlayer)
        {
            string key = BuildActorKey(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(key))
            {
                return new StardewConnectProfileStats
                {
                    ActorName = ResolveActorName(actorName, actorIsPlayer),
                    ActorIsPlayer = actorIsPlayer
                };
            }

            if (!ProfileStats.TryGetValue(key, out StardewConnectProfileStats? stats) || stats == null)
            {
                return new StardewConnectProfileStats
                {
                    ActorName = ResolveActorName(actorName, actorIsPlayer),
                    ActorIsPlayer = actorIsPlayer
                };
            }

            return CloneProfileStats(stats);
        }

        public static List<StardewConnectProfileInteraction> GetTopInteractionsFrom(string actorName, bool actorIsPlayer, int count = 3)
        {
            string key = BuildActorKey(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(key)
                || !ProfileStats.TryGetValue(key, out StardewConnectProfileStats? stats)
                || stats == null)
            {
                return new List<StardewConnectProfileInteraction>();
            }

            return BuildTopInteractionList(stats.InteractionsFrom, count);
        }

        public static List<StardewConnectProfileInteraction> GetTopInteractionsTo(string actorName, bool actorIsPlayer, int count = 3)
        {
            string key = BuildActorKey(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(key)
                || !ProfileStats.TryGetValue(key, out StardewConnectProfileStats? stats)
                || stats == null)
            {
                return new List<StardewConnectProfileInteraction>();
            }

            return BuildTopInteractionList(stats.InteractionsTo, count);
        }

        public static string? AddPlayerPost(string postText, string attachedImageFile = "")
        {
            string authorName = Game1.player?.Name ?? "Player";
            return AddPost(authorName, true, postText, attachedImageFile, attachmentFromPlayerFolder: true);
        }

        public static string? AddPlayerPostWithAttachments(string postText, IEnumerable<string>? attachedImageFiles)
        {
            string authorName = Game1.player?.Name ?? "Player";

            List<StardewConnectPostAttachment> attachments = (attachedImageFiles ?? Enumerable.Empty<string>())
                .Select(file => new StardewConnectPostAttachment
                {
                    ImageFile = NormalizeAttachmentFileName(file),
                    FromPlayerFolder = true
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.ImageFile))
                .ToList();

            return AddPost(
                authorName,
                true,
                postText,
                attachedImageFile: "",
                attachmentFromPlayerFolder: true,
                attachments: attachments);
        }

        public static string? AddNpcPost(string npcName, string postText, string attachedImageFile = "")
        {
            if (Game1.getCharacterFromName(npcName, mustBeVillager: false) == null)
                return null;

            return AddPost(npcName, false, postText, attachedImageFile, attachmentFromPlayerFolder: false);
        }

        public static string? AddNpcPostWithAttachments(string npcName, string postText, IEnumerable<string>? attachedImageFiles)
        {
            if (Game1.getCharacterFromName(npcName, mustBeVillager: false) == null)
                return null;
            List<StardewConnectPostAttachment> attachments = (attachedImageFiles ?? Enumerable.Empty<string>())
                .Select(file => new StardewConnectPostAttachment
                {
                    ImageFile = NormalizeAttachmentFileName(file),
                    FromPlayerFolder = false
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.ImageFile))
                .ToList();
            return AddPost(
                npcName,
                false,
                postText,
                attachedImageFile: "",
                attachmentFromPlayerFolder: false,
                attachments: attachments);
        }

        public static string? AddPost(
            string authorName,
            bool authorIsPlayer,
            string postText = "",
            string attachedImageFile = "",
            bool attachmentFromPlayerFolder = true,
            IEnumerable<StardewConnectPostAttachment>? attachments = null)
        {
            string resolvedAuthorName = (authorName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolvedAuthorName))
            {
                if (authorIsPlayer)
                    resolvedAuthorName = Game1.player?.Name ?? "Player";
                else
                    return null;
            }

            string text = (postText ?? "").Trim();
            List<StardewConnectPostAttachment> normalizedAttachments = NormalizeAttachments(
                attachments,
                attachedImageFile,
                attachmentFromPlayerFolder);

            if (string.IsNullOrWhiteSpace(text) && normalizedAttachments.Count == 0)
                return null;

            StardewConnectPostAttachment? firstAttachment = normalizedAttachments.FirstOrDefault();

            var post = new StardewConnectPost
            {
                Id = Guid.NewGuid().ToString("N"),
                AuthorName = resolvedAuthorName,
                AuthorIsPlayer = authorIsPlayer,
                Text = text,
                AttachedImageFile = firstAttachment?.ImageFile ?? "",
                AttachmentFromPlayerFolder = firstAttachment?.FromPlayerFolder ?? attachmentFromPlayerFolder,
                Attachments = normalizedAttachments,
                Season = GetCurrentSeason(),
                Day = Math.Max(1, Game1.dayOfMonth),
                Year = Math.Max(1, Game1.year),
                TimeOfDay = NormalizeTimeOfDay(Game1.timeOfDay),
                LikedBy = new List<string>(),
                Comments = new List<StardewConnectComment>(),
                PlayerReadCommentCount = 0
            };

            PopulateMissingAttachmentTags(post);
            RefreshPostTag(post);

            StardewConnectProfileStats authorStats = GetOrCreateProfileStats(resolvedAuthorName, authorIsPlayer);
            authorStats.TotalPosts++;

            Posts.Add(post);
            NotifyPhoneMenuDataChanged();
            return post.Id;
        }


        // Get random post IDs with various filters. If no matching posts are found, returns an empty list.
        public static List<string> GetRandomPostIdWithin1DayRange(int count = 1)
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(0, 1);

            if (recentPosts.Count == 0)
                return new List<string>();

            List<string> randomPostIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                StardewConnectPost randomPost = recentPosts[Game1.random.Next(recentPosts.Count)];
                randomPostIds.Add(randomPost.Id);
            }
            return randomPostIds;
        }

        public static List<string> GetRandomPostIdWithin3Days(int count = 1)
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(1, 3);

            if (recentPosts.Count == 0)
                return new List<string>();

            List<string> randomPostIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                StardewConnectPost randomPost = recentPosts[Game1.random.Next(recentPosts.Count)];
                randomPostIds.Add(randomPost.Id);
            }
            return randomPostIds;
        }

        public static List<string> GetRandomPostIdWithin7Days(int count = 1)
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(3, 7);

            if (recentPosts.Count == 0)
                return new List<string>();

            List<string> randomPostIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                StardewConnectPost randomPost = recentPosts[Game1.random.Next(recentPosts.Count)];
                randomPostIds.Add(randomPost.Id);
            }
            return randomPostIds;
        }

        public static string GetMostRecentPostId()
        {
            if (Posts.Count == 0)
                return "";

            StardewConnectPost mostRecentPost = Posts[Posts.Count - 1];
            return mostRecentPost.Id;
        }

        public static string GetMostPopularPostIdWithin1Day()
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(0, 1);

            if (recentPosts.Count == 0)
                return "";

            StardewConnectPost mostPopularPost = recentPosts.OrderByDescending(post => post.LikedBy.Count).First();
            return mostPopularPost.Id;
        }

        public static string GetMostPopularPostIdWithin3Days()
        {
            List<StardewConnectPost> recentPosts = GetPostsWithinPastDays(1, 3);

            if (recentPosts.Count == 0)
                return "";

            StardewConnectPost mostPopularPost = recentPosts.OrderByDescending(post => post.LikedBy.Count).First();
            return mostPopularPost.Id;
        }


        public static bool AddPlayerComment(string postId, string commentText)
        {
            string authorName = Game1.player?.Name ?? "Player";
            return AddComment(postId, authorName, true, commentText);
        }

        public static bool AddNpcComment(string postId, string npcName, string commentText)
        {
            if (Game1.getCharacterFromName(npcName, mustBeVillager: true) == null)
                return false;

            return AddComment(postId, npcName, false, commentText);
        }

        public static bool AddComment(string postId, string authorName, bool authorIsPlayer, string commentText)
        {
            StardewConnectPost? post = GetPost(postId);
            if (post == null)
                return false;

            string resolvedAuthorName = (authorName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolvedAuthorName))
            {
                if (authorIsPlayer)
                    resolvedAuthorName = Game1.player?.Name ?? "Player";
                else
                    return false;
            }

            string text = (commentText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var comment = new StardewConnectComment
            {
                Id = Guid.NewGuid().ToString("N"),
                AuthorName = resolvedAuthorName,
                AuthorIsPlayer = authorIsPlayer,
                Text = text,
                Season = GetCurrentSeason(),
                Day = Math.Max(1, Game1.dayOfMonth),
                Year = Math.Max(1, Game1.year),
                TimeOfDay = NormalizeTimeOfDay(Game1.timeOfDay)
            };

            post.Comments ??= new List<StardewConnectComment>();
            post.Comments.Add(comment);

            ApplyCommentStats(post, resolvedAuthorName, authorIsPlayer);

            if (authorIsPlayer)
            {
                post.PlayerReadCommentCount = post.Comments.Count;
            }
            else if (ModEntry.phoneMenu != null && ModEntry.phoneMenu.IsViewingSocialPost(postId))
            {
                post.PlayerReadCommentCount = post.Comments.Count;
            }

            NotifyPhoneMenuDataChanged();
            return true;
        }

        public static bool TogglePostLikeByPlayer(string postId)
        {
            string actorName = Game1.player?.Name ?? "Player";
            StardewConnectPost? post = GetPost(postId);
            if (post == null)
                return false;

            bool alreadyLiked = IsPostLikedBy(post, actorName);
            return SetPostLike(postId, actorName, !alreadyLiked);
        }

        public static bool SetPostLike(string postId, string actorName, bool liked)
        {
            StardewConnectPost? post = GetPost(postId);
            if (post == null)
                return false;

            string resolvedActorName = (actorName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return false;

            post.LikedBy ??= new List<string>();

            int index = post.LikedBy.FindIndex(name => NameComparer.Equals(name, resolvedActorName));
            bool changed = false;

            if (liked)
            {
                if (index < 0)
                {
                    post.LikedBy.Add(resolvedActorName);
                    changed = true;
                }
            }
            else
            {
                if (index >= 0)
                {
                    post.LikedBy.RemoveAt(index);
                    changed = true;
                }
            }

            if (changed)
            {
                bool actorIsPlayer = IsCurrentPlayerName(resolvedActorName);
                ApplyLikeStats(post, resolvedActorName, actorIsPlayer, added: liked);
                NotifyPhoneMenuDataChanged();
            }

            return true;
        }

        public static bool IsPostLikedByPlayer(StardewConnectPost post)
        {
            string actorName = Game1.player?.Name ?? "Player";
            return IsPostLikedBy(post, actorName);
        }

        public static bool IsPostLikedBy(StardewConnectPost post, string actorName)
        {
            if (post?.LikedBy == null || string.IsNullOrWhiteSpace(actorName))
                return false;

            return post.LikedBy.Any(name => NameComparer.Equals(name, actorName));
        }

        public static int GetUnreadCommentCount(StardewConnectPost post)
        {
            if (post == null)
                return 0;

            post.Comments ??= new List<StardewConnectComment>();
            int readCount = Math.Clamp(post.PlayerReadCommentCount, 0, post.Comments.Count);
            return Math.Max(0, post.Comments.Count - readCount);
        }

        public static int GetOldestUnreadPostIndex()
        {
            for (int i = 0; i < Posts.Count; i++)
            {
                if (GetUnreadCommentCount(Posts[i]) > 0)
                    return i;
            }

            return -1;
        }

        public static void MarkPostCommentsRead(string postId)
        {
            StardewConnectPost? post = GetPost(postId);
            if (post == null)
                return;

            post.Comments ??= new List<StardewConnectComment>();
            post.PlayerReadCommentCount = post.Comments.Count;
            NotifyPhoneMenuDataChanged();
        }

        public static int GetAttachmentCount(StardewConnectPost post)
        {
            if (post == null)
                return 0;

            EnsureAttachmentsMigrated(post);
            return post.Attachments.Count;
        }

        public static string GetAttachmentTag(StardewConnectPost post, int attachmentIndex)
        {
            if (post == null)
                return "";

            EnsureAttachmentsMigrated(post);
            PopulateMissingAttachmentTags(post);
            if (post.Attachments.Count == 0)
                return "";

            int safeIndex = Math.Clamp(attachmentIndex, 0, post.Attachments.Count - 1);
            StardewConnectPostAttachment attachment = post.Attachments[safeIndex];
            attachment.ImageTag = NormalizeAttachmentTag(attachment.ImageTag);
            RefreshPostTag(post);
            return attachment.ImageTag;
        }

        public static string GetPostTag(StardewConnectPost post)
        {
            if (post == null)
                return "";

            EnsureAttachmentsMigrated(post);
            PopulateMissingAttachmentTags(post);
            RefreshPostTag(post);
            return post.PostTag;
        }

        public static string ResolveAttachmentAbsolutePath(StardewConnectPost post)
        {
            return ResolveAttachmentAbsolutePath(post, 0);
        }

        public static string ResolveAttachmentAbsolutePath(StardewConnectPost post, int attachmentIndex)
        {
            if (post == null || string.IsNullOrWhiteSpace(post.AttachedImageFile))
            {
                if (post == null)
                    return "";
            }

            EnsureAttachmentsMigrated(post);
            if (post.Attachments.Count == 0)
                return "";

            int safeIndex = Math.Clamp(attachmentIndex, 0, post.Attachments.Count - 1);
            StardewConnectPostAttachment attachment = post.Attachments[safeIndex];
            if (string.IsNullOrWhiteSpace(attachment.ImageFile))
                return "";

            string imageFile = attachment.ImageFile.Trim();
            if (Path.IsPathRooted(imageFile))
                return imageFile;

            string saveFolder = string.IsNullOrWhiteSpace(Constants.SaveFolderName)
                ? "DefaultSave"
                : Constants.SaveFolderName;

            string baseDirectory = ModEntry.Instance?.Helper?.DirectoryPath
                ?? ModEntry.SHelper?.DirectoryPath
                ?? "";

            if (string.IsNullOrWhiteSpace(baseDirectory))
                return "";

            string folderName = attachment.FromPlayerFolder ? "player_photo" : "npc_photo";
            return Path.Combine(baseDirectory, "userdata", saveFolder, folderName, imageFile);
        }

        public static void SaveData()
        {
            PrunePosts(GetMaxPostCount());
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/stardewConnectPosts", Posts);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/stardewConnectProfileStats", ProfileStats);
            SaveLastSocialVisitSnapshot();
        }

        public static void LoadData()
        {
            Posts = Helper.Data.ReadJsonFile<List<StardewConnectPost>>($"./userdata/{Constants.SaveFolderName}/stardewConnectPosts")
                ?? new List<StardewConnectPost>();

            Dictionary<string, StardewConnectProfileStats> loadedProfileStats =
                Helper.Data.ReadJsonFile<Dictionary<string, StardewConnectProfileStats>>($"./userdata/{Constants.SaveFolderName}/stardewConnectProfileStats")
                ?? new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase);

            LastSocialVisitSnapshot =
                Helper.Data.ReadJsonFile<StardewConnectVisitSnapshot>($"./userdata/{Constants.SaveFolderName}/{LastSocialVisitDataFileName}")
                ?? CreateCurrentVisitSnapshot();

            ProfileStats = new Dictionary<string, StardewConnectProfileStats>(loadedProfileStats, StringComparer.OrdinalIgnoreCase);

            SanitizeLoadedData();
            SanitizeLoadedProfileStats();
            SanitizeVisitSnapshot(LastSocialVisitSnapshot);
            PrunePosts(GetMaxPostCount());

            if (ProfileStats.Count == 0)
                RebuildProfileStatsFromPosts();
        }

        private static void SanitizeLoadedData()
        {
            foreach (StardewConnectPost post in Posts)
            {
                post.Id = string.IsNullOrWhiteSpace(post.Id) ? Guid.NewGuid().ToString("N") : post.Id;
                post.AuthorName = post.AuthorName ?? "";
                post.Text = post.Text ?? "";
                post.AttachedImageFile = post.AttachedImageFile ?? "";
                post.Season = string.IsNullOrWhiteSpace(post.Season) ? "spring" : post.Season;
                post.Day = Math.Max(1, post.Day);
                post.Year = Math.Max(1, post.Year);
                post.TimeOfDay = NormalizeTimeOfDay(post.TimeOfDay);

                post.LikedBy ??= new List<string>();
                post.Comments ??= new List<StardewConnectComment>();
                post.Attachments ??= new List<StardewConnectPostAttachment>();
                post.PostTag = post.PostTag ?? "";

                EnsureAttachmentsMigrated(post);
                PopulateMissingAttachmentTags(post);
                RefreshPostTag(post);

                // Keep like names unique while preserving original order.
                var dedupedLikes = new List<string>();
                foreach (string name in post.LikedBy)
                {
                    string normalized = (name ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(normalized))
                        continue;

                    if (!dedupedLikes.Any(existing => NameComparer.Equals(existing, normalized)))
                        dedupedLikes.Add(normalized);
                }
                post.LikedBy = dedupedLikes;

                foreach (StardewConnectComment comment in post.Comments)
                {
                    comment.Id = string.IsNullOrWhiteSpace(comment.Id) ? Guid.NewGuid().ToString("N") : comment.Id;
                    comment.AuthorName = comment.AuthorName ?? "";
                    comment.Text = comment.Text ?? "";
                    comment.Season = string.IsNullOrWhiteSpace(comment.Season) ? "spring" : comment.Season;
                    comment.Day = Math.Max(1, comment.Day);
                    comment.Year = Math.Max(1, comment.Year);
                    comment.TimeOfDay = NormalizeTimeOfDay(comment.TimeOfDay);
                }

                post.PlayerReadCommentCount = Math.Clamp(post.PlayerReadCommentCount, 0, post.Comments.Count);

                if (post.Attachments.Count > 0)
                {
                    post.AttachedImageFile = post.Attachments[0].ImageFile;
                    post.AttachmentFromPlayerFolder = post.Attachments[0].FromPlayerFolder;
                }
                else
                {
                    post.AttachedImageFile = "";
                }
            }
        }

        private static void PrunePosts(int maxPosts)
        {
            int safeLimit = Math.Max(1, maxPosts);
            if (Posts.Count <= safeLimit)
                return;

            int removeCount = Posts.Count - safeLimit;
            Posts.RemoveRange(0, removeCount);
        }

        private static int GetMaxPostCount()
        {
            return Math.Clamp(ModEntry.Config?.MaxStardewConnectPosts ?? 100, 10, 500);
        }

        private static int NormalizeTimeOfDay(int rawTime)
        {
            if (rawTime <= 0)
                return 600;

            int hour = rawTime / 100;
            int minute = rawTime % 100;

            if (hour < 0)
                hour = 0;
            if (hour > 26)
                hour = 26;

            minute = Math.Clamp(minute, 0, 59);
            return (hour * 100) + minute;
        }

        private static StardewConnectVisitSnapshot CreateCurrentVisitSnapshot()
        {
            return new StardewConnectVisitSnapshot
            {
                Season = GetCurrentSeason(),
                Day = Math.Max(1, Game1.dayOfMonth),
                Year = Math.Max(1, Game1.year),
                TimeOfDay = NormalizeTimeOfDay(Game1.timeOfDay)
            };
        }

        private static void SanitizeVisitSnapshot(StardewConnectVisitSnapshot snapshot)
        {
            if (snapshot == null)
            {
                LastSocialVisitSnapshot = CreateCurrentVisitSnapshot();
                return;
            }

            snapshot.Season = string.IsNullOrWhiteSpace(snapshot.Season) ? "spring" : snapshot.Season;
            snapshot.Day = Math.Max(1, snapshot.Day);
            snapshot.Year = Math.Max(1, snapshot.Year);
            snapshot.TimeOfDay = NormalizeTimeOfDay(snapshot.TimeOfDay);
        }

        private static void SaveLastSocialVisitSnapshot()
        {
            if (Helper == null)
                return;

            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/{LastSocialVisitDataFileName}", LastSocialVisitSnapshot);
        }

        private static string GetCurrentSeason()
        {
            string season = Game1.currentSeason;
            if (string.IsNullOrWhiteSpace(season))
                return "spring";

            return season;
        }

        private static int GetSeasonIndex(string season)
        {
            if (string.IsNullOrWhiteSpace(season))
                return -1;

            return season.Trim().ToLowerInvariant() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => -1
            };
        }

        private static List<StardewConnectPost> GetPostsWithinPastDays(int startDay, int endDays)
        {
            if (Posts.Count == 0 || startDay < 0 || endDays < 0 || endDays < startDay)
                return new List<StardewConnectPost>();

            int currentDayIndex = GetAbsoluteDayIndex(Game1.currentSeason, Game1.dayOfMonth, Game1.year);
            int startDayIndex = currentDayIndex - startDay;
            int endDayIndex = currentDayIndex - endDays;

            return Posts.Where(post =>
            {
                int postDayIndex = GetAbsoluteDayIndex(post.Season, post.Day, post.Year);
                return postDayIndex >= endDayIndex && postDayIndex <= startDayIndex;
            }).ToList();
        }
        // {
        //     if (Posts.Count == 0 || maxDays < 0)
        //         return new List<StardewConnectPost>();

        //     int currentDayIndex = GetAbsoluteDayIndex(Game1.currentSeason, Game1.dayOfMonth, Game1.year);

        //     return Posts.Where(post =>
        //     {
        //         int postDayIndex = GetAbsoluteDayIndex(post.Season, post.Day, post.Year);
        //         int dayDelta = currentDayIndex - postDayIndex;
        //         return dayDelta >= 0 && dayDelta <= maxDays;
        //     }).ToList();
        // }

        private static int GetAbsoluteDayIndex(string season, int day, int year)
        {
            int seasonIndex = GetSeasonIndex(season);
            if (seasonIndex < 0)
                seasonIndex = 0;

            int safeDay = Math.Clamp(day, 1, DaysPerSeason);
            int safeYear = Math.Max(1, year);

            return ((safeYear - 1) * SeasonsPerYear * DaysPerSeason)
                + (seasonIndex * DaysPerSeason)
                + (safeDay - 1);
        }

        private static long BuildChronologicalSortKey(string season, int day, int year, int timeOfDay)
        {
            int dayIndex = GetAbsoluteDayIndex(season, day, year);
            int normalizedTimeOfDay = NormalizeTimeOfDay(timeOfDay);
            int hour = normalizedTimeOfDay / 100;
            int minute = normalizedTimeOfDay % 100;
            int totalMinutes = (hour * 60) + minute;

            return ((long)dayIndex * DaySortKeyMultiplier) + totalMinutes;
        }

        private static string NormalizeAttachmentFileName(string attachedImageFile)
        {
            if (string.IsNullOrWhiteSpace(attachedImageFile))
                return "";

            string fileName = Path.GetFileName(attachedImageFile.Trim());
            return fileName ?? "";
        }

        private static string NormalizeAttachmentTag(string imageTag)
        {
            return string.Join(";", SplitTagParts(imageTag));
        }

        private static IReadOnlyList<string> SplitTagParts(string tagText)
        {
            if (string.IsNullOrWhiteSpace(tagText))
                return Array.Empty<string>();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parts = new List<string>();
            foreach (string rawPart in tagText.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string normalizedPart = (rawPart ?? "").Trim();
                if (string.IsNullOrWhiteSpace(normalizedPart))
                    continue;

                if (seen.Add(normalizedPart))
                    parts.Add(normalizedPart);
            }

            return parts;
        }

        private static string ResolveAttachmentTag(string imageFile)
        {
            string fileName = NormalizeAttachmentFileName(imageFile);
            if (string.IsNullOrWhiteSpace(fileName) || ModEntry.ImageTags == null)
                return "";

            if (!ModEntry.ImageTags.TryGetValue(fileName, out string? imageTag))
                return "";

            return NormalizeAttachmentTag(imageTag ?? "");
        }

        private static void PopulateMissingAttachmentTags(StardewConnectPost post)
        {
            if (post?.Attachments == null || post.Attachments.Count == 0)
                return;

            for (int i = 0; i < post.Attachments.Count; i++)
            {
                StardewConnectPostAttachment attachment = post.Attachments[i];
                if (attachment == null)
                    continue;

                attachment.ImageTag = NormalizeAttachmentTag(attachment.ImageTag);
                if (!string.IsNullOrWhiteSpace(attachment.ImageTag))
                    continue;

                attachment.ImageTag = ResolveAttachmentTag(attachment.ImageFile);
            }
        }

        private static void RefreshPostTag(StardewConnectPost post)
        {
            if (post == null)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mergedParts = new List<string>();

            foreach (string part in SplitTagParts(post.PostTag))
            {
                if (seen.Add(part))
                    mergedParts.Add(part);
            }

            if (post.Attachments != null)
            {
                foreach (StardewConnectPostAttachment attachment in post.Attachments)
                {
                    if (attachment == null)
                        continue;

                    attachment.ImageTag = NormalizeAttachmentTag(attachment.ImageTag);
                    foreach (string part in SplitTagParts(attachment.ImageTag))
                    {
                        if (seen.Add(part))
                            mergedParts.Add(part);
                    }
                }
            }

            post.PostTag = string.Join(";", mergedParts);
        }

        private static List<StardewConnectPostAttachment> NormalizeAttachments(
            IEnumerable<StardewConnectPostAttachment>? attachments,
            string fallbackAttachedImageFile,
            bool fallbackFromPlayerFolder)
        {
            var normalized = new List<StardewConnectPostAttachment>();

            if (attachments != null)
            {
                foreach (StardewConnectPostAttachment attachment in attachments)
                {
                    if (attachment == null)
                        continue;

                    string file = NormalizeAttachmentFileName(attachment.ImageFile);
                    if (string.IsNullOrWhiteSpace(file))
                        continue;

                    bool exists = normalized.Any(item =>
                        item.FromPlayerFolder == attachment.FromPlayerFolder
                        && NameComparer.Equals(item.ImageFile, file));

                    if (exists)
                        continue;

                    normalized.Add(new StardewConnectPostAttachment
                    {
                        ImageFile = file,
                        FromPlayerFolder = attachment.FromPlayerFolder,
                        ImageTag = NormalizeAttachmentTag(attachment.ImageTag)
                    });

                    if (normalized.Count >= MaxAttachmentsPerPost)
                        break;
                }
            }

            if (normalized.Count == 0)
            {
                string fallbackFile = NormalizeAttachmentFileName(fallbackAttachedImageFile);
                if (!string.IsNullOrWhiteSpace(fallbackFile))
                {
                    normalized.Add(new StardewConnectPostAttachment
                    {
                        ImageFile = fallbackFile,
                        FromPlayerFolder = fallbackFromPlayerFolder,
                        ImageTag = ""
                    });
                }
            }

            return normalized;
        }

        private static void EnsureAttachmentsMigrated(StardewConnectPost post)
        {
            post.Attachments ??= new List<StardewConnectPostAttachment>();

            if (post.Attachments.Count == 0)
            {
                string legacyFile = NormalizeAttachmentFileName(post.AttachedImageFile);
                if (!string.IsNullOrWhiteSpace(legacyFile))
                {
                    post.Attachments.Add(new StardewConnectPostAttachment
                    {
                        ImageFile = legacyFile,
                        FromPlayerFolder = post.AttachmentFromPlayerFolder,
                        ImageTag = ""
                    });
                }
            }

            post.Attachments = NormalizeAttachments(
                post.Attachments,
                fallbackAttachedImageFile: "",
                fallbackFromPlayerFolder: post.AttachmentFromPlayerFolder);
        }

        private static StardewConnectProfileStats GetOrCreateProfileStats(string actorName, bool actorIsPlayer)
        {
            string key = BuildActorKey(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(key))
            {
                return new StardewConnectProfileStats
                {
                    ActorName = ResolveActorName(actorName, actorIsPlayer),
                    ActorIsPlayer = actorIsPlayer
                };
            }

            if (!ProfileStats.TryGetValue(key, out StardewConnectProfileStats? stats) || stats == null)
            {
                stats = new StardewConnectProfileStats
                {
                    ActorName = ResolveActorName(actorName, actorIsPlayer),
                    ActorIsPlayer = actorIsPlayer,
                    InteractionsFrom = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                    InteractionsTo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                };
                ProfileStats[key] = stats;
            }
            else
            {
                stats.ActorName = ResolveActorName(stats.ActorName, stats.ActorIsPlayer);
                stats.InteractionsFrom = stats.InteractionsFrom != null
                    ? new Dictionary<string, int>(stats.InteractionsFrom, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                stats.InteractionsTo = stats.InteractionsTo != null
                    ? new Dictionary<string, int>(stats.InteractionsTo, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            return stats;
        }

        private static void ApplyLikeStats(StardewConnectPost post, string actorName, bool actorIsPlayer, bool added)
        {
            if (post == null)
                return;

            string resolvedActorName = ResolveActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return;

            StardewConnectProfileStats sourceStats = GetOrCreateProfileStats(resolvedActorName, actorIsPlayer);
            StardewConnectProfileStats targetStats = GetOrCreateProfileStats(post.AuthorName, post.AuthorIsPlayer);

            int delta = added ? 1 : -1;
            sourceStats.TotalLikesGiven = Math.Max(0, sourceStats.TotalLikesGiven + delta);
            targetStats.TotalLikesReceived = Math.Max(0, targetStats.TotalLikesReceived + delta);

            string sourceKey = BuildActorKey(resolvedActorName, actorIsPlayer);
            string targetKey = BuildActorKey(post.AuthorName, post.AuthorIsPlayer);
            if (!string.IsNullOrWhiteSpace(sourceKey)
                && !string.IsNullOrWhiteSpace(targetKey)
                && !string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                ApplyInteractionDelta(sourceStats.InteractionsTo, targetKey, delta);
                ApplyInteractionDelta(targetStats.InteractionsFrom, sourceKey, delta);
            }
        }

        private static void ApplyCommentStats(StardewConnectPost post, string actorName, bool actorIsPlayer)
        {
            if (post == null)
                return;

            string resolvedActorName = ResolveActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return;

            StardewConnectProfileStats sourceStats = GetOrCreateProfileStats(resolvedActorName, actorIsPlayer);
            StardewConnectProfileStats targetStats = GetOrCreateProfileStats(post.AuthorName, post.AuthorIsPlayer);

            sourceStats.TotalCommentsGiven++;
            targetStats.TotalCommentsReceived++;

            string sourceKey = BuildActorKey(resolvedActorName, actorIsPlayer);
            string targetKey = BuildActorKey(post.AuthorName, post.AuthorIsPlayer);
            if (!string.IsNullOrWhiteSpace(sourceKey)
                && !string.IsNullOrWhiteSpace(targetKey)
                && !string.Equals(sourceKey, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                ApplyInteractionDelta(sourceStats.InteractionsTo, targetKey, 1);
                ApplyInteractionDelta(targetStats.InteractionsFrom, sourceKey, 1);
            }
        }

        private static void ApplyInteractionDelta(Dictionary<string, int> map, string key, int delta)
        {
            if (map == null || string.IsNullOrWhiteSpace(key) || delta == 0)
                return;

            map.TryGetValue(key, out int currentValue);
            int next = Math.Max(0, currentValue + delta);
            if (next <= 0)
                map.Remove(key);
            else
                map[key] = next;
        }

        private static string ResolveActorName(string actorName, bool actorIsPlayer)
        {
            string resolved = (actorName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolved) && actorIsPlayer)
                resolved = Game1.player?.Name ?? "Player";

            return resolved;
        }

        private static bool IsCurrentPlayerName(string actorName)
        {
            string currentPlayerName = Game1.player?.Name ?? "Player";
            return NameComparer.Equals(currentPlayerName, (actorName ?? "").Trim());
        }

        private static string BuildActorKey(string actorName, bool actorIsPlayer)
        {
            string resolved = ResolveActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolved))
                return "";

            return (actorIsPlayer ? "P|" : "N|") + resolved;
        }

        private static bool TryParseActorKey(string actorKey, out string actorName, out bool actorIsPlayer)
        {
            actorName = "";
            actorIsPlayer = false;

            if (string.IsNullOrWhiteSpace(actorKey) || actorKey.Length < 3)
                return false;

            actorIsPlayer = actorKey.StartsWith("P|", StringComparison.OrdinalIgnoreCase);
            if (!actorIsPlayer && !actorKey.StartsWith("N|", StringComparison.OrdinalIgnoreCase))
                return false;

            actorName = actorKey.Substring(2).Trim();
            return !string.IsNullOrWhiteSpace(actorName);
        }

        private static List<StardewConnectProfileInteraction> BuildTopInteractionList(Dictionary<string, int>? interactions, int count)
        {
            if (interactions == null || interactions.Count == 0 || count <= 0)
                return new List<StardewConnectProfileInteraction>();

            var result = new List<StardewConnectProfileInteraction>();
            foreach ((string actorKey, int value) in interactions)
            {
                if (value <= 0)
                    continue;

                if (!TryParseActorKey(actorKey, out string actorName, out bool actorIsPlayer))
                    continue;

                result.Add(new StardewConnectProfileInteraction
                {
                    ActorName = actorName,
                    ActorIsPlayer = actorIsPlayer,
                    Count = value
                });
            }

            return result
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.ActorName, StringComparer.OrdinalIgnoreCase)
                .Take(count)
                .ToList();
        }

        private static StardewConnectProfileStats CloneProfileStats(StardewConnectProfileStats stats)
        {
            return new StardewConnectProfileStats
            {
                ActorName = stats.ActorName,
                ActorIsPlayer = stats.ActorIsPlayer,
                TotalPosts = stats.TotalPosts,
                TotalLikesReceived = stats.TotalLikesReceived,
                TotalCommentsReceived = stats.TotalCommentsReceived,
                TotalLikesGiven = stats.TotalLikesGiven,
                TotalCommentsGiven = stats.TotalCommentsGiven,
                InteractionsFrom = new Dictionary<string, int>(stats.InteractionsFrom ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase),
                InteractionsTo = new Dictionary<string, int>(stats.InteractionsTo ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase)
            };
        }

        private static void SanitizeLoadedProfileStats()
        {
            ProfileStats ??= new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase);

            var normalized = new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, StardewConnectProfileStats? rawStats) in ProfileStats)
            {
                StardewConnectProfileStats stats = rawStats ?? new StardewConnectProfileStats();
                string actorName = (stats.ActorName ?? "").Trim();
                bool actorIsPlayer = stats.ActorIsPlayer;

                if (string.IsNullOrWhiteSpace(actorName)
                    && TryParseActorKey(key, out string parsedName, out bool parsedIsPlayer))
                {
                    actorName = parsedName;
                    actorIsPlayer = parsedIsPlayer;
                }

                actorName = ResolveActorName(actorName, actorIsPlayer);
                if (string.IsNullOrWhiteSpace(actorName))
                    continue;

                string normalizedKey = BuildActorKey(actorName, actorIsPlayer);
                if (string.IsNullOrWhiteSpace(normalizedKey))
                    continue;

                if (!normalized.TryGetValue(normalizedKey, out StardewConnectProfileStats? target) || target == null)
                {
                    target = new StardewConnectProfileStats
                    {
                        ActorName = actorName,
                        ActorIsPlayer = actorIsPlayer,
                        InteractionsFrom = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                        InteractionsTo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    };
                    normalized[normalizedKey] = target;
                }

                target.TotalPosts += Math.Max(0, stats.TotalPosts);
                target.TotalLikesReceived += Math.Max(0, stats.TotalLikesReceived);
                target.TotalCommentsReceived += Math.Max(0, stats.TotalCommentsReceived);
                target.TotalLikesGiven += Math.Max(0, stats.TotalLikesGiven);
                target.TotalCommentsGiven += Math.Max(0, stats.TotalCommentsGiven);

                MergeInteractionMap(target.InteractionsFrom, stats.InteractionsFrom);
                MergeInteractionMap(target.InteractionsTo, stats.InteractionsTo);
            }

            ProfileStats = normalized;
        }

        private static void MergeInteractionMap(Dictionary<string, int> target, Dictionary<string, int>? source)
        {
            if (target == null || source == null)
                return;

            foreach ((string actorKey, int value) in source)
            {
                if (value <= 0)
                    continue;

                if (!TryParseActorKey(actorKey, out string actorName, out bool actorIsPlayer))
                    continue;

                string normalizedKey = BuildActorKey(actorName, actorIsPlayer);
                if (string.IsNullOrWhiteSpace(normalizedKey))
                    continue;

                target.TryGetValue(normalizedKey, out int currentValue);
                target[normalizedKey] = currentValue + value;
            }
        }

        private static void RebuildProfileStatsFromPosts()
        {
            ProfileStats = new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase);

            foreach (StardewConnectPost post in Posts)
            {
                StardewConnectProfileStats postAuthorStats = GetOrCreateProfileStats(post.AuthorName, post.AuthorIsPlayer);
                postAuthorStats.TotalPosts++;

                post.LikedBy ??= new List<string>();
                foreach (string likeActorName in post.LikedBy)
                {
                    string resolvedActorName = (likeActorName ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(resolvedActorName))
                        continue;

                    bool actorIsPlayer = IsCurrentPlayerName(resolvedActorName);
                    ApplyLikeStats(post, resolvedActorName, actorIsPlayer, added: true);
                }

                post.Comments ??= new List<StardewConnectComment>();
                foreach (StardewConnectComment comment in post.Comments)
                {
                    ApplyCommentStats(post, comment.AuthorName, comment.AuthorIsPlayer);
                }
            }
        }

        private static void NotifyPhoneMenuDataChanged()
        {
            if (ModEntry.phoneMenu == null)
                return;

            ModEntry.phoneMenu.OnStardewConnectDataChanged();
        }
    }
}
