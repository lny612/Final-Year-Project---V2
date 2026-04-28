using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Wire TitleScene.unity
//   1. Create asset Assets/UI/Title/TitlePanelSettings.asset
//        (UI Toolkit > Panel Settings, ConstantPixelSize, refDpi 96).
//   2. Add empty GameObject "TitleUIDocument" at scene root.
//   3. Add UIDocument: Panel Settings = TitlePanelSettings, Source = TitleScreen.uxml.
//   4. Add TitleScreenController on the same GameObject; Document is auto-resolved.
//   5. Disable (do not delete) the legacy uGUI Title widgets on Canvas.

/// <summary>
/// UI Toolkit replacement for <see cref="TitleScreenUI"/>. Drives the Start /
/// Quit flow on the parchment-style title screen.
/// </summary>
[DisallowMultipleComponent]
public class TitleScreenController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument hosting TitleScreen.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Copy")]
    public string gameTitle   = "The Wand\nAtelier";
    public string tagline     = "Seven days to make a name. Or lose one.";
    public string versionLine = "v0.1";

    private VisualElement _root;
    private Label  _titleLabel;
    private Label  _taglineLabel;
    private Label  _versionLabel;
    private Button _startButton;
    private Button _quitButton;

    private bool _bootstrapped;

    private void OnEnable() => Bootstrap();

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root = document.rootVisualElement;

        _titleLabel   = _root.Q<Label>("titleText");
        _taglineLabel = _root.Q<Label>("taglineText");
        _versionLabel = _root.Q<Label>("versionText");
        _startButton  = _root.Q<Button>("startButton");
        _quitButton   = _root.Q<Button>("quitButton");

        if (_titleLabel   != null && !string.IsNullOrEmpty(gameTitle))   _titleLabel.text   = gameTitle;
        if (_taglineLabel != null && !string.IsNullOrEmpty(tagline))     _taglineLabel.text = tagline;
        if (_versionLabel != null && !string.IsNullOrEmpty(versionLine)) _versionLabel.text = versionLine;

        if (_startButton != null) _startButton.clicked += OnStart;
        if (_quitButton  != null) _quitButton.clicked  += OnQuit;

        _bootstrapped = true;
    }

    private void OnStart()
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
}
