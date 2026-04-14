# Nyxara AI Studio

Nyxara AI Studio is a Unity editor and runtime tool for turning ARKit-compatible 3D characters into AI companions with a one-click setup flow.

It is built for creators who want a practical local-first workflow inside Unity: set up a character, build a studio root, finalize a companion prefab, and test speech, lip sync, and status tools from one place.

## Who It Is For

- Unity creators building AI-driven character experiences
- Developers working with ARKit-style facial blendshapes
- Teams that want a practical V1 workflow before broader compatibility polish

## V1 Core Capabilities

- One-click studio/root setup for ARKit-compatible characters
- Local LLM, local STT, and local TTS wiring inside Unity
- Studio build, apply-rig, and finalize-prefab workflow
- Expression authoring and expression library tools
- Lip sync editing and testing tools
- Status, diagnostics, and runtime testing panels
- Demo-safe sample expression/profile content

## Basic Workflow

1. Import Nyxara AI Studio.
2. Install the required external dependencies.
3. Open `Nyxara AI > Studio`.
4. Assign a compatible source character.
5. Build the studio root.
6. Finalize the companion prefab.
7. Enter Play Mode and test the local stack.

## Not Included In V1

- Character packs or third-party avatar libraries
- Bundled local model files, Piper executables, or external voice/model repos
- Guaranteed compatibility with every facial rig
- Advanced realism polish for every source mesh
- Cloud/server features in the shipped V1 package

## Unity Version

Developed and tested on Unity `6.0.3f1`.

This Unity version has a known package signature warning.
It does not affect the functionality of Nyxara AI Studio.

You may use newer Unity versions at your own discretion.

For best stability, use the tested version above for V1.

## Release Packaging Notes

- Keep the core Nyxara AI Studio editor/runtime code.
- Exclude external repos, local-only model files, and any third-party character/demo content that is not confirmed safe to redistribute.
- Review [WHAT_IS_INCLUDED.md](WHAT_IS_INCLUDED.md) and [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) before shipping.
