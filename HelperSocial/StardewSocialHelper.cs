using System;
using System.Collections.Generic;
using System.Linq;
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


        // Social post weights
        private static int SocialPostWeightText0Images = 22;
        private static int SocialPostWeightText1Image = 20;
        private static int SocialPostWeightText2Images = 14;
        private static int SocialPostWeightText3Images = 8;
        private static int SocialPostWeightNoText0Images = 0;
        private static int SocialPostWeightNoText1Image = 16;
        private static int SocialPostWeightNoText2Images = 12;
        private static int SocialPostWeightNoText3Images = 8;


        private static NPC? getRandomWeightedNPC()
        {
            if (npcToAgeGroup != null && npcToAgeGroup.Count > 0)
            {
                const int weight0 = 1;
                const int weight16 = 3;
                const int weight36 = 2;
                const int weight55 = 1;

                int totalWeight = 0;
                foreach (var kvp in npcToAgeGroup)
                {
                    int w = kvp.Value == "0+" ? weight0
                          : kvp.Value == "16+" ? weight16
                          : kvp.Value == "36+" ? weight36
                          : kvp.Value == "55+" ? weight55
                          : 0;
                    totalWeight += w;
                }

                if (totalWeight > 0)
                {
                    int pick = Game1.random.Next(totalWeight);
                    int cumulative = 0;
                    string? chosenNpc = null;
                    foreach (var kvp in npcToAgeGroup)
                    {
                        int w = kvp.Value == "0+" ? weight0
                              : kvp.Value == "16+" ? weight16
                              : kvp.Value == "36+" ? weight36
                              : kvp.Value == "55+" ? weight55
                              : 0;
                        cumulative += w;
                        if (pick < cumulative)
                        {
                            chosenNpc = kvp.Key;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(chosenNpc) && Game1.getCharacterFromName(chosenNpc) is NPC randomNpc)
                    {
                        return randomNpc;
                    }
                }
            }

            return null;
        }

        private static void RandomStardewSocialEvent()
        {
            if (!Context.IsWorldReady)
                return;

            if (Game1.random.NextDouble() < SocialEventCreatePostChance)
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

            string postText = includeText ? "POST TEXT" : string.Empty;
            if (string.IsNullOrWhiteSpace(postText) && attachments.Count == 0)
                postText = "POST TEXT";

            string authorName = selectedNpc.Name;
            QueueDelayedSocialAction(() =>
            {
                StardewConnectManager.AddNpcPostWithAttachments(authorName, postText, attachments);
            });
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
            for (int i = 0; i < commentAttempts; i++)
            {
                QueueDelayedSocialAction(() =>
                {
                    StardewConnectPost? post = StardewConnectManager.GetPost(postId);
                    if (post == null)
                        return;

                    string actorName = PickRandomNpcActorNameForComment(post);
                    if (string.IsNullOrWhiteSpace(actorName))
                        return;

                    StardewConnectManager.AddNpcComment(postId, actorName, "COMMENT");
                });
            }
        }

        private static string PickRandomNpcActorNameForComment(StardewConnectPost post)
        {
            if (post == null)
                return string.Empty;

            List<NPC> candidates = Utility.getAllCharacters()
                .OfType<NPC>()
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
                .Where(npc => !string.Equals(npc.Name, post.AuthorName, StringComparison.OrdinalIgnoreCase))
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

            List<NPC> candidates = Utility.getAllCharacters()
                .OfType<NPC>()
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
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
            List<NPC> candidates = Utility.getAllCharacters()
                .OfType<NPC>()
                .Where(npc => npc != null && npc.IsVillager && !string.IsNullOrWhiteSpace(npc.Name) && npc.CanSocialize && Game1.player.friendshipData.ContainsKey(npc.Name))
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

            int delay = Game1.random.Next(0, SocialActionMaxDelayMilliseconds + 1);
            DelayedAction.functionAfterDelay(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Random social action failed: {ex}", LogLevel.Trace);
                }
            }, delay);
        }
    }
}