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
        bool IsAnEventPending();

        /// <summary>
        /// Sends a summary of the player's conversations with NPCs to the mod. This method is used to provide the mod with information about which NPCs the player has interacted with and what topics were discussed, allowing the mod to trigger specific events based on those interactions.
        /// </summary>
        /// <param name="npcConversationSummary">A dictionary containing the NPC names as keys and the conversation summaries as values.</param>
        void SendNpcConversationSummary(Dictionary<string, string> npcConversationSummary);

    }
}
