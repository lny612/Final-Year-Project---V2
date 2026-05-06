using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CraftingScene: inventory + slot selection. On confirm, this scene
/// stashes the chosen materials on <see cref="GameManager"/> and hands
/// off to <see cref="GameManager.SCENE_MINIGAME"/>, where the wand is
/// generated (OpenAI + ComfyUI) in parallel with the tracing minigame.
/// </summary>
[DisallowMultipleComponent]
public class CraftingManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────

    [Header("UI — Left Panel (Inventory)")]
    public Transform coreInventoryContainer;
    public Transform woodInventoryContainer;

    [Header("UI — Center Panel (Slots)")]
    public RawImage  slot1Image;
    public TMP_Text  slot1Name;
    public Button    slot1Clear;

    public RawImage  slot2Image;
    public TMP_Text  slot2Name;
    public Button    slot2Clear;

    public RawImage  slot3Image;
    public TMP_Text  slot3Name;
    public Button    slot3Clear;

    public Button    confirmButton;

    [Header("UI — Status")]
    public TMP_Text statusText;

    [Header("UI — Memo Card (pinned player memo, replaces full dossier)")]
    [Tooltip("Read-only memo card. Shows the 3 keywords the player committed in CustomerGeneratorTest.")]
    public MemoCardUI memoCard;

    [Header("UI Toolkit (new wandcrafter's bench)")]
    [Tooltip("Optional. When assigned, the legacy uGUI inventory/slots above are disabled in scene; the new UI Toolkit panel becomes the player-facing surface.")]
    public CraftingWorkbenchUI workbenchUI;

    // ── Private state ──────────────────────────────────────────────

    private MaterialData _slot1;   // Core 1
    private MaterialData _slot2;   // Core 2 (optional)
    private MaterialData _slot3;   // Wood

    // Inventory items not yet assigned to a slot
    private readonly List<MaterialData> _corePool = new();
    private readonly List<MaterialData> _woodPool = new();

    // UI rows in the inventory panels, parallel to pools
    private readonly List<GameObject> _coreRows = new();
    private readonly List<GameObject> _woodRows = new();

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Start()
    {
        slot1Clear?.onClick.AddListener(() => ClearSlot(1));
        slot2Clear?.onClick.AddListener(() => ClearSlot(2));
        slot3Clear?.onClick.AddListener(() => ClearSlot(3));
        confirmButton?.onClick.AddListener(OnConfirm);

        if (workbenchUI != null)
            workbenchUI.RegisterConfirm(OnConfirm);

        RefreshInventoryUI();
        RefreshConfirmButton();

        if (memoCard != null)
            memoCard.Populate(GameManager.Instance?.currentMemo);
        if (workbenchUI != null)
            workbenchUI.SetMemo(GameManager.Instance?.currentMemo);

        SetStatus("Select materials from your inventory.");
    }

    // ── Inventory UI ──────────────────────────────────────────────

    private void RefreshInventoryUI()
    {
        _corePool.Clear();
        _woodPool.Clear();
        if (coreInventoryContainer != null)
            foreach (Transform t in coreInventoryContainer) Destroy(t.gameObject);
        if (woodInventoryContainer != null)
            foreach (Transform t in woodInventoryContainer) Destroy(t.gameObject);
        _coreRows.Clear();
        _woodRows.Clear();

        if (GameManager.Instance == null) return;

        foreach (var mat in GameManager.Instance.inventory)
        {
            if (mat.materialType == "core")
            {
                _corePool.Add(mat);
                if (coreInventoryContainer != null)
                    _coreRows.Add(CreateInventoryRow(mat, coreInventoryContainer, () => SelectCore(mat)));
            }
            else
            {
                _woodPool.Add(mat);
                if (woodInventoryContainer != null)
                    _woodRows.Add(CreateInventoryRow(mat, woodInventoryContainer, () => SelectWood(mat)));
            }
        }

        RefreshWorkbenchInventory();
    }

    private void RefreshWorkbenchInventory()
    {
        if (workbenchUI == null) return;
        workbenchUI.SetInventory(
            new List<MaterialData>(_corePool),
            new List<MaterialData>(_woodPool),
            SelectCore,
            SelectWood);
    }

    private GameObject CreateInventoryRow(MaterialData mat, Transform parent, Action onClick)
    {
        var go = new GameObject(mat.name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(0, 50);
        int idx = parent.childCount - 1;
        rt.anchoredPosition = new Vector2(0, -idx * 52f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var tt = go.AddComponent<TooltipTrigger>();
        tt.data = mat;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = new Vector2(-10, -6);
        txtRt.anchoredPosition = Vector2.zero;

        var tmp = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = $"<b>{mat.name}</b>  {mat.price}g";
        tmp.fontSize  = 15;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return go;
    }

    private void RemoveInventoryRow(MaterialData mat, List<MaterialData> pool, List<GameObject> rows, Transform container)
    {
        int idx = pool.IndexOf(mat);
        if (idx < 0) return;
        if (idx < rows.Count) Destroy(rows[idx]);
        pool.RemoveAt(idx);
        if (idx < rows.Count) rows.RemoveAt(idx);

        for (int i = 0; i < rows.Count; i++)
        {
            var rt = rows[i].GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(0, -i * 52f);
        }
        RefreshWorkbenchInventory();
    }

    private void ReturnToInventoryPool(MaterialData mat)
    {
        if (mat.materialType == "core")
        {
            _corePool.Add(mat);
            if (coreInventoryContainer != null)
                _coreRows.Add(CreateInventoryRow(mat, coreInventoryContainer, () => SelectCore(mat)));
        }
        else
        {
            _woodPool.Add(mat);
            if (woodInventoryContainer != null)
                _woodRows.Add(CreateInventoryRow(mat, woodInventoryContainer, () => SelectWood(mat)));
        }
        RefreshWorkbenchInventory();
    }

    // ── Slot selection ────────────────────────────────────────────

    private void SelectCore(MaterialData mat)
    {
        if (_slot1 == null)
        {
            _slot1 = mat;
            RemoveInventoryRow(mat, _corePool, _coreRows, coreInventoryContainer);
            RefreshSlotUI(1, mat);
        }
        else if (_slot2 == null)
        {
            _slot2 = mat;
            RemoveInventoryRow(mat, _corePool, _coreRows, coreInventoryContainer);
            RefreshSlotUI(2, mat);
        }
        RefreshConfirmButton();
    }

    private void SelectWood(MaterialData mat)
    {
        if (_slot3 != null)
            ReturnToInventoryPool(_slot3);

        _slot3 = mat;
        RemoveInventoryRow(mat, _woodPool, _woodRows, woodInventoryContainer);
        RefreshSlotUI(3, mat);
        RefreshConfirmButton();
    }

    private void ClearSlot(int slot)
    {
        MaterialData returned = null;
        if (slot == 1 && _slot1 != null) { returned = _slot1; _slot1 = null; }
        else if (slot == 2 && _slot2 != null) { returned = _slot2; _slot2 = null; }
        else if (slot == 3 && _slot3 != null) { returned = _slot3; _slot3 = null; }

        if (returned != null)
        {
            ReturnToInventoryPool(returned);
            ClearSlotUI(slot);
        }
        RefreshConfirmButton();
    }

    private void RefreshSlotUI(int slot, MaterialData mat)
    {
        string label = $"<b>{mat.name}</b>\n{mat.price}g";
        if (slot == 1) { if (slot1Name  != null) slot1Name.text    = label; if (slot1Image != null) slot1Image.texture = mat.generatedImage; }
        if (slot == 2) { if (slot2Name  != null) slot2Name.text    = label; if (slot2Image != null) slot2Image.texture = mat.generatedImage; }
        if (slot == 3) { if (slot3Name  != null) slot3Name.text    = label; if (slot3Image != null) slot3Image.texture = mat.generatedImage; }

        workbenchUI?.SetSlot(slot, mat, () => ClearSlot(slot));
    }

    private void ClearSlotUI(int slot)
    {
        if (slot == 1) { if (slot1Name != null) slot1Name.text = "Core 1 (required)"; if (slot1Image != null) slot1Image.texture = null; }
        if (slot == 2) { if (slot2Name != null) slot2Name.text = "Core 2 (optional)"; if (slot2Image != null) slot2Image.texture = null; }
        if (slot == 3) { if (slot3Name != null) slot3Name.text = "Wood (required)";   if (slot3Image != null) slot3Image.texture = null; }

        workbenchUI?.SetSlot(slot, null, null);
    }

    private void RefreshConfirmButton()
    {
        bool canConfirm = _slot1 != null && _slot3 != null;
        if (confirmButton != null)
            confirmButton.interactable = canConfirm;
        workbenchUI?.SetConfirmEnabled(canConfirm);
    }

    // ── Confirm: commit picks, hand off to MinigameScene ──────────

    private void OnConfirm()
    {
        if (_slot1 == null || _slot3 == null)
        {
            SetStatus("Select at least 1 core and 1 wood.");
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[CraftingManager] No GameManager.Instance — cannot proceed.");
            return;
        }

        // Consume materials from the player's inventory.
        gm.RemoveFromInventory(_slot1);
        if (_slot2 != null) gm.RemoveFromInventory(_slot2);
        gm.RemoveFromInventory(_slot3);

        // Stash the picks for the minigame scene's wand-generation pipeline.
        gm.chosenCore1 = _slot1;
        gm.chosenCore2 = _slot2;
        gm.chosenWood  = _slot3;

        if (confirmButton != null) confirmButton.interactable = false;
        workbenchUI?.SetConfirmEnabled(false);

        SetStatus("Beginning the ritual...");
        gm.LoadScene(GameManager.SCENE_MINIGAME);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        workbenchUI?.SetStatus(msg);
        Debug.Log("[CraftingManager] " + msg);
    }
}
