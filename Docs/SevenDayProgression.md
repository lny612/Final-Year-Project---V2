# 7-Day Progression, Letters, Rent & Multi-Ending

> **Implementation Status (2026-05-06)**
>
> This doc was the original implementation plan. Most of it shipped as written. The annotations below reconcile the plan with what's actually in the repo today; the rest of the doc is preserved for design context.
>
> **✅ Shipped:**
> - `GameManager` state additions (`currentDay`, `peakReputation`, `wandsCrafted`, `lettersReceived`, `bankruptedOnDay3`, rent constants, `ROYAL_REP_MIN`/`RIVAL_REP_MIN`)
> - `AdvanceToNextDay`, `IsRentDueToday`, `GetRentDueToday`, `DetermineEnding`
> - `LetterLibrary` (21 letters across 8 senders — `Neighbor`, `Customer`, `Aristocrat`, `Royal`, `Brigand`, `Landlord`, **+ `Aunt`** for the day-1 intro, **+ `Rival`** for days 5/7)
> - `TypewriterText` (with `OnComplete` event + `PlayFromStart` fire-and-forget)
> - `DayProgressUI` (7 dots + rent icons on indices [2] and [5])
> - `RentPaymentUI` (`payButton` / `pleadButton` / `acceptFateButton`, `RentResult.Paid`/`Bankrupt`)
> - `EndingManager` (5 variants — `Royal`/`Rival`/`Slum`/`BankruptEarly`/`BankruptLate`)
> - Editor-only `[ContextMenu]` shortcuts on `GameManager` for fast iteration (jump to day 3/6/7, force rep tiers, empty wallet)
>
> **🔄 Diverged from plan:**
> - **Production morning controller is `MorningScreenController.cs` (UI Toolkit)**, not `MorningLetterUI.cs`. The legacy uGUI `MorningLetterUI.cs` is still in the repo but its Canvas children are disabled.
> - **Build Settings list grew to 8 scenes** (Title and MinigameTest were added post-plan). Actual order in §"Scene Setup" below.
> - **`OnNextCustomer()` sets `bankruptedOnDay3` inside `RentPaymentUI`** rather than at the `EvaluationManager` call site (cleaner separation; the call site just calls `GoToEnding()` with no parameter).
> - **`LetterSender` enum is now**: `{ Neighbor, Customer, Aristocrat, Royal, Brigand, Landlord, Aunt, Rival }`.
>
> **🔁 Diverged from plan (illustration loading):**
> - **Ending illustrations are now Inspector-wired**, not loaded by `Resources.Load`. The 5 PNGs (`Royal Ending.png`, `Rival Ending.png`, `Slum Ending.png`, `Bankrupt Early Ending.png`, `Bankrupt Late Ending.png`) live in `Assets/Texture/Ending Illustrations/` (1024×640, ink-and-watercolour storybook style). `EndingManager` exposes 5 `Texture2D` slots (`royalArt`/`rivalArt`/`slumArt`/`bankruptEarlyArt`/`bankruptLateArt`) under an "Ending Illustrations" header. The original `Resources/EndingArt/` lookup is kept as a fallback only.
>
> **❌ Not yet shipped:**
> - **Inspector wiring on `EndingScene.unity`.** The 5 PNGs are imported but the slots on `EndingManager` are still empty — drag each file from `Assets/Texture/Ending Illustrations/` into its matching slot, then save the scene. Until wired, each ending falls through to the tinted-placeholder canvas with a console warning.

## Context

The game currently loops a single round (Customer → Market → Craft → Evaluate → back to Customer) with no termination and no narrative arc. Reputation and gold persist but never matter — nothing happens when either rises or falls. This plan turns the loop into a **7-day story** with two rent checkpoints, a daily morning-letter narrative beat, and four ending branches tied to player performance. The goal: give the existing mechanics stakes and give the session a shape that ends.

**Session structure** (locked in):
```
Day 1 (intro letter) → 2 → 3 [RENT 250g] → 4 → 5 → 6 [RENT 400g] → 7 → ENDING
```

