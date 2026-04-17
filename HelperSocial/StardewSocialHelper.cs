using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;

namespace Smartphone
{
    public partial class ModEntry
    {
        // Social simulation tuning (edit these values to tweak behavior).
        private static double SocialEventCreatePostChance = 0.30;
        private static double SocialEventEngagementChance = 0.90;
        private static int SocialActionMaxDelayMilliseconds = 1000; // 10s = 10000ms
        private static int SocialPostMaxDelayMilliseconds = 3000; // 30s = 30000ms
        private static double SocialRepeatCommentChance = 0.30;
        private static bool PendingRandomNpcSocialPost = false;
        private static int PendingRandomNpcSocialPostTime = -1;

        // Engagement limits for random posts
        private static int SocialLikesWithin1DayMax = 8;
        private static int SocialCommentsWithin1DayMax = 5;
        private static int SocialLikesWithin3DaysMax = 7;
        private static int SocialCommentsWithin3DaysMax = 4;

        // Engagement limits for most popular posts
        private static int SocialLikesMostPopularWithin2DayMax = 7;
        private static int SocialCommentsMostPopularWithin2DayMax = 3;
        private static int SocialLikesMostPopularWithin4DaysMax = 5;
        private static int SocialCommentsMostPopularWithin4DaysMax = 3;


        // Social post weights. Keep total 100
        private static int SocialPostWeightText0Images = 25;
        private static int SocialPostWeightText1Image = 30;
        private static int SocialPostWeightText2Images = 15;
        private static int SocialPostWeightText3Images = 5;
        private static int SocialPostWeightNoText0Images = 0;
        private static int SocialPostWeightNoText1Image = 10;
        private static int SocialPostWeightNoText2Images = 10;
        private static int SocialPostWeightNoText3Images = 5;


