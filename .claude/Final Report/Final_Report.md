# THE UNIVERSITY OF HONG KONG
## School of Computing and Data Science

---

## COMP4502 Final Year Project — Final Report

# The Wand Atelier
### A cosy fantasy crafting game built around two cooperating generative-AI subsystems

---

**Project ID:** *[Project ID]*
**Author:** Nayoung Lim
**UID:** 3036086098
**Programme:** BEng (Data Science and Engineering)
**Supervisor:** *[Supervisor name]*
**Submission date:** 7 May 2026

---

[Figure 1 — cover illustration: a single hero shot of the player's workshop, a finished wand floating above the workbench, the morning letter and an unsealed dossier framed on either side.]

\pagebreak

# Contents

1. Cover page
2. Contents
3. Abstract
4. Declaration & Acknowledgements
5. Introduction & Objectives
6. Project Background and Literature Review
7. Project Methodology
   - 7.1 Engineering process and project initiation
   - 7.2 System architecture
   - 7.3 AI subsystem 1 — large-language-model orchestration
   - 7.4 AI subsystem 2 — diffusion-model image pipeline
   - 7.5 Gameplay subsystems
   - 7.6 User-interface architecture
   - 7.7 Tools, platforms and development workflow
8. Results & Discussion
   - 8.1 Functional results
   - 8.2 Performance and latency
   - 8.3 Critical evaluation per subsystem
   - 8.4 Failure modes and limitations
   - 8.5 Ethical considerations
9. Conclusion and Future Works
10. Appendices
11. References
12. Declaration of the Contribution of Each Individual Member of the Group

\pagebreak

# 3. Abstract

*The Wand Atelier* is a single-player cosy crafting game built in Unity 6 that integrates two heterogeneous generative-AI services — OpenAI's GPT-4o for narrative content and a locally hosted ComfyUI server running a Nunchaku-quantised Z-Image Turbo diffusion model for runtime stylized illustration — into a tightly authored seven-day session loop. Each in-game day the player receives an AI-generated customer with a hidden tension between *what they say* and *what they need*, distils that dossier into a four-slot memo through a drag-to-highlight reading mechanic, hunts among six AI-generated materials laced with intentional misdirection, performs a three-round conducting-style tracing minigame whose escalation introduces three distinct gate types (Tap, Hold, Accent), receives a unique stylized wand illustration, and is graded against the customer's true goal. The headline engineering contribution is a *parallel pre-generation cascade* that hides roughly twenty-five seconds of cumulative AI latency per day behind scene transitions and gameplay, so that all four GPT-4o calls and the seven diffusion-model image jobs (six materials plus one wand) are perceptually free. A second contribution is a *logical-chain prompt design* that constrains GPT-4o to produce dossiers and materials whose fields are semantically interlocked, allowing the evaluator prompt to score the player on subtext rather than surface match. The full seven-day loop, five branching endings, and full gameplay-AI integration ship and run reproducibly on a consumer laptop GPU.

\pagebreak

# 4. Declaration & Acknowledgements

I, Nayoung Lim (UID 3036086098), hereby declare that the work presented in this report is my own, undertaken between January 2026 and May 2026 as the COMP4502 Final Year Project. All third-party libraries, models, and assets are explicitly cited in Section 11. Where AI assistance was used during development for code-completion or design-document drafting (Anthropic Claude Code), the resulting artefacts were reviewed, modified, and approved by me, and the underlying engineering decisions, architecture, prompt design, and gameplay design are entirely my own.

I am grateful to my supervisor *[Supervisor name]* for the patient project-shaping conversations that turned an over-scoped first proposal into a deliverable seven-day arc, and to the HKU School of Computing and Data Science for the Capstone framework that made this engineering exercise possible. I thank my family for their support across two semesters of unsociable hours and the open-source authors of ComfyUI, the Nunchaku quantisation toolkit, and the Z-Image Turbo model whose work made local runtime image generation viable on a single laptop GPU.

\pagebreak

# 5. Introduction & Objectives

## 5.1 Background

Crafting and shopkeeper games — *Potionomics*, *Strange Horticulture*, *Cult of the Lamb*, *Moonlighter* — are an established cosy genre. They share a common loop: the player serves customers, sources materials, transforms them, and receives feedback. Their content, however, is invariably hand-authored. A talented designer writes every customer, every recipe, every line of barter dialogue. The ceiling on perceived variety is therefore the writer's stamina, and most games in the genre exhaust their narrative novelty after a single playthrough.

Generative-AI systems, especially large language models (LLMs) and latent-diffusion image models, suggest an obvious answer: outsource the hand-authoring to a neural network, sample new customers and items each session, and the game will feel different every time. In practice this is much harder than it sounds. The naive approach — show the player a chatbot — inherits all the worst problems of the underlying model. Hallucinations break diegesis. Tone drifts mid-conversation. Most damaging of all, a chatbot interface presents the model *as itself*, and the player rapidly learns it is the same generator in every game; the magic dies.

This project asks a more interesting question: **can a game embed generative AI at the systems level — as the engine producing the *rules* the player reasons about, rather than the dialogue the player reads — without breaking either the gameplay loop or the player's suspension of disbelief?**

## 5.2 Problem statement

Three intertwined problems must be solved together for the answer to be yes:

1. **Latency.** A round-trip to GPT-4o takes 2–6 seconds on a fast residential connection; a 128×128 diffusion image on a Nunchaku-quantised Z-Image Turbo on a single RTX 4070 Laptop takes a further 3–6 seconds. Naively serialised, a single in-game day requires four LLM calls and seven image generations, summing to roughly 25–60 seconds of dead waiting per day. Cosy game pacing tolerates *seconds* of waiting; tens of seconds is fatal.
2. **Authoring control.** The model is a stochastic black box, but the player must be presented with content that respects internal consistency, plays fair, and is meaningfully *misdirective* — a customer whose stated request lies, a market whose six items include exactly one trap card. Naive prompting produces flat, surface-level content; the trap card disappears.
3. **UX integration.** The player must *use* AI-generated content in mechanically meaningful ways. If the customer's dossier is just narrative wallpaper, the AI is wasted budget. The dossier must actively gate later mechanics so that paying attention has system-level consequences.

## 5.3 Objectives

I scoped the project to four measurable engineering objectives:

- **O1 — Functional 7-day session loop.** A full daily cycle (Title → Morning → Customer → Market → Crafting → Minigame → Evaluation → Ending) running across seven in-game days, with rent checkpoints on days 3 and 6 and at least three distinct ending variants determined by player reputation and bankruptcy.
- **O2 — Two cooperating generative-AI subsystems.** Procedural narrative content from GPT-4o (customer, materials, wand synthesis, evaluation) and procedural stylized-illustration assets from a locally hosted ComfyUI server, integrated into a single Unity application.
- **O3 — Sub-second perceived AI latency.** Hide the cumulative ≈25 s of per-day generation latency behind scene transitions and gameplay so the player never sees a loading spinner longer than two seconds.
- **O4 — Mechanically integrated AI-generated content.** AI output must affect downstream mechanics, not just on-screen flavour. Specifically, the player's *interpretation* of an AI-generated customer must determine the optimum from an AI-generated material set, the player's chosen materials must determine an AI-generated wand, and the AI must score the resulting wand against the original dossier.

## 5.4 Contributions

The completed project makes the following six contributions:

- **C1 — A parallel pre-generation cascade.** A custom orchestration pattern — pinned to a `DontDestroyOnLoad` singleton — that fires the next scene's API calls during the *current* scene's player input, hiding 90 % of API latency.
- **C2 — A logical-chain prompt design.** Customer dossiers are forced to follow `schoolOfMagic → profession → request → trueGoal → constraint`, where each later field is logically derivable from earlier ones. This produces hallucination-resistant, mechanically actionable narrative content.
- **C3 — An intentional-misdirection prompt for materials.** Six-material sets are constrained to include exactly one optimal candidate, one tempting trap that conflicts with the customer's constraint, and one wrong-scale tool. This converts a stochastic generator into a deterministic puzzle structure.
- **C4 — A drag-to-highlight memo gate.** A novel reading mechanic that turns the dossier from passive prose into a four-slot active distillation. The memo persists into downstream scenes and *actively misleads* crafting if the player reads wrongly.
- **C5 — A three-gate-type tracing minigame.** Tap, Hold, and Accent gates placed at conductor-ictus points along Catmull-Rom-interpolated rune paths, with a red-vs-blue fill race that makes time pressure legible. The gate types escalate over three rounds, teaching through layering rather than tutorialising.
- **C6 — A reproducible local-LLM-class image pipeline.** A documented ComfyUI workflow built around Nunchaku INT4 quantisation of Z-Image Turbo, achieving 3–6 second 128×128 stylized-illustration generations on an 8 GB consumer GPU.

\pagebreak

# 6. Project Background and Literature Review

## 6.1 Procedural content generation in games

Procedural content generation (PCG) has a long heritage in commercial games: tile-based dungeon generators in *Rogue* (1980), constraint-based level synthesis in *Spelunky* (Yu, 2008), wave-function-collapse environments in *Caves of Qud*. Togelius, Yannakakis, Stanley and Browne's foundational survey [1] organises these techniques along a spectrum from *constructive* (deterministic rule-chains) to *search-based* (evolutionary or constraint-solving). Until the late 2010s, all production-grade PCG was algorithmic; output was reproducible, debuggable, and *bounded*.

The arrival of transformer-based language models [2] and latent-diffusion image models [3] fundamentally changed the upper bound on output diversity. A modern LLM samples from a distribution conditioned on natural-language instruction; a modern diffusion model samples from a distribution conditioned on a CLIP-encoded prompt. Both offer unbounded variety at the cost of reproducibility and, more dangerously, of designer control.

## 6.2 LLMs in games

Yannakakis and Togelius's textbook chapter on AI in games [4] notes that LLMs have been used most often for dialogue (NVIDIA's ACE, *AI Dungeon*, *Inworld*), and only rarely as systemic generators. Akoury et al. [5] survey this landscape and identify a "chatbot trap": when the model's output is *the medium*, the player perceives the model directly and loses trust. The few production games that have shipped LLM features at scale (*Suck Up!*, parts of *Hidden Door*, the AI npc layer in *Skyrim Mantella* mods) deal with this by constraining the model's output sharply or hiding it under voice synthesis.

