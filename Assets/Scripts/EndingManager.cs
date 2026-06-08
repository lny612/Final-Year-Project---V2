using UnityEngine;

// TODO-EDITOR: Wire 7.EndingScene.unity (UI Toolkit migration)
//   The Canvas/RawImage/TMP_Text/CanvasGroup hierarchy is no longer used.
//   1. Add empty GameObject "EndingUIDocument" at scene root with:
//        - UIDocument         (PanelSettings: EndingPanelSettings.asset,
//                              Source: Assets/UI/Ending/EndingScreen.uxml)
//        - EndingScreenController  (drag the 5 ending PNGs into its Texture2D slots)
//   2. On this EndingManager component, drag the EndingScreenController into
//      the `screen` field below.
//   3. Set the legacy Canvas under EndingScene root to inactive (don't delete —
//      matches the "kept for revert" pattern from Title/Morning).

/// <summary>
/// Terminal scene controller. Picks the correct ending variant from
/// <see cref="GameManager"/> state, builds the stats line, and hands off
/// presentation to <see cref="EndingScreenController"/>. The controller owns
/// the typewriter, fade, illustrations, and button handling.
/// </summary>
[DisallowMultipleComponent]
public class EndingManager : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("EndingScreenController on the EndingUIDocument GameObject.")]
    public EndingScreenController screen;

    private void Start()
    {
        if (screen == null) screen = FindObjectOfType<EndingScreenController>();
        if (screen == null)
        {
            Debug.LogError("[EndingManager] No EndingScreenController found in scene. "
                         + "Add an EndingUIDocument GameObject (see TODO-EDITOR above).");
            return;
        }

        EndingType ending = ResolveEnding();
        screen.Render(ending, BuildStats());
    }

    private static EndingType ResolveEnding()
    {
        var gm = GameManager.Instance;
        if (gm == null) return EndingType.Slum;

        // Bankruptcy overrides reputation — player didn't make it to day 7.
        if (gm.playerGold < 0 || gm.currentDay < GameManager.TOTAL_DAYS)
        {
            // Check if we arrived here via the rent flow rather than natural end.
            if (gm.bankruptedOnDay3 || gm.currentDay == 3)
                return EndingType.BankruptEarly;
            // Otherwise assume late bankruptcy if currentDay < TOTAL_DAYS
            if (gm.currentDay < GameManager.TOTAL_DAYS)
                return EndingType.BankruptLate;
        }

        return gm.DetermineEnding();
    }

    private static EndingScreenController.EndingStats BuildStats()
    {
        var gm = GameManager.Instance;
        if (gm == null) return default;
        return new EndingScreenController.EndingStats
        {
            daysSurvived    = Mathf.Min(gm.currentDay, GameManager.TOTAL_DAYS),
            lettersReceived = gm.lettersReceived,
            peakReputation  = gm.peakReputation,
            wandsCrafted    = gm.wandsCrafted,
        };
    }
}
