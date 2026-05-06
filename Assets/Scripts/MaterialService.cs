using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Shared helper that runs the OpenAI-text + ComfyUI-image pipeline used to
/// build a round of materials. Lives in its own static class so it can be
/// invoked from <see cref="MorningScreenController"/> or
/// <see cref="CustomerGenerator"/> for pre-generation, AND from
/// <see cref="MaterialGenerator"/>'s live path — same prompts, same parsing,
/// no drift.
///
/// Coroutines are hosted by the caller via <c>StartCoroutine</c>. For pre-gen
/// during scene transitions, hand them to <c>GameManager.Instance</c> (which
/// is DontDestroyOnLoad) so they survive scene loads.
/// </summary>
public static class MaterialService
{
    public const string DefaultOpenAIUrl       = "https://api.openai.com/v1/chat/completions";
    public const string DefaultComfyUIUrl      = "http://127.0.0.1:8000";
    public const string DefaultClipNodeId      = "5";
    public const string DefaultKSamplerNodeId  = "4";
    public const string WorkflowFileName       = "image_z_image_turbo.json";

    // ── System prompt ────────────────────────────────────────────────

    public const string SystemPrompt =
@"You are a material designer for a fantasy wand-crafting game.
Given a customer's dossier, you generate 6 wand materials -
3 monster part cores and 3 woods - that the player must choose from.

Your materials must follow these strict design rules:

DESIGN RULE 1 - LOGICAL PROPERTIES
Every material's properties must be grounded in real-world or
folkloric logic. Draw from mythology, herbalism, and natural history.
A phoenix feather implies speed. Willow implies flexibility and healing.
Dragon scales imply armour and fire. Never invent arbitrary properties.

DESIGN RULE 2 - PRICE LOGIC FOR CORES
Price reflects rarity and yield per creature.
- A heart or eye costs more than hair or scales (one per creature vs. many)
- A dragon part costs more than a troll part (rarity and danger)
- All prices must stay within 0-300 gold range
- Common creatures with abundant yield: 50-120g
- Uncommon creatures or low-yield parts: 120-200g
- Rare creatures or unique parts: 200-300g

DESIGN RULE 3 - PRICE LOGIC FOR WOODS
Price reflects how rare or magical the tree species is.
- Common woodland trees (ash, birch, willow): 50-100g
- Less common but real trees (elder, yew, rowan): 80-150g
- Rare or ancient trees: 150-300g

DESIGN RULE 4 - INTENTIONAL MISDIRECTION
The 6 materials must NOT all perfectly match the customer.
Design them so that:
- For CORES: one material is the optimal pair candidate,
  one is a tempting trap (looks right but conflicts with the
  customer's constraint), one has a useful attribute but
  the wrong scale or application for the customer's true goal.
- For WOODS: one is the correct match, one suits the customer's
  ROLE but not their MAGIC PROBLEM, one sounds partially relevant
  but has a physical property (e.g. rigidity) that conflicts
  with what the customer's magic needs.
The player should need to read all dossier fields carefully
to identify the best combination. Never make it obvious.

DESIGN RULE 5 - IMAGE PROMPTS
For each material, generate an imagePrompt field.
This will be sent to a pixel art image generator.
The prompt must describe ONLY the material object itself -
no background, no characters, no hands holding it.
Always end every imagePrompt with:
""pixel art, 128x128, transparent background, centered, highly detailed, item icon style""

Return ONLY valid JSON. No markdown. No explanation.
Use exactly this structure:
{
  ""cores"": [
    {
      ""materialType"": ""core"",
      ""name"": string,
      ""price"": number,
      ""elementalAffinity"": string,
      ""attributes"": string,
      ""special"": string,
      ""imagePrompt"": string
    }
  ],
  ""woods"": [
    {
      ""materialType"": ""wood"",
      ""name"": string,
      ""price"": number,
      ""personalityMatch"": string,
      ""attributes"": string,
      ""imagePrompt"": string
    }
  ]
}";

    public static string BuildUserMessage(CustomerOrder c) =>
$@"Here is today's customer dossier:

Name: {c.customerName}
School of Magic: {c.schoolOfMagic}
Profession: {c.profession}
Personality: {c.personality}
Request: ""{c.request}""
True Goal: {c.trueGoal}
Constraint: {c.constraint}

Now generate 3 cores and 3 woods for the customer above.
Apply the four design rules. Return ONLY the JSON object.";

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Run the material-text generation. On success invokes
    /// <paramref name="onComplete"/> with the parsed list and a null error;
    /// on failure invokes with null + an error string.
    /// </summary>
    public static IEnumerator GenerateMaterialsAsync(
        string openAIUrl,
        CustomerOrder customer,
        Action<List<MaterialData>, string> onComplete)
    {
        if (customer == null)
        {
            onComplete?.Invoke(null, "no customer to base materials on");
            yield break;
        }

        string apiKey = null;
        yield return LoadApiKey(k => apiKey = k);
        if (string.IsNullOrEmpty(apiKey))
        {
            onComplete?.Invoke(null, "no API key found in StreamingAssets/config.json");
            yield break;
        }

        var body = new JObject
        {
            ["model"]    = "gpt-4o",
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = SystemPrompt                  },
                new JObject { ["role"] = "user",   ["content"] = BuildUserMessage(customer)   }
            }
        };

