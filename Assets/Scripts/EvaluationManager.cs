using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Manages the EvaluationScene: shows customer dossier and wand result,
/// calls OpenAI to evaluate the match, animates score, and handles rewards.
/// </summary>
[DisallowMultipleComponent]
public class EvaluationManager : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────

    [Header("API Settings")]
    public string openAIUrl = "https://api.openai.com/v1/chat/completions";

    [Header("UI — Left Panel (Customer)")]
    public TMP_Text customerNameText;
    public TMP_Text customerSchoolText;
    public TMP_Text customerProfessionText;
    public TMP_Text customerPersonalityText;
    public TMP_Text customerRequestText;
    public TMP_Text customerGoalText;
    public TMP_Text customerConstraintText;

    [Header("UI — Center Panel (Wand)")]
    public RawImage wandImage;
    public TMP_Text wandNameText;
    public TMP_Text wandDescText;
    public TMP_Text wandAttribText;
    public TMP_Text wandMaterialsText;

    [Header("UI — Right Panel (Result)")]
    public GameObject resultPanel;
    public TMP_Text   scoreText;
    public TMP_Text   verdictText;
    public TMP_Text   whatWorkedText;
    public TMP_Text   whatMissedText;
    public TMP_Text   customerReactionText;
    public TMP_Text   goldEarnedText;
    public TMP_Text   reputationText;
    public Button     nextCustomerButton;

    [Header("UI — Status")]
    public TMP_Text statusText;

    [Header("7-Day progression")]
    [Tooltip("Calendar dots shown at top of screen.")]
    public DayProgressUI  dayProgressUI;
    [Tooltip("Modal shown on days 3 and 6 to collect rent.")]
    public RentPaymentUI  rentPaymentUI;

    // ── Private ───────────────────────────────────────────────────

    private CustomerOrder _customer;
    private WandResult    _wand;

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Start()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        nextCustomerButton?.onClick.AddListener(OnNextCustomer);

        _customer = GameManager.Instance?.currentCustomer;
        _wand     = GameManager.Instance?.currentWandResult;

        dayProgressUI?.Refresh();

        PopulateCustomer();
        PopulateWand();
        SetStatus("Evaluating...");
        StartCoroutine(EvaluationPipeline());
    }

    // ── Populate panels ───────────────────────────────────────────

    private void PopulateCustomer()
    {
        if (_customer == null) return;
        void Set(TMP_Text t, string lbl, string val) { if (t != null) t.text = $"<b>{lbl}</b>  {val}"; }
        Set(customerNameText,        "Name:",        _customer.customerName);
        Set(customerSchoolText,      "School:",      _customer.schoolOfMagic);
        Set(customerProfessionText,  "Profession:",  _customer.profession);
        Set(customerPersonalityText, "Personality:", _customer.personality);
        Set(customerRequestText,     "Request:",     _customer.request);
        Set(customerGoalText,        "True Goal:",   _customer.trueGoal);
        Set(customerConstraintText,  "Constraint:",  _customer.constraint);
    }

    private void PopulateWand()
    {
        if (_wand == null) return;
        if (wandImage != null && _wand.wandImage != null)
            wandImage.texture = _wand.wandImage;
        if (wandNameText != null) wandNameText.text = _wand.wandName;
        if (wandDescText != null) wandDescText.text = _wand.description;
        if (wandAttribText != null && _wand.attributes != null)
            wandAttribText.text = "• " + string.Join("\n• ", _wand.attributes);
    }

    // ── Evaluation pipeline ───────────────────────────────────────

    private IEnumerator EvaluationPipeline()
    {
        string apiKey = null;
        yield return StartCoroutine(LoadApiKey(k => apiKey = k));
        if (string.IsNullOrEmpty(apiKey))
        {
            SetStatus("Error: no API key found. Check StreamingAssets/config.json");
            yield break;
        }

        if (_customer == null || _wand == null)
        {
            SetStatus("Error: missing customer or wand data.");
            yield break;
        }

        string userMsg = BuildEvalUserMessage();
        var body = new JObject
        {
            ["model"]    = "gpt-4o",
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = EvalSystemPrompt },
                new JObject { ["role"] = "user",   ["content"] = userMsg }
            }
        };

        string content = null;
        yield return StartCoroutine(PostOpenAI(apiKey, body, r => content = r));
        if (content == null)
        {
            SetStatus("Error: evaluation API call failed. Check API key and connection.");
            yield break;
        }

        content = StripCodeFences(content);

        int    matchScore       = 0;
        string verdict          = "";
        string whatWorked       = "";
        string whatMissed       = null;
        string customerReaction = "";

        try
        {
            var j       = JObject.Parse(content);
            matchScore       = j["matchScore"]?.Value<int>()       ?? 0;
            verdict          = j["verdict"]?.ToString()            ?? "";
            whatWorked       = j["whatWorked"]?.ToString()         ?? "";
            whatMissed       = j["whatMissed"]?.ToString();
            customerReaction = j["customerReaction"]?.ToString()   ?? "";
        }
        catch (Exception ex)
        {
            Debug.LogError("[EvaluationManager] Parse: " + ex.Message);
            SetStatus("Error: unexpected evaluation response format.");
            yield break;
        }

        // Rewards (quality grade from tracing minigame scales rewards)
        float qualityMult    = GetQualityMultiplier(
            GameManager.Instance?.craftingQualityGrade ?? 'A');
        int goldEarned       = Mathf.RoundToInt(150 * (matchScore / 100f) * qualityMult);
        int reputationChange = matchScore >= 40
                               ? Mathf.RoundToInt(20 * (matchScore / 100f) * qualityMult)
                               : -10;

        GameManager.Instance?.AddGold(goldEarned);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerReputation += reputationChange;
            GameManager.Instance.peakReputation = Mathf.Max(
                GameManager.Instance.peakReputation,
                GameManager.Instance.playerReputation);
            GameManager.Instance.wandsCrafted++;
        }

        SetStatus("Evaluation complete.");

        // Show result panel then animate
        if (resultPanel != null) resultPanel.SetActive(true);
        yield return StartCoroutine(RevealResults(
            matchScore, verdict, whatWorked, whatMissed,
            customerReaction, goldEarned, reputationChange));
    }

    // ── Result reveal animation ───────────────────────────────────

    private IEnumerator RevealResults(int score, string verdict, string worked,
        string missed, string reaction, int gold, int rep)
    {
        // Score count-up
        float duration = 1.5f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int displayed = Mathf.RoundToInt(Mathf.Lerp(0, score, elapsed / duration));
            if (scoreText != null)
            {
                scoreText.text  = displayed.ToString();
                scoreText.color = displayed >= 70 ? Color.green
                                : displayed >= 40 ? Color.yellow
                                : Color.red;
            }
            yield return null;
        }
        if (scoreText != null)
        {
            scoreText.text  = score.ToString();
            scoreText.color = score >= 70 ? Color.green : score >= 40 ? Color.yellow : Color.red;
        }

        float delay = 0.3f;

        char grade = GameManager.Instance?.craftingQualityGrade ?? 'A';
        yield return FadeInText(verdictText,
            $"<b>Crafting Quality: {grade}</b>  —  {verdict}",                               delay);
        yield return FadeInText(whatWorkedText,        $"<b>What worked:</b>  {worked}",      delay);
        if (score < 85 && !string.IsNullOrEmpty(missed))
            yield return FadeInText(whatMissedText,   $"<b>What missed:</b>  {missed}",       delay);
        yield return FadeInText(customerReactionText, $"\"{reaction}\"",                      delay);
        yield return FadeInText(goldEarnedText,        $"+{gold}g",                           delay);
        if (goldEarnedText != null) goldEarnedText.color = Color.green;
        yield return FadeInText(reputationText,
            rep >= 0 ? $"+{rep} rep" : $"{rep} rep",  delay);
        if (reputationText != null) reputationText.color = rep >= 0 ? Color.green : Color.red;

        if (nextCustomerButton != null) nextCustomerButton.gameObject.SetActive(true);
    }

    private IEnumerator FadeInText(TMP_Text t, string text, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (t == null) yield break;
        t.text    = text;
        t.alpha   = 0f;
        float el  = 0f;
        while (el < 0.4f)
        {
            el      += Time.deltaTime;
            t.alpha  = Mathf.Clamp01(el / 0.4f);
            yield return null;
        }
        t.alpha = 1f;
    }

    // ── Quality grade ─────────────────────────────────────────────

    private static float GetQualityMultiplier(char grade)
    {
        return grade switch
        {
            'A' => 1.0f,
            'B' => 0.85f,
            'C' => 0.7f,
            'D' => 0.55f,
            _   => 0.4f   // F or unknown
        };
    }

    // ── Advance to next day / trigger ending ──────────────────────

    private void OnNextCustomer()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.IsRentDueToday() && rentPaymentUI != null)
        {
            rentPaymentUI.Show(gm.GetRentDueToday(), result =>
            {
                if (result == RentPaymentUI.RentResult.Paid) ProceedToNextDayOrEnding();
                else                                         GoToEnding();
            });
            return;
        }

        ProceedToNextDayOrEnding();
    }

    private void ProceedToNextDayOrEnding()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.currentDay >= GameManager.TOTAL_DAYS) GoToEnding();
        else                                         gm.AdvanceToNextDay();
    }

    private void GoToEnding()
    {
        GameManager.Instance?.LoadScene(GameManager.SCENE_ENDING);
    }

    // ── Prompts ───────────────────────────────────────────────────

    private const string EvalSystemPrompt =
