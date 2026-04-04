using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MaterialGenerator : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────

    [Header("API Settings")]
    public string openAIUrl      = "https://api.openai.com/v1/chat/completions";
    public string comfyUIUrl     = "http://127.0.0.1:8000";

    [Tooltip("CLIPTextEncode node ID in image_z_image_turbo.json.")]
    public string clipNodeId     = "5";

    [Tooltip("KSampler node ID in image_z_image_turbo.json.")]
    public string kSamplerNodeId = "4";

    [Header("UI — Controls")]
    public Button   generateButton;
    public Button   proceedButton;     // enabled when ≥1 core AND ≥1 wood purchased
    public Button   backButton;        // returns to CustomerGeneratorTest
    public TMP_Text goldText;          // shows "Gold: XXXg"
    public TMP_Text statusText;

    [Header("UI — Customer Dossier")]
    public TMP_Text nameText;
    public TMP_Text schoolText;
    public TMP_Text professionText;
    public TMP_Text personalityText;
    public TMP_Text requestText;
    public TMP_Text trueGoalText;
    public TMP_Text constraintText;

    [Header("UI — Material Cards")]
    [Tooltip("Parent transform for the 3 core cards.")]
    public Transform  coreCardContainer;

    [Tooltip("Parent transform for the 3 wood cards.")]
    public Transform  woodCardContainer;

    [Tooltip("Prefab instantiated once per material.")]
    public GameObject materialCardPrefab;

    // ── Private state ──────────────────────────────────────────────

    private bool _busy;
    private readonly List<MaterialCardUI> _coreCards = new();
    private readonly List<MaterialCardUI> _woodCards = new();
    private Color _goldDefaultColor = Color.white;
    private Coroutine _goldFlashCoroutine;

    // ── Customer generation prompts ────────────────────────────────
    // (Identical to CustomerGenerator.cs — kept here so this script is self-contained.)

    private const string CustomerSystemPrompt =
@"You are a character designer for a fantasy wand-crafting game.
Your job is to generate customer orders for a wand shop.

Each customer is a unique fantasy character who needs a custom wand.
Every field you write must follow a strict internal logic chain:

  schoolOfMagic -> profession -> request -> trueGoal -> constraint

Rules for each field:

customerName
  A fantasy name that feels fitting for this character.

schoolOfMagic
  The element or discipline this person uses.
  Examples: Storm magic, Hydromancy, Shadow magic, Pyromancy, Necromancy.

profession
  Their specific job. It must logically match their school of magic.
  Do not write a generic job title. Write HOW they use their magic in their work.

personality
  Exactly 3 traits. At least one must be in tension with the others.
  Format: ""Trait, trait, trait""

request
  What they say out loud in the shop. Written in first person, conversational tone.
  It must describe a specific practical problem they want the wand to solve.
  The problem must be a logical drawback of their school of magic used in their profession.

trueGoal
  What they actually want to achieve - more specific and ambitious than the request.
  Must include the profession's core demand (speed, precision, range, etc.)
  and show why those demands are in slight tension with each other.

constraint
  Their magical limitation. It must directly explain WHY the problem in the request happens.
  The cruelest constraints are ones that activate at the worst possible moment given their job.

Return ONLY valid JSON. No markdown. No explanation. No extra text.
Use exactly this structure:
{
  ""customerName"": string,
  ""schoolOfMagic"": string,
  ""profession"": string,
  ""personality"": string,
  ""request"": string,
  ""trueGoal"": string,
  ""constraint"": string
}";

    private const string CustomerUserPrompt =
@"Generate a new customer. Here are three examples of the correct style.
The new character must use a different school of magic from all three examples.

EXAMPLE 1:
{
  ""customerName"": ""Elyra Voss"",
  ""schoolOfMagic"": ""Storm magic"",
  ""profession"": ""Assassin mage who uses lightning for the final blow"",
  ""personality"": ""Meticulous, deeply self-doubting, quietly competitive"",
  ""request"": ""I need something discreet for my job. I want the wand to prevent flashing so I can conjure my magic without visible sign."",
  ""trueGoal"": ""To perform a high-stakes lightning spell quickly at the exact right moment without it being too visible"",
  ""constraint"": ""Her magic surges unpredictably when she feels watched""
}

