using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TODO-EDITOR: Font swap to The Garden of Lights
//   The other UI scenes were migrated to UI Toolkit and pull
//   `Assets/Fonts/The Garden of Lights.ttf` via the .uss `-unity-font-definition`
//   rule. This scene is still uGUI, so the font lives on each TMP_Text's Font
//   Asset field. Open EndingScene.unity, select each TMP_Text under Canvas
//   (EndingTitle, DialogueText, StatsText, RestartButton/QuitButton labels)
//   and set their Font Asset to `Assets/Fonts/The Garden of Lights SDF.asset`.
//
// TODO-EDITOR: Wire the 5 ending illustrations on the EndingManager component
//   in EndingScene.unity. Drag each PNG from
//   `Assets/Texture/Ending Illustrations/` into the matching Inspector slot
//   under "Ending Illustrations":
//     royalArt          ← Royal Ending.png
//     rivalArt          ← Rival Ending.png
//     slumArt           ← Slum Ending.png
//     bankruptEarlyArt  ← Bankrupt Early Ending.png
//     bankruptLateArt   ← Bankrupt Late Ending.png
//   The legacy Resources/EndingArt/ fallback still runs if any slot is empty.
//
// TODO-EDITOR: Scene hierarchy reference for EndingScene.unity:
//     - GameManager (same DontDestroyOnLoad pattern)
//     - EventSystem
//     - Canvas (Screen Space - Overlay)
//         - CanvasGroup (attach to Canvas or a child root — wire to 'fader')
//         - Background (full-screen black Image)
//         - Illustration (RawImage, ~60% of screen, centered upper half)
//         - EndingTitle (TMP_Text, large, bold, below illustration)
//         - DialogueText (TMP_Text, body font, centered below title)
//             → add TypewriterText component, target = DialogueText, wire to dialogueTypewriter
//         - StatsText (TMP_Text, small, below dialogue)
//         - RestartButton (Button, bottom-left, "Start a new week")
//         - QuitButton (Button, bottom-right, "Leave the shop" — optional)

/// <summary>
/// Terminal scene controller. Picks the correct ending variant from
/// GameManager state, fades in the illustration, plays the dialogue
/// typewriter, and offers a restart.
/// </summary>
[DisallowMultipleComponent]
public class EndingManager : MonoBehaviour
{
    [Header("Visuals")]
    public RawImage      illustration;
    public CanvasGroup   fader;
    public float         fadeInSeconds = 1.5f;

    [Header("Text")]
    public TMP_Text      endingTitle;
    public TypewriterText dialogueTypewriter;
    public TMP_Text      statsText;

    [Header("Flow")]
    public Button        restartButton;
    public Button        quitButton;

