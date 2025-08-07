using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mindcraft_ce.Models
{
    public class Agent : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public Agent()
        {
            Modes = new Modes();
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        public string FileName { get; set; }

        [JsonProperty("model")]
        public Model ModelInfo { get; set; }

        [JsonProperty("is_checked")]
        private bool isChecked = true;
        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        [JsonProperty("embedding")]
        public string? Embedding { get; set; }

        [JsonProperty("vision_model")]
        public string? VisionModel { get; set; }

        [JsonProperty("code_model")]
        public string? CodeModel { get; set; }

        [JsonProperty("cooldown")]
        public int? Cooldown { get; set; }

        [JsonProperty("conversing")]
        public string? Conversing { get; set; }

        [JsonProperty("coding")]
        public string? Coding { get; set; }

        [JsonProperty("saving_memory")]
        public string? SavingMemory { get; set; }

        [JsonProperty("bot_responder")]
        public string? BotResponder { get; set; }

        [JsonProperty("image_analysis")]
        public string? ImageAnalysis { get; set; }

        [JsonProperty("speak_model")]
        public string? SpeakModel { get; set; }

        [JsonProperty("modes")]
        public Modes? Modes { get; set; }

        [JsonProperty("conversation_examples")]
        public List<List<JObject>>? ConversationExamples { get; set; }
    }
}
