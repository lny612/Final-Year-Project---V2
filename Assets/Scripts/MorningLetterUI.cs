using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TODO-EDITOR: Create a new scene "MorningScene" and add it to Build Settings
//   AFTER the existing 4 scenes (Customer/Market/Crafting/Evaluation).
//   Scene hierarchy:
//     - GameManager (same prefab/GO pattern as other scenes)
//     - EventSystem
//     - Canvas (Screen Space - Overlay)
//         - Background (Image, parchment tone, stretch-fill)
//         - OwlImage (Image, top-center, static owl silhouette or PNG)
//         - LetterBG (Image, centered, letter-paper rectangle)
//             - FromText (TMP_Text, top of letter — "from · sender")
//             - SubjectText (TMP_Text, below FromText, bold, larger)
//             - BodyText (TMP_Text, below subject, body font, typewriter target)
//                 → add TypewriterText component, set target = BodyText
//             - WaxSeal (Image, small square/circle tinted by sender type)
//         - ContinueButton (Button with TMP label "Read more" / "Continue")
//   Add MorningLetterUI component to a GO in the scene (Canvas or a controller GO)
//   and wire all fields. The component will fetch the day+rep-tiered letter from
//   LetterLibrary on Start() and drive the typewriter.

/// <summary>
/// Scene controller for MorningScene. Fetches the morning letter for the
/// current day/reputation, displays owl + typewritten letter, then on Continue
/// either shows the rent-reminder follow-up letter (days 3 and 6) or loads
/// the customer scene.
/// </summary>
[DisallowMultipleComponent]
public class MorningLetterUI : MonoBehaviour
{
    [Header("Letter content")]
    public TMP_Text fromText;
    public TMP_Text subjectText;
    public TypewriterText bodyTypewriter;

    [Header("Visuals")]
    public Image owlImage;
    public Image waxSeal;

    [Header("Flow")]
    public Button continueButton;

    [Header("Seal colors per sender type")]
    public Color sealNeighbor   = new Color(0.95f, 0.88f, 0.72f); // cream
    public Color sealCustomer   = new Color(0.82f, 0.55f, 0.35f); // terracotta
    public Color sealAristocrat = new Color(0.92f, 0.78f, 0.25f); // gold
    public Color sealRoyal      = new Color(0.42f, 0.18f, 0.55f); // royal purple
    public Color sealBrigand    = new Color(0.15f, 0.15f, 0.15f); // black
    public Color sealLandlord   = new Color(0.55f, 0.55f, 0.55f); // grey
    public Color sealAunt       = new Color(0.80f, 0.20f, 0.25f); // crimson
    public Color sealRival      = new Color(0.30f, 0.45f, 0.30f); // moss

    private LetterContent _main;
    private LetterContent _followup;
    private bool          _hasFollowup;
    private bool          _showingFollowup;

    private void Start()
    {
        var gm = GameManager.Instance;
        int day = gm != null ? gm.currentDay : 1;
        int rep = gm != null ? gm.playerReputation : 0;

        _main = LetterLibrary.GetMorningLetter(day, LetterLibrary.GetRepTier(rep, day));

        // Rent-due mornings queue a second landlord letter after the main one.
        if (gm != null && System.Array.IndexOf(GameManager.RENT_DUE_DAYS, day) >= 0)
        {
            _followup = LetterLibrary.GetRentReminder(day, gm.GetRentDueToday());
            _hasFollowup = true;
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (bodyTypewriter != null)
            bodyTypewriter.OnComplete += OnTypewriterComplete;

        ShowLetter(_main);
        if (gm != null) gm.lettersReceived++;
    }

    private void OnDestroy()
    {
        if (bodyTypewriter != null) bodyTypewriter.OnComplete -= OnTypewriterComplete;
    }

    private void ShowLetter(LetterContent letter)
    {
        if (fromText    != null) fromText.text    = "— " + letter.from;
        if (subjectText != null) subjectText.text = letter.subject;
        if (waxSeal     != null) waxSeal.color    = GetSealColor(letter.sender);
        if (bodyTypewriter != null) StartCoroutine(bodyTypewriter.Play(letter.body));
    }

    private void OnTypewriterComplete()
    {
        if (continueButton != null) continueButton.gameObject.SetActive(true);
    }

    private void OnContinueClicked()
    {
        if (_hasFollowup && !_showingFollowup)
        {
            _showingFollowup = true;
            if (continueButton != null) continueButton.gameObject.SetActive(false);
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
            _                       => Color.white
        };
    }
}
