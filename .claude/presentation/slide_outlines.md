# Slide Outlines — *The Wand Atelier*

**Total: 14 slides for a 15-minute presentation.**
**Tool-agnostic: build in PowerPoint, Google Slides, Keynote, or Reveal.js.**

> **Visual style direction:**
> - **Theme:** parchment + ink. Cream/aged-paper background (`#F4ECD8`), dark sepia text (`#3B2F1E`), gold accent (`#C9A24A`), magenta/cyan highlights only on technical diagrams.
> - **Typography:** serif headers (Cinzel, Cormorant, EB Garamond), sans body (Inter, Source Sans).
> - **Asset folder to source from:** `Assets/Texture/`, `Assets/Texture/MinigameSprites/`, `Assets/Resources/EndingArt/` for screenshots.
> - **Capture screenshots at 1920×1080** so they don't pixelate when projected.

---

## Slide 1 — Title

**Layout:** full-bleed hero image with title overlay.

**Content:**
- **Hero image:** screenshot of either (a) the parchment morning letter scene, or (b) the tracing minigame mid-trace with the rune partly filled. Apply a subtle dark gradient at the bottom so title text stays readable.
- **Title (centered, large serif):** **The Wand Atelier**
- **Subtitle (smaller, italic):** An AI-Driven Cozy Crafting Game
- **Bottom-left block:** Author name · Supervisor name · Course code · Date.
- **Bottom-right (small):** university logo if required.

**Build instruction:** keep this slide for ~30 seconds. No animation needed.

---

## Slide 2 — Abstract

**Layout:** three-column icon row at top, four-bullet abstract beneath.

**Content:**
- **Header:** ABSTRACT (small caps, gold)
- **Three icon columns (left → right):**
  - 🪶 **Quill** — caption: "Deduction"
  - 🪄 **Wand** — caption: "Crafting"
  - 🤖 **AI Chip** — caption: "Generation"
- **Four bullets beneath:**
  1. *A 7-day fantasy crafting game in Unity 6 where players read AI-generated customer dossiers and craft wands to fit their hidden needs.*
  2. *OpenAI GPT-4o generates every customer, material, and judge verdict — no two playthroughs share content.*
  3. *Local ComfyUI server (Z-Image Turbo, INT4) generates pixel art at runtime on a laptop GPU.*
  4. *~8,700 lines of C# · 34 scripts · 8 scenes · 4 branching endings.*

**Build instruction:** icons can be free Lucide / Phosphor / Heroicons SVGs.

---

## Slide 3 — Introduction: The Problem

**Layout:** split-screen comparison.

**Content:**
- **Header:** THE PROBLEM
- **Left panel (50%):** title "Traditional crafting games"
  - Sub-bullets: "Recipe lookup tables", "Memorise → execute", "NPCs repeat lines verbatim"
  - Visual: cropped screenshot of a Stardew/Don't Starve/Minecraft crafting grid (cite source under image).
- **Right panel (50%):** title "What if logic — not lookup — decided the outcome?" (italic, gold)
  - Visual: a generated customer dossier prose-card from the actual game.
- **Footer line (centered):** *"The mystery dies the moment you crack the formula."*

**Build instruction:** use a vertical line divider for clarity.

---

## Slide 4 — Objectives & Requirements

**Layout:** three-pillar layout (three vertical columns).

**Content:**
- **Header:** PROJECT OBJECTIVES
- **Pillar 1 — Deduction**
  - Bullet: Reading and inference must matter more than memorisation.
  - Bullet: Customers hide their real need behind a surface request.
- **Pillar 2 — Creative Flexibility**
  - Bullet: No fixed recipes — any combo is valid if reasoning is sound.
  - Bullet: AI evaluator judges intent, not key-value match.
- **Pillar 3 — Ritual Crafting**
  - Bullet: Crafting feels embodied, not a button click.
  - Bullet: Skill check with timing, attention, commitment.
- **Bottom strip — Functional Requirements:**
  - 7-day game arc · 4 endings · 2 AI services concurrent · 60 FPS on laptop GPU.

**Build instruction:** use a faint icon at the top of each pillar (eye / spiral / hand).

---

## Slide 5 — Background & Literature Review

**Layout:** four boxes in a 2×2 grid pointing toward a centre tag.

**Content:**
- **Header:** INFLUENCES & PRIOR ART
- **Centre tag (gold):** THE WAND ATELIER
- **Top-left:** *Hogwarts Legacy* (WB Games, 2023) — caption: "Gestural spell-casting → tracing minigame"
- **Top-right:** *Papers, Please* (Pope, 2013) — caption: "Deduction under time pressure → memo gate"
- **Bottom-left:** *Disco Elysium* (ZA/UM, 2019) — caption: "Narrative density per NPC → 7-field schema"
- **Bottom-right:** *AI Dungeon / Inworld* — caption: "LLM-driven NPCs → GPT-4o customer/material gen"
- **Bottom callout (italic, gold):** *"Existing LLM-NPC games rarely judge the player's response with the same model. This project closes that loop."*