        private static NPC? getRandomWeightedNPC()
        {
            List<NPC> candidates = Utility.getAllVillagers()
                .OfType<NPC>()
                .Where(npc => npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
                .ToList();

            if (candidates.Count == 0)
                return null;

            double getWeight(NPC npc)
            {
                return npc.Age switch
                {
                    1 => 0.50, // teens
                    2 => 0.35, // adults
                    0 => 0.15, // children
                    _ => 0.00
                };
            }

            // 2. Calculate total weight
            double totalWeight = candidates.Sum(npc => getWeight(npc));

            // 3. Pick a random threshold
            double randomValue = Random.Shared.NextDouble() * totalWeight;
            double cumulativeWeight = 0;

            // 4. Iterate and find the selected candidate
            foreach (var npc in candidates)
            {
                cumulativeWeight += getWeight(npc);
                if (randomValue < cumulativeWeight)
                {
                    return npc;
                }
            }

            return null;
        }

        // private static void RandomStardewSocialEvent()
        // {
        //     if (!Context.IsWorldReady)
        //         return;

        //     if (Game1.random.NextDouble() < SocialEventCreatePostChance)
        //         SetPendingRandomNpcSocialPost();

        //     if (Game1.random.NextDouble() < SocialEventEngagementChance)
        //         QueueRandomNpcEngagement();
        // }

        private static int GetSocialPostIntervalFromConfig()
        {
            return Config?.PostPerDay switch
            {
                ModConfig.PostPerDayHigh => 500,
                ModConfig.PostPerDayLow => 700,
                _ => 600
            };
        }

        private static int GetSocialEngagementIntervalFromConfig()
        {
            return Config?.PostPerDay switch
            {
                ModConfig.PostPerDayHigh => 500,
                ModConfig.PostPerDayLow => 800,
                _ => 600
            };
        }

        private static void SetPendingRandomNpcSocialPost()
        {
            PendingRandomNpcSocialPost = true;
            PendingRandomNpcSocialPostTime = Game1.timeOfDay;
            Game1.chatBox.addErrorMessage("Called SetPendingRandomNpcSocialPost at " + PendingRandomNpcSocialPostTime);
        }

        private static void ClearPendingRandomNpcSocialPost()
        {
            PendingRandomNpcSocialPost = false;
            PendingRandomNpcSocialPostTime = -1;
        }

        private static void TryQueuePendingRandomNpcSocialPost()
        {
            if (!PendingRandomNpcSocialPost)
                return;

            Game1.chatBox.addErrorMessage("Called TryQueuePendingRandomNpcSocialPost at " + Game1.timeOfDay);
            ClearPendingRandomNpcSocialPost();
            QueueRandomNpcSocialPost();
        }

        private static bool ShouldForceQueuePendingRandomNpcSocialPost(int currentTime)
        {
            if (!PendingRandomNpcSocialPost)
                return false;

            return GetElapsedInGameMinutes(PendingRandomNpcSocialPostTime, currentTime) >= 200;
        }

        private static int GetElapsedInGameMinutes(int startTime, int endTime)
        {
            if (startTime < 0 || endTime < 0)
                return 0;

            int startHour = Math.Max(0, startTime / 100);
            int startMinute = Math.Clamp(startTime % 100, 0, 59);
            int endHour = Math.Max(0, endTime / 100);
            int endMinute = Math.Clamp(endTime % 100, 0, 59);

            int startTotalMinutes = (startHour * 60) + startMinute;
            int endTotalMinutes = (endHour * 60) + endMinute;
            return Math.Max(0, endTotalMinutes - startTotalMinutes);
        }

        // Create posts
        private static void QueueRandomNpcSocialPost()
        {
            NPC? selectedNpc = getRandomWeightedNPC() ?? GetRandomVillagerNpc();
            if (selectedNpc == null || string.IsNullOrWhiteSpace(selectedNpc.Name))
                return;

            (bool includeText, int imageCount) = ChooseRandomSocialPostFormat();
            Game1.chatBox.addErrorMessage($"{selectedNpc.Name} includeText={includeText}, imageCount={imageCount}");
            int safeImageCount = Math.Clamp(imageCount, 0, 3);
            List<string> attachments = TryCaptureNpcPhoto(selectedNpc.Name, safeImageCount);

            string npcCharacteristic = GetNpcCharacteristicForPrompt(selectedNpc);
            List<List<string>> attachmentTags = attachments.Select(GetAttachmentTagsForPrompt).ToList();
            string authorName = selectedNpc.Name;
            QueueDelayedSocialFuncActionWithLimit(async () =>
            {
                if (!includeText)
                {
                    StardewConnectManager.AddNpcPostWithAttachments(authorName, string.Empty, attachments);
                    return;
                }

                await RunAiActionWithQueueAsync(async () =>
                {
                    string postText = await GenerateNpcSocialPostText(authorName, npcCharacteristic, attachmentTags);
                    StardewConnectManager.AddNpcPostWithAttachments(authorName, postText, attachments);
                });
            }, SocialPostMaxDelayMilliseconds);
        }

        private static List<string> GetAttachmentTagsForPrompt(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return new List<string>();

            string fileName = Path.GetFileName(imagePath.Trim());
            if (string.IsNullOrWhiteSpace(fileName) || ImageTags == null)
                return new List<string>();

            if (!ImageTags.TryGetValue(fileName, out string? imageTag) || string.IsNullOrWhiteSpace(imageTag))
                return new List<string>();

            return imageTag
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static (bool IncludeText, int ImageCount) ChooseRandomSocialPostFormat()
        {
            if (Game1.timeOfDay >= 2130 ||
               (Game1.currentSeason == "winter" && Game1.timeOfDay >= 1900) ||
               ((Game1.currentLocation.IsRainingHere() || Game1.currentLocation.IsGreenRainingHere()) && Game1.timeOfDay >= 1800))
                return (true, 0);

            var weightedFormats = new List<(bool IncludeText, int ImageCount, int Weight)>
            {
                (true, 0, SocialPostWeightText0Images),
                (true, 1, SocialPostWeightText1Image),
                (true, 2, SocialPostWeightText2Images),
                (true, 3, SocialPostWeightText3Images),
                (false, 0, SocialPostWeightNoText0Images),
                (false, 1, SocialPostWeightNoText1Image),
                (false, 2, SocialPostWeightNoText2Images),
                (false, 3, SocialPostWeightNoText3Images),
            };

            int totalWeight = weightedFormats.Sum(format => Math.Max(0, format.Weight));
            if (totalWeight <= 0)
                return (true, 0);

            int roll = Game1.random.Next(totalWeight);
            int cumulative = 0;
            foreach (var format in weightedFormats)
            {
                int safeWeight = Math.Max(0, format.Weight);
                if (safeWeight == 0)
                    continue;

                cumulative += safeWeight;
                if (roll < cumulative)
                    return (format.IncludeText, format.ImageCount);
            }

            return (true, 0);
        }

        private static void QueueRandomNpcEngagement()
        {
            List<string> randomPostsWithin1Day = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithinDayRange(count, 0, 1),
                desiredCount: 3);
            QueueLikesAndCommentsForPosts(randomPostsWithin1Day, SocialLikesWithin1DayMax, SocialCommentsWithin1DayMax);

            List<string> randomPostsWithin3Days = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithinDayRange(count, 1, 3),
                desiredCount: 2);
            QueueLikesAndCommentsForPosts(randomPostsWithin3Days, SocialLikesWithin3DaysMax, SocialCommentsWithin3DaysMax);

            string mostPopularWithin1Day = StardewConnectManager.GetOnePopularPostIdWithinDayRange(1, 2);
            if (!string.IsNullOrWhiteSpace(mostPopularWithin1Day))
                QueueLikesAndCommentsForPosts(new List<string> { mostPopularWithin1Day }, SocialLikesMostPopularWithin2DayMax, SocialCommentsMostPopularWithin2DayMax);

            string mostPopularWithin3Days = StardewConnectManager.GetOnePopularPostIdWithinDayRange(2, 4);
            if (!string.IsNullOrWhiteSpace(mostPopularWithin3Days))
                QueueLikesAndCommentsForPosts(new List<string> { mostPopularWithin3Days }, SocialLikesMostPopularWithin4DaysMax, SocialCommentsMostPopularWithin4DaysMax);
        }

