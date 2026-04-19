using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Allows the player to call Rovy's name by pressing a configurable key.
/// Implements <see cref="IPlayerInteraction"/> so that <see cref="MoodEngine"/>
/// can read the daily name-call count.
/// </summary>
public sealed class PlayerInteraction : MonoBehaviour, IPlayerInteraction
{
    /// <summary>
    /// Duration in seconds for which the feedback text is displayed after calling Rovy's name.
    /// </summary>
    private const float FeedbackDisplayDuration = 1.5f;

    [Header("Dependencies")]
    [Tooltip("Reference to Rovy's MoodEngine component.")]
    [SerializeField] private MoodEngine moodEngine;

    [Tooltip("TextMeshPro UI element used to display feedback when the player calls Rovy's name.")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Settings")]
    [Tooltip("The key the player presses to call Rovy's name.")]
    [SerializeField] private KeyCode callKey = KeyCode.E;

    /// <summary>
    /// Tracks the number of name calls recorded during the current in-game day.
    /// </summary>
    private int nameCallCountToday;

    /// <summary>
    /// Reference to the active feedback coroutine so it can be restarted on rapid presses.
    /// </summary>
    private Coroutine feedbackCoroutine;

    /// <inheritdoc/>
    public int NameCallCountToday => nameCallCountToday;

    private void Start()
    {
        HideFeedback();
    }

    private void Update()
    {
        if (Input.GetKeyDown(callKey))
        {
            CallName();
        }
    }

    /// <summary>
    /// Performs the name-call action: increments the daily counter,
    /// notifies the <see cref="MoodEngine"/>, and shows UI feedback.
    /// </summary>
    private void CallName()
    {
        nameCallCountToday++;

        if (moodEngine != null)
        {
            moodEngine.NotifyNameCalled();
        }

        ShowFeedback();
    }

    /// <summary>
    /// Displays the "Rovy!" feedback text for <see cref="FeedbackDisplayDuration"/> seconds.
    /// If the player presses the key again while the text is still visible, the timer resets.
    /// </summary>
    private void ShowFeedback()
    {
        if (feedbackText == null)
        {
            return;
        }

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }

        feedbackCoroutine = StartCoroutine(FeedbackRoutine());
    }

    /// <summary>
    /// Coroutine that shows the feedback text and hides it after the configured duration.
    /// </summary>
    /// <returns>An enumerator for the coroutine.</returns>
    private IEnumerator FeedbackRoutine()
    {
        feedbackText.text = "Rovy!";
        feedbackText.gameObject.SetActive(true);

        yield return new WaitForSeconds(FeedbackDisplayDuration);

        HideFeedback();
        feedbackCoroutine = null;
    }

    /// <summary>
    /// Hides the feedback text element.
    /// </summary>
    private void HideFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Resets the daily name-call counter to zero.
    /// Call this method when a new in-game day begins.
    /// </summary>
    public void ResetDailyCount()
    {
        nameCallCountToday = 0;
    }
}
