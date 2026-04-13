#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LLMUnity;
using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.Core;
using Nyxara.AICompanion.Diagnostics;
using Nyxara.AICompanion.Expressions;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.LipSync;
using Nyxara.AICompanion.Speech;
using Nyxara.AICompanion.Studio;
using UnityEditor;
using UnityEngine;

namespace Nyxara.AICompanion.Editor
{
    public class NyxaraCompanionStudioWindow : EditorWindow
    {
        private const string DefaultConfigPath = "Assets/AICompanionStudio/Generated/AICompanionStudioConfig.asset";

        private enum StudioTab
        {
            Studio,
            Expression,
            LipSync,
            Diagnostics
        }

        private enum DiagnosticsTab
        {
            SystemScan,
            Runtime,
            Logs
        }

        private struct RendererEntry
        {
            public string Path;
            public string Label;
        }

        private struct LogEntry
        {
            public string Timestamp;
            public string Message;
            public LogType Type;
        }

        private Vector2 _scrollPosition;
        private AICompanionStudioConfig _config;
        private StudioTab _currentTab;
        private DiagnosticsTab _diagnosticsTab;
        private RuntimeDiagnosticsMonitor _runtimeMonitor;
        private SystemDiagnosticsReport _lastScanReport;
        private readonly List<LogEntry> _logEntries = new();
        private string _logFilter = string.Empty;
        private bool _showErrors = true;
        private bool _showWarnings = true;
        private bool _showInfo = true;
        private double _lastRuntimeRepaint;
        private SkinnedMeshRenderer _expressionRenderer;
        private LipSyncData _lipSyncData;
        private ExpressionPreset _selectedExpressionPreset;
        private bool _expressionModeEnabled;
        private string _builderPresetName = "New ARKit Expression";
        private string _builderDescription = string.Empty;
        private ExpressionCategory _builderCategory = ExpressionCategory.Emotion;
        private float _builderTransitionTime = 0.15f;
        private readonly Dictionary<string, string> _builderBlendshapeMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _builderWeights = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _builderBlendshapeOptions = new();

        [MenuItem("Nyxara/AI Companion/Studio")]
        public static void ShowWindow()
        {
            var window = GetWindow<NyxaraCompanionStudioWindow>("Nyxara Studio");
            window.minSize = new Vector2(620f, 760f);
            window.Show();
        }

