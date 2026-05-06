# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Fantasy wand-crafting game built in **Unity 6** (6000.0.58f2) using **URP**. Players receive AI-generated customer orders, buy materials, craft wands, and get scored — all driven by OpenAI GPT-4o for text and a local ComfyUI server for image generation.

## Architecture

### Game Loop (7-day session, 8 production scenes cycled via GameManager)

```
TitleScene (Start) → MorningScene (letter) → CustomerGeneratorTest →
  MaterialGeneratorTest → CraftingScene (selection only) →
  MinigameTest (tracing + parallel wand-gen) → EvaluationScene (theatrical result) →
  [rent if day 3/6] → (day<7: MorningScene next day) |
  (day=7 or bankrupt: EndingScene → TitleScene/restart)
```

Scene files live at `Assets/Scenes/{0..7}.{Name}.unity` — the `N.` prefix orders them in the Project window. `SceneManager.LoadScene` matches by suffix, so `GameManager.SCENE_*` constants stay un-prefixed (`"TitleScene"`, `"MinigameTest"`, etc.). Build settings ordered: 0 Title · 1 Morning · 2 Customer · 3 Material · 4 Crafting · **5 MinigameTest (production)** · 6 Evaluation · 7 Ending.

0. **TitleScene** — `TitleScreenController` (UI Toolkit, parchment-style menu via `Assets/UI/Title/TitleScreen.uxml`+`.uss`). Start button calls `GameManager.ResetForNewPlaythrough()` and loads `MorningScene` (day 1 opens with the aunt's intro letter). Quit exits the app / stops play mode. Legacy `TitleScreenUI.cs` (uGUI) still in repo but its Canvas widgets are disabled — kept for fallback/reference only.
1. **MorningScene** — `MorningScreenController` (UI Toolkit, `Assets/UI/Morning/MorningScene.{uxml,uss}`). Background uses `Assets/Texture/Morning Scene.png` (cozy fantasy shop interior). HUD: parchment day plaque (top-left), gold purse (top-right), centered parchment letter panel with wax seal + ScrollView body, "Read the Dossier" continue button. Pulls a reputation-tiered letter from `LetterLibrary` and types it out via the typewriter. On rent-due mornings (days 3 and 6) the landlord rent-reminder is queued after the main letter. Legacy `MorningLetterUI` Canvas children disabled.
2. **CustomerGeneratorTest** — GPT-4o generates a fantasy customer with request/trueGoal/constraint. UI Toolkit dossier panel (`DossierPanelController`) gates Proceed on a 3-entry memo.
3. **MaterialGeneratorTest** — GPT-4o generates 6 materials (3 cores + 3 woods), ComfyUI generates pixel art images; player buys ≥1 core + ≥1 wood. UXML/USS authored but UIDocument GO not yet wired (still uGUI driver).
4. **CraftingScene** — Player assigns materials to 3 slots (2 core + 1 wood). `CraftingManager` is *selection only* now — on confirm it stashes picks on `GameManager.chosenCore1/Core2/Wood`, removes them from inventory, and calls `LoadScene(SCENE_MINIGAME)`. UI Toolkit workbench via `CraftingWorkbenchUI`.
5. **MinigameTest** — production scene that hosts the tracing ritual and the wand-generation pipeline in parallel (see "MinigameScene handoff" below). `MinigameSceneRunner` orchestrates both; on completion it loads `EvaluationScene`.
6. **EvaluationScene** — GPT-4o scores the wand match (0–100). `EvaluationResultController` (UI Toolkit) drives a theatrical reveal: banner → wand → scoreboard (Conjuring/Materials/Customer Fit/Reward) → final letter Grade. Companion VFX: `WandSparkles` ParticleSystem + `PostFX Volume` (URP Bloom). Day-progress dots are still computed but not part of the new reveal; rent-payment modal pops on days 3/6 before advance. Day 7 or bankruptcy routes to EndingScene.
7. **EndingScene** — `EndingManager` picks one of 5 variants (Royal / Rival / Slum / BankruptEarly / BankruptLate), fades in a pre-generated illustration from `Resources/EndingArt/`, plays a dialogue typewriter, and offers restart.

Additional scenes: `ComfyUITest` (standalone image-generation test), `SampleScene` (unused).

### Day Advance vs Round Reset (`GameManager`)

- **`AdvanceToNextDay()`** — new forward path called by `EvaluationManager`. Increments `currentDay`, clears per-round state, loads `MorningScene`. **Persisted across days:** `inventory`, `playerGold` (starts at 500), `playerReputation`, `peakReputation`, `wandsCrafted`, `lettersReceived`.
- **`StartNextRound()`** — legacy path kept for `MinigameTest` and standalone entry points. Does NOT increment day or route through the morning scene.
- **`ResetForNewPlaythrough()`** — called by the ending's Restart button; resets everything to day 1 defaults.
- **Rent schedule:** `RENT_DUE_DAYS = {3, 6}`, `RENT_AMOUNTS = {250, 400}`. Billed at end of day 3 and day 6 via `RentPaymentUI` modal on the evaluation screen. Insufficient gold → Bankrupt ending (Early if day 3, Late if day 6).
- **Ending thresholds:** `ROYAL_REP_MIN = 90` (≥ → Royal), `RIVAL_REP_MIN = 30` (≥ → Rival), else Slum. Per-day rep delta is −10 to +20, so ~5 good days is needed for Royal.

### Key Patterns

- **GameManager** is a `DontDestroyOnLoad` singleton holding all cross-scene state (customer, materials, inventory, gold, reputation, wand result, craftingQualityGrade). Access via `GameManager.Instance`.
- Each scene has its own manager script (CustomerGenerator, MaterialGenerator, CraftingManager, EvaluationManager) that owns its UI references and API call coroutines
- All OpenAI calls use `UnityWebRequest` POST to chat completions, expecting JSON-only responses parsed with `Newtonsoft.Json.Linq`
- ComfyUI integration: POST workflow JSON to `/prompt` → poll `/history/{id}` every 1.5s (60s timeout) → GET `/view` to download image as Texture2D. All three image-gen scripts have a `FindNodeByClass` fallback that resolves nodes by `class_type` if the configured node ID doesn't match the workflow (guards against stale Inspector overrides).
- **Duplicated helpers** — `LoadApiKey`, `PostOpenAI`, and `StripCodeFences` are copy-pasted in MaterialGenerator, CraftingManager, and EvaluationManager. CustomerGenerator has the same logic inlined differently. When modifying API call helpers, update all four scene manager files.
- **Duplicated prompts** — `MaterialGenerator.cs` contains a full copy of the customer generation prompts from `CustomerGenerator.cs` (noted in a code comment: "kept here so this script is self-contained"). If you change customer generation prompts, update both files.

### External Dependencies

- **OpenAI API** — API key loaded at runtime from `Assets/StreamingAssets/config.json` (`{"openAIApiKey": "..."}`). This file must NOT be committed to git.
- **ComfyUI** — must be running locally at `http://127.0.0.1:8000`; uses Nunchaku INT4 quantized Z-Image Turbo model (`svdq-int4_r32-z-image-turbo.safetensors`) with `qwen_3_4b` CLIP encoder
- **ComfyUI workflow** — `Assets/StreamingAssets/image_z_image_turbo.json`; prompt injected at node `"5"` (CLIPTextEncode), seed randomized at node `"4"` (KSampler)

### Tracing Minigame Subsystem

The crafting ritual spans 4 tightly coupled scripts:

- `TracingMinigameUI` — orchestrator: runs 3 rounds, builds path/fill-segment/gate visuals procedurally as UI GameObjects, updates red/blue trail fill each frame, generates per-round gate-type composition, tracks successes, computes final grade (3 wins→A, 2→B, 1→C, 0→F)
- `TracingCursor` — player input: follows mouse along path with forward-only movement. Carries a `GateState` machine (None / Tapping / Holding / AccentFlicking / AccentReturning) that blocks cursor advance while resolving the active gate. Also exposes `WarpOsMouseToStart()` which uses `UnityEngine.InputSystem.Mouse.current.WarpCursorPosition` (guarded by `ENABLE_INPUT_SYSTEM`) to snap the OS cursor to path t=0 at the start of every round
- `TracingMist` — timer: advances MistT at constant speed; when MistT reaches 1.0 the round is lost. No visual dot — the red fill is rendered by TracingMinigameUI's fill segments. **The mist keeps advancing during all gate-resolution states**, so Hold/Accent gates cost real time
- `RunePathData` — static data: normalized control points for 6 rune shapes (single-measure conductor gestures — Maestoso 4/4, Valse 3/4, Compound 6/8, Take Five 5/4, Quick March 2/4, Fermata Crescendo), Catmull-Rom interpolation, cumulative distance math, plus `CountOverlapPairs` / `HasSelfOverlap` / `ValidateAll` — an editor-only (`UNITY_EDITOR || DEVELOPMENT_BUILD`) self-overlap check run from a static constructor that warns when a shape's sampled path has >6 within-threshold pairs at ≥0.15 arc-length gap

**Gate types (three, `GateType` enum):**
- **Tap** — amber square; press the displayed key once to clear.
- **Hold** — cyan square with a translucent green halo behind (shows the fill target); press AND hold the key for `HOLD_GATE_DURATION` (0.9 s); inner green overlay grows with `TracingCursor.HoldProgress`; early release fizzles in place and the player can retry. Late release is free (but mist ate the extra time).
- **Accent** — magenta square with a protruding magenta line + chevron pointing in a required flick direction (one of 8 compass points, also shown as a Unicode arrow `↑↗→↘↓↙←↖` after the key letter). Press the key, then flick the mouse ≥ `ACCENT_FLICK_MIN_DIST` (60 px) in the shown direction (±30°, `ACCENT_DIR_TOLERANCE` = cos 30°), then return to within `pathTolerance` of the gate.

**Round escalation** (in `TracingMinigameUI.GenerateGateTypes`): R1 = 5 Tap; R2 = 4 Tap + 1 Hold (random index); R3 = 3 Tap + 1 Hold + 1 Accent (two different random indices). Keys, hold durations, and accent directions are generated per round. The pulse animation reads from `_gateBaseColors[gi]` so each gate pulses in its own color.

**Visual mechanic:** The trail fills red from the start (time-based, via TracingMist speed) and blue from the start (player-traced, via TracingCursor progress). Blue overrides red where the player has traced. Each round is a single attempt — no retries. 5 gates per round at the rune's ictus points.

All visuals are procedural UI (`Image` + `TextMeshProUGUI` components on dynamically created `GameObject`s) — no prefabs, no scene references beyond the minigame panel. `MinigameSceneRunner` (the production controller in `5.MinigameTest.unity`) auto-starts the minigame on scene load AND launches the wand-generation pipeline in parallel.

**Z-Image Turbo sprite injection (2026-04-29).** Each visual now optionally accepts a Sprite (Inspector field on `TracingMinigameUI`): `trailStripeSprite` (with `trailStripeTrim` Vector4 cropping the source band — defaults to `(27, 237, 27, 237)` for `Trail 2.png`), `gateMedallionSprite`, `cursorWispSprite`, `endpointPlaqueSprite`, `mistPuffSprite`. When a sprite is null, the procedural rectangle fallback runs as before. Trail sprite is applied to the *main* path pass in `BuildSegments` (`Image.Type.Simple`), not the fill overlay. Cursor + endpoint plaque are forced to `SetAsLastSibling` so they render above gates. Endpoint position is `path[path.Length - 1]`, not the last gate. Source PNGs in `Assets/Texture/MinigameSprites/` (each has a `-removebg-preview` alpha-cut variant for clean compositing).

**Mist VFX.** `EmitMistPuffs(dt)` runs once per frame inside the round loop; spawns UI puffs at `RunePathData.SampleAt(_activePath, _activeCumul, _mist.MistT)` with random scatter; each puff drifts upward + outward, grows, and fades over `mistPuffLife`. Tunables: `mistPuffRate` (38/s), `mistPuffStart`/`mistPuffEnd` (6→22 px), `mistPuffScatter` (22 px), `mistPuffAlpha` (0.55).

**Gate look.** `gateFont` (TMP_FontAsset, wired to `Assets/Fonts/Fantasia SDF.asset`), `gateLabelColor` (cream/gold with thin dark outline), `gateLabelFontSize`, `gateSize` (56). When a sprite is set the 45° diamond rotation is dropped — sprites are designed upright/round.

### 7-Day Progression & Multi-Ending Subsystem

Five tightly-coupled scripts + one static content file drive the day/letter/rent/ending flow:

- `GameManager.cs` — owns all progression state (`currentDay`, `peakReputation`, `wandsCrafted`, `lettersReceived`, `bankruptedOnDay3`) + the rent/ending constants. Provides `AdvanceToNextDay()`, `IsRentDueToday()`, `GetRentDueToday()`, `DetermineEnding()`, `ResetForNewPlaythrough()`. Editor-only `[ContextMenu]` shortcuts jump to specific days / rep tiers / empty wallets for test speedup.
- `LetterLibrary.cs` — pure static class (no MonoBehaviour, no `.asset` dependency). Holds 19 hand-authored letters: the day-1 intro from the player's aunt, plus Low/Mid/High variants for days 2–7, plus 2 landlord rent-reminders. `GetMorningLetter(day, tier)` and `GetRentReminder(day, amount)` are the two lookups. `LetterContent` is a struct (sender enum, from, subject, body).
- `TypewriterText.cs` — reusable char-by-char reveal via TMP's `maxVisibleCharacters` (so rich-text tags like `<b>`/`<i>` don't split mid-tag). Click-anywhere-to-skip. Used by both `MorningLetterUI` and `EndingManager`.
- `MorningLetterUI.cs` — scene controller for `MorningScene`. Reads `GameManager.currentDay` + reputation tier, fetches the letter, tints a wax seal by sender type (Aristocrat=gold, Royal=purple, Brigand=black, etc.), plays the typewriter. Rent-due mornings queue a second landlord letter after the main one.
- `DayProgressUI.cs` — calendar-dot header on the evaluation screen (7 `Image` dots + optional rent-coin icons above days 3/6). `Refresh()` colors past/today/future states from `GameManager.currentDay`.
- `RentPaymentUI.cs` — modal on evaluation scene. `Show(rentAmount, callback)`: if gold ≥ rent, enables Pay/Plead buttons (both → Paid, flavor-only); if gold < rent, enables "Accept fate" → Bankrupt. Sets `bankruptedOnDay3` so EndingManager can pick the right variant.
- `EndingManager.cs` — scene controller for `EndingScene`. Resolves `EndingType` (Royal/Rival/Slum/BankruptEarly/BankruptLate), loads `Resources/EndingArt/{name}.png` (graceful placeholder tint if missing), fades in via `CanvasGroup`, types out hand-written ending dialogue (~5 lines each, verbatim strings in the script), displays stats ("Days survived · Letters · Peak rep · Wands"), restart button calls `ResetForNewPlaythrough()` then loads `MorningScene`.

