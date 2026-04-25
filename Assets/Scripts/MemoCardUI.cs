using TMPro;
using UnityEngine;

/// <summary>
/// Read-only display of the player's PlayerMemo. Pinned in MaterialGeneratorTest
/// and CraftingScene as the sole on-screen reference — the full customer dossier
/// is no longer shown after the player leaves CustomerGeneratorTest, so this card
/// is everything they have to work from.
///
/// Null-safe: Populate(null) blanks the slots and leaves no exceptions.
/// </summary>
[DisallowMultipleComponent]
public class MemoCardUI : MonoBehaviour
{
    [Header("Slot Values (player's committed words)")]
    public TMP_Text purposeText;
    public TMP_Text personalityText;
    public TMP_Text elementText;

    [Header("Slot Labels (question prompts)")]
    [Tooltip("Editable so the question wording can be tuned in-scene without a code change.")]
    public TMP_Text purposeLabel;
    public TMP_Text personalityLabel;
    public TMP_Text elementLabel;

    private const string PLACEHOLDER = "—";
    private const string DEFAULT_PURPOSE_LABEL     = "What do they really want?";
    private const string DEFAULT_PERSONALITY_LABEL = "What temperament hides beneath?";
    private const string DEFAULT_ELEMENT_LABEL     = "What element calls to them?";

    private void Awake()
    {
        if (purposeLabel     != null && string.IsNullOrEmpty(purposeLabel.text))     purposeLabel.text     = DEFAULT_PURPOSE_LABEL;
        if (personalityLabel != null && string.IsNullOrEmpty(personalityLabel.text)) personalityLabel.text = DEFAULT_PERSONALITY_LABEL;
        if (elementLabel     != null && string.IsNullOrEmpty(elementLabel.text))     elementLabel.text     = DEFAULT_ELEMENT_LABEL;
    }

    public void Populate(PlayerMemo memo)
    {
        if (purposeText     != null) purposeText.text     = memo != null && !string.IsNullOrEmpty(memo.purpose)     ? memo.purpose     : PLACEHOLDER;
        if (personalityText != null) personalityText.text = memo != null && !string.IsNullOrEmpty(memo.personality) ? memo.personality : PLACEHOLDER;
        if (elementText     != null) elementText.text     = memo != null && !string.IsNullOrEmpty(memo.element)     ? memo.element     : PLACEHOLDER;
    }
}
