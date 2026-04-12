namespace Nyxara.AICompanion.Configuration
{
    public static class CompanionStackDefaults
    {
        public const string QwenModelFileName = "Qwen2.5-7B-Instruct-1M-Q4_K_M.gguf";
        public const string QwenModelPath = @"D:\Raz\Lm AI\lmstudio-community\Qwen2.5-7B-Instruct-1M-GGUF\Qwen2.5-7B-Instruct-1M-Q4_K_M.gguf";
        public const string WhisperModelRelativePath = "Speech/ggml-tiny.bin";
        public const string PiperOutputFileName = "nyxara_tts.wav";
        public const string PiperExecutablePath = @"C:\Users\Connect2Aryans\Downloads\piper_windows_amd64\piper\piper.exe";
        public const string PiperVoiceFileName = "en_US-amy-medium.onnx";
        public const string DefaultSystemPrompt =
            "You are Nyxara, an emotionally aware AI companion inside a Unity experience. " +
            "Reply naturally, stay concise unless the player asks for depth, and maintain a warm, grounded tone.";
    }
}
