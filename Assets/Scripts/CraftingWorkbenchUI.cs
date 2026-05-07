using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Wire CraftingScene.unity
//   1. Asset Assets/UI/Crafting/CraftingWorkbenchPanelSettings.asset
//        (UI Toolkit > Panel Settings, ScaleWithScreenSize 1920x1080, match 0.5).
//   2. GameObject "CraftingWorkbenchUIDocument" at scene root.
//   3. UIDocument: PanelSettings = CraftingWorkbenchPanelSettings,
//      Source = Assets/UI/Crafting/CraftingWorkbench.uxml.
//   4. CraftingWorkbenchUI on the same GameObject.
//   5. Assign CraftingManager.workbenchUI to this component.
//   6. Disable (do not delete) the legacy uGUI Crafting widgets on the Canvas
//      (inventory containers, slot images/labels/clear buttons, confirm,
//      result panel, status text). The new panel replaces them visually.

/// <summary>
/// UI Toolkit controller for the wandcrafter's bench. Pure presentation —
/// CraftingManager owns all game logic and calls into this controller's
/// public surface to mirror the memo, inventory, slots, and status text.
///
/// Layout: warm walnut plate framing a parchment memo card on the left,
/// a magic-circle stage with three triangular slot wells in the centre,
/// and a parchment inventory list on the right. The wand reveal happens
/// in the dedicated MinigameScene/EvaluationScene flow, not here.
/// </summary>
[DisallowMultipleComponent]
public class CraftingWorkbenchUI : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument hosting CraftingWorkbench.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    // ── root ────────────────────────────────────────────────────────
    private VisualElement _root;

    // ── memo ────────────────────────────────────────────────────────
    private Label _memoElement;
    private Label _memoPersonality;
    private Label _memoPurpose;
    private Label _memoReinforcement;

    // ── slots (1=Core1, 2=Core2, 3=Wood) ────────────────────────────
    private VisualElement _slot1Image, _slot2Image, _slot3Image;
    private Label _slot1Empty, _slot2Empty, _slot3Empty;
    private Label _slot1Name,  _slot2Name,  _slot3Name;
    private Button _slot1Clear, _slot2Clear, _slot3Clear;

    // ── inventory ───────────────────────────────────────────────────
    private VisualElement _coreList;
    private VisualElement _woodList;

    // ── confirm + status ────────────────────────────────────────────
    private Button _confirmButton;
    private Label  _statusText;

    private Action _confirmAction;
    private readonly List<Action> _slotClearActions = new() { null, null, null }; // index 0..2

    private bool _bootstrapped;

    private void OnEnable() => Bootstrap();

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root = document.rootVisualElement;

        _memoElement       = _root.Q<Label>("memoElement");
        _memoPersonality   = _root.Q<Label>("memoPersonality");
        _memoPurpose       = _root.Q<Label>("memoPurpose");
        _memoReinforcement = _root.Q<Label>("memoReinforcement");

        _slot1Image = _root.Q<VisualElement>("slot1Image");
        _slot2Image = _root.Q<VisualElement>("slot2Image");
        _slot3Image = _root.Q<VisualElement>("slot3Image");
        _slot1Empty = _root.Q<Label>("slot1Empty");
        _slot2Empty = _root.Q<Label>("slot2Empty");
        _slot3Empty = _root.Q<Label>("slot3Empty");
        _slot1Name  = _root.Q<Label>("slot1Name");
        _slot2Name  = _root.Q<Label>("slot2Name");
        _slot3Name  = _root.Q<Label>("slot3Name");
        _slot1Clear = _root.Q<Button>("slot1Clear");
        _slot2Clear = _root.Q<Button>("slot2Clear");
        _slot3Clear = _root.Q<Button>("slot3Clear");

        _coreList = _root.Q<VisualElement>("coreList");
        _woodList = _root.Q<VisualElement>("woodList");

        _confirmButton = _root.Q<Button>("confirmButton");
        _statusText    = _root.Q<Label>("statusText");

        if (_slot1Clear != null) _slot1Clear.clicked += () => _slotClearActions[0]?.Invoke();
        if (_slot2Clear != null) _slot2Clear.clicked += () => _slotClearActions[1]?.Invoke();
        if (_slot3Clear != null) _slot3Clear.clicked += () => _slotClearActions[2]?.Invoke();

        if (_confirmButton != null) _confirmButton.clicked += () => _confirmAction?.Invoke();

        // Mark bootstrapped BEFORE invoking any public API that re-enters Bootstrap
        // (e.g. SetConfirmEnabled). Otherwise the recursion guard at the top of
        // Bootstrap never trips and we stack-overflow.
        _bootstrapped = true;

        // Hide the slot × buttons by default (no material to clear yet).
        SetClearButtonVisible(0, false);
        SetClearButtonVisible(1, false);
        SetClearButtonVisible(2, false);
        SetSlotImageVisible(0, false);
        SetSlotImageVisible(1, false);
        SetSlotImageVisible(2, false);

        if (_confirmButton != null) _confirmButton.SetEnabled(false);

        // Hide any legacy result panel that may still be authored in the UXML
        // (kept for older UXML compatibility — it just stays hidden now).
        var legacyResult = _root.Q<VisualElement>("resultPanel");
        if (legacyResult != null && !legacyResult.ClassListContains("hidden"))
            legacyResult.AddToClassList("hidden");
    }

    // ────────────────────────────────────────────────────────────────
    // Public surface called by CraftingManager
    // ────────────────────────────────────────────────────────────────

    /// <summary>Populate the customer memo (Element / Personality / Purpose / Reinforcement).</summary>
    public void SetMemo(PlayerMemo memo)
    {
        Bootstrap();
        if (_memoElement       != null) _memoElement.text       = memo != null && !string.IsNullOrEmpty(memo.element)       ? memo.element       : "—";
        if (_memoPersonality   != null) _memoPersonality.text   = memo != null && !string.IsNullOrEmpty(memo.personality)   ? memo.personality   : "—";
        if (_memoPurpose       != null) _memoPurpose.text       = memo != null && !string.IsNullOrEmpty(memo.purpose)       ? memo.purpose       : "—";
        if (_memoReinforcement != null) _memoReinforcement.text = memo != null && !string.IsNullOrEmpty(memo.reinforcement) ? memo.reinforcement : "—";
    }

    /// <summary>
    /// Rebuild both inventory lists. <paramref name="onCoreClick"/> and
    /// <paramref name="onWoodClick"/> receive the clicked MaterialData.
    /// </summary>
    public void SetInventory(
        List<MaterialData> cores,
        List<MaterialData> woods,
        Action<MaterialData> onCoreClick,
        Action<MaterialData> onWoodClick)
    {
        Bootstrap();
        FillList(_coreList, cores, onCoreClick);
        FillList(_woodList, woods, onWoodClick);
    }

    /// <summary>
    /// Show <paramref name="mat"/> in the given slot (1=Core1, 2=Core2,
    /// 3=Wood). Pass <c>null</c> to clear. <paramref name="onClear"/> is
    /// called when the player clicks the slot's × button (only used while
    /// the slot is filled).
    /// </summary>
    public void SetSlot(int slot, MaterialData mat, Action onClear)
    {
        Bootstrap();
        int idx = slot - 1;
        if (idx < 0 || idx > 2) return;

        VisualElement img = idx == 0 ? _slot1Image : idx == 1 ? _slot2Image : _slot3Image;
        Label   empty = idx == 0 ? _slot1Empty : idx == 1 ? _slot2Empty : _slot3Empty;
        Label   name  = idx == 0 ? _slot1Name  : idx == 1 ? _slot2Name  : _slot3Name;

        if (mat == null)
        {
            if (img   != null) img.style.backgroundImage = new StyleBackground((Texture2D)null);
            SetSlotImageVisible(idx, false);
            if (empty != null) empty.style.display = DisplayStyle.Flex;
            if (name  != null) name.text = idx == 0 ? "Core 1 (required)"
                                          : idx == 1 ? "Core 2 (optional)"
                                          :            "Wood (required)";
            SetClearButtonVisible(idx, false);
            _slotClearActions[idx] = null;
        }
        else
        {
            if (img != null && mat.generatedImage != null)
            {
                img.style.backgroundImage = new StyleBackground(mat.generatedImage);
                SetSlotImageVisible(idx, true);
            }
            else
            {
                SetSlotImageVisible(idx, false);
            }
            if (empty != null) empty.style.display = DisplayStyle.None;
            if (name  != null) name.text = $"{mat.name}\n<size=11>{mat.price}g</size>";
            SetClearButtonVisible(idx, true);
            _slotClearActions[idx] = onClear;
            AudioManager.Instance?.PlayMaterialSelection();
        }
    }

    public void SetStatus(string message)
    {
        Bootstrap();
        if (_statusText != null) _statusText.text = message ?? "";
    }

    public void SetConfirmEnabled(bool enabled)
    {
        Bootstrap();
        if (_confirmButton == null) return;
        _confirmButton.SetEnabled(enabled);
    }

    public void RegisterConfirm(Action onConfirm)
    {
        Bootstrap();
        _confirmAction = onConfirm;
    }

    // ────────────────────────────────────────────────────────────────
    // Internal helpers
    // ────────────────────────────────────────────────────────────────

    private void FillList(VisualElement list, List<MaterialData> items, Action<MaterialData> onClick)
    {
        if (list == null) return;
        list.Clear();

        if (items == null || items.Count == 0)
        {
            var empty = new Label("(empty)");
            empty.AddToClassList("inv-empty");
            list.Add(empty);
            return;
        }

        foreach (var mat in items)
        {
            var row = BuildInventoryRow(mat);
            row.RegisterCallback<ClickEvent>(_ => onClick?.Invoke(mat));
            list.Add(row);
        }
    }

    private static VisualElement BuildInventoryRow(MaterialData mat)
    {
        var row = new VisualElement();
        row.AddToClassList("inv-row");

        var thumb = new VisualElement();
        thumb.AddToClassList("inv-row-thumb");
        if (mat.generatedImage != null)
            thumb.style.backgroundImage = new StyleBackground(mat.generatedImage);
        row.Add(thumb);

        var textCol = new VisualElement();
        textCol.AddToClassList("inv-row-text");

        var name = new Label(mat.name);
        name.AddToClassList("inv-row-name");
        textCol.Add(name);

        var price = new Label($"{mat.price}g");
        price.AddToClassList("inv-row-price");
        textCol.Add(price);

        row.Add(textCol);
        return row;
    }

    private void SetClearButtonVisible(int idx, bool visible)
    {
        Button b = idx == 0 ? _slot1Clear : idx == 1 ? _slot2Clear : _slot3Clear;
        if (b == null) return;
        b.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetSlotImageVisible(int idx, bool visible)
    {
        VisualElement img = idx == 0 ? _slot1Image : idx == 1 ? _slot2Image : _slot3Image;
        if (img == null) return;
        img.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
