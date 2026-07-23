using UnityEngine;

public class RoomLoopSceneContext : MonoBehaviour
{
    [SerializeField] private bool hasAnomaly;
    [SerializeField] private Transform northWestConnectionAnchor;
    [SerializeField] private Transform southEastConnectionAnchor;

    public bool HasAnomaly => hasAnomaly;

    public Transform GetConnectionAnchor(HallwaySide hallwaySide)
    {
        switch (hallwaySide)
        {
            case HallwaySide.NorthWest:
                return northWestConnectionAnchor;
            case HallwaySide.SouthEast:
                return southEastConnectionAnchor;
            default:
                return null;
        }
    }

    public bool HasConnectionAnchors()
    {
        return northWestConnectionAnchor != null && southEastConnectionAnchor != null;
    }
}