        private void OnEnable()
        {
            _config = LoadOrCreateConfig();
            ApplyDefaultPathsIfEmpty(_config);
            ResetWindowState();
            Application.logMessageReceived += OnLogMessageReceived;
            EditorApplication.update += OnEditorUpdate;
            FindRuntimeMonitor();
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_currentTab != StudioTab.Diagnostics || _diagnosticsTab != DiagnosticsTab.Runtime || !EditorApplication.isPlaying)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastRuntimeRepaint > 0.5d)
            {
                _lastRuntimeRepaint = EditorApplication.timeSinceStartup;
                FindRuntimeMonitor();
                Repaint();
            }
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            _logEntries.Insert(0, new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Message = condition,
                Type = type
            });

            while (_logEntries.Count > 500)
            {
                _logEntries.RemoveAt(_logEntries.Count - 1);
            }
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

            DrawMainTabs();
            DrawQuickTools();
            EditorGUILayout.Space(8f);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_currentTab)
            {
                case StudioTab.Studio:
                    DrawStudioTab();
                    break;
                case StudioTab.Expression:
                    DrawExpressionTab();
                    break;
                case StudioTab.LipSync:
                    DrawLipSyncTab();
                    break;
                case StudioTab.Diagnostics:
                    DrawDiagnosticsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_config);
            }
        }

        private void DrawMainTabs()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Studio", EditorStyles.miniButtonLeft)) _currentTab = StudioTab.Studio;
            if (GUILayout.Button("Expression", EditorStyles.miniButtonMid)) _currentTab = StudioTab.Expression;
            if (GUILayout.Button("Lip Sync", EditorStyles.miniButtonMid)) _currentTab = StudioTab.LipSync;
            if (GUILayout.Button("Diagnostics", EditorStyles.miniButtonRight)) _currentTab = StudioTab.Diagnostics;
            GUILayout.EndHorizontal();
        }

        private void DrawQuickTools()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Quick Tools", GUILayout.Width(75f));
            if (GUILayout.Button("Studio", EditorStyles.toolbarButton)) _currentTab = StudioTab.Studio;
            if (GUILayout.Button("Expression Window", EditorStyles.toolbarButton)) ExpressionEditorWindow.ShowWindow();
            if (GUILayout.Button("Lip Sync Window", EditorStyles.toolbarButton)) LipSyncEditorWindow.ShowWindow();
            if (GUILayout.Button("Diagnostics View", EditorStyles.toolbarButton)) _currentTab = StudioTab.Diagnostics;
            if (GUILayout.Button("Create Bootstrap", EditorStyles.toolbarButton)) CompanionSceneSetup.CreateBootstrapObjects();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawStudioTab()
        {
            DrawOverviewSection();
            EditorGUILayout.Space(8f);
            DrawSourceSection();
            EditorGUILayout.Space(8f);
            DrawPathSection();
            EditorGUILayout.Space(8f);
            DrawStudioRigSection();
            EditorGUILayout.Space(8f);
            DrawOptionsSection();
            EditorGUILayout.Space(8f);
            DrawBuildSection();
        }

        private void DrawOverviewSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Studio Overview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Build a portrait-style studio scene with camera focus, face lighting, local AI systems, and prefab output from one place.", MessageType.Info);
            EditorGUILayout.LabelField("Root folder", _config.rootFolder);
            EditorGUILayout.LabelField("Prefab output", _config.prefabFolder);
            EditorGUILayout.LabelField("Companion output", _config.companionPrefabFolder);
            EditorGUILayout.LabelField("Source character", _config.sourceCharacterPrefab != null ? _config.sourceCharacterPrefab.name : "Missing");
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
            _config.sourceIsExistingRootPrefab = EditorGUILayout.Toggle("Source Is Existing Root", _config.sourceIsExistingRootPrefab);
            DrawFaceRendererSelector();
            _config.playerTransform = (Transform)EditorGUILayout.ObjectField("Player Transform", _config.playerTransform, typeof(Transform), true);
            _config.characterProfile = (Nyxara.AICompanion.Data.CharacterProfileData)EditorGUILayout.ObjectField("Character Profile", _config.characterProfile, typeof(Nyxara.AICompanion.Data.CharacterProfileData), false);
            EditorGUILayout.EndVertical();
        }

        private void DrawFaceRendererSelector()
        {
            if (_config.sourceCharacterPrefab == null)
            {
                EditorGUILayout.HelpBox("Assign a source character prefab or FBX first, then choose the face renderer from its hierarchy.", MessageType.Info);
                return;
            }

            var renderers = GetRendererEntries(_config.sourceCharacterPrefab);
            if (renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("No SkinnedMeshRenderer was found under the selected source character.", MessageType.Warning);
                return;
            }

            var selectedIndex = Mathf.Max(0, renderers.FindIndex(entry => entry.Path == _config.preferredFaceRendererPath));
            var labels = renderers.Select(entry => entry.Label).ToArray();
            var newIndex = EditorGUILayout.Popup("Preferred Face Renderer", selectedIndex, labels);
            _config.preferredFaceRendererPath = renderers[newIndex].Path;
            EditorGUILayout.LabelField("Renderer Path", _config.preferredFaceRendererPath, EditorStyles.miniLabel);
        }

        private void DrawPathSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("AI Stack Paths", EditorStyles.boldLabel);

            if (GUILayout.Button("Use Project StreamingAssets Model"))
            {
                _config.llmModelPath = Path.Combine("Models", CompanionStackDefaults.QwenModelFileName).Replace('\\', '/');
            }

            _config.llmModelPath = EditorGUILayout.TextField("LLM Model Path", _config.llmModelPath);
            _config.whisperModelRelativePath = EditorGUILayout.TextField("Whisper Model", _config.whisperModelRelativePath);
            _config.piperExecutablePath = EditorGUILayout.TextField("Piper Executable", _config.piperExecutablePath);
            _config.piperVoicePath = EditorGUILayout.TextField("Piper Voice", _config.piperVoicePath);

            EditorGUILayout.Space(6f);
            DrawPathStatus("LLM", ResolveModelStatusPath(_config.llmModelPath), false);
            DrawPathStatus("Piper EXE", _config.piperExecutablePath, false);
            DrawPathStatus("Piper Voice", _config.piperVoicePath, false);
            DrawPathStatus("Whisper", _config.whisperModelRelativePath, true);
            EditorGUILayout.HelpBox("LLMUnity prefers a StreamingAssets-relative model path for build-safe local models. If the model exists in Assets/StreamingAssets/Models, the builder now uses that path automatically.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawStudioRigSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Studio Rig", EditorStyles.boldLabel);
            _config.createStudioEnvironment = EditorGUILayout.Toggle("Create Studio Environment", _config.createStudioEnvironment);
            _config.createStudioCamera = EditorGUILayout.Toggle("Create Studio Camera", _config.createStudioCamera);
            _config.createStudioLights = EditorGUILayout.Toggle("Create Studio Lights", _config.createStudioLights);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Character Transform", EditorStyles.miniBoldLabel);
            _config.characterLocalPosition = EditorGUILayout.Vector3Field("Character Position", _config.characterLocalPosition);
            _config.characterLocalEuler = EditorGUILayout.Vector3Field("Character Rotation", _config.characterLocalEuler);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Camera Framing", EditorStyles.miniBoldLabel);
            _config.focusHeightOffset = EditorGUILayout.FloatField("Focus Height", _config.focusHeightOffset);
            _config.cameraPivotOffset = EditorGUILayout.Vector3Field("Camera Pivot Offset", _config.cameraPivotOffset);
            _config.cameraDistance = EditorGUILayout.Slider("Camera Distance", _config.cameraDistance, 0.6f, 3f);
            _config.cameraHeight = EditorGUILayout.Slider("Camera Height", _config.cameraHeight, 0.8f, 2.4f);
            _config.cameraYaw = EditorGUILayout.Slider("Camera Yaw", _config.cameraYaw, -35f, 35f);
            _config.cameraFieldOfView = EditorGUILayout.Slider("Camera FOV", _config.cameraFieldOfView, 15f, 60f);
            EditorGUILayout.HelpBox("Use Camera Pivot Offset to nudge the look target without changing the character transform. A small negative Y value usually removes extra empty head space.", MessageType.None);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Lighting", EditorStyles.miniBoldLabel);
            _config.keyLightIntensity = EditorGUILayout.Slider("Key Light", _config.keyLightIntensity, 0f, 4f);
            _config.fillLightIntensity = EditorGUILayout.Slider("Fill Light", _config.fillLightIntensity, 0f, 4f);
            _config.rimLightIntensity = EditorGUILayout.Slider("Rim Light", _config.rimLightIntensity, 0f, 4f);
            _config.studioBackgroundColor = EditorGUILayout.ColorField("Background", _config.studioBackgroundColor);
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
            _config.companionPrefabFolder = EditorGUILayout.TextField("Companion Folder", _config.companionPrefabFolder);
            _config.profileFolder = EditorGUILayout.TextField("Profile Folder", _config.profileFolder);
            _config.generatedFolder = EditorGUILayout.TextField("Generated Folder", _config.generatedFolder);
            _config.expressionFolder = EditorGUILayout.TextField("Expression Folder", _config.expressionFolder);
            EditorGUILayout.EndVertical();
        }

        private void DrawBuildSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            if (GUILayout.Button("Ensure Structure", GUILayout.Height(30f)))
            {
                NyxaraCompanionStudioBuilder.EnsureFolderStructure(_config);
                if (_config.createProfileIfMissing)
                {
                    NyxaraCompanionStudioBuilder.EnsureCharacterProfile(_config);
                }
            }

            GUI.enabled = _config.sourceCharacterPrefab != null;
            if (GUILayout.Button("Build Studio", GUILayout.Height(38f)))
            {
                NyxaraCompanionStudioBuilder.BuildStudioRoot(_config);
            }

            GUI.enabled = true;
            var selectedRoot = ResolveStudioRootFromContext();
            var canApplyToScene = selectedRoot != null;
            GUI.enabled = canApplyToScene;
            if (GUILayout.Button("Apply Rig To Selected Studio Root"))
            {
                NyxaraCompanionStudioBuilder.ApplyStudioRigToExistingRoot(_config, selectedRoot);
            }

            if (GUILayout.Button("Face Character To Studio Camera"))
            {
                NyxaraCompanionStudioBuilder.FaceCharacterTowardStudioCamera(selectedRoot);
            }

            if (GUILayout.Button("Finalize Companion Root Prefab", GUILayout.Height(32f)))
            {
                NyxaraCompanionStudioBuilder.FinalizeCompanionRoot(_config, selectedRoot);
            }

            GUI.enabled = true;
            if (GUILayout.Button("Reset Studio", GUILayout.Height(30f)))
            {
                ResetStudioWorkspace();
            }

            if (GUILayout.Button("Run System Scan"))
            {
                PerformSystemScan();
                _currentTab = StudioTab.Diagnostics;
                _diagnosticsTab = DiagnosticsTab.SystemScan;
            }

            EditorGUILayout.HelpBox("Apply Rig now preserves the character's current facing so manual face alignment is not overwritten. Use Face Character To Studio Camera only when you explicitly want the model turned toward the studio camera.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawExpressionTab()
        {
            var studioRoot = ResolveStudioRootFromContext();
            SyncTabContextFromStudioRoot(studioRoot);
            var expressionLibrary = studioRoot != null ? studioRoot.GetComponent<ExpressionLibraryManager>() : null;
            var faceDriver = studioRoot != null ? studioRoot.GetComponent<ArkItBlendshapeDriver>() : null;
            EnsureBuilderState(faceDriver, expressionLibrary);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Expression Tools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Studio Root", studioRoot != null ? studioRoot.name : "Missing");
            var newExpressionRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Face Renderer", _expressionRenderer, typeof(SkinnedMeshRenderer), true);
            if (newExpressionRenderer != _expressionRenderer)
            {
                _expressionRenderer = newExpressionRenderer;
                _builderBlendshapeMap.Clear();
                _builderWeights.Clear();
                RefreshBuilderBlendshapeOptions();
                AutoDetectBuilderBlendshapes();
                PullWeightsFromFaceDriver(faceDriver);
            }
            EditorGUILayout.LabelField("Library Manager", expressionLibrary != null ? expressionLibrary.name : "Missing in scene");
            EditorGUILayout.LabelField("Loaded Presets", expressionLibrary != null ? expressionLibrary.LoadedPresets.Count.ToString() : "0");
            _selectedExpressionPreset = (ExpressionPreset)EditorGUILayout.ObjectField("Selected Preset", _selectedExpressionPreset, typeof(ExpressionPreset), false);
            var newExpressionMode = EditorGUILayout.ToggleLeft("Expression Mode (full face control, including mouth)", _expressionModeEnabled);
            if (newExpressionMode != _expressionModeEnabled)
            {
                _expressionModeEnabled = newExpressionMode;
                ApplyExpressionModeToScene(studioRoot);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = expressionLibrary != null;
            if (GUILayout.Button("Refresh Library"))
            {
                expressionLibrary.LoadAllPresets();
                if (_selectedExpressionPreset == null && expressionLibrary.LoadedPresets.Count > 0)
                {
                    _selectedExpressionPreset = expressionLibrary.LoadedPresets[0];
                }
            }

            GUI.enabled = expressionLibrary != null && _selectedExpressionPreset != null;
            if (GUILayout.Button("Apply Selected Preset"))
            {
                expressionLibrary.ApplyPreset(_selectedExpressionPreset);
            }

            GUI.enabled = expressionLibrary != null;
            if (GUILayout.Button("Reset Face"))
            {
                expressionLibrary.ResetToNeutral();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(_expressionModeEnabled
                ? "Expression Mode is ON. The expression tools own the full face, including mouth blendshapes."
                : "Expression Mode is OFF. Mouth-related blendshapes are reserved for lip sync, while expressions drive the rest of the face.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Detailed Expression Editor"))
            {
                ExpressionEditorWindow.ShowWindow();
            }

            if (GUILayout.Button("Log Face Blendshape Report"))
            {
                LogFaceBlendshapeReport(studioRoot, expressionLibrary);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8f);
            DrawArkitExpressionBuilder(expressionLibrary, faceDriver);
        }

        private void DrawLipSyncTab()
        {
            var studioRoot = ResolveStudioRootFromContext();
            SyncTabContextFromStudioRoot(studioRoot);
            var lipSyncController = studioRoot != null ? studioRoot.GetComponent<VisemeLipSyncController>() : null;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Lip Sync Tools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Studio Root", studioRoot != null ? studioRoot.name : "Missing");
            _expressionRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Face Renderer", _expressionRenderer, typeof(SkinnedMeshRenderer), true);
            _lipSyncData = (LipSyncData)EditorGUILayout.ObjectField("Lip Sync Data", _lipSyncData, typeof(LipSyncData), false);
            EditorGUILayout.LabelField("Lip Sync Controller", lipSyncController != null ? lipSyncController.name : "Missing in scene");
            EditorGUILayout.HelpBox("Use this tab as the central place to check lip-sync assets and scene wiring. The dedicated lip-sync editor is still there for full viseme editing.", MessageType.Info);
            if (GUILayout.Button("Open Detailed Lip Sync Editor"))
            {
                LipSyncEditorWindow.ShowWindow();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawArkitExpressionBuilder(ExpressionLibraryManager expressionLibrary, ArkItBlendshapeDriver faceDriver)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("ARKit Expression Builder", EditorStyles.boldLabel);

            if (_expressionRenderer == null)
            {
                EditorGUILayout.HelpBox("Select or auto-detect a face renderer first so the builder can find ARKit blendshapes.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_builderBlendshapeOptions.Count == 0)
            {
                RefreshBuilderBlendshapeOptions();
            }

            EditorGUILayout.HelpBox("Use ARKit-style sliders to pose the face, name the expression, then click Build Expression to save it directly into the expression library for runtime and AI use.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Detect ARKit"))
            {
                AutoDetectBuilderBlendshapes();
                PullWeightsFromFaceDriver(faceDriver);
                Repaint();
            }

            if (GUILayout.Button("Pull Current Face"))
            {
                PullWeightsFromFaceDriver(faceDriver);
            }

            if (GUILayout.Button("Reset Builder"))
            {
                ResetBuilderWeights();
                expressionLibrary?.ResetToNeutral();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            _builderPresetName = EditorGUILayout.TextField("Expression Name", _builderPresetName);
            _builderCategory = (ExpressionCategory)EditorGUILayout.EnumPopup("Category", _builderCategory);
            _builderDescription = EditorGUILayout.TextField("Description", _builderDescription);
            _builderTransitionTime = EditorGUILayout.Slider("Transition Time", _builderTransitionTime, 0.05f, 1f);

            EditorGUILayout.Space(8f);
            var controls = ExpressionBuilderHelper.GetDefaultControls();
            for (var i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                DrawBuilderControlRow(control, expressionLibrary, faceDriver);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _selectedExpressionPreset != null;
            if (GUILayout.Button("Load Selected Into Builder"))
            {
                LoadPresetIntoBuilder(_selectedExpressionPreset);
            }

            GUI.enabled = expressionLibrary != null && _selectedExpressionPreset != null;
            if (GUILayout.Button("Delete Selected Preset"))
            {
                if (EditorUtility.DisplayDialog("Delete Expression", $"Delete '{_selectedExpressionPreset.displayName}' from the expression library?", "Delete", "Cancel"))
                {
                    if (expressionLibrary.DeletePreset(_selectedExpressionPreset))
                    {
                        _selectedExpressionPreset = expressionLibrary.LoadedPresets.FirstOrDefault();
                    }
                }
            }

            GUI.enabled = expressionLibrary != null;
            if (GUILayout.Button("Build Expression", GUILayout.Height(32f)))
            {
                BuildExpressionPreset(expressionLibrary);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawBuilderControlRow(
            ExpressionBuilderHelper.ControlDefinition control,
            ExpressionLibraryManager expressionLibrary,
            ArkItBlendshapeDriver faceDriver)
        {
            if (!_builderWeights.ContainsKey(control.key))
            {
                _builderWeights[control.key] = 0f;
            }

            if (!_builderBlendshapeMap.ContainsKey(control.key))
            {
                _builderBlendshapeMap[control.key] = string.Empty;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(control.displayName, GUILayout.Width(135f));

            var popupIndex = GetBuilderPopupIndex(_builderBlendshapeMap[control.key]);
            var newIndex = EditorGUILayout.Popup(popupIndex, _builderBlendshapeOptions.ToArray(), GUILayout.Width(220f));
            var selectedBlendshape = newIndex > 0 && newIndex < _builderBlendshapeOptions.Count ? _builderBlendshapeOptions[newIndex] : string.Empty;
            if (!string.Equals(selectedBlendshape, _builderBlendshapeMap[control.key], StringComparison.Ordinal))
            {
                _builderBlendshapeMap[control.key] = selectedBlendshape;
                _builderWeights[control.key] = !string.IsNullOrWhiteSpace(selectedBlendshape) && faceDriver != null
                    ? faceDriver.GetBlendshapeWeight(selectedBlendshape)
                    : 0f;
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_builderBlendshapeMap[control.key])))
            {
                var currentValue = _builderWeights[control.key];
                var newValue = EditorGUILayout.Slider(currentValue, 0f, 100f);
                if (Math.Abs(newValue - currentValue) > 0.01f)
                {
                    _builderWeights[control.key] = newValue;
                    ApplyBuilderPreview(expressionLibrary, faceDriver);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDiagnosticsTab()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("System Scan", EditorStyles.miniButtonLeft)) _diagnosticsTab = DiagnosticsTab.SystemScan;
            if (GUILayout.Button("Runtime", EditorStyles.miniButtonMid)) _diagnosticsTab = DiagnosticsTab.Runtime;
            if (GUILayout.Button("Logs", EditorStyles.miniButtonRight)) _diagnosticsTab = DiagnosticsTab.Logs;
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(8f);

            switch (_diagnosticsTab)
            {
                case DiagnosticsTab.SystemScan:
                    DrawSystemScanTab();
                    break;
                case DiagnosticsTab.Runtime:
                    DrawRuntimeTab();
                    break;
                case DiagnosticsTab.Logs:
                    DrawLogsTab();
                    break;
            }
        }

        private void DrawSystemScanTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("System Scan", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Checks scene components, source asset setup, prefab output, model paths, and missing links before you hit play.", MessageType.Info);
            if (GUILayout.Button("Run Full System Scan", GUILayout.Height(34f)))
            {
                PerformSystemScan();
            }

            if (_lastScanReport != null)
            {
                DrawScanResults(_lastScanReport);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime Diagnostics", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime diagnostics only update during Play Mode.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_runtimeMonitor == null)
            {
                EditorGUILayout.HelpBox("No RuntimeDiagnosticsMonitor found in the active scene.", MessageType.Warning);
                if (GUILayout.Button("Add Runtime Diagnostics Monitor"))
                {
                    var monitorObject = new GameObject("RuntimeDiagnosticsMonitor");
                    _runtimeMonitor = monitorObject.AddComponent<RuntimeDiagnosticsMonitor>();
                    Selection.activeGameObject = monitorObject;
                }

                EditorGUILayout.EndVertical();
                return;
            }

            var report = _runtimeMonitor.CurrentReport;
            if (report != null)
            {
                DrawRuntimeMetrics(report);
                DrawLiveState(report.runtimeSnapshot);
            }
            else
            {
                EditorGUILayout.LabelField("Waiting for runtime data...");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLogsTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Unity Logs", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _logFilter = EditorGUILayout.TextField("Filter", _logFilter);
            _showErrors = EditorGUILayout.ToggleLeft("Errors", _showErrors, GUILayout.Width(70f));
            _showWarnings = EditorGUILayout.ToggleLeft("Warnings", _showWarnings, GUILayout.Width(85f));
            _showInfo = EditorGUILayout.ToggleLeft("Info", _showInfo, GUILayout.Width(60f));
            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
            {
                _logEntries.Clear();
            }

            EditorGUILayout.EndHorizontal();
            foreach (var entry in FilterLogs())
            {
                var originalColor = GUI.color;
                GUI.color = GetLogColor(entry.Type);
                EditorGUILayout.LabelField($"[{entry.Timestamp}] {entry.Type}: {entry.Message}", EditorStyles.wordWrappedLabel);
                GUI.color = originalColor;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawScanResults(SystemDiagnosticsReport report)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Scan completed at {report.timestamp} in {report.durationMs:F0} ms", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(report.IsHealthy ? "Overall Status: Healthy" : "Overall Status: Issues Found", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);
            DrawComponentStatusCard(report.llmStatus);
            DrawComponentStatusCard(report.sttStatus);
            DrawComponentStatusCard(report.ttsStatus);
            DrawComponentStatusCard(report.faceStatus);
            DrawComponentStatusCard(report.lipSyncStatus);
            DrawComponentStatusCard(report.expressionStatus);

            foreach (var issue in report.configIssues)
            {
                var type = issue.severity == IssueSeverity.Critical ? MessageType.Error : MessageType.Warning;
                EditorGUILayout.HelpBox($"{issue.component}: {issue.issue}\nSuggestion: {issue.suggestion}", type);
            }

            foreach (var path in report.pathValidations)
            {
                EditorGUILayout.LabelField($"{path.name}: {(path.exists ? "Found" : "Missing")}  {path.path}", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawRuntimeMetrics(SystemDiagnosticsReport report)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200f));
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Memory: {report.performance.memoryUsageMB} MB");
            EditorGUILayout.LabelField($"CPU: {report.performance.cpuUsagePercent:F1}%");
            EditorGUILayout.LabelField($"Threads: {report.performance.activeThreads}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box", GUILayout.Width(200f));
            EditorGUILayout.LabelField("Latency", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"LLM: {report.performance.averageLLMLatencyMs:F0} ms");
            EditorGUILayout.LabelField($"STT: {report.performance.averageSTTLatencyMs:F0} ms");
            EditorGUILayout.LabelField($"TTS: {report.performance.averageTTSLatencyMs:F0} ms");
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLiveState(RuntimeSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Mood: {snapshot.currentMood}");
            EditorGUILayout.LabelField($"Task: {snapshot.currentTask}");
            EditorGUILayout.LabelField($"Intent: {snapshot.lastIntent}");
            EditorGUILayout.LabelField($"Action: {snapshot.lastAction}");
            EditorGUILayout.LabelField($"Signal: {snapshot.lastSignal}");
            EditorGUILayout.LabelField($"Dialogue: {snapshot.lastDialogue}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"Speaking: {snapshot.isSpeaking}  Thinking: {snapshot.isThinking}");
            EditorGUILayout.LabelField($"Memory Entries: {snapshot.memoryCount}");
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentStatusCard(ComponentStatus status)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(status.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(status.statusMessage ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(status.isOperational ? "Operational" : "Offline", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private List<LogEntry> FilterLogs()
        {
            IEnumerable<LogEntry> filtered = _logEntries;
            if (!string.IsNullOrWhiteSpace(_logFilter))
            {
                filtered = filtered.Where(entry => entry.Message.IndexOf(_logFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            return filtered.Where(entry =>
            {
                var isError = entry.Type == LogType.Error || entry.Type == LogType.Exception || entry.Type == LogType.Assert;
                var isWarning = entry.Type == LogType.Warning;
                var isInfo = !isError && !isWarning;
                return (_showErrors || !isError) && (_showWarnings || !isWarning) && (_showInfo || !isInfo);
            }).ToList();
        }

        private void FindRuntimeMonitor()
        {
            _runtimeMonitor = FindFirstObjectByType<RuntimeDiagnosticsMonitor>();
        }

        private void PerformSystemScan()
        {
            var scanner = new StudioSystemScanner(_config);
            _lastScanReport = scanner.Scan();
            LogSystemScanToConsole(_lastScanReport);
            Repaint();
        }

        private static void LogSystemScanToConsole(SystemDiagnosticsReport report)
        {
            if (report == null)
            {
                return;
            }

            Debug.Log($"[Nyxara Scan] Completed at {report.timestamp} in {report.durationMs:F0} ms. Healthy={report.IsHealthy}");
            LogComponentStatus(report.llmStatus);
            LogComponentStatus(report.sttStatus);
            LogComponentStatus(report.ttsStatus);
            LogComponentStatus(report.faceStatus);
            LogComponentStatus(report.lipSyncStatus);
            LogComponentStatus(report.expressionStatus);

            foreach (var issue in report.configIssues)
            {
                var message = $"[Nyxara Scan][{issue.severity}] {issue.component}: {issue.issue} | Suggestion: {issue.suggestion}";
                if (issue.severity == IssueSeverity.Critical)
                {
                    Debug.LogError(message);
                }
                else if (issue.severity == IssueSeverity.Warning)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }

            foreach (var path in report.pathValidations)
            {
                Debug.Log($"[Nyxara Scan][Path] {path.name}: {(path.exists ? "Found" : "Missing")} | {path.path}");
            }
        }

        private static void LogComponentStatus(ComponentStatus status)
        {
            if (status == null)
            {
                return;
            }

            var message = $"[Nyxara Scan][Component] {status.name}: {(status.isOperational ? "Operational" : "Offline")} | {status.statusMessage}";
            if (!status.isPresent)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static Color GetLogColor(LogType type)
        {
            return type switch
            {
                LogType.Error => new Color(1f, 0.55f, 0.55f),
                LogType.Exception => new Color(1f, 0.55f, 0.55f),
                LogType.Assert => new Color(1f, 0.55f, 0.55f),
                LogType.Warning => new Color(1f, 0.9f, 0.55f),
                _ => Color.white
            };
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

        private static string ResolveModelStatusPath(string configuredPath)
        {
            var localModel = Path.Combine(Application.streamingAssetsPath, "Models", CompanionStackDefaults.QwenModelFileName);
            if (File.Exists(localModel))
            {
                return localModel;
            }

            return configuredPath;
        }

        private void ResetStudioWorkspace()
        {
            if (_config == null)
            {
                return;
            }

            NyxaraCompanionStudioBuilder.ResetStudio(_config);
            ResetStudioRigDefaults(_config);
            ResetWindowState();
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void ResetWindowState()
        {
            _currentTab = StudioTab.Studio;
            _diagnosticsTab = DiagnosticsTab.SystemScan;
            _scrollPosition = Vector2.zero;
            _lastScanReport = null;
            _logFilter = string.Empty;
            _showErrors = true;
            _showWarnings = true;
            _showInfo = true;
            _lastRuntimeRepaint = 0d;
            _expressionRenderer = null;
            _lipSyncData = null;
            _selectedExpressionPreset = null;
            _expressionModeEnabled = false;
            _builderPresetName = "New ARKit Expression";
            _builderDescription = string.Empty;
            _builderCategory = ExpressionCategory.Emotion;
            _builderTransitionTime = 0.15f;
            _builderBlendshapeMap.Clear();
            _builderWeights.Clear();
            _builderBlendshapeOptions.Clear();
            _logEntries.Clear();
        }

        private static void ResetStudioRigDefaults(AICompanionStudioConfig config)
        {
            config.createStudioEnvironment = true;
            config.createStudioCamera = true;
            config.createStudioLights = true;
            config.createSceneInstance = true;
            config.saveRootPrefab = true;
            config.createProfileIfMissing = true;
            config.autoAttachBootstrap = true;

            config.characterLocalPosition = Vector3.zero;
            config.characterLocalEuler = Vector3.zero;
            config.focusHeightOffset = 1.55f;
            config.cameraPivotOffset = Vector3.zero;
            config.cameraDistance = 1.35f;
            config.cameraHeight = 1.6f;
            config.cameraYaw = 0f;
            config.cameraFieldOfView = 26f;

            config.keyLightIntensity = 2.2f;
            config.fillLightIntensity = 0.9f;
            config.rimLightIntensity = 1.35f;
            config.studioBackgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f);
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
                config.llmModelPath = Path.Combine("Models", CompanionStackDefaults.QwenModelFileName).Replace('\\', '/');
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

            if (string.IsNullOrWhiteSpace(config.companionPrefabFolder))
            {
                config.companionPrefabFolder = "Assets/AICompanionStudio/Companions";
            }
        }

        private static List<RendererEntry> GetRendererEntries(GameObject sourcePrefab)
        {
            var entries = new List<RendererEntry>();
            if (sourcePrefab == null)
            {
                return entries;
            }

            foreach (var renderer in sourcePrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                entries.Add(new RendererEntry
                {
                    Path = GetRelativePath(renderer.transform, sourcePrefab.transform),
                    Label = $"{renderer.name} ({renderer.sharedMesh?.name ?? "No Mesh"})"
                });
            }

            return entries;
        }

        private static string GetRelativePath(Transform current, Transform root)
        {
            if (current == null || root == null || current == root)
            {
                return string.Empty;
            }

            var names = new List<string>();
            var walker = current;
            while (walker != null && walker != root)
            {
                names.Add(walker.name);
                walker = walker.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private void EnsureBuilderState(ArkItBlendshapeDriver faceDriver, ExpressionLibraryManager expressionLibrary)
        {
            if (_expressionRenderer == null && faceDriver != null && faceDriver.TargetRenderer != null)
            {
                _expressionRenderer = faceDriver.TargetRenderer;
            }

            RefreshBuilderBlendshapeOptions();
            if (_builderBlendshapeMap.Count == 0)
            {
                AutoDetectBuilderBlendshapes();
            }

            if (_builderWeights.Count == 0)
            {
                PullWeightsFromFaceDriver(faceDriver);
            }

            if (_selectedExpressionPreset == null && expressionLibrary != null && expressionLibrary.LoadedPresets.Count > 0)
            {
                _selectedExpressionPreset = expressionLibrary.LoadedPresets[0];
            }
        }

        private void RefreshBuilderBlendshapeOptions()
        {
            _builderBlendshapeOptions.Clear();
            _builderBlendshapeOptions.Add("<None>");
            _builderBlendshapeOptions.AddRange(GetAvailableBuilderBlendshapeNames());
        }

        private void AutoDetectBuilderBlendshapes()
        {
            var detected = ExpressionBuilderHelper.AutoDetectBlendshapes(GetAvailableBuilderRenderers());
            foreach (var control in ExpressionBuilderHelper.GetDefaultControls())
            {
                _builderBlendshapeMap[control.key] = detected.TryGetValue(control.key, out var blendshapeName) ? blendshapeName : string.Empty;
                if (!_builderWeights.ContainsKey(control.key))
                {
                    _builderWeights[control.key] = 0f;
                }
            }
        }

        private void PullWeightsFromFaceDriver(ArkItBlendshapeDriver faceDriver)
        {
            foreach (var control in ExpressionBuilderHelper.GetDefaultControls())
            {
                if (!_builderBlendshapeMap.TryGetValue(control.key, out var blendshapeName) || string.IsNullOrWhiteSpace(blendshapeName))
                {
                    _builderWeights[control.key] = 0f;
                    continue;
                }

                _builderWeights[control.key] = faceDriver != null ? faceDriver.GetBlendshapeWeight(blendshapeName) : GetRendererBlendshapeWeight(blendshapeName);
            }
        }

        private void ResetBuilderWeights()
        {
            foreach (var control in ExpressionBuilderHelper.GetDefaultControls())
            {
                _builderWeights[control.key] = 0f;
            }
        }

        private void LoadPresetIntoBuilder(ExpressionPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            _builderPresetName = preset.displayName;
            _builderDescription = preset.description;
            _builderCategory = preset.category;
            _builderTransitionTime = preset.transitionTimeInSeconds;

            if (_builderBlendshapeMap.Count == 0)
            {
                AutoDetectBuilderBlendshapes();
            }

            var loadedWeights = ExpressionBuilderHelper.LoadControlWeightsFromPreset(preset, _builderBlendshapeMap);
            foreach (var pair in loadedWeights)
            {
                _builderWeights[pair.Key] = pair.Value;
            }
        }

        private void BuildExpressionPreset(ExpressionLibraryManager expressionLibrary)
        {
            if (expressionLibrary == null)
            {
                EditorUtility.DisplayDialog("Expression Library Missing", "Add or build an ExpressionLibraryManager on the studio root first.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_builderPresetName))
            {
                EditorUtility.DisplayDialog("Name Required", "Enter a name for the new expression before building it.", "OK");
                return;
            }

            var blendshapeWeights = ExpressionBuilderHelper.BuildBlendshapeWeights(
                _builderBlendshapeMap,
                _builderWeights,
                GetAvailableBuilderBlendshapeNames());
            if (blendshapeWeights.Count == 0)
            {
                EditorUtility.DisplayDialog("No ARKit Weights", "Move at least one ARKit slider before building the expression.", "OK");
                return;
            }

            var preset = expressionLibrary.SavePreset(
                _builderPresetName,
                _builderCategory,
                _builderDescription,
                _builderTransitionTime,
                blendshapeWeights);

            if (preset != null)
            {
                _selectedExpressionPreset = preset;
                expressionLibrary.ApplyPreset(preset);
                EditorGUIUtility.PingObject(preset);
            }
        }

        private void ApplyBuilderPreview(ExpressionLibraryManager expressionLibrary, ArkItBlendshapeDriver faceDriver)
        {
            var blendshapeWeights = ExpressionBuilderHelper.BuildBlendshapeWeights(
                _builderBlendshapeMap,
                _builderWeights,
                GetAvailableBuilderBlendshapeNames());

            if (expressionLibrary != null)
            {
                expressionLibrary.ApplyExpressionWeights(blendshapeWeights);
                return;
            }

            if (faceDriver == null)
            {
                return;
            }

            foreach (var pair in blendshapeWeights)
            {
                faceDriver.TrySetBlendshapeWeight(pair.Key, pair.Value);
            }
        }

        private int GetBuilderPopupIndex(string selectedBlendshape)
        {
            if (string.IsNullOrWhiteSpace(selectedBlendshape))
            {
                return 0;
            }

            var index = _builderBlendshapeOptions.FindIndex(option => string.Equals(option, selectedBlendshape, StringComparison.Ordinal));
            return index >= 0 ? index : 0;
        }

        private float GetRendererBlendshapeWeight(string blendshapeName)
        {
            if (string.IsNullOrWhiteSpace(blendshapeName))
            {
                return 0f;
            }

            var maxWeight = 0f;
            foreach (var renderer in GetAvailableBuilderRenderers())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var index = renderer.sharedMesh.GetBlendShapeIndex(blendshapeName);
                if (index >= 0)
                {
                    maxWeight = Mathf.Max(maxWeight, renderer.GetBlendShapeWeight(index));
                }
            }

            return maxWeight;
        }

        private List<SkinnedMeshRenderer> GetAvailableBuilderRenderers()
        {
            var studioRoot = ResolveStudioRootFromContext();
            var renderers = new List<SkinnedMeshRenderer>();
            var expressionLibrary = studioRoot != null ? studioRoot.GetComponent<ExpressionLibraryManager>() : null;
            if (expressionLibrary?.TargetFaceRenderers != null)
            {
                foreach (var renderer in expressionLibrary.TargetFaceRenderers)
                {
                    if (renderer != null && !renderers.Contains(renderer))
                    {
                        renderers.Add(renderer);
                    }
                }
            }

            if (renderers.Count == 0 && _expressionRenderer != null)
            {
                renderers.Add(_expressionRenderer);
            }

            return renderers;
        }

        private List<string> GetAvailableBuilderBlendshapeNames()
        {
            return ExpressionBuilderHelper.GetBlendshapeNames(GetAvailableBuilderRenderers());
        }

        private void LogFaceBlendshapeReport(GameObject studioRoot, ExpressionLibraryManager expressionLibrary)
        {
            var renderers = new List<SkinnedMeshRenderer>();
            if (expressionLibrary?.TargetFaceRenderers != null)
            {
                renderers.AddRange(expressionLibrary.TargetFaceRenderers.Where(renderer => renderer != null));
            }

            if (renderers.Count == 0 && studioRoot != null)
            {
                renderers.AddRange(studioRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }

            if (renderers.Count == 0)
            {
                Debug.LogWarning("[Nyxara Face Report] No SkinnedMeshRenderer found for expression diagnostics.");
                return;
            }

            Debug.Log($"[Nyxara Face Report] Renderers={renderers.Count}");
            foreach (var renderer in renderers.Distinct())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var blendshapeNames = new List<string>();
                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    if (NyxaraCompanionStudioWindow.ContainsFaceDebugKeyword(name))
                    {
                        blendshapeNames.Add(name);
                    }
                }

                var summary = blendshapeNames.Count > 0 ? string.Join(", ", blendshapeNames) : "No mouth/jaw-related blendshapes detected";
                Debug.Log($"[Nyxara Face Report] Renderer='{renderer.name}' Mesh='{renderer.sharedMesh.name}' Blendshapes: {summary}", renderer);
            }
        }

        private static bool ContainsFaceDebugKeyword(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            return normalized.Contains("mouth") ||
                   normalized.Contains("jaw") ||
                   normalized.Contains("lip") ||
                   normalized.Contains("smile") ||
                   normalized.Contains("frown") ||
                   normalized.Contains("open") ||
                   normalized.Contains("close") ||
                   normalized.Contains("funnel") ||
                   normalized.Contains("pucker") ||
                   normalized.Contains("viseme") ||
                   normalized.Contains("aa") ||
                   normalized.Contains("oh") ||
                   normalized.Contains("ou") ||
                   normalized.Contains("ee") ||
                   normalized.Contains("ih");
        }

        private GameObject ResolveStudioRootFromContext()
        {
            var selected = Selection.activeGameObject;
            while (selected != null)
            {
                if (selected.GetComponent<NyxaraCompanionBrain>() != null)
                {
                    return selected;
                }

                selected = selected.transform.parent != null ? selected.transform.parent.gameObject : null;
            }

            return GameObject.Find(_config != null ? _config.studioRootName : "NyxaraStudioRoot");
        }

        private void SyncTabContextFromStudioRoot(GameObject studioRoot)
        {
            if (studioRoot == null)
            {
                return;
            }

            var expressionLibrary = studioRoot.GetComponent<ExpressionLibraryManager>();
            if (expressionLibrary != null)
            {
                var targetRenderer = GetObjectReference<SkinnedMeshRenderer>(expressionLibrary, "targetFaceRenderer");
                if (targetRenderer != null)
                {
                    _expressionRenderer = targetRenderer;
                }

                if (expressionLibrary.LoadedPresets.Count == 0)
                {
                    expressionLibrary.LoadAllPresets();
                }

                if (_selectedExpressionPreset == null && expressionLibrary.LoadedPresets.Count > 0)
                {
                    _selectedExpressionPreset = expressionLibrary.LoadedPresets[0];
                }

                _expressionModeEnabled = expressionLibrary.ExpressionModeActive;
            }

            var lipSyncController = studioRoot.GetComponent<VisemeLipSyncController>();
            if (lipSyncController != null)
            {
                var lipRenderer = GetObjectReference<SkinnedMeshRenderer>(lipSyncController, "faceRenderer");
                if (lipRenderer != null)
                {
                    _expressionRenderer = lipRenderer;
                }

                var lipSyncData = GetObjectReference<LipSyncData>(lipSyncController, "lipSyncData");
                if (lipSyncData != null)
                {
                    _lipSyncData = lipSyncData;
                }
            }
        }

        private void ApplyExpressionModeToScene(GameObject studioRoot)
        {
            if (studioRoot == null)
            {
                return;
            }

            var faceDriver = studioRoot.GetComponent<ArkItBlendshapeDriver>();
            faceDriver?.SetExpressionMode(_expressionModeEnabled);

            var expressionLibrary = studioRoot.GetComponent<ExpressionLibraryManager>();
            expressionLibrary?.SetExpressionMode(_expressionModeEnabled);

            var signalRouter = studioRoot.GetComponent<ExpressionSignalRouter>();
            signalRouter?.SetExpressionMode(_expressionModeEnabled);

            var lipSyncController = studioRoot.GetComponent<VisemeLipSyncController>();
            lipSyncController?.SetExpressionMode(_expressionModeEnabled);
        }

        private static T GetObjectReference<T>(UnityEngine.Object source, string propertyName) where T : UnityEngine.Object
        {
            if (source == null)
            {
                return null;
            }

            var serializedObject = new SerializedObject(source);
            var property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private sealed class StudioSystemScanner
        {
            private readonly AICompanionStudioConfig _config;

            public StudioSystemScanner(AICompanionStudioConfig config)
            {
                _config = config;
            }

            public SystemDiagnosticsReport Scan()
            {
                var report = new SystemDiagnosticsReport();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var brain = FindFirstObjectByType<NyxaraCompanionBrain>();
                var llmAgent = FindFirstObjectByType<LLMAgent>();
                var whisperInput = FindFirstObjectByType<WhisperMicrophoneInput>();
                var ttsService = FindFirstObjectByType<PiperTtsService>();
                var faceDriver = FindFirstObjectByType<ArkItBlendshapeDriver>();
                var lipSyncController = FindFirstObjectByType<VisemeLipSyncController>();
                var expressionLibrary = FindFirstObjectByType<ExpressionLibraryManager>();

                report.timestamp = DateTime.Now.ToString("HH:mm:ss");
                report.llmStatus = CheckStatus("LLM (Qwen)", llmAgent != null, llmAgent?.llm != null && !string.IsNullOrWhiteSpace(llmAgent.llm.model), llmAgent?.llm?.model ?? "Missing LLM or model");
                report.sttStatus = CheckStatus("STT (Whisper)", whisperInput != null, whisperInput?.WhisperManager != null, whisperInput?.WhisperManager != null ? "Configured" : "Missing WhisperManager");
                report.ttsStatus = CheckStatus("TTS (Piper)", ttsService != null, ttsService != null && ttsService.IsConfigured, ttsService != null && ttsService.IsConfigured ? "Configured" : "Missing paths or files");
                report.faceStatus = CheckStatus("Face System", faceDriver != null, HasObjectReference(faceDriver, "targetRenderer"), faceDriver != null ? "Face driver present" : "Missing face driver");
                report.lipSyncStatus = CheckStatus("Lip Sync", lipSyncController != null, lipSyncController != null, lipSyncController != null ? "Installed" : "Optional component missing");
                report.expressionStatus = CheckStatus("Expression Library", expressionLibrary != null, expressionLibrary != null, expressionLibrary != null ? "Installed" : "Optional component missing");

                report.configIssues = new List<ConfigIssue>();
                if (_config.sourceCharacterPrefab == null)
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Critical, component = "Studio", issue = "No source character assigned", suggestion = "Assign the model or prefab in the Studio tab" });
                }

                if (string.IsNullOrWhiteSpace(_config.preferredFaceRendererPath))
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Studio", issue = "No preferred face renderer selected", suggestion = "Pick the head or face mesh from the renderer dropdown" });
                }

                if (faceDriver != null && !HasObjectReference(faceDriver, "targetRenderer"))
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Face", issue = "Face driver has no targetRenderer assigned", suggestion = "Use Build Studio or Apply Rig To Selected Studio Root to wire the face renderer" });
                }

                if (FindFirstObjectByType<ExpressionSignalRouter>() == null)
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Expression", issue = "No ExpressionSignalRouter in scene", suggestion = "Build the studio root again or add the component manually" });
                }
                else if (!HasObjectReference(FindFirstObjectByType<ExpressionSignalRouter>(), "targetRenderer"))
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Expression", issue = "ExpressionSignalRouter is present but targetRenderer is not assigned", suggestion = "Use Build Studio or Apply Rig To Selected Studio Root to wire the face renderer" });
                }

                if (FindFirstObjectByType<PiperTTSPhonemeExtractor>() == null)
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Lip Sync", issue = "No PiperTTSPhonemeExtractor in scene", suggestion = "Build the studio root again or add the component manually" });
                }
                else
                {
                    var lipSync = FindFirstObjectByType<VisemeLipSyncController>();
                    if (lipSync != null)
                    {
                        if (!HasObjectReference(lipSync, "faceRenderer"))
                        {
                            report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Lip Sync", issue = "VisemeLipSyncController has no faceRenderer assigned", suggestion = "Use Build Studio or Apply Rig To Selected Studio Root to wire the face renderer" });
                        }

                        if (!HasObjectReference(lipSync, "phonemeExtractor"))
                        {
                            report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Warning, component = "Lip Sync", issue = "VisemeLipSyncController has no phonemeExtractor assigned", suggestion = "Rebuild the studio root so Piper phoneme extraction can drive mouth motion" });
                        }
                    }
                }

                AppendFaceRendererDiagnostics(report, expressionLibrary, faceDriver);

                var prefabPath = $"{_config.prefabFolder}/{_config.characterName}_StudioRoot.prefab";
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
                {
                    report.configIssues.Add(new ConfigIssue { severity = IssueSeverity.Info, component = "Studio", issue = "Studio prefab has not been built yet", suggestion = "Run Build Studio to generate the studio root prefab" });
                }

                report.pathValidations = new List<PathValidation>
                {
                    ValidatePath("LLM Model", ResolveModelStatusPath(_config.llmModelPath), false),
                    ValidateStreamingAssetsPath("Whisper Model", _config.whisperModelRelativePath),
                    ValidatePath("Piper Executable", _config.piperExecutablePath, false),
                    ValidatePath("Piper Voice", _config.piperVoicePath, false),
                    new PathValidation { name = "Source Character", path = _config.sourceCharacterPrefab != null ? AssetDatabase.GetAssetPath(_config.sourceCharacterPrefab) : "", exists = _config.sourceCharacterPrefab != null },
                    new PathValidation { name = "Studio Prefab", path = prefabPath, exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null }
                };

                stopwatch.Stop();
                report.durationMs = stopwatch.ElapsedMilliseconds;
                return report;
            }

            private static void AppendFaceRendererDiagnostics(SystemDiagnosticsReport report, ExpressionLibraryManager expressionLibrary, ArkItBlendshapeDriver faceDriver)
            {
                var renderers = new List<SkinnedMeshRenderer>();
                if (expressionLibrary?.TargetFaceRenderers != null)
                {
                    renderers.AddRange(expressionLibrary.TargetFaceRenderers.Where(renderer => renderer != null));
                }

                if (renderers.Count == 0 && faceDriver?.TargetRenderers != null)
                {
                    renderers.AddRange(faceDriver.TargetRenderers.Where(renderer => renderer != null));
                }

                if (renderers.Count == 0)
                {
                    return;
                }

                var distinctRenderers = renderers.Distinct().ToList();
                report.configIssues.Add(new ConfigIssue
                {
                    severity = IssueSeverity.Info,
                    component = "Face",
                    issue = $"Detected {distinctRenderers.Count} face renderer(s): {string.Join(", ", distinctRenderers.Select(renderer => renderer.name))}",
                    suggestion = "This should include head, lashes, eyes, and mouth meshes when they are separate"
                });

                foreach (var renderer in distinctRenderers)
                {
                    if (renderer == null || renderer.sharedMesh == null)
                    {
                        continue;
                    }

                    var mouthShapes = GetMouthRelatedBlendshapeNames(renderer);
                    var eyeShapes = GetEyeRelatedBlendshapeNames(renderer);
                    var jawShapes = GetJawRelatedBlendshapeNames(renderer);
                    var tongueTeethShapes = GetTongueOrTeethBlendshapeNames(renderer);
                    var hasRecognizedShapes = mouthShapes.Any(name => LooksLikeRecognizedMouthShape(name));
                    var isMouthRenderer = renderer.name.IndexOf("mouth", StringComparison.OrdinalIgnoreCase) >= 0;
                    var isEyeRenderer = renderer.name.IndexOf("eye", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isMouthRenderer)
                    {
                        report.configIssues.Add(new ConfigIssue
                        {
                            severity = IssueSeverity.Info,
                            component = "Face",
                            issue = $"Renderer '{renderer.name}' mouth-related blendshapes: {(mouthShapes.Count > 0 ? string.Join(", ", mouthShapes) : "none")}",
                            suggestion = "If these names do not resemble jaw/mouth/viseme shapes, the inner mouth mesh will need custom mapping or re-rigging"
                        });

                        report.configIssues.Add(new ConfigIssue
                        {
                            severity = IssueSeverity.Info,
                            component = "Face",
                            issue = $"Renderer '{renderer.name}' jaw blendshapes: {(jawShapes.Count > 0 ? string.Join(", ", jawShapes) : "none")}",
                            suggestion = "Jaw shapes should exist if speech or open-mouth expressions need to move the inner mouth"
                        });

                        report.configIssues.Add(new ConfigIssue
                        {
                            severity = IssueSeverity.Info,
                            component = "Face",
                            issue = $"Renderer '{renderer.name}' tongue/teeth blendshapes: {(tongueTeethShapes.Count > 0 ? string.Join(", ", tongueTeethShapes) : "none")}",
                            suggestion = "Tongue or teeth shapes are optional, but useful to diagnose inner-mouth motion"
                        });

                        var hasTeethShape = tongueTeethShapes.Any(name =>
                            name.IndexOf("teeth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("tooth", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!hasTeethShape)
                        {
                            report.configIssues.Add(new ConfigIssue
                            {
                                severity = IssueSeverity.Warning,
                                component = "Face",
                                issue = $"Renderer '{renderer.name}' has no teeth-specific blendshapes",
                                suggestion = "Upper/lower teeth opening usually needs teeth blendshapes or proper jaw bone rigging; jawOpen alone will not create that separation"
                            });
                        }
                    }

                    if (isEyeRenderer)
                    {
                        report.configIssues.Add(new ConfigIssue
                        {
                            severity = IssueSeverity.Info,
                            component = "Face",
                            issue = $"Renderer '{renderer.name}' eye-related blendshapes: {(eyeShapes.Count > 0 ? string.Join(", ", eyeShapes) : "none")}",
                            suggestion = "Eye renderers should expose blink, look, squint, or brow-linked shapes when separated"
                        });
                    }

                    if (isMouthRenderer && mouthShapes.Count == 0)
                    {
                        report.configIssues.Add(new ConfigIssue
                        {
                            severity = IssueSeverity.Warning,
                            component = "Face",
                            issue = $"Renderer '{renderer.name}' exists but has no mouth/jaw-related blendshapes",
                            suggestion = "This usually means the inner mouth mesh cannot respond to the current facial expression system"
                        });
                        continue;
                    }

                    if (isMouthRenderer && !hasRecognizedShapes)
                    {
                        report.configIssues.Add(new ConfigIssue
                        {
                            severity = IssueSeverity.Warning,
                            component = "Face",
                            issue = $"Renderer '{renderer.name}' has mouth blendshapes, but their names do not match the current ARKit/lip-sync expectations",
                            suggestion = $"Detected names: {string.Join(", ", mouthShapes)}"
                        });
                    }
                }
            }

            private static List<string> GetMouthRelatedBlendshapeNames(SkinnedMeshRenderer renderer)
            {
                var names = new List<string>();
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return names;
                }

                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    if (ContainsFaceDebugKeyword(name))
                    {
                        names.Add(name);
                    }
                }

                return names;
            }

            private static List<string> GetEyeRelatedBlendshapeNames(SkinnedMeshRenderer renderer)
            {
                var names = new List<string>();
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return names;
                }

                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    if (ExpressionBuilderHelper.IsEyeRelatedBlendshape(name))
                    {
                        names.Add(name);
                    }
                }

                return names;
            }

            private static List<string> GetJawRelatedBlendshapeNames(SkinnedMeshRenderer renderer)
            {
                var names = new List<string>();
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return names;
                }

                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    if (ExpressionBuilderHelper.IsJawRelatedBlendshape(name))
                    {
                        names.Add(name);
                    }
                }

                return names;
            }

            private static List<string> GetTongueOrTeethBlendshapeNames(SkinnedMeshRenderer renderer)
            {
                var names = new List<string>();
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return names;
                }

                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    var name = renderer.sharedMesh.GetBlendShapeName(i);
                    if (ExpressionBuilderHelper.IsTongueOrTeethRelatedBlendshape(name))
                    {
                        names.Add(name);
                    }
                }

                return names;
            }

            private static bool LooksLikeRecognizedMouthShape(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                var normalized = value.ToLowerInvariant();
                return normalized.Contains("mouthsmile") ||
                       normalized.Contains("mouthfrown") ||
                       normalized.Contains("mouthclose") ||
                       normalized.Contains("mouthfunnel") ||
                       normalized.Contains("mouthpucker") ||
                       normalized.Contains("jawopen") ||
                       normalized.Contains("mouthopen") ||
                       normalized.Contains("viseme") ||
                       normalized.Contains("aa") ||
                       normalized.Contains("oh") ||
                       normalized.Contains("ou") ||
                       normalized.Contains("ee") ||
                       normalized.Contains("ih");
            }

            private static ComponentStatus CheckStatus(string name, bool present, bool operational, string message)
            {
                return new ComponentStatus
                {
                    name = name,
                    isPresent = present,
                    isOperational = operational,
                    statusMessage = message
                };
            }

            private static PathValidation ValidatePath(string name, string path, bool relativeToAssets)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return new PathValidation { name = name, path = path, exists = false };
                }

                var fullPath = path;
                if (relativeToAssets && !Path.IsPathRooted(path))
                {
                    fullPath = Path.Combine(Application.dataPath, path.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar));
                }

                var exists = File.Exists(fullPath);
                return new PathValidation
                {
                    name = name,
                    path = path,
                    exists = exists,
                    fileSizeMB = exists ? new FileInfo(fullPath).Length / (1024 * 1024) : 0
                };
            }

            private static PathValidation ValidateStreamingAssetsPath(string name, string relativePath)
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    return new PathValidation { name = name, path = relativePath, exists = false };
                }

                var fullPath = Path.Combine(Application.streamingAssetsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                var exists = File.Exists(fullPath);
                return new PathValidation
                {
                    name = name,
                    path = relativePath,
                    exists = exists,
                    fileSizeMB = exists ? new FileInfo(fullPath).Length / (1024 * 1024) : 0
                };
            }

            private static bool HasObjectReference(UnityEngine.Object target, string fieldName)
            {
                var serializedObject = new SerializedObject(target);
                var property = serializedObject.FindProperty(fieldName);
                return property != null && property.objectReferenceValue != null;
            }
        }
    }
}
#endif
