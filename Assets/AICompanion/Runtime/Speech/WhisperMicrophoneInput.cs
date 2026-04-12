using System;
using System.Threading.Tasks;
using Nyxara.AICompanion.Core;
using UnityEngine;
using Whisper;

namespace Nyxara.AICompanion.Speech
{
    public class WhisperMicrophoneInput : MonoBehaviour
    {
        [SerializeField] private WhisperManager whisperManager;
        [SerializeField] private NyxaraCompanionBrain companionBrain;
        [SerializeField] private string microphoneDevice = string.Empty;
        [SerializeField] private int recordingLengthSeconds = 5;
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private bool autoReplyAfterTranscription = true;

        private AudioClip _recordingClip;

        public event Action<string> TranscriptReady;

        public bool IsRecording { get; private set; }
        public string LastTranscript { get; private set; }
        public WhisperManager WhisperManager => whisperManager;
        public NyxaraCompanionBrain CompanionBrain => companionBrain;

        public void StartRecording()
        {
            if (IsRecording)
            {
                return;
            }

            _recordingClip = Microphone.Start(microphoneDevice, false, recordingLengthSeconds, sampleRate);
            IsRecording = _recordingClip != null;
        }

        public async Task<string> StopRecordingAndTranscribeAsync()
        {
            if (!IsRecording || _recordingClip == null)
            {
                return string.Empty;
            }

            if (whisperManager == null)
            {
                Debug.LogError("WhisperMicrophoneInput requires a WhisperManager reference.");
                return string.Empty;
            }

            var samplePosition = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);
            IsRecording = false;

            if (samplePosition <= 0)
            {
                return string.Empty;
            }

            var clip = TrimClip(_recordingClip, samplePosition);
            var result = await whisperManager.GetTextAsync(clip);
            var transcript = result?.Result?.Trim() ?? string.Empty;

            LastTranscript = transcript;
            TranscriptReady?.Invoke(transcript);

            if (autoReplyAfterTranscription && companionBrain != null && !string.IsNullOrWhiteSpace(transcript))
            {
                await companionBrain.ReplyToAsync(transcript);
            }

            return transcript;
        }

        private static AudioClip TrimClip(AudioClip clip, int sampleLength)
        {
            var channels = clip.channels;
            var samples = new float[sampleLength * channels];
            clip.GetData(samples, 0);

            var trimmed = AudioClip.Create($"{clip.name}_trimmed", sampleLength, channels, clip.frequency, false);
            trimmed.SetData(samples, 0);
            return trimmed;
        }

        private void Reset()
        {
            whisperManager = FindFirstObjectByType<WhisperManager>();
            companionBrain = FindFirstObjectByType<NyxaraCompanionBrain>();
        }
    }
}
