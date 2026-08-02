using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
public class RoomHallwayTransitPortal : MonoBehaviour
{
    [Header("Destination")]
    [FormerlySerializedAs("transitDestination")]
    [SerializeField] private Transform transitDestinationFrame;

    [FormerlySerializedAs("useTagCheck")]
    [SerializeField] private bool usePlayerTagCheck;
    [FormerlySerializedAs("playerTag")]
    [SerializeField] private string playerTagName = "Player";

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!PlayerTriggerUtility.IsPlayer(other, usePlayerTagCheck, playerTagName))
        {
            return;
        }

        AnomalyLoopManager manager = AnomalyLoopManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.SubmitChoice(HallwayChoice.NoAnomaly, HallwaySide.SouthEast);

        Transform destinationFrame = transitDestinationFrame;

        if (destinationFrame == null)
        {
            return;
        }

        Transform playerTransform = PlayerTriggerUtility.ResolvePlayerTransform(other);
        if (playerTransform == null)
        {
            return;
        }

        // Preserve player offset relative to trigger so entry/exit feel spatially continuous.
        Vector3 worldOffsetFromTriggerRoot = playerTransform.position - transform.position;
        Vector3 targetPosition = destinationFrame.position + worldOffsetFromTriggerRoot;
        Quaternion targetRotation = playerTransform.rotation;
        PlayerTriggerUtility.TryTeleportPlayer(playerTransform, targetPosition, targetRotation);
    }
}
