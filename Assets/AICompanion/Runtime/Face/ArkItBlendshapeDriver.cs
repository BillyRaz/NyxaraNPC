using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Nyxara.AICompanion.Face
{
    public class ArkItBlendshapeDriver : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private List<SkinnedMeshRenderer> additionalRenderers = new();
        [SerializeField] private string jawOpenBlendshape = "jawOpen";
        [SerializeField] private string mouthFunnelBlendshape = "mouthFunnel";
        [SerializeField] private string browInnerUpBlendshape = "browInnerUp";
        [SerializeField] private float speakingWeight = 65f;
        [SerializeField] private float thinkingWeight = 25f;
        [SerializeField] private bool expressionModeActive;

        public SkinnedMeshRenderer TargetRenderer => targetRenderer;
        public IReadOnlyList<SkinnedMeshRenderer> TargetRenderers => GetAllRenderers();

        public void SetSpeaking(bool active)
        {
            if (expressionModeActive)
            {
                return;
            }

            SetBlendshapeWeight(jawOpenBlendshape, active ? speakingWeight : 0f);
            SetBlendshapeWeight(mouthFunnelBlendshape, active ? speakingWeight * 0.6f : 0f);
        }

        public void SetThinking(bool active)
        {
            if (expressionModeActive)
            {
                return;
            }

            SetBlendshapeWeight(browInnerUpBlendshape, active ? thinkingWeight : 0f);
        }

        public void SetBlendshapeWeight(string blendshapeName, float weight)
        {
            if (string.IsNullOrWhiteSpace(blendshapeName))
            {
                return;
            }

            foreach (var renderer in GetAllRenderers())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var index = renderer.sharedMesh.GetBlendShapeIndex(blendshapeName);
                if (index >= 0)
                {
                    renderer.SetBlendShapeWeight(index, weight);
                }
            }
        }

        public bool TrySetBlendshapeWeight(string blendshapeName, float weight)
        {
            if (string.IsNullOrWhiteSpace(blendshapeName))
            {
                return false;
            }

            var applied = false;
            foreach (var renderer in GetAllRenderers())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var index = renderer.sharedMesh.GetBlendShapeIndex(blendshapeName);
                if (index < 0)
                {
                    continue;
                }

                renderer.SetBlendShapeWeight(index, weight);
                applied = true;
            }

            return applied;
        }

        public float GetBlendshapeWeight(string blendshapeName)
        {
            if (string.IsNullOrWhiteSpace(blendshapeName))
            {
                return 0f;
            }

            var weight = 0f;
            foreach (var renderer in GetAllRenderers())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var index = renderer.sharedMesh.GetBlendShapeIndex(blendshapeName);
                if (index >= 0)
                {
                    weight = Mathf.Max(weight, renderer.GetBlendShapeWeight(index));
                }
            }

            return weight;
        }

        public bool HasBlendshape(string blendshapeName)
        {
            if (string.IsNullOrWhiteSpace(blendshapeName))
            {
                return false;
            }

            return GetAllRenderers()
                .Any(renderer => renderer != null &&
                                 renderer.sharedMesh != null &&
                                 renderer.sharedMesh.GetBlendShapeIndex(blendshapeName) >= 0);
        }

        public List<string> GetBlendshapeNames()
        {
            var names = new HashSet<string>();
            foreach (var renderer in GetAllRenderers())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    continue;
                }

                var mesh = renderer.sharedMesh;
                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    names.Add(mesh.GetBlendShapeName(i));
                }
            }

            return names.ToList();
        }

        public IEnumerator ReleaseSpeakingWhenSourceStops(AudioSource source)
        {
            if (source == null)
            {
                yield break;
            }

            while (source.isPlaying)
            {
                yield return null;
            }

            SetSpeaking(false);
        }

        public void SetExpressionMode(bool active)
        {
            expressionModeActive = active;
            if (expressionModeActive)
            {
                SetBlendshapeWeight(jawOpenBlendshape, 0f);
                SetBlendshapeWeight(mouthFunnelBlendshape, 0f);
                SetBlendshapeWeight(browInnerUpBlendshape, 0f);
            }
        }

        private List<SkinnedMeshRenderer> GetAllRenderers()
        {
            var renderers = new List<SkinnedMeshRenderer>();
            if (targetRenderer != null)
            {
                renderers.Add(targetRenderer);
            }

            foreach (var renderer in additionalRenderers)
            {
                if (renderer != null && !renderers.Contains(renderer))
                {
                    renderers.Add(renderer);
                }
            }

            return renderers;
        }
    }
}
