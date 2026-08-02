using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = SilentDebug;

internal static class SilentDebug
{
    public static void Log(object message, UnityEngine.Object context = null)
    {
        _ = message;
        _ = context;
    }

    public static void LogWarning(object message, UnityEngine.Object context = null)
    {
        _ = message;
        _ = context;
    }
}

public enum HallwayChoice
{
    NoAnomaly,
    Anomaly
}

public enum LoopSpawnPoint
{
    NorthWestEntry,
    SouthEastEntry
}

public enum HallwaySide
{
    NorthWest,
    SouthEast
}

public enum BootstrapHallwayFrame
{
    PreloadZone,
    NorthWestMount,
    SouthEastMount
}

public class AnomalyLoopManager : MonoBehaviour
{
    public static AnomalyLoopManager Instance { get; private set; }

    [Header("Room Selection")]
    [SerializeField] private string normalSceneName = "TemplateRoom";
    [SerializeField] private List<string> anomalySceneNames = new();
    [SerializeField, Range(0f, 1f)] private float anomalyChance = 0.5f;
    [SerializeField] private string initialRoomSceneName = "TemplateRoom";

    [Header("Win Condition")]
    [SerializeField, Min(1)] private int winRoomCount = 10;
    [SerializeField] private string endRoomSceneName = "End Room";

    [Header("Room Placement")]
    [SerializeField] private Transform northWestRoomMountPoint;
    [SerializeField] private Transform southEastRoomMountPoint;

    [Header("Hallway Frames")]
    [SerializeField] private Transform preloadZoneFrame;

    [Header("Bootstrap Connection")]
    [SerializeField] private bool loadBootstrapConnectedSceneOnStart = true;
    [SerializeField] private string bootstrapConnectedSceneName = "Starting Room";
    [SerializeField] private HallwaySide bootstrapConnectedSceneAnchorSide = HallwaySide.NorthWest;
    [SerializeField] private HallwaySide bootstrapHallwayMountSide = HallwaySide.SouthEast;
    [SerializeField] private string bootstrapHallwayMountPointName = "Starting Room Entry Point";
    [SerializeField] private bool unloadBootstrapConnectedSceneAfterInitialRoomLoad = true;
    [SerializeField] private string northWestEntryPointName = "NW Entry Point";
    [SerializeField] private string southEastEntryPointName = "SE Entry Point";

    [Header("Validation")]
    [SerializeField] private bool logValidationWarnings = true;
    [SerializeField] private bool warnOnOppositeSeamMismatch = false;

    [Header("Debug")]
    [SerializeField] private bool logChoiceCounterDiagnostics = true;

    public int CorrectCount => _choiceAndWinEvaluator != null ? _choiceAndWinEvaluator.CorrectCount : 0;
    public int AttemptCount => _choiceAndWinEvaluator != null ? _choiceAndWinEvaluator.AttemptCount : 0;
    public bool IsCurrentRoomAnomalous => _choiceAndWinEvaluator != null && _choiceAndWinEvaluator.IsCurrentRoomAnomalous;
    public bool AreChoicesArmed { get; private set; }
    public bool IsTransitionInProgress => _transitionInProgress;
    public string ActiveSceneName => SceneManager.GetActiveScene().name;
    public string CurrentRoomSceneName => _currentRoomSceneName;
    public string EndRoomSceneName => endRoomSceneName;
    public bool HasPendingLoopAdvance => _choiceAndWinEvaluator != null && _choiceAndWinEvaluator.PendingLoopAdvance;
    public HallwayChoice? LastSubmittedChoice => _choiceAndWinEvaluator != null ? _choiceAndWinEvaluator.LastSubmittedChoice : null;
    public bool IsHallwayMirrorTransportArmed => _hallwayMirrorTriggerController.IsMirrorTransportArmed;

    public event Action<int, int> ScoreChanged;

    private bool _transitionInProgress;
    private bool _processingMidHallwayZone;

    private string _currentRoomSceneName = string.Empty;
    private string _bootstrapSceneName = string.Empty;

