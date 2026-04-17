// Copyright (c) 2026 Bilal Raza
// Publisher: RAZ Studio
// Product: Nyxara AI Studio

using System;
using System.Threading.Tasks;
using Nyxara.AICompanion.Core;
using UnityEngine;
#if NYXARA_WHISPER
using Whisper;
#endif

namespace Nyxara.AICompanion.Speech
{
    public class WhisperMicrophoneInput : MonoBehaviour
    {
        private const string MissingWhisperMessage = "Nyxara AI Studio: Whisper not installed. Speech-to-text features disabled.";
        private const string MissingWhisperReferenceMessage = "Nyxara AI Studio: Whisper is enabled but no WhisperManager is assigned. Speech-to-text features disabled.";

        [SerializeField] private MonoBehaviour whisperManager;
        [SerializeField] private NyxaraCompanionBrain companionBrain;
        [SerializeField] private string microphoneDevice = string.Empty;
        [SerializeField] private int recordingLengthSeconds = 5;
        [SerializeField] private int sampleRate = 16000;
        [SerializeField] private bool autoReplyAfterTranscription = true;

        private AudioClip _recordingClip;
        private bool _hasLoggedMissingDependency;
        private bool _hasLoggedMissingReference;

#pragma warning disable CS0067
        public event Action<string> TranscriptReady;
#pragma warning restore CS0067

        public bool IsRecording { get; private set; }
        public string LastTranscript { get; private set; }
        public NyxaraCompanionBrain CompanionBrain => companionBrain;
        public bool IsWhisperAvailable => TryGetWhisperManager(out _);
        public bool HasAssignedWhisperManager => whisperManager != null;

        public string ConfiguredModelPath
        {
            get
            {
#if NYXARA_WHISPER
                return TryGetWhisperManager(out var manager) ? manager.ModelPath : string.Empty;
#else
                return string.Empty;
#endif
            }
        }

        public void StartRecording()
        {
            if (IsRecording)
            {
                return;
            }

            if (!CanUseWhisper())
            {
                return;
            }

            _recordingClip = Microphone.Start(microphoneDevice, false, recordingLengthSeconds, sampleRate);
            IsRecording = _recordingClip != null;

            if (!IsRecording)
            {
                Debug.LogWarning("Nyxara AI Studio: Microphone recording could not be started.");
            }
        }

#if NYXARA_WHISPER
        public async Task<string> StopRecordingAndTranscribeAsync()
#else
        public Task<string> StopRecordingAndTranscribeAsync()
#endif
        {
#if NYXARA_WHISPER
            if (!IsRecording || _recordingClip == null)
            {
                return string.Empty;
            }

            if (!CanUseWhisper())
            {
                StopMicrophoneRecording();
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
            var manager = GetWhisperManagerOrNull();
            if (manager == null)
            {
                return string.Empty;
            }

            var result = await manager.GetTextAsync(clip);
            var transcript = result?.Result?.Trim() ?? string.Empty;

            LastTranscript = transcript;
            TranscriptReady?.Invoke(transcript);

            if (autoReplyAfterTranscription && companionBrain != null && !string.IsNullOrWhiteSpace(transcript))
            {
                await companionBrain.ReplyToAsync(transcript);
            }

            return transcript;
#else
            if (!IsRecording || _recordingClip == null)
            {
                return Task.FromResult(string.Empty);
            }

            if (!CanUseWhisper())
            {
                StopMicrophoneRecording();
                return Task.FromResult(string.Empty);
            }

            var samplePosition = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);
            IsRecording = false;

            if (samplePosition <= 0)
            {
                return Task.FromResult(string.Empty);
            }

            _ = autoReplyAfterTranscription;
            return Task.FromResult(string.Empty);
#endif
        }

        private void Awake()
        {
            ValidateSetup();
        }

        private void OnEnable()
        {
            ValidateSetup();
        }

        private bool CanUseWhisper()
        {
            ValidateSetup();
            return TryGetWhisperManager(out _);
        }

        private void ValidateSetup()
        {
#if NYXARA_WHISPER
            if (whisperManager == null && !_hasLoggedMissingReference)
            {
                _hasLoggedMissingReference = true;
                Debug.LogWarning(MissingWhisperReferenceMessage, this);
            }
#else
            if (!_hasLoggedMissingDependency)
            {
                _hasLoggedMissingDependency = true;
                Debug.LogWarning(MissingWhisperMessage, this);
            }
#endif
        }

        private void StopMicrophoneRecording()
        {
            if (Microphone.IsRecording(microphoneDevice))
            {
                Microphone.End(microphoneDevice);
            }

            IsRecording = false;
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
#if NYXARA_WHISPER
            whisperManager = FindFirstObjectByType<WhisperManager>();
#else
            whisperManager = null;
#endif
            companionBrain = FindFirstObjectByType<NyxaraCompanionBrain>();
        }

#if NYXARA_WHISPER
        private WhisperManager GetWhisperManagerOrNull()
        {
            if (TryGetWhisperManager(out var manager))
            {
                return manager;
            }

            return null;
        }

        private bool TryGetWhisperManager(out WhisperManager manager)
        {
            manager = whisperManager as WhisperManager;
            if (manager != null)
            {
                return true;
            }

            if (!_hasLoggedMissingReference)
            {
                _hasLoggedMissingReference = true;
                Debug.LogWarning(MissingWhisperReferenceMessage, this);
            }

            return false;
        }
#else
        private bool TryGetWhisperManager(out object manager)
        {
            manager = null;
            return false;
        }
#endif
    }
}
