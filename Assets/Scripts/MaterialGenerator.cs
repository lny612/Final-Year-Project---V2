using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the MaterialGeneratorTest scene. Owns no UI of its own — all
/// presentation lives in <see cref="MaterialMarketUI"/> on the MarketUIDocument
/// GameObject. This script just orchestrates:
///   1. Consume <see cref="GameManager.pendingMaterials"/> when MorningScene /
///      CustomerGenerator already pre-generated for us, OR fire pre-gen here
///      if we somehow arrived without it.
///   2. Hand the result list to MaterialMarketUI.BuildCards.
///   3. Poll for in-flight image arrivals (ComfyUI is parallel-streaming).
///   4. Wire memo hints (✦) and the buy/lock-cores/lock-other-woods rules.
/// </summary>
[DisallowMultipleComponent]
public class MaterialGenerator : MonoBehaviour
{
    [Header("API Settings")]
    public string openAIUrl     = MaterialService.DefaultOpenAIUrl;
    public string comfyUIUrl    = MaterialService.DefaultComfyUIUrl;
    [Tooltip("CLIPTextEncode node ID in image_z_image_turbo.json.")]
    public string clipNodeId    = MaterialService.DefaultClipNodeId;
    [Tooltip("KSampler node ID in image_z_image_turbo.json.")]
    public string kSamplerNodeId = MaterialService.DefaultKSamplerNodeId;

    [Header("UI — Toolkit market")]
    [Tooltip("Market controller (MaterialMarketUI) on the MarketUIDocument GameObject.")]
    public MaterialMarketUI marketUI;

    // ── Private state ──────────────────────────────────────────────

    private bool _busy;

    // Flat ordered list mirroring the UI Toolkit card grid.
    // Indices 0..2 = cores, 3..5 = woods. Used to resolve buy clicks coming
    // from MaterialMarketUI.OnBuyClicked.
    private const int CORE_SLOTS = 3;
    private readonly List<MaterialData> _orderedMaterials = new();
    private readonly bool[] _orderedSoldOut = new bool[6];

    // Global index of the wood the player has already bought this round, or -1
    // if none bought yet. Cores stay locked until this is set; the other woods
    // become SOLD OUT after this is set (only one wood per round).
    private int _woodBoughtIdx = -1;

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Start()
    {
        if (marketUI == null)
        {
            Debug.LogError("[MaterialGenerator] marketUI is not assigned. Wire MarketUIDocument in the Inspector.");
            return;
        }

        marketUI.OnBackClicked    += () => GameManager.Instance?.LoadScene(GameManager.SCENE_CUSTOMER);
        marketUI.OnProceedClicked += () => GameManager.Instance?.LoadScene(GameManager.SCENE_CRAFTING);
        marketUI.OnBuyClicked     += OnBuyByGlobalIndex;
        marketUI.RefreshMemo(GameManager.Instance?.currentMemo);
        marketUI.SetGold(GameManager.Instance != null ? GameManager.Instance.playerGold : 0);
        marketUI.SetProceedEnabled(false);
        marketUI.SetStatus("Waiting...");
        marketUI.LockCores(); // hidden until a wood is bought

        var gm = GameManager.Instance;
        if (gm?.currentCustomer != null && gm.availableMaterials.Count == 0)
        {
            // Pre-gen kicked off in MorningScene/CustomerScene normally fills
            // pendingMaterials before we arrive. If neither is set (e.g. user
            // entered this scene directly or pre-gen failed), fire it now on
            // the persistent host (GameManager) so coroutines survive any
            // future scene transitions.
            if (gm.pendingMaterials == null && !gm.pendingMaterialsInProgress)
            {
                gm.StartCoroutine(MaterialService.PreGenAsync(
                    gm, gm, gm.currentCustomer, openAIUrl, comfyUIUrl, clipNodeId, kSamplerNodeId));
            }
            StartCoroutine(UsePreGenMaterials(gm));
        }
    }

    // ── Pre-generation consumption path ─────────────────────────────

