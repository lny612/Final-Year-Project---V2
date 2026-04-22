using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main tracing minigame controller. Orchestrates 3 rounds of path-tracing.
/// The player traces a trail with the mouse (fills blue) while a red fill advances
/// on a timer from the starting point. Keyboard-key gates at waypoints must be
/// pressed to proceed. Win = blue reaches end before red. Each win = +1 success.
/// Grade: 3 wins → A, 2 → B, 1 → C, 0 → F.
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
    [Tooltip("Red fill speed in normalized path-units per second.")]
    public float mistSpeed = 0.12f;

    [Tooltip("Max distance (pixels) the mouse can be from the path.")]
    public float pathTolerance = 40f;

    [Header("Colors")]
    public Color pathColor        = new Color(0.31f, 0.93f, 0.97f, 1f);    // cyan outline
    public Color pathGlowColor    = new Color(0.31f, 0.93f, 0.97f, 0.25f);
    public Color tracedColor      = new Color(0.2f, 0.5f, 1f, 0.85f);      // blue fill
    public Color mistColor        = new Color(0.9f, 0.15f, 0.15f, 0.7f);   // red fill
    public Color cursorColor      = new Color(1f, 1f, 1f, 0.95f);
    public Color gateColor        = new Color(1f, 0.85f, 0.3f, 0.8f);      // amber gate marker
    public Color gateClearedColor = new Color(0.3f, 1f, 0.3f, 0.6f);       // green cleared

    // ── Private state ─────────────────────────────────────────────

    private Action<char>     _onComplete;
    private int              _totalSuccesses;
    private RectTransform    _panelRect;
    private Camera           _uiCamera;
    private readonly List<GameObject> _pathVisuals = new();

    // Fill segments for red/blue trail visualization
    private struct FillSegInfo
    {
        public Image image;
        public float t; // normalized T at midpoint of this segment
    }
    private readonly List<FillSegInfo> _fillSegs = new();

    // Gate marker visuals
    private readonly List<Image>    _gateMarkers = new();
    private readonly List<TMP_Text> _gateLabels  = new();

    // Dynamically created components per round
    private TracingCursor _cursor;
    private TracingMist   _mist;

    // Key pool for gate randomization
    private static readonly KeyCode[] GATE_KEY_POOL =
    {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T,
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F,
    };

    // ── Public entry point ────────────────────────────────────────

    /// <summary>
    /// Start the minigame. Calls <paramref name="onComplete"/> with the
    /// quality grade char ('A', 'B', 'C', or 'F') when all rounds finish.
    /// </summary>
    public void Begin(Action<char> onComplete)
    {
        _onComplete     = onComplete;
        _totalSuccesses = 0;
        _panelRect      = minigamePanel.GetComponent<RectTransform>();

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

            // Run this round (single attempt — win or lose)
            yield return RunSingleRound(scaledPath, cumulDist, waypointTs[round], round + 1);

            // Clean up visuals before next round
            ClearRoundVisuals();
        }

        // Compute grade and finish
        char grade = ComputeGrade(_totalSuccesses);

        if (roundText != null)
            roundText.text = $"Crafting Quality: {grade}";
        if (instructionText != null)
            instructionText.text = grade switch
            {
                'A' => "Flawless ritual!",
                'F' => "The ritual faltered...",
                _   => "Ritual complete."
            };

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
            instructionText.text = "Trace the path! Press the shown key at each gate.";

        yield return new WaitForSeconds(1.2f);
    }

    private IEnumerator RunSingleRound(Vector2[] path, float[] cumulDist,
        float[] waypointTs, int roundNumber)
    {
        // Generate random gate keys and build all visuals
        KeyCode[] keys = GenerateGateKeys(waypointTs.Length);

        BuildPathVisuals(path);
        BuildFillSegments(path, cumulDist);
        // Cursor is created BEFORE gate visuals so gate markers (and their
        // key labels) render on top of the cursor. Otherwise, when the
        // cursor parks on an active gate the white dot hides the key letter.
        CreateCursorAndMist(path, cumulDist, waypointTs, keys);
        BuildGateVisuals(path, cumulDist, waypointTs, keys);

        // Brief countdown
        if (instructionText != null)
            instructionText.text = "Get ready...";
        yield return new WaitForSeconds(0.8f);

        if (instructionText != null)
            instructionText.text = "Trace!";

        _cursor.SetActive(true);
        _mist.SetActive(true);

        int lastClearedGate = -1;

        // Run until win or lose — no retries
        bool won = false;
        while (true)
        {
            // Update red/blue fill visualization
            UpdateFillColors(_cursor.CursorT, _mist.MistT);

            // Update gate visuals as cursor clears them
            int nextGate = _cursor.CurrentGateIndex;
            while (lastClearedGate < nextGate - 1)
            {
                lastClearedGate++;
                if (lastClearedGate < _gateMarkers.Count)
                    _gateMarkers[lastClearedGate].color = gateClearedColor;
            }

            // Pulse the active gate when waiting for key press
            if (_cursor.IsWaitingForKey && _cursor.CurrentGateIndex < _gateMarkers.Count)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
                var gc = gateColor;
                gc.a = pulse;
                _gateMarkers[_cursor.CurrentGateIndex].color = gc;
            }

            // Win: blue reached the end
            if (_cursor.ReachedEnd)
            {
                won = true;
                break;
            }

            // Lose: red filled the entire trail
            if (_mist.ReachedEnd)
                break;

            yield return null;
        }

        // Final fill update
        UpdateFillColors(_cursor.CursorT, _mist.MistT);

        _cursor.SetActive(false);
        _mist.SetActive(false);

        if (won)
        {
            _totalSuccesses++;
            if (instructionText != null)
                instructionText.text = "Rune sealed!";
        }
        else
        {
            if (instructionText != null)
                instructionText.text = "The mist consumed the path...";
        }

        yield return new WaitForSeconds(1f);
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
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(len, thickness);

            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
    }

    /// <summary>
    /// Build overlay segments that render red/blue fill on top of the base path.
    /// Each fill segment maps to a path segment and stores its normalized T position.
    /// </summary>
    private void BuildFillSegments(Vector2[] path, float[] cumulDist)
    {
        float totalLen = RunePathData.PathLength(cumulDist);
        if (totalLen < 0.001f) return;

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[i + 1];
            Vector2 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.5f) continue;

            var go = new GameObject("FillSeg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = a;
            rt.sizeDelta = new Vector2(len, 10f); // slightly wider than base for visibility

            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            var img = go.GetComponent<Image>();
            img.color = Color.clear; // start invisible
            img.raycastTarget = false;

            float midDist = (cumulDist[i] + cumulDist[i + 1]) * 0.5f;
            _fillSegs.Add(new FillSegInfo { image = img, t = midDist / totalLen });
        }
    }

    /// <summary>
    /// Build gate markers with TMP key labels at each waypoint position.
    /// </summary>
    private void BuildGateVisuals(Vector2[] path, float[] cumulDist,
        float[] waypointTs, KeyCode[] keys)
    {
        for (int i = 0; i < waypointTs.Length; i++)
        {
            Vector2 pos = RunePathData.SampleAt(path, cumulDist, waypointTs[i]);

            // Gate marker circle
            var go = new GameObject("Gate", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(36f, 36f);

            var img = go.GetComponent<Image>();
            img.color = gateColor;
            img.raycastTarget = false;
            _gateMarkers.Add(img);

            // Key label (child of gate marker)
            var labelGo = new GameObject("KeyLabel", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            _pathVisuals.Add(labelGo);

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = keys[i].ToString();
            label.fontSize = 28f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;
            _gateLabels.Add(label);
        }
    }

    // ── Dynamic cursor + mist creation ────────────────────────────

    private void CreateCursorAndMist(Vector2[] path, float[] cumulDist,
        float[] waypointTs, KeyCode[] keys)
    {
        // Cursor (visible dot on the path)
        var cursorGo = new GameObject("Cursor", typeof(RectTransform), typeof(Image),
            typeof(TracingCursor));
        cursorGo.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(cursorGo);

        var cursorRt = cursorGo.GetComponent<RectTransform>();
        cursorRt.anchorMin = cursorRt.anchorMax = cursorRt.pivot = new Vector2(0.5f, 0.5f);
        cursorRt.sizeDelta = new Vector2(18f, 18f);

        var cursorImg = cursorGo.GetComponent<Image>();
        cursorImg.color = cursorColor;
        cursorImg.raycastTarget = false;

        _cursor = cursorGo.GetComponent<TracingCursor>();
        _cursor.Initialize(path, cumulDist, pathTolerance, waypointTs, keys,
            _panelRect, _uiCamera);

        // Mist (timer only — red fill is rendered via fill segments, no visible dot)
        var mistGo = new GameObject("Mist", typeof(RectTransform), typeof(TracingMist));
        mistGo.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(mistGo);

        _mist = mistGo.GetComponent<TracingMist>();
        _mist.Initialize(mistSpeed);
    }

    // ── Fill visualization ────────────────────────────────────────

    /// <summary>
    /// Update each fill segment's color based on cursor (blue) and mist (red) progress.
    /// Blue takes priority — traced path stays blue even if red has also reached it.
    /// </summary>
    private void UpdateFillColors(float cursorT, float mistT)
    {
        foreach (var seg in _fillSegs)
        {
            if (seg.t <= cursorT)
                seg.image.color = tracedColor;   // player traced — blue
            else if (seg.t <= mistT)
                seg.image.color = mistColor;     // mist reached — red
            else
                seg.image.color = Color.clear;   // neither reached yet
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────

    private void ClearRoundVisuals()
    {
        foreach (var go in _pathVisuals)
        {
            if (go != null) Destroy(go);
        }
        _pathVisuals.Clear();
        _fillSegs.Clear();
        _gateMarkers.Clear();
        _gateLabels.Clear();
        _cursor = null;
        _mist   = null;
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

    /// <summary>
    /// Map cumulative round successes to a quality grade.
    /// 3 wins = A (1.0x), 2 = B (0.85x), 1 = C (0.7x), 0 = F (0.4x).
    /// </summary>
    private static char ComputeGrade(int successes)
    {
        return successes switch
        {
            3 => 'A',
            2 => 'B',
            1 => 'C',
            _ => 'F'
        };
    }

    private static KeyCode[] GenerateGateKeys(int count)
    {
        var keys = new KeyCode[count];
        for (int i = 0; i < count; i++)
            keys[i] = GATE_KEY_POOL[UnityEngine.Random.Range(0, GATE_KEY_POOL.Length)];
        return keys;
    }
}
