/*
 * ================================================================
 *  ComfyUITest.cs  —  Proof-of-concept ComfyUI ↔ Unity bridge
 * ================================================================
 *
 *  HOW TO FIND YOUR WORKFLOW NODE IDs
 *  ───────────────────────────────────
 *  1. Open  Assets/StreamingAssets/image_z_image_turbo.json
 *     in Notepad (or any text editor).
 *
 *  2. To find the CLIP node (where the prompt text goes):
 *       Search (Ctrl-F) for:  CLIPTextEncode
 *       The JSON key on the line just above that entry is the node
 *       ID, e.g. "57:27".  Type that value into the
 *       "Clip Node Id" field on this component in the Inspector.
 *
 *  3. To find the KSampler node (where the random seed goes):
 *       Search (Ctrl-F) for:  KSampler
 *       The JSON key on the line just above that entry is the node
 *       ID, e.g. "57:3".  Type that value into the
 *       "K Sampler Node Id" field in the Inspector.
 *
 *  4. Save the scene.  The new IDs take effect immediately in
 *     Play Mode — no code changes required.
 * ================================================================
 *
 *  PIPELINE OVERVIEW
 *  ─────────────────
 *  Step 1 – POST /prompt        : load workflow JSON, inject prompt
 *                                 + random seed, send to ComfyUI.
 *  Step 2 – GET  /history/{id}  : poll every 1.5 s until output
 *                                 images appear (60 s timeout).
 *  Step 3 – GET  /view?…        : download the finished image and
 *                                 display it in the RawImage.
 * ================================================================
 */

