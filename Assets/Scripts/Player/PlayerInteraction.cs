using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
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

    [Header("Events")]
    [Tooltip("Fired each time the player successfully calls Rovy's name.")]
    [SerializeField] private UnityEvent onNameCalled = new UnityEvent();
    

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
        if (Keyboard.current == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (WasCallKeyPressedThisFrame(keyboard, callKey))
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
            onNameCalled.Invoke();
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

    private static bool WasCallKeyPressedThisFrame(Keyboard keyboard, KeyCode keyCode)
    {
        if (!TryGetInputSystemKey(keyCode, out Key key))
        {
            return false;
        }

        return keyboard[key].wasPressedThisFrame;
    }

    private static bool TryGetInputSystemKey(KeyCode keyCode, out Key key)
    {
        switch (keyCode)
        {
            case KeyCode.Alpha0:
                key = Key.Digit0;
                return true;
            case KeyCode.Alpha1:
                key = Key.Digit1;
                return true;
            case KeyCode.Alpha2:
                key = Key.Digit2;
                return true;
            case KeyCode.Alpha3:
                key = Key.Digit3;
                return true;
            case KeyCode.Alpha4:
                key = Key.Digit4;
                return true;
            case KeyCode.Alpha5:
                key = Key.Digit5;
                return true;
            case KeyCode.Alpha6:
                key = Key.Digit6;
                return true;
            case KeyCode.Alpha7:
                key = Key.Digit7;
                return true;
            case KeyCode.Alpha8:
                key = Key.Digit8;
                return true;
            case KeyCode.Alpha9:
                key = Key.Digit9;
                return true;
            case KeyCode.Return:
                key = Key.Enter;
                return true;
            case KeyCode.KeypadEnter:
                key = Key.NumpadEnter;
                return true;
            case KeyCode.Keypad0:
                key = Key.Numpad0;
                return true;
            case KeyCode.Keypad1:
                key = Key.Numpad1;
                return true;
            case KeyCode.Keypad2:
                key = Key.Numpad2;
                return true;
            case KeyCode.Keypad3:
                key = Key.Numpad3;
                return true;
            case KeyCode.Keypad4:
                key = Key.Numpad4;
                return true;
            case KeyCode.Keypad5:
                key = Key.Numpad5;
                return true;
            case KeyCode.Keypad6:
                key = Key.Numpad6;
                return true;
            case KeyCode.Keypad7:
                key = Key.Numpad7;
                return true;
            case KeyCode.Keypad8:
                key = Key.Numpad8;
                return true;
            case KeyCode.Keypad9:
                key = Key.Numpad9;
                return true;
            case KeyCode.KeypadDivide:
                key = Key.NumpadDivide;
                return true;
            case KeyCode.KeypadMultiply:
                key = Key.NumpadMultiply;
                return true;
            case KeyCode.KeypadMinus:
                key = Key.NumpadMinus;
                return true;
            case KeyCode.KeypadPlus:
                key = Key.NumpadPlus;
                return true;
            case KeyCode.KeypadPeriod:
                key = Key.NumpadPeriod;
                return true;
            case KeyCode.KeypadEquals:
                key = Key.NumpadEquals;
                return true;
            case KeyCode.LeftControl:
                key = Key.LeftCtrl;
                return true;
            case KeyCode.RightControl:
                key = Key.RightCtrl;
                return true;
        }

        if (Enum.TryParse(keyCode.ToString(), true, out key))
        {
            return true;
        }

        key = Key.None;
        return false;
    }
}
