using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CustomerGenerator : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────

    [Header("OpenAI Settings")]
    [Tooltip("OpenAI chat completions endpoint.")]
    public string openAIUrl = "https://api.openai.com/v1/chat/completions";

    [Header("UI — Controls")]
    [Tooltip("The Generate button — disabled during a request.")]
    public Button generateButton;

    [Tooltip("Proceed to Market button — enabled after a customer is generated.")]
    public Button proceedButton;

    [Tooltip("Status label shown below the button.")]
    public TMP_Text statusText;

    [Header("UI — Dossier Fields")]
    [Tooltip("Display text for customerName.")]
    public TMP_Text nameText;

    [Tooltip("Display text for schoolOfMagic.")]
    public TMP_Text schoolText;

    [Tooltip("Display text for profession.")]
    public TMP_Text professionText;

    [Tooltip("Display text for personality.")]
    public TMP_Text personalityText;

    [Tooltip("Display text for request.")]
    public TMP_Text requestText;

    [Tooltip("Display text for trueGoal.")]
    public TMP_Text trueGoalText;

    [Tooltip("Display text for constraint.")]
    public TMP_Text constraintText;

    [Header("Memo Gate")]
    [Tooltip("Reading-gate UI. Proceed button enables only when the memo is filled.")]
    public MemoFillUI memoFillUI;

    [Header("Memo Gate (UI Toolkit)")]
    [Tooltip("UI Toolkit replacement for the dossier + memo. If assigned, this is preferred over memoFillUI.")]
    public DossierPanelController dossierPanel;

    // ── Private state ──────────────────────────────────────────────

    private bool _busy;

    // ── Prompts ───────────────────────────────────────────────────

    private const string SystemPrompt =
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
  Examples:
    - Storm magic -> ""Assassin mage who uses lightning for the final blow""
    - Hydromancy  -> ""Field medic who uses water magic to perform emergency healing""
    - Shadow magic -> ""Bounty hunter who uses shadow magic to track and corner targets""

personality
  Exactly 3 traits. At least one must be in tension with the others.
  Format: ""Trait, trait, trait""
  Example: ""Outwardly calm, internally panicked, fiercely protective""

request
  What they say out loud in the shop. Written in first person, conversational tone.
  It must describe a specific practical problem they want the wand to solve.
  The problem must be a logical drawback of their school of magic used in their profession.
  Do NOT make this a generic ""I want a powerful wand"" request.
  Example: ""I need a wand that lets me control the exact pressure of my water flow.
            Too much force and I damage the wound instead of closing it.""

trueGoal
  What they actually want to achieve - more specific and ambitious than the request.
  Must include the profession's core demand (speed, precision, range, etc.)
  and show why those demands are in slight tension with each other.
  Example: ""To perform precise, high-speed healing on multiple patients
            in rapid succession without losing control of the flow""

constraint
  Their magical limitation. It must directly explain WHY the problem in the request happens.
  The cruelest constraints are ones that activate at the worst possible moment given their job.
  Example: ""Her water magic amplifies in intensity when she is emotionally distressed -
            exactly when she needs it most delicate""

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

    private const string UserPrompt =
@"Generate a new customer. Here are three examples of the correct style.
Study the logical chain in each one before generating a new character.
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

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Start()
    {
        SetStatus("Waiting...");
        ClearDossier();
        if (proceedButton != null)
        {
            proceedButton.interactable = false;
            proceedButton.onClick.AddListener(() =>
                GameManager.Instance?.LoadScene(GameManager.SCENE_MARKET));
        }
    }

    // ── Public API — wired to the button ──────────────────────────

    public void GenerateCustomer()
    {
        if (_busy) return;
        StartCoroutine(GeneratePipeline());
    }

    // ── Pipeline ──────────────────────────────────────────────────

    private IEnumerator GeneratePipeline()
    {
        _busy = true;
        SetButtonInteractable(false);
        SetStatus("Loading API key...");

        // ── Load API key from StreamingAssets/config.json ─────────
        string configPath = Path.Combine(Application.streamingAssetsPath, "config.json");
        string apiKey = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var cfgReq = UnityWebRequest.Get(configPath))
        {
            yield return cfgReq.SendWebRequest();
            if (cfgReq.result == UnityWebRequest.Result.Success)
            {
                try { apiKey = JObject.Parse(cfgReq.downloadHandler.text)["openAIApiKey"]?.ToString(); }
                catch { }
            }
        }
#else
        if (File.Exists(configPath))
        {
            try { apiKey = JObject.Parse(File.ReadAllText(configPath))["openAIApiKey"]?.ToString(); }
            catch { }
        }
