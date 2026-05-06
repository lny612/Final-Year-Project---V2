using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// EvaluationScene driver. Fetches the GPT match-score for the player's wand,
/// applies rewards (gold + reputation), then hands the data off to
/// <see cref="EvaluationResultController"/> for the theatrical UI Toolkit
/// reveal. Day-progression dots and the rent-payment modal still live on the
/// Canvas as their own widgets and are driven by <see cref="DayProgressUI"/>
/// and <see cref="RentPaymentUI"/>.
/// </summary>
[DisallowMultipleComponent]
public class EvaluationManager : MonoBehaviour
{
    [Header("API Settings")]
    public string openAIUrl = "https://api.openai.com/v1/chat/completions";

    [Header("7-Day progression")]
    [Tooltip("Calendar dots shown at top of screen.")]
    public DayProgressUI  dayProgressUI;
    [Tooltip("Modal shown on days 3 and 6 to collect rent.")]
    public RentPaymentUI  rentPaymentUI;

    [Header("UI Toolkit (theatrical reveal)")]
    [Tooltip("EvaluationResultController on the EvaluationUIDocument GameObject.")]
    public EvaluationResultController resultController;

    private CustomerOrder _customer;
    private WandResult    _wand;

    private void Start()
    {
        _customer = GameManager.Instance?.currentCustomer;
        _wand     = GameManager.Instance?.currentWandResult;

        dayProgressUI?.Refresh();
        SetStatus("Evaluating...");
        StartCoroutine(EvaluationPipeline());
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
            GameManager.Instance.lastMatchScore = matchScore;
        }

        SetStatus("Evaluation complete.");

        if (resultController == null)
        {
            Debug.LogError("[EvaluationManager] resultController is not assigned. Cannot reveal result.");
            yield break;
        }

        DriveResultController(matchScore, verdict, whatWorked, whatMissed,
                              customerReaction, goldEarned, reputationChange);
    }

    private void DriveResultController(int matchScore, string verdict, string worked,
        string missed, string reaction, int gold, int rep)
    {
        var gm = GameManager.Instance;
        char conjuringGrade = gm?.craftingQualityGrade ?? 'A';
        int  conjuringWon   = gm?.lastMinigameRoundsWon ?? 3;
        int  conjuringTotal = 3;

        char finalGrade = ComputeFinalGrade(matchScore, conjuringGrade);

        var data = new EvaluationResultController.ResultData
        {
            wandName       = _wand?.wandName ?? "",
            wandTexture    = _wand?.wandImage,
            verdict        = string.IsNullOrEmpty(verdict) ? reaction : verdict,
            conjuringWon   = conjuringWon,
            conjuringTotal = conjuringTotal,
            materialsScore = matchScore,
            customerFitLabel = MatchLabel(matchScore),
            goldEarned     = gold,
            repDelta       = rep,
            finalGrade     = finalGrade,
        };

        resultController.ApplyResult(data, OnNextCustomer);
    }

    private static string MatchLabel(int score)
    {
        if (score >= 85) return "Loved it";
        if (score >= 70) return "Liked it";
        if (score >= 50) return "Acceptable";
        if (score >= 30) return "Lukewarm";
        return "Disappointed";
    }

    private static char ComputeFinalGrade(int matchScore, char conjuringGrade)
    {
        // Conjuring quality scales the match score (mirrors the reward formula).
        float mult = conjuringGrade switch
        {
            'A' => 1.00f,
            'B' => 0.85f,
            'C' => 0.70f,
            'D' => 0.55f,
            _   => 0.40f,
        };
        float composite = matchScore * mult;
        if (composite >= 85f) return 'A';
        if (composite >= 70f) return 'B';
        if (composite >= 55f) return 'C';
        if (composite >= 40f) return 'D';
        return 'F';
    }

    private static float GetQualityMultiplier(char grade)
    {
        return grade switch
        {
            'A' => 1.0f,
            'B' => 0.85f,
            'C' => 0.7f,
            'D' => 0.55f,
            _   => 0.4f
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

    private void GoToEnding() => GameManager.Instance?.LoadScene(GameManager.SCENE_ENDING);

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

    private void SetStatus(string msg) => Debug.Log("[EvaluationManager] " + msg);
}
