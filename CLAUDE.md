# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Fantasy wand-crafting game built in **Unity 6** (6000.0.58f2) using **URP**. Players receive AI-generated customer orders, buy materials, craft wands, and get scored — all driven by OpenAI GPT-4o for text and a local ComfyUI server for image generation.

## Architecture

### Game Loop (4 scenes, cycled via GameManager)

```
CustomerGeneratorTest → MaterialGeneratorTest → CraftingScene → EvaluationScene → (loop)
```

1. **CustomerGeneratorTest** — GPT-4o generates a fantasy customer with request/trueGoal/constraint
2. **MaterialGeneratorTest** — GPT-4o generates 6 materials (3 cores + 3 woods), ComfyUI generates pixel art images; player buys ≥1 core + ≥1 wood
3. **CraftingScene** — Player assigns materials to 3 slots (2 core + 1 wood), GPT-4o generates wand description, ComfyUI generates wand image; tracing minigame runs concurrently with API calls
4. **EvaluationScene** — GPT-4o scores the wand match (0–100), awards gold + reputation, then loops back

Additional scenes: `ComfyUITest` (standalone image generation test), `MinigameTest` (standalone tracing minigame test via `MinigameTestRunner`), `SampleScene` (unused).

### Round Reset (`GameManager.StartNextRound`)

Each round clears `currentCustomer`, `availableMaterials`, `currentWandResult`, and `craftingQualityGrade` (reset to `'A'`). **Persisted across rounds:** `inventory`, `playerGold` (starts at 500), `playerReputation`.

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

- `TracingMinigameUI` — orchestrator: runs 3 rounds, builds path/fill-segment/gate visuals procedurally as UI GameObjects, updates red/blue trail fill each frame, tracks successes, computes final grade (3 wins→A, 2→B, 1→C, 0→F)
- `TracingCursor` — player input: follows mouse along path with forward-only movement, blocked at gate markers until player presses the displayed keyboard key (cannot draw past an uncleared gate)
- `TracingMist` — timer: advances MistT at constant speed; when MistT reaches 1.0 the round is lost. No visual dot — the red fill is rendered by TracingMinigameUI's fill segments
- `RunePathData` — static data: normalized control points for 6 rune shapes, Catmull-Rom interpolation, cumulative distance math

**Visual mechanic:** The trail fills red from the start (time-based, via TracingMist speed) and blue from the start (player-traced, via TracingCursor progress). Blue overrides red where the player has traced. Gate markers show randomized keyboard keys (from pool Q/W/E/R/T/A/S/D/F). Each round is a single attempt — no retries. Rune shapes are based on orchestral conductor beat patterns (Maestoso 4/4, Valse 3/4, Compound 6/8, Take Five 5/4, Quick March 2/4, plus a freeform Fermata Crescendo); each round has 5 gates placed at the baton's ictus points.

All visuals are procedural UI (`Image` + `TextMeshProUGUI` components on dynamically created `GameObject`s) — no prefabs, no scene references beyond the minigame panel. `MinigameTestRunner` is a lightweight harness that auto-starts the minigame on scene load for isolated testing.

### Parallel Execution in CraftingScene

`CraftingManager.OnConfirm()` launches two concurrent operations:
1. `CraftingPipeline` coroutine (OpenAI wand generation → ComfyUI image generation)
2. `TracingMinigameUI.Begin()` (3-round tracing minigame)

Both set completion flags (`_pipelineDone`, `_minigameDone`); `TryShowResult()` waits for both before revealing the result panel. This means the player plays the minigame while the API calls run in the background.

If the minigame reference is null (not yet wired in Editor), the minigame is skipped and `_minigameDone` is set immediately with grade `'A'`.

### Data Classes

- `CustomerOrder` — customerName, schoolOfMagic, profession, personality, request, trueGoal, constraint
- `MaterialData` — dual-type (core vs wood); cores have elementalAffinity/special, woods have personalityMatch; `generatedImage` (Texture2D, `[NonSerialized]`) carries the ComfyUI texture across scene transitions
- `WandResult` — wandName, description, attributes[], imagePrompt, wandImage (Texture2D)

## Scripts Location

All C# scripts are in `Assets/Scripts/`. Flat structure, no subdirectories.

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

The GDD (`Docs/GDD.md`) describes the full design vision. **Not yet implemented:** 2 customers per day (currently 1), commission system (1.75x price orders), reputation tier effects on customer generation. The tracing minigame (3-round path-tracing with red/blue fill race, 5 keyboard-key gates per round at conductor-ictus points, quality grading A/B/C/F based on round wins, reward multiplier) is fully coded but **needs manual Editor setup** — see `TODO-EDITOR` comment at `CraftingManager.cs:59` for MinigamePanel wiring instructions.

### Reward Formula (in EvaluationManager)

```
baseGold   = 150 * (matchScore / 100)
baseRep    = matchScore >= 40 ? 20 * (matchScore / 100) : -10
final      = base * qualityMultiplier   // A=1.0, B=0.85, C=0.7, F=0.4
```

## References

- `README.md` — Mermaid architecture diagram + per-round AI call table
- `Docs/GDD.md` — Full game mechanics and design vision
- `Docs/DossierSortingFeature.md` — Designed (not yet implemented): active "fill-the-card" reading step for the customer dossier + pinned reference in CraftingScene
- `.claude/rules/unity-csharp.md` — C# naming, async, JSON, UI conventions
- `.claude/rules/file-safety.md` — What files to never touch, TODO-EDITOR format