**Integration into EvaluationManager:** `Start()` calls `dayProgressUI.Refresh()`; `EvaluationPipeline` updates `peakReputation` + `wandsCrafted` after the reward apply; `OnNextCustomer()` routes through `RentPaymentUI.Show(...)` on rent days and `ProceedToNextDayOrEnding()` otherwise. Day 7 or bankruptcy loads `SCENE_ENDING`; otherwise `AdvanceToNextDay()` runs.

**Ending art:** Pre-generated PNGs in `Assets/Resources/EndingArt/` (Royal.png, Rival.png, Slum.png, Bankrupt.png). Generate once via the `ComfyUITest` scene and drop them in — not runtime-generated so the climactic moment is reliable.

### CraftingScene → MinigameScene handoff (parallel execution lives here now)

`CraftingManager.OnConfirm()` removes the chosen materials from inventory, stashes them on `GameManager.chosenCore1 / chosenCore2 / chosenWood`, and calls `LoadScene(SCENE_MINIGAME)`. CraftingScene no longer owns the wand-generation pipeline — that moved with the minigame.

`MinigameSceneRunner` (on the `Test Runner` GO in `5.MinigameTest.unity`) launches two concurrent operations on `Start`:
1. `WandPipeline` coroutine (OpenAI wand generation → ComfyUI image generation, identical helpers to the old CraftingManager)
2. `tracingMinigame.Begin(grade => …)` (3-round tracing minigame)

