using TMPro;
using UnityEngine;

public class CorrectCountSignDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private string roomLabel = "ROOM";
    [SerializeField] private string exitLabel = "EXIT";

    private AnomalyLoopManager _boundManager;

    private void Awake()
    {
        if (scoreText == null)
        {
            scoreText = GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        BindToManager();
        RefreshFromManager();
    }

    private void OnDisable()
    {
        UnbindFromManager();
    }

    private void Update()
    {
        if (_boundManager == null && AnomalyLoopManager.Instance != null)
        {
            BindToManager();
            RefreshFromManager();
        }
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

        bool isEndRoom =
            !string.IsNullOrWhiteSpace(_boundManager.CurrentRoomSceneName) &&
            string.Equals(_boundManager.CurrentRoomSceneName, _boundManager.EndRoomSceneName, System.StringComparison.Ordinal);

        if (isEndRoom)
        {
            scoreText.text = exitLabel;
            return;
        }

        UpdateText(_boundManager.CorrectCount);
    }

    private void UpdateText(int correctCount)
    {
        if (scoreText == null)
        {
            return;
        }

        scoreText.text = roomLabel + "\n" + correctCount.ToString("D2");
    }
}
