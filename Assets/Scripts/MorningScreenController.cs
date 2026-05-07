using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Wire MorningScene.unity
//   1. Create asset Assets/UI/Morning/MorningPanelSettings.asset
//        (UI Toolkit > Panel Settings, ScaleWithScreenSize 1920x1080, match 0.5).
//   2. Add empty GameObject "MorningUIDocument" at scene root.
//   3. Add UIDocument: Panel Settings = MorningPanelSettings, Source = MorningScene.uxml.
//   4. Add MorningScreenController on the same GameObject.
//   5. Disable (do not delete) the legacy Canvas children driving MorningLetterUI.

/// <summary>
/// UI Toolkit replacement for <see cref="MorningLetterUI"/>. Drives the
/// cozy fantasy morning screen: full-bleed shop background, day plaque,
/// gold purse, and a centered parchment letter that types itself out.
/// On Continue, queues the rent-reminder follow-up letter on rent-due
/// mornings, then loads the customer scene.
/// </summary>
[DisallowMultipleComponent]
public class MorningScreenController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument hosting MorningScene.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second. ~30-40 reads like dialogue.")]
    public float charsPerSecond = 35f;

    [Tooltip("Click anywhere to fast-forward the current line.")]
    public bool skipOnClick = true;

    [Header("Copy")]
    [Tooltip("Label on the continue button when a non-rent letter is showing.")]
    public string continueLabel       = "Meet the first customer";

    [Tooltip("Label on the continue button when the main letter is followed by a rent reminder.")]
    public string continueLabelRent   = "Read on...";

    [Header("Pre-generation")]
    [Tooltip("If true, kicks off the customer API request as soon as this scene loads " +
             "so the next scene (CustomerGeneratorTest) can use the cached result " +
             "immediately instead of doing its own ~0.5–2s roundtrip.")]
    public bool preGenerateCustomer = true;
    [Tooltip("If true, AS SOON AS the customer pre-generation completes, fires off the " +
             "material text + image API requests so MaterialGenerator scene can skip " +
             "most of its wait when the player arrives.")]
    public bool preGenerateMaterials = true;
    [Tooltip("OpenAI chat completions endpoint used for the pre-generation request.")]
    public string openAIUrl = CustomerService.DefaultUrl;
    [Tooltip("ComfyUI endpoint used for material image pre-generation.")]
    public string comfyUIUrl = MaterialService.DefaultComfyUIUrl;
    [Tooltip("CLIPTextEncode node ID in image_z_image_turbo.json.")]
    public string clipNodeId = MaterialService.DefaultClipNodeId;
    [Tooltip("KSampler node ID in image_z_image_turbo.json.")]
    public string ksamplerNodeId = MaterialService.DefaultKSamplerNodeId;

    [Header("Polish FX")]
    [Tooltip("Seconds the gold-counter takes to count from goldAtMorningStart to playerGold.")]
    public float goldCountDuration = 1.2f;

    [Tooltip("Easing curve applied to the gold-counter t parameter (0..1).")]
    public AnimationCurve goldCountEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Tint of the placeholder heart particles bursting from the letter on day 2+.")]
    public Color heartColor = new Color(0.93f, 0.31f, 0.45f, 1f);

    [Tooltip("Lifetime of each heart particle (lerps position + fades out + scales up over this duration).")]
    public float heartBurstDuration = 0.9f;

    [Tooltip("Pixel size of the placeholder heart label.")]
    public float heartFontSize = 36f;

    [Tooltip("Maximum stagger applied across the spawned hearts so they read as a burst, not a single frame.")]
    public float heartBurstStagger = 0.25f;

    [Header("Seal colors per sender type")]
    public Color sealNeighbor   = new Color(0.95f, 0.88f, 0.72f);
    public Color sealCustomer   = new Color(0.82f, 0.55f, 0.35f);
    public Color sealAristocrat = new Color(0.92f, 0.78f, 0.25f);
    public Color sealRoyal      = new Color(0.42f, 0.18f, 0.55f);
    public Color sealBrigand    = new Color(0.15f, 0.15f, 0.15f);
    public Color sealLandlord   = new Color(0.55f, 0.55f, 0.55f);
    public Color sealAunt       = new Color(0.80f, 0.20f, 0.25f);
    public Color sealRival      = new Color(0.30f, 0.45f, 0.30f);

    private VisualElement _root;
    private Label  _dayNumber;
    private Label  _daySubtle;
    private Label  _goldNumber;
    private Label  _subjectText;
    private Label  _fromText;
    private Label  _bodyText;
    private Label  _hintLabel;
    private VisualElement _waxSeal;
    private Button _continueButton;

    private LetterContent _main;
    private LetterContent _followup;
    private bool          _hasFollowup;
    private bool          _showingFollowup;
    private bool          _bootstrapped;
    private bool          _typing;
    private bool          _skipRequested;
    private Coroutine     _typeRoutine;
    private Coroutine     _goldRoutine;
    private VisualElement _heartLayer;
    private VisualElement _letterPanel;

    private void OnEnable() => Bootstrap();

    private void Update()
    {
        if (!_typing) return;
        if (skipOnClick && Input.GetMouseButtonDown(0)) _skipRequested = true;
    }

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root           = document.rootVisualElement;
        _dayNumber      = _root.Q<Label>("dayNumber");
        _daySubtle      = _root.Q<Label>("daySubtle");
        _goldNumber     = _root.Q<Label>("goldNumber");
        _subjectText    = _root.Q<Label>("subjectText");
        _fromText       = _root.Q<Label>("fromText");
        _bodyText       = _root.Q<Label>("bodyText");
        _hintLabel      = _root.Q<Label>("hintLabel");
        _waxSeal        = _root.Q<VisualElement>("waxSeal");
        _continueButton = _root.Q<Button>("continueButton");
        _letterPanel    = _root.Q<VisualElement>("letterPanel");

        if (_continueButton != null)
        {
            _continueButton.AddToClassList("is-hidden");
            _continueButton.clicked += OnContinueClicked;
        }

        var gm = GameManager.Instance;
        int day = gm != null ? gm.currentDay        : 1;
        int rep = gm != null ? gm.playerReputation  : 0;
        int gold = gm != null ? gm.playerGold       : GameManager.STARTING_GOLD;

        if (_dayNumber  != null) _dayNumber.text  = day.ToString();
        if (_daySubtle  != null) _daySubtle.text  = $"of {GameManager.TOTAL_DAYS}";

        // Animate the gold counter from the snapshot taken in EvaluationManager
        // (right before the wand reward was applied) up to the current balance.
        // Day 1 has no prior snapshot so goldAtMorningStart = 0 and this plays
        // the opening 0 → 500 anim. Runs in parallel with the letter typewriter.
        int goldStart = gm != null ? gm.goldAtMorningStart : 0;
        if (_goldNumber != null)
        {
            _goldNumber.text = goldStart.ToString();
            if (_goldRoutine != null) StopCoroutine(_goldRoutine);
            _goldRoutine = StartCoroutine(AnimateGoldCounter(goldStart, gold));
        }

        _main = LetterLibrary.GetMorningLetter(day, LetterLibrary.GetRepTier(rep, day));

        if (gm != null && Array.IndexOf(GameManager.RENT_DUE_DAYS, day) >= 0)
        {
            _followup    = LetterLibrary.GetRentReminder(day, gm.GetRentDueToday());
            _hasFollowup = true;
        }

        _bootstrapped = true;

        // Build the heart overlay layer eagerly so its layout is resolved by
        // the time BurstHearts wants to read worldBound — adding it lazily
        // means the first frame's worldBound is zero.
        EnsureHeartLayer();

        ShowLetter(_main);
        if (gm != null) gm.lettersReceived++;

        // Heart-burst on day 2+ — quantity scales with the prior day's
        // reputation gain so the player "feels" yesterday's customer reaction
        // before they read today's letter. Day 1 has no prior round so we skip.
        if (gm != null && gm.currentDay > 1)
        {
            int heartCount = HeartCountForRep(gm.lastDayRepEarned);
            if (heartCount > 0) StartCoroutine(BurstHearts(heartCount));
        }

        // Kick off the customer API request in parallel with the typewriter so
        // CustomerGenerator scene can pick up the result without its own wait.
        // The coroutine MUST run on GameManager (DontDestroyOnLoad), not on
        // this controller — otherwise the scene transition to CustomerGenerator
        // destroys this MonoBehaviour, kills the coroutine mid-flight, and
        // leaves `pendingCustomerInProgress` stuck at true so CustomerGenerator
        // waits forever and never populates the dossier.
        if (preGenerateCustomer && gm != null
            && gm.pendingCustomer == null
            && !gm.pendingCustomerInProgress)
        {
            gm.pendingCustomerInProgress = true;
            gm.StartCoroutine(CustomerService.GenerateAsync(openAIUrl, (order, err) =>
            {
                var g = GameManager.Instance;
                if (g == null) return;
                g.pendingCustomerInProgress = false;
                if (order != null)
                {
                    g.pendingCustomer = order;
                    // Cascade: kick off material pre-gen on the same persistent host
                    // (GameManager) so the market scene loads with images already
                    // partially or fully generated.
                    if (preGenerateMaterials
                        && g.pendingMaterials == null
                        && !g.pendingMaterialsInProgress)
                    {
                        g.StartCoroutine(MaterialService.PreGenAsync(
                            g, g, order, openAIUrl, comfyUIUrl, clipNodeId, ksamplerNodeId));
                    }
                }
                else Debug.LogWarning("[MorningScreenController] Pre-generation failed: " + err +
                                       " (CustomerGenerator will request its own.)");
            }));
        }
    }

    private void ShowLetter(LetterContent letter)
    {
        // Defer the "letter receive" cue by one frame so the new scene's
        // audio pipeline has fully come up. Firing PlayOneShot synchronously
        // from OnEnable can land in the brief gap between the unloading
        // scene's AudioListener being destroyed and the next one going live,
        // which produces silence the very first time the player sees the
        // letter — exactly when the cue matters most.
        // Only the *initial* letter triggers the cue; the optional rent
        // reminder is queued in the same scene and shouldn't ping again.
        if (!_showingFollowup) StartCoroutine(PlayLetterReceiveDeferred());

        if (_subjectText != null) _subjectText.text = letter.subject;
        if (_fromText    != null) _fromText.text    = "— " + letter.from;
        if (_waxSeal     != null) _waxSeal.style.backgroundColor = GetSealColor(letter.sender);

        if (_continueButton != null)
        {
            _continueButton.AddToClassList("is-hidden");
            _continueButton.text = (_hasFollowup && !_showingFollowup) ? continueLabelRent : continueLabel;
        }
        if (_hintLabel != null) _hintLabel.style.display = DisplayStyle.Flex;

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeBody(letter.body));
    }

    private IEnumerator PlayLetterReceiveDeferred()
    {
        // One frame is enough for AudioManager.OnSceneLoaded to disable
        // the scene's stale AudioListener and bring its own back online.
        yield return null;
        AudioManager.Instance?.PlayLetterReceive();
    }

    private IEnumerator TypeBody(string text)
    {
        if (_bodyText == null) yield break;

        _typing        = true;
        _skipRequested = false;
        _bodyText.text = text;

        // UI Toolkit doesn't expose maxVisibleCharacters directly on Label,
        // so we substring-grow. Rich-text tags are kept whole by skipping
        // the inside of any "<...>" run when growing.
        int total = text.Length;
        int visible = 0;
        float interval = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;
        float t = 0f;

        if (charsPerSecond <= 0f)
        {
            _bodyText.text = text;
            FinishTyping();
            yield break;
        }

        _bodyText.text = string.Empty;
        bool insideTag = false;

        while (visible < total)
        {
            if (_skipRequested)
            {
                visible        = total;
                _bodyText.text = text;
                break;
            }

            t += Time.unscaledDeltaTime;
            while (t >= interval && visible < total)
            {
                t -= interval;

                // Walk forward at least one char; if we're inside a tag,
                // keep walking until the tag closes so we never display
                // a half-open <b...
                do
                {
                    char c = text[visible];
                    if (c == '<') insideTag = true;
                    else if (c == '>') insideTag = false;
                    visible++;
                } while (insideTag && visible < total);
            }

            _bodyText.text = visible >= total ? text : text.Substring(0, visible);
            yield return null;
        }

        FinishTyping();
    }

    private void FinishTyping()
    {
        _typing = false;
        if (_continueButton != null) _continueButton.RemoveFromClassList("is-hidden");
        if (_hintLabel      != null) _hintLabel.style.display = DisplayStyle.None;
    }

    private void OnContinueClicked()
    {
        if (_hasFollowup && !_showingFollowup)
        {
            _showingFollowup = true;
            if (GameManager.Instance != null) GameManager.Instance.lettersReceived++;
            ShowLetter(_followup);
            return;
        }

        GameManager.Instance?.LoadScene(GameManager.SCENE_CUSTOMER);
    }

    // ── Polish FX: gold counter ──────────────────────────────────

    private IEnumerator AnimateGoldCounter(int from, int to)
    {
        if (_goldNumber == null) yield break;

        // Snap on degenerate inputs.
        if (goldCountDuration <= 0f || from == to)
        {
            _goldNumber.text = to.ToString();
            yield break;
        }

        float elapsed = 0f;
        int   last    = from;
        _goldNumber.text = from.ToString();

        while (elapsed < goldCountDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / goldCountDuration);
            float eased = goldCountEase != null ? goldCountEase.Evaluate(t) : t;
            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            if (current != last)
            {
                _goldNumber.text = current.ToString();
                last = current;
            }
            yield return null;
        }

        _goldNumber.text = to.ToString();
    }

    // ── Polish FX: reputation heart burst ───────────────────────

    private static int HeartCountForRep(int rep)
    {
        if (rep <= 0)  return 0;
        if (rep <= 5)  return 6;
        if (rep <= 10) return 12;
        if (rep <= 15) return 20;
        return 30;
    }

    private void EnsureHeartLayer()
    {
        if (_heartLayer != null || _root == null) return;

        _heartLayer = new VisualElement { name = "heartLayer" };
        _heartLayer.pickingMode = PickingMode.Ignore;
        var s = _heartLayer.style;
        s.position = Position.Absolute;
        s.left   = 0; s.top    = 0;
        s.right  = 0; s.bottom = 0;
        _root.Add(_heartLayer);
        _heartLayer.BringToFront();
    }

    private IEnumerator BurstHearts(int count)
    {
        // Wait one frame so the UI Toolkit layout pass resolves worldBound on
        // letterPanel — querying it during Bootstrap returns NaN/zero rects.
        yield return null;

        EnsureHeartLayer();
        if (_heartLayer == null) yield break;

        Vector2 origin;
        if (_letterPanel != null && _letterPanel.worldBound.width > 0f)
        {
            var wb = _letterPanel.worldBound;
            origin = new Vector2(wb.center.x, wb.center.y);
        }
        else
        {
            // Fallback: use root's center.
            var rb = _root.worldBound;
            origin = new Vector2(rb.center.x, rb.center.y);
        }

        for (int i = 0; i < count; i++)
        {
            float angleDeg = UnityEngine.Random.Range(0f, 360f);
            float distance = UnityEngine.Random.Range(220f, 360f);
            float upBias   = UnityEngine.Random.Range(20f, 60f);
            float stagger  = (count > 1)
                ? UnityEngine.Random.Range(0f, heartBurstStagger)
                : 0f;

            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 target = origin + dir * distance + new Vector2(0f, -upBias);

            StartCoroutine(AnimateOneHeart(origin, target, stagger));
        }
    }

    private IEnumerator AnimateOneHeart(Vector2 origin, Vector2 target, float startDelay)
    {
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
        if (_heartLayer == null) yield break;

        var heart = new Label("♥");
        heart.pickingMode = PickingMode.Ignore;
        var hs = heart.style;
        hs.position = Position.Absolute;
        hs.color = heartColor;
        hs.fontSize = heartFontSize;
        hs.unityFontStyleAndWeight = FontStyle.Bold;
        // Center the glyph on its position rather than top-left anchor.
        hs.translate = new Translate(new Length(-50f, LengthUnit.Percent),
                                     new Length(-50f, LengthUnit.Percent), 0f);

        // worldBound origin is in screen space; convert to heartLayer-local
        // (heartLayer fills the root, so subtract the root's worldBound origin).
        Vector2 layerOrigin = _heartLayer.worldBound.position;
        Vector2 startLocal  = origin - layerOrigin;
        Vector2 endLocal    = target - layerOrigin;

        hs.left = startLocal.x;
        hs.top  = startLocal.y;
        hs.opacity = 0f;
        hs.scale = new Scale(new Vector3(0.6f, 0.6f, 1f));

        _heartLayer.Add(heart);

        float elapsed  = 0f;
        float duration = Mathf.Max(0.05f, heartBurstDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Eased outward drift, scale up, fade in then out.
            float eased = 1f - Mathf.Pow(1f - t, 2f); // ease-out quad
            Vector2 cur = Vector2.Lerp(startLocal, endLocal, eased);
            hs.left = cur.x;
            hs.top  = cur.y;
            hs.scale = new Scale(new Vector3(Mathf.Lerp(0.6f, 1.4f, eased),
                                             Mathf.Lerp(0.6f, 1.4f, eased), 1f));
            // Fade in 0..0.2, hold 0.2..0.6, fade out 0.6..1.0.
            float a = t < 0.2f ? Mathf.InverseLerp(0f, 0.2f, t)
                    : t < 0.6f ? 1f
                    :            1f - Mathf.InverseLerp(0.6f, 1f, t);
            hs.opacity = a;

            yield return null;
        }

        if (heart.parent != null) heart.parent.Remove(heart);
    }

    private Color GetSealColor(LetterSender sender)
    {
        return sender switch
        {
            LetterSender.Neighbor   => sealNeighbor,
            LetterSender.Customer   => sealCustomer,
            LetterSender.Aristocrat => sealAristocrat,
            LetterSender.Royal      => sealRoyal,
            LetterSender.Brigand    => sealBrigand,
            LetterSender.Landlord   => sealLandlord,
            LetterSender.Aunt       => sealAunt,
            LetterSender.Rival      => sealRival,
            _                       => Color.white,
        };
    }
}
