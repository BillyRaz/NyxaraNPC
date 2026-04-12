#if UNITY_EDITOR
using System.IO;
using LLMUnity;
using Nyxara.AICompanion.Core;
using Nyxara.AICompanion.Data;
using Nyxara.AICompanion.Expressions;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.LipSync;
using Nyxara.AICompanion.Runtime;
using Nyxara.AICompanion.Speech;
using Nyxara.AICompanion.Studio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Whisper;

namespace Nyxara.AICompanion.Editor
{
    public static class NyxaraCompanionStudioBuilder
    {
        public static void EnsureFolderStructure(AICompanionStudioConfig config)
        {
            EnsureFolder("Assets", "AICompanionStudio");
            EnsureFolder(config.rootFolder, "Prefabs");
            EnsureFolder(config.rootFolder, "Profiles");
            EnsureFolder(config.rootFolder, "Generated");
            EnsureFolder(config.rootFolder, "Expressions");
            AssetDatabase.Refresh();
        }

        public static CharacterProfileData EnsureCharacterProfile(AICompanionStudioConfig config)
        {
            if (config.characterProfile != null)
            {
                return config.characterProfile;
            }

            if (!config.createProfileIfMissing)
            {
                return null;
            }

            var assetPath = $"{config.profileFolder}/{config.characterName}Profile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfileData>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CharacterProfileData>();
                profile.characterName = config.characterName;
                AssetDatabase.CreateAsset(profile, assetPath);
                AssetDatabase.SaveAssets();
            }

            config.characterProfile = profile;
            EditorUtility.SetDirty(config);
            return profile;
        }

        public static GameObject BuildStudioRoot(AICompanionStudioConfig config)
        {
            EnsureFolderStructure(config);
            var profile = EnsureCharacterProfile(config);

            var root = new GameObject(config.studioRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create AI Companion Studio Root");

            var characterRoot = new GameObject("CharacterRoot");
            characterRoot.transform.SetParent(root.transform, false);

            GameObject characterInstance = null;
            if (config.sourceCharacterPrefab != null)
            {
                characterInstance = PrefabUtility.InstantiatePrefab(config.sourceCharacterPrefab) as GameObject;
                if (characterInstance == null)
                {
                    characterInstance = Object.Instantiate(config.sourceCharacterPrefab);
                }

                characterInstance.name = config.sourceCharacterPrefab.name;
                characterInstance.transform.SetParent(characterRoot.transform, false);
            }

            var systemsRoot = new GameObject("AISystems");
            systemsRoot.transform.SetParent(root.transform, false);

            var llmObject = new GameObject("Local LLM");
            llmObject.transform.SetParent(systemsRoot.transform, false);
            var llm = llmObject.AddComponent<LLM>();
            llm.model = ResolveAbsoluteOrProjectPath(config.llmModelPath);
            llm.contextSize = 8192;
            llm.numThreads = -1;
            llm.numGPULayers = 0;

            var sttObject = new GameObject("Speech To Text");
            sttObject.transform.SetParent(systemsRoot.transform, false);
            var whisperManager = sttObject.AddComponent<WhisperManager>();
            whisperManager.ModelPath = config.whisperModelRelativePath;
            whisperManager.IsModelPathInStreamingAssets = true;

            var speechObject = new GameObject("Speech Synthesis");
            speechObject.transform.SetParent(systemsRoot.transform, false);
            var audioSource = speechObject.AddComponent<AudioSource>();
            var tts = speechObject.AddComponent<PiperTtsService>();
            tts.PiperExecutablePath = ResolveAbsoluteOrProjectPath(config.piperExecutablePath);
            tts.VoiceModelPath = ResolveAbsoluteOrProjectPath(config.piperVoicePath);
            AssignObjectReference(tts, "audioSource", audioSource);

            var agent = root.AddComponent<LLMAgent>();
            agent.llm = llm;

            var brain = root.AddComponent<NyxaraCompanionBrain>();
            var faceDriver = root.AddComponent<ArkItBlendshapeDriver>();
            var signalRouter = root.AddComponent<ExpressionSignalRouter>();
            var expressionLibrary = root.AddComponent<ExpressionLibraryManager>();
            var memoryController = root.AddComponent<RecentMemoryController>();
            var actionGatekeeper = root.AddComponent<ActionGatekeeper>();
            var actionExecutor = root.AddComponent<CompanionActionExecutor>();
            var lipSyncController = root.AddComponent<VisemeLipSyncController>();
            var phonemeExtractor = root.AddComponent<PiperTTSPhonemeExtractor>();
            var microphoneInput = sttObject.AddComponent<WhisperMicrophoneInput>();

            if (config.autoAttachBootstrap)
            {
                root.AddComponent<CompanionBootstrap>();
            }

            var faceRenderer = ResolveFaceRenderer(config, characterInstance);
            AssignObjectReference(faceDriver, "targetRenderer", faceRenderer);
            AssignObjectReference(tts, "faceDriver", faceDriver);
            AssignObjectReference(tts, "lipSyncController", lipSyncController);
            AssignObjectReference(actionGatekeeper, "actionExecutor", actionExecutor);
            AssignObjectReference(actionExecutor, "companionTransform", root.transform);
            AssignObjectReference(actionExecutor, "playerTransform", config.playerTransform);
            AssignObjectReference(expressionLibrary, "targetFaceRenderer", faceRenderer);
            AssignStringField(expressionLibrary, "expressionLibraryPath", config.expressionFolder);
            AssignObjectReference(lipSyncController, "faceRenderer", faceRenderer);
            AssignObjectReference(lipSyncController, "phonemeExtractor", phonemeExtractor);
            AssignObjectReference(lipSyncController, "audioSource", audioSource);
            AssignStringField(phonemeExtractor, "piperExecutablePath", ResolveAbsoluteOrProjectPath(config.piperExecutablePath));
            AssignStringField(phonemeExtractor, "voiceModelPath", ResolveAbsoluteOrProjectPath(config.piperVoicePath));

            AssignObjectReference(brain, "agent", agent);
            AssignObjectReference(brain, "ttsService", tts);
            AssignObjectReference(brain, "faceDriver", faceDriver);
            AssignObjectReference(brain, "signalRouter", signalRouter);
            AssignObjectReference(brain, "memoryController", memoryController);
            AssignObjectReference(brain, "actionGatekeeper", actionGatekeeper);
            AssignObjectReference(brain, "characterProfile", profile);

            AssignObjectReference(microphoneInput, "whisperManager", whisperManager);
            AssignObjectReference(microphoneInput, "companionBrain", brain);

            if (config.saveRootPrefab)
            {
                var prefabPath = $"{config.prefabFolder}/{config.characterName}_StudioRoot.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }

            if (config.createSceneInstance)
            {
                Selection.activeGameObject = root;
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                return root;
            }

            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            return null;
        }

        private static SkinnedMeshRenderer ResolveFaceRenderer(AICompanionStudioConfig config, GameObject characterInstance)
        {
            if (config.preferredFaceRenderer != null)
            {
                return config.preferredFaceRenderer;
            }

            if (characterInstance == null)
            {
                return null;
            }

            return characterInstance.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private static string ResolveAbsoluteOrProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(Application.dataPath, path.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureFolder(string parent, string child)
        {
            var combined = $"{parent}/{child}";
            if (AssetDatabase.IsValidFolder(combined))
            {
                return;
            }

            AssetDatabase.CreateFolder(parent, child);
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

        private static void AssignStringField(Object target, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
