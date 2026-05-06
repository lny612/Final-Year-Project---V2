using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// TODO-EDITOR: Wire 5.MinigameTest.unity (production minigame scene)
//   1. The "Test Runner" GameObject hosts this script.
//   2. Wire `tracingMinigame` to the TracingMinigameUI on Canvas/Minigame Panel.
//   3. (Optional) Wire `statusText` to a TMP_Text on Canvas — surfaces
//      "Forging the wand..." while the OpenAI/ComfyUI calls run in parallel.
//   4. Ensure scene has a GameManager GO (singleton self-destroys duplicates,
//      so it's safe to add for solo-play testing). When entering from
//      CraftingScene, the existing DontDestroyOnLoad GameManager carries
//      the chosen materials forward.
//   5. Add 5.MinigameTest.unity to Build Settings between CraftingScene
//      and EvaluationScene.

/// <summary>
/// Standalone minigame scene controller. Runs the tracing ritual and
/// the wand-generation pipeline (OpenAI text + ComfyUI image) in
/// parallel, then loads EvaluationScene once both finish.
/// Materials are read from <see cref="GameManager.chosenCore1"/>,
/// <see cref="GameManager.chosenCore2"/>, <see cref="GameManager.chosenWood"/>
/// — populated by CraftingManager before this scene loads.
/// </summary>
[DisallowMultipleComponent]
public class MinigameSceneRunner : MonoBehaviour
{
    [Header("Minigame")]
    [Tooltip("TracingMinigameUI on the MinigamePanel that drives the 3-round ritual.")]
    public TracingMinigameUI tracingMinigame;

    [Header("Status (optional)")]
    [Tooltip("Optional TMP label that mirrors API-call status messages.")]
    public TMP_Text statusText;

    [Header("API Settings")]
    public string openAIUrl  = "https://api.openai.com/v1/chat/completions";
    public string comfyUIUrl = "http://127.0.0.1:8000";
    public string clipNodeId     = "5";
    public string kSamplerNodeId = "4";

    private bool _minigameDone;
    private bool _pipelineDone;
    private bool _navigated;

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[MinigameSceneRunner] No GameManager.Instance — entering MinigameScene out of flow?");
        }

        // Wand pipeline kicks off immediately so it overlaps the minigame.
        StartCoroutine(WandPipeline());

        if (tracingMinigame != null)
        {
            tracingMinigame.Begin(grade =>
            {
                if (gm != null)
                {
                    gm.craftingQualityGrade   = grade;
                    gm.lastMinigameRoundsWon  = GradeToRoundsWon(grade);
                }
                _minigameDone = true;
                TryAdvance();
            });
        }
        else
        {
            Debug.LogWarning("[MinigameSceneRunner] tracingMinigame not assigned — skipping minigame, defaulting to grade A.");
            if (gm != null)
            {
                gm.craftingQualityGrade  = 'A';
                gm.lastMinigameRoundsWon = 3;
            }
            _minigameDone = true;
        }
    }

    private static int GradeToRoundsWon(char grade) => grade switch
    {
        'A' => 3,
        'B' => 2,
        'C' => 1,
        _   => 0,
    };

    private void TryAdvance()
    {
        if (_navigated) return;
        if (!_minigameDone || !_pipelineDone) return;

        _navigated = true;
        SetStatus("Bringing the wand to the customer...");
        GameManager.Instance?.LoadScene(GameManager.SCENE_EVALUATION);
    }

    // ── Wand generation pipeline (OpenAI text + ComfyUI image) ────

    private IEnumerator WandPipeline()
    {
        var gm = GameManager.Instance;
        var slot1 = gm?.chosenCore1;
        var slot2 = gm?.chosenCore2;
        var slot3 = gm?.chosenWood;

        if (slot1 == null || slot3 == null)
        {
            Debug.LogError("[MinigameSceneRunner] Missing chosen materials — cannot generate wand.");
            _pipelineDone = true;
            TryAdvance();
            yield break;
        }

        SetStatus("Consulting the wandmaker's tome...");

        string apiKey = null;
        yield return StartCoroutine(LoadApiKey(k => apiKey = k));
        if (string.IsNullOrEmpty(apiKey))
        {
            SetStatus("Error: no API key found in StreamingAssets/config.json");
            _pipelineDone = true;
            TryAdvance();
            yield break;
        }

        string userMsg = BuildWandUserMessage(slot1, slot2, slot3);
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
            SetStatus("Error: wand generation API call failed.");
            _pipelineDone = true;
            TryAdvance();
            yield break;
        }

        content = StripCodeFences(content);
        WandResult wand;
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
            Debug.LogError("[MinigameSceneRunner] Wand parse: " + ex.Message);
            SetStatus("Error: unexpected wand response format.");
            _pipelineDone = true;
            TryAdvance();
            yield break;
        }

        if (gm != null) gm.currentWandResult = wand;

        SetStatus("Forging the wand...");
        Texture2D wandTex = null;
        yield return StartCoroutine(RunImageGeneration(wand.imagePrompt, t => wandTex = t));
        if (wandTex != null)
        {
            wand.wandImage = wandTex;
        }
        else
        {
            Debug.LogError("[MinigameSceneRunner] Wand image generation failed; continuing without image.");
        }

        _pipelineDone = true;
        TryAdvance();
    }

    // ── Wand prompts (parallel to CraftingManager's, kept here so this
    //     scene is self-contained) ───────────────────────────────────

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