**Four endings:**
- **Royal Wandmaker** — reputation ≥ 90 at end of day 7 → summoned by the crown
- **Rival Shop** — reputation 30–89 → a copycat opens across the street; bittersweet
- **Slum Wandmaker** — reputation < 30 → drummed out of town, makes shady wands in the alleys
- **Bankrupt** — insufficient gold on rent day → gnomes seize the shop (can trigger on day 3 or day 6)

**Design decisions already confirmed:**
1. Letters are **pre-authored static** (hardcoded in `LetterLibrary.cs`), picked by reputation tier and day
2. Ending illustrations are **pre-generated once** and shipped as PNGs in `Assets/Texture/Ending Illustrations/`, wired to `EndingManager` via Inspector Texture2D slots (legacy `Resources/EndingArt/` path retained as a fallback)
3. Day progress shows as **calendar dots** (● ● ● ○ ○ ○ ○) at top of evaluation screen, with 💰 icons above days 3 and 6

---

## Brainstorm Additions

These go beyond the user's original spec — flagged so they can be accepted or cut:

- **Letter sender types have wax-seal color tags** — aristocrat=gold, royal=purple, brigand=black, neighbor=cream. A single `Image` tinted by sender type; no per-letter art.
- **Day 1 letter = world-building tutorial** — "Welcome to Ashenbury. Your aunt left you this shop. Rent due in 3 days. The town takes note of what you make." Establishes stakes immediately.
- **Rent-day mornings get a second letter** — a landlord reminder the morning of rent day (day 3 and day 6), in addition to the reputation-reactive letter.
- **Bankruptcy ending has two variants** — different illustration/dialogue depending on whether it hit on day 3 (amateur) vs day 6 (almost made it). Cheap extra nuance.
- **Post-ending stats screen** — "Days survived: 7 · Letters received: 8 · Peak reputation: 112 · Wands crafted: 7" shown under the ending illustration. Reinforces the journey.
- **Ending illustration slow fade-in** — 1.5s fade from black so the ending feels weighty, not abrupt.

I'd recommend all six; they're low-cost and materially improve the feel.

---

## System Overview

```
┌─────────────────┐
│  MorningScene   │  owl + letter (typewriter)
│  NEW            │  Day-1: intro letter; Day-3/6: rent-reminder ALSO shown
└────────┬────────┘
         ↓
┌─────────────────┐
│ CustomerScene   │  existing (unchanged logic)
└────────┬────────┘
         ↓
┌─────────────────┐
│ MarketScene     │  existing
└────────┬────────┘
         ↓
┌─────────────────┐
│ CraftingScene   │  existing
└────────┬────────┘
         ↓
┌─────────────────┐
│ EvaluationScene │  + day-dot header (NEW)
│                 │  + rent-payment modal on days 3,6 (NEW)
│                 │  + ending-trigger check after day 7 (NEW)
└────────┬────────┘
         ↓
     (branch)
   day<7 → MorningScene
   day=7 → EndingScene
   bankrupt → EndingScene (bankrupt variant)

┌─────────────────┐
│  EndingScene    │  illustration + dialogue typewriter + stats
│  NEW            │  4 variants (Royal / Rival / Slum / Bankrupt)
└─────────────────┘
```

---

## State Changes: `GameManager.cs`

Add to existing `GameManager` (no refactor of existing fields):

```csharp
// ── Day/progression state ─────────────────────────────────
public const int TOTAL_DAYS = 7;
public static readonly int[] RENT_DUE_DAYS = { 3, 6 };   // rent owed at END of these days
public static readonly int[] RENT_AMOUNTS  = { 250, 400 };

public int currentDay = 1;
public int peakReputation = 0;        // for post-ending stats
public int wandsCrafted = 0;          // for post-ending stats
public bool bankruptedOnDay3 = false; // distinguishes bankrupt ending variants

// ── Scene constants ───────────────────────────────────────
public const string SCENE_MORNING = "MorningScene";
public const string SCENE_ENDING  = "EndingScene";

// ── API ───────────────────────────────────────────────────
public void AdvanceToNextDay()           // called by EvaluationManager after rent resolves
public bool IsRentDueToday() => Array.IndexOf(RENT_DUE_DAYS, currentDay) >= 0;
public int  GetRentDueToday()            // 250 for day 3, 400 for day 6, 0 otherwise
public EndingType DetermineEnding()      // reads playerReputation, returns Royal/Rival/Slum
```

