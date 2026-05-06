# Memo-Gated Dossier Reading — Design Doc

**Status:** Live in production (UI Toolkit) since 2026-04-29.
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
- `Assets/Scripts/DossierPanelController.cs` — **production** UI Toolkit driver. Renders dossier prose with clickable word links inside `Assets/UI/Dossier/DossierPanel.uxml`, runs the click-word → click-slot orchestration, gates Proceed, animates the ink-blot pop on commit, and shakes the row on rejection. Calls `CustomerGenerator.OnMemoComplete` when all 3 slots commit.
- `Assets/Scripts/MemoFillUI.cs` — *legacy uGUI* fallback (kept in repo for any non-UIDocument scenes; not used in production today). Same orchestration logic at the Canvas level.
- `Assets/Scripts/MemoCardUI.cs` — read-only 3-slot card displayed in market/crafting. Used by both UI layers (still uGUI on those screens).

Modified:
- `Assets/Scripts/GameManager.cs` — added `currentMemo` field; reset in `StartNextRound`, `AdvanceToNextDay`, `ResetForNewPlaythrough`.
- `Assets/Scripts/CustomerGenerator.cs` — added both `dossierPanel` (UI Toolkit) and `memoFillUI` (legacy fallback) fields; prefers `dossierPanel` when wired and falls back to `memoFillUI`. Gates Proceed on `OnMemoComplete`.
- `Assets/Scripts/MaterialGenerator.cs` — added `memoCard` field + `ApplyMemoHints`; blanks legacy dossier when memo card is wired.
- `Assets/Scripts/MaterialCardUI.cs` — added `SetHintGlyph(bool)` with runtime-created ✦ child.
- `Assets/Scripts/MaterialMarketUI.cs` — UI Toolkit market exposes `SetHintGlyph(globalIndex, show)` for the `card-hint-glyph` Label and `RefreshMemo(memo)` for the pinned 3-slot card.
- `Assets/Scripts/CraftingManager.cs` — added `memoCard` field; populates in `Start`.
- `Assets/Scripts/CraftingWorkbenchUI.cs` — UI Toolkit workbench exposes `SetMemo(memo)` to populate the pinned memo card on the workbench panel.

## Wiring (today)

**CustomerGeneratorTest** — UI Toolkit production wiring:
- `Assets/UI/Dossier/DossierPanel.uxml` + `DossierPanel.uss` + `DossierPanelSettings.asset` define the layout.
- A `DossierUIDocument` GameObject in `2.CustomerGeneratorTest.unity` carries a `UIDocument` (with the panel settings + UXML source) and a `DossierPanelController` component.
- `CustomerGenerator.dossierPanel` references the controller; on memo completion it fires `OnMemoComplete` which enables the Proceed button and stores the result on `GameManager.currentMemo`.
- The legacy uGUI Canvas children remain in the scene but are disabled. If you ever need to revert, set the Canvas children active and assign the `MemoFillUI` component instead via `CustomerGenerator.memoFillUI`.

**MaterialGeneratorTest** — uGUI today:
- `MemoCardUI` component sits on a `MemoCardPanel` GameObject under Canvas with three label + value `TMP_Text` children.
- `MaterialGenerator.memoCard` references it; `MaterialGenerator.ApplyMemoHints` toggles ✦ glyphs on cards whose `elementalAffinity` (cores) or `personalityMatch` (woods) overlaps the memo via `IsHintMatch` substring search.
- A UXML/USS market layout is authored at `Assets/UI/Market/` but the UIDocument GameObject hasn't been wired yet, so the uGUI driver is still authoritative. When the UI Toolkit version is wired, `MaterialMarketUI.RefreshMemo` and `SetHintGlyph` mirror the same behaviour.

**CraftingScene** — uGUI memo card pinned the same way:
- `MemoCardUI` on a panel under Canvas, wired into `CraftingManager.memoCard`. `CraftingManager.Start()` calls `Populate(GameManager.Instance?.currentMemo)`.
- The UI Toolkit `CraftingWorkbenchUI.SetMemo(memo)` mirrors this for the new workbench panel.

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
