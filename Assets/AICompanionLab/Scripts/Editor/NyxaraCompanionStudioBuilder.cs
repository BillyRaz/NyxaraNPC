#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using LLMUnity;
using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.Core;
using Nyxara.AICompanion.Data;
using Nyxara.AICompanion.Expressions;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.LipSync;
using Nyxara.AICompanion.Runtime;
using Nyxara.AICompanion.Speech;
using Nyxara.AICompanion.Studio;
using Nyxara.AICompanion.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Whisper;

namespace Nyxara.AICompanion.Editor
{
    public static class NyxaraCompanionStudioBuilder
    {
        public static void EnsureFolderStructure(AICompanionStudioConfig config)
        {
            EnsureFolder("Assets", "AICompanionStudio");
            EnsureFolder(config.rootFolder, "Prefabs");
            EnsureFolder(config.rootFolder, "Companions");
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
            RemoveExistingStudioRoot(config.studioRootName);

            var root = CreateRootFromSource(config, out var characterRoot, out var characterInstance);
            if (root == null)
            {
                return null;
            }

            var systemsRoot = GetOrCreateChild(root.transform, "AISystems");

            var llmObject = GetOrCreateChild(systemsRoot.transform, "Local LLM");
            var llm = GetOrAddComponent<LLM>(llmObject);
            llm.model = ResolveModelPathForLlm(config);
            llm.contextSize = 8192;
            llm.numThreads = -1;
            llm.numGPULayers = 0;

            var sttObject = GetOrCreateChild(systemsRoot.transform, "Speech To Text");
            var whisperManager = GetOrAddComponent<WhisperManager>(sttObject);
            whisperManager.ModelPath = config.whisperModelRelativePath;
            whisperManager.IsModelPathInStreamingAssets = true;

            var speechObject = GetOrCreateChild(systemsRoot.transform, "Speech Synthesis");
            var audioSource = GetOrAddComponent<AudioSource>(speechObject);
            var tts = GetOrAddComponent<PiperTtsService>(speechObject);
            tts.PiperExecutablePath = ResolveAbsoluteOrProjectPath(config.piperExecutablePath);
            tts.VoiceModelPath = ResolveAbsoluteOrProjectPath(config.piperVoicePath);
            AssignObjectReference(tts, "audioSource", audioSource);

            var agent = GetOrAddComponent<LLMAgent>(root);
            agent.llm = llm;

            var brain = GetOrAddComponent<NyxaraCompanionBrain>(root);
            var faceDriver = GetOrAddComponent<ArkItBlendshapeDriver>(root);
            var signalRouter = GetOrAddComponent<ExpressionSignalRouter>(root);
            var expressionLibrary = GetOrAddComponent<ExpressionLibraryManager>(root);
            var memoryController = GetOrAddComponent<RecentMemoryController>(root);
            var actionGatekeeper = GetOrAddComponent<ActionGatekeeper>(root);
            var actionExecutor = GetOrAddComponent<CompanionActionExecutor>(root);
            var lipSyncController = GetOrAddComponent<VisemeLipSyncController>(root);
            var phonemeExtractor = GetOrAddComponent<PiperTTSPhonemeExtractor>(root);
            var microphoneInput = GetOrAddComponent<WhisperMicrophoneInput>(sttObject);

            if (config.autoAttachBootstrap)
            {
                GetOrAddComponent<CompanionBootstrap>(root);
            }

            var faceRenderer = ResolveFaceRenderer(config, characterInstance);
            if (config.createStudioEnvironment)
            {
                CreateStudioEnvironment(config, root, characterRoot.transform, faceRenderer);
            }

            ConfigureRootSystems(config, root, characterInstance, profile);

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

            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            return null;
        }

        public static GameObject FinalizeCompanionRoot(AICompanionStudioConfig config, GameObject root)
        {
            if (config == null || root == null)
            {
                return null;
            }

            EnsureFolderStructure(config);
            EnsureCharacterProfile(config);
            var characterRoot = FindCharacterRoot(root.transform);
            var characterInstance = config.sourceIsExistingRootPrefab
                ? characterRoot?.gameObject
                : (characterRoot != null && characterRoot.childCount > 0 ? characterRoot.GetChild(0).gameObject : null);
            ConfigureRootSystems(config, root, characterInstance, config.characterProfile);

            var prefabPath = $"{config.companionPrefabFolder}/{config.characterName}_CompanionRoot.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return prefab;
        }