**Build instruction:** small game cover thumbnails for each box; cite sources.

---

## Slide 6 — The Gap This Project Fills

**Layout:** two-column comparison table.

**Content:**
- **Header:** THE GAP THIS PROJECT FILLS
- **Table (4 rows, 2 columns):**

| Existing approach | This project |
|---|---|
| Recipe lookup | Logic scoring by AI |
| Hand-written NPCs | LLM-generated dossiers (logical chain) |
| Pre-rendered art | Runtime pixel art (local GPU) |
| One-true-answer puzzle | Soft-gate memo (interpretation matters) |

- **Bottom callout block** — three reasons why desirable:
  1. Replayability without scaling content cost.
  2. AI is both narrator AND judge — closed feedback loop.
  3. Viable production pattern for indie devs on cheap GPUs.

**Build instruction:** use alternating row tints (cream / lighter cream).

---

## Slide 7 — Methodology Overview: Architecture

**Layout:** centered architecture diagram, scene-flow strip beneath.

**Content:**
- **Header:** SYSTEM ARCHITECTURE
- **Diagram (center):**
  - Centre node: **Unity Client** (Unity 6 + URP)
  - Right node: **OpenAI GPT-4o** — arrow labels: *"customer dossier (JSON)"*, *"wand verdict (JSON)"*
  - Left node: **ComfyUI (local)** — arrow labels: *"workflow JSON"*, *"PNG bytes"*
  - Bottom-centre note: **Z-Image Turbo · INT4 · 8 GB VRAM**
- **Scene-flow strip beneath (8 boxes left-to-right):**
  Title → Morning → Customer → Material → Crafting → **Minigame** → Evaluation → Ending
- **Stats footer:** ~8,700 LoC · 34 scripts · 6 GPT-4o calls/day · 7 ComfyUI calls/day

**Build instruction:** make this a clean Mermaid render or hand-drawn in Excalidraw.

---

## Slide 8 — Methodology: The Customer Deduction Loop

**Layout:** screenshot left, memo card right, schema chain below.

**Content:**
- **Header:** THE CUSTOMER DEDUCTION LOOP
- **Left (50%):** screenshot of the dossier panel from `CustomerGeneratorTest`. Highlight three words in different colors (red, blue, green) showing what gets clicked.
- **Right (50%):** screenshot of the memo card with three slots filled (Purpose / Personality / Element).
- **Arrow** between them visualising the click-and-drag.
- **Schema chain at bottom (text strip):**
  `schoolOfMagic → profession → request → trueGoal → constraint`
- **Pull-quote (gold, italic):**
  > *"The constraint must be cruelest when it activates exactly when they need control most."* — GDD §3.2

**Build instruction:** add red circle annotations on the three clicked words in the dossier screenshot.

---

## Slide 9 — Methodology: Tracing Minigame

**Layout:** three-screenshot horizontal strip on top, algorithm sketch on bottom.

**Content:**
- **Header:** THE CRAFTING RITUAL
- **Three screenshots horizontally (R1 / R2 / R3):** capture mid-round visuals showing different gate compositions and the red-mist progression.
- **Gate-type icon strip (3 boxes):**
  - 🟨 **TAP** — "Press key once"
  - 🟦 **HOLD** — "Press & hold 0.9s"
  - 🟪 **ACCENT** — "Press → flick mouse → return"
- **Algorithm sketch panel** (right or bottom):
  - Catmull-Rom interpolation curve (sketch)
  - "Arc-length overlap validator: counts sample-pair re-crossings"
  - Round escalation: R1 = 5T · R2 = 4T+1H · R3 = 3T+1H+1A
- **Reward ladder strip:** A=1.0× · B=0.85× · C=0.7× · F=0.4×

**Build instruction:** if you can record a short GIF of one round, embed it on this slide instead of the static strip.

---

## Slide 10 — Methodology: GPT-4o as Judge

**Layout:** prompt screenshot left, JSON output right, rubric panel beneath.

**Content:**
- **Header:** GPT-4o AS JUDGE
- **Left (45%):** screenshot of the system prompt block from `EvaluationManager.cs`, with the scoring rubric highlighted.
- **Right (45%):** sample JSON response:
  ```json
  {
    "matchScore": 73,
    "verdict": "A thoughtful choice",
    "whatWorked": "The leviathan-scale core caps her overflow",
    "whatMissed": "Doesn't address the emotional distress trigger",
    "customerReaction": "This… might actually work."
  }
  ```
- **Rubric panel (full width, below):**
  - Addresses request only → **40–60**
  - Addresses true goal → **60–75**
  - True goal + accounts for constraint → **75–95**
  - Resolves tension creatively → **90–100**
- **Reward formula footer:** `gold = 150 × (score / 100) × qualityMultiplier`

**Build instruction:** keep code/JSON font monospaced. Add a small magenta highlight on "score < 40 → -10 reputation."

---

## Slide 11 — Results: Demo Walkthrough

