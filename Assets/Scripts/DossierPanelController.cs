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

/// <summary>
/// UI Toolkit dossier panel + memo-fill gate.
///
/// Reading flow:
/// 1. Drag across content words inside any prose section to highlight a phrase.
/// 2. The highlighted phrase becomes BOTH clickable and draggable.
/// 3. Click a memo slot, or drop the dragged highlight onto a slot, to commit
///    the phrase as a chip in that slot.
/// 4. Each slot accepts multiple chips. Click a chip's × to remove it.
/// 5. When all three slots hold at least one chip, the memo is complete and
///    Proceed enables.
/// </summary>
[DisallowMultipleComponent]
public class DossierPanelController : MonoBehaviour
{
    [Header("UI Toolkit")]
    [Tooltip("UIDocument that hosts DossierPanel.uxml. Usually on the same GameObject.")]
    public UIDocument document;

    [Header("Proceed button (UI Toolkit)")]
    [Tooltip("If true, the in-panel Proceed button loads SCENE_MARKET on click.")]
    public bool wireProceedButton = true;

    // ── Source data ─────────────────────────────────────────────────

    private enum SourceKey { Request, TrueGoal, Personality, Profession, School, Constraint }

    private class WordView
    {
        public Label   element;       // visual element (Label) for this token
        public string  text;          // word text (or whitespace/punct)
        public bool    isWord;        // eligible for selection
        public bool    highlighted;   // currently part of the active highlight run
        public bool    committed;     // already part of a slot chip — locked
    }

    private class SourceField
    {
        public SourceKey       key;
        public VisualElement   container;
        public List<WordView>  words = new();
    }

    private class ChipEntry
    {
        public string         text;          // joined phrase shown on the chip
        public SourceKey      sourceKey;     // which prose field it came from
        public int            firstTokenIdx; // inclusive — for restoring on remove
        public int            lastTokenIdx;  // inclusive
        public VisualElement  chipElement;   // the chip VisualElement in the slot
    }

    private readonly List<SourceField> _sources = new();
    private readonly Dictionary<PlayerMemoField, List<ChipEntry>> _slotEntries = new()
    {
        { PlayerMemoField.Element,       new List<ChipEntry>() },
        { PlayerMemoField.Personality,   new List<ChipEntry>() },
        { PlayerMemoField.Purpose,       new List<ChipEntry>() },
        { PlayerMemoField.Reinforcement, new List<ChipEntry>() },
    };

    // ── Active highlight (after drag-select / single click) ─────────

    private SourceField _highlightField;
    private int         _highlightStart = -1;
    private int         _highlightEnd   = -1;

    // ── Pointer state ───────────────────────────────────────────────

    private enum PointerMode { Idle, Selecting, MaybeDragging, Dragging }
    private PointerMode _pointerMode = PointerMode.Idle;
    private int      _capturedPointerId;
    private Vector2  _pointerDownPos;
    private VisualElement _dragGhost;
    private const float DRAG_THRESHOLD_PX = 4f;

    // Hard cap on how many eligible words can sit in a single highlight run.
    private const int MAX_HIGHLIGHT_WORDS = 5;

    // ── Slot UI cache ───────────────────────────────────────────────

    private VisualElement _purposeSlot;
    private VisualElement _personalitySlot;
    private VisualElement _elementSlot;
    private VisualElement _reinforcementSlot;
    private VisualElement _slotDropTarget;  // currently highlighted as drop target (style only)

    // ── Cached element refs (resolved in Bootstrap) ─────────────────

    private VisualElement _root;
    private Label         _customerNameLabel;
    private Label         _statusLabel;
    private Button        _proceedButton;

    private VisualElement _schoolProse;
    private VisualElement _professionProse;
    private VisualElement _personalityProse;
    private VisualElement _requestProse;
    private VisualElement _trueGoalProse;
    private VisualElement _constraintProse;

    private bool   _bootstrapped;
    private Action _onComplete;

    // ── Stop-words (mirror of MemoFillUI; keep in sync for parity) ──

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

    // ── Unity lifecycle ─────────────────────────────────────────────

    private void OnEnable() => Bootstrap();

