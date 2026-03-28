using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smartphone
{
    public interface ISmartPhoneApi
    {
        /// <summary>
        /// Gets a list of NPCs that have been registered to receive smartphone messages. 
        /// This list is used to populate the dropdown menu in the smartphone UI, allowing players to select which NPC they want to send messages to.
        /// </summary>
        /// <returns>A list of NPC names.</returns>
        List<string> GetPhoneNpcList();

        /// <summary>
        /// Sends a message from an NPC to the player. This method is used to simulate receiving messages on the player's smartphone from NPCs in the game.
        /// </summary>
        /// <param name="npcName">The name of the NPC sending the message (case-sensitive).</param>
        /// <param name="message">The content of the message being sent.</param>
        void SendSmartphoneMessageFromNPC(string npcName, string message);

        /// <summary>
        /// Sends a message from the player to an NPC. This method is used to simulate sending messages from the player's smartphone to NPCs in the game.
        /// </summary>
        /// <param name="npcName">The name of the NPC receiving the message (case-sensitive).</param>
        /// <param name="message">The content of the message being sent.</param>
        void SendSmartphoneMessageFromPlayer(string npcName, string message);

        /// <summary>
        /// Sends a notification to the player's smartphone.
        /// </summary>
        /// <param name="message">The content of the notification.</param>
        void SendSmartphoneNotification(string message);
    }
}
