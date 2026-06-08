using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Wire 7.EndingScene.unity
//   1. Asset: Assets/UI/Ending/EndingPanelSettings.asset
//      (UI Toolkit > Panel Settings, ScaleWithScreenSize 1920x1080, match 0.5).
//   2. Add empty GameObject "EndingUIDocument" at scene root.
//   3. Add UIDocument: Panel Settings = EndingPanelSettings, Source = EndingScreen.uxml.
//   4. Add EndingScreenController on the same GameObject.
//      → Drag the 5 PNGs from Assets/Texture/Ending Illustrations/ into:
//         royalArt, rivalArt, slumArt, bankruptEarlyArt, bankruptLateArt.
//   5. On the existing EndingManager component, drag the new EndingScreenController
//      into its `screen` field; clear the legacy uGUI references.
//   6. Disable (don't delete) the legacy Canvas under EndingScene root.

/// <summary>
/// UI Toolkit driver for the ending screen. Owns all 5 ending titles + dialogue
/// strings + illustration slots. <see cref="Render"/> is the single public entry
/// point — both <see cref="EndingManager"/> (production) and
/// <see cref="EndingPreviewController"/> (debug scene) call it.
/// </summary>
[DisallowMultipleComponent]
public class EndingScreenController : MonoBehaviour
{
    /// <summary>Aggregated run-stats line shown under the dialogue.</summary>
    [Serializable]
    public struct EndingStats
    {
        public int daysSurvived;
        public int lettersReceived;
        public int peakReputation;
        public int wandsCrafted;

        public string Format() =>
            $"Days survived: {daysSurvived}  ·  Letters: {lettersReceived}  "
          + $"·  Peak reputation: {peakReputation}  ·  Wands crafted: {wandsCrafted}";
    }

    [Header("UI Toolkit")]
    [Tooltip("UIDocument hosting EndingScreen.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Reveal")]
    [Tooltip("Seconds the plaque + title fade-in takes before the typewriter starts.")]
    public float fadeInSeconds = 1.5f;

    [Tooltip("Characters revealed per second on the dialogue typewriter.")]
    public float charsPerSecond = 35f;

    [Tooltip("Click anywhere to fast-forward the typewriter.")]
    public bool skipOnClick = true;

