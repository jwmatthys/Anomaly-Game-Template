using UnityEngine;
using UnityEngine.Serialization;

public class AnomalyLoopDebugOverlay : MonoBehaviour
{
    [FormerlySerializedAs("showOnStart")]
    [SerializeField] private bool showOverlayOnStart = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [FormerlySerializedAs("panelPosition")]
    [SerializeField] private Vector2 overlayPanelPosition = new(12f, 12f);
    [FormerlySerializedAs("panelSize")]
    [SerializeField] private Vector2 overlayPanelSize = new(310f, 220f);

    private bool _isVisible;
    private GUIStyle _panelStyle;
    private GUIStyle _textStyle;

    private void Awake()
    {
        _isVisible = showOverlayOnStart;
        BuildStyles();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _isVisible = !_isVisible;
        }
    }

    private void OnGUI()
    {
        if (!_isVisible)
        {
            return;
        }

        if (_panelStyle == null || _textStyle == null)
        {
            BuildStyles();
        }

        Rect rect = new(overlayPanelPosition.x, overlayPanelPosition.y, overlayPanelSize.x, overlayPanelSize.y);
        GUILayout.BeginArea(rect, _panelStyle);

        AnomalyLoopManager manager = AnomalyLoopManager.Instance;
        if (manager == null)
        {
            GUILayout.Label("Anomaly Loop Debug", _textStyle);
            GUILayout.Label("Manager not found.", _textStyle);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label("Anomaly Loop Debug", _textStyle);
        GUILayout.Space(6f);
        GUILayout.Label(manager.BuildDebugSummary(), _textStyle);
        GUILayout.Label("Toggle: " + toggleKey, _textStyle);

        GUILayout.EndArea();
    }

    private void BuildStyles()
    {
        // Build once and reuse to keep IMGUI allocations predictable.
        _panelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 10, 10)
        };

        _panelStyle.normal.background = MakePanelTexture(new Color(0.05f, 0.05f, 0.05f, 0.84f));

        _textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white },
            richText = false
        };
    }

    private static Texture2D MakePanelTexture(Color color)
    {
        // Single-color texture is enough for a translucent IMGUI panel backdrop.
        Texture2D texture = new(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