EXAMPLE 2:
{
  ""customerName"": ""Dorian Ashveil"",
  ""schoolOfMagic"": ""Shadow magic"",
  ""profession"": ""Bounty hunter who uses shadow magic to track and corner targets"",
  ""personality"": ""Calculating, emotionally detached, secretly paranoid"",
  ""request"": ""I need a wand that keeps my shadow spells from dispersing mid-chase. My bindings keep collapsing the moment my target starts running."",
  ""trueGoal"": ""To cast shadow binding spells reliably at full sprint without losing hold of the spell"",
  ""constraint"": ""His magic destabilises when his concentration splits between moving and casting simultaneously""
}

EXAMPLE 3:
{
  ""customerName"": ""Sable Mirehn"",
  ""schoolOfMagic"": ""Hydromancy"",
  ""profession"": ""Field medic who uses water magic to perform emergency healing"",
  ""personality"": ""Outwardly calm, internally panicked, fiercely protective"",
  ""request"": ""I need a wand that lets me control the exact pressure of my water flow. Too much force and I damage the wound instead of closing it."",
  ""trueGoal"": ""To perform precise, high-speed healing on multiple patients in rapid succession without losing control of the flow"",
  ""constraint"": ""Her water magic amplifies in intensity when she is emotionally distressed - exactly when she needs it most delicate""
}