Both set completion flags (`_pipelineDone`, `_minigameDone`); `TryAdvance()` waits for both, then calls `LoadScene(SCENE_EVALUATION)`. `GameManager.craftingQualityGrade` and `lastMinigameRoundsWon` carry forward to the evaluation reveal.

If `tracingMinigame` is null (e.g. broken Inspector wiring), the minigame is skipped and `_minigameDone` is set immediately with grade `'A'`. The legacy `MinigameTestRunner.cs` test harness was deleted — `MinigameSceneRunner` replaced it.

### Data Classes

- `CustomerOrder` — customerName, schoolOfMagic, profession, personality, request, trueGoal, constraint
- `MaterialData` — dual-type (core vs wood); cores have elementalAffinity/special, woods have personalityMatch; `generatedImage` (Texture2D, `[NonSerialized]`) carries the ComfyUI texture across scene transitions
- `WandResult` — wandName, description, attributes[], imagePrompt, wandImage (Texture2D)

## Scripts Location

All C# scripts are in `Assets/Scripts/`. Flat structure, no subdirectories. Notable scripts added during the UI Toolkit / scene-split push: `MorningScreenController`, `CraftingWorkbenchUI`, `MinigameSceneRunner`, `EvaluationResultController`. The legacy `MinigameTestRunner.cs` test harness was deleted — `MinigameSceneRunner` is the production replacement.

