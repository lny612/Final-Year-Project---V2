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
    public Color gateColor        = new Color(1f, 0.85f, 0.3f, 0.85f);     // amber — Tap gate
    public Color holdGateColor    = new Color(0.35f, 0.9f, 1f, 0.9f);      // cyan — Hold gate
    public Color accentGateColor  = new Color(1f, 0.4f, 0.95f, 0.9f);      // magenta — Accent gate
    public Color gateClearedColor = new Color(0.3f, 1f, 0.3f, 0.6f);       // green cleared
    public Color holdPreviewColor = new Color(0.3f, 1f, 0.3f, 0.22f);      // green halo — Hold target zone

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
    private readonly List<Image>    _gateMarkers    = new();
    private readonly List<TMP_Text> _gateLabels     = new();
    private readonly List<Image>    _holdFills      = new(); // one entry per gate; null for non-Hold
    private readonly List<Color>    _gateBaseColors = new(); // base amber/cyan/magenta per gate, used by pulse

    // Dynamically created components per round
    private TracingCursor _cursor;
    private TracingMist   _mist;

    // Per-gate tuning constants
    private const float HOLD_GATE_DURATION  = 0.9f; // seconds the player must hold the key
    private const float HOLD_FILL_MAX_SIZE  = 32f;  // px at HoldProgress = 1

    // Key pool for gate randomization
    private static readonly KeyCode[] GATE_KEY_POOL =
    {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T,
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F,
    };

    // 8 compass directions used for accent-gate flicks.
    private static readonly Vector2[] COMPASS_8 =
    {
        new Vector2( 1f,       0f      ),  // E
        new Vector2( 0.7071f,  0.7071f ),  // NE
        new Vector2( 0f,       1f      ),  // N
        new Vector2(-0.7071f,  0.7071f ),  // NW
        new Vector2(-1f,       0f      ),  // W
        new Vector2(-0.7071f, -0.7071f ),  // SW
        new Vector2( 0f,      -1f      ),  // S
        new Vector2( 0.7071f, -0.7071f ),  // SE
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
        // Generate per-gate randomized data: keys, types (Tap/Hold/Accent),
        // hold durations, and accent directions.
        int gateCount = waypointTs.Length;
        KeyCode[]  keys          = GenerateGateKeys(gateCount);
        GateType[] types         = GenerateGateTypes(gateCount, roundNumber);
        float[]    holdDurations = GenerateHoldDurations(types);
        Vector2[]  accentDirs    = GenerateAccentDirections(types);

        BuildPathVisuals(path);
        BuildFillSegments(path, cumulDist);
        // Cursor is created BEFORE gate visuals so gate markers (and their
        // key labels) render on top of the cursor. Otherwise, when the
        // cursor parks on an active gate the white dot hides the key letter.
        CreateCursorAndMist(path, cumulDist, waypointTs, keys, types, holdDurations, accentDirs);
        BuildGateVisuals(path, cumulDist, waypointTs, keys, types, accentDirs);

        // Yield one frame so the Canvas computes the cursor's world position,
        // then warp the OS mouse so the player starts exactly on the trail.
        yield return null;
        _cursor.WarpOsMouseToStart();

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

            // Active-gate visual feedback — pulse for Tap/Accent, fill ring for Hold.
            if (_cursor.IsAtGate && _cursor.CurrentGateIndex < _gateMarkers.Count)
            {
                int gi = _cursor.CurrentGateIndex;
                if (_cursor.CurrentGateState == GateState.Holding && gi < _holdFills.Count && _holdFills[gi] != null)
                {
                    float size = HOLD_FILL_MAX_SIZE * _cursor.HoldProgress;
                    _holdFills[gi].rectTransform.sizeDelta = new Vector2(size, size);
                }
                else
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
                    var gc = _gateBaseColors[gi];
                    gc.a = pulse;
                    _gateMarkers[gi].color = gc;
                }
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
    /// Build gate markers with TMP key labels at each waypoint. Gate type drives
    /// the visual layering so each type is identifiable from a distance:
    ///   Tap    — amber   square + key letter.
    ///   Hold   — cyan    square + key letter + green halo behind (shows fill target)
    ///                  + inner green fill that grows with HoldProgress.
    ///   Accent — magenta square + key letter + directional arrow suffix
    ///                  + a protruding magenta line pointing in the required flick direction.
    /// </summary>
    private void BuildGateVisuals(Vector2[] path, float[] cumulDist,
        float[] waypointTs, KeyCode[] keys, GateType[] types, Vector2[] accentDirs)
    {
        for (int i = 0; i < waypointTs.Length; i++)
        {
            Vector2 pos = RunePathData.SampleAt(path, cumulDist, waypointTs[i]);
            GateType type = types[i];
            Color baseColor = GateBaseColor(type);

            // 1. Pre-decoration (rendered BEHIND the gate marker)
            if (type == GateType.Hold)    BuildHoldPreviewHalo(pos);
            if (type == GateType.Accent)  BuildAccentArrowLine(pos, accentDirs[i]);

            // 2. Outer gate marker square
            var go = new GameObject("Gate", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(36f, 36f);

            var img = go.GetComponent<Image>();
            img.color = baseColor;
            img.raycastTarget = false;
            _gateMarkers.Add(img);
            _gateBaseColors.Add(baseColor);

            // 3. Hold-fill overlay (inner green square, grows as Hold progresses).
            Image holdFill = null;
            if (type == GateType.Hold)
            {
                var fillGo = new GameObject("HoldFill", typeof(RectTransform), typeof(Image));
                fillGo.transform.SetParent(go.transform, false);
                fillGo.transform.SetSiblingIndex(0);
                _pathVisuals.Add(fillGo);

                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = fillRt.anchorMax = fillRt.pivot = new Vector2(0.5f, 0.5f);
                fillRt.sizeDelta = Vector2.zero;

                holdFill = fillGo.GetComponent<Image>();
                holdFill.color = new Color(0.3f, 1f, 0.3f, 0.85f);
                holdFill.raycastTarget = false;
            }
            _holdFills.Add(holdFill);

            // 4. Key label (+ accent arrow suffix)
            var labelGo = new GameObject("KeyLabel", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            _pathVisuals.Add(labelGo);

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            string text = keys[i].ToString();
            if (type == GateType.Accent) text += DirectionToArrow(accentDirs[i]);
            label.text = text;
            label.fontSize = 26f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.black;
            label.raycastTarget = false;
            _gateLabels.Add(label);
        }
    }

    /// <summary>Per-type base color used for the gate marker and pulse animation.</summary>
    private Color GateBaseColor(GateType type) => type switch
    {
        GateType.Hold   => holdGateColor,
        GateType.Accent => accentGateColor,
        _               => gateColor,
    };

    /// <summary>
    /// Translucent green square behind a Hold gate showing the "fill-me" target zone.
    /// Gives the player an instant visual cue that this gate requires holding before
    /// they even reach it.
    /// </summary>
    private void BuildHoldPreviewHalo(Vector2 gatePos)
    {
        var go = new GameObject("HoldHalo", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(go);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = gatePos;
        rt.sizeDelta = new Vector2(52f, 52f);

        var img = go.GetComponent<Image>();
        img.color = holdPreviewColor;
        img.raycastTarget = false;
    }

    /// <summary>
    /// Magenta line extending from an Accent gate outward in the required flick
    /// direction. Tells the player — at a glance — which way to flick the mouse
    /// before they even reach the gate.
    /// </summary>
    private void BuildAccentArrowLine(Vector2 gatePos, Vector2 dir)
    {
        Vector2 n = dir.normalized;
        if (n.sqrMagnitude < 0.0001f) n = Vector2.up;

        const float length    = 50f;
        const float thickness = 6f;
        const float gateHalf  = 18f; // outer marker is 36×36

        // Anchor the rectangle at the gate edge so it grows outward.
        Vector2 anchor = gatePos + n * gateHalf;

        var go = new GameObject("AccentLine", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(go);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f); // left edge at anchor, extends right in local-space
        rt.anchoredPosition = anchor;
        rt.sizeDelta = new Vector2(length, thickness);
        float angle = Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        var img = go.GetComponent<Image>();
        img.color = accentGateColor;
        img.raycastTarget = false;

        // Arrow head — a slightly thicker square at the tip, offset perpendicular
        // to the line to form a chevron feel without needing sprites.
        var headGo = new GameObject("AccentHead", typeof(RectTransform), typeof(Image));
        headGo.transform.SetParent(minigamePanel.transform, false);
        _pathVisuals.Add(headGo);

        var headRt = headGo.GetComponent<RectTransform>();
        headRt.anchorMin = headRt.anchorMax = headRt.pivot = new Vector2(0.5f, 0.5f);
        headRt.anchoredPosition = gatePos + n * (gateHalf + length);
        headRt.sizeDelta = new Vector2(14f, 14f);
        headRt.localRotation = Quaternion.Euler(0, 0, angle + 45f);

        var headImg = headGo.GetComponent<Image>();
        headImg.color = accentGateColor;
        headImg.raycastTarget = false;
    }

    // ── Dynamic cursor + mist creation ────────────────────────────

    private void CreateCursorAndMist(Vector2[] path, float[] cumulDist,
        float[] waypointTs, KeyCode[] keys,
        GateType[] types, float[] holdDurations, Vector2[] accentDirs)
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
            types, holdDurations, accentDirs,
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
        _holdFills.Clear();
        _gateBaseColors.Clear();
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

    /// <summary>
    /// Fixed escalation across the 3 rounds:
    ///   R1 — 5 Tap gates (teaches the baseline tracing + key mechanic).
    ///   R2 — one random gate becomes Hold (introduces time-pressure holds).
    ///   R3 — one random gate becomes Hold + a different gate becomes Accent
    ///        (adds directional flick requirement — full conducting).
    /// </summary>
    private static GateType[] GenerateGateTypes(int count, int roundNumber)
    {
        var types = new GateType[count];
        for (int i = 0; i < count; i++) types[i] = GateType.Tap;
        if (count == 0) return types;

        if (roundNumber >= 2)
        {
            int holdIdx = UnityEngine.Random.Range(0, count);
            types[holdIdx] = GateType.Hold;
        }
        if (roundNumber >= 3)
        {
            // Pick a different index, still Tap, to become Accent.
            for (int attempts = 0; attempts < 10; attempts++)
            {
                int idx = UnityEngine.Random.Range(0, count);
                if (types[idx] == GateType.Tap)
                {
                    types[idx] = GateType.Accent;
                    break;
                }
            }
        }
        return types;
    }

    private static float[] GenerateHoldDurations(GateType[] types)
    {
        var durations = new float[types.Length];
        for (int i = 0; i < types.Length; i++)
            durations[i] = (types[i] == GateType.Hold) ? HOLD_GATE_DURATION : 0f;
        return durations;
    }

    private static Vector2[] GenerateAccentDirections(GateType[] types)
    {
        var dirs = new Vector2[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == GateType.Accent)
                dirs[i] = COMPASS_8[UnityEngine.Random.Range(0, COMPASS_8.Length)];
        }
        return dirs;
    }

    /// <summary>Convert a unit direction vector to the matching Unicode arrow character (with leading space).</summary>
    private static string DirectionToArrow(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return string.Empty;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        int octant = Mathf.RoundToInt(((angle + 360f) % 360f) / 45f) % 8;
        return octant switch
        {
            0 => " →",  // →  E
            1 => " ↗",  // ↗  NE
            2 => " ↑",  // ↑  N
            3 => " ↖",  // ↖  NW
            4 => " ←",  // ←  W
            5 => " ↙",  // ↙  SW
            6 => " ↓",  // ↓  S
            7 => " ↘",  // ↘  SE
            _ => " →"
        };
    }
}
