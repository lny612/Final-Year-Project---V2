using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Char-by-char text reveal for TMP text. Uses maxVisibleCharacters so TMP
/// rich-text tags (bold, italic, color, font sizes) work without being
/// awkwardly split mid-tag. Click-to-skip jumps immediately to the full line.
/// </summary>
[DisallowMultipleComponent]
public class TypewriterText : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("TMP text component that will receive the revealed text.")]
    public TMP_Text target;

    [Header("Speed")]
    [Tooltip("Characters revealed per second. ~30-40 feels like dialogue.")]
    public float charsPerSecond = 35f;

    [Tooltip("If true, a mouse click anywhere fast-forwards the current reveal.")]
    public bool skipOnClick = true;

    [Tooltip("Optional: hold target invisible until Play() is called.")]
    public bool hideUntilPlay = true;

    public bool IsPlaying { get; private set; }
    public event Action OnComplete;

    private Coroutine _routine;
    private bool      _skipRequested;

    private void Awake()
    {
        if (hideUntilPlay && target != null)
        {
            target.text = string.Empty;
            target.maxVisibleCharacters = 0;
        }
    }

    private void Update()
    {
        if (skipOnClick && IsPlaying && Input.GetMouseButtonDown(0))
            _skipRequested = true;
    }

    /// <summary>Reveal text char-by-char. Coroutine completes when done or skipped.</summary>
    public IEnumerator Play(string text)
    {
        if (target == null) yield break;

        // Host GO must be active: TMP needs it to render and we StartCoroutine on
        // ourselves below. Editor wiring sometimes leaves the text GO inactive.
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[TypewriterText] '{name}' has an inactive parent; " +
                             "cannot start typewriter. Activate the parent in the Inspector.");
            yield break;
        }

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayInternal(text));
        yield return _routine;
    }

    /// <summary>Fire-and-forget variant; safe to call from button handlers.</summary>
    public void PlayFromStart(string text)
    {
        if (target == null) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[TypewriterText] '{name}' has an inactive parent; " +
                             "cannot start typewriter. Activate the parent in the Inspector.");
            return;
        }
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayInternal(text));
    }

    public void SkipToEnd() => _skipRequested = true;

    private IEnumerator PlayInternal(string text)
    {
        IsPlaying       = true;
        _skipRequested  = false;
        target.text                  = text;
        target.ForceMeshUpdate();
        int total                    = target.textInfo.characterCount;
        target.maxVisibleCharacters  = 0;

        if (charsPerSecond <= 0f)
        {
            target.maxVisibleCharacters = total;
            IsPlaying = false;
            OnComplete?.Invoke();
            yield break;
        }

        float interval = 1f / charsPerSecond;
        float t        = 0f;
        int   visible  = 0;

        while (visible < total)
        {
            if (_skipRequested)
            {
                visible = total;
                target.maxVisibleCharacters = total;
                break;
            }

            t += Time.unscaledDeltaTime;
            while (t >= interval && visible < total)
            {
                t -= interval;
                visible++;
            }
            target.maxVisibleCharacters = visible;
            yield return null;
        }

        target.maxVisibleCharacters = total;
        IsPlaying = false;
        OnComplete?.Invoke();
    }
}
