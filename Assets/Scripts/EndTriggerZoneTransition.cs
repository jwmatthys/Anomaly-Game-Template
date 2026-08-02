using System.Collections;
using StarterAssets;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class EndTriggerZoneTransition : MonoBehaviour
{
    [SerializeField] private string bootstrapSceneName = "Bootstrap Hallway";
    [SerializeField, Min(0f)] private float delaySeconds = 1.25f;
    [SerializeField, Min(0f)] private float fadeDurationSeconds = 0.85f;
    [SerializeField, Min(0f)] private float triggerArmDelaySeconds = 0.2f;
    [SerializeField] private bool useTagCheck;
    [SerializeField] private string playerTag = "Player";

    private static bool _sequenceRunning;
    private bool _isArmed;

    private void OnEnable()
    {
        _isArmed = false;
        StartCoroutine(ArmAfterDelayRoutine());
    }

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_sequenceRunning || !_isArmed || !IsPlayer(other))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(bootstrapSceneName) || !Application.CanStreamedLevelBeLoaded(bootstrapSceneName))
        {
            return;
        }

        _sequenceRunning = true;

        GameObject runnerObject = new("EndTriggerZoneTransitionRunner");
        TransitionRunner runner = runnerObject.AddComponent<TransitionRunner>();
        runner.Begin(bootstrapSceneName, delaySeconds, fadeDurationSeconds);
    }

    private IEnumerator ArmAfterDelayRoutine()
    {
        if (triggerArmDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(triggerArmDelaySeconds);
        }
        else
        {
            yield return null;
        }

        _isArmed = true;
    }

    private bool IsPlayer(Collider other)
    {
        if (useTagCheck)
        {
            return other.CompareTag(playerTag);
        }

        if (other.GetComponentInParent<FirstPersonController>() != null)
        {
            return true;
        }

        return other.GetComponentInParent<CharacterController>() != null;
    }

    private sealed class TransitionRunner : MonoBehaviour
    {
        private string _bootstrapSceneName;
        private float _delaySeconds;
        private float _fadeDurationSeconds;
        private CanvasGroup _fadeGroup;

        public void Begin(string bootstrapSceneName, float delaySeconds, float fadeDurationSeconds)
        {
            _bootstrapSceneName = bootstrapSceneName;
            _delaySeconds = Mathf.Max(0f, delaySeconds);
            _fadeDurationSeconds = Mathf.Max(0f, fadeDurationSeconds);
            DontDestroyOnLoad(gameObject);
            StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            EnsureFadeOverlay();

            if (_delaySeconds > 0f)
            {
                yield return new WaitForSeconds(_delaySeconds);
            }

            yield return FadeOverlayRoutine(1f, _fadeDurationSeconds);
            yield return DestroyPersistentLoopManagerRoutine();
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(_bootstrapSceneName, LoadSceneMode.Single);
            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }
            yield return FadeOverlayRoutine(0f, _fadeDurationSeconds);

            EndTriggerZoneTransition._sequenceRunning = false;

            if (_fadeGroup != null)
            {
                Destroy(_fadeGroup.gameObject);
            }

            Destroy(gameObject);
        }

        private static IEnumerator DestroyPersistentLoopManagerRoutine()
        {
            AnomalyLoopManager manager = FindAnyObjectByType<AnomalyLoopManager>();
            if (manager != null)
            {
                manager.enabled = false;
                manager.gameObject.SetActive(false);
                Destroy(manager.gameObject);
                yield return null;
            }
        }

        private void EnsureFadeOverlay()
        {
            if (_fadeGroup != null)
            {
                return;
            }

            GameObject fadeRoot = new("EndRoomFadeOverlay");
            DontDestroyOnLoad(fadeRoot);

            Canvas canvas = fadeRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            fadeRoot.AddComponent<GraphicRaycaster>();

            _fadeGroup = fadeRoot.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
            _fadeGroup.interactable = false;

            GameObject fill = new("Fill");
            fill.transform.SetParent(fadeRoot.transform, false);

            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = Color.white;
            fillImage.raycastTarget = false;
        }

        private IEnumerator FadeOverlayRoutine(float targetAlpha, float duration)
        {
            EnsureFadeOverlay();

            if (_fadeGroup == null)
            {
                yield break;
            }

            float startAlpha = _fadeGroup.alpha;
            if (duration <= 0f)
            {
                _fadeGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            _fadeGroup.alpha = targetAlpha;
        }
    }
}
