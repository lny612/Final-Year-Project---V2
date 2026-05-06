using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Wire TitleScene.unity
//   1. Create asset Assets/UI/Title/TitlePanelSettings.asset
//        (UI Toolkit > Panel Settings, ConstantPixelSize, refDpi 96).
//   2. Add empty GameObject "TitleUIDocument" at scene root.
//   3. Add UIDocument: Panel Settings = TitlePanelSettings, Source = TitleScreen.uxml.
//   4. Add TitleScreenController on the same GameObject; Document is auto-resolved.
//   5. Disable (do not delete) the legacy uGUI Title widgets on Canvas.
//   6. Drag Assets/Texture/Title Scene Image.png into the Background Image field
//        on TitleScreenController in the Inspector.

/// <summary>
/// UI Toolkit replacement for <see cref="TitleScreenUI"/>. Drives the
/// Start / Developer / Quit flow on the painted-illustration title screen.
/// </summary>
[DisallowMultipleComponent]
public class TitleScreenController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument hosting TitleScreen.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Background")]
    [Tooltip("Painted shop illustration. Drag Assets/Texture/Title Scene Image.png here.")]
    public Texture2D backgroundImage;

    [Header("Fonts")]
    [Tooltip("Font for the main title (Assets/Fonts/Fantasia.ttf).")]
    public Font titleFont;
    [Tooltip("Font for the menu buttons (Assets/Fonts/The Garden of Lights.ttf).")]
    public Font buttonFont;

    [Header("Copy")]
    public string gameTitle   = "The Wand\nAtelier";
    public string tagline     = "Seven days to make a name. Or lose one.";
    public string versionLine = "v0.1";

    private VisualElement _root;
    private VisualElement _bgImage;
    private Label  _titleLabel;
    private Label  _taglineLabel;
    private Label  _versionLabel;
    private Button _startButton;
    private Button _developerButton;
    private Button _quitButton;
    private VisualElement _developerPanel;
    private VisualElement _developerBackdrop;
    private Button _developerCloseButton;

    private bool _bootstrapped;

    private void OnEnable() => Bootstrap();

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root = document.rootVisualElement;

        _bgImage              = _root.Q<VisualElement>("bgImage");
        _titleLabel           = _root.Q<Label>("titleText");
        _taglineLabel         = _root.Q<Label>("taglineText");
        _versionLabel         = _root.Q<Label>("versionText");
        _startButton          = _root.Q<Button>("startButton");
        _developerButton      = _root.Q<Button>("developerButton");
        _quitButton           = _root.Q<Button>("quitButton");
        _developerPanel       = _root.Q<VisualElement>("developerPanel");
        _developerBackdrop    = _root.Q<VisualElement>("developerBackdrop");
        _developerCloseButton = _root.Q<Button>("developerCloseButton");

        if (_bgImage != null && backgroundImage != null)
            _bgImage.style.backgroundImage = new StyleBackground(backgroundImage);

        if (titleFont != null)
        {
            var titleDef = new StyleFontDefinition(titleFont);
            if (_titleLabel != null) _titleLabel.style.unityFontDefinition = titleDef;
        }
        if (buttonFont != null)
        {
            var btnDef = new StyleFontDefinition(buttonFont);
            if (_startButton          != null) _startButton.style.unityFontDefinition          = btnDef;
            if (_developerButton      != null) _developerButton.style.unityFontDefinition      = btnDef;
            if (_quitButton           != null) _quitButton.style.unityFontDefinition           = btnDef;
            if (_developerCloseButton != null) _developerCloseButton.style.unityFontDefinition = btnDef;
        }

        if (_titleLabel   != null && !string.IsNullOrEmpty(gameTitle))   _titleLabel.text   = gameTitle;
        if (_taglineLabel != null && !string.IsNullOrEmpty(tagline))     _taglineLabel.text = tagline;
        if (_versionLabel != null && !string.IsNullOrEmpty(versionLine)) _versionLabel.text = versionLine;

        if (_startButton          != null) _startButton.clicked          += OnStart;
        if (_developerButton      != null) _developerButton.clicked      += OnDeveloper;
        if (_quitButton           != null) _quitButton.clicked           += OnQuit;
        if (_developerCloseButton != null) _developerCloseButton.clicked += OnDeveloperClose;

        if (_developerBackdrop != null)
            _developerBackdrop.RegisterCallback<ClickEvent>(_ => OnDeveloperClose());

        _bootstrapped = true;
    }

    private void OnStart()
    {
        GameManager.Instance?.ResetForNewPlaythrough();
        GameManager.Instance?.LoadScene(GameManager.SCENE_MORNING);
    }

    private void OnDeveloper()      => SetDeveloperPanelVisible(true);
    private void OnDeveloperClose() => SetDeveloperPanelVisible(false);

    private void SetDeveloperPanelVisible(bool visible)
    {
        if (_developerPanel == null) return;
        if (visible) _developerPanel.RemoveFromClassList("hidden");
        else         _developerPanel.AddToClassList("hidden");
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
