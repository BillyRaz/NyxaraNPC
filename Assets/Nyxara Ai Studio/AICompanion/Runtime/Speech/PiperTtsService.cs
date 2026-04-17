// Copyright (c) 2026 Bilal Raza
// Publisher: RAZ Studio
// Product: Nyxara AI Studio

using System.Collections;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.LipSync;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Nyxara.AICompanion.Speech
{
    public enum PiperTtsAvailabilityStatus
    {
        Ready,
        NotConfigured,
        InvalidPath,
        Disabled
    }

    public class PiperTtsService : MonoBehaviour
    {
        [SerializeField] private bool ttsEnabled;
        [SerializeField, HideInInspector] private bool hasMigratedLegacyConfiguration;
        [SerializeField] private string piperExecutablePath = string.Empty;
        [SerializeField] private string voiceModelPath = string.Empty;
        [SerializeField] private string outputFileName = CompanionStackDefaults.PiperOutputFileName;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private ArkItBlendshapeDriver faceDriver;
        [SerializeField] private VisemeLipSyncController lipSyncController;
        [SerializeField] private bool useLipSync = true;
        private bool _hasLoggedUnavailable;
        private bool _hasLoggedFailure;

        public bool TtsEnabled
        {
            get => ttsEnabled;
            set
            {
                ttsEnabled = value;
                ResetAvailabilityLogging();
            }
        }

        public bool IsConfigured => GetAvailabilityStatus() == PiperTtsAvailabilityStatus.Ready;
        public PiperTtsAvailabilityStatus AvailabilityStatus => GetAvailabilityStatus();
        public AudioSource AudioSource => audioSource;
        public ArkItBlendshapeDriver FaceDriver => faceDriver;
        public VisemeLipSyncController LipSyncController => lipSyncController;

        public string PiperExecutablePath
        {
            get => piperExecutablePath;
            set
            {
                piperExecutablePath = value;
                ResetAvailabilityLogging();
            }
        }

        public string VoiceModelPath
        {
            get => voiceModelPath;
            set
            {
                voiceModelPath = value;
                ResetAvailabilityLogging();
            }
        }

        public void SetLipSyncController(VisemeLipSyncController controller)
        {
            lipSyncController = controller;
        }

        public PiperTtsAvailabilityStatus GetAvailabilityStatus()
        {
            return EvaluateAvailabilityStatus(ttsEnabled, piperExecutablePath, voiceModelPath);
        }

        public async Task<AudioClip> SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var availabilityStatus = GetAvailabilityStatus();
            if (availabilityStatus != PiperTtsAvailabilityStatus.Ready)
            {
                LogUnavailableOnce(availabilityStatus);
                return null;
            }

            var outputPath = Path.Combine(Application.persistentDataPath, outputFileName);
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                await RunPiperAsync(text, outputPath);

                if (!File.Exists(outputPath))
                {
                    LogFailureOnce("Piper finished without producing an audio file. Voice output will be skipped.");
                    return null;
                }

                var clip = await LoadClipAsync(outputPath);
                if (clip != null && audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();

                    if (useLipSync && lipSyncController != null)
                    {
                        _ = lipSyncController.SpeakWithLipSync(text, clip.length);
                    }

                    if (faceDriver != null)
                    {
                        faceDriver.SetSpeaking(true);
                        StartCoroutine(faceDriver.ReleaseSpeakingWhenSourceStops(audioSource));
                    }

                    if (useLipSync && lipSyncController != null)
                    {
                        StartCoroutine(StopLipSyncAfterAudio());
                    }
                }

                return clip;
            }
            catch (Exception ex)
            {
                LogFailureOnce($"Piper voice output is unavailable: {ex.Message}");
                return null;
            }
        }

        private async Task RunPiperAsync(string text, string outputPath)
        {
            var resolvedExecutablePath = ResolveConfiguredPath(piperExecutablePath);
            var resolvedVoiceModelPath = ResolveConfiguredPath(voiceModelPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedExecutablePath,
                Arguments = $"--model \"{resolvedVoiceModelPath}\" --output_file \"{outputPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            await process.StandardInput.WriteAsync(text);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            await WaitForExitAsync(process);

            var stdErr = await stdErrTask;
            if (process.ExitCode != 0)
            {
                var stdOut = await stdOutTask;
                throw new InvalidOperationException(
                    $"Piper exited with code {process.ExitCode}.{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}");
            }
        }

        private static Task WaitForExitAsync(Process process)
        {
            if (process.HasExited)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();

            void ProcessExited(object sender, EventArgs args)
            {
                process.Exited -= ProcessExited;
                tcs.TrySetResult(true);
            }

            process.EnableRaisingEvents = true;
            process.Exited += ProcessExited;

            if (process.HasExited)
            {
                process.Exited -= ProcessExited;
                return Task.CompletedTask;
            }

            return tcs.Task;
        }

        private IEnumerator StopLipSyncAfterAudio()
        {
            if (audioSource == null)
            {
                yield break;
            }

            while (audioSource.isPlaying)
            {
                yield return null;
            }

            lipSyncController?.StopLipSync();
        }

        private static async Task<AudioClip> LoadClipAsync(string outputPath)
        {
            var uri = new Uri(outputPath).AbsoluteUri;
            using var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Nyxara TTS] Failed to load generated audio clip: {request.error}");
                return null;
            }

            return DownloadHandlerAudioClip.GetContent(request);
        }

        private void Awake()
        {
            UpgradeLegacyConfigurationIfNeeded();
        }

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            faceDriver = FindFirstObjectByType<ArkItBlendshapeDriver>();
            lipSyncController = FindFirstObjectByType<VisemeLipSyncController>();
        }

        private void OnValidate()
        {
            UpgradeLegacyConfigurationIfNeeded();
        }

        private void UpgradeLegacyConfigurationIfNeeded()
        {
            if (hasMigratedLegacyConfiguration)
            {
                return;
            }

            if (!ttsEnabled &&
                !string.IsNullOrWhiteSpace(piperExecutablePath) &&
                !string.IsNullOrWhiteSpace(voiceModelPath))
            {
                ttsEnabled = true;
            }

            hasMigratedLegacyConfiguration = true;
        }

        private void ResetAvailabilityLogging()
        {
            _hasLoggedUnavailable = false;
            _hasLoggedFailure = false;
        }

        private void LogUnavailableOnce(PiperTtsAvailabilityStatus status)
        {
            if (_hasLoggedUnavailable || status == PiperTtsAvailabilityStatus.Disabled)
            {
                return;
            }

            _hasLoggedUnavailable = true;
            var message = status switch
            {
                PiperTtsAvailabilityStatus.NotConfigured => "[Nyxara TTS] Voice output is optional and not configured. Text replies will continue without audio.",
                PiperTtsAvailabilityStatus.InvalidPath => "[Nyxara TTS] Voice output is optional but the Piper paths are invalid. Text replies will continue without audio.",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (status == PiperTtsAvailabilityStatus.InvalidPath)
            {
                Debug.LogWarning(message);
                return;
            }

            Debug.Log(message);
        }

        private void LogFailureOnce(string message)
        {
            if (_hasLoggedFailure)
            {
                return;
            }

            _hasLoggedFailure = true;
            Debug.LogWarning($"[Nyxara TTS] {message}");
        }

        public static PiperTtsAvailabilityStatus EvaluateAvailabilityStatus(bool isEnabled, string configuredExecutablePath, string configuredVoiceModelPath)
        {
            if (!isEnabled)
            {
                return PiperTtsAvailabilityStatus.Disabled;
            }

            if (string.IsNullOrWhiteSpace(configuredExecutablePath) || string.IsNullOrWhiteSpace(configuredVoiceModelPath))
            {
                return PiperTtsAvailabilityStatus.NotConfigured;
            }

            var resolvedExecutablePath = ResolveConfiguredPath(configuredExecutablePath);
            var resolvedVoiceModelPath = ResolveConfiguredPath(configuredVoiceModelPath);
            if (string.IsNullOrWhiteSpace(resolvedExecutablePath) ||
                string.IsNullOrWhiteSpace(resolvedVoiceModelPath) ||
                !File.Exists(resolvedExecutablePath) ||
                !File.Exists(resolvedVoiceModelPath))
            {
                return PiperTtsAvailabilityStatus.InvalidPath;
            }

            if (!string.Equals(Path.GetExtension(resolvedVoiceModelPath), ".onnx", StringComparison.OrdinalIgnoreCase))
            {
                return PiperTtsAvailabilityStatus.InvalidPath;
            }

            return PiperTtsAvailabilityStatus.Ready;
        }

        public static string GetStatusLabel(PiperTtsAvailabilityStatus status)
        {
            return status switch
            {
                PiperTtsAvailabilityStatus.Ready => "Ready",
                PiperTtsAvailabilityStatus.NotConfigured => "Not Configured",
                PiperTtsAvailabilityStatus.InvalidPath => "Invalid Path",
                PiperTtsAvailabilityStatus.Disabled => "Disabled",
                _ => "Unknown"
            };
        }

        public static string GetStatusGuidance(PiperTtsAvailabilityStatus status)
        {
            return status switch
            {
                PiperTtsAvailabilityStatus.Ready => "Voice output is ready.",
                PiperTtsAvailabilityStatus.NotConfigured => "Voice output is optional. Enable it and choose a Piper executable plus voice model when you want spoken replies.",
                PiperTtsAvailabilityStatus.InvalidPath => "Voice output is optional. Review the Piper executable and voice model paths in Voice Output (Optional).",
                PiperTtsAvailabilityStatus.Disabled => "Voice output is currently disabled. Enable it if you want spoken replies.",
                _ => string.Empty
            };
        }

        public static string ResolveConfiguredPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            var normalized = configuredPath.Replace('\\', '/').TrimStart('/');
            const string assetsStreamingAssetsPrefix = "Assets/StreamingAssets/";
            if (normalized.StartsWith(assetsStreamingAssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(assetsStreamingAssetsPrefix.Length);
            }
            else if (normalized.StartsWith("StreamingAssets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("StreamingAssets/".Length);
            }

            var streamingAssetsCandidate = Path.Combine(Application.streamingAssetsPath, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(streamingAssetsCandidate))
            {
                return streamingAssetsCandidate;
            }

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Application.dataPath, normalized.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            }

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
