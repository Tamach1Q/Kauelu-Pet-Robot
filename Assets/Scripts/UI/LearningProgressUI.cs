using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays Rovy's route-learning progress as a progress bar and percentage text.
/// Progress combines exploration breadth (how many nodes are known) and
/// route mastery (what fraction of known nodes are favorites).
/// </summary>
[DisallowMultipleComponent]
public sealed class LearningProgressUI : MonoBehaviour
{
    [SerializeField] private RouteMemory routeMemory;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private float updateInterval = 1.0f;
    [SerializeField] private int targetNodeCount = 200;
    [SerializeField] [Range(0.0f, 1.0f)] private float breadthWeight = 0.7f;
    [SerializeField] [Range(0.0f, 1.0f)] private float masteryWeight = 0.3f;
    [SerializeField] [Min(0.1f)] private float smoothSpeed = 2.0f;

    private float targetProgress;
    private float currentDisplayedProgress;
    private float nextUpdateTime;

    private void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            RecalculateTargetProgress();
            nextUpdateTime = Time.time + updateInterval;
        }

        currentDisplayedProgress = Mathf.Lerp(
            currentDisplayedProgress,
            targetProgress,
            smoothSpeed * Time.deltaTime);

        ApplyToUI();
    }

    private void RecalculateTargetProgress()
    {
        if (routeMemory == null)
        {
            targetProgress = 0.0f;
            return;
        }

        int known = routeMemory.KnownNodeCount;
        int favorites = routeMemory.FavoriteRouteCount;

        float breadth = Mathf.Clamp01((float)known / Mathf.Max(1, targetNodeCount));
        float mastery = known > 0 ? Mathf.Clamp01((float)favorites / known) : 0.0f;

        targetProgress = (breadth * breadthWeight) + (mastery * masteryWeight);
    }

    private void ApplyToUI()
    {
        if (progressBar != null)
        {
            progressBar.value = currentDisplayedProgress;
        }

        if (progressText != null)
        {
            progressText.text = $"学習: {Mathf.RoundToInt(currentDisplayedProgress * 100)}%";
        }
    }
}
