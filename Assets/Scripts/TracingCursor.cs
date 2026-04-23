using UnityEngine;

/// <summary>
/// Type of challenge presented by a gate on the rune path.
/// Tap = press key once. Hold = hold key for a target duration.
/// Accent = press key then flick the mouse in a specific direction off the path and back.
/// </summary>
public enum GateType { Tap, Hold, Accent }

/// <summary>
/// Cursor's current relationship to the nearest uncleared gate.
/// </summary>
public enum GateState
{
    None,               // free tracing
    Tapping,            // Tap gate — waiting for the displayed key
    Holding,            // Hold gate — waiting for key to be held for the target duration
    AccentFlicking,     // Accent gate — key pressed, waiting for the off-path flick
    AccentReturning     // Accent gate — flick registered, waiting for mouse to return to the path
}

/// <summary>
/// Player cursor that follows the mouse along the trail path.
/// Forward-only movement. Each gate carries one of three challenge types and the
/// cursor cannot advance past an uncleared gate. Tap/Hold/Accent have distinct
/// resolution rules; during all of them the red mist keeps advancing, so each
/// gate is a commitment of time the player won't get back.
/// </summary>
[DisallowMultipleComponent]
public class TracingCursor : MonoBehaviour
{
    // ── Runtime state (set via Initialize) ─────────────────────────

    private Vector2[]     _path;
    private float[]       _cumulDist;
    private float         _toleranceSq;
    private float[]       _waypointTs;
    private KeyCode[]     _waypointKeys;
    private GateType[]    _waypointTypes;
    private float[]       _holdDurations;     // seconds per Hold gate (0 for other types)
    private Vector2[]     _accentDirections;  // unit vector per Accent gate (zero for other types)
    private RectTransform _panelRect;
    private Camera        _uiCamera;
    private bool          _active;

    // Gate tracking
    private int   _nextGateIndex;   // index into _waypointTs of next uncleared gate
    private float _nextGateT;       // T of next gate, or 1.0 if all cleared

    // Hold-gate timing
    private float _holdElapsed;

    // Accent-gate flag
    private bool _accentKeyPressed;

    // Tunables
    private const float ACCENT_FLICK_MIN_DIST  = 60f;  // pixels mouse must travel off-path
    private const float ACCENT_DIR_TOLERANCE   = 0.866f; // cos(30°) — ±30° from required direction

    // ── Public state ──────────────────────────────────────────────

    /// <summary>Normalized progress of the cursor (0 = start, 1 = end).</summary>
    public float      CursorT          { get; private set; }
    public bool       ReachedEnd       { get; private set; }
    public GateState  CurrentGateState { get; private set; }

    /// <summary>0..1 fill value for the active Hold gate (valid only while CurrentGateState == Holding).</summary>
    public float      HoldProgress     { get; private set; }

    /// <summary>Index of the next uncleared gate (equals waypoint count when all cleared).</summary>
    public int        CurrentGateIndex => _nextGateIndex;

    /// <summary>True whenever the cursor is parked at a gate (any resolution state).</summary>
    public bool       IsAtGate         => CurrentGateState != GateState.None;

    // ── Public API ────────────────────────────────────────────────

    public void Initialize(
        Vector2[] path, float[] cumulDist, float tolerance,
        float[] waypointTs, KeyCode[] waypointKeys,
        GateType[] waypointTypes, float[] holdDurations, Vector2[] accentDirections,
        RectTransform panelRect, Camera uiCamera)
    {
        _path             = path;
        _cumulDist        = cumulDist;
        _toleranceSq      = tolerance * tolerance;
        _waypointTs       = waypointTs;
        _waypointKeys     = waypointKeys;
        _waypointTypes    = waypointTypes;
        _holdDurations    = holdDurations;
        _accentDirections = accentDirections;
        _panelRect        = panelRect;
        _uiCamera         = uiCamera;
        Reset();
    }

    public void SetActive(bool active) => _active = active;

    public void Reset()
    {
        CursorT           = 0f;
        ReachedEnd        = false;
        CurrentGateState  = GateState.None;
        HoldProgress      = 0f;
        _holdElapsed      = 0f;
        _accentKeyPressed = false;
        _nextGateIndex    = 0;
        _nextGateT        = (_waypointTs != null && _waypointTs.Length > 0)
                            ? _waypointTs[0] : 1f;
        _active           = false;
        UpdatePosition();
    }

    // ── Update ────────────────────────────────────────────────────

    private void Update()
    {
        if (!_active || _path == null || _cumulDist == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRect, Input.mousePosition, _uiCamera, out Vector2 localMouse))
            return;

        // ── Active gate: resolve it, don't advance cursor ────────
        if (CurrentGateState != GateState.None)
        {
            UpdateGateState(localMouse);
            return;
        }

        // ── Free-tracing movement ────────────────────────────────
        float newT = RunePathData.ClosestTAhead(
            _path, _cumulDist, localMouse, CursorT, _toleranceSq);
        if (newT < 0f) return;
        newT = Mathf.Max(CursorT, newT); // forward-only