        public static void ApplyStudioRigToExistingRoot(AICompanionStudioConfig config, GameObject root)
        {
            if (config == null || root == null)
            {
                return;
            }

            var characterRoot = FindCharacterRoot(root.transform);
            if (characterRoot == null)
            {
                return;
            }

            Transform characterInstance = config.sourceIsExistingRootPrefab ? characterRoot : (characterRoot.childCount > 0 ? characterRoot.GetChild(0) : null);
            if (characterInstance != null)
            {
                characterInstance.localPosition = config.characterLocalPosition;
            }

            var studioRig = root.transform.Find("StudioRig");
            if (studioRig != null)
            {
                Undo.DestroyObjectImmediate(studioRig.gameObject);
            }

            var faceRenderer = ResolveFaceRenderer(config, characterInstance != null ? characterInstance.gameObject : null);
            if (config.createStudioEnvironment)
            {
                CreateStudioEnvironment(config, root, characterRoot, faceRenderer);
            }

            ConfigureRootSystems(config, root, characterInstance != null ? characterInstance.gameObject : null, config.characterProfile);
        }

        public static void ResetStudio(AICompanionStudioConfig config)
        {
            if (config == null)
            {
                return;
            }

            RemoveExistingStudioRoot(config.studioRootName);

            var prefabPath = $"{config.prefabFolder}/{config.characterName}_StudioRoot.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            var companionPrefabPath = $"{config.companionPrefabFolder}/{config.characterName}_CompanionRoot.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(companionPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(companionPrefabPath);
            }

            AssetDatabase.Refresh();
        }

        public static void FaceCharacterTowardStudioCamera(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var characterRoot = FindCharacterRoot(root.transform);
            if (characterRoot == null)
            {
                return;
            }

            var cameraTransform = root.transform.Find("StudioRig/StudioCamera");
            if (cameraTransform == null)
            {
                return;
            }

            var character = characterRoot.childCount > 0 ? characterRoot.GetChild(0) : characterRoot;
            var toCamera = cameraTransform.position - character.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Undo.RecordObject(character, "Face Character Toward Studio Camera");
            character.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private static SkinnedMeshRenderer ResolveFaceRenderer(AICompanionStudioConfig config, GameObject characterInstance)
        {
            if (characterInstance != null && !string.IsNullOrWhiteSpace(config.preferredFaceRendererPath))
            {
                var preferred = characterInstance.transform.Find(config.preferredFaceRendererPath);
                if (preferred != null)
                {
                    return preferred.GetComponent<SkinnedMeshRenderer>();
                }
            }

            if (characterInstance == null)
            {
                return null;
            }

            return characterInstance.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private static List<SkinnedMeshRenderer> CollectAdditionalFaceRenderers(GameObject characterInstance, SkinnedMeshRenderer primaryRenderer)
        {
            var results = new List<SkinnedMeshRenderer>();
            if (characterInstance == null)
            {
                return results;
            }

            var renderers = characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var primaryBlendshapes = new HashSet<string>();
            if (primaryRenderer != null && primaryRenderer.sharedMesh != null)
            {
                for (var i = 0; i < primaryRenderer.sharedMesh.blendShapeCount; i++)
                {
                    primaryBlendshapes.Add(primaryRenderer.sharedMesh.GetBlendShapeName(i));
                }
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer == primaryRenderer || renderer.sharedMesh == null)
                {
                    continue;
                }

                if (LooksLikeRelatedFaceRenderer(renderer, primaryBlendshapes))
                {
                    results.Add(renderer);
                }
            }

            return results;
        }

        private static bool LooksLikeRelatedFaceRenderer(SkinnedMeshRenderer renderer, HashSet<string> primaryBlendshapes)
        {
            var transformName = renderer.transform.name.ToLowerInvariant();
            var meshName = (renderer.sharedMesh != null ? renderer.sharedMesh.name : string.Empty).ToLowerInvariant();
            var likelyFaceName =
                transformName.Contains("lash") ||
                transformName.Contains("eye") ||
                transformName.Contains("brow") ||
                transformName.Contains("mouth") ||
                transformName.Contains("teeth") ||
                meshName.Contains("lash") ||
                meshName.Contains("eye") ||
                meshName.Contains("brow") ||
                meshName.Contains("mouth") ||
                meshName.Contains("teeth");

            var hasBlendshapeOverlap = false;
            if (renderer.sharedMesh != null)
            {
                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var blendshapeName = renderer.sharedMesh.GetBlendShapeName(i);
                    if (primaryBlendshapes.Contains(blendshapeName))
                    {
                        hasBlendshapeOverlap = true;
                        break;
                    }
                }
            }

            return likelyFaceName || hasBlendshapeOverlap;
        }

