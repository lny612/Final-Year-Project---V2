using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Manages the CraftingScene: inventory display, slot selection, OpenAI wand
/// generation, and ComfyUI wand image generation.
/// </summary>
[DisallowMultipleComponent]
public class CraftingManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────

    [Header("API Settings")]
    public string openAIUrl  = "https://api.openai.com/v1/chat/completions";
    public string comfyUIUrl = "http://127.0.0.1:8000";
    public string clipNodeId     = "5";
    public string kSamplerNodeId = "4";

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

    [Header("UI — Right Panel (Result)")]
    public RawImage  wandResultImage;
    public TMP_Text  wandNameText;
    public TMP_Text  wandDescText;
    public TMP_Text  wandAttribText;
    public Button    proceedButton;
    public GameObject resultPanel;

    [Header("UI — Status")]
    public TMP_Text statusText;

    [Header("UI — Memo Card (pinned player memo, replaces full dossier)")]
    [Tooltip("Read-only memo card. Shows the 3 keywords the player committed in CustomerGeneratorTest.")]
    public MemoCardUI memoCard;

    [Header("Minigame")]
    [Tooltip("Assign the TracingMinigameUI component on the MinigamePanel.")]
    public TracingMinigameUI tracingMinigame;
    // TODO-EDITOR: Create a full-screen UI Panel "MinigamePanel" under Canvas
    //   (anchored stretch-fill, initially inactive). Add TracingMinigameUI component.
    //   Add child TMP_Text "RoundText" (top-center) and "InstructionText" (bottom-center).
    //   Wire all fields. Then assign this field on CraftingManager.

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

    private bool _busy;
    private bool _minigameDone;
    private bool _pipelineDone;

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        if (proceedButton != null) proceedButton.gameObject.SetActive(false);

        slot1Clear?.onClick.AddListener(() => ClearSlot(1));
        slot2Clear?.onClick.AddListener(() => ClearSlot(2));
        slot3Clear?.onClick.AddListener(() => ClearSlot(3));
        confirmButton?.onClick.AddListener(OnConfirm);

        RefreshInventoryUI();
        RefreshConfirmButton();

        if (memoCard != null)
            memoCard.Populate(GameManager.Instance?.currentMemo);

        SetStatus("Select materials from your inventory.");
    }

    // ── Inventory UI ──────────────────────────────────────────────

    private void RefreshInventoryUI()
    {
        _corePool.Clear();
        _woodPool.Clear();
        foreach (Transform t in coreInventoryContainer) Destroy(t.gameObject);
        foreach (Transform t in woodInventoryContainer) Destroy(t.gameObject);
        _coreRows.Clear();
        _woodRows.Clear();

        if (GameManager.Instance == null) return;

        foreach (var mat in GameManager.Instance.inventory)
        {
            if (mat.materialType == "core")
            {
                _corePool.Add(mat);
                _coreRows.Add(CreateInventoryRow(mat, coreInventoryContainer, () => SelectCore(mat)));
            }
            else
            {
                _woodPool.Add(mat);
                _woodRows.Add(CreateInventoryRow(mat, woodInventoryContainer, () => SelectWood(mat)));
            }
        }
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
        // Stack rows
        int idx = parent.childCount - 1;
        rt.anchoredPosition = new Vector2(0, -idx * 52f);

        // Background image (acts as button target)
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() => onClick?.Invoke());

        // Tooltip trigger
        var tt = go.AddComponent<TooltipTrigger>();
        tt.data = mat;

        // Name text child
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
        Destroy(rows[idx]);
        pool.RemoveAt(idx);
        rows.RemoveAt(idx);

        // Restack remaining rows
        for (int i = 0; i < rows.Count; i++)
        {
            var rt = rows[i].GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(0, -i * 52f);
        }
    }

    private void ReturnToInventoryPool(MaterialData mat)
    {
        if (mat.materialType == "core")
        {
            _corePool.Add(mat);
            _coreRows.Add(CreateInventoryRow(mat, coreInventoryContainer, () => SelectCore(mat)));
        }
        else
        {
            _woodPool.Add(mat);
            _woodRows.Add(CreateInventoryRow(mat, woodInventoryContainer, () => SelectWood(mat)));
        }
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
        // Both slots full — do nothing
        RefreshConfirmButton();
    }

    private void SelectWood(MaterialData mat)
    {
        if (_slot3 != null)
        {
            // Return previous wood to pool
            ReturnToInventoryPool(_slot3);
        }
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
    }

    private void ClearSlotUI(int slot)
    {
        if (slot == 1) { if (slot1Name != null) slot1Name.text = "Core 1 (required)"; if (slot1Image != null) slot1Image.texture = null; }
        if (slot == 2) { if (slot2Name != null) slot2Name.text = "Core 2 (optional)"; if (slot2Image != null) slot2Image.texture = null; }
        if (slot == 3) { if (slot3Name != null) slot3Name.text = "Wood (required)";   if (slot3Image != null) slot3Image.texture = null; }
    }

    private void RefreshConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.interactable = _slot1 != null && _slot3 != null;
    }

    // ── Confirm: OpenAI wand + ComfyUI image ──────────────────────

    private void OnConfirm()
    {
        if (_busy) return;
        if (_slot1 == null || _slot3 == null)
        {
            SetStatus("Select at least 1 core and 1 wood.");
            return;
        }

        // Consume from GameManager inventory
        GameManager.Instance?.RemoveFromInventory(_slot1);
        if (_slot2 != null) GameManager.Instance?.RemoveFromInventory(_slot2);
        GameManager.Instance?.RemoveFromInventory(_slot3);

        _busy = true;
        _minigameDone = false;
        _pipelineDone = false;
        confirmButton.interactable = false;

        // Start wand generation immediately (runs during minigame)
        StartCoroutine(CraftingPipeline());

        // Start minigame in parallel — grade only affects evaluation rewards
        if (tracingMinigame != null)
        {
            tracingMinigame.Begin(grade =>
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.craftingQualityGrade = grade;
                _minigameDone = true;
                TryShowResult();
            });
        }
        else
        {
            _minigameDone = true; // no minigame wired — skip (testing)
        }
    }

    private IEnumerator CraftingPipeline()
    {
        // Step 1 — OpenAI wand description
        SetStatus("Consulting the wandmaker's tome...");
        string apiKey = null;
        yield return StartCoroutine(LoadApiKey(k => apiKey = k));
        if (string.IsNullOrEmpty(apiKey))
        {
            SetStatus("Error: no API key found in StreamingAssets/config.json");
            _pipelineDone = true;
            _busy = false;
            confirmButton.interactable = true;
            yield break;
        }

        string userMsg = BuildWandUserMessage();
        var body = new JObject
        {
            ["model"]    = "gpt-4o",
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = WandSystemPrompt },
                new JObject { ["role"] = "user",   ["content"] = userMsg }
            }
        };

        string content = null;
        yield return StartCoroutine(PostOpenAI(apiKey, body, r => content = r));
        if (content == null)
        {
            SetStatus("Error: wand generation API call failed. Check API key and connection.");
            _pipelineDone = true;
            _busy = false;
            confirmButton.interactable = true;
            yield break;
        }

        content = StripCodeFences(content);
        WandResult wand = null;
        try
        {
            var j = JObject.Parse(content);
            wand = new WandResult
            {
                wandName    = j["wandName"]?.ToString()    ?? "Unknown Wand",
                description = j["description"]?.ToString() ?? "",
                imagePrompt = j["imagePrompt"]?.ToString() ?? "",
                attributes  = (j["attributes"] as JArray)?.ToObject<string[]>() ?? Array.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            Debug.LogError("[CraftingManager] Wand parse: " + ex.Message);
            SetStatus("Error: unexpected wand response format from OpenAI.");
            _pipelineDone = true;
            _busy = false;
            confirmButton.interactable = true;
            yield break;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.currentWandResult = wand;

        // Show text result immediately
        ShowWandText(wand);

        // Step 2 — ComfyUI wand image
        SetStatus("Forging the wand...");
        Texture2D wandTex = null;
        yield return StartCoroutine(RunImageGeneration(wand.imagePrompt, t => wandTex = t));

        if (wandTex != null)
        {
            wand.wandImage = wandTex;
            if (wandResultImage != null)
            {
                wandResultImage.gameObject.SetActive(true);
                wandResultImage.texture = wandTex;
            }
        }
        else
        {
            Debug.LogError("[CraftingManager] Wand image generation failed; showing placeholder.");
        }

        _pipelineDone = true;
        TryShowResult();
    }

    /// <summary>
    /// Called by both the minigame callback and CraftingPipeline.
    /// Shows the result panel only when both are done.
    /// </summary>
    private void TryShowResult()
    {
        if (!_minigameDone || !_pipelineDone) return;

        char grade = GameManager.Instance?.craftingQualityGrade ?? 'A';
        SetStatus($"Wand forged (Quality: {grade}). Proceed to evaluation when ready.");

        if (resultPanel   != null) resultPanel.SetActive(true);
        if (proceedButton != null)
        {
            proceedButton.gameObject.SetActive(true);
            proceedButton.onClick.AddListener(
                () => GameManager.Instance?.LoadScene(GameManager.SCENE_EVALUATION));
        }

        _busy = false;
    }

    private void ShowWandText(WandResult wand)
    {
        if (resultPanel  != null) resultPanel.SetActive(true);
        if (wandNameText != null) wandNameText.text = wand.wandName;
        if (wandDescText != null) wandDescText.text = wand.description;
        if (wandAttribText != null)
        {
            if (wand.attributes != null && wand.attributes.Length > 0)
                wandAttribText.text = string.Join("\n• ", wand.attributes);
            else
                wandAttribText.text = "";
        }
    }

    // ── Wand prompts ──────────────────────────────────────────────

    private const string WandSystemPrompt =
@"You are a master wandmaker writing in a fantasy journal.
Given the materials a wand is made from, you synthesise what kind of
wand they produce together. Your reasoning must follow real-world and
folkloric logic — nothing arbitrary.

Rules:
- The wand's properties must emerge logically from the combination
  of materials, not just list them separately.
- If two cores are used, reason about how their affinities interact —
  do they amplify each other, conflict, or create something unexpected?
- The wood's personality traits shape how the magic is channelled,
  not what the magic does. A rigid wood steadies volatile magic.
  A flexible wood lets the caster's emotion guide the spell.
- Never produce generic fantasy flavour. Every sentence must be
  traceable back to a specific property of a specific material used.
- wandName must be evocative and specific to this exact combination.
  Not ""The Wand of Power."" Something like ""The Tidebreaker"" or
  ""Ashbone Whisperer.""

Return ONLY valid JSON. No markdown. No explanation.
Use exactly this structure:
{
  ""wandName"": string,
  ""description"": string,
  ""attributes"": [string, string, string],
  ""imagePrompt"": string
}";

    private string BuildWandUserMessage()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("A wand is being crafted from these materials:");
        sb.AppendLine();
        sb.AppendLine("CORE 1:");
        AppendCore(sb, _slot1);
        if (_slot2 != null)
        {
            sb.AppendLine();
            sb.AppendLine("CORE 2:");
            AppendCore(sb, _slot2);
        }
        sb.AppendLine();
        sb.AppendLine("WOOD:");
        sb.AppendLine($"Name: {_slot3.name}");
        sb.AppendLine($"Personality Match: {_slot3.personalityMatch}");
        sb.AppendLine($"Attributes: {_slot3.attributes}");
        sb.AppendLine();
        sb.AppendLine("Reason about how these materials work together.");
        sb.AppendLine("Return the wand result JSON.");
        return sb.ToString();
    }

    private static void AppendCore(System.Text.StringBuilder sb, MaterialData c)
    {
        sb.AppendLine($"Name: {c.name}");
        sb.AppendLine($"Elemental Affinity: {c.elementalAffinity}");
        sb.AppendLine($"Attributes: {c.attributes}");
        sb.AppendLine($"Special: {c.special}");
    }

    // ── ComfyUI image generation (identical logic to MaterialGenerator) ──

    private IEnumerator RunImageGeneration(string prompt, Action<Texture2D> onDone)
    {
        string workflowPath = Path.Combine(Application.streamingAssetsPath, "image_z_image_turbo.json");
        string workflowJson = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var r = UnityWebRequest.Get(workflowPath))
        {
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
                workflowJson = r.downloadHandler.text;
        }