## Build & Run

This is a standard Unity 6 project. Open with Unity Hub, select Unity 6000.0.58f2. No custom build scripts. Scenes must be added to Build Settings in the order listed above.

**Runtime requirements:** OpenAI API key in `StreamingAssets/config.json` and ComfyUI running on port 8000.

## File Safety

A PostToolUse hook in `.claude/settings.json` **blocks all Write/Edit operations** on `.unity`, `.prefab`, `.asset`, and `.meta` files. These are binary/serialized YAML managed by the Unity Editor — never create or modify them from code.

When a code change requires Inspector wiring or scene hierarchy changes, add a `TODO-EDITOR:` comment with exact instructions (see `.claude/rules/file-safety.md` for format).

### Hooks

Configured in `.claude/settings.json`:
- **PostToolUse** — blocks Write/Edit on `.unity`, `.prefab`, `.asset`, `.meta` files
- **Stop** — plays a two-tone beep when Claude finishes (PowerShell `[Console]::Beep`)
- **Notification** — plays a beep on notifications

## C# Conventions

- `PascalCase` public fields/methods/classes; `camelCase` locals/params; `_camelCase` private fields; `UPPER_SNAKE_CASE` constants
- `[DisallowMultipleComponent]` on all MonoBehaviour managers and UI controllers
- Coroutines (`IEnumerator` + `StartCoroutine`) for all async operations — no async/await
- `Newtonsoft.Json.Linq` (`JObject`, `JArray`) for JSON — not `JsonUtility`
- TextMeshPro (`TMP_Text`, `TextMeshProUGUI`) for all text — no legacy `Text`
- Scene navigation via `GameManager.Instance.LoadScene(GameManager.SCENE_*)` string constants
- If adding events, use `System.Action<T>` (not UnityEvent)