    private RoomLoopSceneContext _currentContext;
    private LoopChoiceAndWinEvaluator _choiceAndWinEvaluator;
    private RoomPlacementCoordinator _roomPlacementCoordinator;
    private LoopRoomStreamingCoordinator _roomStreamingCoordinator;
    private LoopBootstrapConnectionCoordinator _bootstrapConnectionCoordinator;
    private readonly HallwayMirrorTriggerController _hallwayMirrorTriggerController = new();

    private const bool PersistAcrossSceneLoads = true;
    private const bool RequireMainRoomArmingBeforeChoices = true;
    private const bool RequirePreloadZonesForSeamlessTransition = true;
    private const int FramesBeforeSceneActivation = 1;
    private const int FramesBeforeOldRoomUnload = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _bootstrapSceneName = SceneManager.GetActiveScene().name;

        if (PersistAcrossSceneLoads)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (preloadZoneFrame == null)
        {
            preloadZoneFrame = ResolvePreloadZoneFrame();
        }

        InitializeControllers();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void Start()
    {
        InitializeControllers();
        AreChoicesArmed = !RequireMainRoomArmingBeforeChoices;
        ValidateStaticSetup();
        PublishScore();

        if (loadBootstrapConnectedSceneOnStart)
        {
            StartCoroutine(EnsureBootstrapConnectedSceneLoadedRoutine());
        }
    }

    private void InitializeControllers()
    {
        if (_choiceAndWinEvaluator == null)
        {
            _choiceAndWinEvaluator = new LoopChoiceAndWinEvaluator(
                normalSceneName,
                endRoomSceneName,
                anomalySceneNames,
                anomalyChance,
                winRoomCount,
                logValidationWarnings,
                this
            );
        }

        if (_roomPlacementCoordinator == null)
        {
            _roomPlacementCoordinator = new RoomPlacementCoordinator(
                northWestRoomMountPoint,
                southEastRoomMountPoint,
                northWestEntryPointName,
                southEastEntryPointName,
                logValidationWarnings,
                warnOnOppositeSeamMismatch,
                this
            );
        }

        if (_roomStreamingCoordinator == null)
        {
            _roomStreamingCoordinator = new LoopRoomStreamingCoordinator(
                logValidationWarnings,
                FramesBeforeSceneActivation,
                this
            );
        }

        if (_bootstrapConnectionCoordinator == null)
        {
            _bootstrapConnectionCoordinator = new LoopBootstrapConnectionCoordinator(
                logValidationWarnings,
                this
            );
        }
    }

    public void SubmitChoice(HallwayChoice choice)
    {
        SubmitChoice(choice, InferSourceHallway(choice));
    }

    public void SubmitChoice(HallwayChoice choice, HallwaySide sourceHallway)
    {
        InitializeControllers();

        if (_transitionInProgress || !AreChoicesArmed || string.IsNullOrWhiteSpace(_currentRoomSceneName))
        {
            if (logChoiceCounterDiagnostics)
            {
                Debug.Log(
                    "AnomalyLoopManager choice ignored:" +
                    $" choice={choice}" +
                    $" sourceHall={sourceHallway}" +
                    $" transitionInProgress={_transitionInProgress}" +
                    $" areChoicesArmed={AreChoicesArmed}" +
                    $" currentRoomScene={(string.IsNullOrWhiteSpace(_currentRoomSceneName) ? "None" : _currentRoomSceneName)}" +
                    $" correctCount={CorrectCount}" +
                    $" attemptCount={AttemptCount}",
                    this
                );
            }

            return;
        }

        int correctBefore = CorrectCount;
        int attemptsBefore = AttemptCount;

        _choiceAndWinEvaluator.StageChoice(choice, sourceHallway);
        _hallwayMirrorTriggerController.DisarmMirrorTransport();

        if (logChoiceCounterDiagnostics)
        {
            Debug.Log(
                "AnomalyLoopManager choice staged (last trigger wins):" +
                $" choice={choice}" +
                $" sourceHall={sourceHallway}" +
                $" roomHasAnomaly={IsCurrentRoomAnomalous}" +
                $" wasCorrect={_choiceAndWinEvaluator.PendingChoiceWasCorrect}" +
                $" correctBefore={correctBefore}" +
                $" correctAfter={CorrectCount}" +
                $" attemptsBefore={attemptsBefore}" +
                $" attemptsAfter={AttemptCount}" +
                $" pendingTarget={(string.IsNullOrWhiteSpace(_choiceAndWinEvaluator.PendingTargetSceneName) ? "(random)" : _choiceAndWinEvaluator.PendingTargetSceneName)}",
                this
            );
        }
    }

