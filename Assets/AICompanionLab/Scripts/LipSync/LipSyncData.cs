using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nyxara.AICompanion.LipSync
{
    [CreateAssetMenu(fileName = "NewLipSyncData", menuName = "AI Companion/Lip Sync Data")]
    public class LipSyncData : ScriptableObject
    {
        [Header("Viseme to Blendshape Mapping")]
        public List<VisemeMapping> visemeMappings = new();

        [Header("Animation Settings")]
        public float smoothTime = 0.05f;
        public float jawOpenMultiplier = 0.7f;

        [Header("Debug")]
        public bool showDebugInfo;
    }

    [Serializable]
    public class VisemeMapping
    {
        public Viseme viseme;
        public string blendshapeName;
        [Range(0f, 100f)] public float intensity = 100f;
        public float jawOpenContribution;
    }

    public enum Viseme
    {
        sil,
        AA,
        IY,
        UH,
        OW,
        EH,
        IH,
        AH,
        AW,
        EY,
        ER,
        AO,
        OY,
        TH,
        DH,
        FV,
        SZ,
        SH,
        HH,
        M,
        N,
        NG,
        L,
        R,
        Y,
        W,
        BPM,
        DT,
        GK
    }
}
