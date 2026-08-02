using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EscapeToQuit : MonoBehaviour
{
    // Bootstrap globally at runtime so every scene supports Escape without manual wiring.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindAnyObjectByType<EscapeToQuit>() != null)
        {
            return;
        }

        GameObject quitHandler = new("EscapeToQuit");
        DontDestroyOnLoad(quitHandler);
        quitHandler.AddComponent<EscapeToQuit>();
    }

    private void Update()
    {
        if (!WasEscapePressed())
        {
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static bool WasEscapePressed()
    {
        // Support both input backends so projects can switch systems without code changes.
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return true;
        }
#endif

        return false;
    }
}
