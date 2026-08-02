public sealed class HallwayMirrorTriggerController
{
    public bool IsMirrorTransportArmed { get; private set; }

    public void DisarmMirrorTransport()
    {
        IsMirrorTransportArmed = false;
    }

    public void ArmMirrorTransport(HallwayChoice? lastSubmittedChoice)
    {
        IsMirrorTransportArmed = lastSubmittedChoice.HasValue && lastSubmittedChoice.Value == HallwayChoice.Anomaly;
    }

    public bool TryConsumeMirrorTransport(HallwayChoice? lastSubmittedChoice)
    {
        if (!IsMirrorTransportArmed || !lastSubmittedChoice.HasValue || lastSubmittedChoice.Value != HallwayChoice.Anomaly)
        {
            return false;
        }

        IsMirrorTransportArmed = false;
        return true;
    }
}
