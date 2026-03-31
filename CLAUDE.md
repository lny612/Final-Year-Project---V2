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
3. **CraftingScene** — Player assigns materials to slots, GPT-4o generates wand description, ComfyUI generates wand image
4. **EvaluationScene** — GPT-4o scores the wand match (0–100), awards gold + reputation, then loops back

### Key Patterns

- **GameManager** is a `DontDestroyOnLoad` singleton holding all cross-scene state (customer, materials, inventory, gold, reputation, wand result)
- Each scene has its own manager script (CustomerGenerator, MaterialGenerator, CraftingManager, EvaluationManager) that owns its UI references and API call coroutines
- All OpenAI calls use `UnityWebRequest` POST to chat completions, expecting JSON-only responses parsed with `Newtonsoft.Json.Linq`
- ComfyUI integration: POST workflow JSON to `/prompt` → poll `/history/{id}` every 1.5s (60s timeout) → GET `/view` to download image as Texture2D

### External Dependencies

- **OpenAI API** — API key loaded at runtime from `Assets/StreamingAssets/config.json` (`{"openAIApiKey": "..."}`)
- **ComfyUI** — must be running locally at `http://127.0.0.1:8000`; uses `z_image_turbo` model with `qwen_3_4b` CLIP encoder
- **ComfyUI workflow** — `Assets/StreamingAssets/image_z_image_turbo.json`; prompt injected at node `"57:27"`, seed randomized at node `"57:3"`

### Data Classes

- `CustomerOrder` — customerName, schoolOfMagic, profession, personality, request, trueGoal, constraint
- `MaterialData` — dual-type (core vs wood); cores have elementalAffinity/special, woods have personalityMatch
- `WandResult` — wandName, description, attributes[], imagePrompt, wandImage (Texture2D)

## Scripts Location

All C# scripts are in `Assets/Scripts/`. There are no subdirectories — flat structure with ~12 files.

## Build & Run

This is a standard Unity 6 project. Open with Unity Hub, select Unity 6000.0.58f2. No custom build scripts. Scenes must be added to Build Settings in the order listed above.

**Runtime requirements:** OpenAI API key in `StreamingAssets/config.json` and ComfyUI running on port 8000.

## Packages

Key non-default packages: `com.unity.nuget.newtonsoft-json` (JSON parsing), `com.unity.render-pipelines.universal` (URP), `com.unity.inputsystem`.

## AI Team Pipeline

2 specialist programmers + 1 technical artist work in **parallel via worktree-isolated sub-agents**:
- **systems-programmer** — GameManager, scene managers (CustomerGenerator, MaterialGenerator, CraftingManager, EvaluationManager), data classes, API integrations, ComfyUITest
- **ui-programmer** — MaterialCardUI, MaterialTooltip, TooltipTrigger, new UI components
- **technical-artist** — VFX, shaders, particles, materials (new files only)

Workflow: Leader -> Designer (design doc) -> Programmers + Technical Artist (parallel worktrees) -> Review.
See `.claude/agents/` for full agent definitions and file ownership maps.
See `.claude/skills/` for design-doc, implement, implement-vfx, and review-code workflows.

## References

- `Docs/GDD.md` — Full game mechanics and design vision
- `.claude/rules/` — C# conventions and file safety rules