        if (_waypointTs != null && _nextGateIndex < _waypointTs.Length)
            newT = Mathf.Min(newT, _nextGateT); // cap at next uncleared gate

        CursorT = newT;

        // Snap to gate and enter its state when close enough
        if (_waypointTs != null && _nextGateIndex < _waypointTs.Length &&
            CursorT >= _nextGateT - 0.015f)
        {
            CursorT = _nextGateT;
            EnterGateState();
        }

        if (CursorT >= 0.98f) ReachedEnd = true;
        UpdatePosition();
    }

    // ── Gate state machine ────────────────────────────────────────

    private void EnterGateState()
    {
        GateType type = (_waypointTypes != null && _nextGateIndex < _waypointTypes.Length)
            ? _waypointTypes[_nextGateIndex] : GateType.Tap;

        switch (type)
        {
            case GateType.Tap:
                CurrentGateState = GateState.Tapping;
                break;
            case GateType.Hold:
                CurrentGateState = GateState.Holding;
                _holdElapsed = 0f;
                HoldProgress = 0f;
                break;
            case GateType.Accent:
                CurrentGateState = GateState.AccentFlicking;
                _accentKeyPressed = false;
                break;
        }
    }

    private void UpdateGateState(Vector2 localMouse)
    {
        switch (CurrentGateState)
        {
            case GateState.Tapping:          HandleTap();                      break;
            case GateState.Holding:          HandleHold();                     break;
            case GateState.AccentFlicking:   HandleAccentFlick(localMouse);    break;
            case GateState.AccentReturning:  HandleAccentReturn(localMouse);   break;
        }
    }

    private void HandleTap()
    {
        if (_waypointKeys == null || _nextGateIndex >= _waypointKeys.Length) return;
        if (Input.GetKeyDown(_waypointKeys[_nextGateIndex])) AdvanceGate();
    }

    private void HandleHold()
    {
        if (_waypointKeys == null || _nextGateIndex >= _waypointKeys.Length) return;
        KeyCode key = _waypointKeys[_nextGateIndex];
        float target = (_holdDurations != null && _nextGateIndex < _holdDurations.Length)
            ? _holdDurations[_nextGateIndex] : 0.9f;
        if (target <= 0.0001f) target = 0.9f;

        if (Input.GetKey(key))
        {
            _holdElapsed += Time.deltaTime;
        }
        else if (_holdElapsed > 0f)
        {
            // Key released: if target reached, clear; otherwise fizzle (allow retry).
            if (_holdElapsed >= target)
            {
                AdvanceGate();
                return;
            }
            _holdElapsed = 0f;
        }
        HoldProgress = Mathf.Clamp01(_holdElapsed / target);
    }

    private void HandleAccentFlick(Vector2 localMouse)
    {
        if (_waypointKeys == null || _nextGateIndex >= _waypointKeys.Length) return;
        KeyCode key = _waypointKeys[_nextGateIndex];

        if (!_accentKeyPressed)
        {
            if (Input.GetKeyDown(key)) _accentKeyPressed = true;
            return;
        }

        Vector2 gatePos = RunePathData.SampleAt(_path, _cumulDist, _nextGateT);
        Vector2 offset  = localMouse - gatePos;
        if (offset.magnitude < ACCENT_FLICK_MIN_DIST) return;

        Vector2 required = (_accentDirections != null && _nextGateIndex < _accentDirections.Length)
            ? _accentDirections[_nextGateIndex] : Vector2.up;
        if (required.sqrMagnitude < 0.0001f) required = Vector2.up;

        if (Vector2.Dot(offset.normalized, required.normalized) >= ACCENT_DIR_TOLERANCE)
            CurrentGateState = GateState.AccentReturning;
    }

    private void HandleAccentReturn(Vector2 localMouse)
    {
        Vector2 gatePos = RunePathData.SampleAt(_path, _cumulDist, _nextGateT);
        if ((localMouse - gatePos).sqrMagnitude <= _toleranceSq)
            AdvanceGate();
    }

    private void AdvanceGate()
    {
        _nextGateIndex++;
        _nextGateT = (_waypointTs != null && _nextGateIndex < _waypointTs.Length)
                     ? _waypointTs[_nextGateIndex] : 1f;
        CurrentGateState  = GateState.None;
        HoldProgress      = 0f;
        _holdElapsed      = 0f;
        _accentKeyPressed = false;
    }

    // ── Position + mouse warp ─────────────────────────────────────

    private void UpdatePosition()
    {
        if (_path == null || _cumulDist == null) return;
        Vector2 pos = RunePathData.SampleAt(_path, _cumulDist, CursorT);
        ((RectTransform)transform).anchoredPosition = pos;
    }

    /// <summary>
    /// Warp the OS mouse cursor to the current in-game cursor position (path t=0 after Reset).
    /// Called at round start so the player is never stuck hunting for the trail's beginning.
    /// No-op when the Input System package is unavailable or no mouse is present.
    /// </summary>
    public void WarpOsMouseToStart()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;
        Vector3 world  = ((RectTransform)transform).position;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(_uiCamera, world);
        mouse.WarpCursorPosition(screen);
#endif
    }
}
