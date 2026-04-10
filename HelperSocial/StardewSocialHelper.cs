using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    public partial class ModEntry
    {
        // Social simulation tuning (edit these values to tweak behavior).
        private static double SocialEventCreatePostChance = 0.30;
        private static double SocialEventEngagementChance = 0.90;
        private static int SocialActionMaxDelayMilliseconds = 10000;
        private static double SocialRepeatCommentChance = 0.30;

        // Engagement limits for random posts
        private static int SocialLikesWithin1DayMax = 5;
        private static int SocialCommentsWithin1DayMax = 2;
        private static int SocialLikesWithin3DaysMax = 4;
        private static int SocialCommentsWithin3DaysMax = 1;
        private static int SocialLikesWithin7DaysMax = 4;
        private static int SocialCommentsWithin7DaysMax = 1;

        // Engagement limits for most popular posts
        private static int SocialLikesMostPopularWithin1DayMax = 8;
        private static int SocialCommentsMostPopularWithin1DayMax = 3;
        private static int SocialLikesMostPopularWithin3DaysMax = 5;
        private static int SocialCommentsMostPopularWithin3DaysMax = 2;


        // Social post weights. Keep total 200
        private static int SocialPostWeightText0Images = 65;
        private static int SocialPostWeightText1Image = 30;
        private static int SocialPostWeightText2Images = 10;
        private static int SocialPostWeightText3Images = 5;
        private static int SocialPostWeightNoText0Images = 0;
        private static int SocialPostWeightNoText1Image = 30;
        private static int SocialPostWeightNoText2Images = 40;
        private static int SocialPostWeightNoText3Images = 20;


        private static NPC? getRandomWeightedNPC()
        {
            List<NPC> candidates = Utility.getAllVillagers()
                .OfType<NPC>()
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize)
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

        private static void RandomStardewSocialEvent()
        {
            if (!Context.IsWorldReady)
                return;

            if (Game1.random.NextDouble() < 1)
                QueueRandomNpcSocialPost();

            if (Game1.random.NextDouble() < SocialEventEngagementChance)
                QueueRandomNpcEngagement();
        }

        // Create posts
        private static void QueueRandomNpcSocialPost()
        {
            NPC? selectedNpc = getRandomWeightedNPC() ?? GetRandomVillagerNpc();
            if (selectedNpc == null || string.IsNullOrWhiteSpace(selectedNpc.Name))
                return;

            (bool includeText, int imageCount) = ChooseRandomSocialPostFormat();
            List<string> attachments = new List<string>();
            int safeImageCount = Math.Clamp(imageCount, 0, 3);

            for (int i = 0; i < safeImageCount; i++)
            {
                string imagePath = CaptureNpcPhoto(selectedNpc);
                if (!string.IsNullOrWhiteSpace(imagePath))
                    attachments.Add(imagePath);
            }

            string npcCharacteristic = GetNpcCharacteristicForPrompt(selectedNpc);
            List<List<string>> attachmentTags = attachments.Select(GetAttachmentTagsForPrompt).ToList();
            string authorName = selectedNpc.Name;
            QueueDelayedSocialAction(async () =>
            {
                string postText = string.Empty;
                if (includeText)
                    postText = await GenerateNpcSocialPostText(authorName, npcCharacteristic, attachmentTags);

                StardewConnectManager.AddNpcPostWithAttachments(authorName, postText, attachments);
            });
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
                count => StardewConnectManager.GetRandomPostIdWithin1DayRange(count),
                desiredCount: 3);
            QueueLikesAndCommentsForPosts(randomPostsWithin1Day, SocialLikesWithin1DayMax, SocialCommentsWithin1DayMax);

            List<string> randomPostsWithin3Days = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithin3Days(count),
                desiredCount: 2);
            QueueLikesAndCommentsForPosts(randomPostsWithin3Days, SocialLikesWithin3DaysMax, SocialCommentsWithin3DaysMax);

            List<string> randomPostsWithin7Days = GetDistinctRandomPostIds(
                count => StardewConnectManager.GetRandomPostIdWithin7Days(count),
                desiredCount: 1);
            QueueLikesAndCommentsForPosts(randomPostsWithin7Days, SocialLikesWithin7DaysMax, SocialCommentsWithin7DaysMax);

            string mostPopularWithin1Day = StardewConnectManager.GetMostPopularPostIdWithin1Day();
            if (!string.IsNullOrWhiteSpace(mostPopularWithin1Day))
                QueueLikesAndCommentsForPosts(new List<string> { mostPopularWithin1Day }, SocialLikesMostPopularWithin1DayMax, SocialCommentsMostPopularWithin1DayMax);

            string mostPopularWithin3Days = StardewConnectManager.GetMostPopularPostIdWithin3Days();
            if (!string.IsNullOrWhiteSpace(mostPopularWithin3Days))
                QueueLikesAndCommentsForPosts(new List<string> { mostPopularWithin3Days }, SocialLikesMostPopularWithin3DaysMax, SocialCommentsMostPopularWithin3DaysMax);
        }

        private static void QueueLikesAndCommentsForPosts(IEnumerable<string> postIds, int maxLikes, int maxComments)
        {
            foreach (string postId in postIds)
            {
                QueueLikesForPost(postId, maxLikes);
                QueueCommentsForPost(postId, maxComments);
            }
        }

        private static void QueueLikesForPost(string postId, int maxLikes)
        {
            if (string.IsNullOrWhiteSpace(postId) || maxLikes <= 0)
                return;

            int likeAttempts = Game1.random.Next(1, maxLikes + 1);
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

            QueueDelayedSocialAction(async () =>
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
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize)
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
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize)
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
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize)
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

            QueueDelayedSocialAction(() =>
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

        private static void QueueDelayedSocialAction(Func<Task> action)
        {
            if (action == null)
                return;

            int delay = Game1.random.Next(0, SocialActionMaxDelayMilliseconds + 1);
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