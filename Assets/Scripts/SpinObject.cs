using UnityEngine;
using UnityEngine.Serialization;

public class SpinObject : MonoBehaviour
{
    [FormerlySerializedAs("spinSpeed")]
    [SerializeField] private float spinSpeedDegreesPerSecond = 100f;

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeedDegreesPerSecond * Time.deltaTime);
    }
}
