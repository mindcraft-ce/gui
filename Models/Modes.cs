using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ABI.System;
using Newtonsoft.Json.Linq;
using Windows.Services.Maps;

namespace mindcraft_ce.Models
{
    public class Modes
    {
        public bool SelfPreservation { get; set; } = true;
        public bool Unstuck { get; set; } = true;
        public bool Cowardice { get; set; } = false;
        public bool SelfDefense { get; set; } = true;
        public bool Hunting { get; set; } = true;
        public bool ItemCollecting { get; set; } = true;
        public bool TorchPlacing { get; set; } = true;
        public bool ElbowRoom { get; set; } = true;
        public bool IdleStaring { get; set; } = true;
        public bool Cheat { get; set; } = false;

        public Modes() { }
        public Modes(JObject input)
        {
            if (input == null) return;

            SelfPreservation = input.Value<bool?>("self_preservation") ?? SelfPreservation;
            Unstuck = input.Value<bool?>("unstuck") ?? Unstuck;
            Cowardice = input.Value<bool?>("cowardice") ?? Cowardice;
            SelfDefense = input.Value<bool?>("self_defense") ?? SelfDefense;
            Hunting = input.Value<bool?>("hunting") ?? Hunting;
            ItemCollecting = input.Value<bool?>("item_collecting") ?? ItemCollecting;
            TorchPlacing = input.Value<bool?>("torch_placing") ?? TorchPlacing;
            ElbowRoom = input.Value<bool?>("elbow_room") ?? ElbowRoom;
            IdleStaring = input.Value<bool?>("idle_staring") ?? IdleStaring;
            Cheat = input.Value<bool?>("cheat") ?? Cheat;
        }
    }

}
