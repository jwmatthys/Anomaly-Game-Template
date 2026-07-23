using StarterAssets;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AnomalyDecisionTrigger : MonoBehaviour
{
    [SerializeField] private HallwayChoice choice = HallwayChoice.NoAnomaly;
    [SerializeField] private AnomalyLoopManager loopManager;
    [SerializeField] private bool requireChoicesToBeArmed = true;
    [SerializeField] private bool logWarningWhenUnarmed = true;
    [SerializeField] private bool useTagCheck;
    [SerializeField] private string playerTag = "Player";

    public HallwayChoice Choice => choice;

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
            Debug.LogWarning("AnomalyDecisionTrigger could not find AnomalyLoopManager in scene.", this);
            return;
        }

        if (requireChoicesToBeArmed && !manager.AreChoicesArmed)
        {
            if (choice == HallwayChoice.NoAnomaly && manager.IsHallwayMirrorTransportArmed)
            {
                return;
            }

            if (logWarningWhenUnarmed)
            {
                Debug.LogWarning(
                    $"AnomalyDecisionTrigger ({choice}) on '{name}' in scene '{gameObject.scene.name}' at {transform.position}: ignored because choices are not armed yet. Enter the main room first to arm choices.",
                    this
                );
            }

            return;
        }

        manager.SubmitChoice(choice);
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