`peakReputation` is updated inside `EvaluationManager.RevealResults()` after the rep change is applied. `wandsCrafted` increments per evaluation that actually evaluated a wand. The existing `StartNextRound()` is **kept for backward compatibility but no longer called by the game flow** — `AdvanceToNextDay()` replaces it as the forward path.

**Rep thresholds** (tuned against current math of −10 to +20 per day, 7 days):
- Royal: ≥ 90 (requires 5+ good days, realistic but not trivial)
- Rival:  30–89
- Slum:   < 30 (includes all negative runs)

These are constants in `GameManager` so they're tweakable in one place.

---

## New Files

### `Assets/Scripts/TypewriterText.cs`
Reusable char-by-char text reveal coroutine. Mirrors the style of `EvaluationManager.FadeInText` (`EvaluationManager.cs:226-240`).

```csharp
public class TypewriterText : MonoBehaviour
{
    public TMP_Text target;
    public float charsPerSecond = 35f;
    public AudioClip tickSfx; // optional
    public IEnumerator Play(string text);
    public void SkipToEnd();  // on click-to-skip
}
```

Used by both `MorningLetterUI` and `EndingManager`. Click-anywhere-to-skip is standard for this genre.

### `Assets/Scripts/LetterLibrary.cs`
Pure static class — no MonoBehaviour, no assets (avoids `.asset` file-safety block).

```csharp
public enum LetterSender { Neighbor, Customer, Aristocrat, Royal, Brigand, Landlord }
public enum RepTier { Low, Mid, High }  // day-scaled — see GetRepTier table below

public struct LetterContent {
    public LetterSender sender;
    public string from;       // "Mrs. Hensley, next door" etc.
    public string subject;    // shown in letter header
    public string body;       // typewriter target
}

public static class LetterLibrary {
    public static LetterContent GetMorningLetter(int day, RepTier tier);
    public static LetterContent GetRentReminder(int day, int amount);
    public static RepTier GetRepTier(int reputation, int day); // day-scaled (2026-05-06)
}
```

**Day-scaled tier thresholds** (since 2026-05-06): the original flat `<30 / 30-89 / ≥90` cutoff matched the endgame Rival/Royal bar but left day 2 stuck on Low even after a perfect day 1 (per-day rep gain caps at ~+20). `GetRepTier(rep, day)` now uses a per-day curve:

| Day | High ≥ | Mid ≥ |
|-----|--------|-------|
| 2   | 15     | 5     |
| 3   | 32     | 12    |
| 4   | 50     | 20    |
| 5   | 65     | 28    |
| 6   | 80     | 36    |
| 7   | 90     | 45    |

Day 7's High bar = `ROYAL_REP_MIN` (90) so the final-morning letter stays consistent with the Royal ending verdict. Endgame thresholds (`ROYAL_REP_MIN`/`RIVAL_REP_MIN`) are separate and unchanged — those read `peakReputation` over the whole run; letter tiers classify trajectory *at that point*.