@"You are a senior wandmaker evaluating whether a finished wand suits
a specific customer. You are discerning, nuanced, and honest.
You understand that what the customer said in their request is not
always what they truly needed — a skilled wandmaker reads the tension
between the customer's true goal and their constraint.

Scoring rules:
- A wand that only addresses the request scores 40-60.
- A wand that addresses the true goal scores 60-75.
- A wand that addresses both the true goal AND accounts for the
  constraint scores 75-95.
- A wand that perfectly resolves the tension between true goal and
  constraint in a creative way scores 90-100.
- A wand that conflicts with the constraint despite matching the
  request scores 20-40.
- Reward creative material combinations that serve the customer's
  subtext. Penalise combinations that look right but ignore
  the constraint.

Return ONLY valid JSON. No markdown. No explanation.
Use exactly this structure:
{
  ""matchScore"": number,
  ""verdict"": string,
  ""whatWorked"": string,
  ""whatMissed"": string,
  ""customerReaction"": string
}";

    private string BuildEvalUserMessage()
    {
        var attribs = _wand.attributes != null
            ? "• " + string.Join("\n• ", _wand.attributes)
            : "";

        return
$@"CUSTOMER DOSSIER:
Name: {_customer.customerName}
School of Magic: {_customer.schoolOfMagic}
Profession: {_customer.profession}
Personality: {_customer.personality}
Request: ""{_customer.request}""
True Goal: {_customer.trueGoal}
Constraint: {_customer.constraint}

WAND SUBMITTED:
Name: {_wand.wandName}
Description: {_wand.description}
Attributes:
{attribs}

Evaluate this wand against this customer.
Read the tension between their true goal and constraint carefully.
Score and explain accordingly.";
    }

    // ── Shared helpers ────────────────────────────────────────────

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
                Debug.LogError("[EvaluationManager] " + req.error + "\n" + req.downloadHandler.text);
                onContent(null); yield break;
            }

            string content = null;
            try { content = JObject.Parse(req.downloadHandler.text)["choices"]?[0]?["message"]?["content"]?.ToString(); }
            catch (Exception ex) { Debug.LogError("[EvaluationManager] Response parse: " + ex.Message); }

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
        Debug.Log("[EvaluationManager] " + msg);
    }
}
