using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Active-reading gate for CustomerGeneratorTest. The player distils the dossier
/// into a 3-entry memo by clicking content words inside the prose and assigning
/// each to Purpose / Personality / Element slots. The CustomerGenerator Proceed
/// button is enabled only when all three slots commit.
///
/// Accepted sources per slot:
///   Purpose     &lt;- request, trueGoal
///   Personality &lt;- personality, profession
///   Element     &lt;- schoolOfMagic
///
/// Mis-targeting shakes the slot. The committed memo lands on
/// GameManager.currentMemo and drives downstream match hints in
/// MaterialGenerator + the pinned memo card in CraftingScene.
/// </summary>
[DisallowMultipleComponent]
public class MemoFillUI : MonoBehaviour
{
    [Header("Source prose (re-reference the 5 existing dossier TMP fields)")]
    public TMP_Text requestText;
    public TMP_Text trueGoalText;
    public TMP_Text personalityText;
    public TMP_Text professionText;
    public TMP_Text schoolText;

    [Header("Memo slot values (player's committed words)")]
    public TMP_Text purposeSlotText;
    public TMP_Text personalitySlotText;
    public TMP_Text elementSlotText;

    [Header("Memo slot buttons (overlay the slot rows so the whole row is clickable)")]
    public Button purposeSlotButton;
    public Button personalitySlotButton;
    public Button elementSlotButton;

    [Header("Colors")]
    [Tooltip("Tint for the armed word (the one that will commit on next slot click).")]
    public Color armedWordColor = new Color32(0xC5, 0x8A, 0x1F, 0xFF);

    [Tooltip("Tint for available (un-used, un-armed) content words.")]
    public Color linkColor = new Color32(0x2E, 0x55, 0x8C, 0xFF);

    [Tooltip("Tint for words that have already been committed to a slot.")]
    public Color usedColor = new Color32(0x88, 0x88, 0x88, 0xFF);

    // ── Data ─────────────────────────────────────────────────────────

    private enum SourceKey { Request, TrueGoal, Personality, Profession, School }

    private class Token
    {
        public string text;
        public bool   isWord;   // only true for eligible content words
        public bool   used;     // set when a word has been committed to a slot
    }

    private class SourceField
    {
        public SourceKey key;
        public TMP_Text  tmp;
        public string    labelPrefix;   // e.g. "<b>REQUEST:</b> "
        public List<Token> tokens = new();
    }

    private readonly List<SourceField> _sources = new();

    private class Commitment
    {
        public SourceKey source;
        public int       tokenIndex;
        public string    word;
    }

    private readonly Dictionary<PlayerMemoField, Commitment> _committed = new();

    private SourceKey _armedSource;
    private int       _armedTokenIndex = -1;
    private string    _armedWord;
    private bool      _hasArmed;

    private Action _onComplete;

    // ── Stop-words (case-insensitive) ────────────────────────────────

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

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Starts (or re-starts) the fill-the-memo interaction for a given customer.
    /// Call once after the customer dossier is populated in CustomerGenerator.
    /// </summary>
    public void Begin(CustomerOrder order, Action onComplete)
    {
        _onComplete = onComplete;
        _committed.Clear();
        ClearArmed();
        _sources.Clear();

        if (order == null) return;

        AddSource(SourceKey.Request,     requestText,     order.request,       "<b>REQUEST:</b> ");
        AddSource(SourceKey.TrueGoal,    trueGoalText,    order.trueGoal,      "<b>TRUE GOAL:</b> ");
        AddSource(SourceKey.Personality, personalityText, order.personality,   "<b>PERSONALITY:</b> ");
        AddSource(SourceKey.Profession,  professionText,  order.profession,    "<b>PROFESSION:</b> ");
        AddSource(SourceKey.School,      schoolText,      order.schoolOfMagic, "<b>SCHOOL OF MAGIC:</b> ");

        ClearSlotText(PlayerMemoField.Purpose);
        ClearSlotText(PlayerMemoField.Personality);
        ClearSlotText(PlayerMemoField.Element);

        WireSlotButtons();
        WireSourceClicks();
        RenderAllSources();
    }

    // ── Tokenization ─────────────────────────────────────────────────

    private void AddSource(SourceKey key, TMP_Text tmp, string raw, string labelPrefix)
    {
        if (tmp == null) return;
        var field = new SourceField { key = key, tmp = tmp, labelPrefix = labelPrefix };
        Tokenize(raw ?? "", field.tokens);
        _sources.Add(field);
    }

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

