#if UNITY_EDITOR
using UnityEditor;
#endif
using LLMUnity;
using Nyxara.AICompanion.Data;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.Runtime;
using Nyxara.AICompanion.Speech;
using UnityEngine;

namespace Nyxara.AICompanion.Core
{
    public class CompanionBootstrap : MonoBehaviour
    {
        [ContextMenu("Setup Complete Companion")]
        public void SetupCompleteCompanion()
        {
            var brain = GetComponent<NyxaraCompanionBrain>() ?? gameObject.AddComponent<NyxaraCompanionBrain>();
            var agent = GetComponent<LLMAgent>() ?? gameObject.AddComponent<LLMAgent>();
            var tts = GetComponent<PiperTtsService>() ?? gameObject.AddComponent<PiperTtsService>();
            var audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            var faceDriver = GetComponent<ArkItBlendshapeDriver>() ?? gameObject.AddComponent<ArkItBlendshapeDriver>();
            var signalRouter = GetComponent<ExpressionSignalRouter>() ?? gameObject.AddComponent<ExpressionSignalRouter>();
            var memory = GetComponent<RecentMemoryController>() ?? gameObject.AddComponent<RecentMemoryController>();
            var gatekeeper = GetComponent<ActionGatekeeper>() ?? gameObject.AddComponent<ActionGatekeeper>();
            var executor = GetComponent<CompanionActionExecutor>() ?? gameObject.AddComponent<CompanionActionExecutor>();

            AssignObjectReference(brain, "agent", agent);
            AssignObjectReference(brain, "ttsService", tts);
            AssignObjectReference(brain, "faceDriver", faceDriver);
            AssignObjectReference(brain, "signalRouter", signalRouter);
            AssignObjectReference(brain, "memoryController", memory);
            AssignObjectReference(brain, "actionGatekeeper", gatekeeper);

            AssignObjectReference(tts, "audioSource", audioSource);
            AssignObjectReference(tts, "faceDriver", faceDriver);
            AssignObjectReference(gatekeeper, "actionExecutor", executor);

            var profiles = Resources.LoadAll<CharacterProfileData>("");
            if (profiles.Length > 0)
            {
                AssignObjectReference(brain, "characterProfile", profiles[0]);
            }

            Debug.Log("Companion setup complete!");
        }

        private static void AssignObjectReference(Object target, string fieldName, Object value)
        {
#if UNITY_EDITOR
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

#if UNITY_EDITOR
        [MenuItem("Nyxara/AI Companion/Setup Full Companion")]
        public static void CreateFullCompanion()
        {
            var go = new GameObject("NyxaraCompanion");
            var bootstrap = go.AddComponent<CompanionBootstrap>();
            bootstrap.SetupCompleteCompanion();
            Selection.activeGameObject = go;
            Debug.Log("Created full companion setup");
        }
#endif
    }
}
