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
/// (wood frame, spell-name title plaque, round badge, instruction card with
/// keycap glyph). The runtime gameplay (rune path, gates, cursor, red/blue
/// trail) is still rendered procedurally via UGUI on the existing
/// TracingMinigameUI panel — this controller only owns the chrome that
/// frames it.
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

    [Header("Background")]
    [Tooltip("Painted backdrop. Drag Assets/Texture/Mini game Bg image.png here. " +
             "When assigned, it replaces the procedural ink/halo layers.")]
    public Texture2D backgroundImage;

    private VisualElement _root;
    private VisualElement _inkLayer;
    private VisualElement _haloLayer;
    private Label _titleLabel;
    private Label _roundLabel;
    private Label _instructionLabel;
    private Label _instructionSub;
    private Label _keyGlyphLabel;

    private void OnEnable()
    {
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null) return;

        _root             = document.rootVisualElement;
        if (_root == null) return;

        _inkLayer         = _root.Q<VisualElement>("inkLayer");
        _haloLayer        = _root.Q<VisualElement>("haloLayer");
        _titleLabel       = _root.Q<Label>("titleLabel");
        _roundLabel       = _root.Q<Label>("roundLabel");
        _instructionLabel = _root.Q<Label>("instructionLabel");
        _instructionSub   = _root.Q<Label>("instructionSub");
        _keyGlyphLabel    = _root.Q<Label>("keyGlyphLabel");

        ApplyBackgroundImage();

        SetVisible(false);
    }

    private void ApplyBackgroundImage()
    {
        if (_inkLayer == null) return;
        if (backgroundImage != null)
        {
            _inkLayer.style.backgroundImage = new StyleBackground(backgroundImage);
            _inkLayer.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            // The painting already provides its own depth/mood; hide the
            // procedural warm halo so it doesn't mute the image.
            if (_haloLayer != null) _haloLayer.style.display = DisplayStyle.None;
        }
        else
        {
            _inkLayer.style.backgroundImage = new StyleBackground((Texture2D)null);
            if (_haloLayer != null) _haloLayer.style.display = DisplayStyle.Flex;
        }
    }

    /// <summary>Spell name (e.g. "REVELIO", "LEVIOSO"). Defaults to "REVELIO".</summary>
    public void SetSpellName(string name)
    {
        if (_titleLabel != null) _titleLabel.text = (name ?? "").ToUpperInvariant();
    }

    /// <summary>Round indicator text (e.g. "1 / 3").</summary>
    public void SetRoundText(string text)
    {
        if (_roundLabel != null) _roundLabel.text = text;
    }

    /// <summary>Main hint line shown in the instruction card.</summary>
    public void SetInstructionText(string text)
    {
        if (_instructionLabel != null) _instructionLabel.text = text;
    }

    /// <summary>Italic sub-line under the main hint (smaller, optional).</summary>
    public void SetInstructionSub(string text)
    {
        if (_instructionSub != null)
        {
            _instructionSub.text = text ?? "";
            _instructionSub.style.display = string.IsNullOrEmpty(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }
    }

    /// <summary>Letter shown inside the parchment keycap glyph.</summary>
    public void SetKeyGlyph(string letter)
    {
        if (_keyGlyphLabel != null) _keyGlyphLabel.text = letter ?? "";
    }

    public void SetVisible(bool visible)
    {
        if (_root != null)
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
