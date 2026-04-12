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
        public bool sourceIsExistingRootPrefab = false;
        public string preferredFaceRendererPath = string.Empty;
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
        public bool createStudioEnvironment = true;
        public bool createStudioCamera = true;
        public bool createStudioLights = true;

        [Header("Studio Framing")]
        public Vector3 characterLocalPosition = Vector3.zero;
        public Vector3 characterLocalEuler = Vector3.zero;
        public float focusHeightOffset = 1.55f;
        public Vector3 cameraPivotOffset = Vector3.zero;
        public float cameraDistance = 1.35f;
        public float cameraHeight = 1.6f;
        public float cameraYaw = 0f;
        public float cameraFieldOfView = 26f;

        [Header("Studio Lighting")]
        public float keyLightIntensity = 2.2f;
        public float fillLightIntensity = 0.9f;
        public float rimLightIntensity = 1.35f;
        public Color studioBackgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f);

        [Header("Output Paths")]
        public string rootFolder = "Assets/AICompanionStudio";
        public string prefabFolder = "Assets/AICompanionStudio/Prefabs";
        public string companionPrefabFolder = "Assets/AICompanionStudio/Companions";
        public string profileFolder = "Assets/AICompanionStudio/Profiles";
        public string generatedFolder = "Assets/AICompanionStudio/Generated";
        public string expressionFolder = "Assets/AICompanionStudio/Expressions";
    }
}