        private static void ConfigureRootSystems(AICompanionStudioConfig config, GameObject root, GameObject characterInstance, CharacterProfileData profile)
        {
            if (config == null || root == null)
            {
                return;
            }

            var faceRenderer = ResolveFaceRenderer(config, characterInstance);
            var additionalFaceRenderers = CollectAdditionalFaceRenderers(characterInstance, faceRenderer);

            var tts = root.GetComponentInChildren<PiperTtsService>(true);
            var audioSource = tts != null ? GetObjectReference<AudioSource>(tts, "audioSource") : root.GetComponentInChildren<AudioSource>(true);
            var faceDriver = root.GetComponent<ArkItBlendshapeDriver>();
            var signalRouter = root.GetComponent<ExpressionSignalRouter>();
            var expressionLibrary = root.GetComponent<ExpressionLibraryManager>();
            var lipSyncController = root.GetComponent<VisemeLipSyncController>();
            var phonemeExtractor = root.GetComponent<PiperTTSPhonemeExtractor>();
            var runtimeOverlay = root.GetComponent<RuntimeConversationOverlay>() ?? GetOrAddComponent<RuntimeConversationOverlay>(root);
            var brain = root.GetComponent<NyxaraCompanionBrain>();
            var memoryController = root.GetComponent<RecentMemoryController>();
            var actionGatekeeper = root.GetComponent<ActionGatekeeper>();
            var actionExecutor = root.GetComponent<CompanionActionExecutor>();
            var agent = root.GetComponent<LLMAgent>();
            var whisperInput = root.GetComponentInChildren<WhisperMicrophoneInput>(true);
            var whisperManager = root.GetComponentInChildren<WhisperManager>(true);
            var lipSyncData = EnsureLipSyncData(config);

            if (faceDriver != null)
            {
                AssignObjectReference(faceDriver, "targetRenderer", faceRenderer);
                AssignObjectReferenceList(faceDriver, "additionalRenderers", additionalFaceRenderers);
            }

            if (signalRouter != null)
            {
                AssignObjectReference(signalRouter, "targetRenderer", faceRenderer);
            }

            if (expressionLibrary != null)
            {
                AssignObjectReference(expressionLibrary, "targetFaceRenderer", faceRenderer);
                AssignObjectReferenceList(expressionLibrary, "additionalFaceRenderers", additionalFaceRenderers);
                AssignStringField(expressionLibrary, "expressionLibraryPath", config.expressionFolder);
            }

            if (lipSyncController != null)
            {
                AssignObjectReference(lipSyncController, "faceRenderer", faceRenderer);
                AssignObjectReferenceList(lipSyncController, "additionalFaceRenderers", additionalFaceRenderers);
                AssignObjectReference(lipSyncController, "lipSyncData", lipSyncData);
                AssignObjectReference(lipSyncController, "phonemeExtractor", phonemeExtractor);
                AssignObjectReference(lipSyncController, "audioSource", audioSource);
                AssignFloatField(lipSyncController, "mouthOpenAmount", 0.45f);
                AssignFloatField(lipSyncController, "visemeIntensityScale", 0.6f);
                AssignFloatField(lipSyncController, "lowerLipDropAmount", 0.18f);
                AssignFloatField(lipSyncController, "upperLipRaiseAmount", 0.08f);
                AssignFloatField(lipSyncController, "mouthStretchAmount", 0.06f);
                AssignFloatField(lipSyncController, "releaseDuration", 0.08f);
            }

            if (phonemeExtractor != null)
            {
                AssignStringField(phonemeExtractor, "piperExecutablePath", ResolveAbsoluteOrProjectPath(config.piperExecutablePath));
                AssignStringField(phonemeExtractor, "voiceModelPath", ResolveAbsoluteOrProjectPath(config.piperVoicePath));
            }

            if (tts != null)
            {
                AssignObjectReference(tts, "faceDriver", faceDriver);
                AssignObjectReference(tts, "lipSyncController", lipSyncController);
                if (audioSource != null)
                {
                    AssignObjectReference(tts, "audioSource", audioSource);
                }
            }

            if (actionGatekeeper != null)
            {
                AssignObjectReference(actionGatekeeper, "actionExecutor", actionExecutor);
            }

            if (actionExecutor != null)
            {
                AssignObjectReference(actionExecutor, "companionTransform", root.transform);
                AssignObjectReference(actionExecutor, "playerTransform", config.playerTransform);
            }

            if (brain != null)
            {
                AssignObjectReference(brain, "agent", agent);
                AssignObjectReference(brain, "ttsService", tts);
                AssignObjectReference(brain, "faceDriver", faceDriver);
                AssignObjectReference(brain, "signalRouter", signalRouter);
                AssignObjectReference(brain, "memoryController", memoryController);
                AssignObjectReference(brain, "actionGatekeeper", actionGatekeeper);
                AssignObjectReference(brain, "characterProfile", profile);
            }

            if (whisperInput != null)
            {
                AssignObjectReference(whisperInput, "whisperManager", whisperManager);
                AssignObjectReference(whisperInput, "companionBrain", brain);
            }

            if (runtimeOverlay != null)
            {
                AssignObjectReference(runtimeOverlay, "whisperInput", whisperInput);
                AssignObjectReference(runtimeOverlay, "companionBrain", brain);
                AssignBoolField(runtimeOverlay, "showOverlay", config.enableRuntimeConversationOverlay && config.showRuntimeConversationOverlay);
                AssignBoolField(runtimeOverlay, "enabled", config.enableRuntimeConversationOverlay);
                AssignEnumField(runtimeOverlay, "micHoldKey", config.runtimeMicHoldKey);
                AssignEnumField(runtimeOverlay, "promptPopupKey", config.runtimePromptPopupKey);
            }

            if (lipSyncData != null)
            {
                AssignFloatField(lipSyncData, "smoothTime", 0.08f);
                AssignFloatField(lipSyncData, "jawOpenMultiplier", 0.6f);
            }

            var studioListener = root.GetComponentInChildren<AudioListener>(true);
            if (studioListener != null)
            {
                EnsureSingleAudioListener(studioListener);
            }
        }

