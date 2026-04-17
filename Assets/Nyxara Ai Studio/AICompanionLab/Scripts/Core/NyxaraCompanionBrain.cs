// Copyright (c) 2026 Bilal Raza
// Publisher: RAZ Studio
// Product: Nyxara AI Studio

using System;
using System.Diagnostics;
using System.Threading.Tasks;
#if NYXARA_LLMUNITY
using LLMUnity;
#endif
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
        private const string MissingLlmMessage = "Nyxara AI Studio: LLMUnity not installed. AI features disabled.";
        private const string MissingLlmReferenceMessage = "Nyxara AI Studio: LLMUnity is enabled but no LLMAgent is assigned. AI features disabled.";

        [Header("Core Components")]
        [SerializeField] private MonoBehaviour agent;
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
        [SerializeField] private bool warmupOnStart = true;
        [SerializeField] private string fallbackReply = "I heard you, but I need a moment.";

        private PromptBuilder _promptBuilder;
        private Stopwatch _generationTimer;
        private bool _hasLoggedMissingDependency;
        private bool _hasLoggedMissingReference;

#pragma warning disable CS0067
        public event Action<NPCResponseData> ResponseParsed;
#pragma warning restore CS0067
        public event Action<string> ReplyReady;

        public bool IsBusy { get; private set; }
        public string LastReply { get; private set; }
        public NPCResponseData LastParsedResponse { get; private set; }
        public CharacterProfileData CharacterProfile => characterProfile;
        public NPCRuntimeState RuntimeState => runtimeState;
        public dynamic Agent => agent;
        public PiperTtsService TtsService => ttsService;
        public ArkItBlendshapeDriver FaceDriver => faceDriver;
        public ExpressionSignalRouter SignalRouter => signalRouter;
        public RecentMemoryController MemoryController => memoryController;
        public ActionGatekeeper Gatekeeper => actionGatekeeper;
        public bool IsLlmAvailable => TryGetAgent(out _);

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

            ValidateSetup();
        }

#if NYXARA_LLMUNITY
        private async void Start()
#else
        private void Start()
#endif
        {
            if (!Application.isPlaying || !warmupOnStart)
            {
                return;
            }

            ValidateSetup();

#if NYXARA_LLMUNITY
            if (!TryGetAgent(out var llmAgent))
            {
                return;
            }

            try
            {
                await llmAgent.Warmup(_promptBuilder.BuildMinimalPrompt(characterProfile, "Hello."));
                Debug.Log("[Nyxara Runtime] LLM warmup completed.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Nyxara Runtime] LLM warmup failed: {ex.Message}");
            }
#else
            _ = speakReplies;
            _ = useStructuredParsing;
#endif
        }

        public async Task<string> ReplyToAsync(string userText)
        {
            if (IsBusy)
            {
                Debug.LogWarning("Already generating a reply.");
                return LastReply;
            }

            if (string.IsNullOrWhiteSpace(userText))
            {
                return string.Empty;
            }

            ValidateSetup();

#if NYXARA_LLMUNITY
            if (!TryGetAgent(out var llmAgent))
            {
                LastReply = fallbackReply;
                ReplyReady?.Invoke(fallbackReply);
                return fallbackReply;
            }

            IsBusy = true;
            faceDriver?.SetThinking(true);

            try
            {
                _generationTimer.Restart();

                var memoryString = memoryController?.GetMemoryString() ?? "";
                var prompt = _promptBuilder.BuildPrompt(characterProfile, runtimeState, userText, memoryString);
                var rawResponse = await llmAgent.Chat(prompt, null, null, false);
                _generationTimer.Stop();

                Debug.Log($"Generation took {_generationTimer.ElapsedMilliseconds}ms | Prompt chars: {prompt.Length}");

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
                memoryController?.AddPlayerMessage(userText);
                memoryController?.AddNPCResponse(finalDialogue, parsedResponse?.intent ?? "unknown");

                if (speakReplies && ttsService != null)
                {
                    _ = TrySpeakReplyAsync(finalDialogue);
                }

                return finalDialogue;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                LastReply = fallbackReply;
                ReplyReady?.Invoke(fallbackReply);
                return fallbackReply;
            }
            finally
            {
                faceDriver?.SetThinking(false);
                IsBusy = false;
            }
#else
            LastReply = fallbackReply;
            ReplyReady?.Invoke(fallbackReply);
            return await Task.FromResult(fallbackReply);
#endif
        }

        private async Task TrySpeakReplyAsync(string dialogue)
        {
            try
            {
                await ttsService.SpeakAsync(dialogue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Nyxara Runtime] Voice playback was skipped: {ex.Message}");
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
#if NYXARA_LLMUNITY
            agent = GetComponent<LLMAgent>();
#else
            agent = null;
#endif
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

        private void ValidateSetup()
        {
#if NYXARA_LLMUNITY
            if (agent == null && !_hasLoggedMissingReference)
            {
                _hasLoggedMissingReference = true;
                Debug.LogWarning(MissingLlmReferenceMessage, this);
            }
#else
            if (!_hasLoggedMissingDependency)
            {
                _hasLoggedMissingDependency = true;
                Debug.LogWarning(MissingLlmMessage, this);
            }
#endif
        }

#if NYXARA_LLMUNITY
        private bool TryGetAgent(out LLMAgent llmAgent)
        {
            llmAgent = agent as LLMAgent;
            if (llmAgent != null)
            {
                return true;
            }

            if (!_hasLoggedMissingReference)
            {
                _hasLoggedMissingReference = true;
                Debug.LogWarning(MissingLlmReferenceMessage, this);
            }

            return false;
        }
#else
        private bool TryGetAgent(out object llmAgent)
        {
            llmAgent = null;
            return false;
        }
#endif
    }
}