    private IEnumerator UsePreGenMaterials(GameManager gm)
    {
        _busy = true;
        ClearCards();

        // If text isn't done yet, wait — capped so a stuck flag never blocks.
        if (gm.pendingMaterials == null)
        {
            SetStatus("Laying out the wares...");
            const float MaxWaitSeconds = 12f;
            float waited = 0f;
            while (gm.pendingMaterials == null && gm.pendingMaterialsInProgress && waited < MaxWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (gm.pendingMaterials == null)
        {
            SetStatus("Error: could not generate materials.");
            Finish();
            yield break;
        }

        // Consume the pre-genned list.
        var materials = gm.pendingMaterials;
        gm.pendingMaterials   = null;
        gm.availableMaterials = materials;

        var cores = materials.FindAll(m => m.materialType == "core");
        var woods = materials.FindAll(m => m.materialType == "wood");

        _orderedMaterials.Clear();
        _orderedMaterials.AddRange(cores);
        _orderedMaterials.AddRange(woods);
        for (int i = 0; i < _orderedSoldOut.Length; i++) _orderedSoldOut[i] = false;
        marketUI.BuildCards(cores, woods);

        ApplyMemoHints(cores, woods);
        ApplyKnownImages(cores, woods);

        if (gm.pendingMaterialsInProgress)
            yield return StartCoroutine(PollPreGenImages(cores, woods));

        // Any image still missing after pre-gen finished? Fire a per-card retry
        // so the player isn't stuck looking at placeholders.
        yield return StartCoroutine(RetryMissingImages(cores, woods));

        SetStatus("Done.");
        Finish();
    }

    private void ApplyKnownImages(List<MaterialData> cores, List<MaterialData> woods)
    {
        for (int i = 0; i < cores.Count; i++)
        {
            var tex = cores[i].generatedImage;
            if (tex != null) marketUI.SetCardImage(i, tex);
        }
        for (int i = 0; i < woods.Count; i++)
        {
            var tex = woods[i].generatedImage;
            if (tex != null) marketUI.SetCardImage(CORE_SLOTS + i, tex);
        }
    }

    private IEnumerator PollPreGenImages(List<MaterialData> cores, List<MaterialData> woods)
    {
        var gm = GameManager.Instance;
        int total = cores.Count + woods.Count;
        SetStatus($"Generating {total} images...");

        const float pollInterval = 0.25f;
        const float maxSeconds   = 90f;
        float waited = 0f;

        while (waited < maxSeconds)
        {
            ApplyKnownImages(cores, woods);
            int gotImages = CountWithImages(cores) + CountWithImages(woods);
            SetStatus($"Generating {gotImages} of {total} images...");
            if (gotImages >= total) break;
            if (gm == null || !gm.pendingMaterialsInProgress) break;
            yield return new WaitForSeconds(pollInterval);
            waited += pollInterval;
        }

        ApplyKnownImages(cores, woods);
    }

    private IEnumerator RetryMissingImages(List<MaterialData> cores, List<MaterialData> woods)
    {
        var queue = new List<(MaterialData mat, int globalIdx)>();
        for (int i = 0; i < cores.Count; i++)
            if (cores[i].generatedImage == null) queue.Add((cores[i], i));
        for (int i = 0; i < woods.Count; i++)
            if (woods[i].generatedImage == null) queue.Add((woods[i], CORE_SLOTS + i));

        if (queue.Count == 0) yield break;

        int remaining = queue.Count;
        SetStatus($"Generating {queue.Count} remaining images...");

        foreach (var (mat, idx) in queue)
        {
            int captured     = idx;
            var capturedMat  = mat;
            StartCoroutine(MaterialService.GenerateImageAsync(
                comfyUIUrl, clipNodeId, kSamplerNodeId, mat.imagePrompt,
                (tex, _) =>
                {
                    if (tex != null)
                    {
                        capturedMat.generatedImage = tex;
                        marketUI.SetCardImage(captured, tex);
                    }
                    remaining--;
                }));
        }

        while (remaining > 0) yield return null;
    }

    private static int CountWithImages(List<MaterialData> list)
    {
        int n = 0;
        foreach (var m in list) if (m.generatedImage != null) n++;
        return n;
    }

    // ── Memo-hint glyph (✦) ─────────────────────────────────────────

    /// <summary>
    /// Toggle a ✦ glyph on each material card whose affinity overlaps the
    /// player's memo. Cores compare against memo.element, woods against
    /// memo.personality.
    /// </summary>
    private void ApplyMemoHints(List<MaterialData> cores, List<MaterialData> woods)
    {
        var memo = GameManager.Instance?.currentMemo;
        if (memo == null) return;

        for (int i = 0; i < cores.Count; i++)
            marketUI.SetHintGlyph(i, IsHintMatch(memo.element, cores[i].elementalAffinity));

        for (int i = 0; i < woods.Count; i++)
            marketUI.SetHintGlyph(CORE_SLOTS + i, IsHintMatch(memo.personality, woods[i].personalityMatch));
    }

    private static bool IsHintMatch(string memoWord, string materialField)
    {
        if (string.IsNullOrWhiteSpace(memoWord) || string.IsNullOrWhiteSpace(materialField)) return false;
        // Memo entries may be a comma-joined list of multiple highlights
        // (e.g. "fire, lightning"). Any single token substring-matching the
        // material's field counts as a hit.
        var tokens = memoWord.Split(new[] { ',', ';', '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in tokens)
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            if (materialField.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // ── Buy handler — driven by MaterialMarketUI clicks ─────────────

    private void OnBuyByGlobalIndex(int idx)
    {
        if (GameManager.Instance == null) return;
        if (idx < 0 || idx >= _orderedMaterials.Count) return;
        if (_orderedSoldOut[idx]) return;

        var mat = _orderedMaterials[idx];
        bool isCore = idx < CORE_SLOTS;

        // Cores are locked until a wood is purchased. The UI hides the cores
        // tab too — this is a defensive guard.
        if (isCore && _woodBoughtIdx < 0)
        {
            SetStatus("Pick a wood first.");
            return;
        }
        // Only one wood may be owned per round.
        if (!isCore && _woodBoughtIdx >= 0) return;

        if (!GameManager.Instance.CanAfford(mat.price))
        {
            marketUI.FlashGoldRed();
            return;
        }

        GameManager.Instance.SpendGold(mat.price);
        GameManager.Instance.AddToInventory(mat);
        _orderedSoldOut[idx] = true;
        marketUI.MarkSoldOut(idx);

        if (!isCore)
        {
            _woodBoughtIdx = idx;
            marketUI.LockOtherWoods(idx);
            marketUI.UnlockCores();
            // Mirror sold-out flag on the other wood slots.
            for (int w = 0; w < CORE_SLOTS; w++)
            {
                int wIdx = CORE_SLOTS + w;
                if (wIdx == idx) continue;
                _orderedSoldOut[wIdx] = true;
            }
        }

        UpdateGoldDisplay();
        CheckProceedButton();
    }

    private void UpdateGoldDisplay()
    {
        int gold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
        marketUI.SetGold(gold);
    }

    private void CheckProceedButton()
    {
        if (GameManager.Instance == null) return;
        var inv = GameManager.Instance.inventory;
        bool hasCore = inv.Exists(m => m.materialType == "core");
        bool hasWood = inv.Exists(m => m.materialType == "wood");
        marketUI.SetProceedEnabled(hasCore && hasWood);
    }

    private void ClearCards()
    {
        _orderedMaterials.Clear();
        for (int i = 0; i < _orderedSoldOut.Length; i++) _orderedSoldOut[i] = false;
        _woodBoughtIdx = -1;
        marketUI.ClearCards();
        marketUI.LockCores();
    }

    private void SetStatus(string msg)
    {
        marketUI?.SetStatus(msg);
        Debug.Log("[MaterialGenerator] " + msg);
    }

    private void Finish() { _busy = false; }
}
