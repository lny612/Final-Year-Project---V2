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
    public string element;        // What element calls to them?
    public string personality;    // What temperament hides beneath?
    public string purpose;        // What purpose does the wand carry?
    public string reinforcement;  // What must it reinforce — what must it conceal?

    public bool IsComplete() =>
        !string.IsNullOrEmpty(element) &&
        !string.IsNullOrEmpty(personality) &&
        !string.IsNullOrEmpty(purpose) &&
        !string.IsNullOrEmpty(reinforcement);
}

/// <summary>
/// Which memo slot a piece of dossier prose belongs to.
/// Order matches the order rows appear in the field-memo card.
/// </summary>
public enum PlayerMemoField { Element, Personality, Purpose, Reinforcement }
