using UnityEngine;

/// <summary>
/// Timer that drives the red fill advancing along the trail.
/// MistT (0–1) represents how far the red fill has progressed from start to end.
/// When MistT reaches 1.0, the round is lost. The actual red fill visualization
/// is rendered by TracingMinigameUI via fill segments.
/// </summary>
[DisallowMultipleComponent]
public class TracingMist : MonoBehaviour
{
    private float _speed;
    private bool  _active;

    /// <summary>Normalized progress of the red fill (0 = start, 1 = end).</summary>
    public float MistT { get; private set; }

    /// <summary>True when the red fill has reached the end of the trail.</summary>
    public bool ReachedEnd => MistT >= 1f;

    public void Initialize(float speed)
    {
        _speed = speed;
        Reset();
    }

    public void SetActive(bool active) => _active = active;

    public void Reset()
    {
        MistT   = 0f;
        _active = false;
    }

    private void Update()
    {
        if (!_active) return;
        MistT += _speed * Time.deltaTime;
        MistT = Mathf.Clamp01(MistT);
    }
}
