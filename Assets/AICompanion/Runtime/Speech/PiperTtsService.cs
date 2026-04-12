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
    public class PiperTtsService : MonoBehaviour
    {
        [SerializeField] private string piperExecutablePath = string.Empty;
        [SerializeField] private string voiceModelPath = string.Empty;
        [SerializeField] private string outputFileName = CompanionStackDefaults.PiperOutputFileName;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private ArkItBlendshapeDriver faceDriver;
        [SerializeField] private VisemeLipSyncController lipSyncController;
        [SerializeField] private bool useLipSync = true;

        public bool IsConfigured =>
            File.Exists(piperExecutablePath) &&
            File.Exists(voiceModelPath);
        public AudioSource AudioSource => audioSource;
        public ArkItBlendshapeDriver FaceDriver => faceDriver;
        public VisemeLipSyncController LipSyncController => lipSyncController;

        public string PiperExecutablePath
        {
            get => piperExecutablePath;
            set => piperExecutablePath = value;
        }

        public string VoiceModelPath
        {
            get => voiceModelPath;
            set => voiceModelPath = value;
        }

        public async Task<AudioClip> SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (!IsConfigured)
            {
                Debug.LogWarning("PiperTtsService is not configured yet. Set the Piper executable and voice model paths.");
                return null;
            }

            var outputPath = Path.Combine(Application.persistentDataPath, outputFileName);

            if (useLipSync && lipSyncController != null)
            {
                _ = lipSyncController.SpeakWithLipSync(text);
            }

            await RunPiperAsync(text, outputPath);

            var clip = await LoadClipAsync(outputPath);
            if (clip != null && audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
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

        private async Task RunPiperAsync(string text, string outputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = piperExecutablePath,
                Arguments = $"--model \"{voiceModelPath}\" --output_file \"{outputPath}\"",
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
                Debug.LogError($"Failed to load Piper output wav: {request.error}");
                return null;
            }

            return DownloadHandlerAudioClip.GetContent(request);
        }

        private void Reset()
        {
            audioSource = GetComponent<AudioSource>();
            faceDriver = FindFirstObjectByType<ArkItBlendshapeDriver>();
            lipSyncController = FindFirstObjectByType<VisemeLipSyncController>();
        }
    }
}