Now generate one new customer following the same logical chain.
Return ONLY the JSON object.";

    // ── Material generation system prompt ──────────────────────────

    private const string MaterialSystemPrompt =
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

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Start()
    {
        SetStatus("Waiting...");
        ClearDossier();

        if (goldText != null)
        {
            _goldDefaultColor = goldText.color;
            UpdateGoldDisplay();
        }
        if (proceedButton != null)
        {
            proceedButton.interactable = false;
            proceedButton.onClick.AddListener(
                () => GameManager.Instance?.LoadScene(GameManager.SCENE_CRAFTING));
        }
        if (backButton != null)
            backButton.onClick.AddListener(
                () => GameManager.Instance?.LoadScene(GameManager.SCENE_CUSTOMER));

        // Auto-start if arriving from CustomerGenerator with a customer already set
        if (GameManager.Instance?.currentCustomer != null
            && GameManager.Instance.availableMaterials.Count == 0)
        {
            DisplayCustomer(GameManager.Instance.currentCustomer);
            StartCoroutine(MaterialsPipeline(GameManager.Instance.currentCustomer, ownBusy: true));
        }
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>Called by the Generate button. Runs full pipeline.</summary>
    public void StartFullGeneration()
    {
        if (_busy) return;
        StartCoroutine(FullPipeline());
    }

    /// <summary>
    /// Generates materials for an already-known customer.
    /// Can be called externally after a customer is generated elsewhere.
    /// </summary>
    public void GenerateMaterials(CustomerOrder customer)
    {
        if (_busy) return;
        StartCoroutine(MaterialsPipeline(customer, ownBusy: true));
    }

    // ── Full pipeline ──────────────────────────────────────────────

    private IEnumerator FullPipeline()
    {
        _busy = true;
        SetButtonInteractable(false);
        ClearCards();

        // Step 1 — Generate customer
        SetStatus("Generating customer...");
        CustomerOrder customer = null;
        yield return StartCoroutine(RunCustomerGeneration(r => customer = r));
        if (customer == null) { Finish(); yield break; }

        // Step 2 — Display dossier and save to GameManager
        DisplayCustomer(customer);
        if (GameManager.Instance != null)
            GameManager.Instance.currentCustomer = customer;

        // Steps 3–6 — Materials + images
        yield return StartCoroutine(MaterialsPipeline(customer, ownBusy: false));
    }

    // ── Materials pipeline (called after customer is known) ────────

    private IEnumerator MaterialsPipeline(CustomerOrder customer, bool ownBusy)
    {
        if (ownBusy)
        {
            _busy = true;
            SetButtonInteractable(false);
            ClearCards();
        }

        // Step 3 — Generate material descriptions
        SetStatus("Generating materials...");
        List<MaterialData> materials = null;
        yield return StartCoroutine(RunMaterialGeneration(customer, r => materials = r));
        if (materials == null) { Finish(); yield break; }

        // Step 4 — Push to GameManager and instantiate cards
        if (GameManager.Instance != null)
            GameManager.Instance.availableMaterials = materials;

        var cores = materials.FindAll(m => m.materialType == "core");
        var woods = materials.FindAll(m => m.materialType == "wood");
        InstantiateCards(cores, coreCardContainer, _coreCards);
        InstantiateCards(woods, woodCardContainer, _woodCards);

        // Step 5 — Queue all image generation requests in parallel
        var queue = new List<(MaterialCardUI ui, MaterialData data)>();
        for (int i = 0; i < _coreCards.Count && i < cores.Count; i++) queue.Add((_coreCards[i], cores[i]));
        for (int i = 0; i < _woodCards.Count && i < woods.Count; i++) queue.Add((_woodCards[i], woods[i]));

        int completed = 0;
        int total = queue.Count;
        SetStatus($"Generating {total} images...");

        for (int i = 0; i < total; i++)
        {
            var (cardUI, mat) = queue[i];
            StartCoroutine(RunImageGeneration(mat.imagePrompt, tex =>
            {
                completed++;
                if (tex != null)
                {
                    mat.generatedImage = tex;
                    cardUI.SetImage(tex);
                }
                SetStatus($"Generated image {completed} of {total}...");
            }));
        }

        // Wait for all images to finish (or timeout individually)
        while (completed < total)
            yield return null;

        SetStatus("Done.");
        Finish();
    }

    // ── Customer generation ────────────────────────────────────────

    private IEnumerator RunCustomerGeneration(Action<CustomerOrder> onDone)
    {
        string apiKey = null;
        yield return StartCoroutine(LoadApiKey(k => apiKey = k));
        if (string.IsNullOrEmpty(apiKey))
        {
            ReportError("Error: no API key found in StreamingAssets/config.json");
            onDone(null); yield break;
        }

        var body = new JObject
        {
            ["model"]    = "gpt-4o",
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = CustomerSystemPrompt },
                new JObject { ["role"] = "user",   ["content"] = CustomerUserPrompt   }
            }
        };

        string content = null;
        yield return StartCoroutine(PostOpenAI(apiKey, body, r => content = r));
        if (content == null) { onDone(null); yield break; }

        content = StripCodeFences(content);

        CustomerOrder customer = null;
        try
        {
            var j = JObject.Parse(content);
            customer = new CustomerOrder
            {
                customerName  = j["customerName"]?.ToString()  ?? "",
                schoolOfMagic = j["schoolOfMagic"]?.ToString() ?? "",
                profession    = j["profession"]?.ToString()    ?? "",
                personality   = j["personality"]?.ToString()   ?? "",
                request       = j["request"]?.ToString()       ?? "",
                trueGoal      = j["trueGoal"]?.ToString()      ?? "",
                constraint    = j["constraint"]?.ToString()    ?? ""
            };
        }
        catch (Exception ex)
        {
            ReportError("Error: unexpected response format from OpenAI.");
            Debug.LogError("[MaterialGenerator] Customer parse: " + ex.Message);
        }

        onDone(customer);
    }

    // ── Material generation ────────────────────────────────────────

    private IEnumerator RunMaterialGeneration(CustomerOrder c, Action<List<MaterialData>> onDone)
    {
        string apiKey = null;
        yield return StartCoroutine(LoadApiKey(k => apiKey = k));
        if (string.IsNullOrEmpty(apiKey))
        {
            ReportError("Error: material generation failed. Check API key.");
            onDone(null); yield break;
        }

        string userMsg = BuildMaterialUserMessage(c);
        var body = new JObject
        {
            ["model"]    = "gpt-4o",
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = MaterialSystemPrompt },
                new JObject { ["role"] = "user",   ["content"] = userMsg             }
            }
        };

        string content = null;
        yield return StartCoroutine(PostOpenAI(apiKey, body, r => content = r));
        if (content == null) { onDone(null); yield break; }

        content = StripCodeFences(content);

        List<MaterialData> result = null;
        try
        {
            var root  = JObject.Parse(content);
            result    = new List<MaterialData>();
            var cores = root["cores"] as JArray;
            var woods = root["woods"] as JArray;

            if (cores != null)
                foreach (JObject o in cores)
                    result.Add(ParseCore(o));

            if (woods != null)
                foreach (JObject o in woods)
                    result.Add(ParseWood(o));
        }
        catch (Exception ex)
        {
            ReportError("Error: unexpected response from OpenAI.");
            Debug.LogError("[MaterialGenerator] Material parse: " + ex.Message);
            onDone(null); yield break;
        }

        onDone(result);
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

    private static string BuildMaterialUserMessage(CustomerOrder c) =>
