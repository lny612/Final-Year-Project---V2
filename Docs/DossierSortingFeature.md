# Dossier Sorting Feature — Design Doc

**Status:** Superseded by `Docs/MemoFeature.md` (2026-04-24); the memo replacement is now live in production via `DossierPanelController` (UI Toolkit). The 7-slot sort proved to be busywork with no downstream consequence; the memo mechanic distils the dossier into 3 keywords that carry into market + crafting scenes instead. This doc is kept for reference.
**Scenes affected:** `CustomerGeneratorTest`, `CraftingScene`

## Problem

Reading the customer dossier is currently passive. After the LLM returns, all 7 fields (name, school, profession, personality, request, trueGoal, constraint) are dumped into static `TMP_Text` blocks — the player reads a paragraph, clicks Proceed, and by the time they reach `CraftingScene` the information is gone from screen. Nothing rewards careful attention, and nothing helps the player recall what the customer wanted while choosing materials.

## Goals

1. Make reading **active** — player must interact with the text to advance.
2. Make reading **useful at craft time** — the information stays on screen when it matters.
3. **No new art** — procedural UI only, same patterns already used in `TracingMinigameUI` and `CraftingManager.CreateInventoryRow`.

## Design: Fill-the-Card + Pinned Reference

### Interaction in `CustomerGeneratorTest`

1. Player clicks **Generate** → LLM returns customer JSON (unchanged).
2. `GameManager.Instance.currentCustomer` is populated from the response (unchanged — game state is valid regardless of sorting).
3. The 7 returned strings are loaded as **shuffled phrase buttons** in a Phrase Pool panel. The 7 labeled slots (Name, School, Profession, Personality, Request, True Goal, Constraint) display empty placeholders (`NAME: ___`, etc.).
4. Player clicks a phrase button → it becomes "armed" (highlighted).
5. Player clicks a slot:
   - **Correct match** → phrase text fills slot (`<b>NAME:</b>  Elyra Voss`), phrase button is destroyed, slot locks.
   - **Wrong match** → brief red shake/flash on both slot and phrase, armed state clears.
6. When all 7 slots are committed → **Proceed** button enables → advances to `MaterialGeneratorTest`.

**Why this is non-trivial:** `request` vs `trueGoal` are semantically close (both first-person-ish statements about what the customer wants); `personality` vs `constraint` both describe the person. The player has to read carefully to place each correctly. This leverages the existing `trueGoal`/`request` tension in the prompt without needing new LLM content.

**v1 scope:** No distractors — 7 real phrases, 7 real slots, strict validation.

### Pinned dossier in `CraftingScene`

A read-only card displaying all 7 fields of `GameManager.Instance.currentCustomer`, anchored on the side of the crafting screen. Populated once in `Start()`. Same formatting as the Case File: `<b>LABEL:</b>  value`.

## Files to modify / create

### Modify — `Assets/Scripts/CustomerGenerator.cs`
- Keep the LLM call and `currentCustomer` push to `GameManager` (existing lines 293–308).
- Remove the direct `PopulateDossier(customer)` UI call at line 293.
- After LLM response, hand off to `DossierSortingUI.Begin(order, slotTexts, slotKeys, onAllCorrect)` passing the existing 7 `TMP_Text` references.
- `proceedButton.interactable` gated on `onAllCorrect` callback, not on generation finish.
- Add Inspector field: `public DossierSortingUI dossierSorter;`.

### New — `Assets/Scripts/DossierSortingUI.cs`
- Orchestrates the fill-the-card interaction.
- `Begin(CustomerOrder order, TMP_Text[] slotTexts, string[] slotKeys, Action onAllCorrect)`.
- Inspector: `Transform phrasePoolContainer;`, `Color armedColor`, `Color wrongFlashColor`.
- On `Begin`:
  - Reset the 7 slot `TMP_Text`s to placeholders.
  - Ensure each slot's parent has a raycast-receiving `Image` and attach a `Button` at runtime; wire to an `OnSlotClicked(i)` handler.
  - Destroy existing children of `phrasePoolContainer`, create 7 shuffled phrase buttons procedurally (template: `CraftingManager.CreateInventoryRow` at line 127 — `GameObject` + `RectTransform` + `Image` + `Button` + child `TextMeshProUGUI`).
- Internal state: `int _armedPhraseIndex = -1`, `HashSet<int> _committedSlots`, phrase→key map.
- On phrase click: set armed, recolor; on slot click: if armed phrase's key matches slot's key → commit (format `<b>LABEL:</b>  {phrase}`, destroy button, add to committed set, if `.Count == 7` → fire callback). Else → `ShakeFlash` coroutine on slot + phrase, clear armed state.

### Modify — `Assets/Scripts/CraftingManager.cs`
- Add Inspector field: `public DossierCardUI pinnedDossier;`.
- In `Start()` (after line 96): `pinnedDossier?.Populate(GameManager.Instance?.currentCustomer);`.

### New — `Assets/Scripts/DossierCardUI.cs`
- Thin read-only component.
- 7 public `TMP_Text` fields (name, school, profession, personality, request, trueGoal, constraint).
- `public void Populate(CustomerOrder o)` — reuses the existing `<b>LABEL:</b>  value` formatting from `CustomerGenerator.PopulateDossier` (line 322). Null-safe: blanks everything if `o` is null.

### Unchanged
- `CustomerOrder.cs` — no data model change. All 7 fields already exist and already persist via `GameManager.Instance.currentCustomer`.

## TODO-EDITOR (scene wiring)

**CustomerGeneratorTest scene:**
- Create `DossierSortingPanel` GameObject under Canvas. Add `DossierSortingUI` component.
- Create child `PhrasePoolContainer` (RectTransform, preferably with a VerticalLayoutGroup) and assign to `DossierSortingUI.phrasePoolContainer`.
- Assign `DossierSortingUI` to `CustomerGenerator.dossierSorter`.
- The existing 7 dossier `TMP_Text` fields become the Case File slots — ensure each (or its immediate parent) has a raycast-receiving `Image` so clicks register. The `Button` is added at runtime.

**CraftingScene:**
- Create `PinnedDossierPanel` GameObject under Canvas (anchored top-right or side, VerticalLayoutGroup recommended). Add `DossierCardUI` component.
- Create 7 child `TMP_Text` objects (Name, School, Profession, Personality, Request, TrueGoal, Constraint).
- Wire all 7 fields on `DossierCardUI`, then assign the component to `CraftingManager.pinnedDossier`.

## Verification

1. Play `CustomerGeneratorTest`:
   - Generate → sorting UI shows 7 shuffled phrase buttons + 7 empty slots.
   - Correct matches commit; wrong matches flash red.
   - All 7 correct → Proceed enables.
   - Regenerate without proceeding → state resets cleanly.
2. Proceed through `MaterialGeneratorTest` → `CraftingScene` → pinned dossier panel shows the same 7 fields, unchanged from what the player sorted.
3. Load `CraftingScene` directly with no customer in `GameManager` → pinned panel shows blank fields, no `NullReferenceException`.
4. Check `read_console` after each script change for compile errors before Editor testing.

## Out of scope (future iterations)

- Distractor phrases (needs extra LLM content).
- Detective hidden-goal puzzle (player guesses `trueGoal` from options) — could layer on top later.
- Difficulty scaling (more/fewer slots by reputation tier).
