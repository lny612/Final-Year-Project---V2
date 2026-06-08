using UnityEngine;

// TODO-EDITOR: Wire EndingPreviewScene.unity (NOT in Build Settings)
//   1. Create a fresh scene at Assets/Scenes/EndingPreviewScene.unity.
//   2. Add empty GameObject "EndingUIDocument".
//   3. Add UIDocument: PanelSettings = Assets/UI/Ending/EndingPanelSettings.asset,
//      Source = Assets/UI/Ending/EndingScreen.uxml.
//   4. Add EndingScreenController on the same GameObject. Drag the 5 PNGs into
//      its Texture2D slots (royalArt, rivalArt, slumArt, bankruptEarlyArt, bankruptLateArt).
//   5. Add EndingPreviewController on the same GameObject. Wire its `screen` field
//      to the EndingScreenController above.
//   6. Set `previewEnding` in the Inspector to whichever variant you want to view.
//      Press Play. Stop, change the dropdown, press Play again to flip variants.
//   7. Do NOT add this scene to Build Settings — it's a developer-only previewer.

/// <summary>
/// Inspector-driven previewer for <see cref="EndingScreenController"/>. Lives in
/// a dev-only scene (not in Build Settings) so the ending visuals can be checked
/// without playing through 7 days. The `previewEnding` enum picks the variant.
/// No in-game UI — the picker is Inspector-only by design.
/// </summary>
[DisallowMultipleComponent]
public class EndingPreviewController : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("EndingScreenController instance on the same UIDocument GameObject.")]
    public EndingScreenController screen;

    [Header("Preview")]
    [Tooltip("Which ending variant to render on Play. Change & re-press Play, or "
           + "leave Play running and tweak this — OnValidate re-renders live.")]
    public EndingType previewEnding = EndingType.Royal;

    private void Start()
    {
        if (screen == null) screen = GetComponent<EndingScreenController>();
        if (screen == null)
        {
            Debug.LogError("[EndingPreviewController] No EndingScreenController wired or on this GameObject.");
            return;
        }

        // Override the screen's restart/quit to act as a re-render in preview mode
        // — no scene loads, no editor stop. Lets the dev press the live buttons
        // without leaving the preview scene.
        screen.OnRestartRequested -= ReRender; // idempotent in case OnEnable double-fires
        screen.OnRestartRequested += ReRender;
        screen.OnQuitRequested    -= ReRender;
        screen.OnQuitRequested    += ReRender;

        ReRender();
    }

    private void ReRender()
    {
        if (screen == null) return;
        screen.Render(previewEnding, MockStatsFor(previewEnding));
    }

    [ContextMenu("Re-render")]
    private void ReRenderContextMenu() => ReRender();

#if UNITY_EDITOR
    // Live-update during Play when the dropdown changes in Inspector.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        // Defer one frame so we don't fight Unity's serialize-callback ordering.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || !isActiveAndEnabled) return;
            ReRender();
        };
    }
#endif

    // Hand-picked sample numbers per variant so the stats line reads believable
    // for each ending (a Royal run has more wands & higher rep than a bankrupt one).
    private static EndingScreenController.EndingStats MockStatsFor(EndingType e) => e switch
    {
        EndingType.Royal => new EndingScreenController.EndingStats
        {
            daysSurvived = 7, lettersReceived = 8, peakReputation = 95, wandsCrafted = 12,
        },
        EndingType.Rival => new EndingScreenController.EndingStats
        {
            daysSurvived = 7, lettersReceived = 8, peakReputation = 58, wandsCrafted = 9,
        },
        EndingType.Slum => new EndingScreenController.EndingStats
        {
            daysSurvived = 7, lettersReceived = 8, peakReputation = 18, wandsCrafted = 6,
        },
        EndingType.BankruptEarly => new EndingScreenController.EndingStats
        {
            daysSurvived = 3, lettersReceived = 3, peakReputation = 8, wandsCrafted = 2,
        },
        EndingType.BankruptLate => new EndingScreenController.EndingStats
        {
            daysSurvived = 6, lettersReceived = 7, peakReputation = 42, wandsCrafted = 7,
        },
        _ => default,
    };
}