Content table:
| Day | Low tier | Mid tier | High tier |
|-----|----------|----------|-----------|
| 1 | Intro letter (same for all — aunt's letter explaining the shop, rent schedule, town reputation) |
| 2 | neighbor complaining | neighbor gossiping | neighbor bragging about you |
| 3 | landlord warning + customer complaint | landlord warning + neutral customer | landlord warning + aristocrat commission hint |
| 4 | brigand praising "dark work" | customer asking for refund | minor noble inquiring |
| 5 | gang recruitment offer | rival shopkeeper warning | court mage curious about you |
| 6 | landlord final warning + brigand propositions | landlord final warning + neutral traffic | landlord final warning + royal messenger hint |
| 7 | brigand "come join us" | rival "nice shop, shame if..." | royal summons premonition |

~20 letters total. Written directly in the source file as verbatim strings — cheap to edit, git-diffable.

### `Assets/Scripts/MorningLetterUI.cs` *(legacy uGUI — superseded)*

> **Production note:** The actual production controller is `Assets/Scripts/MorningScreenController.cs` (UI Toolkit, `Assets/UI/Morning/MorningScene.{uxml,uss}`), not `MorningLetterUI.cs`. The legacy uGUI version below remains in the repo as a fallback but its Canvas children are disabled in the scene. The behaviour described is the same; only the rendering layer changed.

Scene controller for `MorningScene`. Mirrors `EvaluationManager` patterns (public Inspector fields, `Start()` wires button, coroutine drives reveal).

```csharp
public class MorningLetterUI : MonoBehaviour {
    public TMP_Text fromText, subjectText;
    public TypewriterText bodyTypewriter;
    public Image owlImage;           // static owl art
    public Image waxSeal;            // tinted by sender type
    public Button continueButton;
    // Start(): look up GameManager.currentDay + rep tier → LetterLibrary.GetMorningLetter
    //          if day 3 or 6, also queue the landlord rent-reminder as a second letter
    //          play typewriter, enable Continue, on click → LoadScene(SCENE_CUSTOMER)
}
```

### `Assets/Scripts/DayProgressUI.cs`
Small component for the calendar-dot header on the evaluation screen.

```csharp
public class DayProgressUI : MonoBehaviour {
    public Image[] dayDots = new Image[7];     // filled ● vs empty ○ via color
    public GameObject[] rentIcons = new GameObject[7]; // only dots 2,5 get an active 💰
    public Color filledColor = Color.white;
    public Color emptyColor  = new Color(1,1,1,0.3f);
    // Refresh() reads GameManager.currentDay, sets colors & icons
}
```

Called from `EvaluationManager.Start()` after the panel is built.

### `Assets/Scripts/RentPaymentUI.cs`
Modal panel on the evaluation scene. Activated by `EvaluationManager` after reward reveal if `IsRentDueToday()` returns true.

```csharp
public class RentPaymentUI : MonoBehaviour {
    public GameObject panel;                // inactive at Start()
    public TMP_Text landlordDialogue;       // 1-2 short lines, typewritten
    public TMP_Text goldStatusText;         // "You have 467g. Rent is 250g. After rent: 217g."
    public Button payButton, pleadButton, acceptFateButton;  // plead is flavor-only;
                                                              // acceptFate appears only when can't afford
    public TypewriterText dialogueTypewriter;
    // Public method: Show(int rentAmount, Action onResolved)
    //   if gold >= rent: enable payButton, deduct on click, callback
    //   if gold <  rent: disable payButton, show "You can't pay." → triggers bankrupt ending
}
```

### `Assets/Scripts/EndingManager.cs`
Scene controller for `EndingScene`. Reads `GameManager.DetermineEnding()` and `GameManager.bankruptedOnDay3`, picks the correct illustration + dialogue.

```csharp
public enum EndingType { Royal, Rival, Slum, BankruptEarly, BankruptLate }

public class EndingManager : MonoBehaviour {
    public RawImage illustration;         // assigned the chosen Texture2D at runtime
    public TMP_Text endingTitle;
    public TypewriterText dialogueTypewriter;
    public TMP_Text statsText;            // "Days survived: 7 · Letters: 8 · Peak rep: 112"
    public Button restartButton;
    public CanvasGroup fader;             // for 1.5s fade-in

    // Inspector-wired ending illustrations (one per ending).
    public Texture2D royalArt, rivalArt, slumArt, bankruptEarlyArt, bankruptLateArt;

    // Start(): determine ending, pick the matching Texture2D from the 5 slots
    //          (fallback: Resources.Load<Texture2D>($"EndingArt/{name}") if slot empty,
    //          then a tinted placeholder), fade canvas from black, play dialogue
    //          typewriter, show stats, enable restart.
    // Restart button: reset GameManager state to day 1, load SCENE_MORNING.
}
```

Ending dialogue lines are static strings inside `EndingManager.cs` (4 endings × ~6 lines each = trivial).

### `Assets/Texture/Ending Illustrations/` — **art folder (shipped 2026-05-07)**
Five PNGs: `Royal Ending.png`, `Rival Ending.png`, `Slum Ending.png`, `Bankrupt Early Ending.png`, `Bankrupt Late Ending.png`. Generated externally at 1024×640 (8:5) in an ink-and-watercolour storybook style to match `Assets/UI references/Image Style/Portrait style reference 2.png`. Each PNG is wired into `EndingManager` via a dedicated Inspector `Texture2D` slot (`royalArt` / `rivalArt` / `slumArt` / `bankruptEarlyArt` / `bankruptLateArt`).

The original `Assets/Resources/EndingArt/{Royal,Rival,Slum,Bankrupt}.png` path is retained as a runtime fallback in case a slot is empty, but it is no longer the primary source.

---

## Modified Files

### `Assets/Scripts/GameManager.cs`
- Add fields listed above (currentDay, peakReputation, wandsCrafted, bankruptedOnDay3, SCENE_MORNING, SCENE_ENDING, rent constants)
- Add `AdvanceToNextDay()`, `IsRentDueToday()`, `GetRentDueToday()`, `DetermineEnding()`
- Do NOT modify `StartNextRound()` — leave it so the existing MinigameTest and other entry points still work. New flow uses `AdvanceToNextDay`.

### `Assets/Scripts/EvaluationManager.cs`
Three additions, all at well-defined hook points:

1. **Line ~60 (Start):** instantiate `DayProgressUI.Refresh()`, increment `wandsCrafted`, update `peakReputation`.
2. **Line ~170 (after rep change applied in RevealResults):** update `peakReputation = Mathf.Max(peakRep, currentRep)`.
3. **Replace `OnNextCustomer()` body (line 258–261):**
   ```csharp
   private void OnNextCustomer() {
       if (GameManager.Instance.IsRentDueToday()) {
           rentPaymentUI.Show(GameManager.Instance.GetRentDueToday(), afterRent => {
               if (afterRent == RentResult.Paid)    ProceedToNextDayOrEnding();
               else /* Bankrupt */                  GoToEnding(bankruptEarly: currentDay==3);
           });
       } else {
           ProceedToNextDayOrEnding();
       }
   }

   private void ProceedToNextDayOrEnding() {
       if (GameManager.Instance.currentDay >= GameManager.TOTAL_DAYS) {
           GameManager.Instance.LoadScene(GameManager.SCENE_ENDING);
       } else {
           GameManager.Instance.AdvanceToNextDay();   // increments day, loads SCENE_MORNING
       }
   }
   ```

### `Assets/Scripts/CustomerGenerator.cs`
**No code change.** The morning letter runs *before* customer generation in its own scene; customer generation is unchanged. Reputation tier could be passed into the customer prompt in a future pass (already noted in CLAUDE.md as an unimplemented GDD feature) — out of scope here.

---

## Scene Setup (TODO-EDITOR comments I'll add in the code)

Two new scenes must be created by the user in the Unity Editor. I will add `TODO-EDITOR:` comments to `MorningLetterUI.cs` and `EndingManager.cs` with explicit hierarchy + wiring instructions, matching the style of the existing comment at `CraftingManager.cs:59`.

> **Actual Build Settings (post-plan, 2026-05-06):** the project grew to 8 scenes — Title and MinigameTest were added after this plan was written:
> ```
> 0. 0.TitleScene.unity                 ← NEW (added post-plan)
> 1. 2.CustomerGeneratorTest.unity
> 2. 3.MaterialGeneratorTest.unity
> 3. 4.CraftingScene.unity
> 4. 5.MinigameTest.unity               ← NEW (added post-plan)
> 5. 6.EvaluationScene.unity
> 6. 1.MorningScene.unity
> 7. 7.EndingScene.unity
> ```
> The `N.` filename prefixes order them in the Project window; build indices match the order above. The original 6-scene proposal (below) is kept for design context.

Original proposed Build Settings order:

```
0. CustomerGeneratorTest
1. MaterialGeneratorTest
2. CraftingScene
3. EvaluationScene
4. MorningScene         ← NEW
5. EndingScene          ← NEW
```

Additionally, on `EvaluationScene`, the user must:
- Add `DayProgressUI` component to a new GameObject "DayProgressHeader" at top of canvas, wire the 7 dot Images + 7 rent-icon GameObjects, assign on `EvaluationManager.dayProgressUI`
- Add `RentPaymentUI` panel (inactive) under canvas, wire fields, assign on `EvaluationManager.rentPaymentUI`

Exact TODO-EDITOR text will be in the source files.

---

## Implementation Order

1. **Data layer first** — `GameManager` state additions + `LetterLibrary` content (no Unity dependencies, pure C#, easy to validate)
2. **Reusable UI primitive** — `TypewriterText.cs` (used by three scenes)
3. **Evaluation-scene changes** — `DayProgressUI`, `RentPaymentUI`, `EvaluationManager` hook edits
4. **Morning scene** — `MorningLetterUI` + scene setup TODO
5. **Ending scene** — `EndingManager` + scene setup TODO
6. **Art prep** — generate 5 ending PNGs at 1024×640 in an ink-and-watercolour storybook style, save to `Assets/Texture/Ending Illustrations/`, then drag each into the matching Inspector slot on `EndingManager` in `7.EndingScene.unity`

Each step compiles and runs standalone — no big-bang merge.

---

## Critical Files to Create or Modify

**Create:**
- `Assets/Scripts/TypewriterText.cs`
- `Assets/Scripts/LetterLibrary.cs`
- `Assets/Scripts/MorningLetterUI.cs`
- `Assets/Scripts/DayProgressUI.cs`
- `Assets/Scripts/RentPaymentUI.cs`
- `Assets/Scripts/EndingManager.cs`
- `Assets/Texture/Ending Illustrations/{Royal,Rival,Slum,Bankrupt Early,Bankrupt Late} Ending.png` (image assets, externally generated at 1024×640, wired via `EndingManager` Inspector slots)

**Modify:**
- `Assets/Scripts/GameManager.cs` — add fields + methods listed above (no existing-behavior changes)
- `Assets/Scripts/EvaluationManager.cs` — 3 insertion points: Start(), RevealResults(), replace OnNextCustomer()

**Do not touch:**
- `CustomerGenerator.cs`, `MaterialGenerator.cs`, `CraftingManager.cs` — untouched
- Any `.unity`, `.prefab`, `.asset`, `.meta` files — scene changes via TODO-EDITOR

---

## Verification

End-to-end smoke test after implementation:

1. **Day counter** — play one customer round from CustomerGeneratorTest, reach evaluation, confirm calendar dots show 1 filled / 6 empty, coin icons above dots 3 & 6
2. **Morning letter** — click Continue on day 1's evaluation; a MorningScene should load showing an owl + typewritten letter; continue → back to CustomerGeneratorTest with currentDay=2
3. **Rent check (day 3)** — artificially set `GameManager.playerGold = 100` before day 3 evaluation's Continue; confirm rent modal shows "Can't pay" path; confirm ending scene loads with Bankrupt variant
4. **Rent check (pay path)** — with gold ≥ 250 on day 3, confirm payment deducts correctly and game advances to day 4 morning
5. **Each ending** — force `playerReputation` to 150/60/0 before day 7 Continue; confirm correct ending illustration + dialogue + stats appear
6. **Typewriter skip** — click during typewriter; text should jump to full immediately
7. **Restart** — ending scene's Restart button resets currentDay=1, playerGold=500, playerReputation=0, clears inventory, reloads MorningScene

A `GameManager` developer shortcut (editor-only) to jump-to-day-N and force-rep-N will be added as `[ContextMenu]` methods to speed up testing without 7 live playthroughs.

**Compilation check after each new script:** use `read_console` via the UnityMCP tool to catch errors before wiring.
