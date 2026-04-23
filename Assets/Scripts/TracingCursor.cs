using UnityEngine;

/// <summary>
/// Player cursor that follows the mouse along the trail path.
/// Forward-only movement with keyboard-key gates at waypoints.
/// The cursor cannot advance past an uncleared gate — the player must press
/// the displayed key to proceed. Blue fill progress is driven by CursorT.
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
    private RectTransform _panelRect;
    private Camera        _uiCamera;
    private bool          _active;

    // Gate tracking
    private int   _nextGateIndex;   // index into _waypointTs of next uncleared gate
    private float _nextGateT;       // T of next gate, or 1.0 if all cleared

    /// <summary>Normalized progress of the cursor (0 = start, 1 = end).</summary>
    public float CursorT         { get; private set; }
    public bool  ReachedEnd      { get; private set; }
    public bool  IsWaitingForKey { get; private set; }

    /// <summary>Index of the next uncleared gate (equals waypoint count when all cleared).</summary>
    public int   CurrentGateIndex => _nextGateIndex;

    // ── Public API ────────────────────────────────────────────────

    public void Initialize(Vector2[] path, float[] cumulDist,
        float tolerance, float[] waypointTs, KeyCode[] waypointKeys,
        RectTransform panelRect, Camera uiCamera)
    {
        _path         = path;
        _cumulDist    = cumulDist;
        _toleranceSq  = tolerance * tolerance;
        _waypointTs   = waypointTs;
        _waypointKeys = waypointKeys;
        _panelRect    = panelRect;
        _uiCamera     = uiCamera;
        Reset();
    }

    public void SetActive(bool active) => _active = active;

    public void Reset()
    {
        CursorT         = 0f;
        ReachedEnd      = false;
        IsWaitingForKey = false;
        _nextGateIndex  = 0;
        _nextGateT      = (_waypointTs != null && _waypointTs.Length > 0)
                          ? _waypointTs[0] : 1f;
        _active         = false;
        UpdatePosition();
    }

    // ── Update ────────────────────────────────────────────────────

    private void Update()
    {
        if (!_active || _path == null || _cumulDist == null) return;

        // Convert mouse position to local panel coordinates
        Vector2 localMouse;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panelRect, Input.mousePosition, _uiCamera, out localMouse))
            return;

        // ── Waiting at a gate — check for correct key press ──────
        if (IsWaitingForKey)
        {
            if (_waypointKeys != null &&
                _nextGateIndex < _waypointKeys.Length &&
                Input.GetKeyDown(_waypointKeys[_nextGateIndex]))
            {
                // Gate cleared — advance to next gate
                IsWaitingForKey = false;
                _nextGateIndex++;
                _nextGateT = (_waypointTs != null && _nextGateIndex < _waypointTs.Length)
                             ? _waypointTs[_nextGateIndex] : 1f;
            }
            return; // Don't advance cursor while waiting for key
        }

        // ── Normal movement ──────────────────────────────────────
        float newT = RunePathData.ClosestTAhead(
            _path, _cumulDist, localMouse, CursorT, _toleranceSq);

        if (newT < 0f) return; // Mouse too far from path

        newT = Mathf.Max(CursorT, newT); // Forward-only

        // Cap at next uncleared gate (can't skip gates)
        if (_waypointTs != null && _nextGateIndex < _waypointTs.Length)
            newT = Mathf.Min(newT, _nextGateT);

        CursorT = newT;

        // Snap to gate and enter waiting state when close enough
        if (_waypointTs != null &&
            _nextGateIndex < _waypointTs.Length &&
            CursorT >= _nextGateT - 0.015f)
        {
            CursorT = _nextGateT;
            IsWaitingForKey = true;
        }

        // Check completion
        if (CursorT >= 0.98f)
            ReachedEnd = true;

        UpdatePosition();
    }

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
