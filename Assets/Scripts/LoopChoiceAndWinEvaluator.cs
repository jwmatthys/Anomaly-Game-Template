using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = SilentDebug;

public sealed class LoopChoiceAndWinEvaluator
{
    private readonly string _normalSceneName;
    private readonly string _endRoomSceneName;
    private readonly IReadOnlyList<string> _anomalySceneNames;
    private readonly float _anomalyChance;
    private readonly int _winRoomCount;
    private readonly bool _logValidationWarnings;
    private readonly UnityEngine.Object _logContext;

    private string _lastAnomalySceneName = string.Empty;

    public int CorrectCount { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsCurrentRoomAnomalous { get; private set; }

    public bool PendingLoopAdvance { get; private set; }
    public bool PendingChoiceWasCorrect { get; private set; }
    public HallwaySide PendingSourceHallway { get; private set; } = HallwaySide.NorthWest;
    public HallwayChoice? LastSubmittedChoice { get; private set; }
    public string PendingTargetSceneName { get; private set; } = string.Empty;

    public LoopChoiceAndWinEvaluator(
        string normalSceneName,
        string endRoomSceneName,
        IReadOnlyList<string> anomalySceneNames,
        float anomalyChance,
        int winRoomCount,
        bool logValidationWarnings,
        UnityEngine.Object logContext
    )
    {
        _normalSceneName = normalSceneName;
        _endRoomSceneName = endRoomSceneName;
        _anomalySceneNames = anomalySceneNames;
        _anomalyChance = Mathf.Clamp01(anomalyChance);
        _winRoomCount = Mathf.Max(1, winRoomCount);
        _logValidationWarnings = logValidationWarnings;
        _logContext = logContext;
    }

    public void StageChoice(HallwayChoice choice, HallwaySide sourceHallway)
    {
        LastSubmittedChoice = choice;
        PendingLoopAdvance = true;
        PendingSourceHallway = sourceHallway;

        PendingChoiceWasCorrect = IsChoiceCorrect(choice, IsCurrentRoomAnomalous);
        PendingTargetSceneName = PendingChoiceWasCorrect ? string.Empty : _normalSceneName;
    }

    public void CommitStagedChoice()
    {
        if (!LastSubmittedChoice.HasValue)
        {
            return;
        }

        AttemptCount++;
        if (PendingChoiceWasCorrect)
        {
            CorrectCount++;
        }
        else
        {
            CorrectCount = 0;
        }
    }

    public void CompleteLoopAdvance()
    {
        PendingLoopAdvance = false;
        PendingTargetSceneName = string.Empty;
        PendingChoiceWasCorrect = false;
    }

    public void SetCurrentRoomAnomalyState(bool hasAnomaly)
    {
        IsCurrentRoomAnomalous = hasAnomaly;
    }

    public void ResolveCurrentRoomAnomalyStateFromSceneName(string sceneName)
    {
        IsCurrentRoomAnomalous = !string.Equals(sceneName, _normalSceneName, StringComparison.Ordinal);
    }

    public string ResolvePendingTargetSceneName(string currentRoomSceneName, string initialRoomSceneName)
    {
        if (!string.IsNullOrWhiteSpace(PendingTargetSceneName))
        {
            return PendingTargetSceneName;
        }

        PendingTargetSceneName = PickNextRoomSceneName(currentRoomSceneName, initialRoomSceneName);
        return PendingTargetSceneName;
    }

    public void ResetForRestart()
    {
        CorrectCount = 0;
        AttemptCount = 0;
        IsCurrentRoomAnomalous = false;

        PendingLoopAdvance = false;
        PendingChoiceWasCorrect = false;
        PendingSourceHallway = HallwaySide.NorthWest;
        LastSubmittedChoice = null;
        PendingTargetSceneName = string.Empty;

        _lastAnomalySceneName = string.Empty;
    }

    private static bool IsChoiceCorrect(HallwayChoice choice, bool hasAnomaly)
    {
        if (hasAnomaly)
        {
            return choice == HallwayChoice.Anomaly;
        }

        return choice == HallwayChoice.NoAnomaly;
    }

    private string PickNextRoomSceneName(string currentRoomSceneName, string initialRoomSceneName)
    {
        if (ShouldRouteToEndRoom())
        {
            return ResolveEndRoomSceneName(currentRoomSceneName, initialRoomSceneName);
        }

        List<string> validAnomalyScenes = GetValidAnomalySceneNames();
        bool shouldPickAnomaly = validAnomalyScenes.Count > 0 && UnityEngine.Random.value < _anomalyChance;

        if (!shouldPickAnomaly)
        {
            return ResolveNormalRoomSceneName(currentRoomSceneName, initialRoomSceneName);
        }

        return PickAnomalySceneName(validAnomalyScenes);
    }

    private bool ShouldRouteToEndRoom()
    {
        // End room appears after the target streak is completed.
        return CorrectCount > _winRoomCount;
    }

    private string ResolveEndRoomSceneName(string currentRoomSceneName, string initialRoomSceneName)
    {
        if (IsSceneLoadable(_endRoomSceneName))
        {
            return _endRoomSceneName;
        }

        if (_logValidationWarnings)
        {
            Debug.LogWarning($"AnomalyLoopManager: End room scene '{_endRoomSceneName}' is not in Build Settings.", _logContext);
        }

        return ResolveNormalRoomSceneName(currentRoomSceneName, initialRoomSceneName);
    }

    private string PickAnomalySceneName(List<string> validAnomalyScenes)
    {
        if (validAnomalyScenes.Count == 1)
        {
            _lastAnomalySceneName = validAnomalyScenes[0];
            return _lastAnomalySceneName;
        }

        List<string> candidateScenes = new();
        for (int i = 0; i < validAnomalyScenes.Count; i++)
        {
            string sceneName = validAnomalyScenes[i];
            if (!string.Equals(sceneName, _lastAnomalySceneName, StringComparison.Ordinal))
            {
                candidateScenes.Add(sceneName);
            }
        }

        if (candidateScenes.Count == 0)
        {
            candidateScenes.AddRange(validAnomalyScenes);
        }

        string selectedScene = candidateScenes[UnityEngine.Random.Range(0, candidateScenes.Count)];
        _lastAnomalySceneName = selectedScene;
        return selectedScene;
    }

    private List<string> GetValidAnomalySceneNames()
    {
        List<string> validScenes = new();
        for (int i = 0; i < _anomalySceneNames.Count; i++)
        {
            string sceneName = _anomalySceneNames[i];
            if (!string.IsNullOrWhiteSpace(sceneName) && IsSceneLoadable(sceneName))
            {
                validScenes.Add(sceneName);
            }
        }

        return validScenes;
    }

    private string ResolveNormalRoomSceneName(string currentRoomSceneName, string initialRoomSceneName)
    {
        if (IsSceneLoadable(_normalSceneName))
        {
            return _normalSceneName;
        }

        if (IsSceneLoadable(initialRoomSceneName))
        {
            return initialRoomSceneName;
        }

        return currentRoomSceneName;
    }

    private static bool IsSceneLoadable(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
