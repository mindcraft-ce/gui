using System.Collections.Generic;
using Newtonsoft.Json;

namespace MindcraftCE.Models
{
    public class Settings
    {
        [JsonProperty("minecraft_version")]
        public string MinecraftVersion { get; set; } = "1.21.1";

        [JsonProperty("host")]
        public string Host { get; set; } = "127.0.0.1";

        [JsonProperty("port")]
        public int Port { get; set; } = 55916;

        [JsonProperty("auth")]
        public string Auth { get; set; } = "offline";

        [JsonProperty("host_mindserver")]
        public bool HostMindserver { get; set; } = true;

        [JsonProperty("mindserver_host")]
        public string MindserverHost { get; set; } = "localhost";

        [JsonProperty("mindserver_port")]
        public int MindserverPort { get; set; } = 8080;

        [JsonProperty("base_profile")]
        public string BaseProfile { get; set; } = "./profiles/defaults/_default.json";

        [JsonProperty("profiles")]
        public List<string> Profiles { get; set; } = new() {};

        [JsonProperty("plugins")]
        public List<string> Plugins { get; set; } = new();

        [JsonProperty("load_memory")]
        public bool LoadMemory { get; set; } = true;

        [JsonProperty("init_message")]
        public string InitMessage { get; set; } = "Respond with hello world and your name";

        [JsonProperty("only_chat_with")]
        public List<string> OnlyChatWith { get; set; } = new();

        [JsonProperty("language")]
        public string Language { get; set; } = "en";

        [JsonProperty("show_bot_views")]
        public bool ShowBotViews { get; set; } = false;

        [JsonProperty("allow_insecure_coding")]
        public bool AllowInsecureCoding { get; set; } = false;

        [JsonProperty("allow_vision")]
        public bool AllowVision { get; set; } = false;

        [JsonProperty("vision_mode")]
        public string VisionMode { get; set; } = "prompted";

        [JsonProperty("blocked_actions")]
        public List<string> BlockedActions { get; set; } = new() {};

        [JsonProperty("code_timeout_mins")]
        public int CodeTimeoutMins { get; set; } = -1;

        [JsonProperty("relevant_docs_count")]
        public int RelevantDocsCount { get; set; } = 5;

        [JsonProperty("max_messages")]
        public int MaxMessages { get; set; } = 15;

        [JsonProperty("num_examples")]
        public int NumExamples { get; set; } = 2;

        [JsonProperty("max_commands")]
        public int MaxCommands { get; set; } = -1;

        [JsonProperty("verbose_commands")]
        public bool VerboseCommands { get; set; } = true;

        [JsonProperty("narrate_behavior")]
        public bool NarrateBehavior { get; set; } = true;

        [JsonProperty("chat_bot_messages")]
        public bool ChatBotMessages { get; set; } = true;

        [JsonProperty("auto_idle_trigger")]
        public AutoIdleTrigger AutoIdleTrigger { get; set; } = new();

        [JsonProperty("speak")]
        public bool Speak { get; set; } = true;

        [JsonProperty("stt_transcription")]
        public bool SttTranscription { get; set; } = false;

        [JsonProperty("stt_provider")]
        public string SttProvider { get; set; } = "pollinations";

        [JsonProperty("stt_username")]
        public string SttUsername { get; set; } = "SERVER";

        [JsonProperty("stt_agent_name")]
        public string SttAgentName { get; set; } = "";

        [JsonProperty("stt_rms_threshold")]
        public int SttRmsThreshold { get; set; } = 3000;

        [JsonProperty("stt_silence_duration")]
        public int SttSilenceDuration { get; set; } = 2000;

        [JsonProperty("stt_min_audio_duration")]
        public double SttMinAudioDuration { get; set; } = 0.5;

        [JsonProperty("stt_max_audio_duration")]
        public double SttMaxAudioDuration { get; set; } = 45;

        [JsonProperty("stt_debug_audio")]
        public bool SttDebugAudio { get; set; } = true;

        [JsonProperty("stt_cooldown_ms")]
        public int SttCooldownMs { get; set; } = 2000;

        [JsonProperty("stt_speech_threshold_ratio")]
        public double SttSpeechThresholdRatio { get; set; } = 0.05;

        [JsonProperty("stt_consecutive_speech_samples")]
        public int SttConsecutiveSpeechSamples { get; set; } = 3;

        [JsonProperty("log_normal_data")]
        public bool LogNormalData { get; set; } = false;

        [JsonProperty("log_reasoning_data")]
        public bool LogReasoningData { get; set; } = false;

        [JsonProperty("log_vision_data")]
        public bool LogVisionData { get; set; } = false;

        [JsonProperty("external_logging")]
        public bool ExternalLogging { get; set; } = true;
    }

    public class AutoIdleTrigger
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonProperty("timeout_secs")]
        public int TimeoutSecs { get; set; } = 120;

        [JsonProperty("message")]
        public string Message { get; set; } = "Keep doing stuff!";
    }
}