    private void Bootstrap()
    {
        if (_bootstrapped) return;
        if (document == null) document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null) return;

        _root = document.rootVisualElement;

        _customerNameLabel = _root.Q<Label>("customerName");
        _statusLabel       = _root.Q<Label>("statusLabel");
        _proceedButton     = _root.Q<Button>("proceedButton");

        _schoolProse      = _root.Q<VisualElement>("schoolProse");
        _professionProse  = _root.Q<VisualElement>("professionProse");
        _personalityProse = _root.Q<VisualElement>("personalityProse");
        _requestProse     = _root.Q<VisualElement>("requestProse");
        _trueGoalProse    = _root.Q<VisualElement>("trueGoalProse");
        _constraintProse  = _root.Q<VisualElement>("constraintProse");

        _purposeSlot       = _root.Q<VisualElement>("purposeSlot");
        _personalitySlot   = _root.Q<VisualElement>("personalitySlot");
        _elementSlot       = _root.Q<VisualElement>("elementSlot");
        _reinforcementSlot = _root.Q<VisualElement>("reinforcementSlot");

        WireSlot(_elementSlot,       PlayerMemoField.Element);
        WireSlot(_personalitySlot,   PlayerMemoField.Personality);
        WireSlot(_purposeSlot,       PlayerMemoField.Purpose);
        WireSlot(_reinforcementSlot, PlayerMemoField.Reinforcement);

