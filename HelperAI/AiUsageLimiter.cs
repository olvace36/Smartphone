using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace Smartphone
{
    public partial class ModEntry
    {
        private sealed class QueuedAiAction
        {
            public Func<Task> Action { get; init; } = null!;
            public string QueueKey { get; init; } = string.Empty;
        }

        private const int MaxAiCallsPerDay = 4;
        public static int AiCallsRemainingToday = MaxAiCallsPerDay;
        public static int SuccessfulAiCallsToday = 0;

        private static readonly object aiQuotaLock = new();
        private static readonly Queue<QueuedAiAction> highPriorityQueuedAiActions = new();
        private static readonly Queue<QueuedAiAction> normalPriorityQueuedAiActions = new();
        private static readonly HashSet<string> queuedAiActionKeys = new(StringComparer.OrdinalIgnoreCase);
        private static bool aiQueueProcessorRunning = false;

        public static bool IsAiUsageLimitEnabled()
        {
            return string.IsNullOrWhiteSpace(Config?.OpenAIKey);
        }

        public static void ResetDailyAiUsageLimit()
        {
            lock (aiQuotaLock)
            {
                AiCallsRemainingToday = MaxAiCallsPerDay;
                SuccessfulAiCallsToday = 0;
            }
        }

        public static void HandleAiUsageTimeChanged(int newTime)
        {
            if (!IsAiUsageLimitEnabled())
            {
                TriggerQueuedAiActions();
                return;
            }

            if (newTime % 200 != 0)
                return;

            bool refilled = false;
            lock (aiQuotaLock)
            {
                if (AiCallsRemainingToday < MaxAiCallsPerDay)
                {
                    AiCallsRemainingToday++;
                    refilled = true;
                }
            }

            if (refilled)
            {
                if (TryTakeNextQueuedAiAction(out Func<Task> queuedAction))
                    _ = RunAiActionSafeAsync(queuedAction);
            }
        }

        public static bool TryConsumeAiCallSlot()
        {
            if (!IsAiUsageLimitEnabled())
                return true;

            lock (aiQuotaLock)
            {
                if (AiCallsRemainingToday <= 0)
                    return false;

                AiCallsRemainingToday--;
                return true;
            }
        }

        public static void RegisterSuccessfulAiCall()
        {
            lock (aiQuotaLock)
            {
                SuccessfulAiCallsToday++;
            }
        }

        public static Task RunAiActionWithQueueAsync(Func<Task> action, string queueKey = "", bool highPriority = false)
        {
            return RunAiActionWithQueueInternalAsync(action, queueKey, highPriority, consumeSlotNow: true);
        }

        private static async Task RunAiActionWithQueueInternalAsync(Func<Task> action, string queueKey, bool highPriority, bool consumeSlotNow)
        {
            if (action == null)
                return;

            if (!IsAiUsageLimitEnabled())
            {
                await RunAiActionSafeAsync(action);
                TriggerQueuedAiActions();
                return;
            }

            if (consumeSlotNow)
            {
                if (!TryConsumeAiCallSlot())
                {
                    EnqueueAiAction(
                        () => RunAiActionWithQueueInternalAsync(action, queueKey, highPriority, consumeSlotNow: false),
                        queueKey,
                        highPriority);
                    return;
                }
            }

            await RunAiActionSafeAsync(action);
        }

        public static void ClearQueuedAiActions()
        {
            lock (aiQuotaLock)
            {
                highPriorityQueuedAiActions.Clear();
                normalPriorityQueuedAiActions.Clear();
                queuedAiActionKeys.Clear();
            }
        }

        public static void TriggerQueuedAiActions(int maxActionsToProcess = int.MaxValue)
        {
            if (maxActionsToProcess <= 0)
                return;

            _ = ProcessQueuedAiActionsAsync(maxActionsToProcess);
        }

        private static void EnqueueAiAction(Func<Task> action, string queueKey, bool highPriority)
        {
            if (action == null)
                return;

            lock (aiQuotaLock)
            {
                if (!string.IsNullOrWhiteSpace(queueKey) && queuedAiActionKeys.Contains(queueKey))
                    return;

                var queuedAction = new QueuedAiAction
                {
                    Action = action,
                    QueueKey = queueKey ?? string.Empty
                };

                if (highPriority)
                    highPriorityQueuedAiActions.Enqueue(queuedAction);
                else
                    normalPriorityQueuedAiActions.Enqueue(queuedAction);

                if (!string.IsNullOrWhiteSpace(queuedAction.QueueKey))
                    queuedAiActionKeys.Add(queuedAction.QueueKey);
            }
        }

        private static async Task ProcessQueuedAiActionsAsync(int maxActionsToProcess)
        {
            lock (aiQuotaLock)
            {
                if (aiQueueProcessorRunning)
                    return;

                aiQueueProcessorRunning = true;
            }

            try
            {
                int processed = 0;

                while (processed < maxActionsToProcess)
                {
                    if (!TryTakeNextQueuedAiAction(out Func<Task> nextAction))
                        break;

                    await RunAiActionSafeAsync(nextAction);
                    processed++;
                }
            }
            finally
            {
                lock (aiQuotaLock)
                {
                    aiQueueProcessorRunning = false;
                }
            }
        }

        private static bool TryTakeNextQueuedAiAction(out Func<Task> action)
        {
            action = static () => Task.CompletedTask;
            bool limitEnabled = IsAiUsageLimitEnabled();

            lock (aiQuotaLock)
            {
                if (highPriorityQueuedAiActions.Count == 0 && normalPriorityQueuedAiActions.Count == 0)
                    return false;

                if (limitEnabled && AiCallsRemainingToday <= 0)
                    return false;

                QueuedAiAction nextAction = highPriorityQueuedAiActions.Count > 0
                    ? highPriorityQueuedAiActions.Dequeue()
                    : normalPriorityQueuedAiActions.Dequeue();

                if (!string.IsNullOrWhiteSpace(nextAction.QueueKey))
                    queuedAiActionKeys.Remove(nextAction.QueueKey);

                if (limitEnabled)
                    AiCallsRemainingToday--;

                action = nextAction.Action;
                return true;
            }
        }

        private static async Task RunAiActionSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Queued AI action failed: {ex}", LogLevel.Trace);
            }
        }

        private static int NormalizeTimeToHour(int rawTime)
        {
            if (rawTime <= 0)
                return 0;

            int hour = rawTime / 100;
            return Math.Clamp(hour, 0, 26);
        }
    }
}
