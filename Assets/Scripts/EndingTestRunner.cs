using UnityEngine;

/// <summary>
/// Drop-on-empty-GO ending tester. Renders a tiny IMGUI panel with one
/// button per ending variant; each button forces the matching GameManager
/// state and loads EndingScene. Bootstraps a GameManager if the scene
/// doesn't already have one (so SampleScene can host this without any
/// other wiring). Editor-and-development-build only.
///
/// Use:
///   - Open Assets/Scenes/SampleScene.unity
///   - Add an empty GameObject with this script
///   - Make sure SampleScene + EndingScene are both in Build Settings
///   - Press Play and click any of the five ending buttons.
/// </summary>
[DisallowMultipleComponent]
public class EndingTestRunner : MonoBehaviour
{
    [Tooltip("Stats values to seed on the GameManager so the ending screen's stats line has something to show.")]
    public int testWandsCrafted    = 7;
    public int testLettersReceived = 8;

    private void Awake()
    {
        if (GameManager.Instance == null)
        {
            var go = new GameObject("GameManager (test)");
            go.AddComponent<GameManager>();
        }
    }

    private void OnGUI()
    {
        const int W = 320;
        const int H = 360;
        var rect = new Rect(20, 20, W, H);
        GUI.Box(rect, "Ending Tester");

        GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 28, W - 24, H - 36));
        GUILayout.Label("Click an ending to load EndingScene with the matching GameManager state.");
        GUILayout.Space(6);

        if (Button("Royal      (rep ≥ 90)"))         LoadEnding(EndingType.Royal);
        if (Button("Rival      (rep 30-89)"))        LoadEnding(EndingType.Rival);
        if (Button("Slum       (rep < 30)"))         LoadEnding(EndingType.Slum);
        if (Button("Bankrupt — Early (day 3)"))      LoadEnding(EndingType.BankruptEarly);
        if (Button("Bankrupt — Late  (day 6)"))      LoadEnding(EndingType.BankruptLate);

        GUILayout.Space(8);
        GUILayout.Label($"Day: {GameManager.Instance?.currentDay}   Rep: {GameManager.Instance?.playerReputation}   Gold: {GameManager.Instance?.playerGold}");
        GUILayout.EndArea();
    }

    private static bool Button(string label) => GUILayout.Button(label, GUILayout.Height(36));

    private void LoadEnding(EndingType ending)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Common stats so the EndingManager's stats line isn't empty.
        gm.wandsCrafted    = testWandsCrafted;
        gm.lettersReceived = testLettersReceived;

        switch (ending)
        {
            case EndingType.Royal:
                gm.currentDay        = GameManager.TOTAL_DAYS;   // not bankrupt path
                gm.playerReputation  = 100;
                gm.peakReputation    = 100;
                gm.playerGold        = 1200;
                gm.bankruptedOnDay3  = false;
                break;

            case EndingType.Rival:
                gm.currentDay        = GameManager.TOTAL_DAYS;
                gm.playerReputation  = 55;
                gm.peakReputation    = 70;
                gm.playerGold        = 600;
                gm.bankruptedOnDay3  = false;
                break;

            case EndingType.Slum:
                gm.currentDay        = GameManager.TOTAL_DAYS;
                gm.playerReputation  = 10;
                gm.peakReputation    = 18;
                gm.playerGold        = 80;
                gm.bankruptedOnDay3  = false;
                break;

            case EndingType.BankruptEarly:
                gm.currentDay        = 3;
                gm.bankruptedOnDay3  = true;
                gm.playerReputation  = 5;
                gm.peakReputation    = 12;
                gm.playerGold        = 0;
                break;

            case EndingType.BankruptLate:
                gm.currentDay        = 6;
                gm.bankruptedOnDay3  = false;
                gm.playerReputation  = 22;
                gm.peakReputation    = 40;
                gm.playerGold        = 0;
                break;
        }

        gm.LoadScene(GameManager.SCENE_ENDING);
    }
}