        private static LipSyncData EnsureLipSyncData(AICompanionStudioConfig config)
        {
            if (config == null)
            {
                return null;
            }

            var assetPath = $"{config.generatedFolder}/{config.characterName}_LipSyncData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LipSyncData>(assetPath);
            if (existing != null)
            {
                NormalizeLipSyncMappings(existing);
                return existing;
            }

            var data = ScriptableObject.CreateInstance<LipSyncData>();
            data.visemeMappings = new List<VisemeMapping>
            {
                new() { viseme = Viseme.AA, blendshapeName = "jawOpen", intensity = 27.9f, jawOpenContribution = 1f },
                new() { viseme = Viseme.IY, blendshapeName = "mouthSmileLeft, mouthSmileRight", intensity = 40f, jawOpenContribution = 0.15f },
                new() { viseme = Viseme.UH, blendshapeName = "mouthPucker", intensity = 80f, jawOpenContribution = 0.2f },
                new() { viseme = Viseme.OW, blendshapeName = "mouthFunnel", intensity = 59.4f, jawOpenContribution = 0.3f },
                new() { viseme = Viseme.EH, blendshapeName = "mouthStretchLeft, mouthStretchRight", intensity = 35f, jawOpenContribution = 0.15f },
                new() { viseme = Viseme.FV, blendshapeName = "mouthPressLeft, mouthPressRight", intensity = 30f, jawOpenContribution = 0.05f },
                new() { viseme = Viseme.M, blendshapeName = "mouthClose", intensity = 36.7f, jawOpenContribution = 0f },
                new() { viseme = Viseme.sil, blendshapeName = "mouthClose", intensity = 0f, jawOpenContribution = 0f }
            };
            NormalizeLipSyncMappings(data);

            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void NormalizeLipSyncMappings(LipSyncData data)
        {
            if (data == null || data.visemeMappings == null)
            {
                return;
            }

            foreach (var mapping in data.visemeMappings)
            {
                if (mapping == null)
                {
                    continue;
                }

                mapping.blendshapeName = mapping.viseme switch
                {
                    Viseme.IY when string.Equals(mapping.blendshapeName, "mouthSmileLeft") => "mouthSmileLeft, mouthSmileRight",
                    Viseme.EH when string.Equals(mapping.blendshapeName, "mouthStretchLeft") => "mouthStretchLeft, mouthStretchRight",
                    Viseme.FV when string.Equals(mapping.blendshapeName, "mouthPressLeft") => "mouthPressLeft, mouthPressRight",
                    _ => mapping.blendshapeName
                };

                switch (mapping.viseme)
                {
                    case Viseme.AA:
                        mapping.intensity = 27.9f;
                        break;
                    case Viseme.IY:
                        mapping.intensity = 40f;
                        break;
                    case Viseme.UH:
                        mapping.intensity = 80f;
                        break;
                    case Viseme.OW:
                        mapping.intensity = 59.4f;
                        break;
                    case Viseme.EH:
                        mapping.intensity = 35f;
                        break;
                    case Viseme.M:
                        mapping.intensity = 36.7f;
                        break;
                }
            }

            EditorUtility.SetDirty(data);
        }

        private static GameObject CreateRootFromSource(AICompanionStudioConfig config, out Transform characterRoot, out GameObject characterInstance)
        {
            characterRoot = null;
            characterInstance = null;

            if (config.sourceIsExistingRootPrefab && config.sourceCharacterPrefab != null)
            {
                var instantiatedRoot = PrefabUtility.InstantiatePrefab(config.sourceCharacterPrefab) as GameObject;
                if (instantiatedRoot == null)
                {
                    instantiatedRoot = UnityEngine.Object.Instantiate(config.sourceCharacterPrefab);
                }

                Undo.RegisterCreatedObjectUndo(instantiatedRoot, "Create AI Companion Studio Root");
                instantiatedRoot.name = config.studioRootName;

                characterRoot = FindCharacterRoot(instantiatedRoot.transform);
                characterInstance = characterRoot.gameObject;
                ApplyCharacterTransform(config, characterRoot);
                return instantiatedRoot;
            }

            var root = new GameObject(config.studioRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create AI Companion Studio Root");

            var characterRootObject = CreateUndoGameObject("CharacterRoot", root.transform);
            characterRoot = characterRootObject.transform;

            if (config.sourceCharacterPrefab != null)
            {
                characterInstance = PrefabUtility.InstantiatePrefab(config.sourceCharacterPrefab) as GameObject;
                if (characterInstance == null)
                {
                    characterInstance = UnityEngine.Object.Instantiate(config.sourceCharacterPrefab);
                }

                Undo.RegisterCreatedObjectUndo(characterInstance, "Instantiate Source Character");
                characterInstance.name = config.sourceCharacterPrefab.name;
                Undo.SetTransformParent(characterInstance.transform, characterRoot, "Parent Source Character");
                ApplyCharacterTransform(config, characterInstance.transform);
            }

            return root;
        }

        private static Transform FindCharacterRoot(Transform root)
        {
            var namedRoot = root.Find("CharacterRoot");
            if (namedRoot != null)
            {
                return namedRoot;
            }

            return root;
        }

        private static void ApplyCharacterTransform(AICompanionStudioConfig config, Transform target)
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = config.characterLocalPosition;
            target.localEulerAngles = config.characterLocalEuler;
        }

        private static void CreateStudioEnvironment(AICompanionStudioConfig config, GameObject root, Transform characterRoot, SkinnedMeshRenderer faceRenderer)
        {
            var studioRig = CreateUndoGameObject("StudioRig", root.transform);

            var focusTarget = CreateUndoGameObject("FocusTarget", studioRig.transform);
            focusTarget.transform.position = GetFocusPosition(config, faceRenderer, characterRoot);

            if (config.createStudioCamera)
            {
                CreateStudioCamera(config, studioRig.transform, focusTarget.transform);
            }

            if (config.createStudioLights)
            {
                CreateStudioLights(config, studioRig.transform, focusTarget.transform);
            }
        }

        private static Vector3 GetFocusPosition(AICompanionStudioConfig config, SkinnedMeshRenderer faceRenderer, Transform characterRoot)
        {
            if (faceRenderer != null)
            {
                var bounds = faceRenderer.bounds;
                return bounds.center + Vector3.up * 0.02f + config.cameraPivotOffset;
            }

            return characterRoot.position + Vector3.up * config.focusHeightOffset + config.cameraPivotOffset;
        }

        private static void CreateStudioCamera(AICompanionStudioConfig config, Transform studioRig, Transform focusTarget)
        {
            var cameraObject = CreateUndoGameObject("StudioCamera", studioRig);

            var direction = Quaternion.Euler(0f, config.cameraYaw, 0f) * Vector3.back;
            cameraObject.transform.position = focusTarget.position + (direction * config.cameraDistance) + (Vector3.up * (config.cameraHeight - focusTarget.position.y));
            cameraObject.transform.LookAt(focusTarget.position);

            var camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = Undo.AddComponent<Camera>(cameraObject);
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = config.studioBackgroundColor;
            camera.fieldOfView = config.cameraFieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.depthTextureMode = DepthTextureMode.Depth;

            var listener = cameraObject.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = Undo.AddComponent<AudioListener>(cameraObject);
            }

            listener.enabled = true;
            EnsureSingleAudioListener(listener);

            var additionalCameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
            if (additionalCameraData == null)
            {
                additionalCameraData = Undo.AddComponent<UniversalAdditionalCameraData>(cameraObject);
            }

            additionalCameraData.renderPostProcessing = true;
            additionalCameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            additionalCameraData.antialiasingQuality = AntialiasingQuality.High;
            additionalCameraData.stopNaN = true;
            additionalCameraData.dithering = true;
        }

