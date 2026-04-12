using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LLMUnity;
using Nyxara.AICompanion.Data;
using Nyxara.AICompanion.Face;
using Nyxara.AICompanion.Parsing;
using Nyxara.AICompanion.Prompting;
using Nyxara.AICompanion.Runtime;
using Nyxara.AICompanion.Speech;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nyxara.AICompanion.Core
{
    public class NyxaraCompanionBrain : MonoBehaviour
    {
        [Header("Core Components")]
        [SerializeField] private LLMAgent agent;
        [SerializeField] private PiperTtsService ttsService;
        [SerializeField] private ArkItBlendshapeDriver faceDriver;
        [SerializeField] private ExpressionSignalRouter signalRouter;

        [Header("Data")]
        [SerializeField] private CharacterProfileData characterProfile;
        [SerializeField] private NPCRuntimeState runtimeState;

        [Header("Controllers")]
        [SerializeField] private RecentMemoryController memoryController;
        [SerializeField] private ActionGatekeeper actionGatekeeper;

        [Header("Settings")]
        [SerializeField] private bool speakReplies = true;
        [SerializeField] private bool useStructuredParsing = true;
        [SerializeField] private string fallbackReply = "I heard you, but I need a moment.";

        private PromptBuilder _promptBuilder;
        private Stopwatch _generationTimer;

        public event Action<NPCResponseData> ResponseParsed;
        public event Action<string> ReplyReady;

        public bool IsBusy { get; private set; }
        public string LastReply { get; private set; }
        public NPCResponseData LastParsedResponse { get; private set; }
        public CharacterProfileData CharacterProfile => characterProfile;
        public NPCRuntimeState RuntimeState => runtimeState;
        public LLMAgent Agent => agent;
        public PiperTtsService TtsService => ttsService;
        public ArkItBlendshapeDriver FaceDriver => faceDriver;
        public ExpressionSignalRouter SignalRouter => signalRouter;
        public RecentMemoryController MemoryController => memoryController;
        public ActionGatekeeper Gatekeeper => actionGatekeeper;

        private void Awake()
        {
            _promptBuilder = new PromptBuilder();
            _generationTimer = new Stopwatch();

            if (runtimeState == null)
            {
                runtimeState = new NPCRuntimeState();
            }

            if (characterProfile == null)
            {
                Debug.LogWarning("No CharacterProfile assigned to NyxaraCompanionBrain");
            }
        }

        public async Task<string> ReplyToAsync(string userText)
        {
            if (IsBusy)
            {
                Debug.LogWarning("Already generating a reply.");
                return LastReply;
            }

            if (agent == null)
            {
                Debug.LogError("LLMAgent not assigned.");
                return fallbackReply;
            }

            if (string.IsNullOrWhiteSpace(userText))
            {
                return string.Empty;
            }

            IsBusy = true;
            faceDriver?.SetThinking(true);
            memoryController?.AddPlayerMessage(userText);

            try
            {
                _generationTimer.Restart();

                var memoryString = memoryController?.GetMemoryString() ?? "";
                var prompt = _promptBuilder.BuildPrompt(characterProfile, runtimeState, userText, memoryString);
                var rawResponse = await agent.Chat(prompt);
                _generationTimer.Stop();

                Debug.Log($"Generation took {_generationTimer.ElapsedMilliseconds}ms");

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    rawResponse = fallbackReply;
                }

                NPCResponseData parsedResponse = null;
                var finalDialogue = rawResponse;

                if (useStructuredParsing)
                {
                    parsedResponse = StructuredResponseParser.Parse(rawResponse, runtimeState);
                    LastParsedResponse = parsedResponse;
                    finalDialogue = parsedResponse.dialogue;

                    if (runtimeState != null && parsedResponse.mood != runtimeState.mood)
                    {
                        runtimeState.ApplyMoodShift(parsedResponse.mood);
                    }

                    if (signalRouter != null && parsedResponse.signal != "none")
                    {
                        signalRouter.ApplySignal(parsedResponse.signal, parsedResponse.mood);
                    }

                    if (actionGatekeeper != null && parsedResponse.action != "none")
                    {
                        actionGatekeeper.TryExecuteAction(parsedResponse.action, runtimeState);
                    }

                    ResponseParsed?.Invoke(parsedResponse);
                }

                LastReply = finalDialogue;
                ReplyReady?.Invoke(finalDialogue);
                memoryController?.AddNPCResponse(finalDialogue, parsedResponse?.intent ?? "unknown");

                if (speakReplies && ttsService != null)
                {
                    await ttsService.SpeakAsync(finalDialogue);
                }

                faceDriver?.SetSpeaking(false);
                return finalDialogue;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                LastReply = fallbackReply;
                return fallbackReply;
            }
            finally
            {
                faceDriver?.SetThinking(false);
                IsBusy = false;
            }
        }

        public void SetMood(string newMood)
        {
            if (runtimeState != null)
            {
                runtimeState.mood = newMood;
            }
        }

        public void ModifyTrust(float delta)
        {
            runtimeState?.ModifyTrust(delta);
        }

        private void Reset()
        {
            agent = GetComponent<LLMAgent>();
            ttsService = FindFirstObjectByType<PiperTtsService>();
            faceDriver = FindFirstObjectByType<ArkItBlendshapeDriver>();
            signalRouter = FindFirstObjectByType<ExpressionSignalRouter>();
            memoryController = GetComponent<RecentMemoryController>();
            actionGatekeeper = GetComponent<ActionGatekeeper>();

            var profiles = Resources.LoadAll<CharacterProfileData>("");
            if (profiles.Length > 0)
            {
                characterProfile = profiles[0];
            }
        }
    }
}
