// Copyright (c) 2026 Bilal Raza
// Publisher: RAZ Studio
// Product: Nyxara AI Studio

using System;

namespace Nyxara.AICompanion.Data
{
    [Serializable]
    public class NPCResponseData
    {
        public string intent = "neutral";
        public string mood = "calm";
        public string action = "none";
        public string signal = "none";
        public string dialogue = string.Empty;

        public bool IsValid => !string.IsNullOrWhiteSpace(dialogue);

        public override string ToString()
        {
            return $"[Intent:{intent} Mood:{mood} Action:{action} Signal:{signal}] {dialogue}";
        }
    }
}
