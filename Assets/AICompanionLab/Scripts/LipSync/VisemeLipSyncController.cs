using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Nyxara.AICompanion.LipSync
{
    public class VisemeLipSyncController : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer faceRenderer;
        [SerializeField] private LipSyncData lipSyncData;
        [SerializeField] private PiperTTSPhonemeExtractor phonemeExtractor;
        [SerializeField] private AudioSource audioSource;

        [Header("Runtime Settings")]
        [SerializeField] private bool enableLipSync = true;
        [SerializeField] private float mouthOpenAmount = 0.7f;

        private Coroutine _lipSyncCoroutine;
        private readonly Dictionary<string, int> _blendshapeIndexCache = new();

        public bool IsSpeaking { get; private set; }

        private void Awake()
        {
            CacheBlendshapeIndices();
        }

        private void CacheBlendshapeIndices()
        {
            if (faceRenderer == null || faceRenderer.sharedMesh == null || lipSyncData == null)
            {
                return;
            }

            _blendshapeIndexCache.Clear();
            var mesh = faceRenderer.sharedMesh;
            foreach (var mapping in lipSyncData.visemeMappings)
            {
                var index = mesh.GetBlendShapeIndex(mapping.blendshapeName);
                if (index >= 0)
                {
                    _blendshapeIndexCache[mapping.blendshapeName] = index;
                }
            }
        }

        public async Task SpeakWithLipSync(string text)
        {
            if (!enableLipSync || faceRenderer == null || lipSyncData == null || phonemeExtractor == null)
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
            if (faceRenderer == null || lipSyncData == null)
            {
                return;
            }

            foreach (var mapping in lipSyncData.visemeMappings)
            {
                if (_blendshapeIndexCache.TryGetValue(mapping.blendshapeName, out var index))
                {
                    faceRenderer.SetBlendShapeWeight(index, 0f);
                }
            }

            var mappingForViseme = lipSyncData.visemeMappings.Find(m => m.viseme == viseme);
            if (mappingForViseme != null && _blendshapeIndexCache.TryGetValue(mappingForViseme.blendshapeName, out var targetIndex))
            {
                faceRenderer.SetBlendShapeWeight(targetIndex, mappingForViseme.intensity);
            }

            ApplyJawOpen(viseme == Viseme.sil ? 0f : mouthOpenAmount);
        }

        private void ApplySmoothBlend(Viseme viseme, float t)
        {
            if (faceRenderer == null || lipSyncData == null)
            {
                return;
            }

            var mapping = lipSyncData.visemeMappings.Find(m => m.viseme == viseme);
            if (mapping != null && _blendshapeIndexCache.TryGetValue(mapping.blendshapeName, out var index))
            {
                var smoothedWeight = Mathf.SmoothStep(0f, mapping.intensity, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                faceRenderer.SetBlendShapeWeight(index, smoothedWeight);
            }
        }

        private void ApplyJawOpen(float amount)
        {
            var jawIndex = faceRenderer?.sharedMesh?.GetBlendShapeIndex("jawOpen") ?? -1;
            if (jawIndex >= 0 && lipSyncData != null)
            {
                faceRenderer.SetBlendShapeWeight(jawIndex, amount * lipSyncData.jawOpenMultiplier * 100f);
            }
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

        private void OnDestroy()
        {
            StopLipSync();
        }
    }
}
