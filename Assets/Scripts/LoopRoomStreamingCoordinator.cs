using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = SilentDebug;

public sealed class LoopRoomStreamingCoordinator
{
    private sealed class PreloadedRoom
    {
        public string SceneName;
        public bool IsReady;
        public AsyncOperation LoadOperation;
    }

    private readonly bool _logValidationWarnings;
    private readonly int _framesBeforeSceneActivation;
    private readonly UnityEngine.Object _logContext;

    private PreloadedRoom _preloadedRoom;

    public LoopRoomStreamingCoordinator(bool logValidationWarnings, int framesBeforeSceneActivation, UnityEngine.Object logContext)
    {
        _logValidationWarnings = logValidationWarnings;
        _framesBeforeSceneActivation = Mathf.Max(0, framesBeforeSceneActivation);
        _logContext = logContext;
    }

    public IEnumerator PreloadTargetRoomRoutine(string targetSceneName, string currentRoomSceneName, Action<string> alignRoomSceneToHallwayMount)
    {
        // Preload additively and keep roots disabled so handoff stays seamless.
        if (_preloadedRoom != null && _preloadedRoom.IsReady && string.Equals(_preloadedRoom.SceneName, targetSceneName, StringComparison.Ordinal))
        {
            yield break;
        }

        if (_preloadedRoom != null && !string.Equals(_preloadedRoom.SceneName, targetSceneName, StringComparison.Ordinal))
        {
            if (_preloadedRoom.IsReady)
            {
                Scene oldScene = SceneManager.GetSceneByName(_preloadedRoom.SceneName);
                if (oldScene.IsValid() && oldScene.isLoaded && !string.Equals(oldScene.name, currentRoomSceneName, StringComparison.Ordinal))
                {
                    SceneManager.UnloadSceneAsync(oldScene);
                }
            }

            _preloadedRoom = null;
        }

        if (string.Equals(targetSceneName, currentRoomSceneName, StringComparison.Ordinal))
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
            if (_logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Scene '{targetSceneName}' is not in Build Settings.", _logContext);
            }

            yield break;
        }

        Scene existingScene = SceneManager.GetSceneByName(targetSceneName);
        if (existingScene.IsValid() && existingScene.isLoaded)
        {
            alignRoomSceneToHallwayMount?.Invoke(targetSceneName);
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
        if (loadOperation == null)
        {
            yield break;
        }

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

        yield return WaitFrameCount(_framesBeforeSceneActivation);
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

        alignRoomSceneToHallwayMount?.Invoke(targetSceneName);
        SetSceneRootsActive(loadedScene, false);
        _preloadedRoom.IsReady = true;
        _preloadedRoom.LoadOperation = null;
    }

    public void ClearPreloadCacheExcept(string keepSceneName, string currentRoomSceneName)
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
            if (scene.IsValid() && scene.isLoaded && !string.Equals(scene.name, currentRoomSceneName, StringComparison.Ordinal))
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        _preloadedRoom = null;
    }

    public void Reset()
    {
        _preloadedRoom = null;
    }

    public string BuildPreloadDebugLine()
    {
        if (_preloadedRoom == null)
        {
            return "Preload: None";
        }

        string progress = _preloadedRoom.LoadOperation == null ? "1.00" : _preloadedRoom.LoadOperation.progress.ToString("0.00");
        return $"Preload: {_preloadedRoom.SceneName} | Ready={_preloadedRoom.IsReady} | Progress={progress}";
    }

    private static IEnumerator WaitFrameCount(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
        }
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
}
