using TMPro;
using UnityEngine;

/// <summary>
/// Test harness: auto-starts the tracing minigame on scene load.
/// Create a scene "MinigameTest" with a Canvas → MinigamePanel,
/// wire this component, and press Play.
/// </summary>
[DisallowMultipleComponent]
public class MinigameTestRunner : MonoBehaviour
{
    [Tooltip("The TracingMinigameUI on the MinigamePanel.")]
    public TracingMinigameUI tracingMinigame;

    [Tooltip("Optional — shows the grade result after the minigame ends.")]
    public TMP_Text resultText;

    private void Start()
    {
        if (tracingMinigame == null)
        {
            Debug.LogError("[MinigameTestRunner] tracingMinigame is not assigned.");
            return;
        }

        if (resultText != null)
            resultText.text = "";

        tracingMinigame.Begin(grade =>
        {
            Debug.Log($"[MinigameTestRunner] Minigame finished — Grade: {grade}");
            if (resultText != null)
                resultText.text = $"Grade: {grade}";
        });
    }
}