using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ComfyUITest : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("ComfyUI Settings")]
    [Tooltip("Base URL of your running ComfyUI instance.")]
    public string comfyUIUrl     = "http://127.0.0.1:8000";

    [Tooltip("The JSON key of the CLIPTextEncode node in the workflow. " +
             "Open image_z_image_turbo.json and search for \"CLIPTextEncode\" " +
             "to find this key (e.g. \"57:27\").")]
    public string clipNodeId     = "5";

    [Tooltip("The JSON key of the KSampler node in the workflow. " +
             "Open image_z_image_turbo.json and search for \"KSampler\" " +
             "to find this key (e.g. \"4\").")]
    public string kSamplerNodeId = "4";

    [Header("UI References")]
    [Tooltip("The TMP_InputField where the user types their prompt.")]
    public TMP_InputField promptInput;

    [Tooltip("The Generate button — disabled during generation.")]
    public Button generateButton;

    [Tooltip("The RawImage that displays the generated picture.")]
    public RawImage outputImage;

    [Tooltip("Label that shows current status / error messages.")]
    public TMP_Text statusText;

    // ── Private state ─────────────────────────────────────────────

    private bool _generating = false;

    // ── Unity lifecycle ───────────────────────────────────────────

    private void Start()
    {
        SetStatus("Waiting…");
    }

    // ── Public API — wired to the Generate button ─────────────────

    /// <summary>
    /// Call this from the Button's onClick event (already wired in the
    /// prefab).  Starts the full generation pipeline as a coroutine.
    /// </summary>
    public void GenerateImage()
    {
        if (_generating) return;

        string prompt = promptInput != null ? promptInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(prompt))
        {
            SetStatus("Please type a prompt first.");
            return;
        }

        StartCoroutine(GeneratePipeline(prompt));
    }

    // ── Step 1 : POST the workflow ────────────────────────────────

    private IEnumerator GeneratePipeline(string userPrompt)
    {
        _generating = true;
        SetButtonInteractable(false);
        SetStatus("Sending prompt to ComfyUI…");

        // ── Load the workflow JSON from StreamingAssets ──────────
        string workflowPath = System.IO.Path.Combine(
            Application.streamingAssetsPath,
            "image_z_image_turbo.json");

        string workflowJson = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, StreamingAssets lives inside the APK.
        using (var fileReq = UnityWebRequest.Get(workflowPath))
        {
            yield return fileReq.SendWebRequest();
            if (fileReq.result != UnityWebRequest.Result.Success)
            {
                ReportError("Could not load workflow file: " + fileReq.error);
                yield break;
            }
            workflowJson = fileReq.downloadHandler.text;
        }
#else
        if (!System.IO.File.Exists(workflowPath))
        {
            ReportError("Workflow file not found.\nExpected: " + workflowPath);
            yield break;
        }
        workflowJson = System.IO.File.ReadAllText(workflowPath);
#endif

        // ── Parse the workflow JSON ──────────────────────────────
        JObject workflow;
        try
        {
            workflow = JObject.Parse(workflowJson);
        }
        catch (Exception ex)
        {
            ReportError("Failed to parse workflow JSON: " + ex.Message);
            yield break;
        }

        // ── Inject the user prompt into the CLIP node ────────────
        var clipNode = workflow[clipNodeId];
        if (clipNode == null)
        {
            ReportError(
                $"CLIP node \"{clipNodeId}\" not found in the workflow JSON.\n" +
                "Check the Clip Node Id field in the Inspector\n" +
                "(open the JSON, search for CLIPTextEncode, copy the key above it).");
            yield break;
        }
        clipNode["inputs"]["text"] = userPrompt;

        // ── Inject a fresh random seed into the KSampler node ────
        var ksamplerNode = workflow[kSamplerNodeId];
        if (ksamplerNode == null)
        {
            ReportError(
                $"KSampler node \"{kSamplerNodeId}\" not found in the workflow JSON.\n" +
                "Check the K Sampler Node Id field in the Inspector\n" +
                "(open the JSON, search for KSampler, copy the key above it).");
            yield break;
        }
        // Use the full range so every generation is unique.
        ksamplerNode["inputs"]["seed"] = (long)UnityEngine.Random.Range(0, int.MaxValue);

        // ── Wrap workflow and POST to /prompt ────────────────────
        string body      = new JObject { ["prompt"] = workflow }.ToString();
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);

        using (var postReq = new UnityWebRequest(comfyUIUrl + "/prompt", "POST"))
        {
            postReq.uploadHandler   = new UploadHandlerRaw(bodyBytes);
            postReq.downloadHandler = new DownloadHandlerBuffer();
            postReq.SetRequestHeader("Content-Type", "application/json");

            yield return postReq.SendWebRequest();

            if (postReq.result != UnityWebRequest.Result.Success)
            {
                ReportError(
                    "Error: could not reach ComfyUI. Is it running at " +
                    comfyUIUrl + "?\n" + postReq.error);
                yield break;
            }

            // ── Parse prompt_id from the response ────────────────
            JObject responseJson;
            try
            {
                responseJson = JObject.Parse(postReq.downloadHandler.text);
            }
            catch (Exception ex)
            {
                ReportError("Unexpected response from /prompt: " + ex.Message +
                            "\nResponse: " + postReq.downloadHandler.text);
                yield break;
            }

            string promptId = responseJson["prompt_id"]?.ToString();
            if (string.IsNullOrEmpty(promptId))
            {
                ReportError("No prompt_id in ComfyUI response.\n" +
                            "Response was: " + postReq.downloadHandler.text);
                yield break;
            }

            // ── Step 2 : Poll history, then download ─────────────
            SetStatus("Generating… (this may take a moment)");
            yield return PollAndDownload(promptId);
        }
    }

    // ── Step 2 : Poll /history/{prompt_id} ───────────────────────

    private IEnumerator PollAndDownload(string promptId)
    {
        const float pollInterval = 1.5f;
        const float timeout      = 60f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;

            using (var histReq = UnityWebRequest.Get(
                       $"{comfyUIUrl}/history/{promptId}"))
            {
                yield return histReq.SendWebRequest();

                if (histReq.result != UnityWebRequest.Result.Success)
                {
                    // Transient error — keep trying until timeout.
                    Debug.LogWarning("[ComfyUITest] Poll error (will retry): " +
                                     histReq.error);
                    continue;
                }

                JObject history;
                try   { history = JObject.Parse(histReq.downloadHandler.text); }
                catch { continue; }

                // ComfyUI returns {} while the job is still queued/running.
                var entry = history[promptId];
                if (entry == null) continue;

                var outputs = entry["outputs"];
                if (outputs == null) continue;

                // Walk all output nodes looking for an "images" array.
                string filename  = null;
                string subfolder = "";
                string type      = "output";

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

                if (string.IsNullOrEmpty(filename)) continue;

                // ── Step 3 : Download + display ──────────────────
                SetStatus("Downloading image…");
                yield return DownloadAndDisplay(filename, subfolder, type);
                yield break;    // done — exit the polling loop
            }
        }

        // Fell through the while loop — timed out.
        ReportError("Error: generation timed out. Check ComfyUI logs.");
    }

    // ── Step 3 : Fetch image and assign to RawImage ───────────────

    private IEnumerator DownloadAndDisplay(
        string filename, string subfolder, string type)
    {
        string url = $"{comfyUIUrl}/view" +
                     $"?filename={UnityWebRequest.EscapeURL(filename)}" +
                     $"&subfolder={UnityWebRequest.EscapeURL(subfolder)}" +
                     $"&type={UnityWebRequest.EscapeURL(type)}";

        using (var texReq = UnityWebRequestTexture.GetTexture(url))
        {
            yield return texReq.SendWebRequest();

            if (texReq.result != UnityWebRequest.Result.Success)
            {
                ReportError("Error: image download failed.\n" + texReq.error);
                Debug.LogError("[ComfyUITest] Image URL was: " + url);
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(texReq);

            if (outputImage != null)
                outputImage.texture = tex;

            SetStatus("Done!");
        }

        _generating = false;
        SetButtonInteractable(true);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log("[ComfyUITest] " + message);
    }

    private void ReportError(string message)
    {
        SetStatus(message);
        Debug.LogError("[ComfyUITest] " + message);
        _generating = false;
        SetButtonInteractable(true);
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (generateButton != null)
            generateButton.interactable = interactable;
    }
}