        private static void QueueLikesAndCommentsForPosts(IEnumerable<string> postIds, int maxLikes, int maxComments)
        {
            foreach (string postId in postIds)
            {
                QueueLikesForPost(postId, maxLikes);
                // if (Game1.random.NextBool())
                QueueCommentsForPost(postId, maxComments);
            }
        }

        private static void QueueLikesForPost(string postId, int maxLikes)
        {
            if (string.IsNullOrWhiteSpace(postId) || maxLikes <= 0)
                return;

            int likeAttempts = Game1.random.Next(2, maxLikes + 1);
            for (int i = 0; i < likeAttempts; i++)
            {
                QueueDelayedSocialAction(() =>
                {
                    StardewConnectPost? post = StardewConnectManager.GetPost(postId);
                    if (post == null)
                        return;

                    string actorName = PickRandomNpcActorName(post, excludeAlreadyLiked: true);
                    if (string.IsNullOrWhiteSpace(actorName))
                        return;

                    StardewConnectManager.SetPostLike(postId, actorName, true);
                });
            }
        }

        // Post comments for posts
        private static void QueueCommentsForPost(string postId, int maxComments)
        {
            if (string.IsNullOrWhiteSpace(postId) || maxComments <= 0)
                return;

            int commentAttempts = Game1.random.Next(maxComments + 1);
            if (commentAttempts <= 0)
                return;

            QueueDelayedSocialFuncAction(async () =>
            {
                await RunAiActionWithQueueAsync(async () =>
                {
                    StardewConnectPost? post = StardewConnectManager.GetPost(postId);
                    if (post == null)
                        return;

                    List<string> actorNames = PickRandomNpcActorNamesForCommentBatch(post, commentAttempts);
                    if (actorNames.Count == 0)
                        return;

                    Dictionary<string, string> generatedComments = await GenerateNpcSocialPostComments(post, actorNames);
                    if (generatedComments.Count == 0)
                        return;

                    foreach (string actorName in actorNames)
                    {
                        if (!generatedComments.TryGetValue(actorName, out string? commentText) || string.IsNullOrWhiteSpace(commentText))
                            continue;

                        string commentAuthorName = actorName;
                        string generatedComment = commentText.Trim();
                        QueueDelayedSocialAction(() =>
                        {
                            StardewConnectManager.AddNpcComment(postId, commentAuthorName, generatedComment);
                        });
                    }
                });
            });
        }

        private static string PickRandomNpcActorNameForComment(StardewConnectPost post)
        {
            return PickRandomNpcActorNameForComment(post, excludedNpcNames: null);
        }

        private static List<string> PickRandomNpcActorNamesForCommentBatch(StardewConnectPost post, int desiredCount)
        {
            var selectedActors = new List<string>();
            if (post == null || desiredCount <= 0)
                return selectedActors;

            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < desiredCount; i++)
            {
                string actorName = PickRandomNpcActorNameForComment(post, reservedNames);
                if (string.IsNullOrWhiteSpace(actorName))
                    break;

                if (reservedNames.Add(actorName))
                    selectedActors.Add(actorName);
            }

            return selectedActors;
        }

