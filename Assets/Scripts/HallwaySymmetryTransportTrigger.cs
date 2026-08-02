using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
public class HallwaySymmetryTransportTrigger : MonoBehaviour
{
    [FormerlySerializedAs("useTagCheck")]
    [SerializeField] private bool usePlayerTagCheck;
    [FormerlySerializedAs("playerTag")]
    [SerializeField] private string playerTagName = "Player";
    [FormerlySerializedAs("backtrackTrigger")]
    [SerializeField] private BacktrackTrigger backtrackState;

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

        if (backtrackState == null || !backtrackState.TryConsumeBacktrack())
        {
            return;
        }

        Transform playerTransform = PlayerTriggerUtility.ResolvePlayerTransform(other);
        if (playerTransform == null)
        {
            return;
        }

        // Mirror across world origin and rotate 180 to preserve forward movement direction.
        Vector3 mirroredPosition = playerTransform.position;
        mirroredPosition.x = -mirroredPosition.x;
        mirroredPosition.z = -mirroredPosition.z;

        Vector3 euler = playerTransform.rotation.eulerAngles;
        Quaternion mirroredRotation = Quaternion.Euler(euler.x, euler.y + 180f, euler.z);
        PlayerTriggerUtility.TryTeleportPlayer(playerTransform, mirroredPosition, mirroredRotation);
    }
}
