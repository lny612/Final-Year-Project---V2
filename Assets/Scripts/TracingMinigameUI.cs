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

    [Tooltip("Top-center text showing 'Round N of 3' (legacy UGUI fallback).")]
    public TMP_Text roundText;

    [Tooltip("Bottom instruction text (legacy UGUI fallback).")]
    public TMP_Text instructionText;

    [Tooltip("Optional UI Toolkit chrome controller for the styled REVELIO frame.\n" +
            "If assigned, the round + instruction text are mirrored onto the UXML labels.\n" +
            "TODO-EDITOR: Drag MinigameUIDocument here (see MinigamePanelController.cs for setup).")]
    public MinigamePanelController chromeController;

    [Header("Round Incantations")]
    [Tooltip("Spell name shown on the title plaque for round 1 — calling/awakening the soul-shards in the materials.")]
    public string round1SpellName = "ADVENI";
    [Tooltip("Spell name shown on the title plaque for round 2 — opening the materials' minds so they blend with the wand.")]
    public string round2SpellName = "PATEFACIO";
    [Tooltip("Spell name shown on the title plaque for round 3 — taming and directing the now-open power.")]
    public string round3SpellName = "MANSUETO";

    [Header("Tuning")]
    [Tooltip("Red fill speed in normalized path-units per second.")]
    public float mistSpeed = 0.12f;

    [Tooltip("Max distance (pixels) the mouse can be from the path.")]
    public float pathTolerance = 40f;

    [Header("Colors — palette tuned to mini game reference.png")]
    public Color pathColor        = new Color(0.90f, 0.78f, 0.45f, 1.00f);  // gold rune outline
    public Color pathGlowColor    = new Color(0.90f, 0.78f, 0.45f, 0.30f);  // gold halo
    public Color tracedColor      = new Color(0.47f, 0.71f, 1.00f, 0.95f);  // cool rune-blue trail
    public Color mistColor        = new Color(0.85f, 0.25f, 0.20f, 0.85f);  // ember red mist
    public Color cursorColor      = new Color(1.00f, 0.96f, 0.85f, 1.00f);  // warm white cursor
    public Color gateColor        = new Color(0.95f, 0.78f, 0.40f, 0.95f);  // amber-gold diamond — Tap
    public Color holdGateColor    = new Color(0.55f, 0.85f, 1.00f, 0.95f);  // ice blue diamond — Hold
    public Color accentGateColor  = new Color(0.95f, 0.50f, 0.85f, 0.95f);  // rose magenta diamond — Accent
    public Color gateClearedColor = new Color(0.45f, 0.95f, 0.55f, 0.65f);  // bright green cleared
    public Color holdPreviewColor = new Color(0.45f, 0.95f, 0.55f, 0.22f);  // green halo — Hold target zone

    [Header("Sprites (optional — leave null to keep the procedural rectangles)")]
    [Tooltip("Sprite drawn along each main-path segment (gold filigree). White-core art on pure black tints cleanly via Image.color.")]
    public Sprite trailStripeSprite;
    [Tooltip("Pixel padding cropped from the source texture before drawing (left, top, right, bottom). Use this when the stripe art doesn't fill the full sprite — e.g. Trail 2.png has 27 px of horizontal padding and 237 px of vertical padding.")]
    public Vector4 trailStripeTrim = new Vector4(27f, 237f, 27f, 237f);
    [Tooltip("Backplate sprite for each gate (TMP key letter is overlaid on top). Tinted by gate type at runtime.")]
    public Sprite gateMedallionSprite;
    [Tooltip("Sprite for the player cursor wisp.")]
    public Sprite cursorWispSprite;
    [Tooltip("Diamond plaque dropped at the path's last point. If null, no endpoint visual is drawn.")]
    public Sprite endpointPlaqueSprite;
    [Tooltip("Pixel size (square) of the endpoint plaque visual.")]
    public float  endpointPlaqueSize = 64f;

    [Header("Gate look")]
    [Tooltip("Pixel size (square) of each gate medallion. Default 48.")]
    public float gateSize = 48f;
    [Tooltip("Font used for the key-letter glyph inside each gate. Falls back to TMP default if null.")]
    public TMP_FontAsset gateFont;
    [Tooltip("Color of the key-letter glyph. Bright cream / gold reads well on the dark medallion.")]
    public Color gateLabelColor = new Color(1.00f, 0.95f, 0.70f, 1.00f);
    [Tooltip("Font size of the gate key letter.")]
    public float gateLabelFontSize = 32f;

    [Header("Cursor look")]
    [Tooltip("Pixel size (square) of the player cursor wisp.")]
    public float cursorSize = 36f;

    [Header("Mist VFX (cloudy puffs riding the time-mist head)")]
    [Tooltip("Sprite used for each mist puff. Use a transparent-background sprite so puffs don't render as black squares.")]
    public Sprite mistPuffSprite;
    [Tooltip("Puffs spawned per second while the round is active.")]
    public float  mistPuffRate    = 38f;
    [Tooltip("Lifetime of each puff in seconds — fades from full opacity to 0 over this duration.")]
    public float  mistPuffLife    = 0.7f;
    [Tooltip("Starting puff radius in pixels.")]
    public float  mistPuffStart   = 6f;
    [Tooltip("Final puff radius in pixels (puffs grow as they fade).")]
    public float  mistPuffEnd     = 22f;
    [Tooltip("Random scatter radius (px) around the mist's path point — keeps the cloud feeling diffuse.")]
    public float  mistPuffScatter = 22f;
    [Tooltip("Starting alpha of each puff (0..1). Lower values feel more diffuse.")]
    [Range(0f, 1f)]
    public float  mistPuffAlpha   = 0.55f;

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

    // Snapshot of the active round's path for VFX sampling.
    private Vector2[] _activePath;
    private float[]   _activeCumul;
    private float     _puffAccumulator;

    // Cached trim of trailStripeSprite — built lazily on first access so the
    // GC hit happens off the round-start frame.
    private Sprite _cachedTrailSprite;

    // Procedurally-generated soft circle used as the Hold-gate fill so the
    // green overlay grows as a circle instead of a square. Built once on
    // first hold-gate creation and reused for the rest of the run.
    private Sprite _cachedCircleSprite;

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
        if (chromeController != null) chromeController.SetVisible(true);
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

            // Swap the title-plaque incantation for this round's ritual step.
            if (chromeController != null)
                chromeController.SetSpellName(GetRoundSpellName(round + 1));

            // Show round transition
            yield return ShowTransition(round + 1);

            // Run this round (single attempt — win or lose)
            yield return RunSingleRound(scaledPath, cumulDist, waypointTs[round], round + 1);

            // Clean up visuals before next round
            ClearRoundVisuals();
        }

        // Compute grade and finish
        char grade = ComputeGrade(_totalSuccesses);

        SetRoundLabel($"Crafting Quality: {grade}");
        SetInstructionLabel(grade switch
        {
            'A' => "Flawless ritual!",
            'F' => "The ritual faltered...",
            _   => "Ritual complete."
        });

        yield return new WaitForSeconds(1.5f);

        ClearRoundVisuals();
        minigamePanel.SetActive(false);
        if (chromeController != null) chromeController.SetVisible(false);
        _onComplete?.Invoke(grade);
    }

    private IEnumerator ShowTransition(int roundNumber)
    {
        SetRoundLabel($"Round {roundNumber} of 3");
        SetInstructionLabel("Trace the path! Press the shown key at each gate.");

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

        // Stash the active path so per-frame VFX (mist puffs) can sample it.
        _activePath  = path;
        _activeCumul = cumulDist;
        _puffAccumulator = 0f;

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
        SetInstructionLabel("Get ready...");
        yield return new WaitForSeconds(0.8f);

        SetInstructionLabel("Trace!");

        _cursor.SetActive(true);
        _mist.SetActive(true);
        AudioManager.Instance?.StartTracingLoop();

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
                AudioManager.Instance?.PlayGateClear();
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

            // Cloudy puff VFX riding the mist's head along the path.
            EmitMistPuffs(Time.deltaTime);

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
        AudioManager.Instance?.StopTracingLoop();

        if (won)
        {
            _totalSuccesses++;
            SetInstructionLabel("Rune sealed!");
        }
        else
        {
            SetInstructionLabel("The mist consumed the path...");
        }

        yield return new WaitForSeconds(1f);
    }

    /// <summary>Update both the legacy UGUI roundText and the UIToolkit chrome label (if wired).</summary>
    private void SetRoundLabel(string text)
    {
        if (roundText != null) roundText.text = text;
        if (chromeController != null) chromeController.SetRoundText(text);
    }

    /// <summary>Update both the legacy UGUI instructionText and the UIToolkit chrome label (if wired).</summary>
    private void SetInstructionLabel(string text)
    {
        if (instructionText != null) instructionText.text = text;
        if (chromeController != null) chromeController.SetInstructionText(text);
    }

    /// <summary>Return the title-plaque incantation for the given 1-based round number.</summary>
    private string GetRoundSpellName(int roundNumber) => roundNumber switch
    {
        1 => round1SpellName,
        2 => round2SpellName,
        3 => round3SpellName,
        _ => "REVELIO"
    };

    // ── Path visual construction ──────────────────────────────────

    private void BuildPathVisuals(Vector2[] path)
    {
        // Glow pass (wider, lower alpha) — diffuse halo, plain rect.
        BuildSegments(path, 14f, pathGlowColor, useTrailSprite: false);
        // Main path pass — gold filigree texture lives here.
        BuildSegments(path, 6f, pathColor, useTrailSprite: true);
    }

    private void BuildSegments(Vector2[] path, float thickness, Color color, bool useTrailSprite = false)
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

            if (useTrailSprite)
            {
                var trail = GetEffectiveTrailSprite();
                if (trail != null)
                {
                    img.sprite         = trail;
                    img.type           = Image.Type.Simple;
                    img.preserveAspect = false;
                }
            }
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
            img.color = Color.clear; // start invisible — gets tinted blue / red over time
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

            // 2. Outer gate marker — rotated 45° to read as a diamond (matches reference art).
            var go = new GameObject("Gate", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(gateSize, gateSize);
            // Only rotate to a diamond when we have no sprite — sprites are
            // designed upright and a circular medallion is rotation-invariant.
            rt.localRotation = gateMedallionSprite != null
                ? Quaternion.identity
                : Quaternion.Euler(0, 0, 45f);

            var img = go.GetComponent<Image>();
            img.color = baseColor;
            img.raycastTarget = false;
            if (gateMedallionSprite != null) img.sprite = gateMedallionSprite;
            _gateMarkers.Add(img);
            _gateBaseColors.Add(baseColor);

            // 3. Hold-fill overlay (inner green CIRCLE, grows as Hold progresses).
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
                holdFill.sprite = GetCircleSprite();
                holdFill.type   = Image.Type.Simple;
                holdFill.preserveAspect = true;
            }
            _holdFills.Add(holdFill);

            // 4. Key label (+ accent arrow suffix). Counter-rotate by -45° so the
            // letter stays upright inside the diamond-rotated parent gate.
            var labelGo = new GameObject("KeyLabel", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            _pathVisuals.Add(labelGo);

            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            // Counter-rotate only if the gate parent itself was rotated.
            labelRt.localRotation = gateMedallionSprite != null
                ? Quaternion.identity
                : Quaternion.Euler(0, 0, -45f);

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (gateFont != null) label.font = gateFont;
            // Accent gates get the chevron sprite + magenta line *on the gate
            // medallion* — the keyboard letter alone is shown in the label,
            // no Unicode arrow suffix.
            label.text = keys[i].ToString();
            label.fontSize = gateLabelFontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = gateLabelColor;
            label.raycastTarget = false;
            // Subtle outline so the bright glyph reads on the medallion.
            label.outlineColor = new Color32(46, 32, 22, 220);
            label.outlineWidth = 0.18f;
            _gateLabels.Add(label);
        }

        // Endpoint plaque — drawn near the path's last point if a sprite is wired.
        // Sits ON TOP of the path/gates so it reads as the destination marker.
        // Pulled BACK along the trail tangent so the plaque overlaps the trail
        // end by ~50% of its size — without this the plaque sits in empty space
        // a noticeable distance past where the trail visually ends.
        if (endpointPlaqueSprite != null && path.Length > 0)
        {
            Vector2 endPos = path[path.Length - 1];
            if (path.Length >= 2)
            {
                Vector2 tangent = (endPos - path[path.Length - 2]);
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    endPos -= tangent.normalized * (endpointPlaqueSize * 0.5f);
                }
            }

            var go = new GameObject("EndpointPlaque", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(minigamePanel.transform, false);
            go.transform.SetAsLastSibling(); // on top
            _pathVisuals.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = endPos;
            rt.sizeDelta        = new Vector2(endpointPlaqueSize, endpointPlaqueSize);
            // Sprite art is already a diamond — no rotation needed.
            rt.localRotation    = Quaternion.identity;

            var img = go.GetComponent<Image>();
            img.sprite        = endpointPlaqueSprite;
            img.color         = Color.white;
            img.raycastTarget = false;
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
        rt.localRotation = Quaternion.Euler(0, 0, 45f); // match diamond gate

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
        cursorRt.sizeDelta = new Vector2(cursorSize, cursorSize);

        var cursorImg = cursorGo.GetComponent<Image>();
        cursorImg.color = cursorColor;
        cursorImg.raycastTarget = false;
        if (cursorWispSprite != null) cursorImg.sprite = cursorWispSprite;
        // Always render on top of gates / endpoint so the player can see where they are.
        cursorGo.transform.SetAsLastSibling();

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
        _activePath  = null;
        _activeCumul = null;
    }

    // ── Trail-sprite cropping ─────────────────────────────────────

    /// <summary>
    /// Returns the effective trail sprite, applying <see cref="trailStripeTrim"/>
    /// (left, top, right, bottom — pixels) by carving a runtime sub-sprite out
    /// of the source texture. Cached so the GC hit happens only once.
    /// </summary>
    private Sprite GetEffectiveTrailSprite()
    {
        if (trailStripeSprite == null) return null;

        bool hasTrim = trailStripeTrim.x > 0f || trailStripeTrim.y > 0f
                    || trailStripeTrim.z > 0f || trailStripeTrim.w > 0f;
        if (!hasTrim) return trailStripeSprite;

        if (_cachedTrailSprite != null
            && _cachedTrailSprite.texture == trailStripeSprite.texture)
            return _cachedTrailSprite;

        var tex   = trailStripeSprite.texture;
        float left   = trailStripeTrim.x;
        float top    = trailStripeTrim.y;
        float right  = trailStripeTrim.z;
        float bottom = trailStripeTrim.w;
        var rect = new Rect(
            left,
            bottom,
            tex.width  - left - right,
            tex.height - top  - bottom);

        if (rect.width <= 0f || rect.height <= 0f) return trailStripeSprite;

        _cachedTrailSprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
        _cachedTrailSprite.name = trailStripeSprite.name + "_trimmed";
        return _cachedTrailSprite;
    }

    // ── Mist puff VFX ─────────────────────────────────────────────

    /// <summary>
    /// Emit cloudy puffs at the mist's current path point. Called every frame
    /// from <see cref="RunSingleRound"/>; uses an accumulator to honour the
    /// configured rate independent of frame rate.
    /// </summary>
    private void EmitMistPuffs(float dt)
    {
        if (_mist == null || _activePath == null || _activeCumul == null) return;
        if (mistPuffRate <= 0f) return;

        _puffAccumulator += dt * mistPuffRate;
        while (_puffAccumulator >= 1f)
        {
            _puffAccumulator -= 1f;
            float t = Mathf.Clamp01(_mist.MistT);
            Vector2 pos = RunePathData.SampleAt(_activePath, _activeCumul, t);
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * mistPuffScatter;
            SpawnMistPuff(pos + jitter);
        }
    }

    private void SpawnMistPuff(Vector2 anchoredPos)
    {
        if (minigamePanel == null) return;

        var go = new GameObject("MistPuff", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(minigamePanel.transform, false);
        go.transform.SetAsLastSibling();
        _pathVisuals.Add(go);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(mistPuffStart, mistPuffStart);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        Sprite puff = mistPuffSprite != null ? mistPuffSprite : cursorWispSprite;
        if (puff != null) img.sprite = puff;
        Color tint = mistColor;
        tint.a = mistPuffAlpha;
        img.color = tint;

        StartCoroutine(AnimateMistPuff(go, rt, img));
    }

    private IEnumerator AnimateMistPuff(GameObject go, RectTransform rt, Image img)
    {
        float life = Mathf.Max(0.05f, mistPuffLife);
        float t    = 0f;
        Color baseColor = img.color;
        // Slight upward + outward drift.
        Vector2 drift = new Vector2(
            UnityEngine.Random.Range(-12f, 12f),
            UnityEngine.Random.Range( 12f, 28f));
        Vector2 startPos = rt.anchoredPosition;

        while (t < life && go != null && img != null)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / life);
            float size = Mathf.Lerp(mistPuffStart, mistPuffEnd, u);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = startPos + drift * u;

            var c = baseColor;
            c.a = baseColor.a * (1f - u);
            img.color = c;

            yield return null;
        }

        if (go != null)
        {
            _pathVisuals.Remove(go);
            Destroy(go);
        }
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

    /// <summary>
    /// Build (and cache) a soft circular sprite used by the Hold-gate fill so
    /// it grows as a circle rather than a square. The radial alpha falloff
    /// gives the fill a subtle glow at its edge.
    /// </summary>
    private Sprite GetCircleSprite()
    {
        if (_cachedCircleSprite != null) return _cachedCircleSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.DontSave,
        };
        var pixels = new Color32[size * size];
        float radius = size * 0.5f;
        float radiusSqr = radius * radius;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - radius + 0.5f;
            float dy = y - radius + 0.5f;
            float dSqr = dx * dx + dy * dy;
            byte a;
            if (dSqr >= radiusSqr) { a = 0; }
            else
            {
                // 1.0 at center, smooth falloff at the rim for a slight glow.
                float t = Mathf.Sqrt(dSqr) / radius;          // 0..1
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.85f, 1f, t));
                a = (byte)Mathf.RoundToInt(alpha * 255f);
            }
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);

        _cachedCircleSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        _cachedCircleSprite.hideFlags = HideFlags.DontSave;
        return _cachedCircleSprite;
    }

}