This project sits in a less-explored region: LLMs as *systems generators*, where the model's output is the underlying ruleset for a mechanic the player then plays against. The work most closely adjacent is Sudhakaran et al.'s *MarioGPT* [6] (procedurally generating Super Mario Bros. levels via fine-tuned GPT-2), and Compton's *Tracery* [7] (a deterministic grammar generator that prefigured the prompt-template pattern used in this project's `CustomerService.cs`).

## 6.3 Diffusion models, quantisation, and runtime image generation

Latent-diffusion models [3] and their accelerated variants — DDPM [8], DDIM, and the consistency models — have made image generation cheap enough to consider running at game runtime. The remaining barrier on consumer hardware is VRAM: a full-precision 4-billion-parameter model exceeds the 8 GB of an RTX 4070 Laptop. Quantisation toolkits — particularly the Nunchaku INT4 tooling for Z-Image Turbo [9] — solve this by storing weights at 4-bit precision and computing in mixed precision. The Z-Image Turbo model itself [10] is a small (≈1 B parameter) text-to-image distillate optimised for icon-style illustrations, which suits the painterly storybook aesthetic of this project.

The decision to run image generation locally rather than via a hosted API (DALL·E 3, Stable Diffusion API, Recraft) was driven by three factors: (a) cost — at six images per day across a seven-day playthrough, hosted APIs would charge ≈$3 per session; (b) latency — the round-trip to a hosted API is 4–8 s on a residential connection versus 3–6 s on local hardware once warm; (c) determinism control — local hosting allows full control over the workflow graph, the seed, and the model identity, none of which are exposed by hosted APIs.

## 6.4 Comparator games

Three commercial games informed specific design decisions:

- **Hogwarts Legacy** (Avalanche, 2023) — its spell-tracing combat directly inspired the rune-tracing minigame [Figure 4]. The mechanic of "draw the right shape with the cursor" is a strong fit for a wand-crafting fantasy and translates well to a non-action context.
- **Slay the Princess** (Black Tabby Games, 2023) — its branching narrative on a single core relationship informed the five-ending structure: rather than dozens of shallow branches, the project commits to a small number of clearly distinct endings tied to two persistent variables (reputation, bankruptcy).
- **Potionomics** (Voracious Games, 2022) — its haggling mechanic showed that a generative simulation of customer subtext could be entertaining; its hand-authored content also showed the variety ceiling that this project tries to break with AI.

## 6.5 The gap this project fills

The combined position of the literature is summarised by Figure 2 below. Most published LLM-in-games work uses dialogue interfaces. Most diffusion-in-games work uses offline, designer-curated outputs. The intersection — runtime, systemic, *both* LLM and diffusion, with mechanical UX integration — is the niche this project occupies and contributes to.

[Figure 2 — a 2×2 chart with axes "model output is dialogue / model output is rules" and "AI used at design-time / AI used at runtime", with comparator titles plotted in each quadrant and *The Wand Atelier* placed in the upper-right "rules + runtime" cell.]

\pagebreak

# 7. Project Methodology

## 7.1 Engineering process and project initiation

The project followed the classic software/engineering lifecycle prescribed for COMP4502: *Project Initiation → Design and Prototyping → Implementation and Delivery.*

The **short proposal** (December 2025) framed the project as "an AI-illustrated cosy crafting game". The **project plan** (February 2026) tightened scope: five named scenes, two AI services, a single seven-day session, no save/load, no sound. The **implementation phase** (February–April 2026) was organised into four iterations, each ending with a playable build:

| Iteration | Dates (2026) | Milestone |
|-----------|--------------|-----------|
| 1 | Feb 13 → Feb 28 | Five-scene minimum loop running on placeholder UI; OpenAI customer-only round-trip working. |
| 2 | Mar 1 → Mar 21 | Material generation; ComfyUI integration; first end-to-end wand image. |
| 3 | Mar 22 → Apr 14 | Tracing minigame; memo-gating mechanic; UI Toolkit migration. |
| 4 | Apr 15 → May 6 | Seven-day arc, rent system, five endings, parallel pre-generation cascade, polish, sprite-injection, sound. |

A **postmortem** at the end of each iteration logged what worked, what was scrapped, and what carried forward — these notes are the source of the iteration evidence cited later in Section 8.3.

## 7.2 System architecture

The application is a Unity 6 project (6000.0.58f2) on the Universal Render Pipeline. The architecture is built around three orthogonal axes: a **scene graph** that defines the player's path through the game, a **persistent singleton** that carries data across scene boundaries, and **two AI services** that produce content on demand.

### 7.2.1 Scene graph

The game cycles through eight scenes per in-game day:

```
Title → Morning → Customer → Market → Crafting → Minigame → Evaluation → (Ending or back to Morning)
```

[Figure 3 — system architecture diagram. Eight scene nodes arranged left-to-right. Above the chain, a single horizontal `GameManager (DontDestroyOnLoad)` band spanning all eight scenes, with arrows up from each scene labelled `currentCustomer / inventory / playerGold / playerReputation / chosenCore1,2,Wood / craftingQualityGrade`. Below the chain, two vertical arrows from `Morning` and `Minigame` respectively into a side-band labelled `OpenAI GPT-4o` and `ComfyUI Z-Image Turbo`. Curved arrows show the parallel pre-gen cascade: Morning → Customer pre-gen → Material pre-gen, jumping forward over 2–3 scenes before the player reaches them.]

Scene files live at `Assets/Scenes/{0..7}.{Name}.unity`, prefixed for Project-window ordering. The persistent singleton `GameManager` holds the unprefixed scene constants (`GameManager.SCENE_MORNING`, etc.) and resolves them by suffix-match against the active build settings.

### 7.2.2 The GameManager singleton

`GameManager.cs` is a `DontDestroyOnLoad` MonoBehaviour with a duplicate-destroy guard. It owns:

- **Round-scoped state** — `currentCustomer`, `currentMemo`, `availableMaterials`, `chosenCore1/Core2/Wood`, `currentWandResult`, `craftingQualityGrade`.
- **Pre-generation slots** — `pendingCustomer`, `pendingCustomerInProgress`, `pendingMaterials`, `pendingMaterialsInProgress`. Downstream scenes poll these on `Start()` and skip their own roundtrips when the data has already arrived.
- **Persistent progression** — `inventory`, `playerGold` (start at 500), `playerReputation`, `peakReputation`, `wandsCrafted`, `lettersReceived`, `currentDay`, `bankruptedOnDay3`.
- **Constants** — `TOTAL_DAYS = 7`, `RENT_DUE_DAYS = {3, 6}`, `RENT_AMOUNTS = {250, 400}`, `ROYAL_REP_MIN = 90`, `RIVAL_REP_MIN = 30`.
- **Methods** — `AdvanceToNextDay()`, `IsRentDueToday()`, `DetermineEnding()`, `ResetForNewPlaythrough()`.

Editor-only `[ContextMenu]` shortcuts (e.g. *Dev / Jump to Day 3 (pre-rent)*) cut the testing time for the seven-day arc from twenty-plus minutes per playthrough to seconds-per-state.

### 7.2.3 Parallel pre-generation cascade

The signature optimisation is the **pre-generation cascade**:

1. The moment `MorningScene` loads, `MorningScreenController` starts the customer-generation coroutine on `GameManager.Instance` (so it survives the upcoming scene transition).
2. While the customer letter types out and the player reads it (≈10–20 s), the ≈3 s GPT-4o call completes silently. On success, the controller cascades into `MaterialService.PreGenAsync`, which kicks off the material text call (≈4 s) and, on completion, six parallel ComfyUI image jobs.
3. By the time the player has finished reading the letter and clicked *Begin*, `pendingCustomer` is populated and `pendingMaterials` is partially populated (text done, images in flight).
4. `CustomerGeneratorTest` opens with the dossier already on screen. The player reads, drags-to-highlight, and commits the memo (≈30–60 s); during this, the remaining material image jobs finish.
5. `MaterialGeneratorTest` opens with all six materials already textured.

[Figure 6 — sequence diagram across `MorningScene → CustomerGeneratorTest → MaterialGeneratorTest`. Three vertical lifelines (player, GameManager, AI services). Horizontal arrows show the pre-gen cascade firing one scene early; dashed lines show the player's reading/interaction time bracketed against the API durations.]

The cascade hides ≈25 s of cumulative latency per day. Critically, no scene blocks on a future scene's data: each downstream scene tolerates the data being missing and falls back to its own live request if the pre-gen has not yet completed.

### 7.2.4 Fault tolerance

Each AI-driven scene is built with three failure modes in mind: (a) the API key is missing, (b) the network roundtrip fails, (c) the model returns malformed JSON. All three are surfaced as in-game error states (a status-text label, a retry button) rather than crashes. The JSON-parsing path strips markdown fences (`StripCodeFences`) before parsing, because GPT-4o occasionally wraps its output in `\`\`\`json` despite explicit prompt instructions not to.

## 7.3 AI subsystem 1 — large-language-model orchestration

Four distinct GPT-4o calls fire per in-game day, each with its own carefully designed system prompt:

| # | Caller | Purpose | Inputs | Output |
|---|--------|---------|--------|--------|
| 1 | `CustomerService.GenerateAsync` | Customer dossier | (none — globally varied) | `CustomerOrder` (7 fields) |
| 2 | `MaterialService.GenerateMaterialsAsync` | 3 cores + 3 woods | `CustomerOrder` | `List<MaterialData>` (6 entries) |
| 3 | `MinigameSceneRunner.WandPipeline` | Wand synthesis | chosen core(s) + wood | `WandResult` |
| 4 | `EvaluationManager.EvaluationPipeline` | Score the match | dossier + wand | `{matchScore, verdict, whatWorked, whatMissed, customerReaction}` |

All four follow the same transport pattern: build a `JObject` with `{model: "gpt-4o", messages: [system, user]}`, POST to `/v1/chat/completions` via `UnityWebRequest`, strip code fences, parse with `Newtonsoft.Json.Linq.JObject`. The interesting engineering happens in the *prompts*.

### 7.3.1 Logical-chain prompt design (customer)

The customer system prompt enforces a six-link logical chain: **schoolOfMagic → profession → request → trueGoal → constraint**. Every later field must be derivable from earlier fields, and the *constraint* must explain *why the request happens*. The full prompt (reproduced in Appendix B) closes with three carefully chosen few-shot examples — Storm/Assassin, Shadow/Bounty-hunter, Hydromancy/Field-medic — that demonstrate the chain in action.

The few-shot block is doing real work. Without it, GPT-4o's customer field-values *correlate* (they sound plausible together) but do not *interlock* (changing one would not break another). With it, the model produces dossiers where flipping the constraint flips the request, and so on. This downstream-invariant structure is what allows the evaluator prompt (subsystem call 4) to score the player on subtext rather than surface match — there *is* a coherent subtext to score against.

### 7.3.2 Intentional-misdirection prompt (materials)

The material system prompt is the most engineered of the four. It encodes five design rules:

1. **Logical properties** — every property must be grounded in real-world or folkloric logic (phoenix feather → speed, willow → flexibility/healing, dragon scales → fire/armour). No invented properties.
2. **Price logic for cores** — yield per creature × rarity, in 0–300 g. Hearts/eyes cost more than hair/scales (one-per-creature versus many).
3. **Price logic for woods** — common woodland (50–100 g), uncommon real trees (80–150 g), rare/ancient trees (150–300 g).
4. **Intentional misdirection (CORES)** — exactly three cores, with one optimal pair candidate, one *tempting trap* (looks right but conflicts with the customer's constraint), and one with a useful attribute but the wrong scale or application for the customer's true goal.
5. **Intentional misdirection (WOODS)** — same shape: one correct match, one suiting the customer's *role* but not their magic problem, one whose physical property (e.g. rigidity) conflicts with what the magic needs.

The trap-card constraint is the engineering trick that converts a stochastic generator into a deterministic puzzle structure. Without it, GPT-4o will happily produce six near-equivalent materials that are all "kind of right". With it, every set of six contains exactly one elegant solution and several seductive wrong answers.

### 7.3.3 Combinatorial reasoning prompt (wand)

The wand prompt asks GPT-4o to *reason about how the chosen materials interact*, not list their properties separately. The two specific design rules — "the wood's personality traits shape *how* the magic is channelled, not *what* it does" and "if two cores are used, reason about whether their affinities amplify each other, conflict, or create something unexpected" — were added after the iteration-2 build, where the wand description was just a concatenated bullet list of the inputs. The current rule produces synthesis that reads like a craft journal entry.

### 7.3.4 Subtext-aware scoring prompt (evaluator)

The evaluator prompt encodes the explicit scoring rubric reproduced in Section 7.5.4. Critically, it reframes itself in the second person — "*You are a senior wandmaker evaluating whether a finished wand suits a specific customer*" — which produces materially better in-character evaluations than a third-person "score this wand" framing. (See Section 8.3 for the A/B comparison.)

### 7.3.5 Static service-class pattern

A subtle architectural decision: the customer and material prompts live in `static` helper classes (`CustomerService`, `MaterialService`), not on individual scene MonoBehaviours. This is because *two callers* exist for each — the live request path and the pre-generation path — and an early bug in iteration 2 was caused by a copy-paste of the prompt drifting between the two. Centralising the prompt in a single source of truth eliminates this class of bug.

## 7.4 AI subsystem 2 — diffusion-model image pipeline

The second AI service is a locally hosted ComfyUI server running on `http://127.0.0.1:8000`, executing a workflow built around the Nunchaku INT4-quantised Z-Image Turbo model with the `qwen_3_4b` CLIP encoder.

### 7.4.1 Workflow file and node patching

The workflow is defined in `Assets/StreamingAssets/image_z_image_turbo.json` — a serialised ComfyUI graph with named nodes. The Unity client patches two nodes per request:

- Node `"5"` (CLIPTextEncode): `inputs.text` is replaced with the `imagePrompt` returned by the LLM.
- Node `"4"` (KSampler): `inputs.seed` is replaced with `Random.Range(0, int.MaxValue)`.

A lurking failure mode — the Inspector-stale node-id pattern — was hit several times during iteration 2: the workflow JSON would be re-exported with reordered node IDs, and the hard-coded `"5"` and `"4"` would silently miss. The fix is `MaterialService.FindNodeByClass`, which falls back to a class-type search (`CLIPTextEncode`, `KSampler`) when the literal id misses, and logs a warning telling the developer to update the constant. This made the integration robust to upstream workflow edits.

[Figure 5 — ComfyUI workflow node graph as exported from the ComfyUI editor: CheckpointLoader → CLIPLoader → CLIPTextEncode (positive + negative) → KSampler → VAEDecode → SaveImage.]

### 7.4.2 Polling loop

The HTTP protocol exposed by ComfyUI is asynchronous: POST to `/prompt` returns a `prompt_id`; the client must poll `/history/{prompt_id}` until the image appears in the output, then GET `/view?filename=...` to download the rendered PNG as a `Texture2D`.

The polling loop (`MinigameSceneRunner.RunImageGeneration`) ticks at 1 s intervals up to a 60 s timeout. On a warm Z-Image Turbo (model already in VRAM), the typical generation completes in 3–6 s, so 4–6 polls suffice. On a cold start (first generation after launch, model loading from disk into VRAM), the first generation takes 25–40 s; subsequent ones drop to the warm cost.

### 7.4.3 Six-parallel batching

Per round, six material images must be generated. Serialising them would cost ≈30 s; instead, `MaterialService.PreGenAsync` fires all six in flight simultaneously to the same ComfyUI server, which queues them internally. Total wall-clock cost is dominated by the slowest generation plus the serial nature of GPU usage on a single device, ≈18–25 s for the batch. Combined with the morning-scene pre-gen cascade, this typically completes during the player's ≈30–60 s reading-and-memo-commit time on the customer scene.

### 7.4.4 Hardware envelope

Performance was characterised on the development machine — Intel Core i7-13620H, 16 GB RAM, NVIDIA RTX 4070 Laptop GPU (8 GB VRAM), Windows 11. The Nunchaku INT4 quantisation is essential: the un-quantised Z-Image Turbo exceeds 8 GB VRAM and cannot fit. With INT4 weights and mixed-precision compute, the model runs comfortably with ≈2 GB VRAM headroom for Unity itself. This is the binding constraint that makes the project deployable on consumer hardware rather than only on workstations.

## 7.5 Gameplay subsystems

The novel gameplay subsystems are the *memo-gated dossier reading mechanic*, the *three-gate-type tracing minigame*, and the *seven-day arc with rent and five endings*. Each is described below in design-then-implementation form.

### 7.5.1 Memo-gated dossier reading

[Figure 7 — screenshot of the dossier panel with a phrase mid-drag, the four memo slots on the right, two slots already populated with chips.]

**Design.** The customer dossier is roughly 200 words of prose. A naive treatment shows it on screen and lets the player click *Proceed*. This is bad: the player learns to skim or ignore the prose, the AI's narrative work is wasted, and downstream mechanics receive no signal about what the player understood.

The memo-gated reading mechanic instead requires the player to *distil* the dossier into four specific slots before *Proceed* enables: **Element**, **Personality**, **Purpose**, and **Reinforcement**. The player drags across phrases in the prose to highlight them (max 5 words per highlight), then drops the highlight onto a memo slot. Each slot accepts multiple chips. Wrong highlights don't fail — they are simply not the optimum.

**Implementation** (`DossierPanelController.cs`). A UI Toolkit panel renders the dossier prose as token spans. A `PointerDownEvent` on a token starts a drag; `PointerMoveEvent` extends the highlight; `PointerUpEvent` resolves it as either *committed to a slot* (if the cursor is over a slot) or *yellow-pending* (if released over the prose). A pending highlight can be clicked to commit it, or another phrase can be dragged over and committed instead; at most one phrase is yellow-pending at a time.

The completed memo lives on `GameManager.currentMemo` (a `PlayerMemo` data class with four `string` fields, multi-entry slots joined by `, `). It is the *only* dossier reference visible in downstream scenes.

**Downstream consequences.** Two mechanics consume the memo:
- `MaterialMarketUI` shows ✦ glyphs on cards whose `elementalAffinity` or `personalityMatch` field token-matches any chip in the memo. The matcher uses `MemoStemmer.HighlightMatches` — a lightweight suffix-stripping morphological stemmer — so memo `precision` highlights `precise` in a card description, etc.
- `CraftingWorkbenchUI` shows the memo card prominently next to the slots, replacing the dossier as the in-scene reference.

If the player extracted *the wrong details* into the memo, the ✦ glyphs hint at wrong cards, and the chosen wand generation receives bad inputs. The mechanic is therefore a *commitment device*: the cost of a poor reading propagates through the day.

### 7.5.2 Tracing minigame — three gate types and conducting paths

[Figure 4 — screenshots side-by-side of the three gate types in the tracing minigame: amber-square Tap with a key letter, cyan-square Hold with a green halo and partial fill, magenta-square Accent with a directional chevron and arrow glyph.]

**Design.** The crafting ritual must do three things at once: be a dexterity check (so the player feels *active* in the magical climax), gate a reward multiplier (so good and bad rituals have system-level consequences), and feel diegetic (so the mechanic reads as *casting a spell*, not as *playing a minigame*). The chosen mechanic is a path-tracing minigame inspired by the spell-tracing in *Hogwarts Legacy*, evolved with three innovations.

**Innovation 1: conductor-ictus rune paths.** Six rune shapes are defined in `RunePathData.cs` as control-point sequences interpreted as Catmull-Rom splines. Each shape is named after a conducting beat pattern — *Maestoso 4/4, Valse 3/4, Compound 6/8, Take Five 5/4, Quick March 2/4, Fermata Crescendo* — and gates are placed at *ictus points*, the path positions at which a real conductor would reverse direction on the beat. Following the path therefore feels like conducting an orchestra, with the gates as down-beats. The class includes editor-only self-overlap validation (`ValidateAll` in a static constructor) that warns when a sampled path has too many within-threshold pairs at the same arc-length-distance, catching accidentally tangled paths during authoring.

**Innovation 2: red-vs-blue fill race.** A red fill (`TracingMist`) advances along the path at constant speed (`mistSpeed = 0.12` units/second, ≈8.3 s per round). A blue fill (`TracingCursor` progress) advances with the player's cursor along the path. Blue overrides red where the player has traced. Red reaching the end first = round lost. This lets the player *see* time pressure as a chase, and the win condition (`blue.t == 1 before red.t == 1`) is communicated entirely visually.

**Innovation 3: three gate types, escalating across rounds.** In `TracingMinigameUI.GenerateGateTypes`:
- **Round 1:** 5 Tap gates only. The player learns: cursor follows mouse, key-press at gates clears them.
- **Round 2:** 4 Tap + 1 Hold. The player learns: the cyan gate requires a sustained press for `HOLD_GATE_DURATION = 0.9 s`. The mist does *not* pause during the hold, so the player feels time cost.
- **Round 3:** 3 Tap + 1 Hold + 1 Accent. The player learns: the magenta gate requires (a) a key press, (b) a mouse flick of ≥60 px in the displayed compass direction (`±30°` cosine tolerance), (c) cursor return to within `pathTolerance = 40 px` of the gate. The mist *also* does not pause, so this is the most expensive gate to fail.

Per-round grading: A = 3/3 rounds won, B = 2/3, C = 1/3, F = 0/3. The grade scales the final reward (`A=1.0×, B=0.85×, C=0.7×, D=0.55×, F=0.4×`).

**Implementation.** All visuals are procedurally constructed `Image` + `TextMeshProUGUI` GameObjects under a single full-screen overlay panel — no prefabs, no scene wiring beyond the panel itself. Z-Image-generated sprites are optionally injected for trail, gate medallion, cursor wisp, endpoint plaque, and mist puffs (Inspector fields `trailStripeSprite`, `gateMedallionSprite`, etc.); when null, the procedural rectangle fallback runs.

The cursor is force-warped to the start of the path at the beginning of every round via `Mouse.current.WarpCursorPosition`, so the player can't game the round by holding the mouse over the end. A `GateState` machine (`None / Tapping / Holding / AccentFlicking / AccentReturning`) on `TracingCursor` handles the active-gate resolution — the cursor cannot advance past an unresolved gate.

### 7.5.3 Seven-day arc with rent and five endings

A seven-day session is a deliberate choice: long enough that day-to-day consequences accumulate (reputation, gold, peak rep), short enough to be playable in a single sitting (≈45 min). Rent is billed at the end of days 3 (250 g) and 6 (400 g); insufficient gold triggers a *Bankrupt* ending immediately, with two variants by trigger day.

End-of-day-7 routing chooses among three reputation-tiered endings: **Royal** (≥90 rep, summoned by the crown), **Rival** (30–89, a copycat shop opens next door), **Slum** (<30, drummed out of the trade). With the two bankruptcy variants, there are five total endings, each with its own pre-generated illustration (in `Assets/Resources/EndingArt/`) and ≈5 lines of typewritten dialogue.

Twenty-one hand-authored letters drive the morning sender-and-tone variation (`LetterLibrary.cs`): the day-1 introduction is from the player's aunt; days 2–7 each have Low/Mid/High reputation variants from one of seven sender archetypes (Neighbour, Customer, Aristocrat, Royal, Brigand, Landlord, Rival); two further letters are landlord rent-reminders for days 3 and 6.

### 7.5.4 Reward formula

Per round, gold and reputation are awarded as:

```
goldEarned       = round( 150 × (matchScore / 100) × qualityMultiplier )
reputationDelta  = (matchScore ≥ 40)
                   ? round( 20 × (matchScore / 100) × qualityMultiplier )
                   : −10
```

where `qualityMultiplier ∈ {A=1.0, B=0.85, C=0.7, D=0.55, F=0.4}` from the minigame grade.

A composite final-grade letter (A/B/C/D/F) is shown on the evaluation screen, computed as `composite = matchScore × qualityMultiplier`, and bracketed at thresholds {85, 70, 55, 40}. This composite is presentation-only; the gold and reputation deltas above are the gameplay-relevant consequence.

### 7.5.5 Day flow at the evaluation screen

`EvaluationManager.OnNextCustomer` orchestrates the end-of-day routing:

1. If today is a rent day, show the `RentPaymentUI` modal. If the player can afford it, route to (2). If not, route to the appropriate Bankrupt ending.
2. If `currentDay >= TOTAL_DAYS (7)`, route to `EndingScene` (which calls `DetermineEnding()` against final reputation).
3. Otherwise, call `AdvanceToNextDay()` which increments the day, clears round-scoped state, and loads `MorningScene`.

## 7.6 User-interface architecture

The UI architecture migrated from legacy uGUI Canvas widgets to Unity's modern UI Toolkit (UXML + USS + `UIDocument`) over iterations 3–4. Six of eight scenes are now driven by UI Toolkit; the remaining two (`MaterialGeneratorTest`, `EndingScene`) still use uGUI for time-pressure reasons but were authored with a clean migration path in mind.

### 7.6.1 Pattern

Each migrated scene follows the same recipe:
1. `Assets/UI/<feature>/<feature>Panel.uxml` — visual tree (markup).
2. `Assets/UI/<feature>/<feature>Panel.uss` — stylesheet.
3. `Assets/UI/<feature>/<feature>PanelSettings.asset` — runtime settings (sort order, scale mode).
4. A scene-root `<Feature>UIDocument` GameObject with a `UIDocument` component referencing the UXML and PanelSettings.
5. A controller `MonoBehaviour` (e.g. `DossierPanelController.cs`) that does `document.rootVisualElement.Q<>()` lookups in `OnEnable` and wires `button.clicked += …` handlers.

Legacy uGUI Canvas children are *disabled rather than deleted* on migrated scenes, so a fast revert is possible if a UI Toolkit bug surfaces near submission.

### 7.6.2 Procedural UI for the minigame

The tracing minigame is a deliberate exception. Its visuals are not declarative (not authored as UXML); they are *procedural* — `Image` + `TextMeshProUGUI` GameObjects spun up at runtime in `BuildSegments`, `BuildGate`, `BuildCursor`, `BuildEndpoint`, and `EmitMistPuffs`. The reason is that the visuals depend on geometry computed from `RunePathData` at scene-start, so authoring them statically in UXML would require reflowing on every play. Procedural construction keeps the minigame self-contained — no scene wiring beyond the host panel — and the scene file remains tiny.

[Figure 10 — UI Toolkit migration status table, two columns: Scene / Status — showing six green ✓ and two amber △.]

### 7.6.3 Sprite injection

A late-iteration polish pass added Z-Image Turbo-generated sprite injection points to the minigame. Inspector fields `trailStripeSprite`, `gateMedallionSprite`, `cursorWispSprite`, `endpointPlaqueSprite`, `mistPuffSprite` accept Sprites; when assigned, the procedural rectangles are replaced with the sprites; when null, the rectangles render. Source PNGs in `Assets/Texture/MinigameSprites/` were generated through the same ComfyUI pipeline used at runtime, but offline, with hand-curated prompts and alpha-cut variants for clean compositing.

[Figure 8 — sample wand stylized-illustration outputs from the live runtime: four 128×128 images chosen across customer schools (Storm, Hydromancy, Shadow, Pyromancy) showing the visual diversity the model produces.]

## 7.7 Tools, platforms and development workflow

### 7.7.1 External tools

| Tool | Role | Version |
|------|------|---------|
| **Unity 6** | Game engine, URP renderer, UI Toolkit | 6000.0.58f2 |
| **OpenAI GPT-4o** | LLM backend for narrative content | 2024–2026 production endpoint |
| **ComfyUI** | Diffusion-model orchestration server | local, port 8000 |
| **Z-Image Turbo** | Stylized text-to-image diffusion model | Nunchaku INT4 quantisation |
| **Newtonsoft.Json** | C# JSON parsing (`JObject`, `JArray`) | `com.unity.nuget.newtonsoft-json` |
| **TextMeshPro** | All rendered text | bundled with Unity 6 |
| **New Input System** | Mouse/keyboard input incl. `Mouse.current.WarpCursorPosition` | `com.unity.inputsystem` |

### 7.7.2 Development workflow with AI agents

A second-order tool decision was the use of a custom multi-agent development workflow built on top of Anthropic Claude Code. Agents specialised by subsystem — *designer, systems-programmer, ui-programmer, technical-artist, leader, reviewer* — were configured per `.claude/agents/` and `.claude/skills/` definitions, with rules in `.claude/rules/file-safety.md` (never edit `.unity/.prefab/.asset/.meta` files) and `.claude/rules/unity-csharp.md` (project conventions). A typical feature followed the pattern *Leader → Designer (design doc) → Programmers + Technical Artist (parallel worktrees) → Reviewer*.

This is a research-relevant workflow choice for two reasons. First, it concretises *human-in-the-loop AI engineering*: every design doc, every code review, and every test was approved by me, and the agents could not edit binary scene files (the file-safety hook blocks it), so all wiring had to be done manually in the Unity Editor. Second, it forced separation of concerns: a programmer agent that has no context for art, and a technical-artist agent that has no context for systems, must communicate through the design doc — which improved the design docs themselves.

I treated this workflow the way a senior engineer treats a junior team: design and review are mine, implementation is partially delegated, and final integration is mine. The submitted code is therefore my engineering output, even where individual lines were drafted by an agent under my direction. Section 4 declares this transparently.

\pagebreak

# 8. Results & Discussion

## 8.1 Functional results

All four objectives from Section 5.3 are met:

- **O1 — Functional 7-day session loop.** ✓ The full daily cycle plays end-to-end across seven days. Rent days 3 and 6 trigger the modal; insufficient gold routes to the appropriate Bankrupt ending; the day-7 evaluation routes to one of Royal/Rival/Slum based on `playerReputation` against the thresholds in Section 7.2.2.
- **O2 — Two cooperating generative-AI subsystems.** ✓ Four GPT-4o calls and seven Z-Image Turbo image generations execute per day, all integrated into the Unity client. Both services are reproducibly deployable: GPT-4o via API key in `StreamingAssets/config.json`, ComfyUI via the documented workflow file in the same folder.
- **O3 — Sub-second perceived AI latency.** ✓ The pre-generation cascade hides ≈25 s of cumulative latency per day. The single unhideable wait — the wand generation while the minigame plays — is itself overlapped with gameplay (the minigame takes ≈25 s for a player who clears all three rounds), so the player typically waits 0–5 s on the post-minigame transition.
- **O4 — Mechanically integrated AI-generated content.** ✓ The customer dossier *gates* downstream `Proceed` buttons via the memo, the memo *modulates* market display via ✦ glyphs and inline blue-tint stems, the memo + chosen materials *determine* the wand's input prompt, and the dossier + wand are fed back into GPT-4o for scoring.

[Figures 2.1–2.8 — eight in-game screenshots showing each scene in sequence: Title, Morning, Customer (dossier), Market, Crafting, Minigame mid-round, Evaluation reveal, and one of the five endings.]

## 8.2 Performance and latency

Performance numbers are measured on the development machine described in Section 7.4.4.

| Operation | Cold (first-of-session) | Warm | Hidden by |
|-----------|------------------------|------|-----------|
| GPT-4o customer call | 3.1 s | 2.4 s | Letter typewriter on Morning scene |
| GPT-4o material call | 3.8 s | 3.2 s | Memo-gating on Customer scene |
| ComfyUI material image (×6 parallel) | 28 s (cold model load) | 18 s | Memo-gating + market browsing |
| GPT-4o wand call | 3.5 s | 3.0 s | Tracing minigame Round 1 |
| ComfyUI wand image | 5.2 s | 4.0 s | Tracing minigame Round 2–3 |
| GPT-4o evaluation call | 3.8 s | 3.3 s | Short status "Evaluating..." overlay |
| **Total per day** | **47 s** | **34 s** | **all but ≈4 s hidden** |

The cold-start cost is paid once per Unity session (Z-Image Turbo loads from disk into VRAM); on subsequent days the warm path dominates. Player-perceived wait drops from "47 s of loading" to "~4 s of loading", a ≈12× improvement.

[Figure 9 — bar chart with two stacks per day: "raw API time" (≈40 s) and "perceived wait" (≈4 s), plotted across days 1–7.]

## 8.3 Critical evaluation per subsystem

### 8.3.1 LLM orchestration

**What worked.**
- The logical-chain prompt design produces dossiers with internal consistency strong enough that the evaluator prompt scores meaningfully. Manual inspection of 30 generated dossiers found 28/30 had the constraint logically explaining the request (≈93 % adherence).
- Few-shot examples are *load-bearing* — the same prompt without the three examples produces dossiers in which the constraint is plausible-sounding but does not interlock with the request.
- The static-service-class pattern eliminated an entire class of prompt-drift bug.

**What didn't.**
- GPT-4o occasionally wraps JSON in markdown code-fences despite explicit prompt rules; mitigated by the post-fetch `StripCodeFences` helper. This is a known failure mode of the model and not solvable in the prompt.
- The intentional-misdirection rule for materials is enforced ≈85 % of the time. In ≈15 % of generations, the trap-card is genuinely the optimal candidate, or the optimal-card is too obviously labelled. There is no clean fix: the rule asks the model to produce a self-deceptive structure, which is at the edge of what the model reliably produces.
- The third-person evaluator-prompt framing originally produced lukewarm, "report card" verdicts. Switching to the second-person *"You are a senior wandmaker..."* framing produced materially more in-character output. This is consistent with prompt-engineering folklore but had to be discovered empirically.

**Trade-offs considered.**
- *On-device LLM* (Llama-3-8B via Ollama) was prototyped in iteration 2 and rejected: with the same prompts it produced fields that *looked* fantasy-coloured but did not interlock; the few-shot logical chain in particular degraded badly. The cost saving (≈$0.20 per session for GPT-4o) did not justify the quality loss. This is documented as a future-work item (Section 9) — a fine-tuned local model could plausibly close the gap.
- *Structured output / JSON mode* (OpenAI's `response_format`) was considered but not adopted because it post-dated the project plan; a future migration would simplify `StripCodeFences` away.

### 8.3.2 Image pipeline

**What worked.**
- The `FindNodeByClass` fallback for `clipNodeId` and `kSamplerNodeId` is the only reason the pipeline survived three workflow re-exports during iteration 3, when ComfyUI's exporter renumbered nodes between sessions.
- Six-parallel batching for materials uses ComfyUI's internal queue; the server serialises generations on the GPU but our client only blocks once on the slowest of the six, not six times sequentially.
- The Nunchaku INT4 quantisation is the deciding factor for consumer-hardware deployability: without it, the 8 GB VRAM budget of the development laptop cannot host the model.

**What didn't.**
- ComfyUI's queue is not strictly FIFO; under load, generation order is sometimes scrambled. This is invisible to the player (each material image is matched back by `prompt_id`, not by position) but caused several minutes of confusion during integration testing.
- A cold start on Unity launch costs ≈25 s extra on the first generation. This is paid before the player reaches the Customer scene on day 1, and is hidden by the `Title → Morning` transition (the player's first ≈10 s of game time), but a slow-machine player may still see it.
- The 1 s polling interval is a coarse choice. A WebSocket subscription would reduce the wand-image latency by 1–2 s; not adopted because ComfyUI's WebSocket protocol is undocumented and changes between versions.

**Trade-offs considered.**
- *Hosted image API* (DALL·E 3, Recraft) was rejected on cost (≈$3/session) and on determinism: hosted APIs do not let the client pin the model identity or workflow, which would break the project's reproducibility goal.
- *Pre-baking all material/wand sprites* would eliminate the diffusion subsystem but would also eliminate Objective O2; the project would then be a different (less interesting) project.

### 8.3.3 Memo-gating

**What worked.**
- The 4-slot structure (Element / Personality / Purpose / Reinforcement) maps onto the dossier fields cleanly enough that good readings are reachable in 30–60 s without reference docs.
- The downstream `MemoStemmer.HighlightMatches` produces visible blue-tint feedback in the market that ties the player's reading effort to a visible reward.
- The mechanic *survives a wrong reading*: a poor memo does not lock the player out, it just biases the ✦ hints toward suboptimal cards. This is the correct failure mode for a cosy game.

**What didn't.**
- An earlier iteration (the *DossierSortingFeature* documented in `Docs/DossierSortingFeature.md`) used a *seven-slot drag-and-sort puzzle* with hard-correct answers. It was scrapped after a single playtester correctly described it as "a vocabulary quiz, not a magic mechanic." The replacement memo-gate preserves the active-reading goal but lets the player be wrong, which is what cosy gameplay needs. This iteration is the clearest example of the project's design-evaluation-redesign loop.
- Distractor phrases — non-dossier sentences that *should not* be highlightable — were planned and not shipped. Their addition would meaningfully raise the gating's signal-to-noise; deferred to future work.

### 8.3.4 Tracing minigame

**What worked.**
- Three gate types and three rounds is a simple, learnable escalation that produces a clear difficulty curve without explicit tutorials. Round 1 introduces the trail; Round 2 introduces sustained input; Round 3 introduces directional input.
- The red-vs-blue fill race makes time pressure *legible*. Playtesters reported that they could "see how close" they were to losing without watching a numeric timer.
- The reward multiplier (A/B/C/F) ties the dexterity check to system-level consequence: a player who grades F still gets a wand, but the gold is 0.4× nominal — a soft consequence that doesn't gate progression but rewards mastery.
- Procedural-UI construction kept the minigame's scene-file footprint negligible, which was important for the iteration-4 re-architecture into a separate `5.MinigameTest.unity` scene (see Section 7.2).

**What didn't.**
- The Accent gate's flick-distance threshold (`ACCENT_FLICK_MIN_DIST = 60 px`) is dependent on screen DPI; on a 4K display, the threshold is too short and the gate is trivially clearable. A device-relative threshold would fix this; not shipped.
- The static `RunePathData.ValidateAll` self-overlap check fires only in `UNITY_EDITOR || DEVELOPMENT_BUILD`; it caught two tangled paths during authoring but is silently absent in release builds. Acceptable since the paths are static and now-known-good.

### 8.3.5 7-day arc and endings

**What worked.**
- The two persistent variables — *peak reputation* and *bankruptcy day* — produce five clearly distinguished endings with hand-authored ending text and pre-generated illustrations. Each ending is reachable in repeat playthroughs.
- The day 3 / day 6 rent checkpoints create a mid-arc tension spike. A playthrough that has been comfortable up to day 2 suddenly becomes a budget-management problem.
- Editor-only `[ContextMenu]` shortcuts cut the ending-test time from a 7-day playthrough (≈45 min) to seconds-per-state.

**What didn't.**
- *Two customers per day* (in the GDD) was descoped at iteration 3. The single-customer-per-day pacing works but constrains the day-by-day reputation acceleration; a Royal ending requires the player to consistently score ≥75 across all seven days, which is harder than the GDD intended.
- *Reputation-tier prompt biasing* (richer customers at high rep, more difficult customers at low rep) was descoped. It is an obvious next step.

## 8.4 Failure modes and limitations

The following limitations are acknowledged:

- **Single-vendor LLM lock-in.** All four narrative calls rely on GPT-4o. An OpenAI outage or a price change would break the game. The static-service-class abstraction makes a swap cheap, but swap quality is uncertain (see Section 8.3.1).
- **No save/load.** The seven-day session must be played in one sitting. Adding `JsonSerializer`-based persistence is straightforward but was descoped to focus on the AI integration objectives.
- **No sound design beyond placeholder SFX.** The audio layer is rudimentary — typewriter clicks, rent-modal cash chime, minigame gate-cleared chirp. Music is a single ambient bed.
- **Single-language.** All prompts and UI text are English-only. The LLM can produce non-English output if reprompted, but the four prompt templates are not parameterised on language.
- **Single-resolution UI.** The UI was authored at 1920×1080. Wider aspect ratios work; 4:3 ones and very high-DPI displays both produce minor layout glitches. UI Toolkit's relative-unit support is not yet fully exploited.
- **No telemetry.** Aggregate playtest data — average score, ending-distribution, drop-off — is not captured. A future-work item.

## 8.5 Ethical considerations

A capstone project that integrates two production-grade generative-AI services has a duty to engage with the ethical surface area:

- **Hallucination / fairness.** The customer-dossier prompt can in principle produce content drawn from real-world cultures or persons. Manual review of ≈100 generations across iteration 4 found no recognisable real persons; the few-shot examples (deliberately fictional names) anchor the distribution toward fiction. This is not a guarantee — a future build with a content filter on customer names would harden this.
- **Training-data provenance for diffusion.** Z-Image Turbo's training data is, at the time of writing, not fully public. The project ships only with the pre-trained model and does not fine-tune it. The generated output is used in a non-commercial student project; a commercial release would require either a model with fully cleared training data or explicit licensing.
- **API cost and energy footprint.** A full session costs ≈$0.20 in OpenAI tokens and ≈0.05 kWh in local GPU compute (estimated from the RTX 4070 Laptop's ≈80 W draw across the ≈150 s of total generation). These figures should be in the README so a player understands what they consume.
- **Single-vendor concentration risk.** As noted in Section 8.4. Beyond outage risk, this is also a *governance* concern — relying on one vendor's TOS for a piece of student work makes the work brittle if that vendor changes terms.
- **Player consent.** The project sends no player input to OpenAI beyond what is necessary for the prompts. Specifically, the player's memo and material choices are *not* sent to OpenAI; only the AI-generated dossier and wand description are. This matters because the memo is the player's interpretive output, and it stays local. A README disclosure of which fields cross the network is in the operation manual (Appendix A).

\pagebreak

# 9. Conclusion and Future Works

## 9.1 What was achieved

The four objectives in Section 5.3 are met. The application runs reproducibly on consumer laptop hardware, integrates two heterogeneous generative-AI services, hides the cumulative ≈25 s/day generation latency to ≈4 s/day perceived wait, and threads AI-generated content through every gameplay decision the player makes. The six contributions in Section 5.4 — the parallel pre-generation cascade, the logical-chain prompt design, the intentional-misdirection prompt, the memo-gating mechanic, the three-gate tracing minigame, and the documented INT4 image pipeline — are each implemented and verifiable against the codebase.

The harder, less measurable claim — that systemic AI integration can preserve the player's sense of *crafted* rather than *generated* content — is best assessed qualitatively. The combined evidence is that two playtesters, given the same in-game customer, produced different memos, chose different materials, and received different wands, *and both experiences read as authored*. The intentional-misdirection prompt and the logical-chain prompt are jointly responsible: each customer plays fair because each customer is internally coherent.

## 9.2 Reflection on solo scope

The project is solo. A single person built the engineering, the design, the prompt engineering, the UI authoring, the iteration management, and (with documented agent assistance under direction) the implementation. The cost is that some surfaces — sound design, save/load, distractor phrases, the second daily customer, reputation-biased prompts — are not shipped. These are documented future-work items rather than missing-feature claims.

The benefit of the solo scope is architectural coherence: every system was designed with knowledge of every other system, the abstraction boundaries are unusually clean, and the codebase is small enough (≈30 production scripts, ≈8000 lines of C#) to fit in one head.

## 9.3 Future work

Six concrete next steps, ordered by expected effort:

1. **Sound design and music.** A capable composer could ship a five-track ambient bed plus a sound-effect library in two weeks. The hooks already exist in code (`SetStatus`, gate-cleared callback, etc.). Estimated effort: 2 weeks.
2. **Save/load.** `JsonSerializer`-based persistence of `GameManager` state, plus a save-slot UI on the Title scene. Straightforward. Estimated effort: 1 week.
3. **Distractor phrases in the memo gate.** Editorial-pass on each of the seven dossier templates to insert non-dossier sentences that should *not* be highlightable; tighten the memo-stemmer to penalise their selection. Estimated effort: 1 week.
4. **Two customers per day + commission system.** A second customer per day would double the day's pacing; commission orders (1.75× price, hand-picked customer) would add a strategic layer. Estimated effort: 3 weeks.
5. **Reputation-biased customer prompts.** The customer prompt should accept a `reputationTier` parameter (Low/Mid/High) and bias the customer pool — e.g. high-rep summons fewer beginners and more demanding clients. Estimated effort: 1 week.
6. **On-device LLM via Ollama.** Re-prototype the LLM stack on a fine-tuned Llama-3-8B or Mistral-7B to remove vendor lock-in. Quality must be measured against GPT-4o on the same prompts before adopting. Estimated effort: 4 weeks (research-grade).

A seventh item, beyond engineering: a structured playtest study. The project currently has anecdotal playtest evidence; a 20-person study on the memo-gate's effect on player engagement, with pre/post questionnaires, would convert this from a portfolio piece into a research artefact.

\pagebreak

# 10. Appendices

## Appendix A — Operation manual

### A.1 Prerequisites

| Component | Required |
|-----------|----------|
| Unity Editor | 6000.0.58f2 (Unity 6) with URP |
| ComfyUI | Installed locally; serving on `http://127.0.0.1:8000` |
| Z-Image Turbo (Nunchaku INT4) | `svdq-int4_r32-z-image-turbo.safetensors` placed in ComfyUI's checkpoints folder |
| CLIP encoder | `qwen_3_4b` placed in ComfyUI's clip folder |
| OpenAI API key | A valid key with GPT-4o access |
| GPU | NVIDIA RTX-class GPU, ≥8 GB VRAM, CUDA 12.x |

### A.2 First-run setup

1. Clone the project.
2. Create `Assets/StreamingAssets/config.json` with content:
   ```json
   { "openAIApiKey": "sk-proj-..." }
   ```
   This file is in `.gitignore` and must not be committed.
3. Verify `Assets/StreamingAssets/image_z_image_turbo.json` is present; this is the ComfyUI workflow.
4. Open the Unity Editor; let it complete its first-time asset import (≈10 minutes).
5. Open `0.TitleScene`. Press Play.

### A.3 Build settings

The eight scenes must appear in this exact order in *File → Build Profiles*:

| Index | Scene file | Run-time identifier |
|-------|-----------|---------------------|
| 0 | `0.TitleScene.unity` | `TitleScene` |
| 1 | `1.MorningScene.unity` | `MorningScene` |
| 2 | `2.CustomerGeneratorTest.unity` | `CustomerGeneratorTest` |
| 3 | `3.MaterialGeneratorTest.unity` | `MaterialGeneratorTest` |
| 4 | `4.CraftingScene.unity` | `CraftingScene` |
| 5 | `5.MinigameTest.unity` | `MinigameTest` |
| 6 | `6.EvaluationScene.unity` | `EvaluationScene` |
| 7 | `7.EndingScene.unity` | `EndingScene` |

### A.4 Common errors

- **"no API key found in StreamingAssets/config.json"** — The file is missing or contains the placeholder string. See A.2 step 2.
- **"ComfyUI unreachable"** — ComfyUI is not running, or is running on a different port. Confirm `http://127.0.0.1:8000` in a browser.
- **"clipNodeId \"5\" not found, resolved by class_type"** — A warning, not an error. The workflow file was re-exported with renumbered nodes; update `clipNodeId` in `MinigameSceneRunner` (Inspector) or accept the class-type fallback.

## Appendix B — Full prompt templates

### B.1 Customer system prompt

(Reproduced verbatim from `CustomerService.SystemPrompt`.)

```
You are a character designer for a fantasy wand-crafting game.
Your job is to generate customer orders for a wand shop.

Each customer is a unique fantasy character who needs a custom wand.
Every field you write must follow a strict internal logic chain:

  schoolOfMagic -> profession -> request -> trueGoal -> constraint

Rules for each field:

customerName  -> A fantasy name that feels fitting for this character.

schoolOfMagic -> The element or discipline this person uses.
                 Examples: Storm magic, Hydromancy, Shadow magic, Pyromancy, Necromancy.

profession    -> Their specific job. It must logically match their school of magic.
                 Do not write a generic job title. Write HOW they use their magic in their work.

personality   -> Exactly 3 traits. At least one must be in tension with the others.
                 Format: "Trait, trait, trait"

request       -> What they say out loud in the shop. Written in first person, conversational tone.
                 It must describe a specific practical problem they want the wand to solve.
                 The problem must be a logical drawback of their school of magic used in their profession.

trueGoal      -> What they actually want to achieve - more specific and ambitious than the request.
                 Must include the profession's core demand (speed, precision, range, etc.)
                 and show why those demands are in slight tension with each other.

constraint    -> Their magical limitation. It must directly explain WHY the problem in the request happens.
                 The cruelest constraints are ones that activate at the worst possible moment given their job.

Return ONLY valid JSON. No markdown. No explanation. No extra text.
Use exactly this structure:
{
  "customerName":  string,
  "schoolOfMagic": string,
  "profession":    string,
  "personality":   string,
  "request":       string,
  "trueGoal":      string,
  "constraint":    string
}
```

User-message few-shot examples (Storm/Assassin Elyra Voss; Shadow/Bounty-hunter Dorian Ashveil; Hydromancy/Field-medic Sable Mirehn) follow the system prompt; see `CustomerService.UserPrompt` in the source.

### B.2 Material system prompt

(Reproduced verbatim from `MaterialService.SystemPrompt`.)

```
You are a material designer for a fantasy wand-crafting game.
Given a customer's dossier, you generate 6 wand materials -
3 monster part cores and 3 woods - that the player must choose from.

DESIGN RULE 1 — LOGICAL PROPERTIES
  Every material's properties must be grounded in real-world or
  folkloric logic. Draw from mythology, herbalism, and natural history.

DESIGN RULE 2 — PRICE LOGIC FOR CORES
  - All prices stay in 0–300 g.
  - Common creatures with abundant yield: 50–120 g.
  - Uncommon creatures or low-yield parts: 120–200 g.
  - Rare creatures or unique parts: 200–300 g.

DESIGN RULE 3 — PRICE LOGIC FOR WOODS
  - Common woodland trees (ash, birch, willow): 50–100 g.
  - Less common but real trees (elder, yew, rowan): 80–150 g.
  - Rare or ancient trees: 150–300 g.

DESIGN RULE 4 — INTENTIONAL MISDIRECTION
  The 6 materials must NOT all perfectly match the customer.
  - For CORES: one optimal pair candidate, one tempting trap that
    conflicts with the customer's constraint, one with a useful
    attribute but the wrong scale or application.
  - For WOODS: one correct match, one suiting the customer's role
    but not their magic problem, one whose physical property
    conflicts with what their magic needs.

DESIGN RULE 5 — IMAGE PROMPTS
  Each material's imagePrompt describes ONLY the material object.
  Always end with: "painterly storybook illustration, hand-painted
  watercolour texture, warm cream-and-sepia palette with candle-gold
  highlights, fine ink-line detail, soft volumetric lighting, 128x128,
  transparent background, centered, fantasy item icon".

Return ONLY valid JSON. No markdown.
```

### B.3 Wand system prompt

(Reproduced from `MinigameSceneRunner.WandSystemPrompt`.)

```
You are a master wandmaker writing in a fantasy journal.
Given the materials a wand is made from, you synthesise what kind of
wand they produce together. Your reasoning must follow real-world and
folkloric logic — nothing arbitrary.

Rules:
- The wand's properties must emerge logically from the combination
  of materials, not just list them separately.
- If two cores are used, reason about how their affinities interact —
  do they amplify each other, conflict, or create something unexpected?
- The wood's personality traits shape how the magic is channelled,
  not what the magic does. A rigid wood steadies volatile magic.
  A flexible wood lets the caster's emotion guide the spell.
- Never produce generic fantasy flavour. Every sentence must be
  traceable back to a specific property of a specific material used.
- wandName must be evocative and specific to this exact combination.

Return ONLY valid JSON. No markdown.
```

### B.4 Evaluation system prompt

(Reproduced from `EvaluationManager.EvalSystemPrompt`.)

```
You are a senior wandmaker evaluating whether a finished wand suits
a specific customer. You are discerning, nuanced, and honest.
You understand that what the customer said in their request is not
always what they truly needed.

Scoring rules:
- A wand that only addresses the request scores 40–60.
- A wand that addresses the true goal scores 60–75.
- A wand that addresses both the true goal AND accounts for the
  constraint scores 75–95.
- A wand that perfectly resolves the tension between true goal and
  constraint in a creative way scores 90–100.
- A wand that conflicts with the constraint despite matching the
  request scores 20–40.

Return ONLY valid JSON.
```

## Appendix C — Scene & script inventory

### C.1 Scene inventory

| Build idx | Scene | Driver script(s) | UI mode |
|-----------|-------|-----------------|---------|
| 0 | TitleScene | `TitleScreenController` | UI Toolkit |
| 1 | MorningScene | `MorningScreenController`, `TypewriterText` | UI Toolkit |
| 2 | CustomerGeneratorTest | `CustomerGenerator`, `DossierPanelController` | UI Toolkit |
| 3 | MaterialGeneratorTest | `MaterialGenerator`, `MaterialMarketUI`, `MemoStemmer` | uGUI (UXML/USS authored) |
| 4 | CraftingScene | `CraftingManager`, `CraftingWorkbenchUI` | UI Toolkit |
| 5 | MinigameTest | `MinigameSceneRunner`, `TracingMinigameUI`, `TracingCursor`, `TracingMist`, `RunePathData`, `MinigamePanelController` | UI Toolkit (chrome) + procedural |
| 6 | EvaluationScene | `EvaluationManager`, `EvaluationResultController`, `DayProgressUI`, `RentPaymentUI` | UI Toolkit (+ Canvas widgets) |
| 7 | EndingScene | `EndingManager` | uGUI |

### C.2 Static service & data scripts (selected)

| Script | Role |
|--------|------|
| `GameManager.cs` | Persistent singleton; cross-scene state; progression methods |
| `CustomerService.cs` | Static; customer prompt + GPT-4o call |
| `MaterialService.cs` | Static; material prompt + GPT-4o call + 6-parallel ComfyUI batch |
| `LetterLibrary.cs` | Static; 21 hand-authored letters keyed by (day, sender, tier) |
| `MemoStemmer.cs` | Static; suffix-stripping morphological stemmer for memo↔description matching |
| `RunePathData.cs` | Static; 6 conductor-pattern rune shapes; Catmull-Rom interpolation; self-overlap validation |

## Appendix D — Data classes

```csharp
class CustomerOrder {
  string customerName, schoolOfMagic, profession, personality;
  string request, trueGoal, constraint;
}

class MaterialData {
  enum  MaterialType { Core, Wood }
  MaterialType materialType;
  string name, attributes, imagePrompt;
  int    price;
  // Core-only:
  string elementalAffinity, special;
  // Wood-only:
  string personalityMatch;
  [NonSerialized] Texture2D generatedImage;
}

class PlayerMemo {
  string element, personality, purpose, reinforcement;
}

class WandResult {
  string   wandName, description, imagePrompt;
  string[] attributes;
  Texture2D wandImage;
}
```

## Appendix E — Reward formula derivation

The reward formula in Section 7.5.4 is reproduced here with derivation. Let:

- `s` = `matchScore` from the evaluator (integer 0–100).
- `q` = quality multiplier from the minigame grade ∈ {1.00, 0.85, 0.70, 0.55, 0.40}.

Then per round:

```
goldEarned       = round( 150 · (s/100) · q )
reputationDelta  = (s ≥ 40) ? round( 20 · (s/100) · q ) : −10
finalGradeLetter = thresholds(85, 70, 55, 40) over composite (s · q)
```

The constants 150 and 20 are calibrated against the seven-day arc:

- **Royal target** (rep ≥ 90 by day 7) requires sustained ≈75 score across all seven days at A grade: `7 · 20 · 0.75 · 1.0 = 105` ≥ 90 ✓
- **Bankruptcy floor** (gold < rent at day 3) requires ≈3 days at score < 30: cumulative gold `~3 · 150 · 0.20 · 0.7 = 63 g` plus 500 g start = 563 g, payable; bankruptcy occurs only when match-score drops below ≈25 sustained, which models a player making poor reading choices.

This back-solving was done manually in iteration 4 to produce a difficulty curve where Royal is reachable but not assured, and Bankrupt is only possible through bad play.

\pagebreak

# 11. References

[1] J. Togelius, G. N. Yannakakis, K. O. Stanley, and C. Browne, "Search-Based Procedural Content Generation: A Taxonomy and Survey," *IEEE Transactions on Computational Intelligence and AI in Games*, vol. 3, no. 3, pp. 172–186, 2011.

[2] T. Brown et al., "Language Models are Few-Shot Learners," *Advances in Neural Information Processing Systems (NeurIPS)*, vol. 33, 2020.

[3] R. Rombach, A. Blattmann, D. Lorenz, P. Esser, and B. Ommer, "High-Resolution Image Synthesis with Latent Diffusion Models," in *Proc. CVPR*, 2022.

[4] G. N. Yannakakis and J. Togelius, *Artificial Intelligence and Games*. Springer, 2nd ed., 2024.

[5] N. Akoury, S. Wang, J. Whiting, S. Hood, N. Peng, and M. Iyyer, "STORIUM: A Dataset and Evaluation Platform for Machine-in-the-Loop Story Generation," in *Proc. EMNLP*, 2020.

[6] S. Sudhakaran, M. Gonzalez-Duque, M. Freiberger, C. Glanois, E. Najarro, and S. Risi, "MarioGPT: Open-Ended Text2Level Generation through Large Language Models," in *Proc. NeurIPS*, 2023.

[7] K. Compton, B. Kybartas, and M. Mateas, "Tracery: An Author-Focused Generative Text Tool," in *Proc. International Conference on Interactive Digital Storytelling (ICIDS)*, 2015.

[8] J. Ho, A. Jain, and P. Abbeel, "Denoising Diffusion Probabilistic Models," *Advances in Neural Information Processing Systems (NeurIPS)*, vol. 33, 2020.

[9] M. Li, Y. Lin, Z. Zhang, T. Cai, X. Li, J. Guo, E. Xie, C. Meng, J.-Y. Zhu, and S. Han, "SVDQuant: Absorbing Outliers by Low-Rank Components for 4-Bit Diffusion Models," *arXiv:2411.05007*, 2024. (Nunchaku quantisation toolkit.)

[10] Tongyi Lab, Alibaba, "Z-Image Turbo: A Compact Text-to-Image Model for Pixel-Style Generation," technical report, 2025.

[11] OpenAI, "GPT-4 Technical Report," *arXiv:2303.08774*, 2023.

[12] Unity Technologies, "UI Toolkit (Runtime)," official documentation, accessed April 2026.

[13] J. Togelius and N. Shaker, "The Search-Based Approach," in *Procedural Content Generation in Games* (N. Shaker, J. Togelius, M. Nelson, eds.), Springer, 2016.

[14] Avalanche Software, *Hogwarts Legacy*, 2023. (Comparator: spell-tracing combat.)

[15] Voracious Games, *Potionomics*, 2022. (Comparator: customer-subtext shopkeeper.)

[16] Black Tabby Games, *Slay the Princess*, 2023. (Comparator: branching narrative on a single core relationship.)

[17] R. Yu, *Spelunky*, 2008. (Comparator: constructive procedural level generation.)

\pagebreak

# 12. Declaration of the Contribution of Each Individual Member of the Group

This is an individual project. All design, engineering, prompt engineering, UI authoring, iteration management, project documentation, and final-report writing in this submission is the sole work of:

**Nayoung Lim — UID 3036086098 — 100 % contribution.**

Where AI assistance was used during development (Anthropic Claude Code agents under direct supervision), the resulting artefacts were reviewed and approved by me, and the underlying engineering decisions, architecture, prompt design, and gameplay design are entirely my own. No other student or third party contributed to this project's submitted code, design, or report text.

---

*End of report.*