#else
        if (File.Exists(workflowPath))
            workflowJson = File.ReadAllText(workflowPath);
#endif

        if (string.IsNullOrEmpty(workflowJson))
        {
            Debug.LogError("[CraftingManager] Workflow JSON not found: " + workflowPath);
            onDone(null); yield break;
        }

        JObject workflow;
        try { workflow = JObject.Parse(workflowJson); }
        catch (Exception ex)
        {
            Debug.LogError("[CraftingManager] Workflow parse error: " + ex.Message);
            onDone(null); yield break;
        }

        var clipNode = workflow[clipNodeId];
        if (clipNode == null)
        {
            clipNode = FindNodeByClass(workflow, "CLIPTextEncode");
            if (clipNode == null)
            {
                Debug.LogError($"[CraftingManager] CLIPTextEncode node not found.");
                onDone(null); yield break;
            }
            Debug.LogWarning($"[CraftingManager] clipNodeId \"{clipNodeId}\" not found, resolved CLIPTextEncode by class_type.");
        }
        clipNode["inputs"]["text"] = prompt;

        var ksNode = workflow[kSamplerNodeId];
        if (ksNode == null)
        {
            ksNode = FindNodeByClass(workflow, "KSampler");
            if (ksNode == null)
            {
                Debug.LogError($"[CraftingManager] KSampler node not found.");
                onDone(null); yield break;
            }
            Debug.LogWarning($"[CraftingManager] kSamplerNodeId \"{kSamplerNodeId}\" not found, resolved KSampler by class_type.");
        }
        ksNode["inputs"]["seed"] = (long)UnityEngine.Random.Range(0, int.MaxValue);

        string bodyStr   = new JObject { ["prompt"] = workflow }.ToString();
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyStr);
        string promptId  = null;

        using (var req = new UnityWebRequest(comfyUIUrl + "/prompt", "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[CraftingManager] ComfyUI unreachable: " + req.error);
                onDone(null); yield break;
            }
            try { promptId = JObject.Parse(req.downloadHandler.text)["prompt_id"]?.ToString(); } catch { }
        }

        if (string.IsNullOrEmpty(promptId)) { onDone(null); yield break; }

        const float pollInterval = 1.0f;
        const float timeout      = 60f;
        float elapsed = 0f;
        string filename = null, subfolder = "", type = "output";

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
            using (var hist = UnityWebRequest.Get($"{comfyUIUrl}/history/{promptId}"))
            {
                yield return hist.SendWebRequest();
                if (hist.result != UnityWebRequest.Result.Success) continue;
                JObject history;
                try { history = JObject.Parse(hist.downloadHandler.text); } catch { continue; }
                var entry = history[promptId];
                if (entry == null) continue;
                foreach (var node in entry["outputs"].Children<JProperty>())
                {
                    var imgs = node.Value["images"];
                    if (imgs == null || !imgs.HasValues) continue;
                    filename  = imgs[0]["filename"]?.ToString();
                    subfolder = imgs[0]["subfolder"]?.ToString() ?? "";
                    type      = imgs[0]["type"]?.ToString()      ?? "output";
                    break;
                }
                if (!string.IsNullOrEmpty(filename)) break;
            }
        }

        if (string.IsNullOrEmpty(filename)) { Debug.LogError("[CraftingManager] Image timed out."); onDone(null); yield break; }

        string url = $"{comfyUIUrl}/view?filename={UnityWebRequest.EscapeURL(filename)}" +
                     $"&subfolder={UnityWebRequest.EscapeURL(subfolder)}&type={UnityWebRequest.EscapeURL(type)}";

        using (var texReq = UnityWebRequestTexture.GetTexture(url))
        {
            yield return texReq.SendWebRequest();
            onDone(texReq.result == UnityWebRequest.Result.Success
                ? DownloadHandlerTexture.GetContent(texReq) : null);
        }
    }

    // ── Shared helpers ────────────────────────────────────────────

    private static JToken FindNodeByClass(JObject workflow, string classType)
    {
        foreach (var prop in workflow.Properties())
            if (prop.Value is JObject node && (string)node["class_type"] == classType)
                return node;
        return null;
    }

    private IEnumerator LoadApiKey(Action<string> onDone)
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
        string key = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        using (var r = UnityWebRequest.Get(configPath))
        {
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
                try { key = JObject.Parse(r.downloadHandler.text)["openAIApiKey"]?.ToString(); } catch { }
        }
#else
        if (File.Exists(configPath))
            try { key = JObject.Parse(File.ReadAllText(configPath))["openAIApiKey"]?.ToString(); } catch { }
        yield return null;
#endif
        if (key == "YOUR_KEY_HERE") key = null;
        onDone(key);
    }

    private IEnumerator PostOpenAI(string apiKey, JObject body, Action<string> onContent)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body.ToString());
        using (var req = new UnityWebRequest(openAIUrl, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[CraftingManager] " + req.error + "\n" + req.downloadHandler.text);
                onContent(null); yield break;
            }

            string content = null;
            try { content = JObject.Parse(req.downloadHandler.text)["choices"]?[0]?["message"]?["content"]?.ToString(); }
            catch (Exception ex) { Debug.LogError("[CraftingManager] Response parse: " + ex.Message); }

            onContent(string.IsNullOrEmpty(content) ? null : content);
        }
    }

    private static string StripCodeFences(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```")) return s;
        int first = s.IndexOf('\n');
        int last  = s.LastIndexOf("```");
        return (first >= 0 && last > first) ? s.Substring(first, last - first).Trim() : s;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[CraftingManager] " + msg);
    }
}
