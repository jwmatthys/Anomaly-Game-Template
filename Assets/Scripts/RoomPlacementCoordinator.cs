using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = SilentDebug;

public sealed class RoomPlacementCoordinator
{
    private readonly Transform _northWestRoomMountPoint;
    private readonly Transform _southEastRoomMountPoint;
    private readonly string _northWestEntryPointName;
    private readonly string _southEastEntryPointName;
    private readonly bool _logValidationWarnings;
    private readonly bool _warnOnOppositeSeamMismatch;
    private readonly UnityEngine.Object _logContext;

    public RoomPlacementCoordinator(
        Transform northWestRoomMountPoint,
        Transform southEastRoomMountPoint,
        string northWestEntryPointName,
        string southEastEntryPointName,
        bool logValidationWarnings,
        bool warnOnOppositeSeamMismatch,
        UnityEngine.Object logContext
    )
    {
        _northWestRoomMountPoint = northWestRoomMountPoint;
        _southEastRoomMountPoint = southEastRoomMountPoint;
        _northWestEntryPointName = northWestEntryPointName;
        _southEastEntryPointName = southEastEntryPointName;
        _logValidationWarnings = logValidationWarnings;
        _warnOnOppositeSeamMismatch = warnOnOppositeSeamMismatch;
        _logContext = logContext;
    }

    public void AlignSceneAnchorToHallwayMount(string sceneName, HallwaySide sceneAnchorSide, Transform mount)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        Transform sceneAnchor = FindSceneAnchor(sceneName, sceneAnchorSide);
        if (mount == null || sceneAnchor == null)
        {
            if (_logValidationWarnings)
            {
                Debug.LogWarning(
                    $"AnomalyLoopManager: Could not align scene '{sceneName}' because a hallway mount or scene entry anchor is missing.",
                    _logContext
                );
            }

            return;
        }

