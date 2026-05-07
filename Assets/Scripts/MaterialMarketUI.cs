using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit controller for the cozy market screen
/// (Assets/UI/Market/MaterialMarket.uxml + .uss).
///
/// Drives all visual state. Has no opinions about API calls or game state —
/// MaterialGenerator owns those, calls the Set* methods on this controller,
/// and listens to OnBackClicked / OnProceedClicked / OnBuyClicked.
///
/// Card index space is flat: 0..2 = cores, 3..5 = woods.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public class MaterialMarketUI : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("If left empty, the UIDocument on this GameObject is used.")]
    public UIDocument uiDocument;

    [Tooltip("MaterialCard.uxml — instantiated 6 times into the core / wood rows.")]
    public VisualTreeAsset cardTemplate;

    public const int CORE_COUNT = 3;
    public const int WOOD_COUNT = 3;
    public const int TOTAL_CARDS = CORE_COUNT + WOOD_COUNT;

    // ── Events ─────────────────────────────────────────────────────
    public event Action<int> OnBuyClicked;   // arg = global card index 0..5
    public event Action OnBackClicked;
    public event Action OnProceedClicked;

    // ── Cached visual elements ─────────────────────────────────────
    private VisualElement _root;
    private VisualElement _coreRow;
    private VisualElement _woodRow;
    private VisualElement _goldPanel;
    private Label _goldLabel;
    private Label _statusLabel;
    private Button _proceedButton;
    private Button _backButton;
    private Button _catCores;
    private Button _catWoods;
    private VisualElement _catDivider;
    private Label _memoPurpose;
    private Label _memoPersonality;
    private Label _memoElement;
    private Label _memoReinforcement;
    private string _activeCategory = "woods"; // "cores" | "woods" — default Woods until a wood is bought.

    // Stems of every memo entry — populated in RefreshMemo, consumed in
    // SetField to tint matching description words blue inline.
    private System.Collections.Generic.HashSet<string> _memoStems;
    private const string MemoMatchColorHex = "#2E558C"; // ink-blue, mirrors the dossier's old clickable color

    private readonly List<CardSlot> _cards = new();
    private Coroutine _flashCoroutine;

    private struct CardSlot
    {
        public VisualElement card;
        public Label name;
        public Label subline;
        public VisualElement image;
        public VisualElement placeholder;
        public Label hintGlyph;
        public Label field1;
        public Label field2;
        public Label field3;
        public Label priceAmount;
        public Button buyButton;
    }

    // ── Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[MaterialMarketUI] No UIDocument assigned.");
            return;
        }

        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogError("[MaterialMarketUI] UIDocument has no rootVisualElement yet.");
            return;
        }

        _coreRow         = _root.Q<VisualElement>("core-row");
        _woodRow         = _root.Q<VisualElement>("wood-row");
        _goldPanel       = _root.Q<VisualElement>("market-gold");
        _goldLabel       = _root.Q<Label>("gold-amount");
        _statusLabel     = _root.Q<Label>("market-status");
        _proceedButton   = _root.Q<Button>("market-proceed");
        _backButton      = _root.Q<Button>("market-back");
        _catCores        = _root.Q<Button>("cat-cores");
        _catWoods        = _root.Q<Button>("cat-woods");
        _catDivider      = _root.Q<VisualElement>("cat-divider");
        _memoElement       = _root.Q<Label>("memo-element");
        _memoPersonality   = _root.Q<Label>("memo-personality");
        _memoPurpose       = _root.Q<Label>("memo-purpose");
        _memoReinforcement = _root.Q<Label>("memo-reinforcement");

        if (_backButton != null)
            _backButton.clicked += () => OnBackClicked?.Invoke();
        if (_proceedButton != null)
        {
            _proceedButton.clicked += () => OnProceedClicked?.Invoke();
            _proceedButton.SetEnabled(false);
        }

        if (_catCores != null) _catCores.clicked += () => SelectCategory("cores");
        if (_catWoods != null) _catWoods.clicked += () => SelectCategory("woods");
        // Cores tab + divider start hidden via the UXML `hidden` class so
        // players see only the woods row first; UnlockCores reveals them
        // after the wood is bought.
        SelectCategory(_activeCategory);
    }

    /// <summary>
    /// Hide the Cores tab + divider so the player sees only the Woods row.
    /// Called by MaterialGenerator on scene start; the cores row's `hidden`
    /// class is already set in the UXML.
    /// </summary>
    public void LockCores()
    {
        _catCores?.AddToClassList("hidden");
        _catDivider?.AddToClassList("hidden");
        _coreRow?.AddToClassList("hidden");
        // Make sure the visible category is woods.
        SelectCategory("woods");
    }

    /// <summary>
    /// Reveal the Cores tab + divider after the player buys a wood.
    /// Doesn't auto-switch — the player clicks the tab themselves.
    /// </summary>
    public void UnlockCores()
    {
        _catCores?.RemoveFromClassList("hidden");
        _catDivider?.RemoveFromClassList("hidden");
    }

    /// <summary>
    /// Mark every wood card EXCEPT <paramref name="chosenGlobalIdx"/> as
    /// LOCKED (grey "Sold Out" stamp + dimmed art + disabled buy button).
    /// Called after the player buys their one wood — only one wood may be
    /// owned, but the others read as "Sold Out" rather than "Bought" so the
    /// player can tell which one they actually purchased.
    /// </summary>
    public void LockOtherWoods(int chosenGlobalIdx)
    {
        // Wood cards live at global indices CORE_COUNT..CORE_COUNT+WOOD_COUNT-1.
        for (int i = CORE_COUNT; i < CORE_COUNT + WOOD_COUNT; i++)
        {
            if (i == chosenGlobalIdx) continue;
            MarkLocked(i);
        }
    }

    private void SelectCategory(string cat)
    {
        _activeCategory = cat;
        bool isCores = cat == "cores";
        _catCores?.EnableInClassList("market-cat--selected",  isCores);
        _catWoods?.EnableInClassList("market-cat--selected", !isCores);
        _coreRow?.EnableInClassList("hidden", !isCores);
        _woodRow?.EnableInClassList("hidden",  isCores);
    }

    // ── Public API used by MaterialGenerator ───────────────────────

    public void SetGold(int amount)
    {
        if (_goldLabel != null) _goldLabel.text = amount.ToString();
    }

    public void FlashGoldRed()
    {
        if (_goldPanel == null) return;
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(DoFlashGoldRed());
    }

    private IEnumerator DoFlashGoldRed()
    {
        _goldPanel.AddToClassList("gold-flash");
        yield return new WaitForSeconds(0.45f);
        _goldPanel.RemoveFromClassList("gold-flash");
    }

    public void SetStatus(string msg)
    {
        if (_statusLabel != null) _statusLabel.text = msg ?? "";
    }

    public void SetProceedEnabled(bool enabled)
    {
        if (_proceedButton != null) _proceedButton.SetEnabled(enabled);
    }

    public void RefreshMemo(PlayerMemo memo)
    {
        SetMemoSlot(_memoElement,       memo?.element);
        SetMemoSlot(_memoPersonality,   memo?.personality);
        SetMemoSlot(_memoPurpose,       memo?.purpose);
        SetMemoSlot(_memoReinforcement, memo?.reinforcement);

        // Recompute memo stems for the description-word highlighter and
        // re-paint any already-built cards so existing fields gain blue tints.
        _memoStems = MemoStemmer.BuildMemoStems(memo);
        if (_cards.Count > 0) RepaintFieldHighlights();
    }

    private void RepaintFieldHighlights()
    {
        // Walk the existing card slots and re-set their text via SetField so
        // newly-computed memo stems take effect. Cards still hold the original
        // material data via the buyButton's index closure, which is the wrong
        // place to fish out values from — instead we just refresh whatever
        // text is currently on each label, since SetField only injects color
        // tags around words and is idempotent for non-matches.
        foreach (var slot in _cards)
        {
            ReinjectColorIntoLabel(slot.field1);
            ReinjectColorIntoLabel(slot.field2);
            ReinjectColorIntoLabel(slot.field3);
        }
    }

    /// <summary>
    /// Strip any existing color tags from the label text (so we don't compound
    /// them on repeated calls), recover the bold "&lt;b&gt;Label:&lt;/b&gt; value"
    /// shape, and re-apply current memo stems via MemoStemmer.
    /// </summary>
    private void ReinjectColorIntoLabel(Label l)
    {
        if (l == null || string.IsNullOrEmpty(l.text)) return;
        string current = l.text;

        // Drop any prior <color=...>...</color> wrappers so we always start
        // from the plain text. Cheap manual strip — TMP rich-text only.
        current = StripColorTags(current);

        // Recover the original "<b>Label:</b> value" pattern; if there's no
        // bold prefix (defensive fallback), highlight the whole string.
        int boldEnd = current.IndexOf("</b>", StringComparison.Ordinal);
        string prefix = boldEnd >= 0 ? current.Substring(0, boldEnd + 4) : "";
        string value  = boldEnd >= 0 ? current.Substring(boldEnd + 4)    : current;

        string highlighted = MemoStemmer.HighlightMatches(value, _memoStems, MemoMatchColorHex);
        l.text = prefix + highlighted;
    }

    private static string StripColorTags(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Cheap inline removal: <color=#XXXXXX> ... </color>
        var sb = new System.Text.StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            if (s[i] == '<')
            {
                int end = s.IndexOf('>', i);
                if (end > i)
                {
                    string tag = s.Substring(i, end - i + 1);
                    if (tag.StartsWith("<color=", StringComparison.Ordinal) || tag == "</color>")
                    {
                        i = end + 1;
                        continue;
                    }
                }
            }
            sb.Append(s[i]);
            i++;
        }
        return sb.ToString();
    }

    private static void SetMemoSlot(Label l, string val)
    {
        if (l == null) return;
        l.text = string.IsNullOrWhiteSpace(val) ? "—" : val;
    }

    public void ClearCards()
    {
        _cards.Clear();
        _coreRow?.Clear();
        _woodRow?.Clear();
    }

    /// <summary>
    /// Build all 6 cards from the materials lists. Call once per round.
    /// Cards land at global indices [core0, core1, core2, wood0, wood1, wood2].
    /// </summary>
    public void BuildCards(List<MaterialData> cores, List<MaterialData> woods)
    {
        if (cardTemplate == null)
        {
            Debug.LogError("[MaterialMarketUI] cardTemplate VisualTreeAsset is not assigned.");
            return;
        }

        ClearCards();

        int n = (cores != null ? cores.Count : 0);
        for (int i = 0; i < n; i++)
            AddCard(cores[i], _coreRow, _cards.Count);

        n = (woods != null ? woods.Count : 0);
        for (int i = 0; i < n; i++)
            AddCard(woods[i], _woodRow, _cards.Count);
    }

    private void AddCard(MaterialData mat, VisualElement row, int globalIndex)
    {
        if (row == null || mat == null) return;

        VisualElement instance = cardTemplate.Instantiate();
        // .Instantiate() wraps the template in a TemplateContainer — pluck
        // the actual .material-card child so styling/queries hit the right node.
        VisualElement card = instance.Q(className: "material-card") ?? instance;
        row.Add(instance);
        instance.style.flexGrow = 1;
        instance.style.flexBasis = 0;

        var slot = new CardSlot
        {
            card        = card,
            name        = card.Q<Label>("card-name"),
            subline     = card.Q<Label>("card-subline"),
            image       = card.Q<VisualElement>("card-image"),
            placeholder = card.Q<VisualElement>("card-placeholder"),
            hintGlyph   = card.Q<Label>("card-hint-glyph"),
            field1      = card.Q<Label>("card-field1"),
            field2      = card.Q<Label>("card-field2"),
            field3      = card.Q<Label>("card-field3"),
            priceAmount = card.Q<Label>("card-price-amount"),
            buyButton   = card.Q<Button>("card-buy"),
        };

        // Populate text
        if (slot.name        != null) slot.name.text        = mat.name ?? "";
        if (slot.priceAmount != null) slot.priceAmount.text = mat.price.ToString();

        bool isCore = mat.materialType == "core";
        if (slot.subline != null)
            slot.subline.text = isCore ? "— Core —" : "— Wood —";

        if (isCore)
        {
            SetField(slot.field1, "Affinity", mat.elementalAffinity);
            SetField(slot.field2, "Attributes", mat.attributes);
            SetField(slot.field3, "Special", mat.special);
        }
        else
        {
            SetField(slot.field1, "Suits", mat.personalityMatch);
            SetField(slot.field2, "Attributes", mat.attributes);
            HideField(slot.field3);
        }

        // Image: hidden until SetCardImage is called
        if (slot.image != null) slot.image.style.backgroundImage = new StyleBackground((Texture2D)null);

        // Buy click → forward index to subscriber
        if (slot.buyButton != null)
        {
            int captured = globalIndex;
            slot.buyButton.clicked += () => OnBuyClicked?.Invoke(captured);
        }

        _cards.Add(slot);
    }

    private void SetField(Label l, string label, string value)
    {
        if (l == null) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            HideField(l);
            return;
        }
        l.style.display = DisplayStyle.Flex;
        // Tint any words whose stem matches a memo entry (e.g. memo "precision"
        // → description "precise") so the player sees the customer's hints lit
        // up inline. Runs cheap stem comparisons; no AI roundtrip.
        string highlighted = MemoStemmer.HighlightMatches(value, _memoStems, MemoMatchColorHex);
        l.text = $"<b>{label}:</b> {highlighted}";
    }

    private static void HideField(Label l)
    {
        if (l == null) return;
        l.style.display = DisplayStyle.None;
        l.text = "";
    }

    public void SetCardImage(int globalIndex, Texture2D tex)
    {
        if (!ValidIndex(globalIndex) || tex == null) return;
        var slot = _cards[globalIndex];
        if (slot.image != null)
            slot.image.style.backgroundImage = new StyleBackground(tex);
        if (slot.placeholder != null)
            slot.placeholder.style.display = DisplayStyle.None;
    }

    public void MarkSoldOut(int globalIndex)
    {
        if (!ValidIndex(globalIndex)) return;
        var slot = _cards[globalIndex];
        if (slot.card != null) slot.card.AddToClassList("sold-out");
        if (slot.buyButton != null) slot.buyButton.SetEnabled(false);
    }

    /// <summary>
    /// Mark a card as LOCKED — visually distinct from sold-out (the player's
    /// own purchase). Used for the un-bought woods after the wood-of-choice
    /// is locked in: grey "Sold Out" stamp instead of the red "Bought" one.
    /// </summary>
    public void MarkLocked(int globalIndex)
    {
        if (!ValidIndex(globalIndex)) return;
        var slot = _cards[globalIndex];
        if (slot.card != null) slot.card.AddToClassList("locked");
        if (slot.buyButton != null) slot.buyButton.SetEnabled(false);

        // Repurpose the existing soldout stamp Label — a Label sibling lookup
        // saves us from having to add a second element to the UXML/CSS path.
        var stamp = slot.card?.Q<Label>("card-soldout");
        if (stamp != null) stamp.text = "Sold Out";
    }

    public void SetHintGlyph(int globalIndex, bool show)
    {
        if (!ValidIndex(globalIndex)) return;
        var slot = _cards[globalIndex];
        if (slot.hintGlyph != null)
            slot.hintGlyph.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool ValidIndex(int i) => i >= 0 && i < _cards.Count;
}