Full details in `.claude/rules/unity-csharp.md`.

## Packages

Key non-default packages: `com.unity.nuget.newtonsoft-json` (JSON parsing), `com.unity.render-pipelines.universal` (URP), `com.unity.inputsystem`.

## AI Team Pipeline

2 specialist programmers + 1 technical artist work in **parallel via worktree-isolated sub-agents**:
- **systems-programmer** — GameManager, scene managers (CustomerGenerator, MaterialGenerator, CraftingManager, EvaluationManager), data classes, API integrations, ComfyUITest
- **ui-programmer** — MaterialCardUI, MaterialTooltip, TooltipTrigger, new UI components
- **technical-artist** — VFX, shaders, particles, materials (new files only)

Workflow: Leader -> Designer (design doc) -> Programmers + Technical Artist (parallel worktrees) -> Review.
See `.claude/agents/` for full agent definitions and file ownership maps, and `.claude/skills/` for invokable workflow steps (`design-doc`, `implement`, `implement-vfx`, `review-code`, `eod`).

## GDD vs Current Implementation

The GDD (`Docs/GDD.md`) describes the full design vision. **Not yet implemented:** 2 customers per day (currently 1), commission system (1.75x price orders), reputation tier effects on customer generation. The tracing minigame (3-round path-tracing with red/blue fill race, 5 keyboard-key gates per round at conductor-ictus points, quality grading A/B/C/F based on round wins, reward multiplier) is fully coded and now lives in its own production scene `5.MinigameTest.unity` between Crafting and Evaluation. Sprite Inspector fields on `TracingMinigameUI` (trail / gate / cursor / endpoint / mistPuff) are wired to PNGs in `Assets/Texture/MinigameSprites/` for the Hogwarts-style polished look.

