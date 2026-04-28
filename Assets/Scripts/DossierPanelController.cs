using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// TODO-EDITOR: Create PanelSettings asset
//   Project window > right-click Assets/UI/Dossier > Create > UI Toolkit > Panel Settings Asset.
//   Name it "DossierPanelSettings.asset". Scale Mode: Constant Pixel Size, Reference DPI 96.
//
// TODO-EDITOR: Wire CustomerGeneratorTest.unity
//   1. Create empty GameObject "DossierUIDocument" at the scene root.
//   2. Add component: UI Document
//        - Panel Settings : DossierPanelSettings.asset (created above).
//        - Source Asset   : Assets/UI/Dossier/DossierPanel.uxml
//   3. Add component: DossierPanelController
//        - Document : drag the UIDocument from this same GameObject.
//   4. On the existing CustomerGenerator GameObject, in the Inspector:
//        - Drag DossierUIDocument into the new "Dossier Panel" field.
//        - Leave the old "Memo Fill UI" field assigned for now (fallback during migration).
//   5. Disable (do NOT delete) the old uGUI dossier panel and memo panel under Canvas.
//      Delete only after end-to-end verification of the new panel.
//
// TODO-EDITOR: Optional font swap
//   Drop a serif TTF (e.g. EB Garamond, IM Fell English) into Assets/UI/Dossier/Fonts/
//   then edit the single `-unity-font-definition` line at the top of DossierPanel.uss.

