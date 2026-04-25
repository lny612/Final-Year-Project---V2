# Memo-Gated Dossier Reading — Design Doc

**Status:** Implemented 2026-04-24 (scene wiring pending — see TODO-EDITOR notes).
**Supersedes:** `Docs/DossierSortingFeature.md`
**Scenes affected:** `CustomerGeneratorTest` (gate), `MaterialGeneratorTest` (consumer), `CraftingScene` (persistent reference).

## Problem

Reading the customer dossier was passive. The LLM returned 7 fields, they dumped into static `TMP_Text` blocks, and the Proceed button enabled the moment generation completed. The player could click through without reading. By `CraftingScene`, the dossier was gone from screen.

## Design

The player must distil the dossier into a 3-entry **memo** before Proceed enables. The memo then becomes the *only* on-screen reference in the market and crafting scenes — the full dossier is hidden, and a wrong memo actively misleads downstream material selection.

### Memo entries (question prompts, not bare labels)

| Slot        | Prompt                             | Accepts words from         |
|-------------|------------------------------------|----------------------------|
| Purpose     | "What do they really want?"        | `request`, `trueGoal`      |
| Personality | "What temperament hides beneath?"  | `personality`, `profession`|
| Element     | "What element calls to them?"      | `schoolOfMagic`            |

### Interaction (CustomerGeneratorTest)

1. Generate → LLM returns customer JSON → `GameManager.currentCustomer` populated (unchanged).
2. The 5 source prose fields are re-rendered by `MemoFillUI.Begin(order, onComplete)` with content words wrapped in TMP `<link>` tags. Stop-words and words shorter than 3 characters are skipped. `customerName` and `constraint` render as plain prose (not clickable).
3. Player clicks a word inside the dossier prose → word is **armed** (tint flips to amber).
4. Player clicks a memo slot button:
   - Source-field accepted → word commits, an ink-blot pops, the source word is struck-through grey.
   - Source-field rejected → slot shakes, nothing commits.
   - Slot already committed → word returns to the source and the slot re-opens.
5. When all 3 slots commit → `PlayerMemo` is stored on `GameManager.currentMemo` → Proceed button enables.

**Soft gate:** any content word from a valid source field is acceptable; mechanic does not enforce a single "correct" answer. The player's interpretation is what they carry forward.

### Downstream consequences

- **MaterialGeneratorTest** — the 7 legacy dossier `TMP_Text` fields blank out once `memoCard` is wired; the `MemoCardUI` (3 words) is the only reference. Each generated material is scored by `IsHintMatch` — cores against `memo.element` vs `elementalAffinity`, woods against `memo.personality` vs `personalityMatch`. Matches get a ✦ glyph in the top-right of the card.
- **CraftingScene** — the same `MemoCardUI` is pinned. No dossier.

## Files

Created:
- `Assets/Scripts/PlayerMemo.cs` — `PlayerMemo` data class + `PlayerMemoField` enum.
- `Assets/Scripts/MemoFillUI.cs` — click-word → click-slot orchestrator + `MemoLinkClickRelay`.
- `Assets/Scripts/MemoCardUI.cs` — read-only 3-slot card for market/crafting.

Modified:
- `Assets/Scripts/GameManager.cs` — added `currentMemo` field; reset in `StartNextRound`, `AdvanceToNextDay`, `ResetForNewPlaythrough`.
- `Assets/Scripts/CustomerGenerator.cs` — added `memoFillUI` field; gates Proceed on `OnMemoComplete`.
- `Assets/Scripts/MaterialGenerator.cs` — added `memoCard` field + `ApplyMemoHints`; blanks legacy dossier when memo card is wired.
- `Assets/Scripts/MaterialCardUI.cs` — added `SetHintGlyph(bool)` with runtime-created ✦ child.
- `Assets/Scripts/CraftingManager.cs` — added `memoCard` field; populates in `Start`.

## TODO-EDITOR (scene wiring)

**CustomerGeneratorTest scene:**
- Create `MemoPanel` under Canvas. Add `MemoFillUI` component.
- Create 3 slot rows, each a child GameObject with:
  - A question-prompt label `TMP_Text` (static — can be customized in Inspector).
  - An empty committed-word `TMP_Text`.
  - A `Button` + transparent `Image` covering the row.
- On `MemoFillUI`, assign: the 5 existing dossier `TMP_Text` fields (request, trueGoal, personality, profession, school) as sources, plus the 3 slot value texts and 3 slot buttons.
- Assign `MemoFillUI` to `CustomerGenerator.memoFillUI`.
- The existing dossier `TMP_Text` fields must have `raycastTarget = true` (usually true by default) so pointer clicks register.

**MaterialGeneratorTest scene:**
- Create `MemoCardPanel` under Canvas. Add `MemoCardUI` component.
- Create 3 child label + value `TMP_Text` pairs. Wire on `MemoCardUI`.
- Assign the component to `MaterialGenerator.memoCard`.
- Delete or disable the 7 legacy dossier `TMP_Text` GameObjects — the memo replaces them.

**CraftingScene:**
- Same memo card setup as MaterialGeneratorTest.
- Assign the component to `CraftingManager.memoCard`.

### Canvas note
If the scene Canvas is **Screen Space - Camera** or **World Space**, edit `MemoFillUI.OnSourceClicked` and replace the `null` passed to `TMP_TextUtilities.FindIntersectingLink` with the Canvas's event camera. Screen Space - Overlay (the default) works with `null` as-is.

## Verification

1. Play `CustomerGeneratorTest`:
   - Generate → dossier prose appears; content words in request/trueGoal/personality/profession/school are underlined and tinted blue.
   - Click a word in `request` → turns amber (armed).
   - Click the Purpose slot → word commits, ink blot pops, source word becomes struck-out grey.
   - Click a word in `schoolOfMagic` → armed.
   - Click the Personality slot → shake (rejected).
   - Click the Element slot → commit.
   - Click the Purpose slot → un-commits, word returns to `request` as a fresh link.
   - Fill all 3 slots → Proceed enables.
2. Proceed to `MaterialGeneratorTest`:
   - Memo card pinned; 7 dossier fields are empty or removed.
   - ✦ glyph shows on cards matching memo keywords.
3. Proceed to `CraftingScene` → memo card pinned; no dossier.
4. Regenerate on CustomerGeneratorTest without proceeding → memo resets cleanly, links rebuild.
5. Load `CraftingScene` directly with no memo on `GameManager` → memo card shows `—` placeholders, no NullReferenceException.

## Out of scope (future iterations)

- **Reading sub-score in evaluation** — compare memo to customer's actual profile, award a separate reading score alongside craft match.
- **Contradiction ⚠ warnings** — non-blocking nudge when memo keywords contradict each other.
- **Free-text gut feeling field** for trueGoal with LLM fuzzy match.
- **Difficulty scaling by reputation tier** — e.g. higher rep = distractor words in every source field.
