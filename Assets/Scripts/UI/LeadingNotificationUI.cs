using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays a short message when Rovy transitions into the Exploring state,
/// and exposes a public API for other systems to show contextual messages.
/// </summary>
[DisallowMultipleComponent]
public sealed class LeadingNotificationUI : MonoBehaviour
{
    [SerializeField] private RovyController rovyController;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float displayDuration = 3.0f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float minimumIntervalSeconds = 15.0f;
    [SerializeField] private string[] leadingMessages =
    {
        "Rovyが道を知ってるみたい...",
        "Rovyがリードしてくれる",
        "今日はRovyが先導する番"
    };

    private RovyController.RovyState lastState;
    private float lastNotificationTime = float.NegativeInfinity;
    private Coroutine activeCoroutine;

    private void Start()
    {
        if (rovyController != null)
        {
            lastState = rovyController.CurrentState;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.0f;
        }
    }

    private void Update()
    {
        if (rovyController == null)
        {
            return;
        }

        RovyController.RovyState currentState = rovyController.CurrentState;

        bool transitionedToExploring =
            lastState != RovyController.RovyState.Exploring &&
            currentState == RovyController.RovyState.Exploring;

        lastState = currentState;

        if (transitionedToExploring)
        {
            TryShowRandomLeadingMessage();
        }
    }

    /// <summary>
    /// Shows an arbitrary message using the same fade behavior as the leading notification.
    /// Respects the minimum interval cooldown.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public void ShowMessage(string message)
    {
        if (Time.time - lastNotificationTime < minimumIntervalSeconds)
        {
            return;
        }

        DisplayMessage(message);
    }

    private void TryShowRandomLeadingMessage()
    {
        if (Time.time - lastNotificationTime < minimumIntervalSeconds)
        {
            return;
        }

        if (leadingMessages == null || leadingMessages.Length == 0)
        {
            return;
        }

        string message = leadingMessages[Random.Range(0, leadingMessages.Length)];
        DisplayMessage(message);
    }

    private void DisplayMessage(string message)
    {
        lastNotificationTime = Time.time;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        activeCoroutine = StartCoroutine(ShowAndFadeRoutine(message));
    }

    private IEnumerator ShowAndFadeRoutine(string message)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
        }

        float elapsed = 0.0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        SetAlpha(1.0f);

        yield return new WaitForSeconds(displayDuration);

        elapsed = 0.0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(1.0f - (elapsed / fadeDuration)));
            yield return null;
        }

        SetAlpha(0.0f);
        activeCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }
    }
}
