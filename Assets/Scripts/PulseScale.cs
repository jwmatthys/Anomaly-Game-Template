using UnityEngine;
using UnityEngine.Serialization;

public class PulseScale : MonoBehaviour
{
    [FormerlySerializedAs("pulseSpeed")]
    [SerializeField] private float pulseSpeedHz = 1f;
    [FormerlySerializedAs("minScale")]
    [SerializeField] private float minimumScale = 0.5f;
    [FormerlySerializedAs("maxScale")]
    [SerializeField] private float maximumScale = 1.5f;

    private void Update()
    {
        float scale = Mathf.Lerp(minimumScale, maximumScale, (Mathf.Sin(Time.time * pulseSpeedHz) + 1f) / 2f);
        transform.localScale = new Vector3(scale, scale, scale);
    }
}
