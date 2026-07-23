using UnityEngine;

public class BacktrackTrigger : MonoBehaviour
{
    public bool backtrackTrigger = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            backtrackTrigger = true;
        }
    }
}
