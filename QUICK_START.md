# Quick Start

## Unity Version

Developed and tested on Unity `6.0.3f1`.

This Unity version has a known package signature warning.
It does not affect the functionality of Nyxara AI Studio.

You may use newer Unity versions at your own discretion.

For best stability, use the tested version above for V1.

## Setup

1. Import the Nyxara AI Studio package into your Unity project.
2. Install the required external dependencies used by your workflow:
   `LLMUnity`, Whisper integration, Piper, and any required runtime packages.
3. Open the Studio window from `Nyxara AI > Studio`.
4. Assign your ARKit-compatible source character or prepared source prefab.
5. Review the output folders and runtime paths in the Studio window.
6. Click `Build Studio`.
7. If needed, use `Apply Rig To Selected Studio Root`.
8. When the setup looks correct, click `Finalize Companion Root Prefab`.
9. Enter Play Mode.
10. Use the Testing and Diagnostics tabs to verify TTS, lip sync, and full-system behavior.

## External Dependencies

Nyxara AI Studio V1 assumes you will provide or install these separately:

- local GGUF LLM model
- Whisper model/integration
- Piper executable
- Piper voice model

These are intentionally not bundled into the release package by default.

## Recommended First Validation

- open `Status`
- run the diagnostics/system scan
- run a lip sync test
- run a full system test in Play Mode