**Layout:** full-bleed video frame OR live demo placeholder.

**Content:**
- **Header:** LIVE DEMO
- **Body:** either embed a 90-second screen capture, or leave a single placeholder frame with the words **"DEMO"** in large gold serif text.
- **Bottom strip — demo path checklist (small text, for your reference only):**
  Morning letter → Dossier → Memo (Purpose/Personality/Element) → Market → Crafting (2 cores + 1 wood) → Tracing (3 rounds) → Evaluation reveal.

**Build instruction:**
- If using video: embed at 720p, no audio (you'll narrate live), autoplay on slide entry.
- If live demo: keep the Unity build window pre-loaded on day 3 with a fresh customer ready, and have the screen capture as a backup.

---

## Slide 12 — Results: Evaluation & Critique

**Layout:** 2×2 grid.

**Content:**
- **Header:** RESULTS & CRITIQUE
- **Top-left — Quantitative:**
  - ~8,700 LoC · 60 FPS @ 1080p
  - GPT-4o latency: 4–7s avg
  - ComfyUI image latency: 8–12s avg
  - **Parallel execution hides ~70% of generation time**
- **Top-right — Qualitative (playtester quotes):**
  - *"Feels like reading a real letter."* — Memo loop
  - *"The mist never letting up was stressful in a good way."* — Hold gates
- **Bottom-left — Limitations:**
  - API cost ~$0.04/round
  - GPT verdicts ±10 points across runs (non-determinism)
  - 7-day arc tight for relationship-building
- **Bottom-right — Validation:**
  - Small A/B: structured-prompt customers → **3× more constraint-aware wand submissions**
  - Sample size small; signal directional only

**Build instruction:** use distinct subtle background tints per quadrant (green/blue/red/gold).

---

## Slide 13 — Conclusion & Future Work

**Layout:** three columns.

**Content:**
- **Header:** CONCLUSION & FUTURE WORK
- **Column 1 — Done (✓):**
  - 7-day arc + 4 endings
  - Memo deduction gate
  - 3-gate minigame
  - Dual-AI pipeline (GPT-4o + ComfyUI)
  - Parallel-execution latency masking
  - UI Toolkit migration (6/8 scenes wired)
- **Column 2 — Next 6 months:**
  - 2 customers/day
  - Commission system (1.75× orders)
  - Reputation-tier customer biasing
  - Deterministic scoring rubric
  - TTS voice-acted letters
- **Column 3 — Long term:**
  - Procedural rune shapes
  - Multi-day customer threads
  - Modder API
- **Footer (gold, centered):** *"Generative AI as both author and judge — closing the narrative loop."*

**Build instruction:** use ✓ ✦ ❖ icons at the column heads for visual rhythm.

---

## Slide 14 — Appendices, References, Q&A Invitation

**Layout:** three small boxes in a row, big "Thank you" at the bottom.

**Content:**
- **Header:** APPENDICES
- **Box 1 — Software Stack:**
  - Unity 6 (6000.0.58f2)
  - Universal Render Pipeline (URP)
  - OpenAI GPT-4o
  - ComfyUI + Nunchaku INT4 Z-Image Turbo
  - Newtonsoft.Json, UI Toolkit, Input System
- **Box 2 — Code:**
  - 34 scripts in `Assets/Scripts/`
  - ~8,700 LoC
  - GitHub repo: [link or QR code]
- **Box 3 — References:**
  - Hogwarts Legacy (Avalanche / WB Games, 2023)
  - Papers, Please (Pope, 2013)
  - Disco Elysium (ZA/UM, 2019)
  - Catmull & Rom, *A Class of Local Interpolating Splines*, 1974
  - Nunchaku quantization paper (cite if used)
  - OpenAI GPT-4o model card (2024)
- **Bottom strip (large, centered, gold serif):**
  **Thank you.**
  *Questions?*

**Build instruction:** print the QR code linking to the GitHub repo at moderate size — this is what the audience will photograph.

---

## Optional: Backup slides (do not show unless asked)

Keep these prepared in case Q&A drills into a topic:

- **Backup A:** the actual GPT-4o customer-generation system prompt (full text, screenshot of source).
- **Backup B:** the reward formula derivation with worked examples (score 80, grade B → 102 gold + 14 rep).
- **Backup C:** the path-overlap validator pseudocode + a "double-circle failure" annotated diagram.
- **Backup D:** scene-flow Gantt showing parallel execution timeline (minigame vs wand-gen).
- **Backup E:** non-determinism mitigation table — temperature, system-prompt anchoring, rubric explicitness.

---

## Pre-presentation checklist

- [ ] Screenshots at 1920×1080, no UI debug overlays visible.
- [ ] All slide numbers visible in footer.
- [ ] Tested on the venue projector (colors, contrast, font legibility from back row).
- [ ] Demo build pre-loaded on day 3 with a fresh customer.
- [ ] Backup screen capture ready if the live demo fails.
- [ ] Speaker timer visible from podium.
- [ ] Backup slides hidden but accessible by slide-number jump.
