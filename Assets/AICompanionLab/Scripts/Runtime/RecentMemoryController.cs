using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Nyxara.AICompanion.Runtime
{
    public class RecentMemoryController : MonoBehaviour
    {
        [SerializeField] private int maxMemoryEntries = 5;

        private readonly Queue<MemoryEntry> _memories = new();

        [System.Serializable]
        public class MemoryEntry
        {
            public string content;
            public float importance;
            public float timestamp;

            public MemoryEntry(string content, float importance)
            {
                this.content = content;
                this.importance = importance;
                timestamp = Time.time;
            }
        }

        public void AddMemory(string content, float importance = 0.5f)
        {
            var entry = new MemoryEntry(content, importance);
            _memories.Enqueue(entry);

            while (_memories.Count > maxMemoryEntries)
            {
                _memories.Dequeue();
            }
        }

        public void AddPlayerMessage(string message)
        {
            AddMemory($"Player said: {message}", 0.7f);
        }

        public void AddNPCResponse(string response, string intent)
        {
            AddMemory($"I responded ({intent}): {response}", 0.6f);
        }

        public void AddEvent(string eventDescription, float importance = 0.8f)
        {
            AddMemory($"Event: {eventDescription}", importance);
        }

        public string GetMemoryString()
        {
            if (_memories.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var index = 1;
            foreach (var memory in _memories)
            {
                sb.AppendLine($"{index}. {memory.content}");
                index++;
            }

            return sb.ToString();
        }

        public int GetMemoryCount()
        {
            return _memories.Count;
        }

        public void Clear()
        {
            _memories.Clear();
        }
    }
}
