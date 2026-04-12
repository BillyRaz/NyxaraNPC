#if UNITY_EDITOR
using System.IO;
using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.Studio;
using UnityEditor;
using UnityEngine;

namespace Nyxara.AICompanion.Editor
{
    public class NyxaraCompanionStudioWindow : EditorWindow
    {
        private const string DefaultConfigPath = "Assets/AICompanionStudio/Generated/AICompanionStudioConfig.asset";

        private Vector2 _scrollPosition;
        private AICompanionStudioConfig _config;

        [MenuItem("Nyxara/AI Companion/Studio")]
        public static void ShowWindow()
        {
            var window = GetWindow<NyxaraCompanionStudioWindow>("Nyxara Studio");
            window.minSize = new Vector2(520f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            _config = LoadOrCreateConfig();
            ApplyDefaultPathsIfEmpty(_config);
        }

        private void OnGUI()
        {
            _config = (AICompanionStudioConfig)EditorGUILayout.ObjectField("Studio Config", _config, typeof(AICompanionStudioConfig), false);
            if (_config == null)
            {
                if (GUILayout.Button("Create Studio Config"))
                {
                    _config = LoadOrCreateConfig();
                }

                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space(8f);

            DrawOverviewSection();
            EditorGUILayout.Space(8f);
            DrawSourceSection();
            EditorGUILayout.Space(8f);
            DrawPathSection();
            EditorGUILayout.Space(8f);
            DrawOptionsSection();
            EditorGUILayout.Space(8f);
            DrawBuildSection();

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_config);
            }
        }

        private void DrawOverviewSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Studio Overview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select your character mesh/prefab, tune the generated paths, then use Build Studio to create the full companion root/prefab structure.", MessageType.Info);
            EditorGUILayout.LabelField("Root folder", _config.rootFolder);
            EditorGUILayout.LabelField("Prefab output", _config.prefabFolder);
            EditorGUILayout.LabelField("Profile asset", _config.characterProfile != null ? AssetDatabase.GetAssetPath(_config.characterProfile) : "Will auto-create if enabled");
            EditorGUILayout.EndVertical();
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Character Source", EditorStyles.boldLabel);
            _config.characterName = EditorGUILayout.TextField("Character Name", _config.characterName);
            _config.studioRootName = EditorGUILayout.TextField("Studio Root Name", _config.studioRootName);
            _config.sourceCharacterPrefab = (GameObject)EditorGUILayout.ObjectField("Source Character", _config.sourceCharacterPrefab, typeof(GameObject), false);
            _config.preferredFaceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Preferred Face Renderer", _config.preferredFaceRenderer, typeof(SkinnedMeshRenderer), true);
            _config.playerTransform = (Transform)EditorGUILayout.ObjectField("Player Transform", _config.playerTransform, typeof(Transform), true);
            _config.characterProfile = (Nyxara.AICompanion.Data.CharacterProfileData)EditorGUILayout.ObjectField("Character Profile", _config.characterProfile, typeof(Nyxara.AICompanion.Data.CharacterProfileData), false);
            EditorGUILayout.EndVertical();
        }

        private void DrawPathSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Stack Paths", EditorStyles.boldLabel);
            _config.llmModelPath = EditorGUILayout.TextField("LLM Model Path", _config.llmModelPath);
            _config.whisperModelRelativePath = EditorGUILayout.TextField("Whisper Model", _config.whisperModelRelativePath);
            _config.piperExecutablePath = EditorGUILayout.TextField("Piper Executable", _config.piperExecutablePath);
            _config.piperVoicePath = EditorGUILayout.TextField("Piper Voice", _config.piperVoicePath);

