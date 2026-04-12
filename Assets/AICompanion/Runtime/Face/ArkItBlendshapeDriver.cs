using System.Collections;
using UnityEngine;

namespace Nyxara.AICompanion.Face
{
    public class ArkItBlendshapeDriver : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private string jawOpenBlendshape = "jawOpen";
        [SerializeField] private string mouthFunnelBlendshape = "mouthFunnel";
        [SerializeField] private string browInnerUpBlendshape = "browInnerUp";
        [SerializeField] private float speakingWeight = 65f;
        [SerializeField] private float thinkingWeight = 25f;

        private int _jawOpenIndex = -1;
        private int _mouthFunnelIndex = -1;
        private int _browInnerUpIndex = -1;

        public void SetSpeaking(bool active)
        {
            EnsureIndices();
            SetWeight(_jawOpenIndex, active ? speakingWeight : 0f);
            SetWeight(_mouthFunnelIndex, active ? speakingWeight * 0.6f : 0f);
        }

        public void SetThinking(bool active)
        {
            EnsureIndices();
            SetWeight(_browInnerUpIndex, active ? thinkingWeight : 0f);
        }

        public void SetBlendshapeWeight(string blendshapeName, float weight)
        {
            EnsureIndices();
            if (targetRenderer == null || targetRenderer.sharedMesh == null || string.IsNullOrWhiteSpace(blendshapeName))
            {
                return;
            }

            var index = targetRenderer.sharedMesh.GetBlendShapeIndex(blendshapeName);
            if (index >= 0)
            {
                targetRenderer.SetBlendShapeWeight(index, weight);
            }
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

        private void EnsureIndices()
        {
            if (targetRenderer == null || targetRenderer.sharedMesh == null)
            {
                return;
            }

            if (_jawOpenIndex >= 0)
            {
                return;
            }

            _jawOpenIndex = targetRenderer.sharedMesh.GetBlendShapeIndex(jawOpenBlendshape);
            _mouthFunnelIndex = targetRenderer.sharedMesh.GetBlendShapeIndex(mouthFunnelBlendshape);
            _browInnerUpIndex = targetRenderer.sharedMesh.GetBlendShapeIndex(browInnerUpBlendshape);
        }

        private void SetWeight(int index, float value)
        {
            if (targetRenderer == null || index < 0)
            {
                return;
            }

            targetRenderer.SetBlendShapeWeight(index, value);
        }
    }
}
