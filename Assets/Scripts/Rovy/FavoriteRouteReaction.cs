using UnityEngine;

/// <summary>
/// Monitors Rovy's position and fires a Wag + UI notification when Rovy
/// arrives at a favorite waypoint for the first time in each cooldown window.
/// </summary>
[DisallowMultipleComponent]
public sealed class FavoriteRouteReaction : MonoBehaviour
{
    [SerializeField] private RouteMemory routeMemory;
    [SerializeField] private BehaviorExpressions behaviorExpressions;
    [SerializeField] private LeadingNotificationUI notificationUI;
    [SerializeField] private float detectionRadius = 1.5f;
    [SerializeField] private float reactionCooldown = 30.0f;
    [SerializeField] private string[] favoriteMessages =
    {
        "Rovyのお気に入りの場所みたい",
        "ここ、よく来るね",
        "Rovyが嬉しそう"
    };

    private Vector3? lastReactedWaypoint;
    private float nextReactionTime;

    private void Update()
    {
        if (Time.time < nextReactionTime)
        {
            return;
        }

        if (routeMemory == null)
        {
            return;
        }

        if (!routeMemory.TryGetNearestFavoriteWaypoint(transform.position, detectionRadius, out Vector3 waypoint))
        {
            return;
        }

        if (lastReactedWaypoint.HasValue && AreSameWaypoint(lastReactedWaypoint.Value, waypoint))
        {
            return;
        }

        TriggerReaction(waypoint);
        lastReactedWaypoint = waypoint;
        nextReactionTime = Time.time + reactionCooldown;
    }

    private void TriggerReaction(Vector3 waypoint)
    {
        if (behaviorExpressions != null)
        {
            behaviorExpressions.NotifyNameCalled();
        }

        if (notificationUI != null && favoriteMessages != null && favoriteMessages.Length > 0)
        {
            string message = favoriteMessages[Random.Range(0, favoriteMessages.Length)];
            notificationUI.ShowMessage(message);
        }
    }

    private static bool AreSameWaypoint(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude < 0.01f;
    }
}