            EditorGUILayout.Space(6f);
            DrawPathStatus("LLM", _config.llmModelPath, allowRelative: false);
            DrawPathStatus("Piper EXE", _config.piperExecutablePath, allowRelative: false);
            DrawPathStatus("Piper Voice", _config.piperVoicePath, allowRelative: false);
            DrawPathStatus("Whisper", _config.whisperModelRelativePath, allowRelative: true);
            EditorGUILayout.EndVertical();
        }

        private void DrawOptionsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Build Options", EditorStyles.boldLabel);
            _config.createSceneInstance = EditorGUILayout.Toggle("Create Scene Instance", _config.createSceneInstance);
            _config.saveRootPrefab = EditorGUILayout.Toggle("Save Root Prefab", _config.saveRootPrefab);
            _config.createProfileIfMissing = EditorGUILayout.Toggle("Create Profile If Missing", _config.createProfileIfMissing);
            _config.autoAttachBootstrap = EditorGUILayout.Toggle("Auto Attach Bootstrap", _config.autoAttachBootstrap);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Folders", EditorStyles.miniBoldLabel);
            _config.rootFolder = EditorGUILayout.TextField("Root Folder", _config.rootFolder);
            _config.prefabFolder = EditorGUILayout.TextField("Prefab Folder", _config.prefabFolder);
            _config.profileFolder = EditorGUILayout.TextField("Profile Folder", _config.profileFolder);
            _config.generatedFolder = EditorGUILayout.TextField("Generated Folder", _config.generatedFolder);
            _config.expressionFolder = EditorGUILayout.TextField("Expression Folder", _config.expressionFolder);
            EditorGUILayout.EndVertical();
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

            GUI.enabled = _config != null;
            if (GUILayout.Button("Ensure Structure", GUILayout.Height(30f)))
            {
                NyxaraCompanionStudioBuilder.EnsureFolderStructure(_config);
                if (_config.createProfileIfMissing)
                {
                    NyxaraCompanionStudioBuilder.EnsureCharacterProfile(_config);
                }
            }

            GUI.enabled = _config != null && _config.sourceCharacterPrefab != null;
            if (GUILayout.Button("Build Studio", GUILayout.Height(36f)))
            {
                NyxaraCompanionStudioBuilder.BuildStudioRoot(_config);
            }

            GUI.enabled = _config != null;
            if (GUILayout.Button("Ping Generated Folder"))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(_config.generatedFolder);
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                    Selection.activeObject = folder;
                }
            }

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
        }

        private static void DrawPathStatus(string label, string path, bool allowRelative)
        {
            var exists = false;
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (allowRelative && !Path.IsPathRooted(path))
                {
                    var absolute = Path.Combine(Application.dataPath, path.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar));
                    exists = File.Exists(absolute);
                }
                else
                {
                    exists = File.Exists(path);
                }
            }

            EditorGUILayout.LabelField($"{label} Status", exists ? "Found" : "Missing");
        }

        private static AICompanionStudioConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AICompanionStudioConfig>(DefaultConfigPath);
            if (config != null)
            {
                return config;
            }

            NyxaraCompanionStudioBuilder.EnsureFolderStructure(CreateTemporaryConfig());
            config = ScriptableObject.CreateInstance<AICompanionStudioConfig>();
            ApplyDefaultPathsIfEmpty(config);
            AssetDatabase.CreateAsset(config, DefaultConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static AICompanionStudioConfig CreateTemporaryConfig()
        {
            var config = ScriptableObject.CreateInstance<AICompanionStudioConfig>();
            ApplyDefaultPathsIfEmpty(config);
            return config;
        }

        private static void ApplyDefaultPathsIfEmpty(AICompanionStudioConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.llmModelPath))
            {
                config.llmModelPath = CompanionStackDefaults.QwenModelPath;
            }

            if (string.IsNullOrWhiteSpace(config.whisperModelRelativePath))
            {
                config.whisperModelRelativePath = CompanionStackDefaults.WhisperModelRelativePath;
            }

            if (string.IsNullOrWhiteSpace(config.piperExecutablePath))
            {
                config.piperExecutablePath = CompanionStackDefaults.PiperExecutablePath;
            }

            if (string.IsNullOrWhiteSpace(config.piperVoicePath))
            {
                config.piperVoicePath = Path.Combine(Application.dataPath, "StreamingAssets", "Speech", "PiperVoices", CompanionStackDefaults.PiperVoiceFileName);
            }
        }
    }
}
#endif
