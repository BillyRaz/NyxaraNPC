# Nyxara AI Companion Stack

This Unity project is now scaffolded around a local-first stack:

- `LLMUnity` for GGUF inference inside Unity
- `whisper.unity` for local speech-to-text
- `Piper` as an external local text-to-speech process
- ARKit-style blendshape mapping through a custom Unity driver

## Local repos downloaded

These upstream repositories were cloned into `External/Repos`:

- `LLMUnity`
- `llama.cpp`
- `whisper.unity`
- `whisper.cpp`
- `piper`
- `sherpa-onnx`
- `arfoundation-samples`

## Unity package wiring

`Packages/manifest.json` now points at local package sources:

- `ai.undream.llm` -> `External/Repos/LLMUnity`
- `com.whisper.unity` -> `External/Repos/whisper.unity/Packages/com.whisper.unity`

That keeps the project reproducible without depending on live git package fetches every time Unity refreshes.

## Current model defaults

The project now prefers a project-local Qwen file at:

- `Assets/StreamingAssets/Models/Qwen2.5-7B-Instruct-1M-Q4_K_M.gguf`

If that file is missing, the bootstrap falls back to your original external path:

- `D:\Raz\Lm AI\lmstudio-community\Qwen2.5-7B-Instruct-1M-GGUF\Qwen2.5-7B-Instruct-1M-Q4_K_M.gguf`

That path is defined in:

- `Assets/AICompanion/Runtime/Configuration/CompanionStackDefaults.cs`

## Scene bootstrap

After Unity finishes importing packages, use:

- `Nyxara > AI Companion > Create Bootstrap Objects`

This creates:

- `Local LLM` with `LLM`
- `Companion Agent` with `LLMAgent` and `NyxaraCompanionBrain`
- `Speech To Text` with `WhisperManager` and `WhisperMicrophoneInput`
- `Speech Synthesis` with `AudioSource` and `PiperTtsService`
- `Face Driver` with `ArkItBlendshapeDriver`

## What still needs one manual file drop

### Whisper model

Put a Whisper model file at:

- `Assets/StreamingAssets/Speech/ggml-tiny.bin`

The package README recommends the `ggerganov/whisper.cpp` model files. You can also use a larger model and update the manager path later.

### Piper executable and voice

Point `PiperTtsService` to:

- your local `piper.exe`
- a Piper voice `.onnx` model

This project does not hardcode those paths because Piper deployments vary a lot by machine.

Downloaded Piper voice presets already in the project:

- `Assets/StreamingAssets/Speech/PiperVoices/en_US-amy-medium.onnx`
- `Assets/StreamingAssets/Speech/PiperVoices/en_US-lessac-medium.onnx`

Matching `.onnx.json` config files are stored beside each model.

## External voice tools you already have

- `D:\Raz\Voice` contains a packaged app install for `Voice Creator Pro` version `1.1.4.0`
- That app looks suitable for future voice cloning / custom preset generation
- It is not a source repository, so treat it as an external content-creation tool rather than part of the Unity codebase

## Runtime scripts added

- `NyxaraCompanionBrain` coordinates LLM reply generation and optional TTS playback.
- `WhisperMicrophoneInput` records from the microphone, transcribes with Whisper, and can auto-send text to the brain.
- `PiperTtsService` shells out to Piper, loads the generated wav, and plays it through an `AudioSource`.
- `ArkItBlendshapeDriver` applies simple speaking/thinking values to ARKit-style blendshape names on a `SkinnedMeshRenderer`.

## Suggested next pass inside Unity

1. Open the project and let Package Manager import the two local packages.
2. Run `Nyxara > AI Companion > Create Bootstrap Objects`.
3. Add your face mesh renderer to `Face Driver`.
4. Drop in a Whisper model under `Assets/StreamingAssets/Speech`.
5. Set Piper executable and voice paths on `Speech Synthesis`.
6. Add a simple UI button pair for `StartRecording` and `StopRecordingAndTranscribeAsync`.
