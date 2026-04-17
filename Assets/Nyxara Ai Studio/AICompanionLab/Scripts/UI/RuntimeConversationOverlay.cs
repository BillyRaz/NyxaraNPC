// Copyright (c) 2026 Bilal Raza
// Publisher: RAZ Studio
// Product: Nyxara AI Studio

using System;
using Nyxara.AICompanion.Core;
using Nyxara.AICompanion.Speech;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Nyxara.AICompanion.UI
{
    public class RuntimeConversationOverlay : MonoBehaviour
    {
        [SerializeField] private WhisperMicrophoneInput whisperInput;
        [SerializeField] private NyxaraCompanionBrain companionBrain;
        [SerializeField] private KeyCode micHoldKey = KeyCode.V;
        [SerializeField] private KeyCode promptPopupKey = KeyCode.T;
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showPromptPopup;
        [SerializeField] private string promptText = "Hello Nyxara.";

        private bool _micHeldLastFrame;
        private string _status = "Initializing...";
        private string _lastTranscript = string.Empty;
        private string _lastReply = string.Empty;

        private void Awake()
        {
            if (whisperInput == null)
            {
                whisperInput = GetComponentInChildren<WhisperMicrophoneInput>(true);
            }

            if (companionBrain == null)
            {
                companionBrain = GetComponent<NyxaraCompanionBrain>();
            }
        }

        private void OnEnable()
        {
            if (whisperInput != null)
            {
                whisperInput.TranscriptReady += HandleTranscriptReady;
            }

            if (companionBrain != null)
            {
                companionBrain.ReplyReady += HandleReplyReady;
            }

            _status = GetReadyStatus();
        }

        private void OnDisable()
        {
            if (whisperInput != null)
            {
                whisperInput.TranscriptReady -= HandleTranscriptReady;
            }

            if (companionBrain != null)
            {
                companionBrain.ReplyReady -= HandleReplyReady;
            }
        }

        private void Update()
        {
            if (GetKeyDownCompat(promptPopupKey))
            {
                showPromptPopup = !showPromptPopup;
            }

            var systemsReady = whisperInput != null && whisperInput.IsWhisperAvailable && companionBrain != null && companionBrain.IsLlmAvailable;
            if (!systemsReady)
            {
                _status = GetReadyStatus();
                return;
            }

            if (!showPromptPopup)
            {
                var micHeld = GetKeyCompat(micHoldKey);
                if (micHeld && !_micHeldLastFrame && !whisperInput.IsRecording)
                {
                    whisperInput.StartRecording();
                    _status = whisperInput.IsRecording
                        ? $"Recording... release {micHoldKey} to send"
                        : "Microphone failed to start recording.";
                    Debug.Log($"[Nyxara Runtime] Mic hold started with key {micHoldKey}.");
                }

                if (!micHeld && _micHeldLastFrame && whisperInput.IsRecording)
                {
                    _ = StopAndSendAsync();
                }

                _micHeldLastFrame = micHeld;
            }
        }

        private async System.Threading.Tasks.Task StopAndSendAsync()
        {
            _status = "Transcribing and sending...";
            Debug.Log("[Nyxara Runtime] Mic hold released. Transcribing and sending.");
            try
            {
                _lastTranscript = await whisperInput.StopRecordingAndTranscribeAsync();
                _status = string.IsNullOrWhiteSpace(_lastTranscript)
                    ? "No transcript detected."
                    : "Transcript sent to Nyxara.";
                Debug.Log($"[Nyxara Runtime] Transcript: {_lastTranscript}");
            }
            catch (Exception ex)
            {
                _status = $"Mic send failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private async void SendPrompt()
        {
            if (companionBrain == null || string.IsNullOrWhiteSpace(promptText))
            {
                return;
            }

            _status = "Sending typed prompt...";
            Debug.Log($"[Nyxara Runtime] Sending typed prompt: {promptText}");
            try
            {
                _lastReply = await companionBrain.ReplyToAsync(promptText);
                _status = "Typed prompt sent.";
            }
            catch (Exception ex)
            {
                _status = $"Prompt send failed: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 420f, showPromptPopup ? 310f : 220f), GUI.skin.window);
            GUILayout.Label("Nyxara Runtime Controls");
            GUILayout.Label(GetReadyStatus());
            GUILayout.Label($"Mic: hold {micHoldKey} to talk, release to send");
            GUILayout.Label($"Prompt: press {promptPopupKey} to toggle typed prompt");
            GUILayout.Label($"Status: {_status}");

            if (!string.IsNullOrWhiteSpace(_lastTranscript))
            {
                GUILayout.Label("Last Transcript:");
                GUILayout.TextArea(_lastTranscript, GUILayout.MinHeight(42f));
            }

            if (!string.IsNullOrWhiteSpace(_lastReply))
            {
                GUILayout.Label("Last Reply:");
                GUILayout.TextArea(_lastReply, GUILayout.MinHeight(42f));
            }

            if (showPromptPopup)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Typed Prompt");
                GUI.SetNextControlName("NyxaraRuntimePromptField");
                promptText = GUILayout.TextArea(promptText, GUILayout.MinHeight(64f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Send Prompt", GUILayout.Height(28f)))
                {
                    SendPrompt();
                }

                if (GUILayout.Button("Close", GUILayout.Height(28f)))
                {
                    showPromptPopup = false;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
        }

        private void HandleTranscriptReady(string transcript)
        {
            _lastTranscript = transcript ?? string.Empty;
        }

        private void HandleReplyReady(string reply)
        {
            _lastReply = reply ?? string.Empty;
            _status = "Reply ready.";
        }

        private string GetReadyStatus()
        {
            if (whisperInput == null)
            {
                return "STT: missing microphone input";
            }

            if (!whisperInput.IsWhisperAvailable)
            {
                return "STT: Whisper not installed or WhisperManager missing";
            }

            if (companionBrain == null)
            {
                return "Brain: missing";
            }

            if (!companionBrain.IsLlmAvailable)
            {
                return "LLM: LLMUnity not installed or agent missing";
            }

            return "Systems ready";
        }

        private static bool GetKeyCompat(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetKeyboardKeyState(key, out var isPressed, out _))
            {
                return isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        private static bool GetKeyDownCompat(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            if (TryGetKeyboardKeyState(key, out _, out var wasPressedThisFrame))
            {
                return wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryGetKeyboardKeyState(KeyCode key, out bool isPressed, out bool wasPressedThisFrame)
        {
            isPressed = false;
            wasPressedThisFrame = false;
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var button = key switch
            {
                KeyCode.A => keyboard.aKey,
                KeyCode.B => keyboard.bKey,
                KeyCode.C => keyboard.cKey,
                KeyCode.D => keyboard.dKey,
                KeyCode.E => keyboard.eKey,
                KeyCode.F => keyboard.fKey,
                KeyCode.G => keyboard.gKey,
                KeyCode.H => keyboard.hKey,
                KeyCode.I => keyboard.iKey,
                KeyCode.J => keyboard.jKey,
                KeyCode.K => keyboard.kKey,
                KeyCode.L => keyboard.lKey,
                KeyCode.M => keyboard.mKey,
                KeyCode.N => keyboard.nKey,
                KeyCode.O => keyboard.oKey,
                KeyCode.P => keyboard.pKey,
                KeyCode.Q => keyboard.qKey,
                KeyCode.R => keyboard.rKey,
                KeyCode.S => keyboard.sKey,
                KeyCode.T => keyboard.tKey,
                KeyCode.U => keyboard.uKey,
                KeyCode.V => keyboard.vKey,
                KeyCode.W => keyboard.wKey,
                KeyCode.X => keyboard.xKey,
                KeyCode.Y => keyboard.yKey,
                KeyCode.Z => keyboard.zKey,
                KeyCode.Alpha0 => keyboard.digit0Key,
                KeyCode.Alpha1 => keyboard.digit1Key,
                KeyCode.Alpha2 => keyboard.digit2Key,
                KeyCode.Alpha3 => keyboard.digit3Key,
                KeyCode.Alpha4 => keyboard.digit4Key,
                KeyCode.Alpha5 => keyboard.digit5Key,
                KeyCode.Alpha6 => keyboard.digit6Key,
                KeyCode.Alpha7 => keyboard.digit7Key,
                KeyCode.Alpha8 => keyboard.digit8Key,
                KeyCode.Alpha9 => keyboard.digit9Key,
                KeyCode.Space => keyboard.spaceKey,
                KeyCode.Return => keyboard.enterKey,
                KeyCode.KeypadEnter => keyboard.numpadEnterKey,
                KeyCode.LeftShift => keyboard.leftShiftKey,
                KeyCode.RightShift => keyboard.rightShiftKey,
                KeyCode.LeftControl => keyboard.leftCtrlKey,
                KeyCode.RightControl => keyboard.rightCtrlKey,
                KeyCode.LeftAlt => keyboard.leftAltKey,
                KeyCode.RightAlt => keyboard.rightAltKey,
                KeyCode.Tab => keyboard.tabKey,
                KeyCode.BackQuote => keyboard.backquoteKey,
                KeyCode.Escape => keyboard.escapeKey,
                _ => null
            };

            if (button == null)
            {
                return false;
            }

            isPressed = button.isPressed;
            wasPressedThisFrame = button.wasPressedThisFrame;
            return true;
        }
#endif
    }
}