    [Header("Ending Illustrations")]
    [Tooltip("Royal Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D     royalArt;
    [Tooltip("Rival Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D     rivalArt;
    [Tooltip("Slum Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D     slumArt;
    [Tooltip("Bankrupt Early Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D     bankruptEarlyArt;
    [Tooltip("Bankrupt Late Ending.png from Assets/Texture/Ending Illustrations/")]
    public Texture2D     bankruptLateArt;

    private void Start()
    {
        restartButton?.onClick.AddListener(OnRestart);
        quitButton?.onClick.AddListener(OnQuit);

        if (restartButton != null) restartButton.gameObject.SetActive(false);
        if (quitButton    != null) quitButton.gameObject.SetActive(false);

        EndingType ending = ResolveEnding();
        LoadIllustration(ending);

        if (endingTitle != null) endingTitle.text = GetTitle(ending);
        if (statsText   != null) statsText.text   = BuildStatsLine();

        StartCoroutine(RunEndingSequence(ending));
    }

    private static EndingType ResolveEnding()
    {
        var gm = GameManager.Instance;
        if (gm == null) return EndingType.Slum;

        // Bankruptcy overrides reputation — player didn't make it to day 7.
        if (gm.playerGold < 0 || gm.currentDay < GameManager.TOTAL_DAYS)
        {
            // Check if we arrived here via the rent flow rather than natural end.
            if (gm.bankruptedOnDay3 || gm.currentDay == 3)
                return EndingType.BankruptEarly;
            // Otherwise assume late bankruptcy if currentDay < TOTAL_DAYS
            if (gm.currentDay < GameManager.TOTAL_DAYS)
                return EndingType.BankruptLate;
        }

        return gm.DetermineEnding();
    }

    private void LoadIllustration(EndingType ending)
    {
        if (illustration == null) return;

        Texture2D tex = ending switch
        {
            EndingType.Royal          => royalArt,
            EndingType.Rival          => rivalArt,
            EndingType.Slum           => slumArt,
            EndingType.BankruptEarly  => bankruptEarlyArt,
            EndingType.BankruptLate   => bankruptLateArt,
            _                         => null
        };

        // Fallback: legacy Resources/EndingArt/ lookup when an Inspector slot
        // is unwired — keeps prior setups working until the scene is re-saved.
        if (tex == null)
        {
            string artName = ending switch
            {
                EndingType.Royal          => "Royal",
                EndingType.Rival          => "Rival",
                EndingType.Slum           => "Slum",
                EndingType.BankruptEarly  => "Bankrupt",
                EndingType.BankruptLate   => "Bankrupt",
                _                         => "Slum"
            };
            tex = Resources.Load<Texture2D>($"EndingArt/{artName}");
        }

        if (tex != null)
        {
            illustration.texture = tex;
            illustration.color   = Color.white;
        }
        else
        {
            // Graceful degradation: tint the RawImage as a placeholder.
            illustration.texture = null;
            illustration.color = ending switch
            {
                EndingType.Royal          => new Color(0.92f, 0.78f, 0.25f),
                EndingType.Rival          => new Color(0.55f, 0.55f, 0.60f),
                EndingType.Slum           => new Color(0.30f, 0.25f, 0.25f),
                EndingType.BankruptEarly  => new Color(0.55f, 0.15f, 0.15f),
                EndingType.BankruptLate   => new Color(0.55f, 0.15f, 0.15f),
                _                         => Color.grey
            };
            Debug.LogWarning($"[EndingManager] No illustration wired for {ending} — drag the matching PNG from Assets/Texture/Ending Illustrations/ into the Inspector slot, or place a fallback in Resources/EndingArt/. Using placeholder tint.");
        }
    }

    private IEnumerator RunEndingSequence(EndingType ending)
    {
        if (fader != null)
        {
            fader.alpha = 0f;
            float t = 0f;
            while (t < fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                fader.alpha = Mathf.Clamp01(t / fadeInSeconds);
                yield return null;
            }
            fader.alpha = 1f;
        }

        string dialogue = GetDialogue(ending);
        if (dialogueTypewriter != null)
            yield return dialogueTypewriter.Play(dialogue);

        if (restartButton != null) restartButton.gameObject.SetActive(true);
        if (quitButton    != null) quitButton.gameObject.SetActive(true);
    }

    private string BuildStatsLine()
    {
        var gm = GameManager.Instance;
        if (gm == null) return "";
        int daysSurvived = Mathf.Min(gm.currentDay, GameManager.TOTAL_DAYS);
        return $"Days survived: {daysSurvived} · Letters: {gm.lettersReceived} "
             + $"· Peak reputation: {gm.peakReputation} · Wands crafted: {gm.wandsCrafted}";
    }

    private void OnRestart()
    {
        GameManager.Instance?.ResetForNewPlaythrough();
        GameManager.Instance?.LoadScene(GameManager.SCENE_MORNING);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Ending content ────────────────────────────────────────────

    private static string GetTitle(EndingType e) => e switch
    {
        EndingType.Royal         => "The Royal Wandmaker",
        EndingType.Rival         => "The Street with Two Shops",
        EndingType.Slum          => "The Wandmaker of the Moth",
        EndingType.BankruptEarly => "Short and Unlucky",
        EndingType.BankruptLate  => "So Close, and Then Not",
        _                        => "The End"
    };

    private static string GetDialogue(EndingType e) => e switch
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

        _ => "The end."
    };
}
