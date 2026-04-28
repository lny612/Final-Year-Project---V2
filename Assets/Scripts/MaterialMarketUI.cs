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
    private Label _memoPurpose;
    private Label _memoPersonality;
    private Label _memoElement;

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
        _memoPurpose     = _root.Q<Label>("memo-purpose");
        _memoPersonality = _root.Q<Label>("memo-personality");
        _memoElement     = _root.Q<Label>("memo-element");

        if (_backButton != null)
            _backButton.clicked += () => OnBackClicked?.Invoke();
        if (_proceedButton != null)
        {
            _proceedButton.clicked += () => OnProceedClicked?.Invoke();
            _proceedButton.SetEnabled(false);
        }
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
        SetMemoSlot(_memoPurpose,     memo?.purpose);
        SetMemoSlot(_memoPersonality, memo?.personality);
        SetMemoSlot(_memoElement,     memo?.element);
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

    private static void SetField(Label l, string label, string value)
    {
        if (l == null) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            HideField(l);
            return;
        }
        l.style.display = DisplayStyle.Flex;
        l.text = $"<b>{label}:</b> {value}";
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

    public void SetHintGlyph(int globalIndex, bool show)
    {
        if (!ValidIndex(globalIndex)) return;
        var slot = _cards[globalIndex];
        if (slot.hintGlyph != null)
            slot.hintGlyph.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private bool ValidIndex(int i) => i >= 0 && i < _cards.Count;
}