    public void EnterBlindSpotZone()
    {
        if (_transitionInProgress || _processingMidHallwayZone)
        {
            if (logChoiceCounterDiagnostics)
            {
                Debug.Log(
                    "AnomalyLoopManager blind-spot ignored:" +
                    $" transitionInProgress={_transitionInProgress}" +
                    $" processingMidHallway={_processingMidHallwayZone}" +
                    $" pendingLoopAdvance={HasPendingLoopAdvance}",
                    this
                );
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_currentRoomSceneName))
        {
            if (logChoiceCounterDiagnostics)
            {
                Debug.Log("AnomalyLoopManager blind-spot entered: initializing first room load.", this);
            }

            StartCoroutine(LoadInitialRoomFromBlindSpotRoutine());
            return;
        }

        if (!HasPendingLoopAdvance)
        {
            if (logChoiceCounterDiagnostics)
            {
                Debug.Log("AnomalyLoopManager blind-spot ignored: pendingLoopAdvance=false", this);
            }

            return;
        }

        if (logChoiceCounterDiagnostics)
        {
            Debug.Log(
                "AnomalyLoopManager blind-spot entered: processing loop advance" +
                $" | correctCount={CorrectCount}" +
                $" | attemptCount={AttemptCount}" +
                $" | pendingChoiceCorrect={_choiceAndWinEvaluator.PendingChoiceWasCorrect}",
                this
            );
        }

        AreChoicesArmed = false;

        StartCoroutine(ProcessBlindSpotZoneRoutine());
    }

    public void RequestPreloadForChoice(HallwayChoice choice)
    {
        _ = choice;
        EnterBlindSpotZone();
    }

    public void ArmChoicesFromMainRoom()
    {
        _hallwayMirrorTriggerController.DisarmMirrorTransport();
        AreChoicesArmed = true;
    }

    public bool ConsumeHallwayMirrorTransport()
    {
        return _hallwayMirrorTriggerController.TryConsumeMirrorTransport(LastSubmittedChoice);
    }

    public Transform GetBootstrapHallwayFrame(BootstrapHallwayFrame frame)
    {
        switch (frame)
        {
            case BootstrapHallwayFrame.PreloadZone:
                return ResolvePreloadZoneFrame();
            case BootstrapHallwayFrame.NorthWestMount:
                return northWestRoomMountPoint;
            case BootstrapHallwayFrame.SouthEastMount:
                return southEastRoomMountPoint;
            default:
                return null;
        }
    }

    private IEnumerator LoadInitialRoomFromBlindSpotRoutine()
    {
        _processingMidHallwayZone = true;
        _transitionInProgress = true;

        string startSceneName = ResolveInitialRoomSceneName();
        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            if (logValidationWarnings)
            {
                Debug.LogWarning("AnomalyLoopManager: Could not resolve a loadable initial room scene from current configuration.", this);
            }

            _transitionInProgress = false;
            _processingMidHallwayZone = false;
            yield break;
        }

        Scene existingScene = SceneManager.GetSceneByName(startSceneName);
        if (!existingScene.IsValid() || !existingScene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(startSceneName, LoadSceneMode.Additive);
            while (!loadOperation.isDone)
            {
                yield return null;
            }
        }