The **7-day progression subsystem** (morning letters, rent days, multi-ending) is fully coded and scene-wired via Unity MCP (2026-04-24). `TitleScene` (Build index 0), `MorningScene` (5), and `EndingScene` (6) all built with placeholder tints; `EvaluationScene` has `DayProgressHeader` (7 dots + 💰 icons above day 3/6) and `RentPaymentPanel` (inactive by default) attached and wired. Remaining manual work:
- Drop 4 PNGs into `Assets/Resources/EndingArt/` (Royal / Rival / Slum / Bankrupt) — the Ending scene loads these via `Resources.Load`; if missing, falls back to a tinted placeholder (warns in console). Generate via ComfyUITest once, drop in.
- Replace placeholder art for owl (`OwlImage`), wax seal (`WaxSeal`), background parchment, etc. as desired — all are tinted Images right now.
- See `Docs/SevenDayProgression.md` for full plan + verification steps.

The **memo-gated dossier reading subsystem** (2026-04-24) is fully coded but needs scene wiring. In `CustomerGeneratorTest` the player must distil the 7 dossier fields into a 3-entry memo (Purpose / Personality / Element) by clicking content words in the dossier prose and assigning them to memo slots; Proceed is gated on memo completion. The memo then replaces the full dossier as the on-screen reference in `MaterialGeneratorTest` and `CraftingScene`, and drives ✦ match-hint glyphs on materials whose affinity overlaps the memo. Scripts: `PlayerMemo.cs`, `MemoFillUI.cs`, `MemoCardUI.cs` (+ hooks in `CustomerGenerator`, `MaterialGenerator`, `CraftingManager`, `MaterialCardUI`, `GameManager`). See `Docs/MemoFeature.md` for the TODO-EDITOR wiring list.

### UI Toolkit Migration (in progress, 2026-04-25)

The project is migrating from uGUI (Canvas + RectTransform + Image/TMP) to **UI Toolkit** (UIDocument + UXML + USS). Per-scene wiring uses one `*UIDocument` GameObject per scene with a UXML source and a controller MonoBehaviour. Legacy uGUI Canvas children stay in scene hierarchies but are disabled (not deleted) so they can be reverted if needed.

