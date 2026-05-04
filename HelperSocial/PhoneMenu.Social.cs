using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        private const string SocialAppState = "appSocial";

        private const int SocialViewportYOffset = 126;
        private const int SocialViewportHeight = 780;
        private const int SocialPostCardX = 50;
        private const int SocialPostCardWidth = 500;
        private const int SocialPostSpacing = 14;
        private const int SocialPostTopPadding = 10;
        private const int SocialPostBottomPadding = 10;
        private const int SocialActorIconSize = 56;
        private const int SocialCommentIconSize = 30;
        private const int SocialPostTextMaxWidth = 450;
        private const int SocialCommentPreviewCount = 2;
        private const int SocialCreateSelectionMaxCount = 3;
        private const int SocialImageNavButtonSize = 40;
        private const int SocialImageMaxWidth = SocialPostCardWidth - 30;
        private const int SocialImageMaxHeight = 410;
        private const float SocialPortraitImageMaxWidthRatio = 2f / 3f;
        private static float SocialPostTextScale = 0.9f;
        private static float SocialCommentTextScale = 0.9f;
        private const float SocialImageTagScale = 0.68f;
        private const int SocialImageTagBottomPadding = 6;
        private const float SocialScrollPixelsPerWheelNotch = 52f;
        private const float SocialScrollLerpSpeed = 16f;
        private const float SocialHeaderMetaScale = 0.72f;
        private const float SocialHeaderMetaYOffset = 5f;
        private const int SocialDetailInputX = 50;
        private const int SocialDetailInputY = 850;
        private const int SocialDetailInputWidth = 430;
        private const int SocialDetailInputBottomGap = 12;
        private const int SocialProfileAvatarHeight = 210;
        private const int SocialProfileStatsHeight = 135;
        private const int SocialProfileInteractionHeight = 178;
        private const int SocialProfileSectionSpacing = 12;
        private const int SocialProfilePostsHeaderHeight = 46;
        private const int SocialProfileStatIconSize = 18;
        private const int SocialLikeTooltipMaxNames = 5;
        private const int SocialNotificationButtonSize = 40;
        private const int SocialNotificationCardPadding = 14;
        private const int SocialNotificationCardVerticalPadding = 12;
        private const int SocialNotificationCardWidth = 500;
        private const int SocialNotificationCardSpacing = 12;
        private const int SocialNotificationPreviewWordCount = 4;
        private const int SocialRenderOverscan = 120;
        private const int SocialCardHeightCacheLimit = 4096;
        private const int SocialNotificationHeightCacheLimit = 1024;

        private enum SocialCardRenderContext
        {
            Feed,
            Detail,
            Profile
        }

        private enum SocialNotificationType
        {
            PlayerPostComment,
            FavouriteNpcPost,
            TaggedInPost,
            TaggedInComment
        }

        private sealed class SocialNotificationEntry
        {
            public string Key { get; init; } = "";
            public string PostId { get; init; } = "";
            public string Message { get; init; } = "";
            public long SortKey { get; init; }
            public long TotalGameTime { get; init; }
            public SocialNotificationType Type { get; init; }
        }

        private sealed class SocialProfileClickableTarget
        {
            public Rectangle Bounds { get; init; }
            public string ActorName { get; init; } = "";
            public bool ActorIsPlayer { get; init; }
        }

        private readonly Dictionary<string, Rectangle> socialFeedPostBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedLikeBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedCommentBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedPostImagePrevBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedPostImageNextBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfilePostBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfileLikeBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedLikeHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfileLikeHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialFeedTagHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialProfileTagHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> socialFeedTagHoverText = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> socialProfileTagHoverText = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> socialFeedPostImageIndices = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> socialNotificationItemBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SocialNotificationEntry> socialNotificationItemsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> socialImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> socialFailedImagePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> socialNotifiedPostIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SocialProfileClickableTarget> socialFeedProfileIconBounds = new();
        private readonly List<SocialProfileClickableTarget> socialDetailProfileIconBounds = new();
        private readonly List<SocialProfileClickableTarget> socialProfileIconBounds = new();

        private Rectangle socialFeedOpenCreatePostBounds = Rectangle.Empty;
        private Rectangle socialFeedOpenProfileBounds = Rectangle.Empty;
        private Rectangle socialFeedOpenNotificationBounds = Rectangle.Empty;
        private Rectangle socialCreateSelectionToggleBounds = Rectangle.Empty;
        private Rectangle socialCreatePrevImageBounds = Rectangle.Empty;
        private Rectangle socialCreateNextImageBounds = Rectangle.Empty;
        private Rectangle socialCreateSubmitBounds = Rectangle.Empty;
        private Rectangle socialNotificationClearAllBounds = Rectangle.Empty;
        private Rectangle socialDetailCommentSendBounds = Rectangle.Empty;
        private Rectangle socialDetailLikeBounds = Rectangle.Empty;
        private Rectangle socialDetailLikeHoverBounds = Rectangle.Empty;
        private Rectangle socialDetailImagePrevBounds = Rectangle.Empty;
        private Rectangle socialDetailImageNextBounds = Rectangle.Empty;
        private Rectangle socialDetailTagHoverBounds = Rectangle.Empty;

        private string selectedSocialPostId = "";
        private string selectedSocialProfileActorName = "";
        private bool selectedSocialProfileActorIsPlayer = true;
        private string socialPostDraft = "";
        private int socialPostDraftCursorIndex = 0;
        private int socialPostDraftSelectionAnchorIndex = 0;
        private readonly List<TextEditSnapshot> socialPostDraftUndoHistory = new();
        private string socialCommentDraft = "";
        private int socialCommentDraftCursorIndex = 0;
        private int socialCommentDraftSelectionAnchorIndex = 0;
        private readonly List<TextEditSnapshot> socialCommentDraftUndoHistory = new();
        private bool socialCreateMenuOpen = false;
        private bool socialProfileMenuOpen = false;
        private bool socialNotificationMenuOpen = false;
        private bool socialDetailReturnToProfile = false;
        private string socialDetailReturnProfileActorName = "";
        private bool socialDetailReturnProfileActorIsPlayer = false;
        private readonly List<string> socialCreateCandidateImages = new();
        private readonly List<string> socialCreateSelectedImages = new();
        private int socialCreateCandidateImageIndex = -1;
        private int socialDetailImageIndex = 0;
        private string socialDetailTagHoverText = "";

        private float socialFeedScrollOffset = 0f;
        private float socialFeedScrollTarget = 0f;
        private float socialNotificationScrollOffset = 0f;
        private float socialNotificationScrollTarget = 0f;
        private float socialProfileScrollOffset = 0f;
        private float socialProfileScrollTarget = 0f;
        private float socialDetailScrollOffset = 0f;
        private float socialDetailScrollTarget = 0f;

        private readonly Dictionary<string, int> socialCardHeightCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> socialNotificationCardHeightCache = new(StringComparer.OrdinalIgnoreCase);

        private Rectangle SocialContentViewportRect => new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + SocialViewportYOffset, 520, SocialViewportHeight);

        private Rectangle GetSocialDetailInputBounds()
        {
            int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
            int inputHeight = fontHeight + 30;

            return new Rectangle(
                xPositionOnScreen + SocialDetailInputX,
                yPositionOnScreen + SocialDetailInputY,
                SocialDetailInputWidth,
                inputHeight);
        }

        private Rectangle GetSocialDetailViewportRect()
        {
            Rectangle viewport = SocialContentViewportRect;
            Rectangle inputBounds = GetSocialDetailInputBounds();
            int bottom = Math.Min(viewport.Bottom, inputBounds.Y - SocialDetailInputBottomGap);
            int height = Math.Max(1, bottom - viewport.Y);

            return new Rectangle(viewport.X, viewport.Y, viewport.Width, height);
        }

        public bool IsViewingSocialPost(string postId)
        {
            return currentApp == SocialAppState
                && !socialCreateMenuOpen
                && !string.IsNullOrWhiteSpace(selectedSocialPostId)
                && string.Equals(selectedSocialPostId, postId, StringComparison.OrdinalIgnoreCase);
        }

        public bool HandleSocialBackNavigation()
        {
            if (currentApp != SocialAppState)
                return false;

            if (socialCreateMenuOpen)
            {
                CloseSocialCreatePostMenu(clearDraft: true);
                return true;
            }

            if (socialNotificationMenuOpen)
            {
                CloseSocialNotificationMenu();
                return true;
            }

            if (!string.IsNullOrWhiteSpace(selectedSocialPostId))
            {
                selectedSocialPostId = "";
                ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
                socialDetailImageIndex = 0;

                if (socialDetailReturnToProfile && !string.IsNullOrWhiteSpace(socialDetailReturnProfileActorName))
                {
                    socialProfileMenuOpen = true;
                    selectedSocialProfileActorName = socialDetailReturnProfileActorName;
                    selectedSocialProfileActorIsPlayer = socialDetailReturnProfileActorIsPlayer;
                    ClampSocialProfileScroll();
                }

                socialDetailReturnToProfile = false;
                socialDetailReturnProfileActorName = "";
                socialDetailReturnProfileActorIsPlayer = false;
                return true;
            }

            if (socialProfileMenuOpen)
            {
                socialProfileMenuOpen = false;
                selectedSocialProfileActorName = "";
                selectedSocialProfileActorIsPlayer = true;
                socialProfileScrollOffset = 0f;
                socialProfileScrollTarget = 0f;
                return true;
            }

            CloseSocialApp();
            return true;
        }

        private void CloseSocialApp()
        {
            ResetSocialState();
            currentApp = null;
        }

        public void OnStardewConnectDataChanged()
        {
            if (currentApp != SocialAppState)
                return;

            InvalidateSocialLayoutCaches();

            if (socialCreateMenuOpen)
                EnsureCreateImageCandidatesLoaded();

            List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();
            PruneDismissedSocialNotifications(posts);
            RefreshSocialNotifiedPostIds(posts);

            var postIds = new HashSet<string>(posts.Select(post => post.Id), StringComparer.OrdinalIgnoreCase);
            foreach (string staleId in socialFeedPostImageIndices.Keys.Where(id => !postIds.Contains(id)).ToList())
                socialFeedPostImageIndices.Remove(staleId);

            foreach (StardewConnectPost post in posts)
            {
                int count = StardewConnectManager.GetAttachmentCount(post);
                if (count <= 0)
                {
                    socialFeedPostImageIndices.Remove(post.Id);
                    continue;
                }

                if (!socialFeedPostImageIndices.TryGetValue(post.Id, out int currentIndex))
                {
                    socialFeedPostImageIndices[post.Id] = 0;
                    continue;
                }

                socialFeedPostImageIndices[post.Id] = Math.Clamp(currentIndex, 0, count - 1);
            }

            if (!string.IsNullOrWhiteSpace(selectedSocialPostId))
            {
                StardewConnectPost? selectedPost = StardewConnectManager.GetPost(selectedSocialPostId);
                if (selectedPost == null)
                {
                    selectedSocialPostId = "";
                    socialDetailImageIndex = 0;
                    socialDetailScrollOffset = 0f;
                    socialDetailScrollTarget = 0f;
                }
                else
                {
                    int attachmentCount = StardewConnectManager.GetAttachmentCount(selectedPost);
                    socialDetailImageIndex = attachmentCount <= 0
                        ? 0
                        : Math.Clamp(socialDetailImageIndex, 0, attachmentCount - 1);

                    ClampSocialDetailScroll(selectedPost);
                    return;
                }
            }

            if (socialProfileMenuOpen)
            {
                ClampSocialProfileScroll();
                return;
            }

            if (socialNotificationMenuOpen)
            {
                ClampSocialNotificationScroll(GetActiveSocialNotifications(posts));
                return;
            }

            ClampSocialFeedScroll(posts);
        }

        private void OpenSocialApp()
        {
            InvalidateSocialLayoutCaches();

            selectedSocialPostId = "";
            selectedSocialProfileActorName = "";
            selectedSocialProfileActorIsPlayer = true;
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialCreateMenuOpen = false;
            socialProfileMenuOpen = false;
            socialNotificationMenuOpen = false;
            socialDetailReturnToProfile = false;
            socialDetailReturnProfileActorName = "";
            socialDetailReturnProfileActorIsPlayer = false;
            ResetEditableTextFieldState(EditableTextFieldKind.SocialPost);
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialCreateSelectedImages.Clear();
            socialCreateCandidateImages.Clear();
            socialCreateCandidateImageIndex = -1;
            socialDetailImageIndex = 0;
            socialNotificationScrollOffset = 0f;
            socialNotificationScrollTarget = 0f;
            socialProfileScrollOffset = 0f;
            socialProfileScrollTarget = 0f;
            socialDetailScrollOffset = 0f;
            socialDetailScrollTarget = 0f;
            currentApp = SocialAppState;
            SnapSocialFeedOnOpen();
            StardewConnectManager.MarkSocialAppVisitedNow();
        }

        private void ResetSocialState()
        {
            InvalidateSocialLayoutCaches();

            selectedSocialPostId = "";
            selectedSocialProfileActorName = "";
            selectedSocialProfileActorIsPlayer = true;
            ResetEditableTextFieldState(EditableTextFieldKind.SocialPost);
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialCreateMenuOpen = false;
            socialProfileMenuOpen = false;
            socialNotificationMenuOpen = false;
            socialDetailReturnToProfile = false;
            socialDetailReturnProfileActorName = "";
            socialDetailReturnProfileActorIsPlayer = false;
            socialCreateCandidateImages.Clear();
            socialCreateSelectedImages.Clear();
            socialCreateCandidateImageIndex = -1;
            socialDetailImageIndex = 0;

            socialFeedScrollOffset = 0f;
            socialFeedScrollTarget = 0f;
            socialNotificationScrollOffset = 0f;
            socialNotificationScrollTarget = 0f;
            socialProfileScrollOffset = 0f;
            socialProfileScrollTarget = 0f;
            socialDetailScrollOffset = 0f;
            socialDetailScrollTarget = 0f;

            socialFeedPostBounds.Clear();
            socialFeedLikeBounds.Clear();
            socialFeedCommentBounds.Clear();
            socialFeedPostImagePrevBounds.Clear();
            socialFeedPostImageNextBounds.Clear();
            socialProfilePostBounds.Clear();
            socialProfileLikeBounds.Clear();
            socialFeedLikeHoverBounds.Clear();
            socialProfileLikeHoverBounds.Clear();
            socialFeedTagHoverBounds.Clear();
            socialProfileTagHoverBounds.Clear();
            socialFeedTagHoverText.Clear();
            socialProfileTagHoverText.Clear();
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";
            socialFeedPostImageIndices.Clear();
            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();
            socialNotifiedPostIds.Clear();
            socialFeedProfileIconBounds.Clear();
            socialDetailProfileIconBounds.Clear();
            socialProfileIconBounds.Clear();

            socialFeedOpenCreatePostBounds = Rectangle.Empty;
            socialFeedOpenProfileBounds = Rectangle.Empty;
            socialFeedOpenNotificationBounds = Rectangle.Empty;
            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;
            socialCreateSubmitBounds = Rectangle.Empty;
            socialNotificationClearAllBounds = Rectangle.Empty;
            socialDetailCommentSendBounds = Rectangle.Empty;
            socialDetailLikeBounds = Rectangle.Empty;
            socialDetailImagePrevBounds = Rectangle.Empty;
            socialDetailImageNextBounds = Rectangle.Empty;
        }

        private void InvalidateSocialLayoutCaches()
        {
            socialCardHeightCache.Clear();
            socialNotificationCardHeightCache.Clear();
        }

        private static string BuildSocialCardHeightCacheKey(StardewConnectPost post, bool includeAllComments, int selectedAttachmentIndex)
        {
            string postId = post?.Id ?? "";
            int safeAttachmentIndex = Math.Max(0, selectedAttachmentIndex);
            bool showTags = ModEntry.Config?.ShowSocialImageTags ?? true;
            return $"{postId}|{(includeAllComments ? "all" : "preview")}|{safeAttachmentIndex}|{showTags}";
        }

        private int GetTotalSocialNotificationCount()
        {
            return GetActiveSocialNotifications().Count;
        }

        private bool IsPlayerPostWithNewCommentNotification(StardewConnectPost post)
        {
            if (post == null || !post.AuthorIsPlayer || post.Comments == null || post.Comments.Count == 0)
                return false;

            int totalComments = post.Comments.Count;
            int readCount = Math.Clamp(post.PlayerReadCommentCount, 0, totalComments);
            if (readCount >= totalComments)
                return false;

            for (int i = readCount; i < totalComments; i++)
            {
                StardewConnectComment comment = post.Comments[i];
                if (comment == null || comment.AuthorIsPlayer)
                    continue;

                return true;
            }

            return false;
        }

        private bool IsFavouriteNpcPostNotification(StardewConnectPost post)
        {
            if (post == null || post.AuthorIsPlayer)
                return false;

            return IsFavouriteNpc(post.AuthorName);
        }

        private bool IsSocialNotificationPost(StardewConnectPost post)
        {
            return post != null
                && !string.IsNullOrWhiteSpace(post.Id)
                && socialNotifiedPostIds.Contains(post.Id);
        }

        private bool IsFavouriteNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName) || MessageManager.favouriteNpc == null)
                return false;

            return MessageManager.favouriteNpc.Any(name => string.Equals(name, npcName, StringComparison.OrdinalIgnoreCase));
        }

        private List<SocialNotificationEntry> GetActiveSocialNotifications(List<StardewConnectPost>? posts = null)
        {
            List<SocialNotificationEntry> allEntries = BuildSocialNotificationEntries(posts ?? StardewConnectManager.GetPostsSnapshot());
            if (allEntries.Count == 0)
                return allEntries;

            return allEntries
                .Where(entry => !StardewConnectManager.IsSocialNotificationDismissed(entry.Key))
                .OrderByDescending(entry => entry.SortKey)
                .ThenByDescending(entry => entry.TotalGameTime)
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<SocialNotificationEntry> RefreshSocialNotifiedPostIds(List<StardewConnectPost>? posts = null)
        {
            List<SocialNotificationEntry> activeEntries = GetActiveSocialNotifications(posts);

            socialNotifiedPostIds.Clear();
            foreach (SocialNotificationEntry entry in activeEntries)
            {
                if (!string.IsNullOrWhiteSpace(entry.PostId))
                    socialNotifiedPostIds.Add(entry.PostId);
            }

            return activeEntries;
        }

        private void PruneDismissedSocialNotifications(List<StardewConnectPost>? posts = null)
        {
            List<SocialNotificationEntry> allEntries = BuildSocialNotificationEntries(posts ?? StardewConnectManager.GetPostsSnapshot());
            StardewConnectManager.PruneSocialNotificationDismissals(allEntries.Select(entry => entry.Key));
        }

        private List<SocialNotificationEntry> BuildSocialNotificationEntries(List<StardewConnectPost> posts)
        {
            var entries = new List<SocialNotificationEntry>();
            if (posts == null || posts.Count == 0)
                return entries;

            foreach (StardewConnectPost post in posts)
            {
                if (post == null || string.IsNullOrWhiteSpace(post.Id))
                    continue;

                SocialNotificationEntry? playerCommentEntry = BuildPlayerCommentNotification(post);
                if (playerCommentEntry != null)
                    entries.Add(playerCommentEntry);

                SocialNotificationEntry? favouriteNpcPostEntry = BuildFavouriteNpcPostNotification(post);
                if (favouriteNpcPostEntry != null)
                    entries.Add(favouriteNpcPostEntry);

                SocialNotificationEntry? taggedInPostEntry = BuildTaggedInPostNotification(post);
                if (taggedInPostEntry != null)
                    entries.Add(taggedInPostEntry);

                SocialNotificationEntry? taggedInCommentEntry = BuildTaggedInCommentNotification(post);
                if (taggedInCommentEntry != null)
                    entries.Add(taggedInCommentEntry);
            }

            return entries;
        }

        private SocialNotificationEntry? BuildPlayerCommentNotification(StardewConnectPost post)
        {
            if (!IsPlayerPostWithNewCommentNotification(post))
                return null;

            post.Comments ??= new List<StardewConnectComment>();
            int readCount = Math.Clamp(post.PlayerReadCommentCount, 0, post.Comments.Count);

            List<StardewConnectComment> unreadNpcComments = post.Comments
                .Skip(readCount)
                .Where(comment => comment != null && !comment.AuthorIsPlayer)
                .ToList();

            if (unreadNpcComments.Count == 0)
                return null;

            StardewConnectComment firstUnread = unreadNpcComments[0];
            StardewConnectComment latestUnread = unreadNpcComments[unreadNpcComments.Count - 1];
            int unreadCount = unreadNpcComments.Count;

            string authorName = ResolveSocialActorDisplayName(firstUnread.AuthorName, firstUnread.AuthorIsPlayer);
            if (string.IsNullOrWhiteSpace(authorName))
                authorName = "Someone";

            NPC commenter = Game1.getCharacterFromName(firstUnread.AuthorName, mustBeVillager: false);

            string message = unreadCount <= 1
                ? $"{commenter.displayName} commented on your post (1 new comment)"
                : $"{commenter.displayName} and {unreadCount - 1} others commented on your post ({unreadCount} new comments)";

            return new SocialNotificationEntry
            {
                Key = BuildSocialNotificationKey(SocialNotificationType.PlayerPostComment, post.Id, $"{latestUnread.Id}|{unreadCount}"),
                PostId = post.Id,
                Message = message,
                SortKey = BuildSocialChronologicalSortKey(latestUnread.Season, latestUnread.Day, latestUnread.Year, latestUnread.TimeOfDay),
                TotalGameTime = Math.Max(0L, latestUnread.TotalGameTime),
                Type = SocialNotificationType.PlayerPostComment
            };
        }

        private SocialNotificationEntry? BuildFavouriteNpcPostNotification(StardewConnectPost post)
        {
            if (!IsFavouriteNpcPostNotification(post))
                return null;

            string authorName = ResolveSocialActorDisplayName(post.AuthorName, post.AuthorIsPlayer);
            if (string.IsNullOrWhiteSpace(authorName))
                authorName = "Someone";

            int attachmentCount = StardewConnectManager.GetAttachmentCount(post);
            string message;
            if (attachmentCount > 0)
            {
                string label = attachmentCount == 1 ? "image" : "images";
                message = $"{authorName} posted {attachmentCount} new {label}";
            }
            else
            {
                string preview = BuildSocialNotificationPostPreview(post.Text, SocialNotificationPreviewWordCount);
                message = $"{authorName} posted a new post: {preview}";
            }

            return new SocialNotificationEntry
            {
                Key = BuildSocialNotificationKey(SocialNotificationType.FavouriteNpcPost, post.Id),
                PostId = post.Id,
                Message = message,
                SortKey = BuildSocialChronologicalSortKey(post.Season, post.Day, post.Year, post.TimeOfDay),
                TotalGameTime = Math.Max(0L, post.TotalGameTime),
                Type = SocialNotificationType.FavouriteNpcPost
            };
        }

        private SocialNotificationEntry? BuildTaggedInPostNotification(StardewConnectPost post)
        {
            if (post == null || post.AuthorIsPlayer)
                return null;

            if (!PostTagsPlayer(post))
                return null;

            string authorName = ResolveSocialActorDisplayName(post.AuthorName, post.AuthorIsPlayer);
            if (string.IsNullOrWhiteSpace(authorName))
                authorName = "Someone";

            return new SocialNotificationEntry
            {
                Key = BuildSocialNotificationKey(SocialNotificationType.TaggedInPost, post.Id),
                PostId = post.Id,
                Message = $"{authorName} tagged you in a post",
                SortKey = BuildSocialChronologicalSortKey(post.Season, post.Day, post.Year, post.TimeOfDay),
                TotalGameTime = Math.Max(0L, post.TotalGameTime),
                Type = SocialNotificationType.TaggedInPost
            };
        }

        private SocialNotificationEntry? BuildTaggedInCommentNotification(StardewConnectPost post)
        {
            if (post?.Comments == null || post.Comments.Count == 0)
                return null;

            StardewConnectComment? latestTaggedComment = null;
            for (int i = post.Comments.Count - 1; i >= 0; i--)
            {
                StardewConnectComment comment = post.Comments[i];
                if (comment == null || comment.AuthorIsPlayer)
                    continue;

                if (!ContainsPlayerMention(comment.Text))
                    continue;

                latestTaggedComment = comment;
                break;
            }

            if (latestTaggedComment == null)
                return null;

            string authorName = ResolveSocialActorDisplayName(latestTaggedComment.AuthorName, latestTaggedComment.AuthorIsPlayer);
            if (string.IsNullOrWhiteSpace(authorName))
                authorName = "Someone";

            return new SocialNotificationEntry
            {
                Key = BuildSocialNotificationKey(SocialNotificationType.TaggedInComment, post.Id, latestTaggedComment.Id),
                PostId = post.Id,
                Message = $"{authorName} tagged you in a comment",
                SortKey = BuildSocialChronologicalSortKey(
                    latestTaggedComment.Season,
                    latestTaggedComment.Day,
                    latestTaggedComment.Year,
                    latestTaggedComment.TimeOfDay),
                TotalGameTime = Math.Max(0L, latestTaggedComment.TotalGameTime),
                Type = SocialNotificationType.TaggedInComment
            };
        }

        private bool PostTagsPlayer(StardewConnectPost post)
        {
            if (post == null)
                return false;

            string postTag = StardewConnectManager.GetPostTag(post);
            if (ContainsTag(postTag, $"#Player {Game1.player?.displayName}".Trim()))
                return true;

            return ContainsPlayerMention(post.Text);
        }

        private static bool ContainsTag(string tagText, string expectedTag)
        {
            if (string.IsNullOrWhiteSpace(tagText) || string.IsNullOrWhiteSpace(expectedTag))
                return false;

            foreach (string rawTag in tagText.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = (rawTag ?? "").Trim();
                if (string.Equals(trimmed, expectedTag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool ContainsPlayerMention(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string playerName = (Game1.player?.Name ?? "Player").Trim();
            return ContainsMentionToken(text, "Player")
                || (!string.IsNullOrWhiteSpace(playerName) && ContainsMentionToken(text, playerName));
        }

        private static bool ContainsMentionToken(string text, string token)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
                return false;

            return text.IndexOf("@" + token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildSocialNotificationKey(SocialNotificationType type, string postId, string discriminator = "")
        {
            string safePostId = postId ?? "";
            if (string.IsNullOrWhiteSpace(discriminator))
                return $"{type}|{safePostId}";

            return $"{type}|{safePostId}|{discriminator}";
        }

        private static string BuildSocialNotificationPostPreview(string text, int maxWords)
        {
            string trimmed = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return "...";

            string[] words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int safeWordCount = Math.Max(1, maxWords);
            if (words.Length <= safeWordCount)
                return trimmed;

            return string.Join(" ", words.Take(safeWordCount)) + " ...";
        }

        private void DismissSocialNotificationsForPost(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return;

            List<SocialNotificationEntry> entries = GetActiveSocialNotifications();
            var notificationKeys = new List<string>();
            foreach (SocialNotificationEntry entry in entries)
            {
                if (!string.Equals(entry.PostId, postId, StringComparison.OrdinalIgnoreCase))
                    continue;

                notificationKeys.Add(entry.Key);
            }

            StardewConnectManager.DismissSocialNotifications(notificationKeys);
            StardewConnectManager.MarkPostCommentsRead(postId);
            RefreshSocialNotifiedPostIds();
        }

        private void DismissAllSocialNotifications()
        {
            List<SocialNotificationEntry> entries = GetActiveSocialNotifications();
            if (entries.Count == 0)
                return;

            StardewConnectManager.DismissSocialNotifications(entries.Select(entry => entry.Key));

            foreach (string postId in entries.Select(entry => entry.PostId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase))
                StardewConnectManager.MarkPostCommentsRead(postId);

            RefreshSocialNotifiedPostIds();
        }

        private static long BuildSocialChronologicalSortKey(string season, int day, int year, int timeOfDay)
        {
            int dayIndex = GetSocialAbsoluteDayIndex(season, day, year);
            int normalizedTimeOfDay = NormalizeSocialTimeOfDay(timeOfDay);
            int hour = normalizedTimeOfDay / 100;
            int minute = normalizedTimeOfDay % 100;
            int totalMinutes = (hour * 60) + minute;

            return ((long)dayIndex * 2000L) + totalMinutes;
        }

        private static int CompareSocialChronologicalPosition(
            string leftSeason,
            int leftDay,
            int leftYear,
            int leftTimeOfDay,
            long leftTotalGameTime,
            string rightSeason,
            int rightDay,
            int rightYear,
            int rightTimeOfDay,
            long rightTotalGameTime)
        {
            long leftKey = BuildSocialChronologicalSortKey(leftSeason, leftDay, leftYear, leftTimeOfDay);
            long rightKey = BuildSocialChronologicalSortKey(rightSeason, rightDay, rightYear, rightTimeOfDay);

            int primaryComparison = leftKey.CompareTo(rightKey);
            if (primaryComparison != 0)
                return primaryComparison;

            return CompareSocialTotalGameTime(leftTotalGameTime, rightTotalGameTime);
        }

        private static int CompareSocialTotalGameTime(long left, long right)
        {
            long safeLeft = Math.Max(0L, left);
            long safeRight = Math.Max(0L, right);
            bool hasLeft = safeLeft > 0;
            bool hasRight = safeRight > 0;

            if (hasLeft && hasRight)
                return safeLeft.CompareTo(safeRight);

            if (hasLeft == hasRight)
                return 0;

            return hasLeft ? 1 : -1;
        }

        private static int GetSocialAbsoluteDayIndex(string season, int day, int year)
        {
            int seasonIndex = (season ?? "").Trim().ToLowerInvariant() switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };

            int safeDay = Math.Clamp(day, 1, 28);
            int safeYear = Math.Max(1, year);

            return ((safeYear - 1) * 4 * 28)
                + (seasonIndex * 28)
                + (safeDay - 1);
        }

        private static int NormalizeSocialTimeOfDay(int rawTime)
        {
            if (rawTime <= 0)
                return 600;

            int hour = rawTime / 100;
            int minute = rawTime % 100;

            hour = Math.Clamp(hour, 0, 26);
            minute = Math.Clamp(minute, 0, 59);
            return (hour * 100) + minute;
        }

        private void DrawSocialApp(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
            b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
            b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
            backButton.draw(b);

            //string title = socialCreateMenuOpen
            //    ? "Create Post"
            //    : string.IsNullOrWhiteSpace(selectedSocialPostId) ? "StardewConnect" : "Post";
            //b.DrawString(Game1.smallFont, title, new Vector2(xPositionOnScreen + 105, yPositionOnScreen + 75), Color.Black);

            if (socialCreateMenuOpen)
            {
                DrawSocialCreatePostMenu(b);
                return;
            }

            if (socialNotificationMenuOpen)
            {
                DrawSocialNotificationMenu(b);
                return;
            }

            if (socialProfileMenuOpen)
            {
                DrawSocialProfile(b);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedSocialPostId))
            {
                DrawSocialFeed(b);
                return;
            }

            StardewConnectPost? selectedPost = StardewConnectManager.GetPost(selectedSocialPostId);
            if (selectedPost == null)
            {
                selectedSocialPostId = "";
                DrawSocialFeed(b);
                return;
            }

            DrawSocialDetail(b, selectedPost);
        }

        private void DrawSocialFeed(SpriteBatch b)
        {
            socialFeedPostBounds.Clear();
            socialFeedLikeBounds.Clear();
            socialFeedCommentBounds.Clear();
            socialFeedPostImagePrevBounds.Clear();
            socialFeedPostImageNextBounds.Clear();
            socialProfilePostBounds.Clear();
            socialProfileLikeBounds.Clear();
            socialFeedLikeHoverBounds.Clear();
            socialProfileLikeHoverBounds.Clear();
            socialFeedTagHoverBounds.Clear();
            socialProfileTagHoverBounds.Clear();
            socialFeedTagHoverText.Clear();
            socialProfileTagHoverText.Clear();
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";
            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();

            socialFeedProfileIconBounds.Clear();
            socialDetailProfileIconBounds.Clear();
            socialProfileIconBounds.Clear();

            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;
            socialCreateSubmitBounds = Rectangle.Empty;
            socialNotificationClearAllBounds = Rectangle.Empty;
            socialDetailLikeBounds = Rectangle.Empty;
            socialDetailCommentSendBounds = Rectangle.Empty;
            socialDetailImagePrevBounds = Rectangle.Empty;
            socialDetailImageNextBounds = Rectangle.Empty;
            socialFeedOpenProfileBounds = Rectangle.Empty;
            socialFeedOpenNotificationBounds = Rectangle.Empty;

            List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();
            PruneDismissedSocialNotifications(posts);
            RefreshSocialNotifiedPostIds(posts);

            b.End();

            Rectangle clipRect = SocialContentViewportRect;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int y = yPositionOnScreen + SocialViewportYOffset + SocialPostTopPadding - (int)MathF.Floor(socialFeedScrollOffset);
            int drawMinY = clipRect.Top - SocialRenderOverscan;
            int drawMaxY = clipRect.Bottom + SocialRenderOverscan;
            if (posts.Count == 0)
            {
                b.DrawString(Game1.smallFont, "No post yet.", new Vector2(xPositionOnScreen + 60, yPositionOnScreen + 245), Color.Black);
            }
            else
            {
                foreach (StardewConnectPost post in posts)
                {
                    int selectedAttachmentIndex = GetFeedPostImageIndex(post);
                    int postHeight = MeasureSocialPostCardHeight(
                        post,
                        includeAllComments: false,
                        selectedAttachmentIndex: selectedAttachmentIndex);

                    if (y + postHeight < drawMinY)
                    {
                        y += postHeight + SocialPostSpacing;
                        continue;
                    }

                    if (y > drawMaxY)
                        break;

                    DrawSocialPostCard(
                        b,
                        post,
                        xPositionOnScreen + SocialPostCardX,
                        y,
                        includeAllComments: false,
                        context: SocialCardRenderContext.Feed,
                        selectedAttachmentIndex: selectedAttachmentIndex);

                    y += postHeight + SocialPostSpacing;
                }
            }

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            int buttonX = xPositionOnScreen + 100;
            int buttonY = yPositionOnScreen + 65;
            int buttonWidth = 150;
            int buttonHeight = 50;

            socialFeedOpenCreatePostBounds = new Rectangle(buttonX, buttonY, buttonWidth, buttonHeight);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                buttonX,
                buttonY,
                buttonWidth,
                buttonHeight,
                new Color(255, 255, 255, 220),
                1f,
                false);
            b.DrawString(Game1.smallFont, "Create ...", new Vector2(buttonX + 14, buttonY + 12), Color.Black);

            socialFeedOpenProfileBounds = new Rectangle(
                socialFeedOpenCreatePostBounds.Right + 16,
                socialFeedOpenCreatePostBounds.Y + 4,
                (int)(SocialActorIconSize * 0.75f),
                (int)(SocialActorIconSize * 0.75f));

            DrawSocialActorIcon(
                b,
                Game1.player?.Name ?? "Player",
                actorIsPlayer: true,
                socialFeedOpenProfileBounds);

            socialFeedOpenNotificationBounds = new Rectangle(
                socialFeedOpenProfileBounds.Right + 10,
                socialFeedOpenProfileBounds.Y,
                SocialNotificationButtonSize,
                SocialNotificationButtonSize);

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                socialFeedOpenNotificationBounds.X,
                socialFeedOpenNotificationBounds.Y,
                socialFeedOpenNotificationBounds.Width,
                socialFeedOpenNotificationBounds.Height,
                new Color(255, 255, 255, 220),
                1f,
                false);

            if (textureAppNotification != null)
            {
                Rectangle iconBounds = new Rectangle(
                    socialFeedOpenNotificationBounds.X + 6,
                    socialFeedOpenNotificationBounds.Y + 6,
                    socialFeedOpenNotificationBounds.Width - 12,
                    socialFeedOpenNotificationBounds.Height - 12);
                b.Draw(textureAppNotification, iconBounds, Color.White);
            }

            int socialNotificationCount = GetTotalSocialNotificationCount();
            if (socialNotificationCount > 0)
                DrawSocialUnreadBadge(b, socialFeedOpenNotificationBounds.Right + 8, socialFeedOpenNotificationBounds.Y - 4, socialNotificationCount);

            DrawSocialLikeTooltipIfHovered(b, posts, socialFeedLikeHoverBounds);
            DrawSocialTagTooltipIfHovered(b, posts, socialFeedTagHoverBounds, socialFeedTagHoverText, SocialContentViewportRect);
        }

        private void DrawSocialNotificationMenu(SpriteBatch b)
        {
            socialFeedPostBounds.Clear();
            socialFeedLikeBounds.Clear();
            socialFeedCommentBounds.Clear();
            socialFeedPostImagePrevBounds.Clear();
            socialFeedPostImageNextBounds.Clear();
            socialProfilePostBounds.Clear();
            socialProfileLikeBounds.Clear();
            socialFeedLikeHoverBounds.Clear();
            socialProfileLikeHoverBounds.Clear();
            socialFeedTagHoverBounds.Clear();
            socialProfileTagHoverBounds.Clear();
            socialFeedTagHoverText.Clear();
            socialProfileTagHoverText.Clear();
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";

            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();

            socialFeedProfileIconBounds.Clear();
            socialDetailProfileIconBounds.Clear();
            socialProfileIconBounds.Clear();

            socialFeedOpenCreatePostBounds = Rectangle.Empty;
            socialFeedOpenProfileBounds = Rectangle.Empty;
            socialFeedOpenNotificationBounds = Rectangle.Empty;
            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;
            socialCreateSubmitBounds = Rectangle.Empty;
            socialDetailLikeBounds = Rectangle.Empty;
            socialDetailCommentSendBounds = Rectangle.Empty;
            socialDetailImagePrevBounds = Rectangle.Empty;
            socialDetailImageNextBounds = Rectangle.Empty;

            List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();
            PruneDismissedSocialNotifications(posts);
            List<SocialNotificationEntry> notifications = RefreshSocialNotifiedPostIds(posts);

            b.End();

            Rectangle clipRect = SocialContentViewportRect;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int cardX = xPositionOnScreen + SocialPostCardX;
            int cursorY = yPositionOnScreen + SocialViewportYOffset + SocialPostTopPadding - (int)MathF.Floor(socialNotificationScrollOffset);
            int lineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            int drawMinY = clipRect.Top - SocialRenderOverscan;
            int drawMaxY = clipRect.Bottom + SocialRenderOverscan;

            if (notifications.Count == 0)
            {
                b.DrawString(Game1.smallFont, "No notification.", new Vector2(cardX + 14, cursorY + 10), Color.Black);
            }
            else
            {
                foreach (SocialNotificationEntry entry in notifications)
                {
                    int cardHeight = MeasureSocialNotificationCardHeight(entry);

                    if (cursorY + cardHeight < drawMinY)
                    {
                        cursorY += cardHeight + SocialNotificationCardSpacing;
                        continue;
                    }

                    if (cursorY > drawMaxY)
                        break;

                    List<string> lines = SplitTextIntoLines(
                        entry.Message,
                        Game1.smallFont,
                        SocialNotificationCardWidth - (SocialNotificationCardPadding * 2));
                    if (lines.Count == 0)
                        lines = new List<string> { "" };

                    Rectangle cardBounds = new Rectangle(cardX, cursorY, SocialNotificationCardWidth, cardHeight);

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        cardBounds.X,
                        cardBounds.Y,
                        cardBounds.Width,
                        cardBounds.Height,
                        new Color(255, 255, 255, 230),
                        1f,
                        false);

                    int textY = cardBounds.Y + SocialNotificationCardVerticalPadding;
                    foreach (string line in lines)
                    {
                        b.DrawString(Game1.smallFont, line, new Vector2(cardBounds.X + SocialNotificationCardPadding, textY), Color.Black);
                        textY += lineHeight;
                    }

                    socialNotificationItemBounds[entry.Key] = cardBounds;
                    socialNotificationItemsByKey[entry.Key] = entry;

                    cursorY += cardHeight + SocialNotificationCardSpacing;
                }
            }

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            socialNotificationClearAllBounds = okButton.bounds;
            okButton.draw(b);
        }

        private void DrawSocialDetail(SpriteBatch b, StardewConnectPost selectedPost)
        {
            socialFeedPostBounds.Clear();
            socialFeedLikeBounds.Clear();
            socialFeedCommentBounds.Clear();
            socialFeedPostImagePrevBounds.Clear();
            socialFeedPostImageNextBounds.Clear();
            socialProfilePostBounds.Clear();
            socialProfileLikeBounds.Clear();
            socialFeedLikeHoverBounds.Clear();
            socialProfileLikeHoverBounds.Clear();
            socialFeedTagHoverBounds.Clear();
            socialProfileTagHoverBounds.Clear();
            socialFeedTagHoverText.Clear();
            socialProfileTagHoverText.Clear();
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";
            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();

            socialFeedProfileIconBounds.Clear();
            socialDetailProfileIconBounds.Clear();
            socialProfileIconBounds.Clear();

            socialFeedOpenCreatePostBounds = Rectangle.Empty;
            socialFeedOpenProfileBounds = Rectangle.Empty;
            socialFeedOpenNotificationBounds = Rectangle.Empty;
            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;
            socialCreateSubmitBounds = Rectangle.Empty;
            socialNotificationClearAllBounds = Rectangle.Empty;

            RefreshSocialNotifiedPostIds(StardewConnectManager.GetPostsSnapshot());

            int attachmentCount = StardewConnectManager.GetAttachmentCount(selectedPost);
            socialDetailImageIndex = attachmentCount <= 0
                ? 0
                : Math.Clamp(socialDetailImageIndex, 0, attachmentCount - 1);

            b.End();

            Rectangle clipRect = GetSocialDetailViewportRect();
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int cardY = yPositionOnScreen + SocialViewportYOffset + SocialPostTopPadding - (int)MathF.Floor(socialDetailScrollOffset);
            DrawSocialPostCard(
                b,
                selectedPost,
                xPositionOnScreen + SocialPostCardX,
                cardY,
                includeAllComments: true,
                context: SocialCardRenderContext.Detail,
                selectedAttachmentIndex: socialDetailImageIndex);

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            Rectangle inputBounds = GetSocialDetailInputBounds();

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                inputBounds.X,
                inputBounds.Y,
                inputBounds.Width,
                inputBounds.Height,
                Color.White,
                1f,
                false);

            DrawEditableTextInput(b, inputBounds, socialCommentDraft, socialCommentDraftCursorIndex, socialCommentDraftSelectionAnchorIndex);

            socialDetailCommentSendBounds = okButton.bounds;
            okButton.draw(b);

            if (selectedPost.AuthorIsPlayer)
            {
                b.Draw(
                    removeButton.texture,
                    new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                    removeButton.sourceRect,
                    Color.White * 0.8f,
                    0f,
                    Vector2.Zero,
                    removeButton.scale,
                    SpriteEffects.None,
                    1f);
            }

            DrawSocialLikeTooltipIfHovered(b, selectedPost, socialDetailLikeHoverBounds);
            DrawSocialTagTooltipIfHovered(b, selectedPost, socialDetailTagHoverBounds, socialDetailTagHoverText, GetSocialDetailViewportRect());

            textBox.Selected = true;
            Game1.keyboardDispatcher.Subscriber = textBox;
        }

        private void DrawSocialCreatePostMenu(SpriteBatch b)
        {
            EnsureCreateImageCandidatesLoaded();

            socialFeedPostBounds.Clear();
            socialFeedLikeBounds.Clear();
            socialFeedCommentBounds.Clear();
            socialFeedPostImagePrevBounds.Clear();
            socialFeedPostImageNextBounds.Clear();
            socialProfilePostBounds.Clear();
            socialProfileLikeBounds.Clear();
            socialFeedLikeHoverBounds.Clear();
            socialProfileLikeHoverBounds.Clear();
            socialFeedTagHoverBounds.Clear();
            socialProfileTagHoverBounds.Clear();
            socialFeedTagHoverText.Clear();
            socialProfileTagHoverText.Clear();
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";
            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();
            socialFeedProfileIconBounds.Clear();
            socialDetailProfileIconBounds.Clear();
            socialProfileIconBounds.Clear();
            socialFeedOpenCreatePostBounds = Rectangle.Empty;
            socialFeedOpenProfileBounds = Rectangle.Empty;
            socialFeedOpenNotificationBounds = Rectangle.Empty;
            socialNotificationClearAllBounds = Rectangle.Empty;
            socialDetailLikeBounds = Rectangle.Empty;
            socialDetailCommentSendBounds = Rectangle.Empty;
            socialDetailImagePrevBounds = Rectangle.Empty;
            socialDetailImageNextBounds = Rectangle.Empty;

            Rectangle panelBounds = new Rectangle(xPositionOnScreen + 50, yPositionOnScreen + 200, 500, 670);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                panelBounds.X,
                panelBounds.Y,
                panelBounds.Width,
                panelBounds.Height,
                new Color(255, 255, 255, 230),
                1f,
                false);

            int previewX = panelBounds.X + 30;
            int previewY = panelBounds.Y + 46;
            int previewW = 440;
            int previewH = 380;
            Rectangle previewBounds = new Rectangle(previewX, previewY, previewW, previewH);
            b.Draw(Game1.staminaRect, previewBounds, new Color(0, 0, 0, 70));

            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;

            if (socialCreateCandidateImages.Count > 0
                && socialCreateCandidateImageIndex >= 0
                && socialCreateCandidateImageIndex < socialCreateCandidateImages.Count)
            {
                string fileName = socialCreateCandidateImages[socialCreateCandidateImageIndex];
                StardewConnectPost tempPost = new StardewConnectPost
                {
                    AttachedImageFile = fileName,
                    AttachmentFromPlayerFolder = true,
                    Attachments = new List<StardewConnectPostAttachment>
                    {
                        new StardewConnectPostAttachment
                        {
                            ImageFile = fileName,
                            FromPlayerFolder = true
                        }
                    }
                };

                if (TryGetSocialAttachmentTexture(tempPost, 0, out Texture2D previewTexture))
                {
                    float scale = Math.Min(
                        previewBounds.Width / (float)Math.Max(1, previewTexture.Width),
                        previewBounds.Height / (float)Math.Max(1, previewTexture.Height));
                    int drawWidth = Math.Max(1, (int)Math.Round(previewTexture.Width * scale));
                    int drawHeight = Math.Max(1, (int)Math.Round(previewTexture.Height * scale));

                    Rectangle drawRect = new Rectangle(
                        previewBounds.X + (previewBounds.Width - drawWidth) / 2,
                        previewBounds.Y + (previewBounds.Height - drawHeight) / 2,
                        drawWidth,
                        drawHeight);
                    b.Draw(previewTexture, drawRect, Color.White);
                }

                socialCreatePrevImageBounds = new Rectangle(previewBounds.X + 8, previewBounds.Y + previewBounds.Height / 2 - SocialImageNavButtonSize / 2, SocialImageNavButtonSize, SocialImageNavButtonSize);
                socialCreateNextImageBounds = new Rectangle(previewBounds.Right - SocialImageNavButtonSize - 8, previewBounds.Y + previewBounds.Height / 2 - SocialImageNavButtonSize / 2, SocialImageNavButtonSize, SocialImageNavButtonSize);
                DrawSocialImageNavButton(b, socialCreatePrevImageBounds, isNext: false);
                DrawSocialImageNavButton(b, socialCreateNextImageBounds, isNext: true);

                // image label
                // string fileLabel = fileName;
                // Vector2 fileLabelSize = Game1.smallFont.MeasureString(fileLabel);
                // b.DrawString(
                //     Game1.smallFont,
                //     fileLabel,
                //     new Vector2(previewBounds.X + (previewBounds.Width - fileLabelSize.X) / 2f, previewBounds.Bottom - fileLabelSize.Y + 45),
                //     Color.Black);

                // select image option
                bool selected = IsCreateImageSelected(fileName);
                socialCreateSelectionToggleBounds = new Rectangle(panelBounds.X + 190, previewBounds.Bottom + 60, 120, 50);
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    socialCreateSelectionToggleBounds.X,
                    socialCreateSelectionToggleBounds.Y,
                    socialCreateSelectionToggleBounds.Width,
                    socialCreateSelectionToggleBounds.Height,
                    selected ? new Color(200, 240, 200, 230) : new Color(255, 255, 255, 220),
                    1f,
                    false);

                Rectangle heartBounds = new Rectangle(socialCreateSelectionToggleBounds.X + 17, socialCreateSelectionToggleBounds.Y + 15, 24, 24);
                Rectangle heartRect = selected
                    ? new Rectangle(211, 428, 7, 7)
                    : new Rectangle(218, 428, 7, 7);
                b.Draw(Game1.mouseCursors, heartBounds, heartRect, Color.White);

                string toggleCount = $"{socialCreateSelectedImages.Count}/{SocialCreateSelectionMaxCount}";
                b.DrawString(Game1.smallFont, toggleCount, new Vector2(socialCreateSelectionToggleBounds.Right - 60, socialCreateSelectionToggleBounds.Y + 12), Color.Black);

            }
            else
            {
                b.DrawString(Game1.smallFont, "No photo found", new Vector2(previewBounds.X + 20, previewBounds.Y + 20), Color.Black);
            }

            //string selectedImagesLabel = socialCreateSelectedImages.Count == 0
            //    ? "Selected images: none"
            //    : "Selected images: " + string.Join(", ", socialCreateSelectedImages.Select(name => Path.GetFileNameWithoutExtension(name)));
            //b.DrawString(Game1.smallFont, selectedImagesLabel, new Vector2(panelBounds.X + 20, panelBounds.Y + 470), Color.Black);

            int inputX = panelBounds.X + 20;
            int inputY = panelBounds.Bottom - 90;
            int inputWidth = 390;
            int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
            int inputHeight = fontHeight + 30;

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                inputX,
                inputY,
                inputWidth,
                inputHeight,
                Color.White,
                1f,
                false);

            DrawEditableTextInput(b, new Rectangle(inputX, inputY, inputWidth, inputHeight), socialPostDraft, socialPostDraftCursorIndex, socialPostDraftSelectionAnchorIndex);


            var socialOkButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 465, this.yPositionOnScreen + 780, 64, 64),
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64),
                1f);

            socialCreateSubmitBounds = socialOkButton.bounds;
            socialOkButton.draw(b);

            textBox.Selected = true;
            Game1.keyboardDispatcher.Subscriber = textBox;
        }

        private void DrawSocialProfile(SpriteBatch b)
        {
            socialFeedPostBounds.Clear();
            socialFeedLikeBounds.Clear();
            socialFeedCommentBounds.Clear();
            socialFeedPostImagePrevBounds.Clear();
            socialFeedPostImageNextBounds.Clear();
            socialProfilePostBounds.Clear();
            socialProfileLikeBounds.Clear();
            socialFeedLikeHoverBounds.Clear();
            socialProfileLikeHoverBounds.Clear();
            socialFeedTagHoverBounds.Clear();
            socialProfileTagHoverBounds.Clear();
            socialFeedTagHoverText.Clear();
            socialProfileTagHoverText.Clear();
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";
            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();

            socialFeedProfileIconBounds.Clear();
            socialDetailProfileIconBounds.Clear();
            socialProfileIconBounds.Clear();

            socialFeedOpenCreatePostBounds = Rectangle.Empty;
            socialFeedOpenProfileBounds = Rectangle.Empty;
            socialFeedOpenNotificationBounds = Rectangle.Empty;
            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;
            socialCreateSubmitBounds = Rectangle.Empty;
            socialNotificationClearAllBounds = Rectangle.Empty;
            socialDetailLikeBounds = Rectangle.Empty;
            socialDetailCommentSendBounds = Rectangle.Empty;
            socialDetailImagePrevBounds = Rectangle.Empty;
            socialDetailImageNextBounds = Rectangle.Empty;

            List<StardewConnectPost> profilePosts = GetSelectedProfilePosts();
            RefreshSocialNotifiedPostIds(StardewConnectManager.GetPostsSnapshot());

            StardewConnectProfileStats stats = StardewConnectManager.GetProfileStatsSnapshot(
                selectedSocialProfileActorName,
                selectedSocialProfileActorIsPlayer);

            List<StardewConnectProfileInteraction> topFrom = StardewConnectManager.GetTopInteractionsFrom(
                selectedSocialProfileActorName,
                selectedSocialProfileActorIsPlayer,
                3);

            List<StardewConnectProfileInteraction> topTo = StardewConnectManager.GetTopInteractionsTo(
                selectedSocialProfileActorName,
                selectedSocialProfileActorIsPlayer,
                3);

            b.End();

            Rectangle clipRect = SocialContentViewportRect;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int cardX = xPositionOnScreen + SocialPostCardX;
            int cardWidth = SocialPostCardWidth;
            int cursorY = yPositionOnScreen + SocialViewportYOffset + SocialPostTopPadding - (int)MathF.Floor(socialProfileScrollOffset);
            int drawMinY = clipRect.Top - SocialRenderOverscan;
            int drawMaxY = clipRect.Bottom + SocialRenderOverscan;

            Rectangle headerBounds = new Rectangle(cardX, cursorY, cardWidth, SocialProfileAvatarHeight + 30);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                headerBounds.X,
                headerBounds.Y,
                headerBounds.Width / 2 + 5,
                headerBounds.Height,
                new Color(255, 255, 255, 230),
                1f,
                false);

            int halfWidth = (headerBounds.Width - 40) / 2;
            Rectangle avatarBounds = new Rectangle(headerBounds.X + 15, headerBounds.Y + 15, halfWidth, SocialProfileAvatarHeight);
            Rectangle infoBounds = new Rectangle(avatarBounds.Right + 10, avatarBounds.Y, halfWidth, SocialProfileAvatarHeight);

            DrawSocialActorIcon(
                b,
                selectedSocialProfileActorName,
                selectedSocialProfileActorIsPlayer,
                avatarBounds);

            string birthdayLabel = GetSocialProfileBirthdayLabel(selectedSocialProfileActorName, selectedSocialProfileActorIsPlayer);
            string ageLabel = GetSocialProfileAgeLabel(selectedSocialProfileActorName, selectedSocialProfileActorIsPlayer);
            string actorDisplayName = ResolveSocialActorDisplayName(selectedSocialProfileActorName, selectedSocialProfileActorIsPlayer);

            string[] infoLines =
            {
                $"Name: {actorDisplayName}",
                $"Age: {ageLabel}",
                $"Birthday: {birthdayLabel}"
            };

            int infoLineHeight = Math.Max(24, infoBounds.Height / 3);
            for (int i = 0; i < infoLines.Length; i++)
            {
                string line = infoLines[i];
                Vector2 size = Game1.smallFont.MeasureString(line);
                float lineX = infoBounds.X + 8;
                float lineY = infoBounds.Y + (i * infoLineHeight) + (infoLineHeight - size.Y) / 2f;
                b.DrawString(Game1.smallFont, line, new Vector2(lineX, lineY), Color.Black);
            }

            cursorY += headerBounds.Height + SocialProfileSectionSpacing;

            Rectangle statsBounds = new Rectangle(cardX, cursorY, cardWidth, SocialProfileStatsHeight);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                statsBounds.X,
                statsBounds.Y,
                statsBounds.Width,
                statsBounds.Height,
                new Color(255, 255, 255, 230),
                1f,
                false);

            int statsTextX = statsBounds.X + 18;
            int statsTopY = statsBounds.Y + 16;
            int statsLineHeight = Math.Max(22, (int)Game1.smallFont.MeasureString("A").Y + 6);

            b.DrawString(Game1.smallFont, $"Total posts: {stats.TotalPosts}", new Vector2(statsTextX, statsTopY), Color.Black);

            int metricIconX = statsTextX + 82;

            int receivedLineY = statsTopY + statsLineHeight;
            b.DrawString(Game1.smallFont, "Received", new Vector2(statsTextX, receivedLineY), Color.Black);

            Rectangle receivedHeartBounds = new Rectangle(metricIconX + 30, receivedLineY + 8, SocialProfileStatIconSize, SocialProfileStatIconSize);
            b.Draw(Game1.mouseCursors, receivedHeartBounds, new Rectangle(211, 428, 7, 7), Color.White);
            b.DrawString(Game1.smallFont, stats.TotalLikesReceived.ToString(), new Vector2(receivedHeartBounds.Right + 6, receivedLineY), Color.Black);

            Rectangle receivedCommentBounds = new Rectangle(receivedHeartBounds.Right + 125, receivedLineY + 6, SocialProfileStatIconSize, SocialProfileStatIconSize);
            b.Draw(Game1.mouseCursors, receivedCommentBounds, new Rectangle(139, 465, 24, 24), Color.White);
            b.DrawString(Game1.smallFont, stats.TotalCommentsReceived.ToString(), new Vector2(receivedCommentBounds.Right + 6, receivedLineY), Color.Black);

            int sentLineY = receivedLineY + statsLineHeight;
            b.DrawString(Game1.smallFont, "Sent", new Vector2(statsTextX, sentLineY), Color.Black);

            Rectangle sentHeartBounds = new Rectangle(metricIconX + 30, sentLineY + 8, SocialProfileStatIconSize, SocialProfileStatIconSize);
            b.Draw(Game1.mouseCursors, sentHeartBounds, new Rectangle(211, 428, 7, 7), Color.White);
            b.DrawString(Game1.smallFont, stats.TotalLikesGiven.ToString(), new Vector2(sentHeartBounds.Right + 6, sentLineY), Color.Black);

            Rectangle sentCommentBounds = new Rectangle(sentHeartBounds.Right + 125, sentLineY + 6, SocialProfileStatIconSize, SocialProfileStatIconSize);
            b.Draw(Game1.mouseCursors, sentCommentBounds, new Rectangle(139, 465, 24, 24), Color.White);
            b.DrawString(Game1.smallFont, stats.TotalCommentsGiven.ToString(), new Vector2(sentCommentBounds.Right + 6, sentLineY), Color.Black);

            cursorY += statsBounds.Height + SocialProfileSectionSpacing;

            Rectangle interactionBounds = new Rectangle(cardX, cursorY, cardWidth, SocialProfileInteractionHeight);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                interactionBounds.X,
                interactionBounds.Y,
                interactionBounds.Width,
                interactionBounds.Height,
                new Color(255, 255, 255, 230),
                1f,
                false);

            int columnGap = 8;
            int innerPadding = 15;
            int columnWidth = (interactionBounds.Width - innerPadding * 2 - columnGap) / 2;
            Rectangle fromBounds = new Rectangle(interactionBounds.X + innerPadding, interactionBounds.Y + innerPadding, columnWidth, interactionBounds.Height - innerPadding * 2);
            Rectangle toBounds = new Rectangle(fromBounds.Right + columnGap, fromBounds.Y, columnWidth, fromBounds.Height);

            DrawSocialInteractionColumn(b, fromBounds, "Top Interact From", topFrom);
            DrawSocialInteractionColumn(b, toBounds, "Top Interact To", topTo);

            cursorY += interactionBounds.Height + SocialProfileSectionSpacing;

            Rectangle postsHeaderBounds = new Rectangle(cardX, cursorY, cardWidth, SocialProfilePostsHeaderHeight);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                postsHeaderBounds.X,
                postsHeaderBounds.Y,
                postsHeaderBounds.Width,
                postsHeaderBounds.Height,
                new Color(255, 255, 255, 230),
                1f,
                false);
            b.DrawString(Game1.smallFont, "Posts (Newest to Oldest)", new Vector2(postsHeaderBounds.X + 14, postsHeaderBounds.Y + 9), Color.Black);

            cursorY += postsHeaderBounds.Height + SocialPostSpacing;

            if (profilePosts.Count == 0)
            {
                b.DrawString(Game1.smallFont, "No post yet.", new Vector2(cardX + 14, cursorY + 10), Color.Black);
            }
            else
            {
                foreach (StardewConnectPost post in profilePosts)
                {
                    int selectedAttachmentIndex = GetFeedPostImageIndex(post);
                    int postHeight = MeasureSocialPostCardHeight(
                        post,
                        includeAllComments: false,
                        selectedAttachmentIndex: selectedAttachmentIndex);

                    if (cursorY + postHeight < drawMinY)
                    {
                        cursorY += postHeight + SocialPostSpacing;
                        continue;
                    }

                    if (cursorY > drawMaxY)
                        break;

                    DrawSocialPostCard(
                        b,
                        post,
                        cardX,
                        cursorY,
                        includeAllComments: false,
                        context: SocialCardRenderContext.Profile,
                        selectedAttachmentIndex: selectedAttachmentIndex);

                    cursorY += postHeight + SocialPostSpacing;
                }
            }

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            DrawSocialLikeTooltipIfHovered(b, profilePosts, socialProfileLikeHoverBounds);
            DrawSocialTagTooltipIfHovered(b, profilePosts, socialProfileTagHoverBounds, socialProfileTagHoverText, SocialContentViewportRect);
        }

        private void DrawSocialInteractionColumn(
            SpriteBatch b,
            Rectangle bounds,
            string title,
            List<StardewConnectProfileInteraction> interactions)
        {
            b.DrawString(Game1.smallFont, title, new Vector2(bounds.X, bounds.Y), Color.Black);

            int lineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            int y = bounds.Y + lineHeight + 4;

            if (interactions == null || interactions.Count == 0)
            {
                b.DrawString(Game1.smallFont, "No data", new Vector2(bounds.X, y), Color.DimGray);
                return;
            }

            for (int i = 0; i < interactions.Count && i < 3; i++)
            {
                StardewConnectProfileInteraction row = interactions[i];
                string actorDisplayName = ResolveSocialActorDisplayNameGuess(row.ActorName);
                if (string.IsNullOrWhiteSpace(actorDisplayName))
                    actorDisplayName = "Someone";

                string line = $"{i + 1}. {actorDisplayName} ({row.Count})";
                b.DrawString(Game1.smallFont, line, new Vector2(bounds.X, y), Color.Black);
                y += lineHeight;
            }
        }

        private int DrawSocialPostCard(
            SpriteBatch b,
            StardewConnectPost post,
            int cardX,
            int cardY,
            bool includeAllComments,
            SocialCardRenderContext context,
            int selectedAttachmentIndex)
        {
            int cardWidth = SocialPostCardWidth;
            int cardHeight = MeasureSocialPostCardHeight(post, includeAllComments, selectedAttachmentIndex);
            Rectangle cardBounds = new Rectangle(cardX, cardY, cardWidth, cardHeight);

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                cardBounds.X,
                cardBounds.Y,
                cardBounds.Width,
                cardBounds.Height,
                new Color(255, 255, 255, 230),
                1f,
                false);

            int unreadCommentCount = StardewConnectManager.GetUnreadCommentCount(post);
            bool showUnreadCommentCount = (ModEntry.Config?.ShowUnreadComment ?? true) && unreadCommentCount > 0;
            bool isNotificationPost = IsSocialNotificationPost(post);
            bool shouldShowNotificationBanner = isNotificationPost || showUnreadCommentCount;
            if (shouldShowNotificationBanner)
                DrawSocialNotificationBanner(b, cardBounds, isNotificationPost, showUnreadCommentCount ? unreadCommentCount : 0);

            if (context == SocialCardRenderContext.Feed)
                socialFeedPostBounds[post.Id] = cardBounds;
            else if (context == SocialCardRenderContext.Profile)
                socialProfilePostBounds[post.Id] = cardBounds;

            int baseLineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            int postLineHeight = GetSocialScaledLineHeight(SocialPostTextScale);
            int cursorY = cardY + 15;

            Rectangle actorIconBounds = new Rectangle(cardX + 15, cursorY, SocialActorIconSize, SocialActorIconSize);
            DrawSocialActorIcon(b, post.AuthorName, post.AuthorIsPlayer, actorIconBounds);
            RegisterSocialProfileIconBounds(context, actorIconBounds, post.AuthorName, post.AuthorIsPlayer);

            DrawSocialHeaderWithSmallMeta(
                b,
                post.AuthorName,
                post.AuthorIsPlayer,
                $"at {FormatSocialPostDateTime(post.Season, post.Day, post.TimeOfDay)}",
                new Vector2(actorIconBounds.Right + 10, cursorY + 10),
                Color.Black,
                Color.DarkSlateGray);

            cursorY += SocialActorIconSize + 10;

            if (!string.IsNullOrWhiteSpace(post.Text))
            {
                int postTextWrapWidth = GetSocialScaledWrapWidth(SocialPostTextMaxWidth, SocialPostTextScale);
                List<string> lines = SplitTextIntoLines(post.Text, Game1.smallFont, postTextWrapWidth);
                foreach (string line in lines)
                {
                    b.DrawString(
                        Game1.smallFont,
                        line,
                        new Vector2(cardX + 15, cursorY),
                        Color.Black,
                        0f,
                        Vector2.Zero,
                        SocialPostTextScale,
                        SpriteEffects.None,
                        0f);
                    cursorY += postLineHeight;
                }

                cursorY += 6;
            }

            string imageTagText = GetSocialPostTagText(post);
            if (!string.IsNullOrWhiteSpace(imageTagText))
            {
                int tagLineHeight = GetSocialImageTagLineHeight();
                int tagTopY = cursorY;
                string tagPreviewText = BuildSocialTagPreviewText(imageTagText, SocialPostTextMaxWidth);

                b.DrawString(
                    Game1.smallFont,
                    tagPreviewText,
                    new Vector2(cardX + 15, cursorY),
                    Color.DimGray,
                    0f,
                    Vector2.Zero,
                    SocialImageTagScale,
                    SpriteEffects.None,
                    0f);

                int previewWidth = MeasureScaledSocialTagWidth(tagPreviewText);
                int hoverWidth = Math.Max(1, Math.Min(SocialPostTextMaxWidth, previewWidth + 4));
                Rectangle tagHoverBounds = new Rectangle(cardX + 15, tagTopY, hoverWidth, tagLineHeight);
                RegisterSocialTagHoverBounds(context, post, tagHoverBounds, imageTagText);

                cursorY += tagLineHeight + SocialImageTagBottomPadding;
            }

            int attachmentCount = StardewConnectManager.GetAttachmentCount(post);
            int attachmentAreaHeight = GetSocialAttachmentDrawHeight(post, selectedAttachmentIndex);
            if (attachmentCount > 0 && attachmentAreaHeight > 0)
            {
                int attachmentIndex = Math.Clamp(selectedAttachmentIndex, 0, attachmentCount - 1);
                if (TryGetSocialAttachmentTexture(post, attachmentIndex, out Texture2D attachmentTexture))
                {
                    int imageAreaWidth = cardWidth - 30;
                    (int drawWidth, int drawHeight) = GetAdaptiveSocialImageSize(
                        attachmentTexture.Width,
                        attachmentTexture.Height,
                        imageAreaWidth,
                        SocialImageMaxHeight);
                    int imageX = cardX + 15 + Math.Max(0, (imageAreaWidth - drawWidth) / 2);
                    int imageY = cursorY + Math.Max(0, (attachmentAreaHeight - drawHeight) / 2);
                    Rectangle imageBounds = new Rectangle(imageX, imageY, drawWidth, drawHeight);
                    b.Draw(attachmentTexture, imageBounds, Color.White);

                    if (attachmentCount > 1)
                    {
                        int imageAreaLeft = cardX + 15;
                        int imageAreaRight = cardX + cardWidth - 15;
                        int navY = cursorY + Math.Max(0, attachmentAreaHeight / 2 - SocialImageNavButtonSize / 2);

                        Rectangle prevBounds = new Rectangle(
                            imageAreaLeft + 6,
                            navY,
                            SocialImageNavButtonSize,
                            SocialImageNavButtonSize);
                        Rectangle nextBounds = new Rectangle(
                            imageAreaRight - SocialImageNavButtonSize - 6,
                            navY,
                            SocialImageNavButtonSize,
                            SocialImageNavButtonSize);

                        DrawSocialImageNavButton(b, prevBounds, isNext: false);
                        DrawSocialImageNavButton(b, nextBounds, isNext: true);

                        if (context == SocialCardRenderContext.Feed)
                        {
                            socialFeedPostImagePrevBounds[post.Id] = prevBounds;
                            socialFeedPostImageNextBounds[post.Id] = nextBounds;
                        }
                        else if (context == SocialCardRenderContext.Detail)
                        {
                            socialDetailImagePrevBounds = prevBounds;
                            socialDetailImageNextBounds = nextBounds;
                        }
                    }

                    string indexLabel = $"{attachmentIndex + 1}/{attachmentCount}";
                    Vector2 indexSize = Game1.smallFont.MeasureString(indexLabel);
                    b.DrawString(
                        Game1.smallFont,
                        indexLabel,
                        new Vector2(imageBounds.X + (imageBounds.Width - indexSize.X) / 2f, imageBounds.Bottom - indexSize.Y - 4),
                        Color.White);
                }

                cursorY += attachmentAreaHeight + 10;
            }


            // Post like section
            Rectangle likeIconBounds = new Rectangle(cardX + 18, cursorY + 2, 24, 24);
            Rectangle heartRect = StardewConnectManager.IsPostLikedByPlayer(post)
                ? new Rectangle(211, 428, 7, 7)
                : new Rectangle(218, 428, 7, 7);
            b.Draw(Game1.mouseCursors, likeIconBounds, heartRect, Color.White);

            Rectangle likeClickBounds = new Rectangle(cardX + 14, cursorY, 92, 30);
            if (context == SocialCardRenderContext.Feed)
                socialFeedLikeBounds[post.Id] = likeClickBounds;
            else if (context == SocialCardRenderContext.Detail)
                socialDetailLikeBounds = likeClickBounds;
            else if (context == SocialCardRenderContext.Profile)
                socialProfileLikeBounds[post.Id] = likeClickBounds;

            string likeCountText = post.LikedBy.Count.ToString();
            Vector2 likeCountSize = Game1.smallFont.MeasureString(likeCountText);
            Rectangle likeCountBounds = new Rectangle(
                likeIconBounds.Right + 6,
                cursorY - 1,
                Math.Max(1, (int)Math.Ceiling(likeCountSize.X) + 6),
                Math.Max(1, baseLineHeight + 4));

            if (context == SocialCardRenderContext.Feed)
                socialFeedLikeHoverBounds[post.Id] = likeCountBounds;
            else if (context == SocialCardRenderContext.Detail)
                socialDetailLikeHoverBounds = likeCountBounds;
            else if (context == SocialCardRenderContext.Profile)
                socialProfileLikeHoverBounds[post.Id] = likeCountBounds;

            b.DrawString(Game1.smallFont, likeCountText, new Vector2(likeIconBounds.Right + 8, cursorY), Color.Black);

            // Post comment section
            Rectangle commentIconBounds = new Rectangle(cardX + 132, cursorY + 4, 24, 24);
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(139, 465, 24, 24),
                commentIconBounds.X,
                commentIconBounds.Y,
                commentIconBounds.Width,
                commentIconBounds.Height,
                new Color(240, 240, 240, 220),
                1f,
                false);
            List<StardewConnectComment> comments = post.Comments ?? new List<StardewConnectComment>();
            b.DrawString(Game1.smallFont, comments.Count.ToString(), new Vector2(commentIconBounds.Right + 8, cursorY), Color.Black);

            if (comments.Count > 0)
            {
                int dividerY = cursorY + 33;
                b.Draw(
                    Game1.staminaRect,
                    new Rectangle(cardX + 15, dividerY, cardWidth - 30, 2),
                    new Color(0, 0, 0, 90));
            }

            if (context == SocialCardRenderContext.Feed)
                socialFeedCommentBounds[post.Id] = new Rectangle(commentIconBounds.X - 4, cursorY, 94, 30);

            cursorY += comments.Count > 0 ? 44 : 34;

            int minCommentIndex = includeAllComments
                ? 0
                : Math.Max(0, comments.Count - SocialCommentPreviewCount);

            for (int i = comments.Count - 1; i >= minCommentIndex; i--)
            {
                StardewConnectComment comment = comments[i];
                int commentHeight = DrawSocialCommentBlock(b, comment, cardX + 15, cursorY, SocialPostTextMaxWidth - 20, context);
                cursorY += commentHeight;
            }

            return cardHeight;
        }

        private int DrawSocialCommentBlock(SpriteBatch b, StardewConnectComment comment, int x, int y, int maxTextWidth, SocialCardRenderContext context)
        {
            int headerLineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            int commentLineHeight = GetSocialScaledLineHeight(SocialCommentTextScale);
            int startY = y;

            Rectangle iconBounds = new Rectangle(x, y + 2, SocialCommentIconSize, SocialCommentIconSize);
            DrawSocialActorIcon(b, comment.AuthorName, comment.AuthorIsPlayer, iconBounds);
            RegisterSocialProfileIconBounds(context, iconBounds, comment.AuthorName, comment.AuthorIsPlayer);

            int textX = iconBounds.Right + 8;
            DrawSocialHeaderWithSmallMeta(
                b,
                comment.AuthorName,
                comment.AuthorIsPlayer,
                $"at {FormatSocialPostDateTime(comment.Season, comment.Day, comment.TimeOfDay)}",
                new Vector2(textX, y),
                Color.DarkSlateGray,
                Color.DimGray);

            int textY = y + headerLineHeight;
            int commentTextWrapWidth = GetSocialScaledWrapWidth(
                Math.Max(80, maxTextWidth - SocialCommentIconSize - 8),
                SocialCommentTextScale);
            List<string> commentLines = SplitTextIntoLines(comment.Text ?? "", Game1.smallFont, commentTextWrapWidth);
            if (commentLines.Count == 0)
                commentLines.Add("");

            foreach (string line in commentLines)
            {
                b.DrawString(
                    Game1.smallFont,
                    line,
                    new Vector2(textX, textY),
                    Color.Black,
                    0f,
                    Vector2.Zero,
                    SocialCommentTextScale,
                    SpriteEffects.None,
                    0f);
                textY += commentLineHeight;
            }

            int contentHeight = (textY - startY) + 2;
            int iconHeight = SocialCommentIconSize + 4;
            return Math.Max(contentHeight, iconHeight) + 4;
        }

        private void RegisterSocialProfileIconBounds(SocialCardRenderContext context, Rectangle bounds, string actorName, bool actorIsPlayer)
        {
            string resolvedActorName = ResolveSocialProfileActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return;

            var target = new SocialProfileClickableTarget
            {
                Bounds = bounds,
                ActorName = resolvedActorName,
                ActorIsPlayer = actorIsPlayer
            };

            if (context == SocialCardRenderContext.Feed)
                socialFeedProfileIconBounds.Add(target);
            else if (context == SocialCardRenderContext.Detail)
                socialDetailProfileIconBounds.Add(target);
            else if (context == SocialCardRenderContext.Profile)
                socialProfileIconBounds.Add(target);
        }

        private void DrawSocialHeaderWithSmallMeta(
            SpriteBatch b,
            string actorName,
            bool actorIsPlayer,
            string metaText,
            Vector2 position,
            Color nameColor,
            Color metaColor)
        {
            string safeName = ResolveSocialActorDisplayName(actorName, actorIsPlayer);
            b.DrawString(Game1.smallFont, safeName, position, nameColor);

            if (string.IsNullOrWhiteSpace(metaText))
                return;

            Vector2 nameSize = Game1.smallFont.MeasureString(safeName);
            Vector2 metaPosition = new Vector2(
                position.X + nameSize.X + 4f,
                position.Y + SocialHeaderMetaYOffset);

            b.DrawString(
                Game1.smallFont,
                metaText,
                metaPosition,
                metaColor,
                0f,
                Vector2.Zero,
                SocialHeaderMetaScale,
                SpriteEffects.None,
                0f);
        }

        private int MeasureSocialPostCardHeight(StardewConnectPost post, bool includeAllComments, int selectedAttachmentIndex)
        {
            string cacheKey = BuildSocialCardHeightCacheKey(post, includeAllComments, selectedAttachmentIndex);
            if (socialCardHeightCache.TryGetValue(cacheKey, out int cachedHeight))
                return cachedHeight;

            int lineHeight = GetSocialScaledLineHeight(SocialPostTextScale);
            int height = 15 + SocialActorIconSize + 10;

            if (!string.IsNullOrWhiteSpace(post.Text))
            {
                int postTextWrapWidth = GetSocialScaledWrapWidth(SocialPostTextMaxWidth, SocialPostTextScale);
                List<string> lines = SplitTextIntoLines(post.Text, Game1.smallFont, postTextWrapWidth);
                height += (lines.Count * lineHeight) + 6;
            }

            height += MeasureSocialAttachmentTagHeight(post);

            int attachmentHeight = GetSocialAttachmentDrawHeight(post, selectedAttachmentIndex);
            if (attachmentHeight > 0)
                height += attachmentHeight + 10;

            List<StardewConnectComment> comments = post.Comments ?? new List<StardewConnectComment>();
            height += comments.Count > 0 ? 44 : 34;

            int minCommentIndex = includeAllComments
                ? 0
                : Math.Max(0, comments.Count - SocialCommentPreviewCount);

            for (int i = comments.Count - 1; i >= minCommentIndex; i--)
            {
                StardewConnectComment comment = comments[i];
                height += MeasureSocialCommentHeight(comment, SocialPostTextMaxWidth - 20);

            }

            int measuredHeight = height + 10;

            if (socialCardHeightCache.Count >= SocialCardHeightCacheLimit)
                socialCardHeightCache.Clear();

            socialCardHeightCache[cacheKey] = measuredHeight;

            return measuredHeight;
        }

        private int MeasureSocialCommentHeight(StardewConnectComment comment, int maxTextWidth)
        {
            int headerLineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            int lineHeight = GetSocialScaledLineHeight(SocialCommentTextScale);
            int commentTextWrapWidth = GetSocialScaledWrapWidth(
                Math.Max(80, maxTextWidth - SocialCommentIconSize - 8),
                SocialCommentTextScale);
            List<string> lines = SplitTextIntoLines(comment.Text ?? "", Game1.smallFont, commentTextWrapWidth);
            if (lines.Count == 0)
                lines.Add("");

            int textHeight = headerLineHeight + (lines.Count * lineHeight) + 2;
            int iconHeight = SocialCommentIconSize + 4;
            return Math.Max(textHeight, iconHeight) + 4;
        }

        private int MeasureSocialAttachmentTagHeight(StardewConnectPost post)
        {
            string imageTagText = GetSocialPostTagText(post);
            if (string.IsNullOrWhiteSpace(imageTagText))
                return 0;

            return GetSocialImageTagLineHeight() + SocialImageTagBottomPadding;
        }

        private int GetSocialImageTagLineHeight()
        {
            return GetSocialScaledLineHeight(SocialImageTagScale);
        }

        private static int GetSocialScaledLineHeight(float scale)
        {
            int baseLineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            float safeScale = Math.Max(0.01f, scale);
            return Math.Max(1, (int)Math.Ceiling(baseLineHeight * safeScale));
        }

        private static int GetSocialScaledWrapWidth(int maxWidth, float scale)
        {
            float safeScale = Math.Max(0.01f, scale);
            return Math.Max(1, (int)Math.Floor(maxWidth / safeScale));
        }

        private static (int Width, int Height) GetAdaptiveSocialImageSize(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
        {
            int safeSourceWidth = Math.Max(1, sourceWidth);
            int safeSourceHeight = Math.Max(1, sourceHeight);
            int safeMaxWidth = Math.Max(1, maxWidth);
            int safeMaxHeight = Math.Max(1, maxHeight);

            // Portrait photos are capped to a narrower width to avoid overly tall post cards.
            if (safeSourceHeight > safeSourceWidth)
                safeMaxWidth = Math.Max(1, (int)Math.Round(safeMaxWidth * SocialPortraitImageMaxWidthRatio));

            float scale = Math.Min(
                safeMaxWidth / (float)safeSourceWidth,
                safeMaxHeight / (float)safeSourceHeight);

            int drawWidth = Math.Max(1, (int)Math.Round(safeSourceWidth * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(safeSourceHeight * scale));
            return (drawWidth, drawHeight);
        }

        private string GetSocialPostTagText(StardewConnectPost post)
        {
            if (!IsSocialImageTagVisible() || post == null)
                return "";

            string rawTag = StardewConnectManager.GetPostTag(post);
            if (string.IsNullOrWhiteSpace(rawTag))
                return "";

            string displayTag = string.Join("; ", rawTag
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag)));

            if (string.IsNullOrWhiteSpace(displayTag))
                return "";

            return displayTag;
        }

        private string BuildSocialTagPreviewText(string fullTagText, int maxScaledWidth)
        {
            if (string.IsNullOrWhiteSpace(fullTagText))
                return "";

            int safeMaxScaledWidth = Math.Max(1, maxScaledWidth);
            float maxUnscaledWidth = safeMaxScaledWidth / Math.Max(0.01f, SocialImageTagScale);
            if (Game1.smallFont.MeasureString(fullTagText).X <= maxUnscaledWidth)
                return fullTagText;

            const string ellipsis = "...";
            if (Game1.smallFont.MeasureString(ellipsis).X > maxUnscaledWidth)
                return ellipsis;

            int maxChars = fullTagText.Length;
            while (maxChars > 0)
            {
                string candidate = fullTagText.Substring(0, maxChars).TrimEnd() + ellipsis;
                if (Game1.smallFont.MeasureString(candidate).X <= maxUnscaledWidth)
                    return candidate;

                maxChars--;
            }

            return ellipsis;
        }

        private int MeasureScaledSocialTagWidth(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return Math.Max(1, (int)Math.Ceiling(Game1.smallFont.MeasureString(text).X * SocialImageTagScale));
        }

        private void RegisterSocialTagHoverBounds(
            SocialCardRenderContext context,
            StardewConnectPost post,
            Rectangle bounds,
            string fullTagText)
        {
            if (post == null || string.IsNullOrWhiteSpace(post.Id) || string.IsNullOrWhiteSpace(fullTagText))
                return;

            if (context == SocialCardRenderContext.Feed)
            {
                socialFeedTagHoverBounds[post.Id] = bounds;
                socialFeedTagHoverText[post.Id] = fullTagText;
            }
            else if (context == SocialCardRenderContext.Profile)
            {
                socialProfileTagHoverBounds[post.Id] = bounds;
                socialProfileTagHoverText[post.Id] = fullTagText;
            }
            else if (context == SocialCardRenderContext.Detail)
            {
                socialDetailTagHoverBounds = bounds;
                socialDetailTagHoverText = fullTagText;
            }
        }

        private bool IsSocialImageTagVisible()
        {
            return ModEntry.Config?.ShowSocialImageTags ?? true;
        }

        private int GetSocialAttachmentDrawHeight(StardewConnectPost post, int selectedAttachmentIndex)
        {
            int attachmentCount = StardewConnectManager.GetAttachmentCount(post);
            if (attachmentCount <= 0)
                return 0;

            int maxHeight = 0;
            int clampedSelectedIndex = Math.Clamp(selectedAttachmentIndex, 0, attachmentCount - 1);

            if (TryGetSocialAttachmentTexture(post, clampedSelectedIndex, out Texture2D selectedTexture))
            {
                (_, int selectedDrawHeight) = GetAdaptiveSocialImageSize(
                    selectedTexture.Width,
                    selectedTexture.Height,
                    SocialImageMaxWidth,
                    SocialImageMaxHeight);
                maxHeight = Math.Max(1, selectedDrawHeight);
            }

            for (int i = 0; i < attachmentCount; i++)
            {
                if (i == clampedSelectedIndex)
                    continue;

                if (!TryGetSocialAttachmentTexture(post, i, out Texture2D attachmentTexture))
                    continue;

                (_, int drawHeight) = GetAdaptiveSocialImageSize(
                    attachmentTexture.Width,
                    attachmentTexture.Height,
                    SocialImageMaxWidth,
                    SocialImageMaxHeight);
                maxHeight = Math.Max(maxHeight, drawHeight);
            }

            return maxHeight;
        }

        private bool TryGetSocialAttachmentTexture(StardewConnectPost post, int attachmentIndex, out Texture2D texture)
        {
            texture = null!;

            string absolutePath = StardewConnectManager.ResolveAttachmentAbsolutePath(post, attachmentIndex);
            if (string.IsNullOrWhiteSpace(absolutePath))
                return false;

            if (socialImageCache.TryGetValue(absolutePath, out Texture2D? cachedTexture) && cachedTexture != null)
            {
                texture = cachedTexture;
                return true;
            }

            if (socialFailedImagePaths.Contains(absolutePath))
                return false;

            if (!File.Exists(absolutePath))
            {
                socialFailedImagePaths.Add(absolutePath);
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Texture2D loaded = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                socialImageCache[absolutePath] = loaded;
                texture = loaded;
                return true;
            }
            catch (Exception ex)
            {
                socialFailedImagePaths.Add(absolutePath);
                ModEntry.SMonitor.Log($"Failed to load StardewConnect image '{absolutePath}': {ex.Message}", LogLevel.Warn);
                return false;
            }
        }

        private void DrawSocialActorIcon(SpriteBatch b, string actorName, bool actorIsPlayer, Rectangle bounds)
        {
            if (!actorIsPlayer)
            {
                NPC? npc = Game1.getCharacterFromName(actorName, mustBeVillager: false);
                if (npc?.Portrait != null)
                {
                    b.Draw(npc.Portrait, new Rectangle(bounds.X - 2, bounds.Y + 2, bounds.Width, bounds.Height), new Rectangle(0, 0, 64, 64), Color.White);
                    return;
                }
            }

            if (actorIsPlayer && TryGetSelectedPlayerAvatarTexture(out Texture2D avatarTexture))
            {
                b.Draw(avatarTexture, new Rectangle(bounds.X - 2, bounds.Y + 2, bounds.Width, bounds.Height), Color.White);
                return;
            }

            b.Draw(Game1.staminaRect, new Rectangle(bounds.X - 2, bounds.Y + 2, bounds.Width, bounds.Height), new Color(65, 95, 135, 220));

            string fallbackLetter = "P";
            string fallbackName = ResolveSocialActorDisplayName(actorName, actorIsPlayer);
            if (!string.IsNullOrWhiteSpace(fallbackName))
                fallbackLetter = fallbackName.Trim()[0].ToString().ToUpperInvariant();

            Vector2 letterSize = Game1.smallFont.MeasureString(fallbackLetter);
            Vector2 letterPos = new Vector2(
                bounds.X + (bounds.Width - letterSize.X) / 2f,
                bounds.Y + (bounds.Height - letterSize.Y) / 2f);
            b.DrawString(Game1.smallFont, fallbackLetter, letterPos, Color.White);
        }

        private bool TryGetSelectedPlayerAvatarTexture(out Texture2D texture)
        {
            texture = null!;

            string avatarPath = MessageManager.currentPlayerAvatar;
            if (string.IsNullOrWhiteSpace(avatarPath))
                return false;

            if (socialImageCache.TryGetValue(avatarPath, out Texture2D? cachedTexture) && cachedTexture != null)
            {
                texture = cachedTexture;
                return true;
            }

            if (socialFailedImagePaths.Contains(avatarPath))
                return false;

            if (!File.Exists(avatarPath))
            {
                MessageManager.currentPlayerAvatar = "";
                socialFailedImagePaths.Add(avatarPath);
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(avatarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Texture2D loaded = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                socialImageCache[avatarPath] = loaded;
                texture = loaded;
                return true;
            }
            catch (Exception ex)
            {
                socialFailedImagePaths.Add(avatarPath);
                ModEntry.SMonitor.Log($"Failed to load player avatar '{avatarPath}': {ex.Message}", LogLevel.Warn);
                return false;
            }
        }

        private void DrawSocialNotificationBanner(SpriteBatch b, Rectangle cardBounds, bool isNotificationPost, int unreadCommentCount = 0)
        {
            int bannerSize = 30;

            if (unreadCommentCount > 0)
            {
                string unreadText = unreadCommentCount.ToString();
                Rectangle bannerBounds1 = new Rectangle(cardBounds.Right - bannerSize - 42, cardBounds.Y + 25, bannerSize, bannerSize);
                b.Draw(Game1.staminaRect, bannerBounds1, new Color(0, 0, 0, 100));
                Vector2 unreadTextSize = Game1.smallFont.MeasureString(unreadText);
                b.DrawString(
                    Game1.smallFont,
                    unreadText,
                    new Vector2(
                        bannerBounds1.X + (bannerBounds1.Width - unreadTextSize.X) / 2f,
                        bannerBounds1.Y + (bannerBounds1.Height - unreadTextSize.Y) / 2f),
                    Color.White);
            }

            if (isNotificationPost)
            {
                Rectangle bannerBounds = new Rectangle(cardBounds.Right - bannerSize - 12, cardBounds.Y + 25, bannerSize, bannerSize);
                b.Draw(Game1.staminaRect, bannerBounds, new Color(214, 33, 33, 230));
                string text = "!";
                Vector2 textSize = Game1.smallFont.MeasureString(text);
                b.DrawString(
                    Game1.smallFont,
                    text,
                new Vector2(
                    bannerBounds.X + (bannerBounds.Width - textSize.X) / 2f,
                    bannerBounds.Y + (bannerBounds.Height - textSize.Y) / 2f),
                Color.White);
            }
        }

        private void DrawSocialUnreadBadge(SpriteBatch b, int rightX, int y, int unreadCount)
        {
            string text = Math.Min(99, unreadCount).ToString();
            Vector2 textSize = Game1.smallFont.MeasureString(text);

            int badgeWidth = Math.Max(26, (int)textSize.X + 12);
            int badgeHeight = Math.Max(20, (int)textSize.Y + 4);
            int badgeX = rightX - badgeWidth;

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                badgeX,
                y,
                badgeWidth,
                badgeHeight,
                new Color(220, 0, 0, 170),
                1f,
                false);

            b.DrawString(
                Game1.smallFont,
                text,
                new Vector2(badgeX + (badgeWidth - textSize.X) / 2f, y + (badgeHeight - textSize.Y) / 2f),
                Color.White);
        }

        private List<string> GetRecentLikeUserNames(StardewConnectPost post, int maxCount = SocialLikeTooltipMaxNames)
        {
            if (post?.LikedBy == null || post.LikedBy.Count == 0 || maxCount <= 0)
                return new List<string>();

            List<string> likerNames = post.LikedBy
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => ResolveSocialActorDisplayNameGuess(name.Trim()))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            if (likerNames.Count == 0)
                return new List<string>();

            List<string> recentLikeNames = likerNames
                .AsEnumerable()
                .Reverse()
                .Take(maxCount)
                .ToList();

            if (likerNames.Count > recentLikeNames.Count)
                recentLikeNames.Add("...");

            return recentLikeNames;
        }

        private void DrawSocialLikeTooltipIfHovered(
            SpriteBatch b,
            IEnumerable<StardewConnectPost> posts,
            IReadOnlyDictionary<string, Rectangle> likeHoverBoundsByPostId)
        {
            if (posts == null || likeHoverBoundsByPostId == null)
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            foreach (StardewConnectPost post in posts)
            {
                if (post == null || string.IsNullOrWhiteSpace(post.Id))
                    continue;

                if (!likeHoverBoundsByPostId.TryGetValue(post.Id, out Rectangle hoverBounds))
                    continue;

                if (!hoverBounds.Contains(mouseX, mouseY))
                    continue;

                DrawSocialLikeTooltip(b, GetRecentLikeUserNames(post), mouseX, mouseY);
                return;
            }
        }

        private void DrawSocialLikeTooltipIfHovered(SpriteBatch b, StardewConnectPost? post, Rectangle likeHoverBounds)
        {
            if (post == null || likeHoverBounds.IsEmpty)
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            if (!likeHoverBounds.Contains(mouseX, mouseY))
                return;

            DrawSocialLikeTooltip(b, GetRecentLikeUserNames(post), mouseX, mouseY);
        }

        private void DrawSocialLikeTooltip(SpriteBatch b, IReadOnlyList<string> likerNames, int mouseX, int mouseY)
        {
            if (likerNames == null || likerNames.Count == 0)
                return;

            int lineHeight = Math.Max(22, (int)Game1.smallFont.MeasureString("A").Y + 4);
            int paddingX = 12;
            int paddingY = 10;

            int maxTextWidth = 0;
            foreach (string line in likerNames)
                maxTextWidth = Math.Max(maxTextWidth, (int)Math.Ceiling(Game1.smallFont.MeasureString(line).X));

            int boxWidth = Math.Max(120, maxTextWidth + paddingX * 2);
            int boxHeight = paddingY * 2 + (likerNames.Count * lineHeight);

            int x = mouseX + 24;
            int y = mouseY + 24;
            int maxX = Math.Max(12, Game1.viewport.Width - boxWidth - 12);
            int maxY = Math.Max(12, Game1.viewport.Height - boxHeight - 12);
            x = Math.Clamp(x, 12, maxX);
            y = Math.Clamp(y, 12, maxY);

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                x,
                y,
                boxWidth,
                boxHeight,
                new Color(255, 255, 255, 235),
                1f,
                false);

            for (int i = 0; i < likerNames.Count; i++)
            {
                string line = likerNames[i];
                Color textColor = line == "..." ? Color.DimGray : Color.Black;
                b.DrawString(Game1.smallFont, line, new Vector2(x + paddingX, y + paddingY + (i * lineHeight)), textColor);
            }
        }

        private void DrawSocialTagTooltipIfHovered(
            SpriteBatch b,
            IEnumerable<StardewConnectPost> posts,
            IReadOnlyDictionary<string, Rectangle> tagHoverBoundsByPostId,
            IReadOnlyDictionary<string, string> tagTextByPostId,
            Rectangle viewport)
        {
            if (posts == null || tagHoverBoundsByPostId == null || tagTextByPostId == null)
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            foreach (StardewConnectPost post in posts)
            {
                if (post == null || string.IsNullOrWhiteSpace(post.Id))
                    continue;

                if (!tagHoverBoundsByPostId.TryGetValue(post.Id, out Rectangle hoverBounds))
                    continue;

                if (!tagTextByPostId.TryGetValue(post.Id, out string? fullTagText) || string.IsNullOrWhiteSpace(fullTagText))
                    continue;

                if (!IsPointOnVisibleBounds(hoverBounds, mouseX, mouseY, viewport))
                    continue;

                DrawSocialTagTooltip(b, fullTagText, mouseX, mouseY);
                return;
            }
        }

        private void DrawSocialTagTooltipIfHovered(
            SpriteBatch b,
            StardewConnectPost? post,
            Rectangle tagHoverBounds,
            string fullTagText,
            Rectangle viewport)
        {
            if (post == null || tagHoverBounds.IsEmpty || string.IsNullOrWhiteSpace(fullTagText))
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            if (!IsPointOnVisibleBounds(tagHoverBounds, mouseX, mouseY, viewport))
                return;

            DrawSocialTagTooltip(b, fullTagText, mouseX, mouseY);
        }

        private void DrawSocialTagTooltip(SpriteBatch b, string tagText, int mouseX, int mouseY)
        {
            if (string.IsNullOrWhiteSpace(tagText))
                return;

            List<string> lines = SplitTextIntoLines(tagText, Game1.smallFont, 360);
            if (lines.Count == 0)
                lines.Add(tagText);

            int lineHeight = Math.Max(22, (int)Game1.smallFont.MeasureString("A").Y + 4);
            int paddingX = 12;
            int paddingY = 10;

            int maxTextWidth = 0;
            foreach (string line in lines)
                maxTextWidth = Math.Max(maxTextWidth, (int)Math.Ceiling(Game1.smallFont.MeasureString(line).X));

            int boxWidth = Math.Max(150, maxTextWidth + paddingX * 2);
            int boxHeight = paddingY * 2 + (lines.Count * lineHeight);

            int x = mouseX + 24;
            int y = mouseY + 24;
            int maxX = Math.Max(12, Game1.viewport.Width - boxWidth - 12);
            int maxY = Math.Max(12, Game1.viewport.Height - boxHeight - 12);
            x = Math.Clamp(x, 12, maxX);
            y = Math.Clamp(y, 12, maxY);

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                x,
                y,
                boxWidth,
                boxHeight,
                new Color(255, 255, 255, 235),
                1f,
                false);

            for (int i = 0; i < lines.Count; i++)
                b.DrawString(Game1.smallFont, lines[i], new Vector2(x + paddingX, y + paddingY + (i * lineHeight)), Color.Black);
        }

        private void DrawSocialImageNavButton(SpriteBatch b, Rectangle bounds, bool isNext)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                new Color(0, 0, 0, 140),
                1f,
                false);

            Rectangle source = isNext
                ? Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33)
                : Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44);

            b.Draw(
                Game1.mouseCursors,
                new Rectangle(bounds.X + 8, bounds.Y + 8, bounds.Width - 16, bounds.Height - 16),
                source,
                Color.White);
        }

        private bool HandleSocialLeftClick(int x, int y)
        {
            if (socialCreateMenuOpen)
                return HandleSocialCreateMenuClick(x, y);

            if (socialNotificationMenuOpen)
                return HandleSocialNotificationMenuClick(x, y);

            if (socialProfileMenuOpen)
                return HandleSocialProfileMenuClick(x, y);

            if (string.IsNullOrWhiteSpace(selectedSocialPostId))
            {
                if (socialFeedOpenCreatePostBounds.Contains(x, y))
                {
                    OpenSocialCreatePostMenu();
                    Game1.playSound("smallSelect");
                    return true;
                }

                if (socialFeedOpenProfileBounds.Contains(x, y))
                {
                    OpenSocialProfile(Game1.player?.Name ?? "Player", actorIsPlayer: true);
                    Game1.playSound("smallSelect");
                    return true;
                }

                if (socialFeedOpenNotificationBounds.Contains(x, y))
                {
                    OpenSocialNotificationMenu();
                    Game1.playSound("smallSelect");
                    return true;
                }

                Rectangle viewportRect = SocialContentViewportRect;
                if (!viewportRect.Contains(x, y))
                    return false;

                foreach (SocialProfileClickableTarget target in socialFeedProfileIconBounds)
                {
                    if (!IsPointOnVisibleBounds(target.Bounds, x, y, viewportRect))
                        continue;

                    OpenSocialProfile(target.ActorName, target.ActorIsPlayer);
                    Game1.playSound("smallSelect");
                    return true;
                }

                foreach ((string postId, Rectangle likeBounds) in socialFeedLikeBounds)
                {
                    if (!IsPointOnVisibleBounds(likeBounds, x, y, viewportRect))
                        continue;

                    bool liked = StardewConnectManager.TogglePostLikeByPlayer(postId);
                    Game1.playSound(liked ? "smallSelect" : "cancel");
                    return true;
                }

                foreach ((string postId, Rectangle commentBounds) in socialFeedCommentBounds)
                {
                    if (!IsPointOnVisibleBounds(commentBounds, x, y, viewportRect))
                        continue;

                    OpenSocialPostDetail(postId, returnToProfile: false);
                    Game1.playSound("smallSelect");
                    return true;
                }

                foreach ((string postId, Rectangle postBounds) in socialFeedPostBounds)
                {
                    if (socialFeedPostImagePrevBounds.TryGetValue(postId, out Rectangle prevBounds)
                        && IsPointOnVisibleBounds(prevBounds, x, y, viewportRect))
                    {
                        CycleFeedPostImage(postId, -1);
                        Game1.playSound("shwip");
                        return true;
                    }

                    if (socialFeedPostImageNextBounds.TryGetValue(postId, out Rectangle nextBounds)
                        && IsPointOnVisibleBounds(nextBounds, x, y, viewportRect))
                    {
                        CycleFeedPostImage(postId, 1);
                        Game1.playSound("shwip");
                        return true;
                    }

                    if (!IsPointOnVisibleBounds(postBounds, x, y, viewportRect))
                        continue;

                    OpenSocialPostDetail(postId, returnToProfile: false);
                    Game1.playSound("smallSelect");
                    return true;
                }

                return false;
            }

            Rectangle detailViewport = GetSocialDetailViewportRect();

            StardewConnectPost? selectedPost = StardewConnectManager.GetPost(selectedSocialPostId);

            if (selectedPost != null && selectedPost.AuthorIsPlayer && removeButton.containsPoint(x, y))
            {
                bool deleted = TryDeleteSelectedSocialPost(selectedPost);
                Game1.playSound(deleted ? "trashcan" : "cancel");
                return true;
            }

            foreach (SocialProfileClickableTarget target in socialDetailProfileIconBounds)
            {
                if (!IsPointOnVisibleBounds(target.Bounds, x, y, detailViewport))
                    continue;

                OpenSocialProfile(target.ActorName, target.ActorIsPlayer);
                Game1.playSound("smallSelect");
                return true;
            }

            if (IsPointOnVisibleBounds(socialDetailImagePrevBounds, x, y, detailViewport))
            {
                CycleDetailPostImage(-1);
                Game1.playSound("shwip");
                return true;
            }

            if (IsPointOnVisibleBounds(socialDetailImageNextBounds, x, y, detailViewport))
            {
                CycleDetailPostImage(1);
                Game1.playSound("shwip");
                return true;
            }

            if (IsPointOnVisibleBounds(socialDetailLikeBounds, x, y, detailViewport))
            {
                bool liked = StardewConnectManager.TogglePostLikeByPlayer(selectedSocialPostId);
                Game1.playSound(liked ? "smallSelect" : "cancel");
                return true;
            }

            if (socialDetailCommentSendBounds.Contains(x, y))
            {
                bool created = TryCreatePlayerSocialComment();
                Game1.playSound(created ? "smallSelect" : "cancel");
                return true;
            }

            return false;
        }

        private bool HandleSocialNotificationMenuClick(int x, int y)
        {
            if (socialNotificationClearAllBounds.Contains(x, y))
            {
                bool hadNotifications = GetActiveSocialNotifications().Count > 0;
                if (hadNotifications)
                    DismissAllSocialNotifications();

                Game1.playSound(hadNotifications ? "smallSelect" : "cancel");
                return true;
            }

            Rectangle viewportRect = SocialContentViewportRect;
            if (!viewportRect.Contains(x, y))
                return false;

            foreach ((string key, Rectangle bounds) in socialNotificationItemBounds.ToList())
            {
                if (!IsPointOnVisibleBounds(bounds, x, y, viewportRect))
                    continue;

                if (!socialNotificationItemsByKey.TryGetValue(key, out SocialNotificationEntry? entry)
                    || entry == null
                    || string.IsNullOrWhiteSpace(entry.PostId))
                {
                    continue;
                }

                CloseSocialNotificationMenu();
                OpenSocialPostDetail(entry.PostId, returnToProfile: false);
                Game1.playSound("smallSelect");
                return true;
            }

            return false;
        }

        private bool HandleSocialProfileMenuClick(int x, int y)
        {
            Rectangle viewportRect = SocialContentViewportRect;
            if (!viewportRect.Contains(x, y))
                return false;

            foreach (SocialProfileClickableTarget target in socialProfileIconBounds)
            {
                if (!IsPointOnVisibleBounds(target.Bounds, x, y, viewportRect))
                    continue;

                OpenSocialProfile(target.ActorName, target.ActorIsPlayer);
                Game1.playSound("smallSelect");
                return true;
            }

            foreach ((string postId, Rectangle likeBounds) in socialProfileLikeBounds)
            {
                if (!IsPointOnVisibleBounds(likeBounds, x, y, viewportRect))
                    continue;

                bool liked = StardewConnectManager.TogglePostLikeByPlayer(postId);
                Game1.playSound(liked ? "smallSelect" : "cancel");
                return true;
            }

            foreach ((string postId, Rectangle postBounds) in socialProfilePostBounds)
            {
                if (!IsPointOnVisibleBounds(postBounds, x, y, viewportRect))
                    continue;

                OpenSocialPostDetail(
                    postId,
                    returnToProfile: true,
                    profileActorName: selectedSocialProfileActorName,
                    profileActorIsPlayer: selectedSocialProfileActorIsPlayer);
                Game1.playSound("smallSelect");
                return true;
            }

            return false;
        }

        private bool HandleSocialCreateMenuClick(int x, int y)
        {
            if (socialCreatePrevImageBounds.Contains(x, y))
            {
                MoveCreateCandidate(-1);
                Game1.playSound("shwip");
                return true;
            }

            if (socialCreateNextImageBounds.Contains(x, y))
            {
                MoveCreateCandidate(1);
                Game1.playSound("shwip");
                return true;
            }

            if (socialCreateSelectionToggleBounds.Contains(x, y))
            {
                ToggleCreateCandidateSelection();
                Game1.playSound("smallSelect");
                return true;
            }

            if (socialCreateSubmitBounds.Contains(x, y))
            {
                bool created = TryCreatePlayerSocialPost();
                Game1.playSound(created ? "smallSelect" : "cancel");
                return true;
            }

            return false;
        }

        private void HandleSocialScroll(int direction)
        {
            if (socialCreateMenuOpen)
                return;

            float scrollSteps = GetNormalizedScrollSteps(direction);
            if (Math.Abs(scrollSteps) <= 0.0001f)
                return;

            if (socialNotificationMenuOpen)
            {
                List<SocialNotificationEntry> notifications = GetActiveSocialNotifications();
                ClampSocialNotificationScroll(notifications);

                socialNotificationScrollTarget = Math.Clamp(
                    socialNotificationScrollTarget - scrollSteps * SocialScrollPixelsPerWheelNotch,
                    0f,
                    CalculateSocialNotificationMaxScroll(notifications));

                return;
            }

            if (socialProfileMenuOpen)
            {
                List<StardewConnectPost> profilePosts = GetSelectedProfilePosts();
                ClampSocialProfileScroll(profilePosts);

                socialProfileScrollTarget = Math.Clamp(
                    socialProfileScrollTarget - scrollSteps * SocialScrollPixelsPerWheelNotch,
                    0f,
                    CalculateSocialProfileMaxScroll(profilePosts));

                return;
            }

            if (string.IsNullOrWhiteSpace(selectedSocialPostId))
            {
                List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();
                ClampSocialFeedScroll(posts);

                socialFeedScrollTarget = Math.Clamp(
                    socialFeedScrollTarget - scrollSteps * SocialScrollPixelsPerWheelNotch,
                    0f,
                    CalculateSocialFeedMaxScroll(posts));

                return;
            }

            StardewConnectPost? selectedPost = StardewConnectManager.GetPost(selectedSocialPostId);
            if (selectedPost == null)
                return;

            ClampSocialDetailScroll(selectedPost);
            socialDetailScrollTarget = Math.Clamp(
                socialDetailScrollTarget - scrollSteps * SocialScrollPixelsPerWheelNotch,
                0f,
                CalculateSocialDetailMaxScroll(selectedPost));
        }

        private bool HandleSocialTyping(Keys key)
        {
            if (socialCreateMenuOpen)
            {
                return HandleSocialPostInputKey(key, isRepeat: false);
            }

            if (string.IsNullOrWhiteSpace(selectedSocialPostId))
                return false;

            return HandleSocialCommentInputKey(key, isRepeat: false);
        }

        private bool TryCreatePlayerSocialPost()
        {
            string? newPostId = StardewConnectManager.AddPlayerPostWithAttachments(socialPostDraft, socialCreateSelectedImages);
            if (string.IsNullOrWhiteSpace(newPostId))
                return false;

            CloseSocialCreatePostMenu(clearDraft: true);
            SnapSocialFeedToBottom();
            return true;
        }

        private bool TryCreatePlayerSocialComment()
        {
            if (string.IsNullOrWhiteSpace(selectedSocialPostId))
                return false;

            bool created = StardewConnectManager.AddPlayerComment(selectedSocialPostId, socialCommentDraft);
            if (!created)
                return false;

            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);

            StardewConnectPost? selectedPost = StardewConnectManager.GetPost(selectedSocialPostId);
            if (selectedPost != null)
                ClampSocialDetailScroll(selectedPost);

            return true;
        }

        private bool TryDeleteSelectedSocialPost(StardewConnectPost? selectedPost)
        {
            if (selectedPost == null || !selectedPost.AuthorIsPlayer)
                return false;

            bool returnToProfile = socialDetailReturnToProfile;
            string returnProfileActorName = socialDetailReturnProfileActorName;
            bool returnProfileActorIsPlayer = socialDetailReturnProfileActorIsPlayer;

            bool deleted = StardewConnectManager.DeletePost(selectedPost.Id);
            if (!deleted)
                return false;

            selectedSocialPostId = "";
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialDetailImageIndex = 0;
            socialDetailScrollOffset = 0f;
            socialDetailScrollTarget = 0f;
            socialDetailReturnToProfile = false;
            socialDetailReturnProfileActorName = "";
            socialDetailReturnProfileActorIsPlayer = false;
            socialDetailLikeBounds = Rectangle.Empty;
            socialDetailCommentSendBounds = Rectangle.Empty;
            socialDetailImagePrevBounds = Rectangle.Empty;
            socialDetailImageNextBounds = Rectangle.Empty;
            socialDetailLikeHoverBounds = Rectangle.Empty;
            socialDetailTagHoverBounds = Rectangle.Empty;
            socialDetailTagHoverText = "";

            if (returnToProfile)
            {
                socialProfileMenuOpen = true;
                selectedSocialProfileActorName = returnProfileActorName;
                selectedSocialProfileActorIsPlayer = returnProfileActorIsPlayer;
                ClampSocialProfileScroll();
            }

            return true;
        }

        private void OpenSocialPostDetail(
            string postId,
            bool returnToProfile,
            string profileActorName = "",
            bool profileActorIsPlayer = false)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return;

            StardewConnectPost? post = StardewConnectManager.GetPost(postId);
            if (post == null)
                return;

            selectedSocialPostId = postId;
            socialProfileMenuOpen = false;
            socialNotificationMenuOpen = false;
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialDetailImageIndex = 0;
            socialDetailScrollOffset = 0f;
            socialDetailScrollTarget = 0f;

            socialDetailReturnToProfile = returnToProfile;
            socialDetailReturnProfileActorName = returnToProfile
                ? ResolveSocialProfileActorName(profileActorName, profileActorIsPlayer)
                : "";
            socialDetailReturnProfileActorIsPlayer = returnToProfile && profileActorIsPlayer;

            DismissSocialNotificationsForPost(postId);
        }

        private void OpenSocialProfile(string actorName, bool actorIsPlayer)
        {
            string resolvedActorName = ResolveSocialProfileActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return;

            selectedSocialPostId = "";
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialCreateMenuOpen = false;
            socialNotificationMenuOpen = false;
            socialProfileMenuOpen = true;
            selectedSocialProfileActorName = resolvedActorName;
            selectedSocialProfileActorIsPlayer = actorIsPlayer;
            socialProfileScrollOffset = 0f;
            socialProfileScrollTarget = 0f;

            socialDetailReturnToProfile = false;
            socialDetailReturnProfileActorName = "";
            socialDetailReturnProfileActorIsPlayer = false;

            ClampSocialProfileScroll();
        }

        private void OpenSocialCreatePostMenu()
        {
            socialCreateMenuOpen = true;
            socialNotificationMenuOpen = false;
            socialProfileMenuOpen = false;
            selectedSocialPostId = "";
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialDetailReturnToProfile = false;
            socialDetailReturnProfileActorName = "";
            socialDetailReturnProfileActorIsPlayer = false;
            EnsureCreateImageCandidatesLoaded();
        }

        private void CloseSocialCreatePostMenu(bool clearDraft)
        {
            socialCreateMenuOpen = false;

            if (clearDraft)
            {
                ResetEditableTextFieldState(EditableTextFieldKind.SocialPost);
                socialCreateSelectedImages.Clear();
            }

            socialCreateCandidateImages.Clear();
            socialCreateCandidateImageIndex = -1;
            socialCreateSelectionToggleBounds = Rectangle.Empty;
            socialCreatePrevImageBounds = Rectangle.Empty;
            socialCreateNextImageBounds = Rectangle.Empty;
            socialCreateSubmitBounds = Rectangle.Empty;
        }

        private void OpenSocialNotificationMenu()
        {
            socialCreateMenuOpen = false;
            socialProfileMenuOpen = false;
            socialNotificationMenuOpen = true;
            selectedSocialPostId = "";
            ResetEditableTextFieldState(EditableTextFieldKind.SocialComment);
            socialDetailReturnToProfile = false;
            socialDetailReturnProfileActorName = "";
            socialDetailReturnProfileActorIsPlayer = false;

            List<SocialNotificationEntry> notifications = GetActiveSocialNotifications();
            ClampSocialNotificationScroll(notifications);
        }

        private void CloseSocialNotificationMenu()
        {
            socialNotificationMenuOpen = false;
            socialNotificationItemBounds.Clear();
            socialNotificationItemsByKey.Clear();
            socialNotificationClearAllBounds = Rectangle.Empty;
            socialNotificationScrollOffset = 0f;
            socialNotificationScrollTarget = 0f;
        }

        private void EnsureCreateImageCandidatesLoaded()
        {
            socialCreateCandidateImages.Clear();

            string userCaptureFolderPath = GetCaptureFolderPath(PlayerPhotoFolderName);

            if (Directory.Exists(userCaptureFolderPath))
            {
                socialCreateCandidateImages.AddRange(
                    Directory.GetFiles(userCaptureFolderPath, "*.png")
                        .OrderByDescending(path => File.GetCreationTime(path))
                        .Select(path => Path.GetFileName(path) ?? "")
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                );
            }

            if (socialCreateCandidateImages.Count == 0)
            {
                socialCreateCandidateImageIndex = -1;
            }
            else
            {
                socialCreateCandidateImageIndex = Math.Clamp(socialCreateCandidateImageIndex, 0, socialCreateCandidateImages.Count - 1);
            }

            socialCreateSelectedImages.RemoveAll(selected =>
                !socialCreateCandidateImages.Any(candidate => string.Equals(candidate, selected, StringComparison.OrdinalIgnoreCase)));
        }

        private void MoveCreateCandidate(int delta)
        {
            if (socialCreateCandidateImages.Count == 0)
            {
                socialCreateCandidateImageIndex = -1;
                return;
            }

            socialCreateCandidateImageIndex += delta;
            if (socialCreateCandidateImageIndex < 0)
                socialCreateCandidateImageIndex = socialCreateCandidateImages.Count - 1;
            else if (socialCreateCandidateImageIndex >= socialCreateCandidateImages.Count)
                socialCreateCandidateImageIndex = 0;
        }

        private void ToggleCreateCandidateSelection()
        {
            if (socialCreateCandidateImageIndex < 0 || socialCreateCandidateImageIndex >= socialCreateCandidateImages.Count)
                return;

            string fileName = socialCreateCandidateImages[socialCreateCandidateImageIndex];

            int existingIndex = socialCreateSelectedImages.FindIndex(selected =>
                string.Equals(selected, fileName, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                socialCreateSelectedImages.RemoveAt(existingIndex);
                return;
            }

            if (socialCreateSelectedImages.Count >= SocialCreateSelectionMaxCount)
                return;

            socialCreateSelectedImages.Add(fileName);
        }

        private bool IsCreateImageSelected(string fileName)
        {
            return socialCreateSelectedImages.Any(selected => string.Equals(selected, fileName, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsPointOnVisibleBounds(Rectangle bounds, int x, int y, Rectangle viewport)
        {
            if (!viewport.Contains(x, y))
                return false;

            if (!viewport.Intersects(bounds))
                return false;

            return bounds.Contains(x, y);
        }

        private void CycleFeedPostImage(string postId, int delta)
        {
            StardewConnectPost? post = StardewConnectManager.GetPost(postId);
            if (post == null)
                return;

            int count = StardewConnectManager.GetAttachmentCount(post);
            if (count <= 1)
                return;

            int currentIndex = GetFeedPostImageIndex(post);
            int nextIndex = currentIndex + delta;
            if (nextIndex < 0)
                nextIndex = count - 1;
            else if (nextIndex >= count)
                nextIndex = 0;

            SetFeedPostImageIndex(post, nextIndex);
        }

        private void CycleDetailPostImage(int delta)
        {
            if (string.IsNullOrWhiteSpace(selectedSocialPostId))
                return;

            StardewConnectPost? post = StardewConnectManager.GetPost(selectedSocialPostId);
            if (post == null)
                return;

            int count = StardewConnectManager.GetAttachmentCount(post);
            if (count <= 1)
                return;

            int nextIndex = socialDetailImageIndex + delta;
            if (nextIndex < 0)
                nextIndex = count - 1;
            else if (nextIndex >= count)
                nextIndex = 0;

            socialDetailImageIndex = nextIndex;
        }

        private int GetFeedPostImageIndex(StardewConnectPost post)
        {
            int count = StardewConnectManager.GetAttachmentCount(post);
            if (count <= 0)
                return 0;

            if (!socialFeedPostImageIndices.TryGetValue(post.Id, out int currentIndex))
            {
                socialFeedPostImageIndices[post.Id] = 0;
                return 0;
            }

            int clamped = Math.Clamp(currentIndex, 0, count - 1);
            socialFeedPostImageIndices[post.Id] = clamped;
            return clamped;
        }

        private void SetFeedPostImageIndex(StardewConnectPost post, int index)
        {
            int count = StardewConnectManager.GetAttachmentCount(post);
            if (count <= 0)
            {
                socialFeedPostImageIndices.Remove(post.Id);
                return;
            }

            socialFeedPostImageIndices[post.Id] = Math.Clamp(index, 0, count - 1);
        }

        private void SnapSocialFeedOnOpen()
        {
            List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();
            if (posts.Count == 0)
            {
                socialFeedScrollOffset = 0f;
                socialFeedScrollTarget = 0f;
                return;
            }

            StardewConnectVisitSnapshot lastVisit = StardewConnectManager.GetLastSocialVisitSnapshot();
            int targetIndex = FindSocialOpenTargetPostIndexByLastVisit(posts, lastVisit);
            if (targetIndex < 0)
            {
                SnapSocialFeedToBottom();
                return;
            }

            SnapSocialFeedToPostIndex(posts, targetIndex);
        }

        private int FindSocialOpenTargetPostIndexByLastVisit(List<StardewConnectPost> posts, StardewConnectVisitSnapshot lastVisit)
        {
            string lastVisitSeason = lastVisit?.Season ?? "spring";
            int lastVisitDay = lastVisit?.Day ?? 1;
            int lastVisitYear = lastVisit?.Year ?? 1;
            int lastVisitTimeOfDay = lastVisit?.TimeOfDay ?? 600;
            long lastVisitTotalGameTime = Math.Max(0L, lastVisit?.TotalGameTime ?? 0L);

            for (int i = 0; i < posts.Count; i++)
            {
                StardewConnectPost post = posts[i];
                if (post == null)
                    continue;

                int comparison = CompareSocialChronologicalPosition(
                    post.Season,
                    post.Day,
                    post.Year,
                    post.TimeOfDay,
                    post.TotalGameTime,
                    lastVisitSeason,
                    lastVisitDay,
                    lastVisitYear,
                    lastVisitTimeOfDay,
                    lastVisitTotalGameTime);

                if (comparison >= 0)
                    return i;
            }

            return -1;
        }

        private void SnapSocialFeedToPostIndex(List<StardewConnectPost> posts, int postIndex)
        {
            if (posts == null || posts.Count == 0)
            {
                socialFeedScrollOffset = 0f;
                socialFeedScrollTarget = 0f;
                return;
            }

            int safeIndex = Math.Clamp(postIndex, 0, posts.Count - 1);

            float targetOffset = SocialPostTopPadding;
            for (int i = 0; i < safeIndex; i++)
                targetOffset += MeasureSocialPostCardHeight(posts[i], includeAllComments: false, selectedAttachmentIndex: GetFeedPostImageIndex(posts[i])) + SocialPostSpacing;

            float maxScroll = CalculateSocialFeedMaxScroll(posts);
            targetOffset = Math.Clamp(targetOffset, 0f, maxScroll);
            socialFeedScrollOffset = targetOffset;
            socialFeedScrollTarget = targetOffset;
        }

        private void SnapSocialFeedToBottom()
        {
            List<StardewConnectPost> posts = StardewConnectManager.GetPostsSnapshot();
            float maxScroll = CalculateSocialFeedMaxScroll(posts);
            socialFeedScrollOffset = maxScroll;
            socialFeedScrollTarget = maxScroll;
        }

        private List<StardewConnectPost> GetSelectedProfilePosts()
        {
            string actorName = ResolveSocialProfileActorName(selectedSocialProfileActorName, selectedSocialProfileActorIsPlayer);
            if (string.IsNullOrWhiteSpace(actorName))
                return new List<StardewConnectPost>();

            return StardewConnectManager.GetPostsByAuthor(actorName, selectedSocialProfileActorIsPlayer);
        }

        private string ResolveSocialProfileActorName(string actorName, bool actorIsPlayer)
        {
            string resolved = (actorName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolved) && actorIsPlayer)
                resolved = Game1.player?.Name ?? "Player";

            return resolved;
        }

        private string ResolveSocialActorDisplayName(string actorName, bool actorIsPlayer)
        {
            string resolvedActorName = ResolveSocialProfileActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedActorName))
                return actorIsPlayer ? (Game1.player?.Name ?? "Player") : "";

            if (actorIsPlayer)
                return resolvedActorName;

            return GetNpcDisplayName(resolvedActorName);
        }

        private string ResolveSocialActorDisplayNameGuess(string actorName)
        {
            string resolved = (actorName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolved))
                return "";

            string playerName = (Game1.player?.Name ?? "Player").Trim();
            if (!string.IsNullOrWhiteSpace(playerName)
                && string.Equals(resolved, playerName, StringComparison.OrdinalIgnoreCase))
            {
                return playerName;
            }

            return GetNpcDisplayName(resolved);
        }

        private string GetSocialProfileAgeLabel(string actorName, bool actorIsPlayer)
        {
            if (actorIsPlayer)
                return "Adult";

            string resolvedName = ResolveSocialProfileActorName(actorName, actorIsPlayer);
            NPC? npc = Game1.getCharacterFromName(resolvedName);
            if (npc == null)
                return "Unknown";

            return npc.Age == 0 ? "Adult" : npc.Age == 1 ? "Teens" : npc.Age == 2 ? "Child" : "Adult";
        }


        private string GetSocialProfileBirthdayLabel(string actorName, bool actorIsPlayer)
        {
            if (actorIsPlayer)
                return "Unknown";

            string resolvedName = ResolveSocialProfileActorName(actorName, actorIsPlayer);
            if (string.IsNullOrWhiteSpace(resolvedName))
                return "Unknown";

            NPC? npc = Game1.getCharacterFromName(resolvedName, mustBeVillager: false);
            if (npc == null || npc.Birthday_Day <= 0 || string.IsNullOrWhiteSpace(npc.Birthday_Season))
                return "Unknown";

            string season = npc.Birthday_Season.Trim();
            string seasonLabel = char.ToUpperInvariant(season[0]) + season.Substring(1).ToLowerInvariant();
            return $"{seasonLabel} {npc.Birthday_Day}";
        }

        private float CalculateSocialProfileMaxScroll(List<StardewConnectPost> posts)
        {
            int contentHeight = CalculateSocialProfileContentHeight(posts);
            return Math.Max(0f, contentHeight - SocialViewportHeight);
        }

        private int CalculateSocialProfileContentHeight(List<StardewConnectPost> posts)
        {
            int contentHeight = SocialPostTopPadding + SocialPostBottomPadding;
            contentHeight += (SocialProfileAvatarHeight + 30) + SocialProfileSectionSpacing;
            contentHeight += SocialProfileStatsHeight + SocialProfileSectionSpacing;
            contentHeight += SocialProfileInteractionHeight + SocialProfileSectionSpacing;
            contentHeight += SocialProfilePostsHeaderHeight + SocialPostSpacing;

            if (posts.Count == 0)
                return contentHeight + 30;

            for (int i = 0; i < posts.Count; i++)
            {
                int selectedAttachmentIndex = GetFeedPostImageIndex(posts[i]);
                contentHeight += MeasureSocialPostCardHeight(posts[i], includeAllComments: false, selectedAttachmentIndex: selectedAttachmentIndex);
                if (i < posts.Count - 1)
                    contentHeight += SocialPostSpacing;
            }

            return contentHeight;
        }

        private void ClampSocialProfileScroll(List<StardewConnectPost> posts)
        {
            float maxScroll = CalculateSocialProfileMaxScroll(posts);
            socialProfileScrollTarget = Math.Clamp(socialProfileScrollTarget, 0f, maxScroll);
            socialProfileScrollOffset = Math.Clamp(socialProfileScrollOffset, 0f, maxScroll);
        }

        private void ClampSocialProfileScroll()
        {
            ClampSocialProfileScroll(GetSelectedProfilePosts());
        }

        private float CalculateSocialFeedMaxScroll(List<StardewConnectPost> posts)
        {
            if (posts.Count == 0)
                return 0f;

            int contentHeight = SocialPostTopPadding + SocialPostBottomPadding;
            for (int i = 0; i < posts.Count; i++)
            {
                int selectedAttachmentIndex = GetFeedPostImageIndex(posts[i]);
                contentHeight += MeasureSocialPostCardHeight(posts[i], includeAllComments: false, selectedAttachmentIndex: selectedAttachmentIndex);
                if (i < posts.Count - 1)
                    contentHeight += SocialPostSpacing;
            }

            return Math.Max(0f, contentHeight - SocialViewportHeight);
        }

        private int MeasureSocialNotificationCardHeight(SocialNotificationEntry entry)
        {
            string cacheKey = $"{entry?.Key}|{entry?.Message}";
            if (socialNotificationCardHeightCache.TryGetValue(cacheKey, out int cachedHeight))
                return cachedHeight;

            List<string> lines = SplitTextIntoLines(
                entry?.Message ?? "",
                Game1.smallFont,
                SocialNotificationCardWidth - (SocialNotificationCardPadding * 2));

            if (lines.Count == 0)
                lines.Add("");

            int lineHeight = (int)Game1.smallFont.MeasureString("A").Y + 4;
            int measuredHeight = (lines.Count * lineHeight) + (SocialNotificationCardVerticalPadding * 2);

            if (socialNotificationCardHeightCache.Count >= SocialNotificationHeightCacheLimit)
                socialNotificationCardHeightCache.Clear();

            socialNotificationCardHeightCache[cacheKey] = measuredHeight;
            return measuredHeight;
        }

        private float CalculateSocialNotificationMaxScroll(List<SocialNotificationEntry> notifications)
        {
            if (notifications == null || notifications.Count == 0)
                return 0f;

            int contentHeight = SocialPostTopPadding + SocialPostBottomPadding;
            for (int i = 0; i < notifications.Count; i++)
            {
                contentHeight += MeasureSocialNotificationCardHeight(notifications[i]);
                if (i < notifications.Count - 1)
                    contentHeight += SocialNotificationCardSpacing;
            }

            return Math.Max(0f, contentHeight - SocialViewportHeight);
        }

        private void ClampSocialNotificationScroll(List<SocialNotificationEntry> notifications)
        {
            float maxScroll = CalculateSocialNotificationMaxScroll(notifications);
            socialNotificationScrollTarget = Math.Clamp(socialNotificationScrollTarget, 0f, maxScroll);
            socialNotificationScrollOffset = Math.Clamp(socialNotificationScrollOffset, 0f, maxScroll);
        }

        private void ClampSocialNotificationScroll()
        {
            ClampSocialNotificationScroll(GetActiveSocialNotifications());
        }

        private float CalculateSocialDetailMaxScroll(StardewConnectPost post)
        {
            int contentHeight = SocialPostTopPadding
                + MeasureSocialPostCardHeight(post, includeAllComments: true, selectedAttachmentIndex: socialDetailImageIndex)
                + SocialPostBottomPadding;

            return Math.Max(0f, contentHeight - GetSocialDetailViewportRect().Height);
        }

        private void ClampSocialFeedScroll(List<StardewConnectPost> posts)
        {
            float maxScroll = CalculateSocialFeedMaxScroll(posts);
            socialFeedScrollTarget = Math.Clamp(socialFeedScrollTarget, 0f, maxScroll);
            socialFeedScrollOffset = Math.Clamp(socialFeedScrollOffset, 0f, maxScroll);
        }

        private void ClampSocialDetailScroll(StardewConnectPost post)
        {
            float maxScroll = CalculateSocialDetailMaxScroll(post);
            socialDetailScrollTarget = Math.Clamp(socialDetailScrollTarget, 0f, maxScroll);
            socialDetailScrollOffset = Math.Clamp(socialDetailScrollOffset, 0f, maxScroll);
        }

        private string GetTailTextToFit(string text, SpriteFont font, int maxWidth)
        {
            string visible = text ?? "";
            while (font.MeasureString(visible).X > maxWidth && visible.Length > 1)
                visible = visible.Substring(1);

            return visible;
        }

        private bool HandleSocialPostInputKey(Keys key, bool isRepeat)
        {
            bool handled = TryApplyEditableTextKeyToField(
                EditableTextFieldKind.SocialPost,
                key,
                allowEnter: true,
                allowPaste: !isRepeat,
                out bool textChanged,
                out bool submitted);

            if (!handled)
                return false;

            if (textChanged && ShouldPlayTypingSound(key, allowPaste: !isRepeat))
                Game1.playSound("coin");

            if (submitted)
                TryCreatePlayerSocialPost();

            if (!isRepeat)
            {
                if (IsRepeatableTextInputKey(key))
                    BeginTextInputRepeat(EditableTextFieldKind.SocialPost, key);
                else
                    ResetTextInputRepeatState();
            }

            return true;
        }

        private bool HandleSocialCommentInputKey(Keys key, bool isRepeat)
        {
            bool handled = TryApplyEditableTextKeyToField(
                EditableTextFieldKind.SocialComment,
                key,
                allowEnter: true,
                allowPaste: !isRepeat,
                out bool textChanged,
                out bool submitted);

            if (!handled)
                return false;

            if (textChanged && ShouldPlayTypingSound(key, allowPaste: !isRepeat))
                Game1.playSound("coin");

            if (submitted)
                TryCreatePlayerSocialComment();

            if (!isRepeat)
            {
                if (IsRepeatableTextInputKey(key))
                    BeginTextInputRepeat(EditableTextFieldKind.SocialComment, key);
                else
                    ResetTextInputRepeatState();
            }

            return true;
        }

        private (string VisibleText, int VisibleStartIndex, int CursorOffset) GetVisibleTextForInput(string text, SpriteFont font, int maxWidth, int cursorIndex)
        {
            string safeText = text ?? "";
            cursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);

            if (safeText.Length == 0 || font.MeasureString(safeText).X <= maxWidth)
                return (safeText, 0, (int)font.MeasureString(safeText[..cursorIndex]).X);

            int startIndex = GetVisibleWindowStart(safeText, font, maxWidth, cursorIndex);
            int endIndex = GetVisibleWindowEnd(safeText, font, maxWidth, startIndex, cursorIndex);

            string visibleText = safeText.Substring(startIndex, endIndex - startIndex);
            int cursorOffset = MeasureTextSubstringWidth(font, safeText, startIndex, cursorIndex - startIndex);
            return (visibleText, startIndex, cursorOffset);
        }

        private void DrawEditableTextInput(SpriteBatch b, Rectangle inputBounds, string text, int cursorIndex, int selectionAnchorIndex)
        {
            SpriteFont font = Game1.smallFont;
            int maxWidth = inputBounds.Width - 30;
            string safeText = text ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);

            (string visibleText, int visibleStartIndex, int cursorOffset) = GetVisibleTextForInput(safeText, font, maxWidth, safeCursorIndex);
            (int selectionStart, int selectionEnd) = GetSelectionRange(safeCursorIndex, safeSelectionAnchorIndex, safeText.Length);
            bool hasSelection = selectionStart != selectionEnd;

            if (hasSelection)
            {
                int visibleSelectionStart = Math.Clamp(selectionStart, visibleStartIndex, visibleStartIndex + visibleText.Length);
                int visibleSelectionEnd = Math.Clamp(selectionEnd, visibleStartIndex, visibleStartIndex + visibleText.Length);
                if (visibleSelectionEnd > visibleSelectionStart)
                {
                    int highlightX = inputBounds.X + 15 + MeasureTextSubstringWidth(font, safeText, visibleStartIndex, visibleSelectionStart - visibleStartIndex);
                    int highlightWidth = MeasureTextSubstringWidth(font, safeText, visibleSelectionStart, visibleSelectionEnd - visibleSelectionStart);
                    int highlightHeight = (int)font.MeasureString("A").Y + 2;
                    b.Draw(Game1.staminaRect, new Rectangle(highlightX, inputBounds.Y + 18, Math.Max(2, highlightWidth), highlightHeight), new Color(80, 140, 255, 140));
                }
            }

            Vector2 textPosition = new Vector2(inputBounds.X + 15, inputBounds.Y + 20);
            b.DrawString(font, visibleText, textPosition, Color.Black);

            bool showCursor = ((int)(textCursorBlinkElapsedSeconds / 0.5d) % 2) == 0;
            if (!showCursor)
                return;

            int cursorHeight = (int)font.MeasureString("A").Y + 2;
            int cursorX = inputBounds.X + 15 + cursorOffset;
            int cursorY = inputBounds.Y + 18;
            b.Draw(Game1.staminaRect, new Rectangle(cursorX, cursorY, 2, cursorHeight), Color.Black);
        }

        private static string FormatSocialPostDateTime(string season, int day, int timeOfDay)
        {
            string seasonName = string.IsNullOrWhiteSpace(season)
                ? "Spring"
                : char.ToUpperInvariant(season[0]) + season.Substring(1).ToLowerInvariant();

            return $"{FormatTimeOfDay(timeOfDay)}, {seasonName} {Math.Max(1, day)}";
        }

        private static string FormatTimeOfDay(int timeOfDay)
        {
            int hour24 = Math.Max(0, timeOfDay / 100) % 24;
            int minute = Math.Clamp(timeOfDay % 100, 0, 59);

            string period = hour24 >= 12 ? "PM" : "AM";
            int hour12 = hour24 % 12;
            if (hour12 == 0)
                hour12 = 12;

            return $"{hour12}:{minute:00}{period}";
        }
    }
}
