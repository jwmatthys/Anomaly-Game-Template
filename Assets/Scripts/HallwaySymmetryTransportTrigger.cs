using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HallwaySymmetryTransportTrigger : MonoBehaviour
{
    [SerializeField] private bool useTagCheck;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private BacktrackTrigger backtrackTrigger;

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

        if (backtrackTrigger == null)
        {
            return;
        }

        if (!backtrackTrigger.backtrackTrigger)
        {
            return;
        }

        backtrackTrigger.backtrackTrigger = false;

        Transform playerTransform = ResolvePlayerTransform(other);
        if (playerTransform == null)
        {
            return;
        }

        CharacterController movementController = playerTransform.GetComponent<CharacterController>();
        if (movementController == null)
        {
            movementController = playerTransform.GetComponentInParent<CharacterController>();
        }

        Vector3 mirroredPosition = playerTransform.position;
        mirroredPosition.x = -mirroredPosition.x;
        mirroredPosition.z = -mirroredPosition.z;

        Vector3 euler = playerTransform.rotation.eulerAngles;
        Quaternion mirroredRotation = Quaternion.Euler(euler.x, euler.y + 180f, euler.z);

        FirstPersonController controller = playerTransform.GetComponent<FirstPersonController>();
        if (controller == null)
        {
            controller = playerTransform.GetComponentInParent<FirstPersonController>();
        }

        if (controller != null)
        {
            controller.TeleportTo(mirroredPosition, mirroredRotation);
            return;
        }

        CharacterController fallbackController = movementController;
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

        fallbackController.transform.SetPositionAndRotation(mirroredPosition, mirroredRotation);

        if (wasEnabled)
        {
            fallbackController.enabled = true;
        }
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
        return other.transform;

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