        private static void EnsureSingleAudioListener(AudioListener preferredListener)
        {
            if (preferredListener == null)
            {
                return;
            }

            preferredListener.enabled = true;
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (listener == null || listener == preferredListener)
                {
                    continue;
                }

                if (!listener.enabled)
                {
                    continue;
                }

                Undo.RecordObject(listener, "Disable Extra Audio Listener");
                listener.enabled = false;
                EditorUtility.SetDirty(listener);
            }

            EditorUtility.SetDirty(preferredListener);
        }

        private static void CreateStudioLights(AICompanionStudioConfig config, Transform studioRig, Transform focusTarget)
        {
            CreatePortraitLight("KeyLight", studioRig, focusTarget.position, new Vector3(-32f, 28f, 0f), config.keyLightIntensity, new Color(1f, 0.96f, 0.9f));
            CreatePortraitLight("FillLight", studioRig, focusTarget.position, new Vector3(24f, -18f, 0f), config.fillLightIntensity, new Color(0.78f, 0.86f, 1f));
            CreatePortraitLight("RimLight", studioRig, focusTarget.position, new Vector3(145f, 18f, 0f), config.rimLightIntensity, new Color(1f, 0.98f, 0.95f));
        }

        private static void CreatePortraitLight(string name, Transform parent, Vector3 focusPosition, Vector3 eulerAngles, float intensity, Color color)
        {
            var lightObject = CreateUndoGameObject(name, parent);
            lightObject.transform.position = focusPosition + (Quaternion.Euler(eulerAngles) * new Vector3(0f, 0f, -2.5f));
            lightObject.transform.LookAt(focusPosition);

            var additionalLightData = lightObject.GetComponent<UniversalAdditionalLightData>();
            if (additionalLightData == null)
            {
                additionalLightData = Undo.AddComponent<UniversalAdditionalLightData>(lightObject);
            }

            var light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = Undo.AddComponent<Light>(lightObject);
            }