        // Root-level pointer-move/up so we can do drag-select & drag-drop with
        // root pointer capture and manual hit-testing across the dossier prose.
        _root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnRootPointerUp);

        if (_proceedButton != null)
        {
            _proceedButton.SetEnabled(false);
            if (wireProceedButton)
                _proceedButton.clicked += () =>
                    GameManager.Instance?.LoadScene(GameManager.SCENE_MARKET);
        }

        _bootstrapped = true;
    }

    private void WireSlot(VisualElement slot, PlayerMemoField field)
    {
        if (slot == null) return;
        // Clickable fires after a press-release on the slot itself with no significant
        // movement. That's exactly the "click a slot to commit highlight" gesture.
        slot.AddManipulator(new Clickable(() => OnSlotClicked(field)));
        UpdateSlotPlaceholder(field);
    }

    // ── Public API ──────────────────────────────────────────────────

    public void Begin(CustomerOrder order, Action onComplete)
    {
        Bootstrap();
        if (!_bootstrapped) return;

        _onComplete = onComplete;
        ClearHighlight();
        _sources.Clear();

        // Wipe any previous chips
        foreach (var kv in _slotEntries) kv.Value.Clear();
        ClearSlotChildren(_elementSlot);
        ClearSlotChildren(_personalitySlot);
        ClearSlotChildren(_purposeSlot);
        ClearSlotChildren(_reinforcementSlot);
        UpdateSlotPlaceholder(PlayerMemoField.Element);
        UpdateSlotPlaceholder(PlayerMemoField.Personality);
        UpdateSlotPlaceholder(PlayerMemoField.Purpose);
        UpdateSlotPlaceholder(PlayerMemoField.Reinforcement);

        if (order == null) return;

        if (_customerNameLabel != null)
            _customerNameLabel.text = string.IsNullOrEmpty(order.customerName) ? "—" : order.customerName;

        BuildSource(SourceKey.School,      _schoolProse,      order.schoolOfMagic);
        BuildSource(SourceKey.Profession,  _professionProse,  order.profession);
        BuildSource(SourceKey.Personality, _personalityProse, order.personality);
        BuildSource(SourceKey.Request,     _requestProse,     order.request);
        BuildSource(SourceKey.TrueGoal,    _trueGoalProse,    order.trueGoal);
        BuildSource(SourceKey.Constraint,  _constraintProse,  order.constraint);

        if (_proceedButton != null) _proceedButton.SetEnabled(false);
        SetStatus("Drag across the dossier to highlight phrases. Then click or drag onto a memo slot.");
    }

    public void SetStatus(string msg)
    {
        if (_statusLabel != null) _statusLabel.text = msg;
    }

    // ── Tokenization ────────────────────────────────────────────────

    private static bool IsWordChar(char c) => char.IsLetter(c) || c == '\'' || c == '-';

    private void BuildSource(SourceKey key, VisualElement container, string raw)
    {
        if (container == null) return;
        container.Clear();

        var field = new SourceField { key = key, container = container };
        raw ??= "";

        int i = 0;
        while (i < raw.Length)
        {
            if (IsWordChar(raw[i]))
            {
                int start = i;
                while (i < raw.Length && IsWordChar(raw[i])) i++;
                string word = raw.Substring(start, i - start);
                bool eligible = word.Length >= 3 && !StopWords.Contains(word);

                var wv = new WordView { text = word, isWord = eligible };
                var lbl = new Label(word);
                lbl.AddToClassList(eligible ? "word-clickable" : "word-static");
                lbl.pickingMode = eligible ? PickingMode.Position : PickingMode.Ignore;
                wv.element = lbl;

                if (eligible)
                {
                    int capturedIdx = field.words.Count;
                    SourceField capturedField = field;
                    lbl.RegisterCallback<PointerDownEvent>(e => OnWordPointerDown(capturedField, capturedIdx, e));
                }

                container.Add(lbl);
                field.words.Add(wv);
            }
            else
            {
                int start = i;
                while (i < raw.Length && !IsWordChar(raw[i])) i++;
                string ws = raw.Substring(start, i - start);

                var wv = new WordView { text = ws, isWord = false };
                var lbl = new Label(ws);
                lbl.AddToClassList("word-static");
                lbl.pickingMode = PickingMode.Ignore;
                wv.element = lbl;

                container.Add(lbl);
                field.words.Add(wv);
            }
        }

        _sources.Add(field);
    }

    // ── Word pointer-down: start drag-select OR begin drag-from-highlight ──

    private void OnWordPointerDown(SourceField field, int wordIdx, PointerDownEvent e)
    {
        if (e.button != 0) return;
        var wv = field.words[wordIdx];
        if (!wv.isWord || wv.committed) return;

        e.StopPropagation();

        _pointerDownPos    = e.position;
        _capturedPointerId = e.pointerId;
        _root.CapturePointer(e.pointerId);

        if (wv.highlighted && _highlightField == field)
        {
            // Tentative drag — if user moves past the threshold we spawn a ghost.
            _pointerMode = PointerMode.MaybeDragging;
        }
        else
        {
            // Begin a fresh drag-select run (clears any prior highlight).
            ClearHighlight();
            _highlightField = field;
            _highlightStart = wordIdx;
            _highlightEnd   = wordIdx;
            _pointerMode    = PointerMode.Selecting;
            ApplyHighlightFromRange();
        }
    }

    // ── Root pointer-move ───────────────────────────────────────────

    private void OnRootPointerMove(PointerMoveEvent e)
    {
        if (_pointerMode == PointerMode.Idle) return;

        if (_pointerMode == PointerMode.Selecting)
        {
            int hit = FindWordIndexAt(_highlightField, e.position);
            if (hit >= 0)
            {
                int clamped = ClampToMaxWords(_highlightStart, hit, MAX_HIGHLIGHT_WORDS);
                if (clamped != _highlightEnd)
                {
                    _highlightEnd = clamped;
                    ApplyHighlightFromRange();
                }
            }
            return;
        }

        if (_pointerMode == PointerMode.MaybeDragging)
        {
            float dist = ((Vector2)e.position - _pointerDownPos).magnitude;
            if (dist >= DRAG_THRESHOLD_PX)
            {
                SpawnDragGhost();
                _pointerMode = PointerMode.Dragging;
            }
        }

        if (_pointerMode == PointerMode.Dragging)
        {
            UpdateDragGhostPosition(e.position);
            UpdateSlotDropHover(e.position);
        }
    }

    // ── Root pointer-up ─────────────────────────────────────────────

    private void OnRootPointerUp(PointerUpEvent e)
    {
        if (_pointerMode == PointerMode.Idle) return;

        if (_pointerMode == PointerMode.Selecting)
        {
            _pointerMode = PointerMode.Idle;
            _root.ReleasePointer(_capturedPointerId);
            return;
        }

        if (_pointerMode == PointerMode.Dragging)
        {
            var dropSlot = FindSlotUnder(e.position);
            DestroyDragGhost();
            ClearSlotDropHover();
            if (dropSlot.HasValue && HasHighlight())
                CommitHighlightToSlot(dropSlot.Value);
        }
        // MaybeDragging without crossing the threshold is just a click — keep
        // the highlight as-is.

        _pointerMode = PointerMode.Idle;
        _root.ReleasePointer(_capturedPointerId);
    }

    // ── Highlight helpers ───────────────────────────────────────────

    private bool HasHighlight() =>
        _highlightField != null && _highlightStart >= 0 && _highlightEnd >= 0;

    private void ClearHighlight()
    {
        if (_highlightField != null)
        {
            foreach (var w in _highlightField.words)
            {
                if (w.highlighted)
                {
                    w.highlighted = false;
                    w.element?.RemoveFromClassList("word-highlighted");
                }
            }
        }
        _highlightField = null;
        _highlightStart = -1;
        _highlightEnd   = -1;
    }

    private void ApplyHighlightFromRange()
    {
        if (_highlightField == null) return;

        int lo = Mathf.Min(_highlightStart, _highlightEnd);
        int hi = Mathf.Max(_highlightStart, _highlightEnd);

        for (int i = 0; i < _highlightField.words.Count; i++)
        {
            var w = _highlightField.words[i];
            bool shouldHighlight = i >= lo && i <= hi && w.isWord && !w.committed;
            if (shouldHighlight && !w.highlighted)
            {
                w.highlighted = true;
                w.element?.AddToClassList("word-highlighted");
            }
            else if (!shouldHighlight && w.highlighted)
            {
                w.highlighted = false;
                w.element?.RemoveFromClassList("word-highlighted");
            }
        }
    }

    /// <summary>
    /// Walk from <paramref name="anchor"/> toward <paramref name="target"/> through
    /// <c>_highlightField.words</c> and return the farthest token index reachable
    /// without exceeding <paramref name="maxWords"/> eligible words in the run.
    /// </summary>
    private int ClampToMaxWords(int anchor, int target, int maxWords)
    {
        if (_highlightField == null || maxWords <= 0) return anchor;
        int step = target > anchor ? 1 : (target < anchor ? -1 : 0);
        if (step == 0) return anchor;

        int wordCount = 0;
        int lastValid = anchor;
        var words = _highlightField.words;
        for (int i = anchor; i >= 0 && i < words.Count; i += step)
        {
            if (words[i].isWord) wordCount++;
            if (wordCount > maxWords) break;
            lastValid = i;
            if (i == target) break;
        }
        return lastValid;
    }

    private string GetHighlightedPhrase()
    {
        if (!HasHighlight()) return "";

        int lo = Mathf.Min(_highlightStart, _highlightEnd);
        int hi = Mathf.Max(_highlightStart, _highlightEnd);

        var sb = new System.Text.StringBuilder();
        for (int i = lo; i <= hi; i++)
        {
            sb.Append(_highlightField.words[i].text);
        }
        return sb.ToString().Trim();
    }

    // ── Hit-testing ─────────────────────────────────────────────────

    private int FindWordIndexAt(SourceField field, Vector2 worldPos)
    {
        if (field == null) return -1;
        for (int i = 0; i < field.words.Count; i++)
        {
            var w = field.words[i];
            if (!w.isWord) continue;
            if (w.element == null) continue;
            if (w.element.worldBound.Contains(worldPos)) return i;
        }
        return -1;
    }

    private PlayerMemoField? FindSlotUnder(Vector2 worldPos)
    {
        if (_elementSlot       != null && _elementSlot.worldBound.Contains(worldPos))       return PlayerMemoField.Element;
        if (_personalitySlot   != null && _personalitySlot.worldBound.Contains(worldPos))   return PlayerMemoField.Personality;
        if (_purposeSlot       != null && _purposeSlot.worldBound.Contains(worldPos))       return PlayerMemoField.Purpose;
        if (_reinforcementSlot != null && _reinforcementSlot.worldBound.Contains(worldPos)) return PlayerMemoField.Reinforcement;
        return null;
    }

    // ── Drag-ghost ──────────────────────────────────────────────────

    private void SpawnDragGhost()
    {
        DestroyDragGhost();
        var phrase = GetHighlightedPhrase();
        if (string.IsNullOrEmpty(phrase)) return;

        var ghost = new Label(phrase.Length > 40 ? phrase.Substring(0, 37) + "…" : phrase);
        ghost.AddToClassList("drag-ghost");
        ghost.pickingMode = PickingMode.Ignore;
        _root.Add(ghost);
        _dragGhost = ghost;
    }

    private void UpdateDragGhostPosition(Vector2 worldPos)
    {
        if (_dragGhost == null) return;
        var local = _root.WorldToLocal(worldPos);
        _dragGhost.style.left = new StyleLength(new Length(local.x + 14f, LengthUnit.Pixel));
        _dragGhost.style.top  = new StyleLength(new Length(local.y + 14f, LengthUnit.Pixel));
    }

    private void DestroyDragGhost()
    {
        if (_dragGhost != null)
        {
            _dragGhost.RemoveFromHierarchy();
            _dragGhost = null;
        }
    }

    private void UpdateSlotDropHover(Vector2 worldPos)
    {
        var slot = FindSlotUnder(worldPos);
        VisualElement target = slot.HasValue ? GetSlotElement(slot.Value) : null;
        if (target == _slotDropTarget) return;
        ClearSlotDropHover();
        if (target != null)
        {
            target.AddToClassList("memo-slot--drop-target");
            _slotDropTarget = target;
        }
    }

    private void ClearSlotDropHover()
    {
        if (_slotDropTarget != null)
            _slotDropTarget.RemoveFromClassList("memo-slot--drop-target");
        _slotDropTarget = null;
    }

    // ── Slot click → commit current highlight ───────────────────────

    private void OnSlotClicked(PlayerMemoField slot)
    {
        // Click only commits if a highlight exists — clicks on an empty slot
        // with no active highlight are no-ops.
        if (HasHighlight())
            CommitHighlightToSlot(slot);
    }

    // ── Commit / uncommit ───────────────────────────────────────────

    private void CommitHighlightToSlot(PlayerMemoField slot)
    {
        if (!HasHighlight()) return;

        int lo = Mathf.Min(_highlightStart, _highlightEnd);
        int hi = Mathf.Max(_highlightStart, _highlightEnd);
        var field = _highlightField;
        string phrase = GetHighlightedPhrase();

        if (string.IsNullOrEmpty(phrase))
        {
            ClearHighlight();
            return;
        }

        // Skip duplicate entries within the same slot.
        var entries = _slotEntries[slot];
        if (entries.Exists(c => string.Equals(c.text, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            ClearHighlight();
            return;
        }

        // Mark all words in the run as committed (strikethrough, no longer
        // selectable). They'll be restored if the chip is later removed.
        for (int i = lo; i <= hi; i++)
        {
            var w = field.words[i];
            w.highlighted = false;
            w.element?.RemoveFromClassList("word-highlighted");
            if (w.isWord)
            {
                w.committed = true;
                w.element?.AddToClassList("word-committed");
            }
        }

        var entry = new ChipEntry
        {
            text          = phrase,
            sourceKey     = field.key,
            firstTokenIdx = lo,
            lastTokenIdx  = hi,
        };
        entry.chipElement = BuildChip(slot, entry);
        entries.Add(entry);

        var slotEl = GetSlotElement(slot);
        if (slotEl != null)
        {
            RemoveSlotPlaceholder(slotEl);
            slotEl.Add(entry.chipElement);
            slotEl.AddToClassList("memo-slot--filled");
        }

        ClearHighlight();
        CheckMemoCompletion();
    }

    private VisualElement BuildChip(PlayerMemoField slot, ChipEntry entry)
    {
        var chip = new VisualElement();
        chip.AddToClassList("memo-chip");

        var txt = new Label(entry.text);
        txt.AddToClassList("memo-chip__text");
        txt.pickingMode = PickingMode.Ignore;
        chip.Add(txt);

        var rm = new Button(() => RemoveChip(slot, entry));
        rm.AddToClassList("memo-chip__remove");
        rm.text = "×";
        chip.Add(rm);

        return chip;
    }

    private void RemoveChip(PlayerMemoField slot, ChipEntry entry)
    {
        var entries = _slotEntries[slot];
        if (!entries.Remove(entry)) return;

        // Restore the underlying words so the player can re-pick them.
        var field = _sources.Find(s => s.key == entry.sourceKey);
        if (field != null)
        {
            for (int i = entry.firstTokenIdx; i <= entry.lastTokenIdx && i < field.words.Count; i++)
            {
                var w = field.words[i];
                if (w.isWord && w.committed)
                {
                    w.committed = false;
                    w.element?.RemoveFromClassList("word-committed");
                }
            }
        }

        entry.chipElement?.RemoveFromHierarchy();

        var slotEl = GetSlotElement(slot);
        if (slotEl != null && entries.Count == 0)
        {
            slotEl.RemoveFromClassList("memo-slot--filled");
            UpdateSlotPlaceholder(slot);
        }

        CheckMemoCompletion();
    }

    private VisualElement GetSlotElement(PlayerMemoField slot) => slot switch
    {
        PlayerMemoField.Element       => _elementSlot,
        PlayerMemoField.Personality   => _personalitySlot,
        PlayerMemoField.Purpose       => _purposeSlot,
        PlayerMemoField.Reinforcement => _reinforcementSlot,
        _                             => null
    };

    private void ClearSlotChildren(VisualElement slot)
    {
        if (slot == null) return;
        slot.Clear();
        slot.RemoveFromClassList("memo-slot--filled");
    }

    private void UpdateSlotPlaceholder(PlayerMemoField slot)
    {
        var slotEl = GetSlotElement(slot);
        if (slotEl == null) return;
        if (_slotEntries[slot].Count > 0) return;

        // Only add a placeholder if one isn't already there.
        if (slotEl.childCount == 0)
        {
            var ph = new Label(slot switch
            {
                PlayerMemoField.Element       => "(drop or click to add an element)",
                PlayerMemoField.Personality   => "(drop or click to add a trait)",
                PlayerMemoField.Purpose       => "(drop or click to add a purpose)",
                PlayerMemoField.Reinforcement => "(drop or click — what to reinforce or hide)",
                _                             => "(empty)"
            });
            ph.AddToClassList("memo-slot__placeholder");
            ph.pickingMode = PickingMode.Ignore;
            slotEl.Add(ph);
        }
    }

    private void RemoveSlotPlaceholder(VisualElement slotEl)
    {
        if (slotEl == null) return;
        var ph = slotEl.Q<Label>(className: "memo-slot__placeholder");
        ph?.RemoveFromHierarchy();
    }

    // ── Memo completion ─────────────────────────────────────────────

    private void CheckMemoCompletion()
    {
        bool complete = _slotEntries[PlayerMemoField.Element].Count       > 0
                     && _slotEntries[PlayerMemoField.Personality].Count   > 0
                     && _slotEntries[PlayerMemoField.Purpose].Count       > 0
                     && _slotEntries[PlayerMemoField.Reinforcement].Count > 0;

        if (_proceedButton != null) _proceedButton.SetEnabled(complete);

        if (complete)
        {
            var memo = new PlayerMemo
            {
                element       = string.Join(", ", EntryTexts(PlayerMemoField.Element)),
                personality   = string.Join(", ", EntryTexts(PlayerMemoField.Personality)),
                purpose       = string.Join(", ", EntryTexts(PlayerMemoField.Purpose)),
                reinforcement = string.Join(", ", EntryTexts(PlayerMemoField.Reinforcement)),
            };
            if (GameManager.Instance != null) GameManager.Instance.currentMemo = memo;
            SetStatus("Memo complete. Proceed to the market.");
            _onComplete?.Invoke();
        }
        else
        {
            SetStatus("Drag across the dossier to highlight phrases. Then click or drag onto a memo slot.");
        }
    }

    private IEnumerable<string> EntryTexts(PlayerMemoField slot)
    {
        foreach (var entry in _slotEntries[slot]) yield return entry.text;
    }
}
