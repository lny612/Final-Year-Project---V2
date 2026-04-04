using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Red mist that chases the player along the rune path at constant speed.
/// Attach to a UI Image child inside the minigame overlay.
/// </summary>
[DisallowMultipleComponent]
public class TracingMist : MonoBehaviour
{
    // ── Runtime state (set via Initialize) ─────────────────────────

    private Vector2[] _path;
    private float[]   _cumulDist;
    private float     _speed;       // normalized units per second
    private bool      _active;
    private Image     _image;

    public float MistT { get; private set; }

    // ── Public API ────────────────────────────────────────────────

    public void Initialize(Vector2[] path, float[] cumulDist, float speed)
    {
        _path      = path;
        _cumulDist = cumulDist;
        _speed     = speed;
        _image     = GetComponent<Image>();
        Reset();
    }

    public void SetActive(bool active)
    {
        _active = active;
    }

    public void Reset()
    {
        MistT   = 0f;
        _active = false;
        UpdatePosition();
    }

    public bool CaughtPlayer(float cursorT)
    {
        return MistT >= cursorT;
    }

    // ── Update ────────────────────────────────────────────────────

    private void Update()
    {
        if (!_active || _path == null) return;

        MistT += _speed * Time.deltaTime;
        MistT = Mathf.Clamp01(MistT);

        UpdatePosition();

        // Pulsing alpha
        if (_image != null)
        {
            float alpha = 0.6f + 0.15f * Mathf.Sin(Time.time * 3f);
            var c = _image.color;
            c.a = alpha;
            _image.color = c;
        }
    }

    private void UpdatePosition()
    {
        if (_path == null || _cumulDist == null) return;
        Vector2 pos = RunePathData.SampleAt(_path, _cumulDist, MistT);
        ((RectTransform)transform).anchoredPosition = pos;
    }
}