    // ── Rendering ────────────────────────────────────────────────────

    private void RenderAllSources()
    {
        foreach (var f in _sources) RenderSource(f);
    }

    private void RenderSource(SourceField field)
    {
        var sb = new StringBuilder(256);
        if (!string.IsNullOrEmpty(field.labelPrefix)) sb.Append(field.labelPrefix);

        string linkHex  = ColorUtility.ToHtmlStringRGB(linkColor);
        string armedHex = ColorUtility.ToHtmlStringRGB(armedWordColor);
        string usedHex  = ColorUtility.ToHtmlStringRGB(usedColor);

        for (int t = 0; t < field.tokens.Count; t++)
        {
            var tok = field.tokens[t];
            if (!tok.isWord)
            {
                sb.Append(tok.text);
                continue;
            }

            if (tok.used)
            {
                sb.Append("<color=#").Append(usedHex).Append("><s>")
                  .Append(tok.text)
                  .Append("</s></color>");
                continue;
            }

            bool isArmed = _hasArmed && _armedSource == field.key && _armedTokenIndex == t;
            string hex = isArmed ? armedHex : linkHex;
            sb.Append("<link=\"").Append((int)field.key).Append(':').Append(t).Append("\">")
              .Append("<u><color=#").Append(hex).Append('>')
              .Append(tok.text)
              .Append("</color></u></link>");
        }

        field.tmp.text = sb.ToString();
    }

    // ── Source click routing ─────────────────────────────────────────

    private void WireSourceClicks()
    {
        foreach (var f in _sources)
        {
            var relay = f.tmp.gameObject.GetComponent<MemoLinkClickRelay>();
            if (relay == null) relay = f.tmp.gameObject.AddComponent<MemoLinkClickRelay>();
            relay.Configure(f.tmp, OnSourceClicked);
            f.tmp.raycastTarget = true;
        }
    }

