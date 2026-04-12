using System.Text;
using Nyxara.AICompanion.Data;

namespace Nyxara.AICompanion.Prompting
{
    public class PromptBuilder
    {
        private const string PromptTemplate = @"You are {name}.

Identity:
- Role: {role}
- Personality: {personality}
- Speech style: {speech_style}
- Background summary: {background}

Current State:
- Mood: {mood}
- Trust: {trust}
- Relationship: {relationship}
- Current task: {current_task}
- Danger level: {danger}

Player said:
{player_text}

Return exactly in this format:
Intent: <intent>
Mood: <mood>
Action: <action>
Signal: <signal>
Dialogue: <spoken dialogue only>

Rules:
- spoken dialogue only in Dialogue field
- no narration, no stage directions, no roleplay formatting
- no asterisks, no square brackets, no parentheses
- keep response short and natural";

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
                prompt.AppendLine("Recent Memory:");
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
