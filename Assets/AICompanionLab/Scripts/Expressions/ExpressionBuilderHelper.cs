using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nyxara.AICompanion.Expressions
{
    public static class ExpressionBuilderHelper
    {
        [Serializable]
        public sealed class ControlDefinition
        {
            public string key;
            public string displayName;
            public string[] candidateBlendshapeNames;
        }

        private static readonly List<ControlDefinition> DefaultControls = new()
        {
            Create("browInnerUp", "Brow Inner Up", "browInnerUp", "BrowInnerUp"),
            Create("browDownLeft", "Brow Down Left", "browDownLeft", "BrowDownLeft"),
            Create("browDownRight", "Brow Down Right", "browDownRight", "BrowDownRight"),
            Create("eyeBlinkLeft", "Eye Blink Left", "eyeBlinkLeft", "EyeBlinkLeft"),
            Create("eyeBlinkRight", "Eye Blink Right", "eyeBlinkRight", "EyeBlinkRight"),
            Create("eyeSquintLeft", "Eye Squint Left", "eyeSquintLeft", "EyeSquintLeft"),
            Create("eyeSquintRight", "Eye Squint Right", "eyeSquintRight", "EyeSquintRight"),
            Create("eyeWideLeft", "Eye Wide Left", "eyeWideLeft", "EyeWideLeft"),
            Create("eyeWideRight", "Eye Wide Right", "eyeWideRight", "EyeWideRight"),
            Create("cheekPuff", "Cheek Puff", "cheekPuff", "CheekPuff"),
            Create("cheekSquintLeft", "Cheek Squint Left", "cheekSquintLeft", "CheekSquintLeft"),
            Create("cheekSquintRight", "Cheek Squint Right", "cheekSquintRight", "CheekSquintRight"),
            Create("noseSneerLeft", "Nose Sneer Left", "noseSneerLeft", "NoseSneerLeft"),
            Create("noseSneerRight", "Nose Sneer Right", "noseSneerRight", "NoseSneerRight"),
            Create("jawOpen", "Jaw Open", "jawOpen", "JawOpen", "mouthOpen"),
            Create("mouthClose", "Mouth Close", "mouthClose", "MouthClose"),
            Create("mouthFunnel", "Mouth Funnel", "mouthFunnel", "MouthFunnel"),
            Create("mouthPucker", "Mouth Pucker", "mouthPucker", "MouthPucker"),
            Create("mouthSmileLeft", "Smile Left", "mouthSmileLeft", "mouthSmile_L", "SmileLeft"),
            Create("mouthSmileRight", "Smile Right", "mouthSmileRight", "mouthSmile_R", "SmileRight"),
            Create("mouthFrownLeft", "Frown Left", "mouthFrownLeft", "mouthFrown_L", "FrownLeft"),
            Create("mouthFrownRight", "Frown Right", "mouthFrownRight", "mouthFrown_R", "FrownRight"),
            Create("mouthDimpleLeft", "Dimple Left", "mouthDimpleLeft", "mouthDimple_L", "DimpleLeft"),
            Create("mouthDimpleRight", "Dimple Right", "mouthDimpleRight", "mouthDimple_R", "DimpleRight"),
            Create("mouthStretchLeft", "Mouth Stretch Left", "mouthStretchLeft", "mouthStretch_L", "StretchLeft"),
            Create("mouthStretchRight", "Mouth Stretch Right", "mouthStretchRight", "mouthStretch_R", "StretchRight"),
            Create("mouthRollUpper", "Roll Upper Lip", "mouthRollUpper", "MouthRollUpper"),
            Create("mouthRollLower", "Roll Lower Lip", "mouthRollLower", "MouthRollLower"),
            Create("mouthShrugUpper", "Shrug Upper Lip", "mouthShrugUpper", "MouthShrugUpper"),
            Create("mouthShrugLower", "Shrug Lower Lip", "mouthShrugLower", "MouthShrugLower")
        };

        public static IReadOnlyList<ControlDefinition> GetDefaultControls()
        {
            return DefaultControls;
        }

        public static List<string> GetBlendshapeNames(SkinnedMeshRenderer renderer)
        {
            var names = new List<string>();
            if (renderer == null || renderer.sharedMesh == null)
            {
                return names;
            }

            var mesh = renderer.sharedMesh;
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                names.Add(mesh.GetBlendShapeName(i));
            }

            return names;
        }

        public static Dictionary<string, string> AutoDetectBlendshapes(SkinnedMeshRenderer renderer)
        {
            var blendshapeNames = GetBlendshapeNames(renderer);
            return AutoDetectBlendshapes(blendshapeNames);
        }

        public static Dictionary<string, string> AutoDetectBlendshapes(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            var blendshapeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    foreach (var name in GetBlendshapeNames(renderer))
                    {
                        blendshapeNames.Add(name);
                    }
                }
            }

            return AutoDetectBlendshapes(blendshapeNames);
        }

        public static List<string> GetBlendshapeNames(IEnumerable<SkinnedMeshRenderer> renderers)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    foreach (var name in GetBlendshapeNames(renderer))
                    {
                        names.Add(name);
                    }
                }
            }

            return names.ToList();
        }

        public static bool IsMouthRelatedBlendshape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            return normalized.Contains("mouth") ||
                   normalized.Contains("jaw") ||
                   normalized.Contains("lip") ||
                   normalized.Contains("smile") ||
                   normalized.Contains("frown") ||
                   normalized.Contains("funnel") ||
                   normalized.Contains("pucker") ||
                   normalized.Contains("shrug") ||
                   normalized.Contains("roll") ||
                   normalized.Contains("press") ||
                   normalized.Contains("tongue") ||
                   normalized.Contains("viseme") ||
                   normalized.Contains("aa") ||
                   normalized.Contains("oh") ||
                   normalized.Contains("ou") ||
                   normalized.Contains("ee") ||
                   normalized.Contains("ih");
        }

        public static bool IsEyeRelatedBlendshape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            return normalized.Contains("eye") ||
                   normalized.Contains("brow") ||
                   normalized.Contains("cheek") ||
                   normalized.Contains("nose");
        }

        public static bool IsJawRelatedBlendshape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            return normalized.Contains("jaw");
        }

        public static bool IsTongueOrTeethRelatedBlendshape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            return normalized.Contains("tongue") ||
                   normalized.Contains("teeth") ||
                   normalized.Contains("tooth");
        }

        private static Dictionary<string, string> AutoDetectBlendshapes(IEnumerable<string> blendshapeNames)
        {
            var blendshapeNameList = blendshapeNames?.ToList() ?? new List<string>();
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var control in DefaultControls)
            {
                var detected = FindBestMatch(blendshapeNameList, control.candidateBlendshapeNames);
                if (!string.IsNullOrWhiteSpace(detected))
                {
                    result[control.key] = detected;
                }
            }

            return result;
        }

        public static Dictionary<string, float> BuildBlendshapeWeights(
            IReadOnlyDictionary<string, string> controlToBlendshapeMap,
            IReadOnlyDictionary<string, float> controlWeights)
        {
            return BuildBlendshapeWeights(controlToBlendshapeMap, controlWeights, null);
        }

        public static Dictionary<string, float> BuildBlendshapeWeights(
            IReadOnlyDictionary<string, string> controlToBlendshapeMap,
            IReadOnlyDictionary<string, float> controlWeights,
            IEnumerable<string> availableBlendshapeNames)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (controlToBlendshapeMap == null || controlWeights == null)
            {
                return result;
            }

            var availableNames = new HashSet<string>(availableBlendshapeNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var control in DefaultControls)
            {
                if (!controlToBlendshapeMap.TryGetValue(control.key, out var blendshapeName) || string.IsNullOrWhiteSpace(blendshapeName))
                {
                    continue;
                }

                if (!controlWeights.TryGetValue(control.key, out var weight) || weight <= 0.01f)
                {
                    continue;
                }

                result[blendshapeName] = weight;

                foreach (var alias in control.candidateBlendshapeNames)
                {
                    if (string.IsNullOrWhiteSpace(alias) || string.Equals(alias, blendshapeName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (availableNames.Count == 0 || availableNames.Contains(alias))
                    {
                        result[alias] = weight;
                    }
                }
            }

            return result;
        }

        public static Dictionary<string, float> LoadControlWeightsFromPreset(
            ExpressionPreset preset,
            IReadOnlyDictionary<string, string> controlToBlendshapeMap)
        {
            var result = DefaultControls.ToDictionary(control => control.key, _ => 0f, StringComparer.OrdinalIgnoreCase);
            if (preset == null || controlToBlendshapeMap == null)
            {
                return result;
            }

            var presetLookup = preset.blendshapeWeights.ToDictionary(weight => weight.blendshapeName, weight => weight.weight, StringComparer.OrdinalIgnoreCase);
            foreach (var control in DefaultControls)
            {
                if (controlToBlendshapeMap.TryGetValue(control.key, out var blendshapeName) &&
                    !string.IsNullOrWhiteSpace(blendshapeName) &&
                    presetLookup.TryGetValue(blendshapeName, out var weight))
                {
                    result[control.key] = weight;
                }
            }

            return result;
        }

        private static ControlDefinition Create(string key, string displayName, params string[] candidates)
        {
            return new ControlDefinition
            {
                key = key,
                displayName = displayName,
                candidateBlendshapeNames = candidates ?? Array.Empty<string>()
            };
        }

        private static string FindBestMatch(IReadOnlyList<string> blendshapeNames, IReadOnlyList<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                var exact = blendshapeNames.FirstOrDefault(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }
            }

            foreach (var candidate in candidates)
            {
                var normalizedCandidate = Normalize(candidate);
                var contains = blendshapeNames.FirstOrDefault(name =>
                    Normalize(name).IndexOf(normalizedCandidate, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrWhiteSpace(contains))
                {
                    return contains;
                }
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        }
    }
}