/// <summary>
/// UI Toolkit replacement for the dossier panel + memo-fill gate.
/// Mirrors the gameplay of <see cref="MemoFillUI"/>: the player clicks a content
/// word inside the dossier prose to arm it, then clicks one of the three memo
/// slots to commit. When all three slots commit, <see cref="GameManager.currentMemo"/>
/// is populated and the supplied onComplete callback fires.
///
/// All chrome (parchment panel, gold corners, status bar) is authored in
/// DossierPanel.uxml + DossierPanel.uss; this controller only owns data binding,
/// per-word Button construction, and the arm/commit state machine.
/// </summary>
[DisallowMultipleComponent]
public class DossierPanelController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument that hosts DossierPanel.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Generate / Proceed buttons (UI Toolkit)")]
    [Tooltip("Optional: if assigned, clicking the in-panel \"Summon a customer\" button calls this.")]
    public CustomerGenerator customerGenerator;

    [Tooltip("If true, the in-panel Proceed button loads SCENE_MARKET on click.")]
    public bool wireProceedButton = true;

    // ── Source / commit data ─────────────────────────────────────────

    private enum SourceKey { Request, TrueGoal, Personality, Profession, School }

    private class Token
    {
        public string text;
        public bool   isWord;
        public bool   used;
    }

    private class SourceField
    {
        public SourceKey   key;
        public VisualElement container;
        public List<Token> tokens = new();
        public List<VisualElement> wordElements = new();   // 1:1 with tokens; null for non-word tokens
    }

    private readonly List<SourceField> _sources = new();
    private readonly Dictionary<PlayerMemoField, Commitment> _committed = new();

    private class Commitment
    {
        public SourceKey source;
        public int       tokenIndex;
        public string    word;
    }

    private SourceKey _armedSource;
    private int       _armedTokenIndex = -1;
    private string    _armedWord;
    private bool      _hasArmed;

    private Action _onComplete;

    // ── Cached element refs (resolved in Bootstrap) ──────────────────

    private VisualElement _root;
    private Label         _customerNameLabel;
    private Label         _constraintLabel;
    private Label         _statusLabel;
    private Button        _generateButton;
    private Button        _proceedButton;

    private VisualElement _schoolProse;
    private VisualElement _professionProse;
    private VisualElement _personalityProse;
    private VisualElement _requestProse;
    private VisualElement _trueGoalProse;

    private Button _purposeSlot;
    private Button _personalitySlot;
    private Button _elementSlot;

    private bool _bootstrapped;

    // ── Stop-words (mirror of MemoFillUI; keep in sync for parity) ───

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","so","for","of","to","in","on","at",
        "by","with","from","into","onto","as","is","are","was","were","be",
        "been","being","am","do","does","did","have","has","had",
        "i","me","my","mine","you","your","yours","he","him","his","she","her","hers",
        "it","its","we","us","our","ours","they","them","their","theirs",
        "this","that","these","those","then","than","there","here",
        "what","which","who","whom","whose","when","where","why","how",
        "if","else","not","no","yes",
        "can","could","would","should","will","may","might","must","shall",
        "one","two","three","too","very","just","also","even",
        "some","any","all","each","every","both","more","most","less","much","many","few",
        "own","same","such","only","other","another",
        "get","got","make","makes","made","up","down","out","over","under",
        "before","after","again","still","ever","never",
        "need","needs","needed","want","wants","use","uses","used","keep","keeps",
        "something","anything","nothing","everything","someone","anyone"
    };

    // ── Unity lifecycle ──────────────────────────────────────────────

    private void OnEnable()
    {
        Bootstrap();
    }

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root = document.rootVisualElement;

        _customerNameLabel = _root.Q<Label>("customerName");
        _constraintLabel   = _root.Q<Label>("constraintText");
        _statusLabel       = _root.Q<Label>("statusLabel");
        _generateButton    = _root.Q<Button>("generateButton");
        _proceedButton     = _root.Q<Button>("proceedButton");

        _schoolProse      = _root.Q<VisualElement>("schoolProse");
        _professionProse  = _root.Q<VisualElement>("professionProse");
        _personalityProse = _root.Q<VisualElement>("personalityProse");
        _requestProse     = _root.Q<VisualElement>("requestProse");
        _trueGoalProse    = _root.Q<VisualElement>("trueGoalProse");

        _purposeSlot     = _root.Q<Button>("purposeSlot");
        _personalitySlot = _root.Q<Button>("personalitySlot");
        _elementSlot     = _root.Q<Button>("elementSlot");

        if (_purposeSlot     != null) _purposeSlot.clicked     += () => OnSlotClicked(PlayerMemoField.Purpose);
        if (_personalitySlot != null) _personalitySlot.clicked += () => OnSlotClicked(PlayerMemoField.Personality);
        if (_elementSlot     != null) _elementSlot.clicked     += () => OnSlotClicked(PlayerMemoField.Element);

        if (_generateButton != null && customerGenerator != null)
            _generateButton.clicked += () => customerGenerator.GenerateCustomer();

        if (_proceedButton != null)
        {
            _proceedButton.SetEnabled(false);
            if (wireProceedButton)
                _proceedButton.clicked += () =>
                    GameManager.Instance?.LoadScene(GameManager.SCENE_MARKET);
        }

        _bootstrapped = true;
    }

    // ── Public API (signature mirrors MemoFillUI.Begin) ──────────────

    public void Begin(CustomerOrder order, Action onComplete)
    {
        Bootstrap();
        if (!_bootstrapped) return;

        _onComplete = onComplete;
        _committed.Clear();
        ClearArmed();
        _sources.Clear();

        if (order == null) return;

        if (_customerNameLabel != null) _customerNameLabel.text = string.IsNullOrEmpty(order.customerName) ? "—" : order.customerName;
        if (_constraintLabel   != null) _constraintLabel.text   = string.IsNullOrEmpty(order.constraint) ? "—" : order.constraint;

        BuildSource(SourceKey.School,      _schoolProse,      order.schoolOfMagic);
        BuildSource(SourceKey.Profession,  _professionProse,  order.profession);
        BuildSource(SourceKey.Personality, _personalityProse, order.personality);
        BuildSource(SourceKey.Request,     _requestProse,     order.request);
        BuildSource(SourceKey.TrueGoal,    _trueGoalProse,    order.trueGoal);

        ClearSlot(PlayerMemoField.Purpose);
        ClearSlot(PlayerMemoField.Personality);
        ClearSlot(PlayerMemoField.Element);

        if (_proceedButton != null) _proceedButton.SetEnabled(false);
        SetStatus("Read the dossier. Click words to fill the memo.");
    }

    public void SetStatus(string msg)
    {
        if (_statusLabel != null) _statusLabel.text = msg;
    }

    public void SetGenerateEnabled(bool v)
    {
        Bootstrap();
        if (_generateButton != null) _generateButton.SetEnabled(v);
    }

    // ── Tokenization (mirror of MemoFillUI; keep in sync) ────────────

    private static void Tokenize(string raw, List<Token> outTokens)
    {
        outTokens.Clear();
        int i = 0;
        while (i < raw.Length)
        {
            if (IsWordChar(raw[i]))
            {
                int start = i;
                while (i < raw.Length && IsWordChar(raw[i])) i++;
                string word = raw.Substring(start, i - start);
                bool eligible = word.Length >= 3 && !StopWords.Contains(word);
                outTokens.Add(new Token { text = word, isWord = eligible });
            }
            else
            {
                int start = i;
                while (i < raw.Length && !IsWordChar(raw[i])) i++;
                outTokens.Add(new Token { text = raw.Substring(start, i - start), isWord = false });
            }
        }
    }

    private static bool IsWordChar(char c) => char.IsLetter(c) || c == '\'' || c == '-';

    // ── Prose construction ───────────────────────────────────────────

    private void BuildSource(SourceKey key, VisualElement container, string raw)
    {
        if (container == null) return;
        container.Clear();

        var field = new SourceField { key = key, container = container };
        Tokenize(raw ?? "", field.tokens);

        for (int t = 0; t < field.tokens.Count; t++)
        {
            var tok = field.tokens[t];
            if (!tok.isWord)
            {
                var lbl = new Label(tok.text);
                lbl.AddToClassList("word-static");
                lbl.pickingMode = PickingMode.Ignore;
                container.Add(lbl);
                field.wordElements.Add(null);
                continue;
            }

            int capturedIndex = t;
            SourceKey capturedKey = key;
            var btn = new Button(() => OnWordClicked(capturedKey, capturedIndex));
            btn.text = tok.text;
            btn.AddToClassList("word-clickable");
            container.Add(btn);
            field.wordElements.Add(btn);
        }

        _sources.Add(field);
    }

    // ── Word click ───────────────────────────────────────────────────

    private void OnWordClicked(SourceKey src, int tokenIdx)
    {
        var field = _sources.Find(s => s.key == src);
        if (field == null || tokenIdx < 0 || tokenIdx >= field.tokens.Count) return;

        var tok = field.tokens[tokenIdx];
        if (tok.used) return;

        // Toggle: clicking the armed word again disarms.
        if (_hasArmed && _armedSource == src && _armedTokenIndex == tokenIdx)
        {
            ClearArmed();
            RefreshAllWordVisuals();
            return;
        }

        // Silent reject: every slot this word could fill is already committed.
        if (!HasEligibleEmptySlot(src)) return;

        _armedSource     = src;
        _armedTokenIndex = tokenIdx;
        _armedWord       = tok.text;
        _hasArmed        = true;
        RefreshAllWordVisuals();
    }

    private bool HasEligibleEmptySlot(SourceKey src)
    {
        for (int i = 0; i < 3; i++)
        {
            var slot = (PlayerMemoField)i;
            if (_committed.ContainsKey(slot)) continue;
            if (SlotAccepts(slot, src)) return true;
        }
        return false;
    }

    // ── Slot click ───────────────────────────────────────────────────

    private void OnSlotClicked(PlayerMemoField slot)
    {
        // Clicking a committed slot uncommits the word.
        if (_committed.TryGetValue(slot, out var existing))
        {
            var src = _sources.Find(s => s.key == existing.source);
            if (src != null && existing.tokenIndex >= 0 && existing.tokenIndex < src.tokens.Count)
                src.tokens[existing.tokenIndex].used = false;
            _committed.Remove(slot);
            ClearSlot(slot);
            RefreshAllWordVisuals();
            return;
        }

        if (!_hasArmed) return;

        if (!SlotAccepts(slot, _armedSource))
        {
            ShakeSlot(GetSlotButton(slot));
            return;
        }

        var sourceField = _sources.Find(s => s.key == _armedSource);
        if (sourceField != null && _armedTokenIndex >= 0 && _armedTokenIndex < sourceField.tokens.Count)
            sourceField.tokens[_armedTokenIndex].used = true;

        _committed[slot] = new Commitment
        {
            source     = _armedSource,
            tokenIndex = _armedTokenIndex,
            word       = _armedWord
        };
        SetSlotText(slot, _armedWord);
        SpawnInkBlot(slot);

        ClearArmed();
        RefreshAllWordVisuals();

        if (_committed.Count == 3)
        {
            var memo = new PlayerMemo
            {
                purpose     = _committed.TryGetValue(PlayerMemoField.Purpose,     out var p)  ? p.word  : "",
                personality = _committed.TryGetValue(PlayerMemoField.Personality, out var pe) ? pe.word : "",
                element     = _committed.TryGetValue(PlayerMemoField.Element,     out var el) ? el.word : ""
            };
            if (GameManager.Instance != null) GameManager.Instance.currentMemo = memo;
            if (_proceedButton != null) _proceedButton.SetEnabled(true);
            SetStatus("Memo complete. Proceed to the market.");
            _onComplete?.Invoke();
        }
    }

    private static bool SlotAccepts(PlayerMemoField slot, SourceKey src) => slot switch
    {
        PlayerMemoField.Purpose     => src == SourceKey.Request     || src == SourceKey.TrueGoal,
        PlayerMemoField.Personality => src == SourceKey.Personality || src == SourceKey.Profession,
        PlayerMemoField.Element     => src == SourceKey.School,
        _ => false
    };

    // ── Slot helpers ─────────────────────────────────────────────────

    private Button GetSlotButton(PlayerMemoField slot) => slot switch
    {
        PlayerMemoField.Purpose     => _purposeSlot,
        PlayerMemoField.Personality => _personalitySlot,
        PlayerMemoField.Element     => _elementSlot,
        _ => null
    };

    private void SetSlotText(PlayerMemoField slot, string word)
    {
        var btn = GetSlotButton(slot);
        if (btn == null) return;
        btn.text = word;
        btn.AddToClassList("memo-slot--filled");
    }

    private void ClearSlot(PlayerMemoField slot)
    {
        var btn = GetSlotButton(slot);
        if (btn == null) return;
        btn.text = "";
        btn.RemoveFromClassList("memo-slot--filled");
    }

    private void ClearArmed()
    {
        _hasArmed        = false;
        _armedTokenIndex = -1;
        _armedWord       = null;
    }

    // ── Word visual state ────────────────────────────────────────────

    private void RefreshAllWordVisuals()
    {
        foreach (var field in _sources)
        {
            for (int t = 0; t < field.tokens.Count; t++)
            {
                var ve = field.wordElements[t];
                if (ve == null) continue;
                var btn = ve as Button;
                if (btn == null) continue;
                var tok = field.tokens[t];

                btn.RemoveFromClassList("word-armed");
                btn.RemoveFromClassList("word-committed");

                if (tok.used)
                {
                    btn.AddToClassList("word-committed");
                    btn.text = "<s>" + tok.text + "</s>";
                    btn.SetEnabled(false);
                }
                else
                {
                    btn.text = tok.text;
                    btn.SetEnabled(true);
                    if (_hasArmed && _armedSource == field.key && _armedTokenIndex == t)
                        btn.AddToClassList("word-armed");
                }
            }
        }
    }

    // ── Visual feedback ──────────────────────────────────────────────

    private void ShakeSlot(VisualElement el)
    {
        if (el == null) return;
        const int totalMs = 300;
        const float amp = 6f;
        long started = DateTime.UtcNow.Ticks;

        el.schedule.Execute(() =>
        {
            float elapsed = (DateTime.UtcNow.Ticks - started) / 10000f;
            if (elapsed >= totalMs)
            {
                el.style.translate = new StyleTranslate(new Translate(0, 0));
                return;
            }
            float p = 1f - (elapsed / totalMs);
            float dx = Mathf.Sin(elapsed / 1000f * 60f) * amp * p;
            el.style.translate = new StyleTranslate(new Translate(dx, 0));
        }).Every(16).Until(() =>
            (DateTime.UtcNow.Ticks - started) / 10000f >= totalMs);
    }

    private void SpawnInkBlot(PlayerMemoField slot)
    {
        var btn = GetSlotButton(slot);
        if (btn == null) return;

        var blot = new VisualElement();
        blot.AddToClassList("ink-blot");
        blot.pickingMode = PickingMode.Ignore;
        blot.style.scale = new StyleScale(new Scale(new Vector3(0.1f, 0.1f, 1f)));
        btn.Add(blot);

        long startedTicks = DateTime.UtcNow.Ticks;
        const float riseMs = 150f, holdMs = 1200f, fadeMs = 500f;
        const float total = riseMs + holdMs + fadeMs;

        blot.schedule.Execute(() =>
        {
            float t = (DateTime.UtcNow.Ticks - startedTicks) / 10000f;
            if (t >= total)
            {
                if (blot.parent != null) blot.RemoveFromHierarchy();
                return;
            }
            if (t < riseMs)
            {
                float k = Mathf.Clamp01(t / riseMs);
                float s = Mathf.Lerp(0.1f, 1f, k);
                blot.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
                blot.style.opacity = 0.85f;
            }
            else if (t < riseMs + holdMs)
            {
                blot.style.scale = new StyleScale(new Scale(Vector3.one));
                blot.style.opacity = 0.85f;
            }
            else
            {
                float k = Mathf.Clamp01((t - riseMs - holdMs) / fadeMs);
                blot.style.opacity = Mathf.Lerp(0.85f, 0f, k);
            }
        }).Every(16).Until(() =>
            (DateTime.UtcNow.Ticks - startedTicks) / 10000f >= total);
    }
}