Return ONLY valid JSON. No markdown. No explanation.
Use exactly this structure:
{
  ""wandName"": string,
  ""description"": string,
  ""attributes"": [string, string, string],
  ""imagePrompt"": string
}";

    private static string BuildWandUserMessage(MaterialData slot1, MaterialData slot2, MaterialData slot3)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("A wand is being crafted from these materials:");
        sb.AppendLine();
        sb.AppendLine("CORE 1:");
        AppendCore(sb, slot1);
        if (slot2 != null)
        {
            sb.AppendLine();
            sb.AppendLine("CORE 2:");
            AppendCore(sb, slot2);
        }
        sb.AppendLine();
        sb.AppendLine("WOOD:");
        sb.AppendLine($"Name: {slot3.name}");
        sb.AppendLine($"Personality Match: {slot3.personalityMatch}");
        sb.AppendLine($"Attributes: {slot3.attributes}");
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

    // ── ComfyUI image generation ──────────────────────────────────

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
            Debug.LogError("[MinigameSceneRunner] Workflow JSON not found: " + workflowPath);
            onDone(null); yield break;
        }

        JObject workflow;
        try { workflow = JObject.Parse(workflowJson); }
        catch (Exception ex)
        {
            Debug.LogError("[MinigameSceneRunner] Workflow parse error: " + ex.Message);
            onDone(null); yield break;
        }

        var clipNode = workflow[clipNodeId];
        if (clipNode == null)
        {
            clipNode = FindNodeByClass(workflow, "CLIPTextEncode");
            if (clipNode == null) { Debug.LogError("[MinigameSceneRunner] CLIPTextEncode node not found."); onDone(null); yield break; }
            Debug.LogWarning($"[MinigameSceneRunner] clipNodeId \"{clipNodeId}\" not found, resolved by class_type.");
        }
        clipNode["inputs"]["text"] = prompt;

        var ksNode = workflow[kSamplerNodeId];
        if (ksNode == null)
        {
            ksNode = FindNodeByClass(workflow, "KSampler");
            if (ksNode == null) { Debug.LogError("[MinigameSceneRunner] KSampler node not found."); onDone(null); yield break; }
            Debug.LogWarning($"[MinigameSceneRunner] kSamplerNodeId \"{kSamplerNodeId}\" not found, resolved by class_type.");
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
                Debug.LogError("[MinigameSceneRunner] ComfyUI unreachable: " + req.error);
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

        if (string.IsNullOrEmpty(filename)) { Debug.LogError("[MinigameSceneRunner] Image timed out."); onDone(null); yield break; }

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
                Debug.LogError("[MinigameSceneRunner] " + req.error + "\n" + req.downloadHandler.text);
                onContent(null); yield break;
            }

            string content = null;
            try { content = JObject.Parse(req.downloadHandler.text)["choices"]?[0]?["message"]?["content"]?.ToString(); }
            catch (Exception ex) { Debug.LogError("[MinigameSceneRunner] Response parse: " + ex.Message); }

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
        Debug.Log("[MinigameSceneRunner] " + msg);
    }
}
