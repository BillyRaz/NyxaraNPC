using System;
using System.Collections.Generic;

namespace Nyxara.AICompanion.Diagnostics
{
    [Serializable]
    public class SystemDiagnosticsReport
    {
        public string timestamp;
        public float durationMs;
        public ComponentStatus llmStatus = new();
        public ComponentStatus sttStatus = new();
        public ComponentStatus ttsStatus = new();
        public ComponentStatus faceStatus = new();
        public ComponentStatus lipSyncStatus = new();
        public ComponentStatus expressionStatus = new();
        public PerformanceMetrics performance = new();
        public List<ConfigIssue> configIssues = new();
        public List<PathValidation> pathValidations = new();
        public RuntimeSnapshot runtimeSnapshot = new();

        public bool IsHealthy =>
            llmStatus.isOperational &&
            sttStatus.isOperational &&
            ttsStatus.isOperational &&
            configIssues.Count == 0;
    }

    [Serializable]
    public class ComponentStatus
    {
        public string name;
        public bool isPresent;
        public bool isOperational;
        public string statusMessage;
        public float lastResponseTimeMs;
        public int errorCount;
        public string lastError;
    }

    [Serializable]
    public class PerformanceMetrics
    {
        public float averageLLMLatencyMs;
        public float averageSTTLatencyMs;
        public float averageTTSLatencyMs;
        public long memoryUsageMB;
        public float cpuUsagePercent;
        public int activeThreads;
        public int queueLength;
    }

    [Serializable]
    public class ConfigIssue
    {
        public IssueSeverity severity;
        public string component;
        public string issue;
        public string suggestion;
    }

    [Serializable]
    public class PathValidation
    {
        public string name;
        public string path;
        public bool exists;
        public long fileSizeMB;
        public string lastModified;
    }

    [Serializable]
    public class RuntimeSnapshot
    {
        public string currentMood;
        public float trust;
        public float affection;
        public float suspicion;
        public string currentTask;
        public bool isSpeaking;
        public bool isThinking;
        public string lastIntent;
        public string lastAction;
        public string lastSignal;
        public string lastDialogue;
        public float timeSinceLastResponse;
        public int memoryCount;
    }

    public enum IssueSeverity
    {
        Critical,
        Warning,
        Info
    }
}