            if (light == null)
            {
                return;
            }

            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = 8f;
            light.spotAngle = 52f;
            light.innerSpotAngle = 35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.92f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.2f;
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

        private static GameObject CreateUndoGameObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            if (parent != null)
            {
                Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            return go;
        }

        private static GameObject GetOrCreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            return CreateUndoGameObject(childName, parent);
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            return Undo.AddComponent<T>(target);
        }

        private static string ResolveModelPathForLlm(AICompanionStudioConfig config)
        {
            var streamingAssetsModel = Path.Combine(Application.streamingAssetsPath, "Models", CompanionStackDefaults.QwenModelFileName);
            if (File.Exists(streamingAssetsModel))
            {
                return Path.Combine("Models", CompanionStackDefaults.QwenModelFileName).Replace('\\', '/');
            }

            return ResolveAbsoluteOrProjectPath(config.llmModelPath);
        }

        private static void RemoveExistingStudioRoot(string rootName)
        {
            if (string.IsNullOrWhiteSpace(rootName))
            {
                return;
            }

            var existing = GameObject.Find(rootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }
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

        private static void AssignObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
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

        private static T GetObjectReference<T>(UnityEngine.Object source, string fieldName) where T : UnityEngine.Object
        {
            if (source == null)
            {
                return null;
            }

            var serializedObject = new SerializedObject(source);
            var property = serializedObject.FindProperty(fieldName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static void AssignObjectReferenceList(UnityEngine.Object target, string fieldName, IReadOnlyList<SkinnedMeshRenderer> values)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                return;
            }

            property.arraySize = values?.Count ?? 0;
            if (values != null)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignStringField(UnityEngine.Object target, string fieldName, string value)
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

        private static void AssignFloatField(UnityEngine.Object target, string fieldName, float value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBoolField(UnityEngine.Object target, string fieldName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignEnumField(UnityEngine.Object target, string fieldName, Enum value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.intValue = Convert.ToInt32(value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
