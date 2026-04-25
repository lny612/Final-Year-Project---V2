using System;

/// <summary>
/// The player's hand-written memo distilled from the customer dossier.
/// Authored in CustomerGeneratorTest via MemoFillUI, consumed in
/// MaterialGeneratorTest (drives material match hints) and CraftingScene
/// (pinned as the sole on-screen reference since the full dossier is hidden).
/// </summary>
[Serializable]
public class PlayerMemo
{
    public string purpose;      // word picked from request or trueGoal
    public string personality;  // word picked from personality or profession
    public string element;      // word picked from schoolOfMagic

    public bool IsComplete() =>
        !string.IsNullOrEmpty(purpose) &&
        !string.IsNullOrEmpty(personality) &&
        !string.IsNullOrEmpty(element);
}

/// <summary>
/// Which memo slot a piece of dossier prose belongs to.
/// Used by MemoFillUI to map source fields -> valid target slots.
/// </summary>
public enum PlayerMemoField { Purpose, Personality, Element }
