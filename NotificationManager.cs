using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;

namespace Smartphone
{
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
        }

        public static void AddUnreadNotification(int add = 1)
        {
            UnreadNotification += add;
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
        }

        public static void ClearNotification()
        {
            NotificationList.Clear();
            ResetUnreadNotification();
        }

        public static void SaveNotificationData()
        {
            ModEntry.SHelper.Data.WriteJsonFile(ModEntry.GetSaveDataPath("notificationList"), NotificationList);
        }

        public static void LoadNotificationData()
        {
            NotificationList = ModEntry.SHelper.Data.ReadJsonFile<List<string>>(ModEntry.GetSaveDataPath("notificationList"))
                          ?? new List<string>();
        }
    }
}