$@"Here is today's customer dossier:

Name: {c.customerName}
School of Magic: {c.schoolOfMagic}
Profession: {c.profession}
Personality: {c.personality}
Request: ""{c.request}""
True Goal: {c.trueGoal}
Constraint: {c.constraint}

Here are two complete examples of the correct output style.
Study the misdirection design in each group before generating.

--- EXAMPLE OUTPUT for a Hydromancy / Field Medic customer ---

CORES EXAMPLE:
[
  {{
    ""materialType"": ""core"",
    ""name"": ""Selkie Heart"",
    ""price"": 180,
    ""elementalAffinity"": ""Water"",
    ""attributes"": ""Deepens emotional resonance with water magic. Spells feel more intuitive and flow naturally from intent."",
    ""special"": ""The caster's emotional state directly shapes the spell's behaviour - calm produces gentle flow, distress produces surge."",
    ""imagePrompt"": ""a glowing translucent heart from a selkie, deep sea blue, soft pulsing light, water droplets surrounding it, pixel art, 128x128, transparent background, centered, highly detailed, item icon style""
  }},
  {{
    ""materialType"": ""core"",
    ""name"": ""Leviathan Scale"",
    ""price"": 270,
    ""elementalAffinity"": ""Water / Deep pressure"",
    ""attributes"": ""Regulates and caps the maximum output force of water spells. Acts as a natural pressure limiter."",
    ""special"": ""Reduces peak spell intensity by up to 30%, making overflow far less likely."",
    ""imagePrompt"": ""a single enormous leviathan scale, deep ocean blue, slightly iridescent, water droplets beading on the surface, pixel art, 128x128, transparent background, centered, highly detailed, item icon style""
  }},
  {{
    ""materialType"": ""core"",
    ""name"": ""Nixie Finger Bone"",
    ""price"": 95,
    ""elementalAffinity"": ""Water / Precision"",
    ""attributes"": ""Enhances fine motor control of water spells. Dramatically improves targeting accuracy at close range."",
    ""special"": ""Spells become difficult to scale up in power - best suited for small, precise applications."",
    ""imagePrompt"": ""a small delicate finger bone from a river nixie, pale white with faint blue luminescence, etched with tiny runes, pixel art, 128x128, transparent background, centered, highly detailed, item icon style""
  }}
]

WOODS EXAMPLE:
[
  {{
    ""materialType"": ""wood"",
    ""name"": ""Weeping Willow"",
    ""price"": 60,
    ""personalityMatch"": ""Intuitive, emotionally guided, adaptive"",
    ""attributes"": ""Bends without breaking under magical pressure. Historically associated with water, grief, and healing."",
    ""imagePrompt"": ""a smooth wand rod of pale weeping willow wood, soft grey-green tint, fine drooping grain lines, faintly glowing, pixel art, 128x128, transparent background, centered, highly detailed, item icon style""
  }},
  {{
    ""materialType"": ""wood"",
    ""name"": ""Elder Wood"",
    ""price"": 110,
    ""personalityMatch"": ""Protective, willing to sacrifice, drawn to life-and-death work"",
    ""attributes"": ""Elder has ancient associations with both healing and warding off harm. Historically used in protective charms and medicines."",
    ""imagePrompt"": ""a wand rod of dark elder wood, deep brown with faint purple grain, small elderflower carvings along the shaft, pixel art, 128x128, transparent background, centered, highly detailed, item icon style""
  }},
  {{
    ""materialType"": ""wood"",
    ""name"": ""Ashwood"",
    ""price"": 75,
    ""personalityMatch"": ""Resilient, enduring, performs best under sustained pressure"",
    ""attributes"": ""One of the hardest and most durable woods. Historically associated with warriors and endurance."",
    ""imagePrompt"": ""a straight wand rod of pale ash wood, tight silvery grain, very smooth surface, slight metallic sheen, pixel art, 128x128, transparent background, centered, highly detailed, item icon style""
  }}
]