    [Header("Ending Illustrations")]
    [Tooltip("Royal Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D royalArt;
    [Tooltip("Rival Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D rivalArt;
    [Tooltip("Slum Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D slumArt;
    [Tooltip("Bankrupt Early Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D bankruptEarlyArt;
    [Tooltip("Bankrupt Late Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D bankruptLateArt;

    /// <summary>Raised when the player presses "Start a new week". If no listener
    /// is attached the controller falls back to <see cref="DefaultRestart"/>.</summary>
    public event Action OnRestartRequested;

    /// <summary>Raised when the player presses "Leave the shop". If no listener
    /// is attached the controller falls back to <see cref="DefaultQuit"/>.</summary>
    public event Action OnQuitRequested;

    private VisualElement _root;
    private VisualElement _illustration;
    private VisualElement _illustrationPlaque;
    private VisualElement _titleBand;
    private VisualElement _dialoguePanel;
    private VisualElement _statsRule;
    private Label  _endingTitle;
    private Label  _dialogueText;
    private Label  _statsLabel;
    private Label  _hintLabel;
    private Button _restartButton;
    private Button _quitButton;

    private bool      _bootstrapped;
    private bool      _typing;
    private bool      _skipRequested;
    private Coroutine _revealCo;
    private EndingType _currentTone = (EndingType)(-1);

    private static readonly string[] ToneClasses =
    {
        "tone-royal",
        "tone-rival",
        "tone-slum",
        "tone-bankrupt-early",
        "tone-bankrupt-late",
    };

    private void OnEnable()  => Bootstrap();

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

        _root               = document.rootVisualElement;
        _illustration       = _root.Q<VisualElement>("illustration");
        _illustrationPlaque = _root.Q<VisualElement>("illustrationPlaque");
        _titleBand          = _root.Q<VisualElement>("titleBand");
        _dialoguePanel      = _root.Q<VisualElement>("dialoguePanel");
        _statsRule          = _root.Q<VisualElement>(className: "stats-rule");
        _endingTitle        = _root.Q<Label>("endingTitle");
        _dialogueText       = _root.Q<Label>("dialogueText");
        _statsLabel         = _root.Q<Label>("statsLabel");
        _hintLabel          = _root.Q<Label>("hintLabel");
        _restartButton      = _root.Q<Button>("restartButton");
        _quitButton         = _root.Q<Button>("quitButton");

        if (_restartButton != null) _restartButton.clicked += OnRestartClicked;
        if (_quitButton    != null) _quitButton.clicked    += OnQuitClicked;

        _bootstrapped = true;
    }

    /// <summary>
    /// Apply an ending variant to the screen. Cancels any in-flight reveal
    /// coroutine, swaps illustration/title/dialogue/stats, then re-runs the
    /// fade-in + typewriter sequence.
    /// </summary>
    public void Render(EndingType ending, EndingStats stats)
    {
        Bootstrap();
        if (_root == null)
        {
            // UIDocument hasn't populated rootVisualElement yet (callers in Start
            // can race this on the first frame). Retry next frame.
            StartCoroutine(RenderDeferred(ending, stats));
            return;
        }

        if (_revealCo != null) { StopCoroutine(_revealCo); _revealCo = null; }
        _typing = false;
        _skipRequested = false;

        ApplyTone(ending);
        ApplyIllustration(ending);

        if (_endingTitle  != null) _endingTitle.text  = GetTitle(ending);
        if (_statsLabel   != null) _statsLabel.text   = stats.Format();
        if (_dialogueText != null) _dialogueText.text = string.Empty;
        if (_hintLabel    != null) _hintLabel.style.display = DisplayStyle.Flex;

        SetVisible(_illustrationPlaque, false);
        SetVisible(_titleBand,          false);
        SetVisible(_endingTitle,        false);
        SetVisible(_dialoguePanel,      false);
        SetVisible(_statsRule,          false);
        SetVisible(_statsLabel,         false);

        if (_restartButton != null)
        {
            _restartButton.AddToClassList("is-hidden");
            _restartButton.RemoveFromClassList("visible");
        }
        if (_quitButton != null)
        {
            _quitButton.AddToClassList("is-hidden");
            _quitButton.RemoveFromClassList("visible");
        }

        _revealCo = StartCoroutine(RunReveal(ending));
    }

    private IEnumerator RenderDeferred(EndingType ending, EndingStats stats)
    {
        // Spin up to ~10 frames waiting for the document tree to materialise.
        for (int i = 0; i < 10 && _root == null; i++)
        {
            yield return null;
            Bootstrap();
        }
        if (_root != null) Render(ending, stats);
    }

    // ── Reveal sequence ────────────────────────────────────────────

    private IEnumerator RunReveal(EndingType ending)
    {
        // Stage 1: plaque + title band fade in immediately.
        yield return null; // give the previous frame's display:none a tick to settle
        SetVisible(_illustrationPlaque, true);
        SetVisible(_titleBand,          true);
        SetVisible(_endingTitle,        true);

        // Stage 2: wait for fadeInSeconds before the dialogue panel arrives.
        float wait = Mathf.Max(0.05f, fadeInSeconds);
        float t = 0f;
        while (t < wait) { t += Time.unscaledDeltaTime; yield return null; }

        SetVisible(_dialoguePanel, true);
        SetVisible(_statsRule,     true);
        SetVisible(_statsLabel,    true);

        // Stage 3: typewriter the dialogue.
        yield return TypeDialogue(GetDialogue(ending));

        // Stage 4: pop the buttons in.
        if (_hintLabel != null) _hintLabel.style.display = DisplayStyle.None;
        if (_restartButton != null)
        {
            _restartButton.RemoveFromClassList("is-hidden");
            _restartButton.AddToClassList("visible");
        }
        if (_quitButton != null)
        {
            _quitButton.RemoveFromClassList("is-hidden");
            _quitButton.AddToClassList("visible");
        }

        _revealCo = null;
    }

    // Substring-grow typewriter that respects rich-text tag boundaries (so
    // half-open <i...> never renders). Ported from MorningScreenController.
    private IEnumerator TypeDialogue(string text)
    {
        if (_dialogueText == null) yield break;

        if (charsPerSecond <= 0f)
        {
            _dialogueText.text = text;
            yield break;
        }

        _typing = true;
        _skipRequested = false;
        _dialogueText.text = string.Empty;

        int total = text?.Length ?? 0;
        if (total == 0) { _typing = false; yield break; }

        int   visible  = 0;
        float interval = 1f / charsPerSecond;
        float t        = 0f;
        bool  insideTag = false;

        while (visible < total)
        {
            if (_skipRequested)
            {
                _dialogueText.text = text;
                break;
            }

            t += Time.unscaledDeltaTime;
            while (t >= interval && visible < total)
            {
                t -= interval;
                do
                {
                    char c = text[visible];
                    if      (c == '<') insideTag = true;
                    else if (c == '>') insideTag = false;
                    visible++;
                } while (insideTag && visible < total);
            }

            _dialogueText.text = visible >= total ? text : text.Substring(0, visible);
            yield return null;
        }

        _typing = false;
    }

    // ── Helpers ────────────────────────────────────────────────────

    private void ApplyTone(EndingType ending)
    {
        if (_root == null) return;
        foreach (var c in ToneClasses) _root.RemoveFromClassList(c);
        _root.AddToClassList(ToneClass(ending));
        _currentTone = ending;
    }

    private void ApplyIllustration(EndingType ending)
    {
        if (_illustration == null) return;

        Texture2D tex = ending switch
        {
            EndingType.Royal         => royalArt,
            EndingType.Rival         => rivalArt,
            EndingType.Slum          => slumArt,
            EndingType.BankruptEarly => bankruptEarlyArt,
            EndingType.BankruptLate  => bankruptLateArt,
            _                        => null,
        };

        // Legacy fallback so older scene wirings don't visibly break.
        if (tex == null)
        {
            string artName = ending switch
            {
                EndingType.Royal         => "Royal",
                EndingType.Rival         => "Rival",
                EndingType.Slum          => "Slum",
                EndingType.BankruptEarly => "Bankrupt",
                EndingType.BankruptLate  => "Bankrupt",
                _                        => "Slum",
            };
            tex = Resources.Load<Texture2D>($"EndingArt/{artName}");
        }

        if (tex != null)
        {
            _illustration.RemoveFromClassList("placeholder");
            _illustration.style.backgroundImage = new StyleBackground(tex);
            _illustration.style.backgroundColor = StyleKeyword.Null;
        }
        else
        {
            _illustration.AddToClassList("placeholder");
            _illustration.style.backgroundImage = StyleKeyword.None;
            Color tint = ending switch
            {
                EndingType.Royal         => new Color(0.92f, 0.78f, 0.25f),
                EndingType.Rival         => new Color(0.55f, 0.55f, 0.60f),
                EndingType.Slum          => new Color(0.30f, 0.25f, 0.25f),
                EndingType.BankruptEarly => new Color(0.55f, 0.15f, 0.15f),
                EndingType.BankruptLate  => new Color(0.55f, 0.15f, 0.15f),
                _                        => Color.grey,
            };
            _illustration.style.backgroundColor = tint;
            Debug.LogWarning(
                $"[EndingScreenController] No illustration wired for {ending} — "
              + "drag the matching PNG from Assets/Texture/Ending Illustrations/ "
              + "into the Inspector slot. Using placeholder tint.");
        }
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (el == null) return;
        if (visible) el.AddToClassList("visible");
        else         el.RemoveFromClassList("visible");
    }

    private static string ToneClass(EndingType e) => e switch
    {
        EndingType.Royal         => "tone-royal",
        EndingType.Rival         => "tone-rival",
        EndingType.Slum          => "tone-slum",
        EndingType.BankruptEarly => "tone-bankrupt-early",
        EndingType.BankruptLate  => "tone-bankrupt-late",
        _                        => "tone-slum",
    };

    // ── Button handlers ────────────────────────────────────────────

    private void OnRestartClicked()
    {
        if (OnRestartRequested != null) OnRestartRequested.Invoke();
        else                              DefaultRestart();
    }

    private void OnQuitClicked()
    {
        if (OnQuitRequested != null) OnQuitRequested.Invoke();
        else                          DefaultQuit();
    }

    private static void DefaultRestart()
    {
        var gm = GameManager.Instance;
        gm?.ResetForNewPlaythrough();
        gm?.LoadScene(GameManager.SCENE_MORNING);
    }

    private static void DefaultQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Static content (verbatim from old EndingManager) ──────────

    public static string GetTitle(EndingType e) => e switch
    {
        EndingType.Royal         => "The Royal Wandmaker",
        EndingType.Rival         => "The Street with Two Shops",
        EndingType.Slum          => "The Wandmaker of the Moth",
        EndingType.BankruptEarly => "Short and Unlucky",
        EndingType.BankruptLate  => "So Close, and Then Not",
        _                        => "The End",
    };

    public static string GetDialogue(EndingType e) => e switch
    {
        EndingType.Royal =>
            "The escort arrives in velvet, as promised.\n\n"
          + "You pack one apron and the wand-knife your aunt left under the third floorboard. "
          + "The horses know the road. The palace gates know your name.\n\n"
          + "By year's end, every wand in the Royal Atelier bears your mark. "
          + "The cat your neighbor once accused you of turning blue arrives too — "
          + "a gift, from Mrs. Abernathy. It is, in fact, still blue.\n\n"
          + "<i>Some things a wandmaker cannot take back. Some, she does not have to.</i>",

        EndingType.Rival =>
            "You pay the rent twice, survive the week, and keep the shop.\n\n"
          + "A week later, <i>Oakscroft & Son</i> opens across the cobbles. "
          + "A brass sign. Velvet ropes. A queue of the curious.\n\n"
          + "Your queue is shorter now. But it is your queue. And Mrs. Hensley still "
          + "tells every woman at market which door to knock on.\n\n"
          + "It is not the ending you imagined on day one. "
          + "It is, however, an ending you worked for — and those are the ones that keep.",

        EndingType.Slum =>
            "By the seventh day, the shop is empty and the town is full of stories about you.\n\n"
          + "Grey Finch is waiting behind the Drowned Moth, as he said he would be. "
          + "He does not gloat. He does not welcome. He simply nods and holds the door.\n\n"
          + "The wands you make down there are <i>cheaper,</i> and <i>quieter,</i> "
          + "and they do what people ask without asking why. It is work. "
          + "It is even, on the good nights, a kind of craft.\n\n"
          + "<i>Your aunt is pestering the stars about you. The stars are pretending not to listen.</i>",

        EndingType.BankruptEarly =>
            "The gnomes came at dawn. They were very polite.\n\n"
          + "They took the workbench. They took the sign. "
          + "They took the third floorboard and everything under it. "
          + "They even took the owl, which you had grown fond of.\n\n"
          + "You are still in Ashenbury. You are not, however, a wandmaker. "
          + "Three days was not enough.\n\n"
          + "<i>Maybe next time.</i>",

        EndingType.BankruptLate =>
            "Six days of good, patient work.\n\n"
          + "And then, on the eve of the seventh, Mr. Grimsby's hand on the doorframe. "
          + "\"The gnomes will be here at dawn,\" he says — and they are.\n\n"
          + "The wand you might have made tomorrow will never be made. "
          + "The customer who might have walked in will never know your name.\n\n"
          + "<i>So close. You'll think about this week for a long time.</i>",

        _ => "The end.",
    };
}
