using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main tracing minigame controller. Orchestrates 3 rounds of rune-tracing,
/// builds path visuals, spawns cursor + mist, tracks retries, computes grade.
/// Lives on a full-screen overlay panel inside the CraftingScene canvas.
/// </summary>
[DisallowMultipleComponent]
public class TracingMinigameUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("UI — Minigame Overlay")]
    [Tooltip("The full-screen panel that covers crafting UI during the minigame.")]
    public GameObject minigamePanel;

    [Tooltip("Top-center text showing 'Round N of 3'.")]
    public TMP_Text roundText;

    [Tooltip("Bottom instruction text.")]
    public TMP_Text instructionText;

    [Header("Tuning")]
    [Tooltip("Mist speed in normalized path-units per second.")]
    public float mistSpeed = 0.12f;

    [Tooltip("Seconds the player must hold at each waypoint.")]
    public float waypointHoldTime = 0.4f;

    [Tooltip("Max distance (pixels) the mouse can be from the path.")]
    public float pathTolerance = 40f;

    [Tooltip("Total retries across all rounds before forced F grade.")]
    public int maxTotalRetries = 9;

    [Header("Colors")]
    public Color pathColor     = new Color(0.31f, 0.93f, 0.97f, 1f);    // #4FECF7
    public Color pathGlowColor = new Color(0.31f, 0.93f, 0.97f, 0.25f);
    public Color mistColor     = new Color(0.9f, 0.15f, 0.15f, 0.7f);
    public Color cursorColor   = new Color(1f, 1f, 1f, 0.95f);
    public Color waypointColor = new Color(0.31f, 0.93f, 0.97f, 0.5f);

    // ── Private state ─────────────────────────────────────────────

    private Action<char>     _onComplete;
    private int              _totalRetries;
    private RectTransform    _panelRect;
    private Camera           _uiCamera;
    private readonly List<GameObject> _pathVisuals = new();

    // Dynamically created components per round
    private TracingCursor _cursor;
    private TracingMist   _mist;
    private Image         _channelFill;

    // ── Public entry point ────────────────────────────────────────

    /// <summary>
    /// Start the minigame. Calls <paramref name="onComplete"/> with the
    /// quality grade char ('A'–'F') when all rounds finish.
    /// </summary>
    public void Begin(Action<char> onComplete)
    {
        _onComplete   = onComplete;
        _totalRetries = 0;
        _panelRect    = minigamePanel.GetComponent<RectTransform>();

        // Find the UI camera (null for Screen Space – Overlay canvases)
        var canvas = minigamePanel.GetComponentInParent<Canvas>();
        _uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        minigamePanel.SetActive(true);
        StartCoroutine(RunAllRounds());
    }

    // ── Round orchestration ───────────────────────────────────────

    private IEnumerator RunAllRounds()
    {
        // Pick 3 unique rune paths
        RunePathData.PickThreeUnique(out Vector2[][] rawPaths, out float[][] waypointTs);

        for (int round = 0; round < 3; round++)
        {
            // Scale normalized path to panel pixel space
            Vector2 panelSize = _panelRect.rect.size;
            Vector2[] scaledPath = ScalePath(rawPaths[round], panelSize);
            float[] cumulDist = RunePathData.BuildCumulativeDistances(scaledPath);

            // Show round transition
            yield return ShowTransition(round + 1);

            // Run this round (may loop on retries)
            yield return RunSingleRound(scaledPath, cumulDist, waypointTs[round], round + 1);

            // Check forced F
            if (_totalRetries >= maxTotalRetries) break;

            // Clean up visuals before next round
            ClearRoundVisuals();
        }

        // Compute grade and finish
        char grade = ComputeGrade(_totalRetries);

        if (roundText != null)
            roundText.text = $"Crafting Quality: {grade}";
        if (instructionText != null)
            instructionText.text = grade == 'A'
                ? "Flawless ritual."
                : grade == 'F'
                    ? "The ritual faltered..."
                    : "Ritual complete.";

        yield return new WaitForSeconds(1.5f);

        ClearRoundVisuals();
        minigamePanel.SetActive(false);
        _onComplete?.Invoke(grade);
    }

    private IEnumerator ShowTransition(int roundNumber)
    {
        if (roundText != null)
            roundText.text = $"Round {roundNumber} of 3";
        if (instructionText != null)
            instructionText.text = "Trace the rune before the mist catches you.\nHold at waypoints to channel.";

        yield return new WaitForSeconds(1.2f);
    }

    private IEnumerator RunSingleRound(Vector2[] path, float[] cumulDist,
        float[] waypointTs, int roundNumber)
    {
        // Build visuals
        BuildPathVisuals(path);
        BuildWaypointVisuals(path, cumulDist, waypointTs);
        CreateCursorAndMist(path, cumulDist, waypointTs);

        while (true)
        {
            // Reset for this attempt
            _cursor.Reset();
            _mist.Reset();

            // Brief countdown
            if (instructionText != null)
                instructionText.text = "Get ready...";
            yield return new WaitForSeconds(0.8f);

            if (instructionText != null)
                instructionText.text = "Trace!";

            _cursor.SetActive(true);
            _mist.SetActive(true);

            // Run until success or failure
            bool caught = false;
            while (!_cursor.ReachedEnd)
            {
                if (_mist.CaughtPlayer(_cursor.CursorT))
                {
                    caught = true;
                    break;
                }
                yield return null;
            }

            _cursor.SetActive(false);
            _mist.SetActive(false);

            if (!caught)
            {
                // Round succeeded
                if (instructionText != null)
                    instructionText.text = "Rune sealed!";
                yield return new WaitForSeconds(0.6f);
                break;
            }

            // Failed — retry
            _totalRetries++;

            if (_totalRetries >= maxTotalRetries)
            {
                if (instructionText != null)
                    instructionText.text = "The ritual collapses...";
                yield return new WaitForSeconds(1f);
                break;
            }

            // Flash red feedback
            yield return FlashFeedback();

            if (roundText != null)
                roundText.text = $"Round {roundNumber} of 3  (retry)";
        }
    }

    // ── Path visual construction ──────────────────────────────────

    private void BuildPathVisuals(Vector2[] path)
    {
        // Glow pass (wider, lower alpha)
        BuildSegments(path, 14f, pathGlowColor);
        // Main path pass
        BuildSegments(path, 6f, pathColor);
    }

    private void BuildSegments(Vector2[] path, float thickness, Color color)
    {
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[i + 1];
            Vector2 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.5f) continue;

            var go = new GameObject("PathSeg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(len, thickness);

            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
    }

    private void BuildWaypointVisuals(Vector2[] path, float[] cumulDist, float[] waypointTs)
    {
        foreach (float wt in waypointTs)
        {
            Vector2 pos = RunePathData.SampleAt(path, cumulDist, wt);

            var go = new GameObject("Waypoint", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(24f, 24f);

            var img = go.GetComponent<Image>();
            img.color = waypointColor;
            img.raycastTarget = false;
        }
    }

    // ── Dynamic cursor + mist creation ────────────────────────────

    private void CreateCursorAndMist(Vector2[] path, float[] cumulDist, float[] waypointTs)
    {
        // Channel fill indicator
        var fillGo = new GameObject("ChannelFill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(fillGo);

        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = fillRt.anchorMax = fillRt.pivot = new Vector2(0.5f, 0.5f);
        fillRt.sizeDelta = new Vector2(36f, 36f);

        _channelFill = fillGo.GetComponent<Image>();
        _channelFill.color = waypointColor;
        _channelFill.type = Image.Type.Filled;
        _channelFill.fillMethod = Image.FillMethod.Radial360;
        _channelFill.fillAmount = 0f;
        _channelFill.raycastTarget = false;
        fillGo.SetActive(false);

        // Cursor
        var cursorGo = new GameObject("Cursor", typeof(RectTransform), typeof(Image), typeof(TracingCursor));
        cursorGo.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(cursorGo);

        var cursorRt = cursorGo.GetComponent<RectTransform>();
        cursorRt.anchorMin = cursorRt.anchorMax = cursorRt.pivot = new Vector2(0.5f, 0.5f);
        cursorRt.sizeDelta = new Vector2(18f, 18f);

        var cursorImg = cursorGo.GetComponent<Image>();
        cursorImg.color = cursorColor;
        cursorImg.raycastTarget = false;

        _cursor = cursorGo.GetComponent<TracingCursor>();
        _cursor.Initialize(path, cumulDist, pathTolerance, waypointHoldTime,
            waypointTs, _panelRect, _uiCamera, _channelFill);

        // Mist
        var mistGo = new GameObject("Mist", typeof(RectTransform), typeof(Image), typeof(TracingMist));
        mistGo.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(mistGo);

        var mistRt = mistGo.GetComponent<RectTransform>();
        mistRt.anchorMin = mistRt.anchorMax = mistRt.pivot = new Vector2(0.5f, 0.5f);
        mistRt.sizeDelta = new Vector2(60f, 60f);

        var mistImg = mistGo.GetComponent<Image>();
        mistImg.color = mistColor;
        mistImg.raycastTarget = false;

        _mist = mistGo.GetComponent<TracingMist>();
        _mist.Initialize(path, cumulDist, mistSpeed);
    }

    // ── Cleanup ───────────────────────────────────────────────────

    private void ClearRoundVisuals()
    {
        foreach (var go in _pathVisuals)
        {
            if (go != null) Destroy(go);
        }
        _pathVisuals.Clear();
        _cursor = null;
        _mist   = null;
        _channelFill = null;
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static Vector2[] ScalePath(Vector2[] normalized, Vector2 panelSize)
    {
        // Inset by 10% to keep paths away from panel edges
        float marginX = panelSize.x * 0.1f;
        float marginY = panelSize.y * 0.1f;
        float usableW = panelSize.x - 2f * marginX;
        float usableH = panelSize.y - 2f * marginY;

        // Offset so (0,0) maps to bottom-left of usable area relative to panel center
        float offsetX = -panelSize.x * 0.5f + marginX;
        float offsetY = -panelSize.y * 0.5f + marginY;

        var scaled = new Vector2[normalized.Length];
        for (int i = 0; i < normalized.Length; i++)
        {
            scaled[i] = new Vector2(
                normalized[i].x * usableW + offsetX,
                normalized[i].y * usableH + offsetY
            );
        }
        return scaled;
    }

    private static char ComputeGrade(int totalRetries)
    {
        return totalRetries switch
        {
            0 => 'A',
            1 => 'B',
            2 => 'C',
            3 => 'D',
            _ => 'F'
        };
    }

    private IEnumerator FlashFeedback()
    {
        // Brief red flash using a temporary overlay
        var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flashGo.transform.SetParent(minigamePanel.transform, false);

        var rt = flashGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = flashGo.GetComponent<Image>();
        img.color = new Color(1f, 0f, 0f, 0.3f);
        img.raycastTarget = false;

        yield return new WaitForSeconds(0.25f);
        Destroy(flashGo);
        yield return new WaitForSeconds(0.15f);
    }
}