Now generate 3 cores and 3 woods for the customer above.
Apply the four design rules. Return ONLY the JSON object.";

    // ── ComfyUI image generation ───────────────────────────────────

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
            Debug.LogError("[MaterialGenerator] Workflow JSON not found: " + workflowPath);
            onDone(null); yield break;
        }

        JObject workflow;
        try { workflow = JObject.Parse(workflowJson); }
        catch (Exception ex)
        {
            Debug.LogError("[MaterialGenerator] Workflow parse error: " + ex.Message);
            onDone(null); yield break;
        }

        var clipNode = workflow[clipNodeId];
        if (clipNode == null)
        {
            clipNode = FindNodeByClass(workflow, "CLIPTextEncode");
            if (clipNode == null)
            {
                Debug.LogError($"[MaterialGenerator] CLIPTextEncode node not found in workflow.");
                onDone(null); yield break;
            }
            Debug.LogWarning($"[MaterialGenerator] clipNodeId \"{clipNodeId}\" not found, resolved CLIPTextEncode by class_type.");
        }
        clipNode["inputs"]["text"] = prompt;

        var ksNode = workflow[kSamplerNodeId];
        if (ksNode == null)
        {
            ksNode = FindNodeByClass(workflow, "KSampler");
            if (ksNode == null)
            {
                Debug.LogError($"[MaterialGenerator] KSampler node not found in workflow.");
                onDone(null); yield break;
            }
            Debug.LogWarning($"[MaterialGenerator] kSamplerNodeId \"{kSamplerNodeId}\" not found, resolved KSampler by class_type.");
        }
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
                Debug.LogError("[MaterialGenerator] ComfyUI unreachable: " + req.error);
                SetStatus("Error: ComfyUI not running at 127.0.0.1:8000");
                onDone(null); yield break;
            }

            try { promptId = JObject.Parse(req.downloadHandler.text)["prompt_id"]?.ToString(); }
            catch { }
        }

        if (string.IsNullOrEmpty(promptId))
        {
            Debug.LogError("[MaterialGenerator] No prompt_id in ComfyUI response.");
            onDone(null); yield break;
        }

        // Poll history
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
            Debug.LogError("[MaterialGenerator] Image generation timed out.");
            onDone(null); yield break;
        }

        // Download
        string url = $"{comfyUIUrl}/view" +
                     $"?filename={UnityWebRequest.EscapeURL(filename)}" +
                     $"&subfolder={UnityWebRequest.EscapeURL(subfolder)}" +
                     $"&type={UnityWebRequest.EscapeURL(type)}";

        using (var texReq = UnityWebRequestTexture.GetTexture(url))
        {
            yield return texReq.SendWebRequest();
            if (texReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[MaterialGenerator] Image download failed: " + texReq.error);
                onDone(null); yield break;
            }
            onDone(DownloadHandlerTexture.GetContent(texReq));
        }
    }

    // ── UI helpers ────────────────────────────────────────────────

    private void InstantiateCards(
        List<MaterialData> materials,
        Transform container,
        List<MaterialCardUI> cardList)
    {
        cardList.Clear();
        if (container == null || materialCardPrefab == null) return;

        for (int i = 0; i < materials.Count; i++)
        {
            var go   = Instantiate(materialCardPrefab, container);
            var card = go.GetComponent<MaterialCardUI>();
            if (card == null) { Debug.LogError("[MaterialGenerator] MaterialCard prefab missing MaterialCardUI"); continue; }

            var mat = materials[i]; // capture for lambda
            card.SetData(mat);
            cardList.Add(card);

            // Wire the Buy button
            if (card.buyButton != null)
            {
                card.buyButton.onClick.RemoveAllListeners();
                card.buyButton.onClick.AddListener(() => OnBuyCard(card, mat));
            }

            // Divide the container into thirds horizontally
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                float lo = i       / 3f;
                float hi = (i + 1) / 3f;
                rt.anchorMin        = new Vector2(lo, 0f);
                rt.anchorMax        = new Vector2(hi, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(-10f, 0f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
            }
        }
    }

    private void OnBuyCard(MaterialCardUI card, MaterialData mat)
    {
        if (GameManager.Instance == null) return;

        if (!GameManager.Instance.CanAfford(mat.price))
        {
            FlashGoldRed();
            return;
        }

        GameManager.Instance.SpendGold(mat.price);
        GameManager.Instance.AddToInventory(mat);
        card.SetSoldOut();
        UpdateGoldDisplay();
        CheckProceedButton();
    }

    private void UpdateGoldDisplay()
    {
        if (goldText == null) return;
        int gold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
        goldText.text = $"Gold: {gold}g";
    }

    private void CheckProceedButton()
    {
        if (proceedButton == null || GameManager.Instance == null) return;
        var inv = GameManager.Instance.inventory;
        bool hasCore = inv.Exists(m => m.materialType == "core");
        bool hasWood = inv.Exists(m => m.materialType == "wood");
        proceedButton.interactable = hasCore && hasWood;
    }

    private void FlashGoldRed()
    {
        if (goldText == null) return;
        if (_goldFlashCoroutine != null) StopCoroutine(_goldFlashCoroutine);
        _goldFlashCoroutine = StartCoroutine(DoFlashGoldRed());
    }

    private IEnumerator DoFlashGoldRed()
    {
        goldText.color = Color.red;
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            goldText.color = Color.Lerp(Color.red, _goldDefaultColor, t / 0.6f);
            yield return null;
        }
        goldText.color = _goldDefaultColor;
    }

    private void ClearCards()
    {
        _coreCards.Clear();
        _woodCards.Clear();
        if (coreCardContainer != null)
            foreach (Transform c in coreCardContainer) Destroy(c.gameObject);
        if (woodCardContainer != null)
            foreach (Transform c in woodCardContainer) Destroy(c.gameObject);
    }

    private void DisplayCustomer(CustomerOrder c)
    {
        void Set(TMP_Text t, string label, string val)
        { if (t != null) t.text = $"<b>{label}</b>  {val}"; }

        Set(nameText,        "NAME:",            c.customerName);
        Set(schoolText,      "SCHOOL OF MAGIC:", c.schoolOfMagic);
        Set(professionText,  "PROFESSION:",      c.profession);
        Set(personalityText, "PERSONALITY:",     c.personality);
        Set(requestText,     "REQUEST:",         c.request);
        Set(trueGoalText,    "TRUE GOAL:",       c.trueGoal);
        Set(constraintText,  "CONSTRAINT:",      c.constraint);
    }

    private void ClearDossier()
    {
        void Clear(TMP_Text t, string label) { if (t != null) t.text = $"<b>{label}</b>  —"; }
        Clear(nameText,        "NAME:");
        Clear(schoolText,      "SCHOOL OF MAGIC:");
        Clear(professionText,  "PROFESSION:");
        Clear(personalityText, "PERSONALITY:");
        Clear(requestText,     "REQUEST:");
        Clear(trueGoalText,    "TRUE GOAL:");
        Clear(constraintText,  "CONSTRAINT:");
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
                ReportError("Error: could not reach OpenAI. Check your API key and internet connection.");
                Debug.LogError("[MaterialGenerator] " + req.error + "\n" + req.downloadHandler.text);
                onContent(null); yield break;
            }

            string content = null;
            try { content = JObject.Parse(req.downloadHandler.text)["choices"]?[0]?["message"]?["content"]?.ToString(); }
            catch (Exception ex) { Debug.LogError("[MaterialGenerator] Response parse: " + ex.Message); }

            if (string.IsNullOrEmpty(content))
            { ReportError("Error: unexpected response format from OpenAI."); onContent(null); yield break; }

            onContent(content);
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
        Debug.Log("[MaterialGenerator] " + msg);
    }

    private void ReportError(string msg)
    {
        SetStatus(msg);
        Debug.LogError("[MaterialGenerator] " + msg);
    }

    private void SetButtonInteractable(bool v)
    { if (generateButton != null) generateButton.interactable = v; }

    private void Finish()
    {
        _busy = false;
        SetButtonInteractable(true);
    }
}
