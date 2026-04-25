using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TODO-EDITOR: Create a new scene "TitleScene" and make it the FIRST entry in
//   Build Settings (index 0 — this is what Unity loads when the player launches).
//   Scene hierarchy:
//     - GameManager (same DontDestroyOnLoad pattern as other scenes)
//     - EventSystem
//     - Canvas (Screen Space - Overlay)
//         - Background (full-screen Image — dark parchment or an illustration)
//         - TitleText (TMP_Text, large, centered upper-third — e.g. "The Wand Atelier")
//         - TaglineText (TMP_Text, smaller, below title — e.g. "Seven days to make a name.")
//         - ButtonsPanel (VerticalLayoutGroup, centered lower-third)
//             - StartButton (Button + TMP label "Start")
//             - ContinueButton (Button + TMP label "Continue" — optional, hidden for now)
//             - QuitButton (Button + TMP label "Quit")
//         - VersionText (TMP_Text, bottom-right, small, e.g. "v0.1")
//   Add TitleScreenUI component to Canvas (or a controller GO), wire:
//     titleText, taglineText, startButton, quitButton, (optional) versionText.
//   Start button loads SCENE_MORNING (which shows the Day 1 intro letter from the aunt).

/// <summary>
/// Title screen controller. Start button resets run state and jumps into the
/// Day 1 morning letter. Quit exits the application (or stops play mode in editor).
/// </summary>
[DisallowMultipleComponent]
public class TitleScreenUI : MonoBehaviour
{
    [Header("Text (optional — set in Inspector or leave blank)")]
    public TMP_Text titleText;
    public TMP_Text taglineText;
    public TMP_Text versionText;

    [Header("Buttons")]
    public Button startButton;
    public Button quitButton;

    [Header("Copy")]
    public string gameTitle   = "The Wand Atelier";
    public string tagline     = "Seven days to make a name. Or lose one.";
    public string versionLine = "v0.1";

    private void Start()
    {
        if (titleText   != null && !string.IsNullOrEmpty(gameTitle))   titleText.text   = gameTitle;
        if (taglineText != null && !string.IsNullOrEmpty(tagline))     taglineText.text = tagline;
        if (versionText != null && !string.IsNullOrEmpty(versionLine)) versionText.text = versionLine;

        startButton?.onClick.AddListener(OnStart);
        quitButton?.onClick.AddListener(OnQuit);
    }

    private void OnStart()
    {
        // Fresh run: clear any lingering state (gold, rep, inventory, day counter).
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
}
