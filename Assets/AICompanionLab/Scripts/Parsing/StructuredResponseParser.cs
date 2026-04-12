using System;
using System.Text.RegularExpressions;
using Nyxara.AICompanion.Data;

namespace Nyxara.AICompanion.Parsing
{
    public static class StructuredResponseParser
    {
        private static readonly Regex IntentRegex = new(@"Intent:\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MoodRegex = new(@"Mood:\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ActionRegex = new(@"Action:\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SignalRegex = new(@"Signal:\s*(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DialogueRegex = new(@"Dialogue:\s*(.+?)(?=\n\w+:|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly string[] ValidIntents = { "neutral", "question", "tease", "flirt", "reassure", "warn", "suspicious", "friendly", "follow_accept", "follow_reject" };
        private static readonly string[] ValidMoods = { "calm", "playful", "warm", "guarded", "tense", "confident", "curious" };
        private static readonly string[] ValidActions = { "none", "follow", "wait", "stop", "focus_player", "warn" };
        private static readonly string[] ValidSignals = { "none", "smile", "eyebrow_raise", "head_tilt", "suspicious_look", "shy_smile", "amused_smirk", "concerned", "bold_stare" };

        public static NPCResponseData Parse(string rawResponse, NPCRuntimeState currentState)
        {
            var result = new NPCResponseData();
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                result.dialogue = "...";
                return result;
            }

            result.intent = ParseField(IntentRegex, rawResponse, "neutral", ValidIntents);
            result.mood = ParseField(MoodRegex, rawResponse, currentState?.mood ?? "calm", ValidMoods);
            result.action = ParseField(ActionRegex, rawResponse, "none", ValidActions);
            result.signal = ParseField(SignalRegex, rawResponse, "none", ValidSignals);
            result.dialogue = DialogueSanitizer.Sanitize(ParseDialogue(rawResponse));

            if (string.IsNullOrWhiteSpace(result.dialogue))
            {
                result.dialogue = "I'm not sure what to say right now.";
            }

            return result;
        }

        private static string ParseField(Regex regex, string input, string defaultValue, string[] validValues)
        {
            var match = regex.Match(input);
            if (!match.Success)
            {
                return defaultValue;
            }

            var value = match.Groups[1].Value.Trim().ToLowerInvariant();
            foreach (var valid in validValues)
            {
                if (string.Equals(value, valid, StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return defaultValue;
        }

        private static string ParseDialogue(string input)
        {
            var match = DialogueRegex.Match(input);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            var lines = input.Split('\n');
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line) &&
                    !line.StartsWith("Intent:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("Mood:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("Action:", StringComparison.OrdinalIgnoreCase) &&
                    !line.StartsWith("Signal:", StringComparison.OrdinalIgnoreCase))
                {
                    return line;
                }
            }

            return string.Empty;
        }
    }
}