        private static string PickRandomNpcActorNameForComment(StardewConnectPost post, ISet<string>? excludedNpcNames)
        {
            if (post == null)
                return string.Empty;

            List<NPC> candidates = Utility.getAllVillagers()
                .OfType<NPC>()
                .Where(npc => npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
                .Where(npc => !string.Equals(npc.Name, post.AuthorName, StringComparison.OrdinalIgnoreCase))
                .Where(npc => excludedNpcNames == null || !excludedNpcNames.Contains(npc.Name))
                .ToList();

            if (candidates.Count == 0)
                return string.Empty;

            NPC chosen = candidates[Game1.random.Next(candidates.Count)];
            if (!HasNpcAlreadyCommentedOnPost(post, chosen.Name))
                return chosen.Name;

            if (Game1.random.NextDouble() < SocialRepeatCommentChance)
                return chosen.Name;

            List<NPC> freshCandidates = candidates
                .Where(npc => !HasNpcAlreadyCommentedOnPost(post, npc.Name))
                .ToList();

            if (freshCandidates.Count == 0)
                return string.Empty;

            return freshCandidates[Game1.random.Next(freshCandidates.Count)].Name;
        }

        private static string PickRandomNpcActorName(StardewConnectPost post, bool excludeAlreadyLiked)
        {
            if (post == null)
                return string.Empty;

            List<NPC> candidates = Utility.getAllVillagers()
                .OfType<NPC>()
                .Where(npc => npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
                .Where(npc => !string.Equals(npc.Name, post.AuthorName, StringComparison.OrdinalIgnoreCase))
                .Where(npc => !excludeAlreadyLiked || !StardewConnectManager.IsPostLikedBy(post, npc.Name))
                .ToList();

            if (candidates.Count == 0)
                return string.Empty;

            return candidates[Game1.random.Next(candidates.Count)].Name;
        }

        private static bool HasNpcAlreadyCommentedOnPost(StardewConnectPost post, string npcName)
        {
            if (post?.Comments == null || string.IsNullOrWhiteSpace(npcName))
                return false;

            return post.Comments.Any(comment => comment != null && string.Equals(comment.AuthorName, npcName, StringComparison.OrdinalIgnoreCase));
        }

        private static NPC? GetRandomVillagerNpc(string excludedNpcName = "")
        {
            List<NPC> candidates = Utility.getAllVillagers()
                .OfType<NPC>()
                .Where(npc => npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
                .Where(npc => string.IsNullOrWhiteSpace(excludedNpcName)
                    || !string.Equals(npc.Name, excludedNpcName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
                return null;

            return candidates[Game1.random.Next(candidates.Count)];
        }

        private static List<string> GetDistinctRandomPostIds(Func<int, List<string>> getter, int desiredCount)
        {
            var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (getter == null || desiredCount <= 0)
                return new List<string>();

            int attempts = 0;
            while (uniqueIds.Count < desiredCount && attempts < 4)
            {
                int requestCount = Math.Max(1, desiredCount * 2);
                List<string> batch = getter(requestCount) ?? new List<string>();
                foreach (string postId in batch)
                {
                    if (!string.IsNullOrWhiteSpace(postId))
                        uniqueIds.Add(postId.Trim());
                }

                attempts++;
            }

            List<string> result = uniqueIds.ToList();
            ShuffleList(result);
            if (result.Count > desiredCount)
                result = result.Take(desiredCount).ToList();

            return result;
        }

        private static void ShuffleList<T>(IList<T> items)
        {
            if (items == null)
                return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Game1.random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private static void QueueDelayedSocialAction(Action action)
        {
            if (action == null)
                return;

            QueueDelayedSocialFuncAction(() =>
            {
                try
                {
                    action();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            });
        }

        private static void QueueDelayedSocialFuncAction(Func<Task> action)
        {
            if (action == null)
                return;

            QueueDelayedSocialFuncActionWithLimit(action, SocialActionMaxDelayMilliseconds);
        }

        private static void QueueDelayedSocialFuncActionWithLimit(Func<Task> action, int maxDelayMilliseconds)
        {
            if (action == null)
                return;

            int delay = Game1.random.Next(0, Math.Max(0, maxDelayMilliseconds) + 1);
            DelayedAction.functionAfterDelay(() =>
            {
                _ = RunDelayedSocialActionAsync(action);
            }, delay);
        }

        private static async Task RunDelayedSocialActionAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Random social action failed: {ex}", LogLevel.Trace);
            }
        }
    }
}