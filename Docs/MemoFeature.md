# Memo-Gated Dossier Reading

**Status:** Live in production (UI Toolkit).
**Supersedes:** `Docs/DossierSortingFeature.md`
**Scenes affected:** `CustomerGeneratorTest` (gate), `MaterialGeneratorTest` (consumer), `CraftingScene` (persistent reference).

## Problem

Reading the customer dossier was passive. The LLM returned 7 fields, they dumped into static `TMP_Text` blocks, and the Proceed button enabled the moment generation completed. The player could click through without reading.

## Design

The player must distil the dossier into a **4-entry memo** before Proceed enables. The memo then becomes the *only* on-screen reference in the market and crafting scenes — the full dossier is hidden, and the player's choices about what to write down shape downstream material selection (✦ hints) and the in-card highlight tints.

### Memo entries (4 slots, in display order)

| Slot          | Prompt                                                       |
|---------------|--------------------------------------------------------------|
| Element       | "What element calls to them?"                                |
| Personality   | "What temperament hides beneath?"                            |
| Purpose       | "What purpose does the wand carry?"                          |
| Reinforcement | "What must it reinforce — what must it conceal?"             |

Slot acceptance is intentionally relaxed: any phrase from any prose section can land in any slot. The label is guidance, not a gate.

### Interaction (CustomerGeneratorTest)

1. Customer pre-gen (kicked off in MorningScene) lands on `GameManager.pendingCustomer`. `CustomerGenerator` consumes it and hands the order to `DossierPanelController.Begin(order, OnMemoComplete)`.
2. Each prose section (school / profession / personality / request / trueGoal / constraint) is tokenized into per-word `Label` elements. Content words (≥3 chars, not stop-words) are clickable; whitespace and stop-words render plain.
3. **Drag** across a run of words to highlight a phrase (yellow). A 5-word cap is enforced so highlights stay focused.
4. Either **click a slot** with the highlight active, or **drag the highlight onto a slot** (a ghost label follows the cursor). Either commits the phrase as a chip in that slot.
5. Each slot accepts multiple chips. An `×` on each chip removes it and restores the underlying words to selectable.
6. Proceed enables when all four slots hold ≥1 chip. The completed memo is stored on `GameManager.currentMemo`.

### Downstream consequences

- **MaterialGeneratorTest** (`MaterialMarketUI`):
  - The four memo strings show on the left rail.
  - **✦ match-hint glyph** — `MaterialGenerator.ApplyMemoHints` toggles a ✦ on each card whose `elementalAffinity` (cores) or `personalityMatch` (woods) overlaps the memo via `IsHintMatch` (token-split substring search).
  - **Inline blue tint** — `MemoStemmer.HighlightMatches` walks each card's description text, stems each word, and wraps memo-matching words in TMP `<color=#2E558C>...</color>` tags. Stems catch morphological pairs (e.g. memo *"precision"* matches description *"precise"*).
- **CraftingScene** (`CraftingWorkbenchUI`):
  - The same four memo entries show on the workbench parchment card.

## Files

Production scripts:
- `Assets/Scripts/PlayerMemo.cs` — `PlayerMemo` data class (`element`, `personality`, `purpose`, `reinforcement`) + `PlayerMemoField` enum (`Element, Personality, Purpose, Reinforcement`).
- `Assets/Scripts/DossierPanelController.cs` — UI Toolkit driver for the dossier scene. Renders prose, handles drag-select highlight + chip commits, gates Proceed.
- `Assets/Scripts/MemoStemmer.cs` — pure static suffix-stripping stemmer + tokenizer + rich-text highlighter.
- `Assets/Scripts/MaterialMarketUI.cs` — `RefreshMemo(memo)` (left rail) + `SetHintGlyph(idx, show)` (✦) + memo-match blue tint via `MemoStemmer.HighlightMatches` in `SetField`.
- `Assets/Scripts/CraftingWorkbenchUI.cs` — `SetMemo(memo)` populates the four memo rows on the workbench card.

UI assets:
- `Assets/UI/Dossier/DossierPanel.uxml` + `DossierPanel.uss` + `DossierPanelSettings.asset`.
- `Assets/UI/Market/MaterialMarket.uxml` + `MaterialMarket.uss` (memo rows + ✦ glyph styling).
- `Assets/UI/Crafting/CraftingWorkbench.uxml` + `CraftingWorkbench.uss` (memo card on the parchment column).

Removed in cleanup:
- `MemoFillUI.cs` (legacy uGUI memo gate) — orphan, deleted.
- `MemoCardUI.cs` (legacy uGUI read-only memo card) — orphan, deleted.

## Wiring

**CustomerGeneratorTest** — `DossierUIDocument` GameObject carries `UIDocument` + `DossierPanelController`. `CustomerGenerator.dossierPanel` references the controller; on memo completion the controller stores the result on `GameManager.currentMemo` and enables its own Proceed button.

**MaterialGeneratorTest** — `MarketUIDocument` carries `UIDocument` + `MaterialMarketUI`. `MaterialGenerator.marketUI` references it; `MaterialMarketUI.RefreshMemo` is called from `MaterialGenerator.Start` to paint the four memo entries and prime the stem cache.

**CraftingScene** — `CraftingWorkbenchUIDocument` carries `UIDocument` + `CraftingWorkbenchUI`. `CraftingManager.workbenchUI` references it; `CraftingManager.Start` calls `workbenchUI.SetMemo(GameManager.Instance?.currentMemo)`.

## Verification

1. **CustomerGeneratorTest** — drag across a multi-word phrase (e.g. *"the precision of"*). Yellow highlight is continuous across stop-words. Click any slot → chip lands. Drag from a highlight onto another slot → also commits. Click a chip's × → words restore. Fill all four slots → Proceed enables.
2. **MaterialGeneratorTest** — left rail shows the four memo entries; ✦ glyphs appear on cards whose affinity overlaps memo content; description words sharing a stem with memo words render blue inline (e.g. memo *"precise"* highlights *"precision"* in a card).
3. **CraftingScene** — workbench memo card shows the same four entries; loading the scene without a memo on `GameManager` shows `—` placeholders without errors.

## Out of scope (future iterations)

- **Reading sub-score in evaluation** — compare memo to customer's actual profile, award a separate reading score alongside craft match.
- **Synonym matching** — `MemoStemmer` catches morphological variants but not synonyms (e.g. *calm ↔ tranquil*). Could be augmented with a one-shot OpenAI call after memo completion if the gap matters.
- **Difficulty scaling by reputation tier** — higher rep could add distractor words to source fields.
