using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Smartphone
{
    public partial class ModEntry
    {
        private const string SocialSyncProtocolVersion = "1";

        private const string SocialMessageFullSyncRequest = "social-full-sync-request";
        private const string SocialMessageFullSyncSnapshot = "social-full-sync-snapshot";
        private const string SocialMessagePhotoUpsert = "social-photo-upsert";

        private const string SocialMessageCreatePostRequest = "social-create-post-request";
        private const string SocialMessageAddCommentRequest = "social-add-comment-request";
        private const string SocialMessageSetLikeRequest = "social-set-like-request";
        private const string SocialMessageUpdateAvatarRequest = "social-update-avatar-request";

        private const string SocialMessagePostDelta = "social-post-delta";
        private const string SocialMessageCommentDelta = "social-comment-delta";
        private const string SocialMessageLikeDelta = "social-like-delta";
        private const string SocialMessageAvatarDelta = "social-avatar-delta";

        private static string PendingSocialSyncRequestId = string.Empty;

        private sealed class SocialUploadedImageMessage
        {
            public string SourceFileName { get; set; } = string.Empty;
            public string Base64Image { get; set; } = string.Empty;
            public string ImageTag { get; set; } = string.Empty;
        }

        private sealed class SocialPhotoUpsertMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string FileName { get; set; } = string.Empty;
            public string Base64Image { get; set; } = string.Empty;
            public string ImageTag { get; set; } = string.Empty;
        }

        private sealed class SocialFullSyncRequestMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string RequestId { get; set; } = string.Empty;
            public string RequestingPlayerName { get; set; } = string.Empty;
            public List<string> ExistingNpcPhotoNames { get; set; } = new();
            public List<string> ExistingPlayerAvatarNames { get; set; } = new();
        }

        private sealed class SocialFullSyncSnapshotMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string RequestId { get; set; } = string.Empty;
            public List<StardewConnectPost> Posts { get; set; } = new();
            public Dictionary<string, StardewConnectProfileStats> ProfileStats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> SharedPlayerAvatars { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> NpcImageTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public List<string> DeleteNpcPhotoNames { get; set; } = new();
            public List<string> DeletePlayerAvatarNames { get; set; } = new();
        }

        private sealed class SocialCreatePostRequestMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string DesiredPostId { get; set; } = string.Empty;
            public string AuthorName { get; set; } = string.Empty;
            public string PostText { get; set; } = string.Empty;
            public List<SocialUploadedImageMessage> UploadedImages { get; set; } = new();
        }

        private sealed class SocialAddCommentRequestMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string DesiredCommentId { get; set; } = string.Empty;
            public string PostId { get; set; } = string.Empty;
            public string AuthorName { get; set; } = string.Empty;
            public string CommentText { get; set; } = string.Empty;
        }

        private sealed class SocialSetLikeRequestMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string PostId { get; set; } = string.Empty;
            public string ActorName { get; set; } = string.Empty;
            public bool Liked { get; set; }
        }

        private sealed class SocialPostDeltaMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public StardewConnectPost Post { get; set; } = new();
            public Dictionary<string, StardewConnectProfileStats> ProfileStats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public List<SocialPhotoUpsertMessage> PhotoPayloads { get; set; } = new();
        }

        private sealed class SocialCommentDeltaMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string PostId { get; set; } = string.Empty;
            public StardewConnectComment Comment { get; set; } = new();
            public int PlayerReadCommentCount { get; set; }
            public Dictionary<string, StardewConnectProfileStats> ProfileStats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class SocialLikeDeltaMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string PostId { get; set; } = string.Empty;
            public string ActorName { get; set; } = string.Empty;
            public bool Liked { get; set; }
            public Dictionary<string, StardewConnectProfileStats> ProfileStats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class SocialUpdateAvatarRequestMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string PlayerName { get; set; } = string.Empty;
            public bool ClearAvatar { get; set; }
            public SocialUploadedImageMessage AvatarImage { get; set; } = new();
        }

        private sealed class SocialAvatarDeltaMessage
        {
            public string ProtocolVersion { get; set; } = SocialSyncProtocolVersion;
            public string PlayerName { get; set; } = string.Empty;
            public bool ClearAvatar { get; set; }
            public SocialPhotoUpsertMessage AvatarImage { get; set; } = new();
        }

        internal static bool IsFarmhandSocialPeer()
        {
            return Context.IsWorldReady && Context.IsMultiplayer && !Context.IsMainPlayer;
        }

        internal static bool ShouldRoutePlayerSocialActionToHost()
        {
            return IsFarmhandSocialPeer();
        }

        internal static bool ShouldBroadcastAuthoritativeSocialChanges()
        {
            return Context.IsWorldReady && Context.IsMultiplayer && Context.IsMainPlayer;
        }

        internal static bool ShouldHostRunSocialSimulation()
        {
            return !Context.IsMultiplayer || Context.IsMainPlayer;
        }

        internal static string GetNpcPhotoAbsolutePath(string imageFileName)
        {
            string normalizedFileName = Path.GetFileName(imageFileName ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return string.Empty;

            return Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                "npc_photo",
                normalizedFileName);
        }

        internal static string GetPlayerAvatarAbsolutePath(string imageFileName)
        {
            string normalizedFileName = Path.GetFileName(imageFileName ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return string.Empty;

            return Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                "player_avatar",
                normalizedFileName);
        }

        private static string GetPlayerPhotoAbsolutePath(string imageFileName)
        {
            string normalizedFileName = Path.GetFileName(imageFileName ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return string.Empty;

            return Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                "player_photo",
                normalizedFileName);
        }

        private static string GetNpcPhotoDirectoryAbsolutePath()
        {
            return Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                "npc_photo");
        }

        private static string GetPlayerAvatarDirectoryAbsolutePath()
        {
            return Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                "player_avatar");
        }

        private static string BuildSharedNpcPhotoFileName(string prefix, string extension = ".png")
        {
            string safePrefix = SanitizeFileNameSegment(prefix);
            if (string.IsNullOrWhiteSpace(safePrefix))
                safePrefix = "shared";

            string safeExtension = string.IsNullOrWhiteSpace(extension) ? ".png" : extension;
            if (!safeExtension.StartsWith('.'))
                safeExtension = "." + safeExtension;

            return $"{safePrefix}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Game1.random.Next(0, 99999):D5}{safeExtension}";
        }

        private static string BuildPlayerAvatarFileName(string playerName)
        {
            string safePlayerName = SanitizeFileNameSegment(playerName);
            if (string.IsNullOrWhiteSpace(safePlayerName))
                safePlayerName = "player";

            return safePlayerName + "_avatar.png";
        }

        private static string SanitizeFileNameSegment(string input)
        {
            string value = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static IEnumerable<string> SplitImageTagText(string imageTag)
        {
            if (string.IsNullOrWhiteSpace(imageTag))
                return Enumerable.Empty<string>();

            return imageTag
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => (part ?? string.Empty).Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<StardewConnectPostAttachment> PrepareAuthoritativePlayerPostAttachments(IEnumerable<string>? sourceFiles, string authorName)
        {
            List<string> normalizedFiles = (sourceFiles ?? Enumerable.Empty<string>())
                .Select(file => Path.GetFileName(file ?? string.Empty) ?? string.Empty)
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var attachments = new List<StardewConnectPostAttachment>();

            foreach (string fileName in normalizedFiles)
            {
                if (Context.IsMultiplayer && Context.IsMainPlayer)
                {
                    string sourcePath = GetPlayerPhotoAbsolutePath(fileName);
                    string imageTag = ImageTags != null && ImageTags.TryGetValue(fileName, out string? loadedTag)
                        ? loadedTag ?? string.Empty
                        : string.Empty;

                    string sharedFileName = CopyImageToNpcPhoto(sourcePath, authorName, fileName, imageTag);
                    if (string.IsNullOrWhiteSpace(sharedFileName))
                        continue;

                    attachments.Add(new StardewConnectPostAttachment
                    {
                        ImageFile = sharedFileName,
                        FromPlayerFolder = false,
                        ImageTag = imageTag
                    });
                }
                else
                {
                    string imageTag = ImageTags != null && ImageTags.TryGetValue(fileName, out string? loadedTag)
                        ? loadedTag ?? string.Empty
                        : string.Empty;

                    attachments.Add(new StardewConnectPostAttachment
                    {
                        ImageFile = fileName,
                        FromPlayerFolder = true,
                        ImageTag = imageTag
                    });
                }
            }

            return attachments;
        }

        private static string CopyImageToNpcPhoto(string sourceAbsolutePath, string actorName, string sourceFileName, string imageTag)
        {
            if (string.IsNullOrWhiteSpace(sourceAbsolutePath) || !File.Exists(sourceAbsolutePath))
                return string.Empty;

            string npcDirectory = GetNpcPhotoDirectoryAbsolutePath();
            Directory.CreateDirectory(npcDirectory);

            string extension = Path.GetExtension(sourceAbsolutePath);
            string copiedFileName = BuildSharedNpcPhotoFileName(actorName, extension);
            string destinationPath = Path.Combine(npcDirectory, copiedFileName);

            File.Copy(sourceAbsolutePath, destinationPath, overwrite: true);

            IEnumerable<string> tags = SplitImageTagText(imageTag);
            if (tags.Any())
                SetImageTags(copiedFileName, tags);
            else
                RemoveImageTags(copiedFileName);

            return copiedFileName;
        }

        internal static bool TryRequestHostCreatePlayerPost(string authorName, string postText, IEnumerable<string>? sourceFiles, string desiredPostId)
        {
            if (!ShouldRoutePlayerSocialActionToHost())
                return false;

            string text = (postText ?? string.Empty).Trim();
            List<SocialUploadedImageMessage> uploads = BuildPlayerPhotoUploads(sourceFiles);
            if (string.IsNullOrWhiteSpace(text) && uploads.Count == 0)
                return false;

            var payload = new SocialCreatePostRequestMessage
            {
                DesiredPostId = (desiredPostId ?? string.Empty).Trim(),
                AuthorName = (authorName ?? string.Empty).Trim(),
                PostText = text,
                UploadedImages = uploads
            };

            return SendMessageToHost(payload, SocialMessageCreatePostRequest);
        }

        internal static bool TryRequestHostAddPlayerComment(string postId, string authorName, string commentText, string desiredCommentId)
        {
            if (!ShouldRoutePlayerSocialActionToHost())
                return false;

            string text = (commentText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(text))
                return false;

            var payload = new SocialAddCommentRequestMessage
            {
                DesiredCommentId = (desiredCommentId ?? string.Empty).Trim(),
                PostId = postId.Trim(),
                AuthorName = (authorName ?? string.Empty).Trim(),
                CommentText = text
            };

            return SendMessageToHost(payload, SocialMessageAddCommentRequest);
        }

        internal static bool TryRequestHostSetPlayerLike(string postId, string actorName, bool liked)
        {
            if (!ShouldRoutePlayerSocialActionToHost())
                return false;

            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(actorName))
                return false;

            var payload = new SocialSetLikeRequestMessage
            {
                PostId = postId.Trim(),
                ActorName = actorName.Trim(),
                Liked = liked
            };

            return SendMessageToHost(payload, SocialMessageSetLikeRequest);
        }

        internal static void BroadcastSyncedSocialPost(StardewConnectPost post, Dictionary<string, StardewConnectProfileStats> profileStats)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges() || post == null)
                return;

            var payload = new SocialPostDeltaMessage
            {
                Post = post,
                ProfileStats = profileStats ?? new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase),
                PhotoPayloads = BuildPostPhotoPayloads(post)
            };

            BroadcastToFarmhands(payload, SocialMessagePostDelta);
        }

        internal static void BroadcastSyncedSocialComment(
            string postId,
            StardewConnectComment comment,
            int playerReadCommentCount,
            Dictionary<string, StardewConnectProfileStats> profileStats)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges() || comment == null || string.IsNullOrWhiteSpace(postId))
                return;

            var payload = new SocialCommentDeltaMessage
            {
                PostId = postId,
                Comment = comment,
                PlayerReadCommentCount = playerReadCommentCount,
                ProfileStats = profileStats ?? new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase)
            };

            BroadcastToFarmhands(payload, SocialMessageCommentDelta);
        }

        internal static void BroadcastSyncedSocialLike(
            string postId,
            string actorName,
            bool liked,
            Dictionary<string, StardewConnectProfileStats> profileStats)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges() || string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(actorName))
                return;

            var payload = new SocialLikeDeltaMessage
            {
                PostId = postId,
                ActorName = actorName,
                Liked = liked,
                ProfileStats = profileStats ?? new Dictionary<string, StardewConnectProfileStats>(StringComparer.OrdinalIgnoreCase)
            };

            BroadcastToFarmhands(payload, SocialMessageLikeDelta);
        }

        private static List<SocialPhotoUpsertMessage> BuildPostPhotoPayloads(StardewConnectPost post)
        {
            var payloads = new List<SocialPhotoUpsertMessage>();
            if (post?.Attachments == null)
                return payloads;

            HashSet<string> sent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (StardewConnectPostAttachment attachment in post.Attachments)
            {
                if (attachment == null || attachment.FromPlayerFolder)
                    continue;

                string fileName = Path.GetFileName(attachment.ImageFile ?? string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(fileName) || !sent.Add(fileName))
                    continue;

                if (TryCreateNpcPhotoUpsertPayload(fileName, out SocialPhotoUpsertMessage? payload))
                    payloads.Add(payload);
            }

            return payloads;
        }

        private static bool TryCreateNpcPhotoUpsertPayload(string fileName, out SocialPhotoUpsertMessage? payload)
        {
            payload = null;
            string normalizedFileName = Path.GetFileName(fileName ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return false;

            string absolutePath = GetNpcPhotoAbsolutePath(normalizedFileName);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return false;

            try
            {
                byte[] bytes = File.ReadAllBytes(absolutePath);
                string tag = ImageTags != null && ImageTags.TryGetValue(normalizedFileName, out string? loadedTag)
                    ? loadedTag ?? string.Empty
                    : string.Empty;

                payload = new SocialPhotoUpsertMessage
                {
                    FileName = normalizedFileName,
                    Base64Image = Convert.ToBase64String(bytes),
                    ImageTag = tag
                };

                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to serialize social photo '{normalizedFileName}' for sync: {ex}", LogLevel.Trace);
                return false;
            }
        }

        private static List<SocialUploadedImageMessage> BuildPlayerPhotoUploads(IEnumerable<string>? sourceFiles)
        {
            var uploads = new List<SocialUploadedImageMessage>();
            List<string> normalizedFileNames = (sourceFiles ?? Enumerable.Empty<string>())
                .Select(file => Path.GetFileName(file ?? string.Empty) ?? string.Empty)
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string fileName in normalizedFileNames)
            {
                string absolutePath = GetPlayerPhotoAbsolutePath(fileName);
                if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                    continue;

                try
                {
                    byte[] bytes = File.ReadAllBytes(absolutePath);
                    string imageTag = ImageTags != null && ImageTags.TryGetValue(fileName, out string? loadedTag)
                        ? loadedTag ?? string.Empty
                        : string.Empty;

                    uploads.Add(new SocialUploadedImageMessage
                    {
                        SourceFileName = fileName,
                        Base64Image = Convert.ToBase64String(bytes),
                        ImageTag = imageTag
                    });
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Unable to read player photo '{fileName}' for social sync: {ex}", LogLevel.Trace);
                }
            }

            return uploads;
        }

        private static string SaveUploadedNpcPhoto(SocialUploadedImageMessage uploadedImage, string actorName, string fallbackPrefix = "shared")
        {
            if (uploadedImage == null || string.IsNullOrWhiteSpace(uploadedImage.Base64Image))
                return string.Empty;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(uploadedImage.Base64Image);
            }
            catch (Exception)
            {
                return string.Empty;
            }

            string sourceFileName = Path.GetFileName(uploadedImage.SourceFileName ?? string.Empty) ?? string.Empty;
            string extension = Path.GetExtension(sourceFileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".png";

            string prefix = string.IsNullOrWhiteSpace(actorName) ? fallbackPrefix : actorName;
            string targetFileName = BuildSharedNpcPhotoFileName(prefix, extension);
            string targetPath = GetNpcPhotoAbsolutePath(targetFileName);
            if (string.IsNullOrWhiteSpace(targetPath))
                return string.Empty;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetNpcPhotoDirectoryAbsolutePath());
                File.WriteAllBytes(targetPath, bytes);

                IEnumerable<string> tags = SplitImageTagText(uploadedImage.ImageTag);
                if (tags.Any())
                    SetImageTags(targetFileName, tags);
                else
                    RemoveImageTags(targetFileName);

                return targetFileName;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to write synced social photo '{targetFileName}': {ex}", LogLevel.Trace);
                return string.Empty;
            }
        }

        private static string SaveUploadedPlayerAvatar(SocialUploadedImageMessage uploadedImage, string playerName)
        {
            if (uploadedImage == null || string.IsNullOrWhiteSpace(uploadedImage.Base64Image))
                return string.Empty;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(uploadedImage.Base64Image);
            }
            catch (Exception)
            {
                return string.Empty;
            }

            string avatarFileName = BuildPlayerAvatarFileName(playerName);
            string targetPath = GetPlayerAvatarAbsolutePath(avatarFileName);
            if (string.IsNullOrWhiteSpace(targetPath))
                return string.Empty;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetPlayerAvatarDirectoryAbsolutePath());
                File.WriteAllBytes(targetPath, bytes);
                return avatarFileName;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to write synced player avatar '{avatarFileName}': {ex}", LogLevel.Trace);
                return string.Empty;
            }
        }

        private static bool TryApplyNpcPhotoUpsert(SocialPhotoUpsertMessage payload)
        {
            if (payload == null)
                return false;

            string normalizedFileName = Path.GetFileName(payload.FileName ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedFileName) || string.IsNullOrWhiteSpace(payload.Base64Image))
                return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(payload.Base64Image);
            }
            catch (Exception)
            {
                return false;
            }

            string targetPath = GetNpcPhotoAbsolutePath(normalizedFileName);
            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetNpcPhotoDirectoryAbsolutePath());
                File.WriteAllBytes(targetPath, bytes);

                IEnumerable<string> tags = SplitImageTagText(payload.ImageTag);
                if (tags.Any())
                    SetImageTags(normalizedFileName, tags);
                else
                    RemoveImageTags(normalizedFileName);

                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to apply synced photo '{normalizedFileName}': {ex}", LogLevel.Trace);
                return false;
            }
        }

        private static bool TryApplyPlayerAvatarUpsert(SocialPhotoUpsertMessage payload)
        {
            if (payload == null)
                return false;

            string normalizedFileName = Path.GetFileName(payload.FileName ?? string.Empty) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedFileName) || string.IsNullOrWhiteSpace(payload.Base64Image))
                return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(payload.Base64Image);
            }
            catch (Exception)
            {
                return false;
            }

            string targetPath = GetPlayerAvatarAbsolutePath(normalizedFileName);
            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetPlayerAvatarDirectoryAbsolutePath());
                File.WriteAllBytes(targetPath, bytes);
                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to apply synced player avatar '{normalizedFileName}': {ex}", LogLevel.Trace);
                return false;
            }
        }

        private static bool TryCreatePlayerAvatarUpsertPayload(string playerName, out SocialPhotoUpsertMessage? payload)
        {
            payload = null;
            string avatarFileName = BuildPlayerAvatarFileName(playerName);
            string absolutePath = GetPlayerAvatarAbsolutePath(avatarFileName);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
                return false;

            try
            {
                payload = new SocialPhotoUpsertMessage
                {
                    FileName = avatarFileName,
                    Base64Image = Convert.ToBase64String(File.ReadAllBytes(absolutePath)),
                    ImageTag = string.Empty
                };

                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to serialize player avatar '{avatarFileName}' for sync: {ex}", LogLevel.Trace);
                return false;
            }
        }

        private static Dictionary<string, string> BuildNpcImageTagSnapshot()
        {
            HashSet<string> npcPhotos = GetNpcPhotoFileNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach ((string fileName, string imageTag) in ImageTags)
            {
                if (!npcPhotos.Contains(fileName))
                    continue;

                snapshot[fileName] = imageTag ?? string.Empty;
            }

            return snapshot;
        }

        private static void ApplyNpcImageTagSnapshot(Dictionary<string, string>? snapshot)
        {
            HashSet<string> npcPhotos = GetNpcPhotoFileNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool changed = false;

            foreach (string existingTagKey in ImageTags.Keys.ToList())
            {
                if (!npcPhotos.Contains(existingTagKey))
                    continue;

                ImageTags.Remove(existingTagKey);
                changed = true;
            }

            if (snapshot != null)
            {
                foreach ((string fileName, string imageTag) in snapshot)
                {
                    if (!npcPhotos.Contains(fileName))
                        continue;

                    ImageTags[fileName] = imageTag ?? string.Empty;
                    changed = true;
                }
            }

            if (changed)
                SaveImageTags();
        }

        private static List<string> GetNpcPhotoFileNames()
        {
            string npcDirectory = GetNpcPhotoDirectoryAbsolutePath();
            if (!Directory.Exists(npcDirectory))
                return new List<string>();

            return Directory.GetFiles(npcDirectory, "*.png")
                .Select(path => Path.GetFileName(path) ?? string.Empty)
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetPlayerAvatarFileNames()
        {
            string avatarDirectory = GetPlayerAvatarDirectoryAbsolutePath();
            if (!Directory.Exists(avatarDirectory))
                return new List<string>();

            return Directory.GetFiles(avatarDirectory, "*.png")
                .Select(path => Path.GetFileName(path) ?? string.Empty)
                .Where(file => !string.IsNullOrWhiteSpace(file))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void DeleteNpcPhotos(IEnumerable<string>? fileNames)
        {
            if (fileNames == null)
                return;

            foreach (string fileName in fileNames)
            {
                string normalizedFileName = Path.GetFileName(fileName ?? string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedFileName))
                    continue;

                string path = GetNpcPhotoAbsolutePath(normalizedFileName);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        SMonitor.Log($"Unable to delete stale synced photo '{normalizedFileName}': {ex}", LogLevel.Trace);
                    }
                }

                RemoveImageTags(normalizedFileName);
            }
        }

        private static void DeletePlayerAvatarFiles(IEnumerable<string>? fileNames)
        {
            if (fileNames == null)
                return;

            foreach (string fileName in fileNames)
            {
                string normalizedFileName = Path.GetFileName(fileName ?? string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedFileName))
                    continue;

                string avatarPath = GetPlayerAvatarAbsolutePath(normalizedFileName);
                if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
                    continue;

                try
                {
                    File.Delete(avatarPath);
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Unable to delete stale player avatar '{normalizedFileName}': {ex}", LogLevel.Trace);
                }
            }
        }

        private static void DeletePlayerAvatarForPlayer(string playerName)
        {
            string avatarFileName = BuildPlayerAvatarFileName(playerName);
            DeletePlayerAvatarFiles(new[] { avatarFileName });
        }

        private static bool SendMessageToHost<TPayload>(TPayload payload, string messageType)
        {
            if (SHelper?.Multiplayer == null || Instance?.ModManifest == null || !Context.IsMultiplayer || Game1.MasterPlayer == null)
                return false;

            try
            {
                SHelper.Multiplayer.SendMessage(
                    payload,
                    messageType,
                    modIDs: new[] { Instance.ModManifest.UniqueID },
                    playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to send social message '{messageType}' to host: {ex}", LogLevel.Trace);
                return false;
            }
        }

        private static bool SendMessageToPlayer<TPayload>(TPayload payload, string messageType, long playerId)
        {
            if (SHelper?.Multiplayer == null || Instance?.ModManifest == null)
                return false;

            try
            {
                SHelper.Multiplayer.SendMessage(
                    payload,
                    messageType,
                    modIDs: new[] { Instance.ModManifest.UniqueID },
                    playerIDs: new[] { playerId });
                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to send social message '{messageType}' to player {playerId}: {ex}", LogLevel.Trace);
                return false;
            }
        }

        private static void BroadcastToFarmhands<TPayload>(TPayload payload, string messageType)
        {
            if (SHelper?.Multiplayer == null || Instance?.ModManifest == null)
                return;

            long[] recipients = GetConnectedFarmhandPlayerIds();
            if (recipients.Length == 0)
                return;

            try
            {
                SHelper.Multiplayer.SendMessage(
                    payload,
                    messageType,
                    modIDs: new[] { Instance.ModManifest.UniqueID },
                    playerIDs: recipients);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to broadcast social message '{messageType}' to farmhands: {ex}", LogLevel.Trace);
            }
        }

        private static long[] GetConnectedFarmhandPlayerIds()
        {
            if (!Context.IsMultiplayer || Game1.MasterPlayer == null)
                return Array.Empty<long>();

            return SHelper.Multiplayer
                .GetConnectedPlayers()
                .Where(peer => peer.PlayerID != Game1.MasterPlayer.UniqueMultiplayerID)
                .Select(peer => peer.PlayerID)
                .ToArray();
        }

        private static bool IsMessageFromHost(long playerId)
        {
            return Context.IsMultiplayer
                && Game1.MasterPlayer != null
                && playerId == Game1.MasterPlayer.UniqueMultiplayerID;
        }

        private void InitializeSocialCoopOnSaveLoaded()
        {
            PendingSocialSyncRequestId = string.Empty;

            if (!IsFarmhandSocialPeer())
                return;

            var payload = new SocialFullSyncRequestMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                RequestingPlayerName = Game1.player?.Name ?? "Player",
                ExistingNpcPhotoNames = GetNpcPhotoFileNames(),
                ExistingPlayerAvatarNames = GetPlayerAvatarFileNames()
            };

            PendingSocialSyncRequestId = payload.RequestId;
            SendMessageToHost(payload, SocialMessageFullSyncRequest);
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e == null || e.FromModID != this.ModManifest.UniqueID)
                return;

            switch (e.Type)
            {
                case SocialMessageFullSyncRequest:
                    HandleSocialFullSyncRequest(e);
                    break;
                case SocialMessageFullSyncSnapshot:
                    HandleSocialFullSyncSnapshot(e);
                    break;
                case SocialMessagePhotoUpsert:
                    HandleSocialPhotoUpsert(e);
                    break;
                case SocialMessageCreatePostRequest:
                    HandleSocialCreatePostRequest(e);
                    break;
                case SocialMessageAddCommentRequest:
                    HandleSocialAddCommentRequest(e);
                    break;
                case SocialMessageSetLikeRequest:
                    HandleSocialSetLikeRequest(e);
                    break;
                case SocialMessagePostDelta:
                    HandleSocialPostDelta(e);
                    break;
                case SocialMessageCommentDelta:
                    HandleSocialCommentDelta(e);
                    break;
                case SocialMessageLikeDelta:
                    HandleSocialLikeDelta(e);
                    break;
                case SocialMessageUpdateAvatarRequest:
                    HandleSocialUpdateAvatarRequest(e);
                    break;
                case SocialMessageAvatarDelta:
                    HandleSocialAvatarDelta(e);
                    break;
            }
        }

        private void HandleSocialFullSyncRequest(ModMessageReceivedEventArgs e)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges())
                return;

            SocialFullSyncRequestMessage? request = e.ReadAs<SocialFullSyncRequestMessage>();
            if (request == null)
                return;

            List<string> hostNpcPhotoNames = GetNpcPhotoFileNames();
            HashSet<string> farmhandNpcPhotos = new HashSet<string>(
                request.ExistingNpcPhotoNames ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            Dictionary<string, string> sharedPlayerAvatars = StardewConnectManager.GetSharedPlayerAvatarSnapshot();
            HashSet<string> hostPlayerAvatarNames = sharedPlayerAvatars.Values
                .Select(fileName => Path.GetFileName(fileName ?? string.Empty) ?? string.Empty)
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            HashSet<string> farmhandPlayerAvatarNames = new HashSet<string>(
                request.ExistingPlayerAvatarNames ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            List<string> missingOnFarmhand = hostNpcPhotoNames
                .Where(fileName => !farmhandNpcPhotos.Contains(fileName))
                .ToList();

            List<string> staleOnFarmhand = farmhandNpcPhotos
                .Where(fileName => !hostNpcPhotoNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                .ToList();

            List<string> stalePlayerAvatarsOnFarmhand = farmhandPlayerAvatarNames
                .Where(fileName => !hostPlayerAvatarNames.Contains(fileName))
                .ToList();

            HashSet<string> missingPlayerAvatarsOnFarmhand = hostPlayerAvatarNames
                .Where(fileName => !farmhandPlayerAvatarNames.Contains(fileName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var snapshot = new SocialFullSyncSnapshotMessage
            {
                RequestId = request.RequestId ?? string.Empty,
                Posts = StardewConnectManager.GetPostsSnapshot(),
                ProfileStats = StardewConnectManager.GetProfileStatsSnapshot(),
                SharedPlayerAvatars = sharedPlayerAvatars,
                NpcImageTags = BuildNpcImageTagSnapshot(),
                DeleteNpcPhotoNames = staleOnFarmhand,
                DeletePlayerAvatarNames = stalePlayerAvatarsOnFarmhand
            };

            SendMessageToPlayer(snapshot, SocialMessageFullSyncSnapshot, e.FromPlayerID);

            foreach (string fileName in missingOnFarmhand)
            {
                if (TryCreateNpcPhotoUpsertPayload(fileName, out SocialPhotoUpsertMessage? payload) && payload != null)
                    SendMessageToPlayer(payload, SocialMessagePhotoUpsert, e.FromPlayerID);
            }

            foreach ((string playerName, string avatarFileName) in sharedPlayerAvatars)
            {
                string normalizedAvatarFileName = Path.GetFileName(avatarFileName ?? string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedAvatarFileName)
                    || !missingPlayerAvatarsOnFarmhand.Contains(normalizedAvatarFileName))
                {
                    continue;
                }

                if (!TryCreatePlayerAvatarUpsertPayload(playerName, out SocialPhotoUpsertMessage? avatarPayload)
                    || avatarPayload == null)
                {
                    continue;
                }

                var avatarDelta = new SocialAvatarDeltaMessage
                {
                    PlayerName = playerName,
                    ClearAvatar = false,
                    AvatarImage = avatarPayload
                };

                SendMessageToPlayer(avatarDelta, SocialMessageAvatarDelta, e.FromPlayerID);
            }
        }

        private void HandleSocialFullSyncSnapshot(ModMessageReceivedEventArgs e)
        {
            if (!IsFarmhandSocialPeer() || !IsMessageFromHost(e.FromPlayerID))
                return;

            SocialFullSyncSnapshotMessage? snapshot = e.ReadAs<SocialFullSyncSnapshotMessage>();
            if (snapshot == null)
                return;

            if (!string.IsNullOrWhiteSpace(PendingSocialSyncRequestId)
                && !string.Equals(snapshot.RequestId, PendingSocialSyncRequestId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            StardewConnectManager.ApplySocialStateSnapshot(snapshot.Posts, snapshot.ProfileStats, snapshot.SharedPlayerAvatars);
            ApplyNpcImageTagSnapshot(snapshot.NpcImageTags);
            DeleteNpcPhotos(snapshot.DeleteNpcPhotoNames);
            DeletePlayerAvatarFiles(snapshot.DeletePlayerAvatarNames);
            InvalidateSocialImageLoadCaches();

            PendingSocialSyncRequestId = string.Empty;
        }

        private void HandleSocialPhotoUpsert(ModMessageReceivedEventArgs e)
        {
            if (!IsFarmhandSocialPeer() || !IsMessageFromHost(e.FromPlayerID))
                return;

            SocialPhotoUpsertMessage? payload = e.ReadAs<SocialPhotoUpsertMessage>();
            if (payload == null)
                return;

            if (TryApplyNpcPhotoUpsert(payload))
                InvalidateSocialImageLoadCaches();
        }

        private void HandleSocialCreatePostRequest(ModMessageReceivedEventArgs e)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges())
                return;

            SocialCreatePostRequestMessage? request = e.ReadAs<SocialCreatePostRequestMessage>();
            if (request == null)
                return;

            string desiredPostId = (request.DesiredPostId ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(desiredPostId) && StardewConnectManager.GetPost(desiredPostId) != null)
                return;

            string authorName = (request.AuthorName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(authorName))
                authorName = "Player";

            string postText = (request.PostText ?? string.Empty).Trim();

            var attachments = new List<StardewConnectPostAttachment>();
            foreach (SocialUploadedImageMessage uploadedImage in request.UploadedImages ?? new List<SocialUploadedImageMessage>())
            {
                string savedFileName = SaveUploadedNpcPhoto(uploadedImage, authorName);
                if (string.IsNullOrWhiteSpace(savedFileName))
                    continue;

                attachments.Add(new StardewConnectPostAttachment
                {
                    ImageFile = savedFileName,
                    FromPlayerFolder = false,
                    ImageTag = uploadedImage.ImageTag ?? string.Empty
                });
            }

            StardewConnectManager.AddPost(
                authorName,
                true,
                postText,
                attachedImageFile: string.Empty,
                attachmentFromPlayerFolder: true,
                attachments: attachments,
                forcedPostId: desiredPostId,
                broadcastChange: true);
        }

        private void HandleSocialAddCommentRequest(ModMessageReceivedEventArgs e)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges())
                return;

            SocialAddCommentRequestMessage? request = e.ReadAs<SocialAddCommentRequestMessage>();
            if (request == null)
                return;

            StardewConnectManager.AddComment(
                request.PostId,
                request.AuthorName,
                authorIsPlayer: true,
                request.CommentText,
                forcedCommentId: request.DesiredCommentId,
                broadcastChange: true);
        }

        private void HandleSocialSetLikeRequest(ModMessageReceivedEventArgs e)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges())
                return;

            SocialSetLikeRequestMessage? request = e.ReadAs<SocialSetLikeRequestMessage>();
            if (request == null)
                return;

            StardewConnectManager.SetPostLike(request.PostId, request.ActorName, request.Liked, broadcastChange: true);
        }

        private void HandleSocialPostDelta(ModMessageReceivedEventArgs e)
        {
            if (!IsFarmhandSocialPeer() || !IsMessageFromHost(e.FromPlayerID))
                return;

            SocialPostDeltaMessage? payload = e.ReadAs<SocialPostDeltaMessage>();
            if (payload == null || payload.Post == null)
                return;

            bool wroteFiles = false;
            foreach (SocialPhotoUpsertMessage photoPayload in payload.PhotoPayloads ?? new List<SocialPhotoUpsertMessage>())
            {
                if (TryApplyNpcPhotoUpsert(photoPayload))
                    wroteFiles = true;
            }

            StardewConnectManager.UpsertSyncedPost(payload.Post, payload.ProfileStats);

            if (wroteFiles)
                InvalidateSocialImageLoadCaches();
        }

        private void HandleSocialCommentDelta(ModMessageReceivedEventArgs e)
        {
            if (!IsFarmhandSocialPeer() || !IsMessageFromHost(e.FromPlayerID))
                return;

            SocialCommentDeltaMessage? payload = e.ReadAs<SocialCommentDeltaMessage>();
            if (payload == null || payload.Comment == null)
                return;

            StardewConnectManager.ApplySyncedComment(
                payload.PostId,
                payload.Comment,
                payload.PlayerReadCommentCount,
                payload.ProfileStats);
        }

        private void HandleSocialLikeDelta(ModMessageReceivedEventArgs e)
        {
            if (!IsFarmhandSocialPeer() || !IsMessageFromHost(e.FromPlayerID))
                return;

            SocialLikeDeltaMessage? payload = e.ReadAs<SocialLikeDeltaMessage>();
            if (payload == null)
                return;

            StardewConnectManager.ApplySyncedLike(payload.PostId, payload.ActorName, payload.Liked, payload.ProfileStats);
        }

        public static void PublishLocalPlayerAvatarSelection(string avatarPath)
        {
            if (!Context.IsWorldReady)
                return;

            string playerName = (Game1.player?.Name ?? "Player").Trim();
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player";

            if (!Context.IsMultiplayer)
            {
                if (string.IsNullOrWhiteSpace(avatarPath))
                {
                    DeletePlayerAvatarForPlayer(playerName);
                    StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, string.Empty, saveData: true, notifyPhoneMenu: true);
                }
                else
                {
                    SocialUploadedImageMessage? upload = BuildAvatarUploadFromAbsolutePath(avatarPath);
                    if (upload == null)
                        return;

                    string localAvatarFileName = SaveUploadedPlayerAvatar(upload, playerName);
                    if (string.IsNullOrWhiteSpace(localAvatarFileName))
                        return;

                    StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, localAvatarFileName, saveData: true, notifyPhoneMenu: true);
                }

                return;
            }

            if (Context.IsMainPlayer)
            {
                if (string.IsNullOrWhiteSpace(avatarPath))
                {
                    DeletePlayerAvatarForPlayer(playerName);
                    StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, string.Empty, saveData: true, notifyPhoneMenu: true);
                    BroadcastAvatarDelta(playerName, clearAvatar: true);
                    return;
                }

                SocialUploadedImageMessage? upload = BuildAvatarUploadFromAbsolutePath(avatarPath);
                if (upload == null)
                    return;

                string syncedFileName = SaveUploadedPlayerAvatar(upload, playerName);
                if (string.IsNullOrWhiteSpace(syncedFileName))
                    return;

                StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, syncedFileName, saveData: true, notifyPhoneMenu: true);
                BroadcastAvatarDelta(playerName, clearAvatar: false);
                return;
            }

            var payload = new SocialUpdateAvatarRequestMessage
            {
                PlayerName = playerName,
                ClearAvatar = string.IsNullOrWhiteSpace(avatarPath)
            };

            if (!payload.ClearAvatar)
            {
                SocialUploadedImageMessage? upload = BuildAvatarUploadFromAbsolutePath(avatarPath);
                if (upload == null)
                    return;

                payload.AvatarImage = upload;
            }

            SendMessageToHost(payload, SocialMessageUpdateAvatarRequest);
        }

        private static SocialUploadedImageMessage? BuildAvatarUploadFromAbsolutePath(string avatarPath)
        {
            if (string.IsNullOrWhiteSpace(avatarPath) || !File.Exists(avatarPath))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(avatarPath);
                string sourceFileName = Path.GetFileName(avatarPath) ?? "avatar.png";
                string imageTag = string.Empty;

                if (ImageTags != null
                    && ImageTags.TryGetValue(sourceFileName, out string? loadedTag)
                    && !string.IsNullOrWhiteSpace(loadedTag))
                {
                    imageTag = loadedTag;
                }

                return new SocialUploadedImageMessage
                {
                    SourceFileName = sourceFileName,
                    Base64Image = Convert.ToBase64String(bytes),
                    ImageTag = imageTag
                };
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to read selected avatar for social sync: {ex}", LogLevel.Trace);
                return null;
            }
        }

        private static void BroadcastAvatarDelta(string playerName, bool clearAvatar)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges())
                return;

            var payload = new SocialAvatarDeltaMessage
            {
                PlayerName = playerName,
                ClearAvatar = clearAvatar
            };

            if (!clearAvatar
                && TryCreatePlayerAvatarUpsertPayload(playerName, out SocialPhotoUpsertMessage? upsert)
                && upsert != null)
            {
                payload.AvatarImage = upsert;
            }

            BroadcastToFarmhands(payload, SocialMessageAvatarDelta);
        }

        private void HandleSocialUpdateAvatarRequest(ModMessageReceivedEventArgs e)
        {
            if (!ShouldBroadcastAuthoritativeSocialChanges())
                return;

            SocialUpdateAvatarRequestMessage? payload = e.ReadAs<SocialUpdateAvatarRequestMessage>();
            if (payload == null)
                return;

            string playerName = (payload.PlayerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Player";

            if (payload.ClearAvatar)
            {
                DeletePlayerAvatarForPlayer(playerName);
                StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, string.Empty, saveData: true, notifyPhoneMenu: true);
                BroadcastAvatarDelta(playerName, clearAvatar: true);
                return;
            }

            string syncedFileName = SaveUploadedPlayerAvatar(payload.AvatarImage, playerName);
            if (string.IsNullOrWhiteSpace(syncedFileName))
                return;

            StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, syncedFileName, saveData: true, notifyPhoneMenu: true);
            BroadcastAvatarDelta(playerName, clearAvatar: false);
        }

        private void HandleSocialAvatarDelta(ModMessageReceivedEventArgs e)
        {
            if (!IsFarmhandSocialPeer() || !IsMessageFromHost(e.FromPlayerID))
                return;

            SocialAvatarDeltaMessage? payload = e.ReadAs<SocialAvatarDeltaMessage>();
            if (payload == null)
                return;

            string playerName = (payload.PlayerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(playerName))
                return;

            if (payload.ClearAvatar)
            {
                DeletePlayerAvatarForPlayer(playerName);
                StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, string.Empty, saveData: true, notifyPhoneMenu: true);
                InvalidateSocialImageLoadCaches();
                return;
            }

            if (TryApplyPlayerAvatarUpsert(payload.AvatarImage))
            {
                StardewConnectManager.SetSharedPlayerAvatarFileName(playerName, payload.AvatarImage.FileName, saveData: true, notifyPhoneMenu: true);
                InvalidateSocialImageLoadCaches();
            }
        }

        private static void InvalidateSocialImageLoadCaches()
        {
            if (phoneMenu != null)
                phoneMenu.ResetSocialImageLoadCache();
        }
    }
}

