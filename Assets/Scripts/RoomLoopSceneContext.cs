using UnityEngine;
using UnityEngine.Serialization;

public class RoomLoopSceneContext : MonoBehaviour
{
    [FormerlySerializedAs("hasAnomaly")]
    [SerializeField] private bool roomHasAnomaly;
    [FormerlySerializedAs("northWestConnectionAnchor")]
    [SerializeField] private Transform northWestConnectionAnchorTransform;
    [FormerlySerializedAs("southEastConnectionAnchor")]
    [SerializeField] private Transform southEastConnectionAnchorTransform;

    public bool HasAnomaly => roomHasAnomaly;

    public Transform GetConnectionAnchor(HallwaySide hallwaySide)
    {
        return hallwaySide == HallwaySide.NorthWest ? northWestConnectionAnchorTransform :
               hallwaySide == HallwaySide.SouthEast ? southEastConnectionAnchorTransform :
               null;
    }

    public bool HasConnectionAnchors()
    {
        return northWestConnectionAnchorTransform != null && southEastConnectionAnchorTransform != null;
    }
}
