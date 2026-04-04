using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player cursor that follows the mouse along the rune path.
/// Handles forward-only movement, path tolerance, and waypoint channeling.
/// </summary>
[DisallowMultipleComponent]
public class TracingCursor : MonoBehaviour
{
    // ── Runtime state (set via Initialize) ─────────────────────────

    private Vector2[]    _path;
    private float[]      _cumulDist;
    private float        _toleranceSq;
    private float        _waypointHoldTime;
    private float[]      _waypointTs;
    private RectTransform _panelRect;
    private Camera       _uiCamera;
    private bool         _active;

    // Channeling
    private int   _currentWaypointIdx = -1;   // index into _waypointTs, -1 = none
    private float _channelProgress;            // 0 → _waypointHoldTime

    // Visual feedback
    private Image _channelFill;                // radial fill image for waypoint hold

    public float CursorT       { get; private set; }
    public bool  ReachedEnd    { get; private set; }
    public bool  IsChanneling  { get; private set; }

    // ── Public API ────────────────────────────────────────────────

    public void Initialize(Vector2[] path, float[] cumulDist,
        float tolerance, float waypointHoldTime, float[] waypointTs,
        RectTransform panelRect, Camera uiCamera, Image channelFillImage)
    {
        _path             = path;
        _cumulDist        = cumulDist;
        _toleranceSq      = tolerance * tolerance;
        _waypointHoldTime = waypointHoldTime;
        _waypointTs       = waypointTs;
        _panelRect        = panelRect;
        _uiCamera         = uiCamera;
        _channelFill      = channelFillImage;
        Reset();
    }

    public void SetActive(bool active)
    {
        _active = active;
    }

    public void Reset()
    {
        CursorT             = 0f;
        ReachedEnd          = false;
        IsChanneling        = false;
        _currentWaypointIdx = -1;
        _channelProgress    = 0f;
        _active             = false;

        if (_channelFill != null)
        {
            _channelFill.fillAmount = 0f;
            _channelFill.gameObject.SetActive(false);
        }

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

        // ── Channeling state ──────────────────────────────────────
        if (IsChanneling)
        {
            if (Input.GetMouseButton(0))
            {
                _channelProgress += Time.deltaTime;
                if (_channelFill != null)
                    _channelFill.fillAmount = _channelProgress / _waypointHoldTime;

                if (_channelProgress >= _waypointHoldTime)
                {
                    // Waypoint cleared
                    IsChanneling = false;
                    _currentWaypointIdx = -1;
                    _channelProgress = 0f;
                    if (_channelFill != null)
                    {
                        _channelFill.fillAmount = 0f;
                        _channelFill.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // Released early — reset channel progress
                _channelProgress = 0f;
                if (_channelFill != null)
                    _channelFill.fillAmount = 0f;
            }

            // Don't advance cursor while channeling
            return;
        }

        // ── Normal movement ───────────────────────────────────────
        float newT = RunePathData.ClosestTAhead(
            _path, _cumulDist, localMouse, CursorT, _toleranceSq);

        if (newT < 0f)
        {
            // Mouse too far from path — don't advance
            return;
        }

        // Forward-only
        newT = Mathf.Max(CursorT, newT);
        CursorT = newT;

        // Check if entering a waypoint zone
        if (_waypointTs != null)
        {
            for (int i = 0; i < _waypointTs.Length; i++)
            {
                float wpT = _waypointTs[i];
                // Only trigger waypoints ahead of current position, within a small zone
                if (CursorT >= wpT - 0.02f && CursorT <= wpT + 0.02f)
                {
                    // Check if we already passed this waypoint
                    if (i <= _currentWaypointIdx) continue;

                    // Enter channeling
                    IsChanneling = true;
                    _currentWaypointIdx = i;
                    _channelProgress = 0f;
                    CursorT = wpT; // snap to waypoint

                    if (_channelFill != null)
                    {
                        _channelFill.gameObject.SetActive(true);
                        _channelFill.fillAmount = 0f;
                        // Position fill indicator at waypoint
                        Vector2 wpPos = RunePathData.SampleAt(_path, _cumulDist, wpT);
                        ((RectTransform)_channelFill.transform).anchoredPosition = wpPos;
                    }
                    break;
                }
            }
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
}
