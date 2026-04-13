using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Nyxara.AICompanion.LipSync
{
    public class VisemeLipSyncController : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private List<SkinnedMeshRenderer> additionalFaceRenderers = new();
        [SerializeField] private LipSyncData lipSyncData;
        [SerializeField] private PiperTTSPhonemeExtractor phonemeExtractor;
        [SerializeField] private AudioSource audioSource;

        [Header("Runtime Settings")]
        [SerializeField] private bool enableLipSync = true;
        [SerializeField] private float mouthOpenAmount = 0.7f;
        [SerializeField] private bool expressionModeActive;
        [SerializeField] private float lowerLipDropAmount = 0.35f;
        [SerializeField] private float upperLipRaiseAmount = 0.18f;
        [SerializeField] private float mouthStretchAmount = 0.12f;

        private Coroutine _lipSyncCoroutine;

        public bool IsSpeaking { get; private set; }

        private void Awake()
        {
            CacheBlendshapeIndices();
        }

        private void CacheBlendshapeIndices()
        {
            if (GetAllRenderers().Count == 0 || lipSyncData == null)
            {
                return;
            }
        }

        public async Task SpeakWithLipSync(string text)
        {
            if (expressionModeActive)
            {
                StopLipSync();
                return;
            }

            if (!enableLipSync || GetAllRenderers().Count == 0 || lipSyncData == null || phonemeExtractor == null)
            {
                if (_lipSyncCoroutine != null)
                {
                    StopCoroutine(_lipSyncCoroutine);
                }

                _lipSyncCoroutine = StartCoroutine(SimpleJawMovement());
                return;
            }

            if (_lipSyncCoroutine != null)
            {
                StopCoroutine(_lipSyncCoroutine);
            }

            IsSpeaking = true;
            var phonemeTimeline = await phonemeExtractor.ExtractPhonemesFromText(text);
            _lipSyncCoroutine = StartCoroutine(ProcessLipSyncTimeline(phonemeTimeline));
        }

        private IEnumerator ProcessLipSyncTimeline(List<PiperTTSPhonemeExtractor.VisemeFrame> timeline)
        {
            foreach (var frame in timeline)
            {
                ApplyViseme(frame.viseme);
                var elapsed = 0f;
                while (elapsed < frame.duration)
                {
                    ApplySmoothBlend(frame.viseme, frame.duration <= 0f ? 1f : elapsed / frame.duration);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            ApplyViseme(Viseme.sil);
            _lipSyncCoroutine = null;
            IsSpeaking = false;
        }

        private void ApplyViseme(Viseme viseme)
        {
            if (GetAllRenderers().Count == 0 || lipSyncData == null)
            {
                return;
            }

            foreach (var mapping in lipSyncData.visemeMappings)
            {
                SetBlendshapeWeight(mapping.blendshapeName, 0f);
            }

            var mappingForViseme = lipSyncData.visemeMappings.Find(m => m.viseme == viseme);
            if (mappingForViseme != null)
            {
                SetBlendshapeWeight(mappingForViseme.blendshapeName, mappingForViseme.intensity);
            }

            ApplyJawOpen(viseme == Viseme.sil ? 0f : mouthOpenAmount);
        }

        private void ApplySmoothBlend(Viseme viseme, float t)
        {
            if (GetAllRenderers().Count == 0 || lipSyncData == null)
            {
                return;
            }

            var mapping = lipSyncData.visemeMappings.Find(m => m.viseme == viseme);
            if (mapping != null)
            {
                var smoothedWeight = Mathf.SmoothStep(0f, mapping.intensity, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                SetBlendshapeWeight(mapping.blendshapeName, smoothedWeight);
            }
        }

        private void ApplyJawOpen(float amount)
        {
            if (lipSyncData == null)
            {
                return;
            }

            var jawWeight = amount * lipSyncData.jawOpenMultiplier * 100f;
            SetBlendshapeWeight("jawOpen", jawWeight);

            // These helpers expose the teeth/opening more naturally during speech.
            SetBlendshapeWeight("mouthLowerDownLeft", jawWeight * lowerLipDropAmount);
            SetBlendshapeWeight("mouthLowerDownRight", jawWeight * lowerLipDropAmount);
            SetBlendshapeWeight("mouthUpperUpLeft", jawWeight * upperLipRaiseAmount);
            SetBlendshapeWeight("mouthUpperUpRight", jawWeight * upperLipRaiseAmount);
            SetBlendshapeWeight("mouthStretchLeft", jawWeight * mouthStretchAmount);
            SetBlendshapeWeight("mouthStretchRight", jawWeight * mouthStretchAmount);
        }

        private IEnumerator SimpleJawMovement()
        {
            IsSpeaking = true;
            var timer = 0f;
            const float duration = 1f;
            while (timer < duration)
            {
                var jawWeight = Mathf.PingPong(timer * 20f, 50f);
                ApplyJawOpen(jawWeight / 100f);
                timer += Time.deltaTime;
                yield return null;
            }

            ApplyJawOpen(0f);
            IsSpeaking = false;
        }

        public void StopLipSync()
        {
            if (_lipSyncCoroutine != null)
            {
                StopCoroutine(_lipSyncCoroutine);
                _lipSyncCoroutine = null;
            }

            ApplyViseme(Viseme.sil);
            IsSpeaking = false;
        }

        public void SetExpressionMode(bool active)
        {
            expressionModeActive = active;
            if (expressionModeActive)
            {
                StopLipSync();
            }
        }

        private void OnDestroy()
        {
            StopLipSync();
        }

        private void SetBlendshapeWeight(string blendshapeName, float weight)
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

        private List<SkinnedMeshRenderer> GetAllRenderers()
        {
            var renderers = new List<SkinnedMeshRenderer>();
            if (faceRenderer != null)
            {
                renderers.Add(faceRenderer);
            }

            foreach (var renderer in additionalFaceRenderers)
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
