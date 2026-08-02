using UnityEngine;

public class SpinObject : MonoBehaviour
{
    [SerializeField] private float spinSpeed = 100f;

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);
    }
}
