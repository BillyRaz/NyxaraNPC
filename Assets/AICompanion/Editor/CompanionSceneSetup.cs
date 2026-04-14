#if UNITY_EDITOR
using LLMUnity;
using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.Core;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.Speech;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Whisper;

namespace Nyxara.AICompanion.Editor
{
    public static class CompanionSceneSetup
    {
        [MenuItem("Nyxara AI/Legacy/Create Bootstrap Objects")]
        public static void CreateBootstrapObjects()
        {
            var root = FindOrCreate("Nyxara Companion");

            var llmObject = FindOrCreate("Local LLM", root.transform);
            var llm = llmObject.GetComponent<LLM>() ?? llmObject.AddComponent<LLM>();
            llm.model = ResolvePreferredModelPath();
            llm.contextSize = 8192;
            llm.numThreads = -1;
            llm.numGPULayers = 0;

            var agentObject = FindOrCreate("Companion Agent", root.transform);
            var agent = agentObject.GetComponent<LLMAgent>() ?? agentObject.AddComponent<LLMAgent>();
            agent.llm = llm;
            agent.systemPrompt = CompanionStackDefaults.DefaultSystemPrompt;
            agent.temperature = 0.7f;
            agent.topP = 0.9f;

            var ttsObject = FindOrCreate("Speech Synthesis", root.transform);
            var audioSource = ttsObject.GetComponent<AudioSource>() ?? ttsObject.AddComponent<AudioSource>();
            var tts = ttsObject.GetComponent<PiperTtsService>() ?? ttsObject.AddComponent<PiperTtsService>();
            tts.PiperExecutablePath = ResolvePreferredPiperExecutablePath();
            tts.VoiceModelPath = ResolvePreferredPiperVoicePath();

            var faceObject = FindOrCreate("Face Driver", root.transform);
            var faceDriver = faceObject.GetComponent<ArkItBlendshapeDriver>() ?? faceObject.AddComponent<ArkItBlendshapeDriver>();

            var whisperObject = FindOrCreate("Speech To Text", root.transform);
            var whisper = whisperObject.GetComponent<WhisperManager>() ?? whisperObject.AddComponent<WhisperManager>();
            whisper.ModelPath = CompanionStackDefaults.WhisperModelRelativePath;
            whisper.IsModelPathInStreamingAssets = true;

            var micInput = whisperObject.GetComponent<WhisperMicrophoneInput>() ?? whisperObject.AddComponent<WhisperMicrophoneInput>();
            var brain = agentObject.GetComponent<NyxaraCompanionBrain>() ?? agentObject.AddComponent<NyxaraCompanionBrain>();

            AssignObjectReference(tts, "audioSource", audioSource);
            AssignObjectReference(tts, "faceDriver", faceDriver);
            AssignObjectReference(brain, "agent", agent);
            AssignObjectReference(brain, "ttsService", tts);
            AssignObjectReference(brain, "faceDriver", faceDriver);
            AssignObjectReference(micInput, "whisperManager", whisper);
            AssignObjectReference(micInput, "companionBrain", brain);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static GameObject FindOrCreate(string name, Transform parent = null)
        {
            GameObject existing = null;
            if (parent == null)
            {
                existing = GameObject.Find(name);
            }
            else
            {
                var child = parent.Find(name);
                existing = child != null ? child.gameObject : null;
            }

            if (existing != null)
            {
                return existing;
            }

            var created = new GameObject(name);
            if (parent != null)
            {
                created.transform.SetParent(parent);
            }

            return created;
        }

        private static void AssignObjectReference(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string ResolvePreferredModelPath()
        {
            var localModelPath = Path.Combine(Application.dataPath, "StreamingAssets", "Models", CompanionStackDefaults.QwenModelFileName);
            if (File.Exists(localModelPath))
            {
                return localModelPath;
            }

            return CompanionStackDefaults.QwenModelPath;
        }

        private static string ResolvePreferredPiperExecutablePath()
        {
            if (File.Exists(CompanionStackDefaults.PiperExecutablePath))
            {
                return CompanionStackDefaults.PiperExecutablePath;
            }

            return string.Empty;
        }

        private static string ResolvePreferredPiperVoicePath()
        {
            var localVoicePath = Path.Combine(Application.dataPath, "StreamingAssets", "Speech", "PiperVoices", CompanionStackDefaults.PiperVoiceFileName);
            if (File.Exists(localVoicePath))
            {
                return localVoicePath;
            }

            return string.Empty;
        }
    }
}
#endif
