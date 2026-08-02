using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
public class AnomalyDecisionTrigger : MonoBehaviour
{
    [FormerlySerializedAs("choice")]
    [SerializeField] private HallwayChoice decisionChoice = HallwayChoice.NoAnomaly;
    [FormerlySerializedAs("loopManager")]
    [SerializeField] private AnomalyLoopManager loopManagerOverride;
    [FormerlySerializedAs("requireChoicesToBeArmed")]
    [SerializeField] private bool requireArmedChoices = true;
    [FormerlySerializedAs("useTagCheck")]
    [SerializeField] private bool usePlayerTagCheck;
    [FormerlySerializedAs("playerTag")]
    [SerializeField] private string playerTagName = "Player";

    public HallwayChoice Choice => decisionChoice;

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

        if (requireArmedChoices && !manager.AreChoicesArmed)
        {
            return;
        }

        manager.SubmitChoice(decisionChoice);
    }
}
