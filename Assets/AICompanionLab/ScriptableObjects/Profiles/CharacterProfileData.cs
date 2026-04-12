using System.Collections.Generic;
using UnityEngine;

namespace Nyxara.AICompanion.Data
{
    [CreateAssetMenu(fileName = "NewCharacterProfile", menuName = "AI Companion/Character Profile")]
    public class CharacterProfileData : ScriptableObject
    {
        [Header("Identity")]
        public string characterName = "Nyxara";
        public string role = "companion";

        [Header("Personality")]
        public List<string> corePersonality = new() { "witty", "protective", "curious" };
        public string speechStyle = "short, natural, expressive";
        public string backgroundSummary = "A sharp, emotionally aware companion who reads the player well.";

        [Header("Preferences")]
        public List<string> likes = new() { "bravery", "honesty", "playfulness" };
        public List<string> dislikes = new() { "cowardice", "arrogance" };

        [Header("Voice & Expression")]
        public string defaultMood = "calm";
        [Range(0f, 1f)] public float flirtLevel = 0.3f;
        [Range(0f, 1f)] public float teaseLevel = 0.5f;
        public string voiceProfileId = "young_warm_female";
        public string expressionProfileId = "social_readable";
    }
}