#endif

        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_KEY_HERE")
        {
            ReportError("Error: no API key found in StreamingAssets/config.json");
            yield break;
        }

        SetStatus("Generating customer...");

        // ── Build request body ────────────────────────────────────
        var body = new JObject
        {
            ["model"]    = "gpt-4o",
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = SystemPrompt },
                new JObject { ["role"] = "user",   ["content"] = UserPrompt   }
            }
        };

        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(body.ToString());

        using (var req = new UnityWebRequest(openAIUrl, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type",  "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                ReportError("Error: could not reach OpenAI. Check your API key and internet connection.");
                Debug.LogError("[CustomerGenerator] HTTP error: " + req.error +
                               "\nResponse: " + req.downloadHandler.text);
                yield break;
            }

            // ── Extract content string from response ──────────────
            string content = null;
            try
            {
                var response = JObject.Parse(req.downloadHandler.text);
                content = response["choices"]?[0]?["message"]?["content"]?.ToString();
            }
            catch (Exception ex)
            {
                ReportError("Error: unexpected response format from OpenAI.");
                Debug.LogError("[CustomerGenerator] Response parse error: " + ex.Message);
                yield break;
            }

            if (string.IsNullOrEmpty(content))
            {
                ReportError("Error: unexpected response format from OpenAI.");
                yield break;
            }

            // ── Strip markdown code fences if present ─────────────
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                int firstNewline = content.IndexOf('\n');
                int lastFence    = content.LastIndexOf("```");
                if (firstNewline >= 0 && lastFence > firstNewline)
                    content = content.Substring(firstNewline, lastFence - firstNewline).Trim();
            }

            // ── Parse the customer JSON ───────────────────────────
            JObject customer;
            try { customer = JObject.Parse(content); }
            catch (Exception ex)
            {
                ReportError("Error: unexpected response format from OpenAI.");
                Debug.LogError("[CustomerGenerator] Customer JSON parse error: " + ex.Message +
                               "\nRaw content: " + content);
                yield break;
            }

            PopulateDossier(customer);

            // Push to GameManager so other scenes can read the customer.
            CustomerOrder order = new CustomerOrder
            {
                customerName  = customer["customerName"]?.ToString()  ?? "",
                schoolOfMagic = customer["schoolOfMagic"]?.ToString() ?? "",
                profession    = customer["profession"]?.ToString()     ?? "",
                personality   = customer["personality"]?.ToString()   ?? "",
                request       = customer["request"]?.ToString()       ?? "",
                trueGoal      = customer["trueGoal"]?.ToString()      ?? "",
                constraint    = customer["constraint"]?.ToString()    ?? "",
            };
            if (GameManager.Instance != null)
            {
                GameManager.Instance.currentCustomer = order;
                GameManager.Instance.currentMemo     = null;
            }

            // Memo gate: prefer the UI Toolkit panel if assigned, else fall back
            // to the legacy uGUI MemoFillUI. If neither is wired (standalone test),
            // Proceed enables immediately.
            if (dossierPanel != null)
            {
                dossierPanel.Begin(order, OnMemoComplete);
                SetStatus("Read the dossier. Fill the memo to proceed.");
            }
            else if (memoFillUI != null)
            {
                memoFillUI.Begin(order, OnMemoComplete);
                SetStatus("Read the dossier. Fill the memo to proceed.");
            }
            else
            {
                if (proceedButton != null) proceedButton.interactable = true;
                SetStatus("Customer ready. Proceed to the market.");
            }
        }

        _busy = false;
        SetButtonInteractable(true);
    }

    private void OnMemoComplete()
    {
        if (proceedButton != null) proceedButton.interactable = true;
        SetStatus("Memo complete. Proceed to the market.");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void PopulateDossier(JObject c)
    {
        void Set(TMP_Text t, string label, string key)
        {
            if (t != null) t.text = $"<b>{label}</b>  {c[key]?.ToString() ?? "—"}";
        }

        Set(nameText,        "NAME:",            "customerName");
        Set(schoolText,      "SCHOOL OF MAGIC:", "schoolOfMagic");
        Set(professionText,  "PROFESSION:",      "profession");
        Set(personalityText, "PERSONALITY:",     "personality");
        Set(requestText,     "REQUEST:",         "request");
        Set(trueGoalText,    "TRUE GOAL:",       "trueGoal");
        Set(constraintText,  "CONSTRAINT:",      "constraint");
    }

    private void ClearDossier()
    {
        void Clear(TMP_Text t, string label)
        { if (t != null) t.text = $"<b>{label}</b>  —"; }

        Clear(nameText,        "NAME:");
        Clear(schoolText,      "SCHOOL OF MAGIC:");
        Clear(professionText,  "PROFESSION:");
        Clear(personalityText, "PERSONALITY:");
        Clear(requestText,     "REQUEST:");
        Clear(trueGoalText,    "TRUE GOAL:");
        Clear(constraintText,  "CONSTRAINT:");
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[CustomerGenerator] " + msg);
    }

    private void ReportError(string msg)
    {
        SetStatus(msg);
        Debug.LogError("[CustomerGenerator] " + msg);
        _busy = false;
        SetButtonInteractable(true);
    }

    private void SetButtonInteractable(bool v)
    {
        if (generateButton != null) generateButton.interactable = v;
    }
}