        Vector3 positionOffset = mount.position - sceneAnchor.position;
        positionOffset.y = 0f;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            roots[i].transform.position += positionOffset;
        }
    }

    public void AlignRoomSceneToHallwayMount(string sceneName, string endRoomSceneName, string bootstrapSceneName)
    {
        // Room entry mapping is cross-wired: hallway NW mount aligns to room SE anchor.
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        Transform northWestMount = _northWestRoomMountPoint;
        if (northWestMount == null)
        {
            return;
        }

        Scene roomScene = SceneManager.GetSceneByName(sceneName);
        if (!roomScene.IsValid() || !roomScene.isLoaded)
        {
            return;
        }

        RoomLoopSceneContext context = FindSceneContext(sceneName);
        bool isEndRoomScene = string.Equals(sceneName, endRoomSceneName, StringComparison.Ordinal);

        // Cross-connection mapping:
        // NW hallway mount <-> SE room anchor
        // SE hallway mount <-> NW room anchor
        Transform roomNorthWestAnchor = FindSceneAnchor(sceneName, HallwaySide.NorthWest);
        Transform roomSouthEastAnchor = isEndRoomScene
            ? FindTransformInSceneByName(sceneName, _southEastEntryPointName)
            : FindSceneAnchor(sceneName, HallwaySide.SouthEast);

        bool hasRequiredAnchors = isEndRoomScene
            ? roomSouthEastAnchor != null
            : roomNorthWestAnchor != null && roomSouthEastAnchor != null;

        if (!hasRequiredAnchors)
        {
            if (_logValidationWarnings)
            {
                string source = context == null ? "RoomLoopSceneContext or named entry points" : "connection anchors";
                Debug.LogWarning($"AnomalyLoopManager: Room scene '{sceneName}' is missing one or more {source}.", context != null ? context : _logContext);
            }

            return;
        }

        Transform targetMount = isEndRoomScene
            ? FindTransformInSceneByName(bootstrapSceneName, _northWestEntryPointName)
            : northWestMount;
        if (targetMount == null)
        {
            targetMount = northWestMount;
        }

        Transform targetRoomAnchor = roomSouthEastAnchor;

        if (targetMount == null || targetRoomAnchor == null)
        {
            if (_logValidationWarnings)
            {
                Debug.LogWarning($"AnomalyLoopManager: Room scene '{sceneName}' missing NW mount or SE room anchor for translation alignment.", context);
            }

            return;
        }

        Vector3 positionOffset = targetMount.position - targetRoomAnchor.position;
        positionOffset.y = 0f;

        GameObject[] roots = roomScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform root = roots[i].transform;
            root.position += positionOffset;
        }

        if (_logValidationWarnings && _warnOnOppositeSeamMismatch)
        {
            Transform oppositeMount = _southEastRoomMountPoint;
            Transform oppositeRoomAnchor = roomNorthWestAnchor;

            if (oppositeMount != null && oppositeRoomAnchor != null)
            {
                // oppositeRoomAnchor has already moved with the room roots, so compare directly.
                Vector2 predictedXZ = new Vector2(oppositeRoomAnchor.position.x, oppositeRoomAnchor.position.z);
                Vector2 mountXZ = new Vector2(oppositeMount.position.x, oppositeMount.position.z);
                float seamError = Vector2.Distance(predictedXZ, mountXZ);

                if (seamError > 0.05f)
                {
                    float hallwaySpan = Vector2.Distance(
                        new Vector2(northWestMount.position.x, northWestMount.position.z),
                        new Vector2(_southEastRoomMountPoint.position.x, _southEastRoomMountPoint.position.z)
                    );
                    float roomSpan = Vector2.Distance(
                        new Vector2(roomNorthWestAnchor.position.x, roomNorthWestAnchor.position.z),
                        new Vector2(roomSouthEastAnchor.position.x, roomSouthEastAnchor.position.z)
                    );

                    Debug.LogWarning(
                        $"AnomalyLoopManager: Room scene '{sceneName}' seam mismatch is {seamError:0.###}m on the opposite exit. " +
                        $"Hallway span={hallwaySpan:0.###}m, room anchor span={roomSpan:0.###}m. " +
                        "Adjust room NW/SE connection anchor placement to match hallway mount spacing.",
                        context
                    );
                }
            }
        }
    }

    public Transform FindTransformInSceneByName(string sceneName, string targetName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform matched = FindChildByNameRecursive(roots[i].transform, targetName);
            if (matched != null)
            {
                return matched;
            }
        }

        return null;
    }

    public Transform GetHallwayMountPoint(HallwaySide side)
    {
        return side == HallwaySide.NorthWest ? _northWestRoomMountPoint : _southEastRoomMountPoint;
    }

    private Transform FindSceneAnchor(string sceneName, HallwaySide side)
    {
        RoomLoopSceneContext context = FindSceneContext(sceneName);
        if (context != null)
        {
            Transform contextAnchor = context.GetConnectionAnchor(side);
            if (contextAnchor != null)
            {
                return contextAnchor;
            }
        }

        string fallbackAnchorName = side == HallwaySide.NorthWest ? _northWestEntryPointName : _southEastEntryPointName;
        if (string.IsNullOrWhiteSpace(fallbackAnchorName))
        {
            return null;
        }

        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform matched = FindChildByNameRecursive(roots[i].transform, fallbackAnchorName);
            if (matched != null)
            {
                return matched;
            }
        }

        return null;
    }

    private static Transform FindChildByNameRecursive(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildByNameRecursive(root.GetChild(i), targetName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static RoomLoopSceneContext FindSceneContext(string sceneName)
    {
        RoomLoopSceneContext[] contexts = UnityEngine.Object.FindObjectsByType<RoomLoopSceneContext>(FindObjectsInactive.Include);
        for (int i = 0; i < contexts.Length; i++)
        {
            if (string.Equals(contexts[i].gameObject.scene.name, sceneName, StringComparison.Ordinal))
            {
                return contexts[i];
            }
        }

        return null;
    }
}
