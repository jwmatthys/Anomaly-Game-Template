using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MainRoomChoiceArmingZone : MonoBehaviour
{
    [SerializeField] private AnomalyLoopManager loopManager;
    [SerializeField] private bool useTagCheck;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        AnomalyLoopManager manager = loopManager != null ? loopManager : AnomalyLoopManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("MainRoomChoiceArmingZone could not find AnomalyLoopManager in scene.", this);
            return;
        }

        manager.ArmChoicesFromMainRoom();
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
}
