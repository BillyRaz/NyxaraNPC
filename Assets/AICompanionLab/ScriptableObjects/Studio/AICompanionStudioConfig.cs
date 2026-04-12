using Nyxara.AICompanion.Configuration;
using Nyxara.AICompanion.Data;
using UnityEngine;

namespace Nyxara.AICompanion.Studio
{
    [CreateAssetMenu(fileName = "AICompanionStudioConfig", menuName = "AI Companion/Studio Config")]
    public class AICompanionStudioConfig : ScriptableObject
    {
        [Header("Identity")]
        public string characterName = "Nyxara";
        public string studioRootName = "NyxaraStudioRoot";

        [Header("Source")]
        public GameObject sourceCharacterPrefab;
        public SkinnedMeshRenderer preferredFaceRenderer;
        public Transform playerTransform;

        [Header("Data")]
        public CharacterProfileData characterProfile;

        [Header("AI Paths")]
        public string llmModelPath = CompanionStackDefaults.QwenModelPath;
        public string whisperModelRelativePath = CompanionStackDefaults.WhisperModelRelativePath;
        public string piperExecutablePath = CompanionStackDefaults.PiperExecutablePath;
        public string piperVoicePath = string.Empty;

        [Header("Build Options")]
        public bool createSceneInstance = true;
        public bool saveRootPrefab = true;
        public bool createProfileIfMissing = true;
        public bool autoAttachBootstrap = true;

        [Header("Output Paths")]
        public string rootFolder = "Assets/AICompanionStudio";
        public string prefabFolder = "Assets/AICompanionStudio/Prefabs";
        public string profileFolder = "Assets/AICompanionStudio/Profiles";
        public string generatedFolder = "Assets/AICompanionStudio/Generated";
        public string expressionFolder = "Assets/AICompanionStudio/Expressions";
    }
}
