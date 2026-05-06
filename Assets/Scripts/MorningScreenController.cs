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
        if (_goldNumber != null) _goldNumber.text = gold.ToString();

        _main = LetterLibrary.GetMorningLetter(day, LetterLibrary.GetRepTier(rep));

        if (gm != null && Array.IndexOf(GameManager.RENT_DUE_DAYS, day) >= 0)
        {
            _followup    = LetterLibrary.GetRentReminder(day, gm.GetRentDueToday());
            _hasFollowup = true;
        }

        _bootstrapped = true;

        ShowLetter(_main);
        if (gm != null) gm.lettersReceived++;

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