        _currentRoomSceneName = startSceneName;
        ResolveRoomContextBySceneName(_currentRoomSceneName);
        _roomPlacementCoordinator.AlignRoomSceneToHallwayMount(_currentRoomSceneName, endRoomSceneName, _bootstrapSceneName);
        ValidateCurrentRoomSetup();

        if (unloadBootstrapConnectedSceneAfterInitialRoomLoad)
        {
            yield return StartCoroutine(_bootstrapConnectionCoordinator.TryUnloadBootstrapConnectedSceneRoutine(bootstrapConnectedSceneName, _currentRoomSceneName));
        }

        _transitionInProgress = false;
        _processingMidHallwayZone = false;
        PublishScore();

        if (logChoiceCounterDiagnostics)
        {
            Debug.Log($"AnomalyLoopManager initial room loaded from blind-spot: {_currentRoomSceneName}", this);
        }
    }

    private IEnumerator ProcessBlindSpotZoneRoutine()
    {
        // Commit score state first so game/UI state is deterministic before any load transition starts.
        _processingMidHallwayZone = true;

        if (LastSubmittedChoice.HasValue)
        {
            int correctBefore = CorrectCount;
            int attemptsBefore = AttemptCount;

            _choiceAndWinEvaluator.CommitStagedChoice();

            PublishScore();

            if (logChoiceCounterDiagnostics)
            {
                Debug.Log(
                    "AnomalyLoopManager blind-spot finalized staged choice:" +
                    $" choice={LastSubmittedChoice.Value}" +
                    $" wasCorrect={_choiceAndWinEvaluator.PendingChoiceWasCorrect}" +
                    $" correctBefore={correctBefore}" +
                    $" correctAfter={CorrectCount}" +
                    $" attemptsBefore={attemptsBefore}" +
                    $" attemptsAfter={AttemptCount}",
                    this
                );
            }
        }

        string targetSceneName = ResolvePendingTargetSceneName();
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            _processingMidHallwayZone = false;
            yield break;
        }

        bool usesCurrentRoom = string.Equals(targetSceneName, _currentRoomSceneName, StringComparison.Ordinal);
        if (!usesCurrentRoom)
        {
            yield return StartCoroutine(_roomStreamingCoordinator.PreloadTargetRoomRoutine(targetSceneName, _currentRoomSceneName, AlignLoadedRoomSceneToHallway));

            _transitionInProgress = true;
            yield return null;

            Scene nextRoomScene = SceneManager.GetSceneByName(targetSceneName);
            if (!nextRoomScene.IsValid() || !nextRoomScene.isLoaded)
            {
                if (logValidationWarnings)
                {
                    Debug.LogWarning($"AnomalyLoopManager: Could not activate target room scene '{targetSceneName}'.", this);
                }
            }
            else
            {
                SetSceneRootsActive(nextRoomScene, true);
                yield return null;

                string previousRoomSceneName = _currentRoomSceneName;
                _currentRoomSceneName = targetSceneName;
                ResolveRoomContextBySceneName(_currentRoomSceneName);
                _roomPlacementCoordinator.AlignRoomSceneToHallwayMount(_currentRoomSceneName, endRoomSceneName, _bootstrapSceneName);
                yield return null;

                if (!string.IsNullOrWhiteSpace(previousRoomSceneName) && !string.Equals(previousRoomSceneName, targetSceneName, StringComparison.Ordinal))
                {
                    yield return WaitFrameCount(FramesBeforeOldRoomUnload);

                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousRoomSceneName);
                    if (unloadOperation != null)
                    {
                        while (!unloadOperation.isDone)
                        {
                            yield return null;
                        }
                    }
                }
            }

            _transitionInProgress = false;
        }

        _roomStreamingCoordinator.ClearPreloadCacheExcept(targetSceneName, _currentRoomSceneName);
        CompleteLoopAdvance();
        _processingMidHallwayZone = false;
    }

    private string ResolvePendingTargetSceneName()
    {
        return _choiceAndWinEvaluator.ResolvePendingTargetSceneName(_currentRoomSceneName, initialRoomSceneName);
    }

    private static IEnumerator WaitFrameCount(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
        }
    }

    private IEnumerator EnsureBootstrapConnectedSceneLoadedRoutine()
    {
        yield return StartCoroutine(_bootstrapConnectionCoordinator.EnsureBootstrapConnectedSceneLoadedRoutine(
            bootstrapConnectedSceneName,
            _bootstrapSceneName,
            bootstrapConnectedSceneAnchorSide,
            ResolveBootstrapHallwayMountPoint,
            AlignBootstrapConnectedSceneToHallwayMount
        ));
    }

    private Transform ResolveBootstrapHallwayMountPoint()
    {
        if (!string.IsNullOrWhiteSpace(bootstrapHallwayMountPointName))
        {
            Transform namedMount = _roomPlacementCoordinator.FindTransformInSceneByName(_bootstrapSceneName, bootstrapHallwayMountPointName);
            if (namedMount != null)
            {
                return namedMount;
            }
        }

        return _roomPlacementCoordinator.GetHallwayMountPoint(bootstrapHallwayMountSide);
    }

    private void ResetLoopStateForRestart()
    {
        // Clear transient loop state so a bootstrap reload behaves like a fresh session.
        InitializeControllers();
        _choiceAndWinEvaluator.ResetForRestart();
        AreChoicesArmed = !RequireMainRoomArmingBeforeChoices;

        _processingMidHallwayZone = false;
        _hallwayMirrorTriggerController.DisarmMirrorTransport();

        _currentRoomSceneName = string.Empty;
        _currentContext = null;
        _roomStreamingCoordinator.Reset();
        _bootstrapConnectionCoordinator.Reset();

        preloadZoneFrame = ResolvePreloadZoneFrame();
        ValidateStaticSetup();
        PublishScore();
    }

    private void CompleteLoopAdvance()
    {
        int finalCorrectCount = CorrectCount;
        int finalAttemptCount = AttemptCount;

        _choiceAndWinEvaluator.CompleteLoopAdvance();
        _hallwayMirrorTriggerController.ArmMirrorTransport(LastSubmittedChoice);
        AreChoicesArmed = !RequireMainRoomArmingBeforeChoices;
        ValidateCurrentRoomSetup();
        PublishScore();

        if (logChoiceCounterDiagnostics)
        {
            Debug.Log(
                "AnomalyLoopManager loop advance complete:" +
                $" currentRoomScene={_currentRoomSceneName}" +
                $" roomNumber={finalCorrectCount:00}" +
                $" attempts={finalAttemptCount}" +
                $" armed={AreChoicesArmed}",
                this
            );
        }
    }

    private static HallwaySide GetOppositeHallway(HallwaySide side)
    {
        return side == HallwaySide.NorthWest ? HallwaySide.SouthEast : HallwaySide.NorthWest;
    }

    private static void SetSceneRootsActive(Scene scene, bool active)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            roots[i].SetActive(active);
        }
    }

    private void ResolveRoomContextBySceneName(string sceneName)
    {
        InitializeControllers();
        _currentContext = FindSceneContext(sceneName);

        if (_currentContext != null)
        {
            _choiceAndWinEvaluator.SetCurrentRoomAnomalyState(_currentContext.HasAnomaly);
        }
        else
        {
            _choiceAndWinEvaluator.ResolveCurrentRoomAnomalyStateFromSceneName(sceneName);
        }
    }

    private static RoomLoopSceneContext FindSceneContext(string sceneName)
    {
        RoomLoopSceneContext[] contexts = FindObjectsByType<RoomLoopSceneContext>(FindObjectsInactive.Include);
        for (int i = 0; i < contexts.Length; i++)
        {
            if (string.Equals(contexts[i].gameObject.scene.name, sceneName, StringComparison.Ordinal))
            {
                return contexts[i];
            }
        }

        return null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A single-mode bootstrap load is treated as a hard run reset.
        if (mode == LoadSceneMode.Single && string.Equals(scene.name, _bootstrapSceneName, StringComparison.Ordinal))
        {
            ResetLoopStateForRestart();
        }

        if (!string.IsNullOrWhiteSpace(_currentRoomSceneName) && string.Equals(scene.name, _currentRoomSceneName, StringComparison.Ordinal))
        {
            ResolveRoomContextBySceneName(scene.name);
            ValidateCurrentRoomSetup();
            PublishScore();
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (string.Equals(scene.name, _currentRoomSceneName, StringComparison.Ordinal))
        {
            _currentContext = null;
            _currentRoomSceneName = string.Empty;
            if (_choiceAndWinEvaluator != null)
            {
                _choiceAndWinEvaluator.SetCurrentRoomAnomalyState(false);
            }
        }
    }

    private void ValidateStaticSetup()
    {
        if (!logValidationWarnings)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(normalSceneName))
        {
            Debug.LogWarning("AnomalyLoopManager: Normal scene name is empty.", this);
        }
        else if (!IsSceneLoadable(normalSceneName))
        {
            Debug.LogWarning($"AnomalyLoopManager: Normal scene '{normalSceneName}' is not in Build Settings.", this);
        }

        if (string.IsNullOrWhiteSpace(initialRoomSceneName))
        {
            Debug.LogWarning("AnomalyLoopManager: Initial room scene name is empty.", this);
        }
        else if (!IsSceneLoadable(initialRoomSceneName))
        {
            Debug.LogWarning($"AnomalyLoopManager: Initial room scene '{initialRoomSceneName}' is not in Build Settings.", this);
        }

        List<string> validAnomalyScenes = GetValidAnomalySceneNames();
        if (validAnomalyScenes.Count == 0)
        {
            Debug.LogWarning("AnomalyLoopManager: No anomaly scenes configured. Only the normal room will load.", this);
        }

        for (int i = 0; i < validAnomalyScenes.Count; i++)
        {
            if (!IsSceneLoadable(validAnomalyScenes[i]))
            {
                Debug.LogWarning($"AnomalyLoopManager: Anomaly scene '{validAnomalyScenes[i]}' is not in Build Settings.", this);
            }
        }

        AnomalyDecisionTrigger[] decisionTriggers = FindObjectsByType<AnomalyDecisionTrigger>(FindObjectsInactive.Exclude);
        bool hasAnomalyTrigger = false;

        for (int i = 0; i < decisionTriggers.Length; i++)
        {
            if (decisionTriggers[i].Choice == HallwayChoice.Anomaly)
            {
                hasAnomalyTrigger = true;
            }
        }

        if (!hasAnomalyTrigger)
        {
            Debug.LogWarning("AnomalyLoopManager: Hallway should include an Anomaly decision trigger.", this);
        }

        if (RequirePreloadZonesForSeamlessTransition)
        {
            HallwayPreloadZone[] preloadZones = FindObjectsByType<HallwayPreloadZone>(FindObjectsInactive.Exclude);
            if (preloadZones.Length == 0)
            {
                Debug.LogWarning("AnomalyLoopManager: Add at least one HallwayPreloadZone in the blind-spot section of the hallway.", this);
            }
            else if (ResolvePreloadZoneFrame() == null)
            {
                Debug.LogWarning("AnomalyLoopManager: Assign preloadZoneFrame so room hallway portals can target the bootstrap preload position.", this);
            }
        }

        if (loadBootstrapConnectedSceneOnStart)
        {
            if (string.IsNullOrWhiteSpace(bootstrapConnectedSceneName))
            {
                Debug.LogWarning("AnomalyLoopManager: Bootstrap connected scene name is empty.", this);
            }
            else if (!IsSceneLoadable(bootstrapConnectedSceneName))
            {
                Debug.LogWarning($"AnomalyLoopManager: Bootstrap connected scene '{bootstrapConnectedSceneName}' is not in Build Settings.", this);
            }
        }

        if (string.IsNullOrWhiteSpace(endRoomSceneName))
        {
            Debug.LogWarning("AnomalyLoopManager: End room scene name is empty.", this);
        }
        else if (!IsSceneLoadable(endRoomSceneName))
        {
            Debug.LogWarning($"AnomalyLoopManager: End room scene '{endRoomSceneName}' is not in Build Settings.", this);
        }

    }

    private void ValidateCurrentRoomSetup()
    {
        if (!logValidationWarnings)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentRoomSceneName))
        {
            return;
        }

        if (northWestRoomMountPoint == null)
        {
            Debug.LogWarning("AnomalyLoopManager: Assign northWestRoomMountPoint for room alignment.", this);
        }

        if (_currentContext == null)
        {
            Debug.LogWarning($"AnomalyLoopManager: Room scene '{_currentRoomSceneName}' is missing RoomLoopSceneContext.", this);
            return;
        }

        if (!_currentContext.HasConnectionAnchors())
        {
            Debug.LogWarning($"AnomalyLoopManager: Room scene '{_currentRoomSceneName}' is missing one or more connection anchors on RoomLoopSceneContext.", _currentContext);
        }

        if (RequireMainRoomArmingBeforeChoices && !HasArmingZoneInScene(_currentRoomSceneName))
        {
            Debug.LogWarning($"AnomalyLoopManager: Room scene '{_currentRoomSceneName}' needs a MainRoomChoiceArmingZone.", this);
        }
    }

    private void PublishScore()
    {
        ScoreChanged?.Invoke(CorrectCount, AttemptCount);
    }

    private List<string> GetValidAnomalySceneNames()
    {
        List<string> validScenes = new();
        for (int i = 0; i < anomalySceneNames.Count; i++)
        {
            string sceneName = anomalySceneNames[i];
            if (!string.IsNullOrWhiteSpace(sceneName) && IsSceneLoadable(sceneName))
            {
                validScenes.Add(sceneName);
            }
        }

        return validScenes;
    }

    private string ResolveInitialRoomSceneName()
    {
        if (IsSceneLoadable(initialRoomSceneName))
        {
            return initialRoomSceneName;
        }

        if (IsSceneLoadable(normalSceneName))
        {
            return normalSceneName;
        }

        List<string> validAnomalyScenes = GetValidAnomalySceneNames();
        if (validAnomalyScenes.Count > 0)
        {
            return validAnomalyScenes[0];
        }

        return string.Empty;
    }

    private string ResolveNormalRoomSceneName()
    {
        if (IsSceneLoadable(normalSceneName))
        {
            return normalSceneName;
        }

        if (IsSceneLoadable(initialRoomSceneName))
        {
            return initialRoomSceneName;
        }

        return _currentRoomSceneName;
    }

    private static bool IsSceneLoadable(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private Transform ResolvePreloadZoneFrame()
    {
        HallwayPreloadZone selectedPreloadZone = FindPreferredPreloadZone();
        if (selectedPreloadZone != null)
        {
            preloadZoneFrame = selectedPreloadZone.transform;
            return preloadZoneFrame;
        }

        if (preloadZoneFrame != null && preloadZoneFrame.GetComponent<HallwayPreloadZone>() != null)
        {
            return preloadZoneFrame;
        }

        preloadZoneFrame = null;

        return preloadZoneFrame;
    }

    private HallwayPreloadZone FindPreferredPreloadZone()
    {
        HallwayPreloadZone[] preloadZones = FindObjectsByType<HallwayPreloadZone>(FindObjectsInactive.Include);
        if (preloadZones.Length == 0)
        {
            return null;
        }

        if (preloadZones.Length == 1)
        {
            return preloadZones[0];
        }

        string preferredSceneName = ResolvePreferredBootstrapSceneName();
        if (!string.IsNullOrWhiteSpace(preferredSceneName))
        {
            for (int i = 0; i < preloadZones.Length; i++)
            {
                if (string.Equals(preloadZones[i].gameObject.scene.name, preferredSceneName, StringComparison.Ordinal))
                {
                    return preloadZones[i];
                }
            }
        }

        // Final deterministic fallback when multiple preload zones exist.
        return preloadZones[0];
    }

    private string ResolvePreferredBootstrapSceneName()
    {
        if (northWestRoomMountPoint != null)
        {
            return northWestRoomMountPoint.gameObject.scene.name;
        }

        if (southEastRoomMountPoint != null)
        {
            return southEastRoomMountPoint.gameObject.scene.name;
        }

        if (!string.IsNullOrWhiteSpace(_bootstrapSceneName))
        {
            return _bootstrapSceneName;
        }

        return gameObject.scene.name;
    }

    private static bool HasArmingZoneInScene(string sceneName)
    {
        MainRoomChoiceArmingZone[] zones = FindObjectsByType<MainRoomChoiceArmingZone>(FindObjectsInactive.Include);
        for (int i = 0; i < zones.Length; i++)
        {
            if (string.Equals(zones[i].gameObject.scene.name, sceneName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public string BuildDebugSummary()
    {
        StringBuilder summary = new();
        summary.AppendLine($"Scene: {ActiveSceneName}");
        summary.AppendLine($"Current Room Scene: {(string.IsNullOrWhiteSpace(_currentRoomSceneName) ? "None" : _currentRoomSceneName)}");
        summary.AppendLine($"Room Type: {(IsCurrentRoomAnomalous ? "Anomaly" : "Normal")}");
        summary.AppendLine($"Correct: {CorrectCount}");
        summary.AppendLine($"Attempts: {AttemptCount}");
        summary.AppendLine($"Armed: {AreChoicesArmed}");
        summary.AppendLine($"Pending Advance: {HasPendingLoopAdvance}");
        summary.AppendLine($"Pending Correct: {_choiceAndWinEvaluator != null && _choiceAndWinEvaluator.PendingChoiceWasCorrect}");
        summary.AppendLine($"Pending Target: {(string.IsNullOrWhiteSpace(_choiceAndWinEvaluator != null ? _choiceAndWinEvaluator.PendingTargetSceneName : string.Empty) ? "(random)" : _choiceAndWinEvaluator.PendingTargetSceneName)}");
        HallwaySide pendingSourceHallway = _choiceAndWinEvaluator != null ? _choiceAndWinEvaluator.PendingSourceHallway : HallwaySide.NorthWest;
        summary.AppendLine($"Pending Source Hall: {pendingSourceHallway}");
        summary.AppendLine($"Target Entry Hall: {GetOppositeHallway(pendingSourceHallway)}");
        summary.AppendLine($"Mirror Transport Armed: {IsHallwayMirrorTransportArmed}");
        summary.AppendLine($"Transitioning: {_transitionInProgress}");
        summary.AppendLine($"Blind Spot Processing: {_processingMidHallwayZone}");
        summary.AppendLine(_roomStreamingCoordinator != null ? _roomStreamingCoordinator.BuildPreloadDebugLine() : "Preload: None");
        return summary.ToString();
    }

    private void AlignLoadedRoomSceneToHallway(string sceneName)
    {
        _roomPlacementCoordinator.AlignRoomSceneToHallwayMount(sceneName, endRoomSceneName, _bootstrapSceneName);
    }

    private void AlignBootstrapConnectedSceneToHallwayMount(string sceneName, HallwaySide sceneAnchorSide, Transform mountPoint)
    {
        _roomPlacementCoordinator.AlignSceneAnchorToHallwayMount(sceneName, sceneAnchorSide, mountPoint);
    }

    private static HallwaySide InferSourceHallway(HallwayChoice choice)
    {
        return choice == HallwayChoice.NoAnomaly ? HallwaySide.SouthEast : HallwaySide.NorthWest;
    }

    private void OnValidate()
    {
        anomalyChance = Mathf.Clamp01(anomalyChance);
        winRoomCount = Mathf.Max(1, winRoomCount);

        if (string.IsNullOrWhiteSpace(initialRoomSceneName))
        {
            initialRoomSceneName = normalSceneName;
        }
    }
}
