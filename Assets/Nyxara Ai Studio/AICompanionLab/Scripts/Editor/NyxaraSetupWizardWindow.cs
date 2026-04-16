#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.LipSync;
using Nyxara.AICompanion.Speech;
using Nyxara.AICompanion.Studio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nyxara.AICompanion.Editor
{
    public class NyxaraSetupWizardWindow : EditorWindow
    {
        private const string WizardTitle = "Nyxara Setup Wizard";
        private const string DefaultConfigPath = "Assets/Nyxara Ai Studio/AICompanionStudio/Generated/AICompanionStudioConfig.asset";
        private const string DefaultModelsFolder = "Models";
        private const string DefaultSpeechFolder = "Speech";
        private const string DefaultPiperRuntimeFolder = "Speech/PiperRuntime";
        private const string DefaultPiperVoicesFolder = "Speech/PiperVoices";

        private AICompanionStudioConfig _config;
        private Vector2 _scrollPosition;
        private string _llmSourcePath = string.Empty;
        private string _whisperIntegrationFolderPath = string.Empty;
        private string _whisperSourcePath = string.Empty;
        private string _piperRuntimeFolderPath = string.Empty;
        private string _piperVoiceSourcePath = string.Empty;
        private string _installSummary = string.Empty;
        private MessageType _installSummaryType = MessageType.Info;

        [MenuItem("Nyxara AI/Setup Wizard", false, 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<NyxaraSetupWizardWindow>(WizardTitle);
            window.minSize = new Vector2(640f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            _config = LoadOrCreateConfig();
            ApplyDefaultPathsIfEmpty(_config);
            NormalizeOptionalDependencyState();
        }

        private void OnGUI()
        {
            _config = (AICompanionStudioConfig)EditorGUILayout.ObjectField("Studio Config", _config, typeof(AICompanionStudioConfig), false);
            if (_config == null)
            {
                if (GUILayout.Button("Create Studio Config"))
                {
                    _config = LoadOrCreateConfig();
                    ApplyDefaultPathsIfEmpty(_config);
                }

                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            try
            {
                DrawOverview();
                EditorGUILayout.Space(8f);
                DrawLlmInstaller();
                EditorGUILayout.Space(8f);
                DrawWhisperInstaller();
                EditorGUILayout.Space(8f);
                DrawTtsInstaller();
                EditorGUILayout.Space(8f);
                DrawActions();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorGUILayout.HelpBox($"Nyxara Setup Wizard hit an editor error: {ex.Message}", MessageType.Error);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawOverview()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("New User Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Download your optional dependencies manually first, then use this wizard to place them into the correct Nyxara folders automatically. No internet setup runs here, and the wizard only copies files you already selected.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Install targets: GGUF -> StreamingAssets/Models, Whisper model -> StreamingAssets/Speech, Piper runtime -> StreamingAssets/Speech/PiperRuntime, Piper voice -> StreamingAssets/Speech/PiperVoices.",
                MessageType.None);

            var whisperStatus = string.IsNullOrWhiteSpace(_config.whisperModelRelativePath)
                ? CompanionStackDefaults.WhisperModelRelativePath
                : _config.whisperModelRelativePath;
            var ttsStatus = PiperTtsService.EvaluateAvailabilityStatus(_config.ttsEnabled, _config.piperExecutablePath, _config.piperVoicePath);

            EditorGUILayout.LabelField("Current Config", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("LLM", GetConfigDisplayPath(_config.llmModelPath));
            EditorGUILayout.LabelField("Whisper", GetConfigDisplayPath(whisperStatus));
            EditorGUILayout.LabelField("Piper Runtime", GetConfigDisplayPath(_config.piperExecutablePath));
            EditorGUILayout.LabelField("Piper Voice", GetConfigDisplayPath(_config.piperVoicePath));
            EditorGUILayout.LabelField("Voice Output", PiperTtsService.GetStatusLabel(ttsStatus));
            EditorGUILayout.EndVertical();
        }

        private void DrawLlmInstaller()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("1. LLM GGUF", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose the GGUF model file you already downloaded. The wizard will copy it into StreamingAssets/Models and update the Studio Config.", MessageType.None);
            DrawFilePicker("Source GGUF", ref _llmSourcePath, "gguf", "Select GGUF Model");
            EditorGUILayout.LabelField("Install To", BuildStreamingAssetsPreviewPath(DefaultModelsFolder, GetSafeFileName(_llmSourcePath)));
            EditorGUILayout.EndVertical();
        }

        private void DrawWhisperInstaller()
        {
            EditorGUILayout.BeginVertical("box");
            try
            {
                EditorGUILayout.LabelField("2. Speech To Text (Whisper)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("You can install the Whisper Unity integration from a downloaded folder and optionally copy a Whisper model into StreamingAssets/Speech. Sample/demo folders are skipped to avoid bringing broken example scripts into the project.", MessageType.None);
                DrawFolderPicker("Whisper Package Folder", ref _whisperIntegrationFolderPath, "Select Whisper Package Folder");
                EditorGUILayout.LabelField("Package Action", string.IsNullOrWhiteSpace(_whisperIntegrationFolderPath) ? "No Whisper integration folder selected" : DescribeWhisperImportTarget(_whisperIntegrationFolderPath));

                EditorGUILayout.Space(4f);
                DrawFilePicker("Source Model", ref _whisperSourcePath, "bin", "Select Whisper Model");
                EditorGUILayout.LabelField("Install To", BuildStreamingAssetsPreviewPath(DefaultSpeechFolder, GetSafeFileName(_whisperSourcePath)));
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawTtsInstaller()
        {
            EditorGUILayout.BeginVertical("box");
            try
            {
                EditorGUILayout.LabelField("3. Voice Output (Piper)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Choose the Piper runtime folder and the Piper voice model you already downloaded. The wizard copies the full runtime folder so any required companion files stay together.", MessageType.None);

                DrawFolderPicker("Piper Runtime Folder", ref _piperRuntimeFolderPath, "Select Piper Runtime Folder");
                var detectedExecutable = FindPiperExecutableInFolder(_piperRuntimeFolderPath);
                EditorGUILayout.LabelField("Detected Executable", string.IsNullOrWhiteSpace(detectedExecutable) ? "No Piper executable found yet" : detectedExecutable);
                EditorGUILayout.LabelField("Install To", BuildStreamingAssetsPreviewPath(DefaultPiperRuntimeFolder, GetSafeFileName(_piperRuntimeFolderPath)));

                EditorGUILayout.Space(4f);
                DrawFilePicker("Voice Model", ref _piperVoiceSourcePath, "onnx", "Select Piper Voice Model");
                EditorGUILayout.LabelField("Install To", BuildStreamingAssetsPreviewPath(DefaultPiperVoicesFolder, GetSafeFileName(_piperVoiceSourcePath)));
                EditorGUILayout.HelpBox("If a matching .onnx.json file is beside the voice model, the wizard copies that too.", MessageType.None);
            }
            finally
            {
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Install Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Install LLM"))
            {
                InstallLlmModel();
            }

            if (GUILayout.Button("Install Whisper"))
            {
                InstallWhisperDependencies();
            }

            if (GUILayout.Button("Install Piper"))
            {
                InstallPiperDependencies();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Install All"))
            {
                InstallAllSelected();
            }

            if (GUILayout.Button("Open StreamingAssets Folder"))
            {
                EnsureDirectoryExists(Application.streamingAssetsPath);
                EditorUtility.RevealInFinder(Application.streamingAssetsPath);
            }

            if (GUILayout.Button("Open Studio"))
            {
                NyxaraCompanionStudioWindow.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_installSummary))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(_installSummary, _installSummaryType);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawFilePicker(string label, ref string value, string extension, string title)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("Browse", GUILayout.Width(90f)))
            {
                var startDirectory = GetSafeBrowseDirectory(value);
                var selected = EditorUtility.OpenFilePanel(title, startDirectory, extension);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    value = selected.Replace('\\', '/');
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawFolderPicker(string label, ref string value, string title)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("Browse", GUILayout.Width(90f)))
            {
                var startDirectory = GetSafeBrowseDirectory(value);
                var selected = EditorUtility.OpenFolderPanel(title, startDirectory, string.Empty);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    value = selected.Replace('\\', '/');
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void InstallAllSelected()
        {
            NormalizeOptionalDependencyState();
            var results = new List<string>();

            if (!string.IsNullOrWhiteSpace(_llmSourcePath))
            {
                results.Add(InstallLlmModel());
            }

            if (!string.IsNullOrWhiteSpace(_whisperIntegrationFolderPath) || !string.IsNullOrWhiteSpace(_whisperSourcePath))
            {
                results.Add(InstallWhisperDependencies());
            }

            if (!string.IsNullOrWhiteSpace(_piperRuntimeFolderPath) || !string.IsNullOrWhiteSpace(_piperVoiceSourcePath))
            {
                results.Add(InstallPiperDependencies());
            }

            if (results.Count == 0)
            {
                SetSummary("Nyxara AI Studio: No source paths were selected yet. Browse to the files you downloaded, then run Install All.", MessageType.Warning);
                return;
            }

            SetSummary(string.Join(Environment.NewLine, results.Where(result => !string.IsNullOrWhiteSpace(result))), MessageType.Info);
        }

        private string InstallLlmModel()
        {
            if (!TryValidateFile(_llmSourcePath, "GGUF model", out var llmSourcePath))
            {
                return _installSummary;
            }

            var destinationPath = CopyFileIntoStreamingAssets(llmSourcePath, DefaultModelsFolder);
            _config.llmModelPath = MakeStreamingAssetsRelative(destinationPath);
            SaveConfigAndSyncLiveServices();

            var message = $"Nyxara AI Studio: Installed LLM model to {_config.llmModelPath}.";
            SetSummary(message, MessageType.Info);
            return message;
        }

        private string InstallWhisperModel()
        {
            if (!TryValidateFile(_whisperSourcePath, "Whisper model", out var whisperSourcePath))
            {
                return _installSummary;
            }

            var destinationPath = CopyFileIntoStreamingAssets(whisperSourcePath, DefaultSpeechFolder);
            _config.whisperModelRelativePath = MakeStreamingAssetsRelative(destinationPath);
            SaveConfigAndSyncLiveServices();

            var message = $"Nyxara AI Studio: Installed Whisper model to {_config.whisperModelRelativePath}.";
            SetSummary(message, MessageType.Info);
            return message;
        }

        private string InstallWhisperDependencies()
        {
            var messages = new List<string>();

            if (!string.IsNullOrWhiteSpace(_whisperIntegrationFolderPath))
            {
                var integrationMessage = InstallWhisperIntegration();
                if (!string.IsNullOrWhiteSpace(integrationMessage))
                {
                    messages.Add(integrationMessage);
                }
            }

            if (!string.IsNullOrWhiteSpace(_whisperSourcePath))
            {
                var modelMessage = InstallWhisperModel();
                if (!string.IsNullOrWhiteSpace(modelMessage))
                {
                    messages.Add(modelMessage);
                }
            }

            if (messages.Count == 0)
            {
                SetSummary("Nyxara AI Studio: Select a Whisper package folder and/or a Whisper model before installing.", MessageType.Warning);
                return _installSummary;
            }

            var summary = string.Join(Environment.NewLine, messages.Where(message => !string.IsNullOrWhiteSpace(message)));
            SetSummary(summary, MessageType.Info);
            return summary;
        }

        private string InstallWhisperIntegration()
        {
            if (!TryValidateDirectory(_whisperIntegrationFolderPath, "Whisper package folder", out var packageFolderPath))
            {
                return _installSummary;
            }

            if (TryFindAssetsFolder(packageFolderPath, out var assetsFolderPath))
            {
                CopyDirectoryContents(assetsFolderPath, Application.dataPath, ShouldSkipWhisperImportRelativePath);
                AssetDatabase.Refresh();
                var message = $"Nyxara AI Studio: Imported Whisper assets from {assetsFolderPath.Replace('\\', '/')} into the Unity project without sample/demo folders.";
                SetSummary(message, MessageType.Info);
                return message;
            }

            if (TryFindEmbeddedPackageFolder(packageFolderPath, out var embeddedPackageFolderPath))
            {
                var destinationFolder = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Packages", GetSafeFileName(embeddedPackageFolderPath));
                CopyDirectoryContents(embeddedPackageFolderPath, destinationFolder, ShouldSkipWhisperImportRelativePath);
                AssetDatabase.Refresh();
                var message = $"Nyxara AI Studio: Imported Whisper embedded package from {embeddedPackageFolderPath.Replace('\\', '/')} into Packages/{GetSafeFileName(embeddedPackageFolderPath)} without sample/demo folders.";
                SetSummary(message, MessageType.Info);
                return message;
            }

            if (TryFindUnityPackage(packageFolderPath, out var unityPackagePath))
            {
                SetSummary($"Nyxara AI Studio: Found a Whisper .unitypackage at {unityPackagePath.Replace('\\', '/')}, but the setup wizard skips auto-importing .unitypackage files to avoid sample/demo script conflicts. Use an extracted Whisper folder with Assets or package.json content instead.", MessageType.Warning);
                return _installSummary;
            }

            SetSummary("Nyxara AI Studio: No importable Whisper package content was found. Select a Whisper folder containing a .unitypackage, an Assets folder, or a package.json package root.", MessageType.Error);
            return _installSummary;
        }

        private string InstallPiperDependencies()
        {
            var messages = new List<string>();

            if (!string.IsNullOrWhiteSpace(_piperRuntimeFolderPath))
            {
                if (!TryValidateDirectory(_piperRuntimeFolderPath, "Piper runtime folder", out var runtimeFolderPath))
                {
                    return _installSummary;
                }

                var detectedExecutable = FindPiperExecutableInFolder(runtimeFolderPath);
                if (string.IsNullOrWhiteSpace(detectedExecutable))
                {
                    SetSummary("Nyxara AI Studio: The selected Piper runtime folder does not contain a Piper executable. Select the extracted Piper folder and try again.", MessageType.Error);
                    return _installSummary;
                }

                var copiedRuntimeRoot = CopyDirectoryIntoStreamingAssets(runtimeFolderPath, DefaultPiperRuntimeFolder);
                var relativeExecutablePath = GetRelativePath(runtimeFolderPath, detectedExecutable);
                var installedExecutablePath = Path.Combine(copiedRuntimeRoot, relativeExecutablePath.Replace('/', Path.DirectorySeparatorChar));
                _config.piperExecutablePath = MakeStreamingAssetsRelative(installedExecutablePath);
                messages.Add($"Installed Piper runtime to {_config.piperExecutablePath}.");
            }

            if (!string.IsNullOrWhiteSpace(_piperVoiceSourcePath))
            {
                if (!TryValidateFile(_piperVoiceSourcePath, "Piper voice model", out var voiceSourcePath))
                {
                    return _installSummary;
                }

                var destinationVoicePath = CopyFileIntoStreamingAssets(voiceSourcePath, DefaultPiperVoicesFolder);
                CopyOptionalPiperVoiceMetadata(voiceSourcePath, destinationVoicePath);
                _config.piperVoicePath = MakeStreamingAssetsRelative(destinationVoicePath);
                messages.Add($"Installed Piper voice to {_config.piperVoicePath}.");
            }

            _config.ttsEnabled = !string.IsNullOrWhiteSpace(_config.piperExecutablePath) && !string.IsNullOrWhiteSpace(_config.piperVoicePath);
            SaveConfigAndSyncLiveServices();

            if (messages.Count == 0)
            {
                SetSummary("Nyxara AI Studio: Select a Piper runtime folder and/or a Piper voice model before installing.", MessageType.Warning);
                return _installSummary;
            }

            var summary = "Nyxara AI Studio: " + string.Join(" ", messages);
            SetSummary(summary, MessageType.Info);
            return summary;
        }

        private static void CopyOptionalPiperVoiceMetadata(string sourceVoicePath, string destinationVoicePath)
        {
            var candidatePaths = new[]
            {
                sourceVoicePath + ".json",
                Path.ChangeExtension(sourceVoicePath, ".json")
            };

            var copiedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidatePath in candidatePaths)
            {
                if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
                {
                    continue;
                }

                var destinationPath = candidatePath.EndsWith(".onnx.json", StringComparison.OrdinalIgnoreCase)
                    ? destinationVoicePath + ".json"
                    : Path.ChangeExtension(destinationVoicePath, ".json");

                if (!copiedTargets.Add(destinationPath))
                {
                    continue;
                }

                File.Copy(candidatePath, destinationPath, true);
            }
        }

        private static string CopyFileIntoStreamingAssets(string sourcePath, string relativeFolder)
        {
            var destinationFolder = Path.Combine(Application.streamingAssetsPath, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            EnsureDirectoryExists(destinationFolder);

            var destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
            if (!PathsMatch(sourcePath, destinationPath))
            {
                File.Copy(sourcePath, destinationPath, true);
            }

            AssetDatabase.Refresh();
            return destinationPath;
        }

        private static string CopyDirectoryIntoStreamingAssets(string sourceFolderPath, string relativeFolder)
        {
            var folderName = GetSafeFileName(sourceFolderPath);
            var destinationRoot = Path.Combine(Application.streamingAssetsPath, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            var destinationFolderPath = Path.Combine(destinationRoot, folderName);

            EnsureDirectoryExists(destinationRoot);
            if (PathsMatch(sourceFolderPath, destinationFolderPath))
            {
                return destinationFolderPath;
            }

            CopyDirectoryContents(sourceFolderPath, destinationFolderPath);
            AssetDatabase.Refresh();
            return destinationFolderPath;
        }

        private static void CopyDirectoryContents(string sourceFolderPath, string destinationFolderPath, Func<string, bool> skipRelativePath = null)
        {
            EnsureDirectoryExists(destinationFolderPath);

            foreach (var directory in Directory.GetDirectories(sourceFolderPath, "*", SearchOption.AllDirectories))
            {
                var relativeDirectory = GetRelativePath(sourceFolderPath, directory);
                if (skipRelativePath != null && skipRelativePath(relativeDirectory))
                {
                    continue;
                }

                EnsureDirectoryExists(Path.Combine(destinationFolderPath, relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));
            }

            foreach (var filePath in Directory.GetFiles(sourceFolderPath, "*", SearchOption.AllDirectories))
            {
                var relativeFilePath = GetRelativePath(sourceFolderPath, filePath);
                if (skipRelativePath != null && skipRelativePath(relativeFilePath))
                {
                    continue;
                }

                var destinationPath = Path.Combine(destinationFolderPath, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    EnsureDirectoryExists(destinationDirectory);
                }

                if (!PathsMatch(filePath, destinationPath))
                {
                    File.Copy(filePath, destinationPath, true);
                }
            }
        }

        private void SaveConfigAndSyncLiveServices()
        {
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            SyncConfigToLiveComponents();
            AssetDatabase.Refresh();
        }

        private void SyncConfigToLiveComponents()
        {
            var llm = FindComponentByTypeName("LLM");
            if (llm != null)
            {
                AssignStringFieldToComponent(llm, "model", _config.llmModelPath);
                EditorUtility.SetDirty(llm);
            }

            var whisper = FindComponentByTypeName("WhisperManager");
            if (whisper != null)
            {
                AssignStringFieldToComponent(whisper, "ModelPath", _config.whisperModelRelativePath);
                AssignBoolFieldToComponent(whisper, "IsModelPathInStreamingAssets", true);
                EditorUtility.SetDirty(whisper);
            }

            var ttsService = FindFirstObjectByType<PiperTtsService>();
            if (ttsService != null)
            {
                ttsService.TtsEnabled = _config.ttsEnabled;
                ttsService.PiperExecutablePath = _config.piperExecutablePath;
                ttsService.VoiceModelPath = _config.piperVoicePath;
                EditorUtility.SetDirty(ttsService);
            }

            var phonemeExtractor = FindFirstObjectByType<PiperTTSPhonemeExtractor>();
            if (phonemeExtractor != null)
            {
                AssignStringFieldToComponent(phonemeExtractor, "piperExecutablePath", _config.piperExecutablePath);
                AssignStringFieldToComponent(phonemeExtractor, "voiceModelPath", _config.piperVoicePath);
                EditorUtility.SetDirty(phonemeExtractor);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static Component FindComponentByTypeName(string typeName)
        {
            return FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(component => component != null && string.Equals(component.GetType().Name, typeName, StringComparison.Ordinal));
        }

        private static void AssignStringFieldToComponent(Component component, string fieldName, string value)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                return;
            }

            property.stringValue = value ?? string.Empty;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBoolFieldToComponent(Component component, string fieldName, bool value)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                return;
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private bool TryValidateDirectory(string selectedPath, string label, out string validatedPath)
        {
            validatedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                SetSummary($"Nyxara AI Studio: Select a {label} before installing.", MessageType.Warning);
                return false;
            }

            try
            {
                validatedPath = Path.GetFullPath(selectedPath.Trim().Trim('"'));
                if (Directory.Exists(validatedPath))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            SetSummary($"Nyxara AI Studio: The selected {label} could not be found. Browse to a valid location and try again.", MessageType.Error);
            return false;
        }

        private bool TryValidateFile(string selectedPath, string label, out string validatedPath)
        {
            validatedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                SetSummary($"Nyxara AI Studio: Select a {label} before installing.", MessageType.Warning);
                return false;
            }

            try
            {
                validatedPath = Path.GetFullPath(selectedPath.Trim().Trim('"'));
                if (File.Exists(validatedPath))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }

            SetSummary($"Nyxara AI Studio: The selected {label} could not be found. Browse to a valid file and try again.", MessageType.Error);
            return false;
        }

        private void SetSummary(string message, MessageType messageType)
        {
            _installSummary = message;
            _installSummaryType = messageType;
        }

        private static string BuildStreamingAssetsPreviewPath(string relativeFolder, string fileName)
        {
            var folderPath = Path.Combine(Application.streamingAssetsPath, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(fileName)
                ? folderPath.Replace('\\', '/')
                : Path.Combine(folderPath, fileName).Replace('\\', '/');
        }

        private static string GetConfigDisplayPath(string configuredPath)
        {
            return string.IsNullOrWhiteSpace(configuredPath) ? "Not configured" : configuredPath.Replace('\\', '/');
        }

        private static string GetSafeBrowseDirectory(string currentPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    var fullPath = Path.GetFullPath(currentPath.Trim().Trim('"'));
                    if (File.Exists(fullPath))
                    {
                        var directory = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                        {
                            return directory;
                        }
                    }

                    if (Directory.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            catch (Exception)
            {
            }

            EnsureDirectoryExists(Application.streamingAssetsPath);
            return Application.streamingAssetsPath;
        }

        private static string FindPiperExecutableInFolder(string runtimeFolderPath)
        {
            if (string.IsNullOrWhiteSpace(runtimeFolderPath) || !Directory.Exists(runtimeFolderPath))
            {
                return string.Empty;
            }

            foreach (var executableName in new[] { "piper.exe", "piper" })
            {
                var match = Directory.GetFiles(runtimeFolderPath, executableName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match.Replace('\\', '/');
                }
            }

            return string.Empty;
        }

        private static string DescribeWhisperImportTarget(string folderPath)
        {
            try
            {
                var fullPath = Path.GetFullPath(folderPath.Trim().Trim('"'));
                if (!Directory.Exists(fullPath))
                {
                    return "Selected folder not found";
                }

                if (TryFindUnityPackage(fullPath, out var unityPackagePath))
                {
                    return $"Will import Unity package: {unityPackagePath.Replace('\\', '/')}";
                }

                if (TryFindAssetsFolder(fullPath, out var assetsFolderPath))
                {
                    return $"Will copy Assets folder into project: {assetsFolderPath.Replace('\\', '/')}";
                }

                if (TryFindEmbeddedPackageFolder(fullPath, out var embeddedPackageFolderPath))
                {
                    return $"Will copy embedded package into project Packages: {embeddedPackageFolderPath.Replace('\\', '/')}";
                }

                return "No importable Whisper package content found yet";
            }
            catch (Exception)
            {
                return "Unable to inspect selected folder";
            }
        }

        private static bool TryFindUnityPackage(string rootFolderPath, out string unityPackagePath)
        {
            unityPackagePath = Directory.GetFiles(rootFolderPath, "*.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            return !string.IsNullOrWhiteSpace(unityPackagePath);
        }

        private static bool TryFindAssetsFolder(string rootFolderPath, out string assetsFolderPath)
        {
            assetsFolderPath = string.Empty;
            if (string.Equals(GetSafeFileName(rootFolderPath), "Assets", StringComparison.OrdinalIgnoreCase))
            {
                assetsFolderPath = rootFolderPath;
                return true;
            }

            assetsFolderPath = Directory.GetDirectories(rootFolderPath, "Assets", SearchOption.AllDirectories).FirstOrDefault();
            return !string.IsNullOrWhiteSpace(assetsFolderPath);
        }

        private static bool TryFindEmbeddedPackageFolder(string rootFolderPath, out string packageFolderPath)
        {
            packageFolderPath = string.Empty;

            if (File.Exists(Path.Combine(rootFolderPath, "package.json")))
            {
                packageFolderPath = rootFolderPath;
                return true;
            }

            packageFolderPath = Directory.GetFiles(rootFolderPath, "package.json", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            return !string.IsNullOrWhiteSpace(packageFolderPath);
        }

        private static bool ShouldSkipWhisperImportRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            var normalized = relativePath.Replace('\\', '/');
            if (normalized.StartsWith("Samples/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Samples~/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Examples/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Example/", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Samples.meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Samples~.meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Examples.meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Example.meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (string.Equals(segment, "Samples", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Samples~", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Examples", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Example", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Samples.meta", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Samples~.meta", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Examples.meta", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Example.meta", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, "Documentation", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segment, ".github", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetRelativePath(string basePath, string targetPath)
        {
            var baseUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(basePath)));
            var targetUri = new Uri(Path.GetFullPath(targetPath));
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString()).Replace('\\', '/');
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string MakeStreamingAssetsRelative(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            try
            {
                var normalizedAbsolute = Path.GetFullPath(absolutePath.Trim().Trim('"'));
                var normalizedStreamingAssets = Path.GetFullPath(Application.streamingAssetsPath);
                if (normalizedAbsolute.StartsWith(normalizedStreamingAssets, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = normalizedAbsolute.Substring(normalizedStreamingAssets.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return relative.Replace('\\', '/');
                }

                return normalizedAbsolute.Replace('\\', '/');
            }
            catch (Exception)
            {
                return absolutePath.Replace('\\', '/');
            }
        }

        private static string GetSafeFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(path.Trim().Trim('"'));
            }
            catch (Exception)
            {
                return path;
            }
        }

        private static bool PathsMatch(string leftPath, string rightPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(leftPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(rightPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            Directory.CreateDirectory(directoryPath);
        }

        private static AICompanionStudioConfig LoadOrCreateConfig()
        {
            var configPath = AssetDatabase.FindAssets("t:AICompanionStudioConfig")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = DefaultConfigPath;
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory) && !AssetDatabase.IsValidFolder(directory))
                {
                    CreateFolderRecursively(directory.Replace('\\', '/'));
                }

                var createdConfig = CreateInstance<AICompanionStudioConfig>();
                AssetDatabase.CreateAsset(createdConfig, configPath);
                AssetDatabase.SaveAssets();
            }

            return AssetDatabase.LoadAssetAtPath<AICompanionStudioConfig>(configPath);
        }

        private static void CreateFolderRecursively(string folderPath)
        {
            var normalizedPath = folderPath.Replace('\\', '/');
            var segments = normalizedPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return;
            }

            var currentPath = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var nextPath = currentPath + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[i]);
                }

                currentPath = nextPath;
            }
        }

        private static void ApplyDefaultPathsIfEmpty(AICompanionStudioConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(config.llmModelPath))
            {
                config.llmModelPath = CompanionStackDefaults.QwenModelPath;
            }

            if (string.IsNullOrWhiteSpace(config.whisperModelRelativePath))
            {
                config.whisperModelRelativePath = CompanionStackDefaults.WhisperModelRelativePath;
            }
        }

        private void NormalizeOptionalDependencyState()
        {
            if (_config == null)
            {
                return;
            }

            if (string.Equals(_config.piperVoicePath, CompanionStackDefaults.PiperVoiceRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                var defaultVoicePath = Path.Combine(Application.streamingAssetsPath, CompanionStackDefaults.PiperVoiceRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(defaultVoicePath))
                {
                    _config.piperVoicePath = string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(_config.piperExecutablePath) || string.IsNullOrWhiteSpace(_config.piperVoicePath))
            {
                _config.ttsEnabled = false;
            }

            EditorUtility.SetDirty(_config);
        }
    }
}
#endif
