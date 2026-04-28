using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Create PanelSettings asset
//   Project window > right-click Assets/UI/Minigame > Create > UI Toolkit > Panel Settings Asset.
//   Name it "MinigamePanelSettings.asset". Scale Mode: Constant Pixel Size, Reference DPI 96.
//   Sort Order: -1 (so the chrome draws BEHIND the existing UGUI gameplay canvas).
//
// TODO-EDITOR: Wire MinigameTest.unity (and CraftingScene.unity)
//   1. Create empty GameObject "MinigameUIDocument" at scene root.
//   2. Add component: UI Document
//        - Panel Settings : MinigamePanelSettings.asset (created above).
//        - Source Asset   : Assets/UI/Minigame/MinigamePanel.uxml
//   3. Add component: MinigamePanelController
//        - Document : drag the UIDocument from this same GameObject.
//   4. Select the GameObject that holds TracingMinigameUI. In the Inspector:
//        - Drag MinigameUIDocument into the new "Chrome Controller" field.
//
// TODO-EDITOR: Optional font swap
//   Drop a serif TTF (e.g. EB Garamond, IM Fell English, Cardo) into Assets/UI/Minigame/Fonts/
//   then edit the single `-unity-font-definition` line at the top of MinigamePanel.uss.

/// <summary>
/// Lightweight UIDocument controller that drives the minigame chrome layer
/// (vignette, REVELIO title bar, round pill, instruction bar). The runtime
/// gameplay (rune path, gates, cursor, red/blue trail) is still rendered
/// procedurally via UGUI on the existing TracingMinigameUI panel — this
/// controller only owns the styled chrome that frames it.
///
/// All chrome elements have <c>picking-mode="Ignore"</c> in the UXML so mouse
/// input falls through to the UGUI gameplay layer.
/// </summary>
[DisallowMultipleComponent]
public class MinigamePanelController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument that hosts MinigamePanel.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    private Label _roundLabel;
    private Label _instructionLabel;
    private VisualElement _root;

    private void OnEnable()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null) return;

        _root             = document.rootVisualElement;
        _roundLabel       = _root?.Q<Label>("roundLabel");
        _instructionLabel = _root?.Q<Label>("instructionLabel");

        SetVisible(false);
    }

    public void SetRoundText(string text)
    {
        if (_roundLabel != null) _roundLabel.text = text;
    }

    public void SetInstructionText(string text)
    {
        if (_instructionLabel != null) _instructionLabel.text = text;
    }

    public void SetVisible(bool visible)
    {
        if (_root != null)
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
