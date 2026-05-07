using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Persistent audio host. Plays one BGM track per scene with smooth
/// crossfades between tracks (and a no-op when adjacent scenes share a
/// track), plus a small PlayXxx API for SFX triggered by gameplay scripts.
///
/// One AudioManager GameObject lives in <c>0.TitleScene</c>. Like
/// <see cref="GameManager"/>, this script self-destroys duplicates so the
/// first instance carries forward via <c>DontDestroyOnLoad</c>.
///
/// AudioManager OWNS the only active AudioListener at runtime. Each scene
/// ships with its own AudioListener on the Main Camera (Unity warns if
/// missing) — at scene-load we disable those scene listeners so the
/// persistent one on the manager is the single source of truth. Without
/// this, a brief gap between scenes (old listener already destroyed, new
/// one not yet active) eats SFX fired during the new scene's OnEnable
/// (most visibly the morning-letter "ping" and the dossier highlighter).
///
/// Inspector wiring (done once via Unity MCP set_property — see CLAUDE.md):
/// drop the BGM + SFX clips from <c>Assets/Sound/</c> into the matching
/// fields. After that, no additional setup is needed in any other scene.
/// </summary>
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM (looping)")]
    [Tooltip("Title / Customer / Crafting / Minigame / Evaluation / Ending — the main theme.")]
    public AudioClip bgmMain;
    [Tooltip("Morning scene only.")]
    public AudioClip bgmMorning;
    [Tooltip("Market scene only.")]
    public AudioClip bgmMarket;

    [Header("SFX (one-shot)")]
    public AudioClip sfxButtonHover;
    public AudioClip sfxButtonDown;
    public AudioClip sfxLetterReceive;
    public AudioClip sfxHighlighter;
    public AudioClip sfxPurchase;
    public AudioClip sfxGateClear;
    public AudioClip sfxEvaluationReveal;
    [Tooltip("Plays when a material is dropped into a crafting slot.")]
    public AudioClip sfxMaterialSelection;

    [Header("SFX (looping)")]
    public AudioClip sfxTracingLoop;

    [Header("Volumes")]
    [Range(0f, 1f)] public float bgmVolume = 0.45f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    [Header("Crossfade")]
    [Tooltip("Seconds it takes to crossfade from the old BGM to the new BGM when scenes change.")]
    [Range(0f, 4f)] public float bgmCrossfadeDuration = 0.6f;

    // Two BGM sources so we can crossfade — one fades out while the other
    // fades in. _bgmActive points at whichever is currently the "live" track.
    private AudioSource _bgmA;
    private AudioSource _bgmB;
    private AudioSource _bgmActive;
    private AudioSource _sfxSource;
    private AudioSource _loopSource;
    private AudioListener _listener;

    private Coroutine _crossfadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Own the AudioListener ourselves so we don't depend on whichever
        // scene camera happens to be alive between scene transitions.
        _listener = gameObject.GetComponent<AudioListener>();
        if (_listener == null) _listener = gameObject.AddComponent<AudioListener>();
        _listener.enabled = true;

        _bgmA      = ConfigureSource(loop: true,  volume: 0f);
        _bgmB      = ConfigureSource(loop: true,  volume: 0f);
        _sfxSource = ConfigureSource(loop: false, volume: sfxVolume);
        _loopSource = ConfigureSource(loop: true,  volume: sfxVolume);
        _bgmActive = _bgmA;

        // Force every wired clip into RAM up-front. With preloadAudioData=0
        // (the default for our imported clips), the FIRST PlayOneShot call
        // can race against the async load and produce silence — exactly the
        // symptom we'd see for the morning letter (single-shot at scene
        // load) but not for the cash register (later in the run, by which
        // time other clips have already been loaded).
        PreloadAllClips();

        SceneManager.sceneLoaded += OnSceneLoaded;
        // Run for the current scene so launching from any scene primes BGM.
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private AudioSource ConfigureSource(bool loop, float volume)
    {
        var src              = gameObject.AddComponent<AudioSource>();
        src.loop             = loop;
        src.playOnAwake      = false;
        // Force 2D playback regardless of how the imported clip is flagged
        // (most of our clips have `3D: 1` in their .meta, which would otherwise
        // make a freshly-added AudioSource attenuate by listener distance).
        src.spatialBlend     = 0f;
        src.bypassEffects    = false;
        src.bypassListenerEffects = false;
        src.bypassReverbZones     = true;
        src.volume           = volume;
        return src;
    }

    private void PreloadAllClips()
    {
        AudioClip[] clips = {
            bgmMain, bgmMorning, bgmMarket,
            sfxButtonHover, sfxButtonDown, sfxLetterReceive,
            sfxHighlighter, sfxPurchase, sfxGateClear,
            sfxEvaluationReveal, sfxMaterialSelection, sfxTracingLoop
        };
        foreach (var c in clips)
        {
            if (c == null) continue;
            // Both LoadAudioData and a no-op access prime the streamed clip.
            // LoadAudioData is the canonical call; the bool result is ignored
            // because failure here is non-fatal (PlayOneShot will still load
            // on demand later, just possibly silently the first time).
            c.LoadAudioData();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A loop-SFX (e.g. the tracing-minigame ambience) doesn't survive scene
        // transitions — the scene that owns it owns the lifecycle too.
        StopLoopSfx();

        // Disable any AudioListener that the new scene shipped with so ours
        // remains the single active listener. Doing this on every scene-load
        // is cheap and survives users adding more cameras later.
        DisableSceneListeners(scene);

        // Always re-hook scene buttons even if the BGM stays the same; new
        // scenes have new buttons that need hover/click sounds.
        HookAllSceneButtons(scene);

        var target = SelectBgmFor(scene.name);
        SwitchBgm(target);
    }

    private void DisableSceneListeners(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var l in root.GetComponentsInChildren<AudioListener>(true))
            {
                if (l == _listener) continue;
                l.enabled = false;
            }
        }
    }

    /// <summary>
    /// Smoothly transition from the current BGM to <paramref name="target"/>.
    /// If the same track is already playing, this is a no-op so the music
    /// doesn't restart on identical-BGM scene transitions (e.g. Customer→Crafting).
    /// </summary>
    private void SwitchBgm(AudioClip target)
    {
        // Same track + already playing → keep it looping uninterrupted.
        if (_bgmActive != null && _bgmActive.clip == target && _bgmActive.isPlaying)
            return;

        if (_crossfadeRoutine != null) StopCoroutine(_crossfadeRoutine);
        _crossfadeRoutine = StartCoroutine(CrossfadeTo(target));
    }

    private IEnumerator CrossfadeTo(AudioClip target)
    {
        var fadeOut = _bgmActive;
        var fadeIn  = (_bgmActive == _bgmA) ? _bgmB : _bgmA;
        _bgmActive  = fadeIn;

        // Prime the incoming source.
        if (target != null)
        {
            fadeIn.clip   = target;
            fadeIn.volume = 0f;
            fadeIn.Play();
        }
        else
        {
            fadeIn.Stop();
            fadeIn.clip = null;
        }

        float startOutVol = fadeOut != null ? fadeOut.volume : 0f;
        float t = 0f;
        float dur = Mathf.Max(0.01f, bgmCrossfadeDuration);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(startOutVol, 0f, k);
            if (target  != null) fadeIn.volume  = Mathf.Lerp(0f, bgmVolume, k);
            yield return null;
        }

        if (fadeOut != null)
        {
            fadeOut.volume = 0f;
            fadeOut.Stop();
            fadeOut.clip = null;
        }
        if (target != null) fadeIn.volume = bgmVolume;

        _crossfadeRoutine = null;
    }

    private AudioClip SelectBgmFor(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return bgmMain;
        // Scene files are prefixed with their build index (e.g. "1.MorningScene").
        // Match by suffix so the prefix never breaks the lookup.
        if (sceneName.EndsWith("MorningScene"))           return bgmMorning;
        if (sceneName.EndsWith("MaterialGeneratorTest"))  return bgmMarket;
        return bgmMain;
    }

    // ── Public SFX API ────────────────────────────────────────────

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxSource == null) return;
        // Re-assert per-call in case the user adjusted sfxVolume in the
        // Inspector at runtime — the source.volume × volumeScale product is
        // what PlayOneShot multiplies against.
        _sfxSource.volume = sfxVolume;
        _sfxSource.PlayOneShot(clip, volumeScale);
    }

    public void PlayButtonHover()       => PlaySfx(sfxButtonHover, 0.7f);
    public void PlayButtonDown()        => PlaySfx(sfxButtonDown);
    public void PlayLetterReceive()     => PlaySfx(sfxLetterReceive);
    public void PlayHighlighter()       => PlaySfx(sfxHighlighter, 0.8f);
    public void PlayPurchase()          => PlaySfx(sfxPurchase);
    public void PlayGateClear()         => PlaySfx(sfxGateClear);
    public void PlayEvaluationReveal()  => PlaySfx(sfxEvaluationReveal);
    public void PlayMaterialSelection() => PlaySfx(sfxMaterialSelection);

    public void StartTracingLoop()
    {
        if (sfxTracingLoop == null || _loopSource == null) return;
        if (_loopSource.clip == sfxTracingLoop && _loopSource.isPlaying) return;
        _loopSource.clip   = sfxTracingLoop;
        _loopSource.volume = sfxVolume;
        _loopSource.Play();
    }

    public void StopTracingLoop() => StopLoopSfx();

    private void StopLoopSfx()
    {
        if (_loopSource != null && _loopSource.isPlaying) _loopSource.Stop();
    }

    // ── Auto button hook-up ───────────────────────────────────────

    private void HookAllSceneButtons(Scene scene)
    {
        // uGUI Buttons that live on active GameObjects in this scene.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var btn in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (btn == null) continue;
                btn.onClick.AddListener(PlayButtonDown);
                AttachUguiHover(btn);
            }

            foreach (var doc in root.GetComponentsInChildren<UIDocument>(true))
            {
                HookUiToolkitButtons(doc);
            }
        }
    }

    private void AttachUguiHover(UnityEngine.UI.Button btn)
    {
        var trigger = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        // Avoid stacking the same callback if a previous scene-load already added it.
        for (int i = 0; i < trigger.triggers.Count; i++)
            if (trigger.triggers[i].eventID == UnityEngine.EventSystems.EventTriggerType.PointerEnter
                && trigger.triggers[i].callback?.GetPersistentEventCount() == 0)
                return;
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry
        {
            eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
        };
        entry.callback.AddListener(_ => PlayButtonHover());
        trigger.triggers.Add(entry);
    }

    private void HookUiToolkitButtons(UIDocument doc)
    {
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;
        // Pointer events are the canonical UI-Toolkit input pipeline in Unity 6
        // (Mouse* events are legacy and don't fire reliably for all panels).
        // Re-bind on every scene-load — Unregister-then-Register dedups even
        // when a previous scene-load already attached the callback.
        root.UnregisterCallback<PointerEnterEvent>(OnUtkPointerEnter);
        root.RegisterCallback<PointerEnterEvent>(OnUtkPointerEnter, TrickleDown.TrickleDown);
        root.UnregisterCallback<PointerDownEvent>(OnUtkPointerDown);
        root.RegisterCallback<PointerDownEvent>(OnUtkPointerDown, TrickleDown.TrickleDown);
    }

    private void OnUtkPointerEnter(PointerEnterEvent evt)
    {
        if (HasButtonAncestor(evt.target as VisualElement)) PlayButtonHover();
    }

    private void OnUtkPointerDown(PointerDownEvent evt)
    {
        // Only fire on left-click (button 0) to mirror onClick semantics and
        // avoid double-firing for context menus / drag interactions.
        if (evt.button != 0) return;
        if (HasButtonAncestor(evt.target as VisualElement)) PlayButtonDown();
    }

    private static bool HasButtonAncestor(VisualElement ve)
    {
        // Pointer events fire on the deepest hit child (a Button's text Label,
        // for instance). Walk up to find the enclosing Button.
        while (ve != null)
        {
            if (ve is Button) return true;
            ve = ve.parent;
        }
        return false;
    }
}
