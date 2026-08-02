using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = SilentDebug;

public sealed class LoopBootstrapConnectionCoordinator
{
    private readonly bool _logValidationWarnings;
    private readonly UnityEngine.Object _logContext;

    private bool _bootstrapConnectedSceneReady;

    public LoopBootstrapConnectionCoordinator(bool logValidationWarnings, UnityEngine.Object logContext)
    {
        _logValidationWarnings = logValidationWarnings;
        _logContext = logContext;
    }

    public IEnumerator EnsureBootstrapConnectedSceneLoadedRoutine(
        string bootstrapConnectedSceneName,
        string bootstrapSceneName,
        HallwaySide bootstrapConnectedSceneAnchorSide,
        Func<Transform> resolveBootstrapHallwayMountPoint,
        Action<string, HallwaySide, Transform> alignSceneAnchorToHallwayMount
    )
    {
        if (_bootstrapConnectedSceneReady)
        {
            yield break;
        }

        string sceneName = bootstrapConnectedSceneName;
        if (string.IsNullOrWhiteSpace(sceneName) || string.Equals(sceneName, bootstrapSceneName, StringComparison.Ordinal))
        {
            yield break;
        }

        if (!IsSceneLoadable(sceneName))
        {
            if (_logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Bootstrap connected scene '{sceneName}' is not in Build Settings.", _logContext);
            }

            yield break;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }
        }

        Transform mountPoint = resolveBootstrapHallwayMountPoint != null ? resolveBootstrapHallwayMountPoint.Invoke() : null;
        alignSceneAnchorToHallwayMount?.Invoke(sceneName, bootstrapConnectedSceneAnchorSide, mountPoint);
        _bootstrapConnectedSceneReady = true;
    }

    public IEnumerator TryUnloadBootstrapConnectedSceneRoutine(string bootstrapConnectedSceneName, string loadedRoomSceneName)
    {
        string sceneName = bootstrapConnectedSceneName;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            yield break;
        }

        if (string.Equals(sceneName, loadedRoomSceneName, StringComparison.Ordinal))
        {
            yield break;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            yield break;
        }

        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(sceneName);
        while (unloadOperation != null && !unloadOperation.isDone)
        {
            yield return null;
        }

        _bootstrapConnectedSceneReady = false;
    }

    public void Reset()
    {
        _bootstrapConnectedSceneReady = false;
    }

    private static bool IsSceneLoadable(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
