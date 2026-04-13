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
        [SerializeField] private float mouthOpenAmount = 0.45f;
        [SerializeField] private bool expressionModeActive;
        [SerializeField] private float visemeIntensityScale = 0.6f;
        [SerializeField] private float lowerLipDropAmount = 0.18f;
        [SerializeField] private float upperLipRaiseAmount = 0.08f;
        [SerializeField] private float mouthStretchAmount = 0.06f;
        [SerializeField] private float releaseDuration = 0.08f;

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

        public async Task SpeakWithLipSync(string text, float clipDuration = -1f)
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
            phonemeTimeline = RetimeTimelineToClipDuration(phonemeTimeline, clipDuration);
            _lipSyncCoroutine = StartCoroutine(ProcessLipSyncTimeline(phonemeTimeline));
        }

        private IEnumerator ProcessLipSyncTimeline(List<PiperTTSPhonemeExtractor.VisemeFrame> timeline)
        {
            if (timeline == null || timeline.Count == 0)
            {
                yield return ReleaseToSilence();
                _lipSyncCoroutine = null;
                IsSpeaking = false;
                yield break;
            }

            for (var i = 0; i < timeline.Count; i++)
            {
                var currentFrame = timeline[i];
                var nextFrame = i + 1 < timeline.Count ? timeline[i + 1] : new PiperTTSPhonemeExtractor.VisemeFrame
                {
                    viseme = Viseme.sil,
                    duration = releaseDuration
                };

                var duration = Mathf.Max(0.02f, currentFrame.duration);
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    var t = Mathf.Clamp01(elapsed / duration);
                    ApplyFrameBlend(currentFrame.viseme, nextFrame.viseme, Mathf.SmoothStep(0f, 1f, t));
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            yield return ReleaseToSilence();
            _lipSyncCoroutine = null;
            IsSpeaking = false;
        }

        private void ApplyViseme(Viseme viseme)
        {
            if (GetAllRenderers().Count == 0 || lipSyncData == null)
            {
                return;
            }

            ResetMappedBlendshapes();

            var mappingForViseme = lipSyncData.visemeMappings.Find(m => m.viseme == viseme);
            if (mappingForViseme != null)
            {
                SetMappingWeight(mappingForViseme, mappingForViseme.intensity * visemeIntensityScale);
            }

            var jawContribution = mappingForViseme != null ? mappingForViseme.jawOpenContribution : 0f;
            ApplyJawOpen(jawContribution * mouthOpenAmount);
        }

        private void ApplyFrameBlend(Viseme currentViseme, Viseme nextViseme, float t)
        {
            if (GetAllRenderers().Count == 0 || lipSyncData == null)
            {
                return;
            }

            ResetMappedBlendshapes();

            var currentMapping = ResolveMapping(currentViseme);
            var nextMapping = ResolveMapping(nextViseme);

            if (currentMapping != null)
            {
                var currentWeight = Mathf.Lerp(currentMapping.intensity * visemeIntensityScale, 0f, t);
                SetMappingWeight(currentMapping, currentWeight);
            }

            if (nextMapping != null && nextMapping != currentMapping)
            {
                var nextWeight = Mathf.Lerp(0f, nextMapping.intensity * visemeIntensityScale, t);
                SetMappingWeight(nextMapping, nextWeight);
            }

            var currentJaw = currentMapping != null ? currentMapping.jawOpenContribution : 0f;
            var nextJaw = nextMapping != null ? nextMapping.jawOpenContribution : 0f;
            ApplyJawOpen(Mathf.Lerp(currentJaw, nextJaw, t) * mouthOpenAmount);
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
                var jawWeight = Mathf.PingPong(timer * 8f, 28f);
                ApplyJawOpen(jawWeight / 100f);
                timer += Time.deltaTime;
                yield return null;
            }

            yield return ReleaseToSilence();
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

        private List<PiperTTSPhonemeExtractor.VisemeFrame> RetimeTimelineToClipDuration(List<PiperTTSPhonemeExtractor.VisemeFrame> timeline, float clipDuration)
        {
            if (timeline == null || timeline.Count == 0 || clipDuration <= 0f)
            {
                return timeline ?? new List<PiperTTSPhonemeExtractor.VisemeFrame>();
            }

            var sourceDuration = timeline.Sum(frame => Mathf.Max(0f, frame.duration));
            if (sourceDuration <= 0.001f)
            {
                return timeline;
            }

            var scale = clipDuration / sourceDuration;
            var retimed = new List<PiperTTSPhonemeExtractor.VisemeFrame>(timeline.Count);
            var timestamp = 0f;
            foreach (var frame in timeline)
            {
                var duration = Mathf.Max(0.02f, frame.duration * scale);
                retimed.Add(new PiperTTSPhonemeExtractor.VisemeFrame
                {
                    viseme = frame.viseme,
                    timestamp = timestamp,
                    duration = duration
                });
                timestamp += duration;
            }

            return retimed;
        }

        private IEnumerator ReleaseToSilence()
        {
            var elapsed = 0f;
            while (elapsed < releaseDuration)
            {
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, releaseDuration));
                ApplyFrameBlend(Viseme.sil, Viseme.sil, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            ResetMappedBlendshapes();
            ApplyJawOpen(0f);
        }

        private VisemeMapping ResolveMapping(Viseme viseme)
        {
            return lipSyncData != null ? lipSyncData.visemeMappings.Find(m => m.viseme == viseme) : null;
        }

        private void ResetMappedBlendshapes()
        {
            if (lipSyncData == null)
            {
                return;
            }

            foreach (var mapping in lipSyncData.visemeMappings)
            {
                SetMappingWeight(mapping, 0f);
            }
        }

        private void SetMappingWeight(VisemeMapping mapping, float weight)
        {
            if (mapping == null)
            {
                return;
            }

            foreach (var blendshapeName in mapping.EnumerateBlendshapeNames())
            {
                SetBlendshapeWeight(blendshapeName, weight);
            }
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
