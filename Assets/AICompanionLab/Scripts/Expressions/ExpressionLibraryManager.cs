using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nyxara.AICompanion.Expressions
{
    public class ExpressionLibraryManager : MonoBehaviour
    {
        [SerializeField] private string expressionLibraryPath = "Assets/AICompanionStudio/Expressions";
        [SerializeField] private SkinnedMeshRenderer targetFaceRenderer;

        private List<ExpressionPreset> _loadedPresets = new();
        private Dictionary<string, ExpressionPreset> _presetLookup = new();

        public event Action<ExpressionPreset> OnPresetSelected;
        public event Action<IReadOnlyList<ExpressionPreset>> OnLibraryUpdated;

        public IReadOnlyList<ExpressionPreset> LoadedPresets => _loadedPresets;

        private void Awake()
        {
            LoadAllPresets();
        }

        public void LoadAllPresets()
        {
            _loadedPresets.Clear();
            _presetLookup.Clear();

#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets("t:ExpressionPreset", new[] { expressionLibraryPath });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<ExpressionPreset>(path);
                if (preset != null)
                {
                    _loadedPresets.Add(preset);
                    if (!string.IsNullOrWhiteSpace(preset.presetId))
                    {
                        _presetLookup[preset.presetId] = preset;
                    }
                }
            }
#endif

            _loadedPresets = _loadedPresets
                .OrderBy(p => p.category)
                .ThenBy(p => p.displayName)
                .ToList();

            OnLibraryUpdated?.Invoke(_loadedPresets);
        }

        public ExpressionPreset GetPreset(string presetId)
        {
            _presetLookup.TryGetValue(presetId, out var preset);
            return preset;
        }

        public void ApplyPreset(string presetId)
        {
            ApplyPreset(GetPreset(presetId));
        }

        public void ApplyPreset(ExpressionPreset preset)
        {
            if (preset == null || targetFaceRenderer == null)
            {
                return;
            }

            preset.ApplyToSkinnedMeshRenderer(targetFaceRenderer);
            OnPresetSelected?.Invoke(preset);
        }

        public void ResetToNeutral()
        {
            if (targetFaceRenderer == null || targetFaceRenderer.sharedMesh == null)
            {
                return;
            }

            var mesh = targetFaceRenderer.sharedMesh;
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                targetFaceRenderer.SetBlendShapeWeight(i, 0f);
            }
        }

        public List<ExpressionPreset> GetPresetsByCategory(ExpressionCategory category)
        {
            return _loadedPresets.Where(p => p.category == category).ToList();
        }
    }
}
