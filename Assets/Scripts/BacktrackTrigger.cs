using UnityEngine;
using UnityEngine.Serialization;

public class BacktrackTrigger : MonoBehaviour
{
    [FormerlySerializedAs("backtrackTrigger")]
    [SerializeField] private bool isBacktrackPending;

    // One-shot consume pattern avoids repeated symmetry teleports while staying inside trigger volume.
    public bool TryConsumeBacktrack()
    {
        if (!isBacktrackPending)
        {
            return false;
        }

        isBacktrackPending = false;
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isBacktrackPending = true;
        }
    }
}
