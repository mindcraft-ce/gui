using Newtonsoft.Json;

namespace MindcraftCE.Models
{
    public class Modes
    {
        [JsonProperty("self_preservation")]
        public bool SelfPreservation { get; set; } = true;

        [JsonProperty("unstuck")]
        public bool Unstuck { get; set; } = true;

        [JsonProperty("cowardice")]
        public bool Cowardice { get; set; } = false;

        [JsonProperty("self_defense")]
        public bool SelfDefense { get; set; } = true;

        [JsonProperty("hunting")]
        public bool Hunting { get; set; } = true;

        [JsonProperty("item_collecting")]
        public bool ItemCollecting { get; set; } = true;

        [JsonProperty("torch_placing")]
        public bool TorchPlacing { get; set; } = true;

        [JsonProperty("elbow_room")]
        public bool ElbowRoom { get; set; } = true;

        [JsonProperty("idle_staring")]
        public bool IdleStaring { get; set; } = true;

        [JsonProperty("cheat")]
        public bool Cheat { get; set; } = false;
    }
}
