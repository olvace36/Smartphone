using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;

namespace Smartphone
{
    public static class NotificationManager
    {
        public static List<string> notificationList = new();
        public static int unreadNotification = 0;

        public static int getUnreadNotication()
        {
            return unreadNotification;
        }

        public static void resetUnreadNotication()
        {
            unreadNotification = 0;
        }

        public static void addUnreadNotication(int add = 1)
        {
            unreadNotification += add;
        }

        public static List<string> getNoticationList()
        {
            return notificationList;
        }

        public static void addNotification(string notificationMessage, string notificationName = "")
        {
            notificationList.Add(notificationMessage);
            if (ModEntry.Config?.notifyNotification ?? true)
            {
                if (!string.IsNullOrWhiteSpace(notificationName))
                    Game1.addHUDMessage(new HUDMessage($"New notification from {notificationName}", HUDMessage.newQuest_type));
                else
                    Game1.addHUDMessage(new HUDMessage("New notification", HUDMessage.newQuest_type));
            }

            DelayedAction.playSoundAfterDelay(MessageManager.currentPhoneSound, 0);
            if (ModEntry.phoneMenu != null && PhoneMenu.currentApp != "appNotification")
                addUnreadNotication();
        }

        public static void clearNotification()
        {
            notificationList.Clear();
            resetUnreadNotication();
        }

        public static void SaveNoticationData()
        {
            ModEntry.SHelper.Data.WriteJsonFile(ModEntry.GetSaveDataPath("notificationList"), notificationList);
        }

        public static void LoadNoticationData()
        {
            notificationList = ModEntry.SHelper.Data.ReadJsonFile<List<string>>(ModEntry.GetSaveDataPath("notificationList"))
                          ?? new List<string>();
        }
    }
}
