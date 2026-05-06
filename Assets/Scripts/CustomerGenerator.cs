using System.Collections;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CustomerGenerator : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────

    [Header("OpenAI Settings")]
    [Tooltip("OpenAI chat completions endpoint.")]
    public string openAIUrl = "https://api.openai.com/v1/chat/completions";

    [Header("Material Pre-generation")]
    [Tooltip("If true, fires off the material text + image API requests as soon as " +
             "a customer is set so MaterialGenerator scene can skip most of its wait.")]
    public bool preGenerateMaterials = true;
    [Tooltip("ComfyUI endpoint used for material image pre-generation.")]
    public string comfyUIUrl = MaterialService.DefaultComfyUIUrl;
    public string clipNodeId = MaterialService.DefaultClipNodeId;
    public string ksamplerNodeId = MaterialService.DefaultKSamplerNodeId;

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

    // Customer prompts now live in CustomerService.cs so MorningScreenController
    // can pre-generate against the same prompts.

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

        // Auto-generate the customer the moment the scene loads. The legacy
        // "Summon a customer" button is gone — the morning scene's "Read the
        // Dossier" button is now the only entry point into this scene.
        GenerateCustomer();
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

        var gm = GameManager.Instance;

        // ── Fast path: MorningScene already kicked off the API request and
        // either has the result or is still waiting for the response. Use it
        // instead of doing a second roundtrip.
        if (gm != null && (gm.pendingCustomer != null || gm.pendingCustomerInProgress))
        {
            if (gm.pendingCustomer == null)
            {
                SetStatus("Greeting the customer...");
                // Defensive timeout: if pendingCustomerInProgress somehow gets
                // stuck (e.g. coroutine host destroyed mid-flight), bail to a
                // fresh request after 10s instead of looping forever.
                const float MaxWaitSeconds = 10f;
                float waited = 0f;
                while (gm.pendingCustomerInProgress && waited < MaxWaitSeconds)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (gm.pendingCustomerInProgress)
                {
                    Debug.LogWarning("[CustomerGenerator] Pre-gen took too long; firing a fresh request.");
                    gm.pendingCustomerInProgress = false;
                }
            }

            if (gm.pendingCustomer != null)
            {
                var order = gm.pendingCustomer;
                gm.pendingCustomer = null;
                UseOrder(order);
                _busy = false;
                SetButtonInteractable(true);
                yield break;
            }
            // pendingCustomer was cleared with no result (failure during pre-gen);
            // fall through to a fresh request below.
        }

        SetStatus("Generating customer...");

        CustomerOrder generated = null;
        string error = null;
        yield return CustomerService.GenerateAsync(openAIUrl, (order, err) =>
        {
            generated = order;
            error     = err;
        });

        if (generated == null)
        {
            ReportError("Error: " + (error ?? "could not generate customer."));
            yield break;
        }

        UseOrder(generated);

        _busy = false;
        SetButtonInteractable(true);
    }

    private void UseOrder(CustomerOrder order)
    {
        // Mirror the populated fields onto the legacy uGUI dossier (still used
        // as a fallback if neither memo controller is wired).
        var customer = new JObject
        {
            ["customerName"]  = order.customerName,
            ["schoolOfMagic"] = order.schoolOfMagic,
            ["profession"]    = order.profession,
            ["personality"]   = order.personality,
            ["request"]       = order.request,
            ["trueGoal"]      = order.trueGoal,
            ["constraint"]    = order.constraint,
        };
        PopulateDossier(customer);

        var gmHere = GameManager.Instance;
        if (gmHere != null)
        {
            gmHere.currentCustomer = order;
            gmHere.currentMemo     = null;

            // Kick off material pre-generation if MorningScene didn't already do it.
            // Hosted on GameManager so the coroutine survives scene transitions.
            if (preGenerateMaterials
                && gmHere.pendingMaterials == null
                && !gmHere.pendingMaterialsInProgress)
            {
                gmHere.StartCoroutine(MaterialService.PreGenAsync(
                    gmHere, gmHere, order, openAIUrl, comfyUIUrl, clipNodeId, ksamplerNodeId));
            }
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
