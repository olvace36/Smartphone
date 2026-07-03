using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;

namespace Smartphone
{
    public class NotificationSaveModel
    {
        public List<string> NotificationList { get; set; } = new();
        public int UnreadNotification { get; set; } = 0;
    }

    public static class NotificationManager
    {
        public static List<string> NotificationList = new();
        public static int UnreadNotification = 0;

        public static int GetUnreadNotification()
        {
            return UnreadNotification;
        }

        public static void ResetUnreadNotification()
        {
            UnreadNotification = 0;
            SaveNotificationData();
        }

        public static void AddUnreadNotification(int add = 1)
        {
            UnreadNotification += add;
            SaveNotificationData();
        }

        public static List<string> GetNotificationList()
        {
            return NotificationList;
        }

        public static void AddNotification(string notificationMessage, string notificationName = "")
        {
            string entry = string.IsNullOrEmpty(notificationName)
                ? notificationMessage
                : notificationName + "::" + notificationMessage;
            NotificationList.Add(entry);
            if (ModEntry.Config?.NotifyNotification ?? true)
            {
                if (!string.IsNullOrWhiteSpace(notificationName))
                    Game1.addHUDMessage(new HUDMessage(ModEntry.SHelper.Translation.Get("ui.hud.new_notification_from", new { notificationName = notificationName }), HUDMessage.newQuest_type));
                else
                    Game1.addHUDMessage(new HUDMessage(ModEntry.SHelper.Translation.Get("ui.hud.new_notification"), HUDMessage.newQuest_type));
            }

            DelayedAction.playSoundAfterDelay(ModEntry.currentPhoneSound, 0);
            if (ModEntry.phoneMenu == null || PhoneMenu.currentApp != "appNotification")
                AddUnreadNotification();
            else
                SaveNotificationData();
        }

        public static void ClearNotification()
        {
            NotificationList.Clear();
            ResetUnreadNotification();
        }

        public static void SaveNotificationData()
        {
            var model = new NotificationSaveModel
            {
                NotificationList = NotificationList,
                UnreadNotification = UnreadNotification
            };
            ModEntry.SHelper.Data.WriteJsonFile(ModEntry.GetSaveDataPath("notification_list.json"), model);
        }

        public static void LoadNotificationData()
        {
            var model = ModEntry.SHelper.Data.ReadJsonFile<NotificationSaveModel>(ModEntry.GetSaveDataPath("notification_list.json"));
            NotificationList = model?.NotificationList ?? new List<string>();
            UnreadNotification = model?.UnreadNotification ?? 0;
        }
    }
}
