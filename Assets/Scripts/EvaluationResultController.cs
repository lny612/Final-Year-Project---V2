using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Wire EvaluationScene.unity
//   1. Asset Assets/UI/Evaluation/EvaluationResultPanelSettings.asset
//        (UI Toolkit > Panel Settings, ScaleWithScreenSize 1920x1080, match 0.5).
//   2. GameObject "EvaluationUIDocument" at scene root.
//   3. UIDocument: PanelSettings = EvaluationResultPanelSettings,
//      Source = Assets/UI/Evaluation/EvaluationResult.uxml.
//   4. EvaluationResultController on the same GameObject.
//   5. Wire EvaluationManager.resultController to this component
//      (or it is auto-found via FindObjectOfType in Start).
//   6. Disable (do not delete) the legacy uGUI Customer / Wand / Result
//      widgets on Canvas — the new UXML replaces them visually.

/// <summary>
/// UI Toolkit controller for the post-minigame result page.
/// Pure presentation: <see cref="EvaluationManager"/> calls
/// <see cref="ApplyResult"/> with everything the page needs to display.
/// Layout: theatrical wand reveal (centred image with radial light rays
/// behind it) on the left; parchment scoreboard plaque + final grade
/// letter on the right.
/// </summary>
[DisallowMultipleComponent]
public class EvaluationResultController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument hosting EvaluationResult.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Reveal pacing")]
    [Tooltip("Score count-up duration in seconds.")]
    public float scoreCountDuration = 1.2f;

    [Tooltip("Delay between row reveals (Conjuring → Materials → Customer Fit → Reward → Grade).")]
    public float rowRevealStagger    = 0.35f;

    private VisualElement _root;
    private VisualElement _rayWrap, _rayGlow, _rayHalo, _wandHaloOuter, _wandHaloInner, _wandImage;
    private VisualElement _scoreboard, _gradeBadge;
    private Label _bannerTitle, _wandName, _verdictLine;
    private Label _conjuringValue, _materialsValue, _customerFitValue, _rewardValue, _gradeLetter, _hintLabel;
    private Button _continueButton;

    private bool _bootstrapped;
    private Coroutine _revealRoutine;
    private bool _skipRequested;

    private Action _onContinue;

    private void OnEnable() => Bootstrap();

    private void Update()
    {
        if (_revealRoutine != null && Input.GetMouseButtonDown(0))
            _skipRequested = true;
    }

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root            = document.rootVisualElement;
        _rayWrap         = _root.Q<VisualElement>("rayWrap");
        _rayGlow         = _root.Q<VisualElement>("rayGlow");
        _rayHalo         = _root.Q<VisualElement>("rayHalo");
        _wandHaloOuter   = _root.Q<VisualElement>("wandHaloOuter");
        _wandHaloInner   = _root.Q<VisualElement>("wandHaloInner");
        _wandImage       = _root.Q<VisualElement>("wandImage");
        _scoreboard      = _root.Q<VisualElement>("scoreboard");
        _gradeBadge      = _root.Q<VisualElement>("gradeBadge");
        _bannerTitle     = _root.Q<Label>("bannerTitle");
        _wandName        = _root.Q<Label>("wandName");
        _verdictLine     = _root.Q<Label>("verdictLine");
        _conjuringValue  = _root.Q<Label>("conjuringValue");
        _materialsValue  = _root.Q<Label>("materialsValue");
        _customerFitValue= _root.Q<Label>("customerFitValue");
        _rewardValue     = _root.Q<Label>("rewardValue");
        _gradeLetter     = _root.Q<Label>("gradeLetter");
        _hintLabel       = _root.Q<Label>("hintLabel");
        _continueButton  = _root.Q<Button>("continueButton");

        // Initial state — everything hidden so the reveal lands.
        SetHidden(_continueButton, true);
        AddRevealClass(_scoreboard, "reveal-fade");
        AddRevealClass(_bannerTitle, "reveal-fade");
        AddRevealClass(_wandName, "reveal-fade");
        AddRevealClass(_wandImage, "reveal-pop");
        AddRevealClass(_wandHaloInner, "reveal-fade");
        AddRevealClass(_wandHaloOuter, "reveal-fade");

        if (_continueButton != null) _continueButton.clicked += () => _onContinue?.Invoke();

        _bootstrapped = true;
    }

    private static void AddRevealClass(VisualElement el, string cls)
    {
        if (el == null) return;
        if (!el.ClassListContains(cls)) el.AddToClassList(cls);
    }

    /// <summary>
    /// Drive the result page. Idempotent — calling twice restarts the reveal.
    /// </summary>
    public void ApplyResult(ResultData data, Action onContinue)
    {
        Bootstrap();
        if (_root == null) return;

        _onContinue = onContinue;

        // Static text first.
        if (_wandName    != null) _wandName.text    = data.wandName ?? "";
        if (_verdictLine != null) _verdictLine.text = data.verdict  ?? "";

        // Wand image.
        if (_wandImage != null)
        {
            if (data.wandTexture != null)
                _wandImage.style.backgroundImage = new StyleBackground(data.wandTexture);
            else
                _wandImage.style.backgroundImage = new StyleBackground((Texture2D)null);
        }

        // Initial scoreboard values (will count up during reveal).
        if (_conjuringValue   != null) _conjuringValue.text   = $"0 / {data.conjuringTotal}";
        if (_materialsValue   != null) _materialsValue.text   = $"0 / 100";
        if (_customerFitValue != null) _customerFitValue.text = "—";
        if (_rewardValue      != null) _rewardValue.text      = "+0g  +0 rep";
        if (_gradeLetter      != null) _gradeLetter.text      = "—";

        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _skipRequested = false;
        _revealRoutine = StartCoroutine(RevealCoroutine(data));
    }

    private IEnumerator RevealCoroutine(ResultData data)
    {
        // Phase 1 — banner + wand reveal.
        yield return WaitOrSkip(0.1f);
        SetVisible(_bannerTitle, true);
        AudioManager.Instance?.PlayEvaluationReveal();

        yield return WaitOrSkip(0.25f);
        SetVisible(_wandHaloOuter, true);
        SetVisible(_wandHaloInner, true);
        SetVisible(_wandImage, true);

        yield return WaitOrSkip(0.35f);
        SetVisible(_wandName, true);

        // Phase 2 — scoreboard slides in.
        yield return WaitOrSkip(0.3f);
        SetVisible(_scoreboard, true);

        // Phase 3 — count-ups, staggered.
        yield return WaitOrSkip(0.2f);
        yield return CountUp(_conjuringValue, 0, data.conjuringWon, scoreCountDuration, won => $"{won} / {data.conjuringTotal}");
        yield return WaitOrSkip(rowRevealStagger);
        yield return CountUp(_materialsValue, 0, data.materialsScore, scoreCountDuration, n => $"{n} / 100");
        yield return WaitOrSkip(rowRevealStagger);
        if (_customerFitValue != null) _customerFitValue.text = data.customerFitLabel ?? "—";
        yield return WaitOrSkip(rowRevealStagger);
        if (_rewardValue != null)
        {
            string repStr = data.repDelta >= 0 ? $"+{data.repDelta}" : data.repDelta.ToString();
            _rewardValue.text = $"+{data.goldEarned}g  {repStr} rep";
        }

        // Phase 4 — final grade badge pop.
        yield return WaitOrSkip(0.3f);
        if (_gradeLetter != null) _gradeLetter.text = data.finalGrade.ToString();
        if (_gradeBadge  != null)
        {
            _gradeBadge.style.scale = new StyleScale(new Scale(new Vector3(1.25f, 1.25f, 1f)));
            yield return WaitOrSkip(0.18f);
            _gradeBadge.style.scale = new StyleScale(new Scale(Vector3.one));
        }

        // Phase 5 — show continue.
        SetHidden(_continueButton, false);
        if (_hintLabel != null) _hintLabel.style.display = DisplayStyle.None;

        _revealRoutine = null;
    }

    private IEnumerator CountUp(Label label, int from, int to, float duration, Func<int, string> format)
    {
        if (label == null) yield break;
        if (duration <= 0f || from == to)
        {
            label.text = format(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_skipRequested) break;
            elapsed += Time.unscaledDeltaTime;
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            label.text = format(v);
            yield return null;
        }
        label.text = format(to);
    }

    private IEnumerator WaitOrSkip(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (_skipRequested) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (el == null) return;
        if (visible)
        {
            if (!el.ClassListContains("visible")) el.AddToClassList("visible");
        }
        else
        {
            el.RemoveFromClassList("visible");
        }
    }

    private static void SetHidden(VisualElement el, bool hidden)
    {
        if (el == null) return;
        if (hidden)
        {
            if (!el.ClassListContains("is-hidden")) el.AddToClassList("is-hidden");
        }
        else
        {
            el.RemoveFromClassList("is-hidden");
        }
    }

    /// <summary>Data bundle handed in by EvaluationManager.</summary>
    public struct ResultData
    {
        public string    wandName;
        public Texture2D wandTexture;
        public string    verdict;

        // Scoreboard rows
        public int conjuringWon;       // rounds won (0..conjuringTotal)
        public int conjuringTotal;     // total rounds (typically 3)
        public int materialsScore;     // matchScore 0..100 from GPT eval
        public string customerFitLabel; // e.g. "Loved it", "Liked it", "Lukewarm"
        public int goldEarned;
        public int repDelta;

        // Final grade letter (A/B/C/D/F or composite of conjuring + match)
        public char finalGrade;
    }
}
