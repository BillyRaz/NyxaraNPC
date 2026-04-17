// Copyright (c) 2026 Bilal Raza
// Publisher: RAZ Studio
// Product: Nyxara AI Studio

using System.Text;
using Nyxara.AICompanion.Data;

namespace Nyxara.AICompanion.Prompting
{
    public class PromptBuilder
    {
        private const string PromptTemplate = @"You are {name}, a {role}.
Traits: {personality}
Style: {speech_style}
Background: {background}
State: mood={mood}, trust={trust}, relationship={relationship}, task={current_task}, danger={danger}
Player: {player_text}

Reply exactly as:
Intent: <intent>
Mood: <mood>
Action: <action>
Signal: <signal>
Dialogue: <short spoken dialogue>

Rules:
- Dialogue must be short, natural, and under 2 sentences
- no narration
- no stage directions
- no brackets or asterisks";

        public string BuildPrompt(CharacterProfileData profile, NPCRuntimeState state, string playerText, string recentMemory = "")
        {
            var personality = profile != null && profile.corePersonality != null && profile.corePersonality.Count > 0
                ? string.Join(", ", profile.corePersonality)
                : "friendly";

            var prompt = new StringBuilder(PromptTemplate);
            prompt.Replace("{name}", profile != null ? profile.characterName : "Nyxara");
            prompt.Replace("{role}", profile != null ? profile.role : "companion");
            prompt.Replace("{personality}", personality);
            prompt.Replace("{speech_style}", profile != null ? profile.speechStyle : "short, natural, expressive");
            prompt.Replace("{background}", profile != null ? profile.backgroundSummary : "An emotionally aware companion.");
            prompt.Replace("{mood}", state?.mood ?? profile?.defaultMood ?? "calm");
            prompt.Replace("{trust}", (state?.trust ?? 0.5f).ToString("0.00"));
            prompt.Replace("{relationship}", state?.relationship ?? "neutral");
            prompt.Replace("{current_task}", state?.currentTask ?? "talking");
            prompt.Replace("{danger}", (state?.dangerLevel ?? 0f).ToString("0.00"));
            prompt.Replace("{player_text}", playerText?.Trim() ?? "...");

            if (!string.IsNullOrWhiteSpace(recentMemory))
            {
                prompt.AppendLine();
                prompt.AppendLine("Memory:");
                prompt.AppendLine(recentMemory);
            }

            return prompt.ToString();
        }

        public string BuildMinimalPrompt(CharacterProfileData profile, string playerText)
        {
            var name = profile != null ? profile.characterName : "Nyxara";
            var background = profile != null ? profile.backgroundSummary : "an emotionally aware companion";
            return $@"You are {name}, {background}

Player: {playerText}

Return exactly:
Intent: <intent>
Mood: <mood>
Action: <action>
Signal: <signal>
Dialogue: <spoken dialogue>";
        }
    }
}
