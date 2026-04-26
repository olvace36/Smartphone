using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smartphone
{
    public interface IUnlimitedEventExpansionApi
    {
        /// <summary>
        /// Check if an event is in the process of creation.
        /// </summary>
        /// <returns>True if an event is pending, otherwise false.</returns>
        // bool IsAnEventPending();

        /// <summary>
        /// Sends a summary of the player's conversations with NPCs to the mod. This method is used to provide the mod with information about which NPCs the player has interacted with and what topics were discussed, allowing the mod to trigger specific events based on those interactions.
        /// </summary>
        /// <param name="npcConversationSummary">A dictionary containing the NPC names as keys and the conversation summaries as values.</param>
        void SendNpcConversationSummary(Dictionary<string, string> npcConversationSummary);

        /// <summary>
        /// Opens the schedule event time menu for a specific NPC and event type. This method is used to allow players to schedule events with NPCs at specific times, providing a more interactive and personalized gaming experience.
        /// </summary>
        /// <param name="eventNpcName">The name of the NPC for whom the event is being scheduled.</param>
        /// <param name="eventType">The type of event being scheduled.</param>
        /// <param name="eventDisplayName">The display name of the event.</param>
        /// <param name="npcDisplayName">The display name of the NPC.</param>
        /// <param name="npcResponse">The confirmation message for the NPC's response.</param>
        void OpenScheduleEventTimeMenu(string eventNpcName,
            string eventType,
            string eventDisplayName,
            string npcDisplayName,
            string? npcResponse = null
            );

    }
}
