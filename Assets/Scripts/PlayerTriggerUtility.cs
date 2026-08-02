using StarterAssets;
using UnityEngine;

public static class PlayerTriggerUtility
{
    public static bool IsPlayer(Collider other, bool useTagCheck, string playerTag)
    {
        if (other == null)
        {
            return false;
        }

        if (useTagCheck)
        {
            return other.CompareTag(playerTag);
        }

        return other.GetComponentInParent<FirstPersonController>() != null ||
               other.GetComponentInParent<CharacterController>() != null;
    }

    public static Transform ResolvePlayerTransform(Collider other)
    {
        if (other == null)
        {
            return null;
        }

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

    // Uses FirstPersonController teleport when available so camera/controller state stays coherent.
    public static bool TryTeleportPlayer(Transform playerTransform, Vector3 targetPosition, Quaternion targetRotation)
    {
        if (playerTransform == null)
        {
            return false;
        }

        FirstPersonController controller = playerTransform.GetComponent<FirstPersonController>();
        if (controller == null)
        {
            controller = playerTransform.GetComponentInParent<FirstPersonController>();
        }

        if (controller != null)
        {
            controller.TeleportTo(targetPosition, targetRotation);
            return true;
        }

        CharacterController fallbackController = playerTransform.GetComponent<CharacterController>();
        if (fallbackController == null)
        {
            fallbackController = playerTransform.GetComponentInParent<CharacterController>();
        }

        if (fallbackController == null)
        {
            return false;
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

        return true;
    }
}