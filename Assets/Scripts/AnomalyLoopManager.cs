using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using StarterAssets;
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
    private sealed class PreloadedRoom
    {
        public string SceneName;
        public bool IsReady;
        public AsyncOperation LoadOperation;
    }

    public static AnomalyLoopManager Instance { get; private set; }

    [Header("Room Selection")]
    [SerializeField] private string normalSceneName = "TemplateRoom";
    [SerializeField] private List<string> anomalySceneNames = new();
    [SerializeField, Range(0f, 1f)] private float anomalyChance = 0.5f;
    [SerializeField] private string initialRoomSceneName = "TemplateRoom";

    [Header("Room Placement")]
    [SerializeField] private Transform northWestRoomMountPoint;
    [SerializeField] private Transform southEastRoomMountPoint;

    [Header("Hallway Frames")]
    [SerializeField] private Transform preloadZoneFrame;

    [Header("Validation")]
    [SerializeField] private bool logValidationWarnings = true;
    [SerializeField] private bool warnOnOppositeSeamMismatch = false;

    [Header("Debug")]
    [SerializeField] private bool logChoiceCounterDiagnostics = true;

    public int CorrectCount { get; private set; }
    public int AttemptCount { get; private set; }
    public bool IsCurrentRoomAnomalous { get; private set; }
    public bool AreChoicesArmed { get; private set; }
    public bool IsTransitionInProgress => _transitionInProgress;
    public string ActiveSceneName => SceneManager.GetActiveScene().name;
    public string CurrentRoomSceneName => _currentRoomSceneName;
    public bool HasPendingLoopAdvance => _pendingLoopAdvance;
    public HallwayChoice? LastSubmittedChoice => _lastSubmittedChoice;
    public bool IsHallwayMirrorTransportArmed => _hallwayMirrorTransportArmed;

    public event Action<int, int> ScoreChanged;

    private bool _transitionInProgress;
    private bool _processingMidHallwayZone;
    private bool _pendingLoopAdvance;
    private bool _pendingChoiceWasCorrect;
    private bool _hallwayMirrorTransportArmed;
    private HallwaySide _pendingSourceHallway = HallwaySide.NorthWest;
    private HallwayChoice? _lastSubmittedChoice;

    private string _lastAnomalySceneName = string.Empty;
    private string _currentRoomSceneName = string.Empty;
    private string _pendingTargetSceneName = string.Empty;
    private string _bootstrapSceneName = string.Empty;

    private RoomLoopSceneContext _currentContext;
    private PreloadedRoom _preloadedRoom;

    private const bool PersistAcrossSceneLoads = true;
    private const bool RequireMainRoomArmingBeforeChoices = true;
    private const bool RequirePreloadZonesForSeamlessTransition = true;
    private const bool DisableInputDuringTransition = false;
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
        AreChoicesArmed = !RequireMainRoomArmingBeforeChoices;
        ValidateStaticSetup();
        PublishScore();
    }

    public void SubmitChoice(HallwayChoice choice)
    {
        SubmitChoice(choice, InferSourceHallway(choice));
    }

    public void SubmitChoice(HallwayChoice choice, HallwaySide sourceHallway)
    {
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

        _lastSubmittedChoice = choice;
        _pendingLoopAdvance = true;
        _hallwayMirrorTransportArmed = false;
        _pendingSourceHallway = sourceHallway;

        _pendingChoiceWasCorrect = IsChoiceCorrect(choice, IsCurrentRoomAnomalous);
        if (_pendingChoiceWasCorrect)
        {
            _pendingTargetSceneName = string.Empty;
        }
        else
        {
            _pendingTargetSceneName = normalSceneName;
        }

        if (logChoiceCounterDiagnostics)
        {
            Debug.Log(
                "AnomalyLoopManager choice staged (last trigger wins):" +
                $" choice={choice}" +
                $" sourceHall={sourceHallway}" +
                $" roomHasAnomaly={IsCurrentRoomAnomalous}" +
                $" wasCorrect={_pendingChoiceWasCorrect}" +
                $" correctBefore={correctBefore}" +
                $" correctAfter={CorrectCount}" +
                $" attemptsBefore={attemptsBefore}" +
                $" attemptsAfter={AttemptCount}" +
                $" pendingTarget={(string.IsNullOrWhiteSpace(_pendingTargetSceneName) ? "(random)" : _pendingTargetSceneName)}",
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
                    $" pendingLoopAdvance={_pendingLoopAdvance}",
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

        if (!_pendingLoopAdvance)
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
                $" | pendingChoiceCorrect={_pendingChoiceWasCorrect}",
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
        _hallwayMirrorTransportArmed = false;
        AreChoicesArmed = true;
    }

    public bool ConsumeHallwayMirrorTransport()
    {
        if (!_hallwayMirrorTransportArmed || !_lastSubmittedChoice.HasValue || _lastSubmittedChoice.Value != HallwayChoice.Anomaly)
        {
            return false;
        }

        _hallwayMirrorTransportArmed = false;
        return true;
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

    private static bool IsChoiceCorrect(HallwayChoice choice, bool hasAnomaly)
    {
        if (hasAnomaly)
        {
            return choice == HallwayChoice.Anomaly;
        }

        return choice == HallwayChoice.NoAnomaly;
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
        AlignRoomSceneToHallwayMount(_currentRoomSceneName);
        ValidateCurrentRoomSetup();

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
        _processingMidHallwayZone = true;

        if (_lastSubmittedChoice.HasValue)
        {
            int correctBefore = CorrectCount;
            int attemptsBefore = AttemptCount;

            AttemptCount++;
            if (_pendingChoiceWasCorrect)
            {
                CorrectCount++;
            }
            else
            {
                CorrectCount = 0;
            }

            PublishScore();

            if (logChoiceCounterDiagnostics)
            {
                Debug.Log(
                    "AnomalyLoopManager blind-spot finalized staged choice:" +
                    $" choice={_lastSubmittedChoice.Value}" +
                    $" wasCorrect={_pendingChoiceWasCorrect}" +
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
            yield return StartCoroutine(PreloadTargetRoomRoutine(targetSceneName));

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
                AlignRoomSceneToHallwayMount(_currentRoomSceneName);
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

        ClearPreloadCacheExcept(targetSceneName);
        CompleteLoopAdvance();
        _processingMidHallwayZone = false;
    }

    private string ResolvePendingTargetSceneName()
    {
        if (!string.IsNullOrWhiteSpace(_pendingTargetSceneName))
        {
            return _pendingTargetSceneName;
        }

        _pendingTargetSceneName = PickNextRoomSceneName();
        return _pendingTargetSceneName;
    }

    private IEnumerator PreloadTargetRoomRoutine(string targetSceneName)
    {
        if (_preloadedRoom != null && _preloadedRoom.IsReady && string.Equals(_preloadedRoom.SceneName, targetSceneName, StringComparison.Ordinal))
        {
            yield break;
        }

        if (_preloadedRoom != null && !string.Equals(_preloadedRoom.SceneName, targetSceneName, StringComparison.Ordinal))
        {
            if (_preloadedRoom.IsReady)
            {
                Scene oldScene = SceneManager.GetSceneByName(_preloadedRoom.SceneName);
                if (oldScene.IsValid() && oldScene.isLoaded && !string.Equals(oldScene.name, _currentRoomSceneName, StringComparison.Ordinal))
                {
                    SceneManager.UnloadSceneAsync(oldScene);
                }
            }

            _preloadedRoom = null;
        }

        if (string.Equals(targetSceneName, _currentRoomSceneName, StringComparison.Ordinal))
        {
            _preloadedRoom = new PreloadedRoom
            {
                SceneName = targetSceneName,
                IsReady = true,
                LoadOperation = null
            };
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            if (logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Scene '{targetSceneName}' is not in Build Settings.", this);
            }

            yield break;
        }

        Scene existingScene = SceneManager.GetSceneByName(targetSceneName);
        if (existingScene.IsValid() && existingScene.isLoaded)
        {
            AlignRoomSceneToHallwayMount(targetSceneName);
            SetSceneRootsActive(existingScene, false);
            _preloadedRoom = new PreloadedRoom
            {
                SceneName = targetSceneName,
                IsReady = true,
                LoadOperation = null
            };
            yield break;
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        loadOperation.allowSceneActivation = false;

        _preloadedRoom = new PreloadedRoom
        {
            SceneName = targetSceneName,
            IsReady = false,
            LoadOperation = loadOperation
        };

        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }

        yield return WaitFrameCount(FramesBeforeSceneActivation);
        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene loadedScene = SceneManager.GetSceneByName(targetSceneName);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            _preloadedRoom = null;
            yield break;
        }

        AlignRoomSceneToHallwayMount(targetSceneName);
        SetSceneRootsActive(loadedScene, false);
        _preloadedRoom.IsReady = true;
        _preloadedRoom.LoadOperation = null;
    }

    private static IEnumerator WaitFrameCount(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
        }
    }

    private void AlignRoomSceneToHallwayMount(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Transform northWestMount = northWestRoomMountPoint;
        if (northWestMount == null)
        {
            return;
        }

        Scene roomScene = SceneManager.GetSceneByName(sceneName);
        if (!roomScene.IsValid() || !roomScene.isLoaded)
        {
            return;
        }

        RoomLoopSceneContext context = FindSceneContext(sceneName);
        if (context == null)
        {
            if (logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Room scene '{sceneName}' has no RoomLoopSceneContext for alignment.", this);
            }

            return;
        }

        // Cross-connection mapping:
        // NW hallway mount <-> SE room anchor
        // SE hallway mount <-> NW room anchor
        Transform roomNorthWestAnchor = context.GetConnectionAnchor(HallwaySide.NorthWest);
        Transform roomSouthEastAnchor = context.GetConnectionAnchor(HallwaySide.SouthEast);
        if (roomNorthWestAnchor == null || roomSouthEastAnchor == null)
        {
            if (logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Room scene '{sceneName}' is missing one or more connection anchors.", context);
            }

            return;
        }

        Transform targetMount = northWestMount;
        Transform targetRoomAnchor = roomSouthEastAnchor;

        if (targetMount == null || targetRoomAnchor == null)
        {
            if (logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Room scene '{sceneName}' missing NW mount or SE room anchor for translation alignment.", context);
            }

            return;
        }

        Vector3 positionOffset = targetMount.position - targetRoomAnchor.position;
        positionOffset.y = 0f;

        GameObject[] roots = roomScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i].transform;
            root.position += positionOffset;
        }

        if (logValidationWarnings && warnOnOppositeSeamMismatch)
        {
            Transform oppositeMount = southEastRoomMountPoint;
            Transform oppositeRoomAnchor = roomNorthWestAnchor;

            if (oppositeMount != null && oppositeRoomAnchor != null)
            {
                // oppositeRoomAnchor has already moved with the room roots, so compare directly.
                Vector2 predictedXZ = new Vector2(oppositeRoomAnchor.position.x, oppositeRoomAnchor.position.z);
                Vector2 mountXZ = new Vector2(oppositeMount.position.x, oppositeMount.position.z);
                float seamError = Vector2.Distance(predictedXZ, mountXZ);

                if (seamError > 0.05f)
                {
                    float hallwaySpan = Vector2.Distance(
                        new Vector2(northWestMount.position.x, northWestMount.position.z),
                        new Vector2(southEastRoomMountPoint.position.x, southEastRoomMountPoint.position.z)
                    );
                    float roomSpan = Vector2.Distance(
                        new Vector2(roomNorthWestAnchor.position.x, roomNorthWestAnchor.position.z),
                        new Vector2(roomSouthEastAnchor.position.x, roomSouthEastAnchor.position.z)
                    );

                    Debug.LogWarning(
                        $"AnomalyLoopManager: Room scene '{sceneName}' seam mismatch is {seamError:0.###}m on the opposite exit. " +
                        $"Hallway span={hallwaySpan:0.###}m, room anchor span={roomSpan:0.###}m. " +
                        "Adjust room NW/SE connection anchor placement to match hallway mount spacing.",
                        context
                    );
                }
            }
        }
    }

    private string PickNextRoomSceneName()
    {
        List<string> validAnomalyScenes = GetValidAnomalySceneNames();
        bool shouldPickAnomaly = validAnomalyScenes.Count > 0 && UnityEngine.Random.value < anomalyChance;

        if (!shouldPickAnomaly)
        {
            return ResolveNormalRoomSceneName();
        }

        return PickAnomalySceneName(validAnomalyScenes);
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

    private void ClearPreloadCacheExcept(string keepSceneName)
    {
        if (_preloadedRoom == null)
        {
            return;
        }

        if (string.Equals(_preloadedRoom.SceneName, keepSceneName, StringComparison.Ordinal))
        {
            return;
        }

        if (_preloadedRoom.IsReady)
        {
            Scene scene = SceneManager.GetSceneByName(_preloadedRoom.SceneName);
            if (scene.IsValid() && scene.isLoaded && !string.Equals(scene.name, _currentRoomSceneName, StringComparison.Ordinal))
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        _preloadedRoom = null;
    }

    private void CompleteLoopAdvance()
    {
        int finalCorrectCount = CorrectCount;
        int finalAttemptCount = AttemptCount;

        _pendingLoopAdvance = false;
        _pendingTargetSceneName = string.Empty;
        _pendingChoiceWasCorrect = false;
        _hallwayMirrorTransportArmed = _lastSubmittedChoice.HasValue && _lastSubmittedChoice.Value == HallwayChoice.Anomaly;
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

    private bool TryResolveCurrentRoomFromLoadedScenes()
    {
        RoomLoopSceneContext[] contexts = FindObjectsByType<RoomLoopSceneContext>(FindObjectsInactive.Include);
        if (contexts.Length == 0)
        {
            _currentContext = null;
            _currentRoomSceneName = string.Empty;
            IsCurrentRoomAnomalous = false;
            return false;
        }

        RoomLoopSceneContext chosen = contexts[0];
        _currentContext = chosen;
        _currentRoomSceneName = chosen.gameObject.scene.name;
        IsCurrentRoomAnomalous = chosen.HasAnomaly;
        return true;
    }

    private void ResolveRoomContextBySceneName(string sceneName)
    {
        _currentContext = FindSceneContext(sceneName);

        if (_currentContext != null)
        {
            IsCurrentRoomAnomalous = _currentContext.HasAnomaly;
        }
        else
        {
            IsCurrentRoomAnomalous = !string.Equals(sceneName, normalSceneName, StringComparison.Ordinal);
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
        _ = mode;

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
            IsCurrentRoomAnomalous = false;
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

    private static Transform FindPreloadZoneFrameInScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return null;
        }

        HallwayPreloadZone[] preloadZones = FindObjectsByType<HallwayPreloadZone>(FindObjectsInactive.Include);
        for (int i = 0; i < preloadZones.Length; i++)
        {
            if (string.Equals(preloadZones[i].gameObject.scene.name, sceneName, StringComparison.Ordinal))
            {
                return preloadZones[i].transform;
            }
        }

        return null;
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
        summary.AppendLine($"Pending Advance: {_pendingLoopAdvance}");
        summary.AppendLine($"Pending Correct: {_pendingChoiceWasCorrect}");
        summary.AppendLine($"Pending Target: {(string.IsNullOrWhiteSpace(_pendingTargetSceneName) ? "(random)" : _pendingTargetSceneName)}");
        summary.AppendLine($"Pending Source Hall: {_pendingSourceHallway}");
        summary.AppendLine($"Target Entry Hall: {GetOppositeHallway(_pendingSourceHallway)}");
        summary.AppendLine($"Mirror Transport Armed: {_hallwayMirrorTransportArmed}");
        summary.AppendLine($"Transitioning: {_transitionInProgress}");
        summary.AppendLine($"Blind Spot Processing: {_processingMidHallwayZone}");
        summary.AppendLine(BuildPreloadDebugLine(_preloadedRoom));
        return summary.ToString();
    }

    private static string BuildPreloadDebugLine(PreloadedRoom preload)
    {
        if (preload == null)
        {
            return "Preload: None";
        }

        string progress = preload.LoadOperation == null ? "1.00" : preload.LoadOperation.progress.ToString("0.00");
        return $"Preload: {preload.SceneName} | Ready={preload.IsReady} | Progress={progress}";
    }

    private static HallwaySide InferSourceHallway(HallwayChoice choice)
    {
        return choice == HallwayChoice.NoAnomaly ? HallwaySide.SouthEast : HallwaySide.NorthWest;
    }

    private void OnValidate()
    {
        anomalyChance = Mathf.Clamp01(anomalyChance);

        if (string.IsNullOrWhiteSpace(initialRoomSceneName))
        {
            initialRoomSceneName = normalSceneName;
        }
    }
}
