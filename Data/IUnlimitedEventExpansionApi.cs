using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smartphone.Data
{
    public interface IUnlimitedEventExpansionApi
    {
        void SendNpcConversationSummary(Dictionary<string, string> npcConversationSummary);
        void TriggerDinnerEvent(string npcName);
        void TriggerNpcBirthdayEvent(string npcName);
        void TriggerPicnicEvent(string npcName);
        void TriggerCampingEvent(string npcName);
    }
}
