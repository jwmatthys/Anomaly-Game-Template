using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
public class HallwayPreloadZone : MonoBehaviour
{
    [FormerlySerializedAs("loopManager")]
    [SerializeField] private AnomalyLoopManager loopManagerOverride;
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

        AnomalyLoopManager manager = loopManagerOverride != null ? loopManagerOverride : AnomalyLoopManager.Instance;
        if (manager == null)
        {
            return;
        }

        manager.EnterBlindSpotZone();
    }
}
