using System.Collections;
using UnityEngine;

/// <summary>
/// CustomerGeneratorTest scene driver. Owns no UI of its own — all
/// presentation lives in <see cref="DossierPanelController"/> on the
/// DossierUIDocument GameObject.
///
/// Flow: pick up a pre-generated customer from MorningScene if available;
/// otherwise call <see cref="CustomerService.GenerateAsync"/> ourselves.
/// As soon as a customer is set, kick off material pre-generation on the
/// persistent GameManager so the next scene loads with images already
/// streaming.
/// </summary>
[DisallowMultipleComponent]
public class CustomerGenerator : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [Tooltip("OpenAI chat completions endpoint.")]
    public string openAIUrl = CustomerService.DefaultUrl;

    [Header("Material Pre-generation")]
    [Tooltip("If true, fires off the material text + image API requests as soon as " +
             "a customer is set so MaterialGenerator scene can skip most of its wait.")]
    public bool preGenerateMaterials = true;
    [Tooltip("ComfyUI endpoint used for material image pre-generation.")]
    public string comfyUIUrl = MaterialService.DefaultComfyUIUrl;
    public string clipNodeId = MaterialService.DefaultClipNodeId;
    public string ksamplerNodeId = MaterialService.DefaultKSamplerNodeId;

    [Header("UI Toolkit dossier panel")]
    [Tooltip("DossierPanelController on the DossierUIDocument GameObject. Required.")]
    public DossierPanelController dossierPanel;

    private bool _busy;

    private void Start()
    {
        if (dossierPanel == null)
        {
            Debug.LogError("[CustomerGenerator] dossierPanel is not assigned.");
            return;
        }
        dossierPanel.SetStatus("Greeting the customer...");
        GenerateCustomer();
    }

    public void GenerateCustomer()
    {
        if (_busy) return;
        StartCoroutine(GeneratePipeline());
    }

    private IEnumerator GeneratePipeline()
    {
        _busy = true;
        var gm = GameManager.Instance;

        // Fast path: MorningScene already kicked off the API request and
        // either has the result or is still waiting for the response.
        if (gm != null && (gm.pendingCustomer != null || gm.pendingCustomerInProgress))
        {
            if (gm.pendingCustomer == null)
            {
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
                yield break;
            }
        }

        dossierPanel.SetStatus("Generating customer...");

        CustomerOrder generated = null;
        string error = null;
        yield return CustomerService.GenerateAsync(openAIUrl, (order, err) =>
        {
            generated = order;
            error     = err;
        });

        if (generated == null)
        {
            dossierPanel.SetStatus("Error: " + (error ?? "could not generate customer."));
            Debug.LogError("[CustomerGenerator] " + error);
            _busy = false;
            yield break;
        }

        UseOrder(generated);
        _busy = false;
    }

    private void UseOrder(CustomerOrder order)
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.currentCustomer = order;
            gm.currentMemo     = null;

            // Kick off material pre-generation if MorningScene didn't already.
            // Hosted on GameManager so coroutines survive scene transitions.
            if (preGenerateMaterials
                && gm.pendingMaterials == null
                && !gm.pendingMaterialsInProgress)
            {
                gm.StartCoroutine(MaterialService.PreGenAsync(
                    gm, gm, order, openAIUrl, comfyUIUrl, clipNodeId, ksamplerNodeId));
            }
        }

        dossierPanel.Begin(order, OnMemoComplete);
    }

    private void OnMemoComplete()
    {
        // DossierPanelController owns the Proceed button and re-enables it;
        // nothing else to do here.
    }
}