**Folder layout:** `Assets/UI/<feature>/<feature>Panel.uxml`, `<feature>Panel.uss`, `<feature>PanelSettings.asset`. Folders so far: `Title/`, `Dossier/`, `Market/`, `Minigame/`, `Morning/`, `Crafting/`, `Evaluation/`.

**Scene status (2026-04-29):**
- ✅ `TitleScene` — `TitleUIDocument` + `TitleScreenController.cs`. Parchment menu with Start + Quit. Old `TitleScreenUI` Canvas children disabled.
- ✅ `CustomerGeneratorTest` — `DossierUIDocument` + `DossierPanelController.cs`. Memo gameplay (click word → click slot).
- ✅ `MorningScene` — `MorningUIDocument` + `MorningScreenController.cs`. Background `Assets/Texture/Morning Scene.png`. HUD: parchment day plaque + gold purse + centered letter panel with wax seal & ScrollView body. PanelSettings: ScaleWithScreenSize 1920×1080.
- ✅ `EvaluationScene` — `EvaluationUIDocument` + `EvaluationResultController.cs`. Theatrical reveal (banner → wand → scoreboard → grade letter), `WandSparkles` ParticleSystem + `PostFX Volume` (URP Bloom). Legacy uGUI panels disabled.
- ✅ `CraftingScene` workbench — `CraftingWorkbenchUIDocument` + `CraftingWorkbenchUI.cs`. Result modal removed (handoff to MinigameScene).
- ✅ `MinigameTest` chrome — `MinigameUIDocument` + `MinigamePanelController.cs`. Wood-frame UXML/USS rebuilt; PanelSettings `sortingOrder = -10` so Canvas-hosted procedural gameplay renders on top.
- 🟡 `MaterialGeneratorTest` — UXML/USS/PanelSettings authored, no `*UIDocument` GO yet.
- ❌ `EndingScene` — still uGUI only.

**Pattern** (used in both wired scenes):
1. `Assets/UI/<feature>/<feature>PanelSettings.asset` — created via `execute_code` with `PanelSettings` + `AssetDatabase.CreateAsset`.
2. Scene-root GameObject `<Feature>UIDocument` with `UIDocument` (panelSettings + sourceAsset) + `<Feature>...Controller`.
3. Controller does `document.rootVisualElement.Q<>()` lookups in a `Bootstrap()` called from `OnEnable`. Click handlers via `button.clicked += …`.
4. Scene's old uGUI children → `SetActive(false)` (kept, not deleted, per migration plan).

**UI Toolkit gotcha:** when wiring a UIDocument's `panelSettings`/`sourceAsset` via MCP `set_property`, the visual tree doesn't auto-refresh in the editor — toggle `doc.enabled = false; doc.enabled = true;` to force a rebuild before `Q<>()` lookups will succeed at edit time. At play time `OnEnable` does this automatically.

### Reward Formula (in EvaluationManager)

```
baseGold   = 150 * (matchScore / 100)
baseRep    = matchScore >= 40 ? 20 * (matchScore / 100) : -10
final      = base * qualityMultiplier   // A=1.0, B=0.85, C=0.7, F=0.4
```

## References

- `README.md` — Mermaid architecture diagram + per-round AI call table
- `Docs/GDD.md` — Full game mechanics and design vision
- `Docs/MemoFeature.md` — Active-reading gate: click words from dossier prose to fill a 3-entry memo (Purpose/Personality/Element). Memo replaces the full dossier in market + crafting scenes and drives material match hints.
- `Docs/DossierSortingFeature.md` — Superseded by MemoFeature; kept for reference.
- `.claude/rules/unity-csharp.md` — C# naming, async, JSON, UI conventions
- `.claude/rules/file-safety.md` — What files to never touch, TODO-EDITOR format