    private void OnSourceClicked(TMP_Text tmp, Vector2 screenPos)
    {
        // For Screen Space Overlay canvas, passing null camera is correct.
        // TODO-EDITOR: if the Canvas uses Screen Space - Camera, pass the event camera here instead.
        int linkIdx = TMP_TextUtilities.FindIntersectingLink(tmp, screenPos, null);
        if (linkIdx < 0) return;

        string linkId = tmp.textInfo.linkInfo[linkIdx].GetLinkID();
        if (!TryParseLinkId(linkId, out SourceKey src, out int tokenIdx)) return;

        var field = _sources.Find(s => s.key == src);
        if (field == null || tokenIdx < 0 || tokenIdx >= field.tokens.Count) return;
        var tok = field.tokens[tokenIdx];
        if (tok.used) return;

        // Toggle: clicking the armed word again disarms.
        if (_hasArmed && _armedSource == src && _armedTokenIndex == tokenIdx)
        {
            ClearArmed();
            RenderAllSources();
            return;
        }

        // Silent reject: if every slot this word could fill is already committed.
        if (!HasEligibleEmptySlot(src))
        {
            return;
        }

        _armedSource     = src;
        _armedTokenIndex = tokenIdx;
        _armedWord       = tok.text;
        _hasArmed        = true;
        RenderAllSources();
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

    private static bool TryParseLinkId(string id, out SourceKey src, out int tokenIdx)
    {
        src = SourceKey.Request;
        tokenIdx = -1;
        if (string.IsNullOrEmpty(id)) return false;
        int colon = id.IndexOf(':');
        if (colon <= 0 || colon >= id.Length - 1) return false;
        if (!int.TryParse(id.Substring(0, colon), out int rawKey)) return false;
        if (!int.TryParse(id.Substring(colon + 1), out tokenIdx)) return false;
        src = (SourceKey)rawKey;
        return true;
    }

    // ── Slot buttons ─────────────────────────────────────────────────

    private void WireSlotButtons()
    {
        HookSlot(purposeSlotButton,     PlayerMemoField.Purpose);
        HookSlot(personalitySlotButton, PlayerMemoField.Personality);
        HookSlot(elementSlotButton,     PlayerMemoField.Element);
    }

    private void HookSlot(Button btn, PlayerMemoField slot)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnSlotClicked(slot));
    }

    private void OnSlotClicked(PlayerMemoField slot)
    {
        // Clicking a committed slot uncommits the word.
        if (_committed.TryGetValue(slot, out var existing))
        {
            var src = _sources.Find(s => s.key == existing.source);
            if (src != null && existing.tokenIndex >= 0 && existing.tokenIndex < src.tokens.Count)
                src.tokens[existing.tokenIndex].used = false;
            _committed.Remove(slot);
            ClearSlotText(slot);
            RenderAllSources();
            return;
        }

        if (!_hasArmed) return;

        if (!SlotAccepts(slot, _armedSource))
        {
            StartCoroutine(ShakeSlot(GetSlotTextForRef(slot)?.transform));
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
        RenderAllSources();

        if (_committed.Count == 3)
        {
            var memo = new PlayerMemo
            {
                purpose     = _committed.TryGetValue(PlayerMemoField.Purpose,     out var p) ? p.word : "",
                personality = _committed.TryGetValue(PlayerMemoField.Personality, out var pe) ? pe.word : "",
                element     = _committed.TryGetValue(PlayerMemoField.Element,     out var el) ? el.word : ""
            };
            if (GameManager.Instance != null) GameManager.Instance.currentMemo = memo;
            _onComplete?.Invoke();
        }
    }

    // ── Source -> slot mapping ───────────────────────────────────────

    private static bool SlotAccepts(PlayerMemoField slot, SourceKey src) => slot switch
    {
        PlayerMemoField.Purpose     => src == SourceKey.Request     || src == SourceKey.TrueGoal,
        PlayerMemoField.Personality => src == SourceKey.Personality || src == SourceKey.Profession,
        PlayerMemoField.Element     => src == SourceKey.School,
        _ => false
    };

    // ── Slot value helpers ───────────────────────────────────────────

    private TMP_Text GetSlotTextForRef(PlayerMemoField slot) => slot switch
    {
        PlayerMemoField.Purpose     => purposeSlotText,
        PlayerMemoField.Personality => personalitySlotText,
        PlayerMemoField.Element     => elementSlotText,
        _ => null
    };

    private void SetSlotText(PlayerMemoField slot, string word)
    {
        var t = GetSlotTextForRef(slot);
        if (t != null) t.text = word;
    }

    private void ClearSlotText(PlayerMemoField slot)
    {
        var t = GetSlotTextForRef(slot);
        if (t != null) t.text = "";
    }

    private void ClearArmed()
    {
        _hasArmed        = false;
        _armedTokenIndex = -1;
        _armedWord       = null;
    }

    // ── Visual feedback ──────────────────────────────────────────────

    private IEnumerator ShakeSlot(Transform t)
    {
        if (t == null) yield break;
        Vector3 orig = t.localPosition;
        const float dur = 0.3f, amp = 6f;
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float p = 1f - (e / dur);
            t.localPosition = orig + new Vector3(Mathf.Sin(e * 60f) * amp * p, 0f, 0f);
            yield return null;
        }
        t.localPosition = orig;
    }

    private void SpawnInkBlot(PlayerMemoField slot)
    {
        var slotText = GetSlotTextForRef(slot);
        if (slotText == null) return;

        var go = new GameObject("InkBlot", typeof(RectTransform));
        go.transform.SetParent(slotText.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-10f, 0f);
        rt.sizeDelta = new Vector2(14f, 14f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.18f, 0.85f);
        img.raycastTarget = false;
        go.transform.SetAsFirstSibling();
        StartCoroutine(InkBlotPop(rt, img));
    }

    private IEnumerator InkBlotPop(RectTransform rt, Image img)
    {
        const float rise = 0.15f, hold = 1.2f, fade = 0.5f;
        Color baseColor = img.color;

        float e = 0f;
        while (e < rise)
        {
            e += Time.deltaTime;
            rt.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(e / rise));
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(hold);

        e = 0f;
        while (e < fade)
        {
            e += Time.deltaTime;
            var c = baseColor; c.a = Mathf.Lerp(baseColor.a, 0f, Mathf.Clamp01(e / fade));
            img.color = c;
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }
}

/// <summary>
/// Pointer-click relay attached at runtime to each source TMP_Text by
/// MemoFillUI. Forwards the click position so MemoFillUI can resolve
/// which TMP link was clicked.
/// </summary>
public class MemoLinkClickRelay : MonoBehaviour, IPointerClickHandler
{
    private TMP_Text _tmp;
    private Action<TMP_Text, Vector2> _onClick;

    public void Configure(TMP_Text tmp, Action<TMP_Text, Vector2> onClick)
    {
        _tmp = tmp;
        _onClick = onClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_tmp == null || _onClick == null) return;
        _onClick.Invoke(_tmp, eventData.position);
    }
}
