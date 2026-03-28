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
        /// Sends a summary of the player's conversations with NPCs to the mod. This method is used to provide the mod with information about which NPCs the player has interacted with and what topics were discussed, allowing the mod to trigger specific events based on those interactions.
        /// </summary>
        /// <param name="npcConversationSummary">A dictionary containing the NPC names as keys and the conversation summaries as values.</param>
        void SendNpcConversationSummary(Dictionary<string, string> npcConversationSummary);

        /// <summary>
        /// Triggers a dinner event for a given NPC based on the player's interactions with that NPC.
        /// </summary>
        /// <param name="npcName">The name of the NPC for whom the event should be triggered.</param>
        void TriggerDinnerEvent(string npcName);

        /// <summary>
        /// Triggers a birthday event for a given NPC based on the player's interactions with that NPC.
        /// </summary>
        /// <param name="npcName">The name of the NPC for whom the event should be triggered.</param> 
        void TriggerNpcBirthdayEvent(string npcName);

        /// <summary>
        /// Triggers a picnic event for a given NPC based on the player's interactions with that NPC.
        /// </summary>
        /// <param name="npcName">The name of the NPC for whom the event should be triggered.</param>
        void TriggerPicnicEvent(string npcName);

        /// <summary>
        /// Triggers a camping event for a given NPC based on the player's interactions with that NPC.
        /// </summary>
        /// <param name="npcName">The name of the NPC for whom the event should be triggered.</param>
        void TriggerCampingEvent(string npcName);
    }
}
