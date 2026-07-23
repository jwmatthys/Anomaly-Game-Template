using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RoomHallwayTransitPortal : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private Transform transitDestination;

    [Header("Debug")]
    [SerializeField] private bool logTeleportDiagnostics = true;

    [SerializeField] private bool useTagCheck;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float reentryCooldownSeconds = 0.2f;

    private static readonly Dictionary<Transform, float> LastTeleportTimesByTransform = new();

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

        AnomalyLoopManager manager = AnomalyLoopManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("RoomHallwayTransitPortal could not find AnomalyLoopManager.", this);
            return;
        }

        manager.SubmitChoice(HallwayChoice.NoAnomaly, HallwaySide.SouthEast);

        Transform destinationFrame = transitDestination;

        if (destinationFrame == null)
        {
            Debug.LogWarning("RoomHallwayTransitPortal is missing transitDestination.", this);
            return;
        }

        Transform playerTransform = ResolvePlayerTransform(other);
        if (playerTransform == null)
        {
            return;
        }

        if (LastTeleportTimesByTransform.TryGetValue(playerTransform, out float lastTeleportTime))
        {
            if (Time.time - lastTeleportTime < reentryCooldownSeconds)
            {
                return;
            }
        }

        Vector3 worldOffsetFromTriggerRoot = playerTransform.position - transform.position;
        Vector3 targetPosition = destinationFrame.position + worldOffsetFromTriggerRoot;
        Quaternion targetRotation = playerTransform.rotation;

        if (logTeleportDiagnostics)
        {
            Debug.Log(
                "RoomHallwayTransitPortal teleport:" +
                $" source={transform.name} (scene={gameObject.scene.name}, pos={transform.position})" +
                $" destination={destinationFrame.name} (scene={destinationFrame.gameObject.scene.name}, pos={destinationFrame.position})" +
                $" worldOffset={worldOffsetFromTriggerRoot}" +
                $" playerBefore={playerTransform.position}" +
                $" playerAfter={targetPosition}",
                this
            );
        }

        FirstPersonController controller = playerTransform.GetComponent<FirstPersonController>();
        if (controller == null)
        {
            controller = playerTransform.GetComponentInParent<FirstPersonController>();
        }

        if (controller != null)
        {
            controller.TeleportTo(targetPosition, targetRotation);
        }
        else
        {
            CharacterController fallbackController = playerTransform.GetComponent<CharacterController>();
            if (fallbackController == null)
            {
                fallbackController = playerTransform.GetComponentInParent<CharacterController>();
            }

            if (fallbackController == null)
            {
                return;
            }

            bool wasEnabled = fallbackController.enabled;
            if (wasEnabled)
            {
                fallbackController.enabled = false;
            }

            fallbackController.transform.SetPositionAndRotation(targetPosition, targetRotation);

            if (wasEnabled)
            {
                fallbackController.enabled = true;
            }
        }

        LastTeleportTimesByTransform[playerTransform] = Time.time;
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

    private static Transform ResolvePlayerTransform(Collider other)
    {
        FirstPersonController firstPersonController = other.GetComponentInParent<FirstPersonController>();
        if (firstPersonController != null)
        {
            return firstPersonController.transform;
        }

        CharacterController characterController = other.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            return characterController.transform;
        }

        return null;
    }


}
