using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class CorrectCountSignDisplay : MonoBehaviour
{
    [FormerlySerializedAs("scoreText")]
    [SerializeField] private TMP_Text scoreTextComponent;
    [FormerlySerializedAs("roomLabel")]
    [SerializeField] private string roomLabelText = "ROOM";
    [FormerlySerializedAs("exitLabel")]
    [SerializeField] private string exitLabelText = "EXIT";

    private AnomalyLoopManager _boundManager;

    private void Awake()
    {
        if (scoreTextComponent == null)
        {
            scoreTextComponent = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindToManager();
        RefreshFromManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindFromManager();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _ = scene;
        _ = mode;
        BindToManager();
        RefreshFromManager();
    }

    private void BindToManager()
    {
        if (_boundManager == AnomalyLoopManager.Instance || AnomalyLoopManager.Instance == null)
        {
            return;
        }

        UnbindFromManager();
        _boundManager = AnomalyLoopManager.Instance;
        _boundManager.ScoreChanged += HandleScoreChanged;
    }

    private void UnbindFromManager()
    {
        if (_boundManager == null)
        {
            return;
        }

        _boundManager.ScoreChanged -= HandleScoreChanged;
        _boundManager = null;
    }

    private void HandleScoreChanged(int correctCount, int attemptCount)
    {
        _ = correctCount;
        _ = attemptCount;
        RefreshFromManager();
    }

    private void RefreshFromManager()
    {
        if (_boundManager == null)
        {
            UpdateText(0);
            return;
        }

        // End room intentionally replaces the running counter with a terminal label.
        bool isEndRoom =
            !string.IsNullOrWhiteSpace(_boundManager.CurrentRoomSceneName) &&
            string.Equals(_boundManager.CurrentRoomSceneName, _boundManager.EndRoomSceneName, System.StringComparison.Ordinal);

        if (isEndRoom)
        {
            scoreTextComponent.text = exitLabelText;
            return;
        }

        UpdateText(_boundManager.CorrectCount);
    }

    private void UpdateText(int correctCount)
    {
        if (scoreTextComponent == null)
        {
            return;
        }

        scoreTextComponent.text = roomLabelText + "\n" + correctCount.ToString("D2");
    }
}
