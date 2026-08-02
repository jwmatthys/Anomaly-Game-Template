using UnityEngine;

public class PulseScale : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 1f;
    [SerializeField] private float minScale = 0.5f;
    [SerializeField] private float maxScale = 1.5f;


    // Update is called once per frame
    void Update()
    {
        float scale = Mathf.Lerp(minScale, maxScale, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
        transform.localScale = new Vector3(scale, scale, scale);        
    }
}