        string content = null;
        string err = null;
        yield return PostOpenAI(string.IsNullOrEmpty(openAIUrl) ? DefaultOpenAIUrl : openAIUrl, apiKey, body,
            (c, e) => { content = c; err = e; });
        if (content == null)
        {
            onComplete?.Invoke(null, err ?? "OpenAI request failed");
            yield break;
        }

        content = StripCodeFences(content);

        List<MaterialData> result = null;
        try
        {
            var root  = JObject.Parse(content);
            result    = new List<MaterialData>();
            var cores = root["cores"] as JArray;
            var woods = root["woods"] as JArray;

            if (cores != null) foreach (JObject o in cores) result.Add(ParseCore(o));
            if (woods != null) foreach (JObject o in woods) result.Add(ParseWood(o));
        }
        catch (Exception ex)
        {
            Debug.LogError("[MaterialService] Material parse: " + ex.Message + "\nRaw: " + content);
            onComplete?.Invoke(null, "unexpected response format from OpenAI");
            yield break;
        }

        onComplete?.Invoke(result, null);
    }

    /// <summary>
    /// Generate a single image via ComfyUI (POST /prompt → poll /history → GET /view).
    /// Invokes the callback with the texture on success or null + error string on failure.
    /// </summary>
    public static IEnumerator GenerateImageAsync(
        string comfyUIUrl,
        string clipNodeId,
        string ksamplerNodeId,
        string prompt,
        Action<Texture2D, string> onComplete)
    {
        if (string.IsNullOrEmpty(comfyUIUrl)) comfyUIUrl = DefaultComfyUIUrl;
        if (string.IsNullOrEmpty(clipNodeId)) clipNodeId = DefaultClipNodeId;
        if (string.IsNullOrEmpty(ksamplerNodeId)) ksamplerNodeId = DefaultKSamplerNodeId;

        string workflowPath = Path.Combine(Application.streamingAssetsPath, WorkflowFileName);
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
        yield return null;
#endif

        if (string.IsNullOrEmpty(workflowJson))
        {
            Debug.LogError("[MaterialService] Workflow JSON not found: " + workflowPath);
            onComplete?.Invoke(null, "workflow JSON not found");
            yield break;
        }

        JObject workflow;
        try { workflow = JObject.Parse(workflowJson); }
        catch (Exception ex)
        {
            Debug.LogError("[MaterialService] Workflow parse error: " + ex.Message);
            onComplete?.Invoke(null, "workflow parse error");
            yield break;
        }

        var clipNode = workflow[clipNodeId] ?? FindNodeByClass(workflow, "CLIPTextEncode");
        if (clipNode == null) { onComplete?.Invoke(null, "CLIPTextEncode node not found"); yield break; }
        clipNode["inputs"]["text"] = prompt;

        var ksNode = workflow[ksamplerNodeId] ?? FindNodeByClass(workflow, "KSampler");
        if (ksNode == null) { onComplete?.Invoke(null, "KSampler node not found"); yield break; }
        ksNode["inputs"]["seed"] = (long)UnityEngine.Random.Range(0, int.MaxValue);

        string bodyStr = new JObject { ["prompt"] = workflow }.ToString();
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyStr);

        string promptId = null;
        using (var req = new UnityWebRequest(comfyUIUrl + "/prompt", "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[MaterialService] ComfyUI unreachable: " + req.error);
                onComplete?.Invoke(null, "ComfyUI unreachable at " + comfyUIUrl);
                yield break;
            }

            try { promptId = JObject.Parse(req.downloadHandler.text)["prompt_id"]?.ToString(); }
            catch { }
        }

        if (string.IsNullOrEmpty(promptId))
        {
            onComplete?.Invoke(null, "no prompt_id from ComfyUI");
            yield break;
        }

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
                var outputs = entry["outputs"];
                if (outputs == null) continue;

                foreach (var node in outputs.Children<JProperty>())
                {
                    var images = node.Value["images"];
                    if (images == null || !images.HasValues) continue;
                    var first = images[0];
                    filename  = first["filename"]?.ToString();
                    subfolder = first["subfolder"]?.ToString() ?? "";
                    type      = first["type"]?.ToString()      ?? "output";
                    break;
                }

                if (!string.IsNullOrEmpty(filename)) break;
            }
        }

        if (string.IsNullOrEmpty(filename))
        {
            onComplete?.Invoke(null, "image generation timed out");
            yield break;
        }

        string url = $"{comfyUIUrl}/view" +
                     $"?filename={UnityWebRequest.EscapeURL(filename)}" +
                     $"&subfolder={UnityWebRequest.EscapeURL(subfolder)}" +
                     $"&type={UnityWebRequest.EscapeURL(type)}";

        using (var texReq = UnityWebRequestTexture.GetTexture(url))
        {
            yield return texReq.SendWebRequest();
            if (texReq.result != UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(null, "image download failed: " + texReq.error);
                yield break;
            }
            onComplete?.Invoke(DownloadHandlerTexture.GetContent(texReq), null);
        }
    }

    /// <summary>
    /// Convenience: kick off the full pipeline (text → 6 parallel images) on
    /// the given <paramref name="host"/>. Stores results on
    /// <see cref="GameManager.pendingMaterials"/> as soon as text is ready;
    /// each image populates <see cref="MaterialData.generatedImage"/> as it
    /// arrives. Sets <c>pendingMaterialsInProgress</c> = false when all 6
    /// image jobs complete (success or timeout).
    /// </summary>
    public static IEnumerator PreGenAsync(MonoBehaviour host, GameManager gm, CustomerOrder customer,
        string openAIUrl, string comfyUIUrl, string clipNodeId, string ksamplerNodeId)
    {
        if (gm == null || customer == null) yield break;

        gm.pendingMaterialsInProgress = true;

        List<MaterialData> materials = null;
        string textErr = null;
        yield return GenerateMaterialsAsync(openAIUrl, customer, (m, e) => { materials = m; textErr = e; });

        if (materials == null)
        {
            Debug.LogWarning("[MaterialService] Pre-gen materials failed: " + textErr);
            gm.pendingMaterials = null;
            gm.pendingMaterialsInProgress = false;
            yield break;
        }

        // Make text available immediately even though images are still loading.
        gm.pendingMaterials = materials;

        int remaining = materials.Count;
        if (remaining == 0)
        {
            gm.pendingMaterialsInProgress = false;
            yield break;
        }

        foreach (var mat in materials)
        {
            var captured = mat;
            host.StartCoroutine(GenerateImageAsync(comfyUIUrl, clipNodeId, ksamplerNodeId, mat.imagePrompt,
                (tex, err) =>
                {
                    if (tex != null) captured.generatedImage = tex;
                    else if (!string.IsNullOrEmpty(err))
                        Debug.LogWarning($"[MaterialService] Image pre-gen for '{captured.name}' failed: {err}");

                    remaining--;
                    if (remaining <= 0 && GameManager.Instance != null)
                        GameManager.Instance.pendingMaterialsInProgress = false;
                }));
        }
    }

    // ── Internal helpers ────────────────────────────────────────────

    private static IEnumerator LoadApiKey(Action<string> onDone)
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

    private static IEnumerator PostOpenAI(string url, string apiKey, JObject body, Action<string, string> onDone)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body.ToString());
        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[MaterialService] " + req.error + "\n" + req.downloadHandler.text);
                onDone(null, "could not reach OpenAI");
                yield break;
            }

            string content = null;
            try { content = JObject.Parse(req.downloadHandler.text)["choices"]?[0]?["message"]?["content"]?.ToString(); }
            catch (Exception ex) { Debug.LogError("[MaterialService] Response parse: " + ex.Message); }

            if (string.IsNullOrEmpty(content))
            {
                onDone(null, "unexpected response format from OpenAI");
                yield break;
            }

            onDone(content, null);
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

    private static MaterialData ParseCore(JObject o) => new MaterialData
    {
        materialType      = "core",
        name              = o["name"]?.ToString()              ?? "",
        price             = o["price"]?.Value<int>()           ?? 0,
        elementalAffinity = o["elementalAffinity"]?.ToString() ?? "",
        attributes        = o["attributes"]?.ToString()        ?? "",
        special           = o["special"]?.ToString()           ?? "",
        imagePrompt       = o["imagePrompt"]?.ToString()       ?? ""
    };

    private static MaterialData ParseWood(JObject o) => new MaterialData
    {
        materialType     = "wood",
        name             = o["name"]?.ToString()             ?? "",
        price            = o["price"]?.Value<int>()          ?? 0,
        personalityMatch = o["personalityMatch"]?.ToString() ?? "",
        attributes       = o["attributes"]?.ToString()       ?? "",
        imagePrompt      = o["imagePrompt"]?.ToString()      ?? ""
    };

    private static JToken FindNodeByClass(JObject workflow, string classType)
    {
        foreach (var prop in workflow.Properties())
            if (prop.Value is JObject node && (string)node["class_type"] == classType)
                return node;
        return null;
    }
}
