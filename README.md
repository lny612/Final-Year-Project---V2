# Final Year Project - V2

A cozy fantasy shop game where you read between the lines of quirky AI-generated customers' requests, hunt for the perfect magical materials, and trace rune patterns under pressure — then watch as your crafted wand is conjured into a unique pixel art illustration and scored against what the customer truly wanted.

## Architecture

```mermaid
flowchart TB
    subgraph loop["Game Loop (repeats each round)"]
        direction TB

        S1["<b>Scene 1: Customer Order</b><br/>Generate fantasy customer<br/>with hidden true goal"]
        S2["<b>Scene 2: Material Market</b><br/>Browse & buy cores + woods<br/>(6 AI-generated materials)"]
        S3["<b>Scene 3: Crafting</b>"]
        S4["<b>Scene 4: Evaluation</b><br/>Score wand vs. customer need<br/>Award gold & reputation"]

        S1 -- "CustomerOrder" --> S2
        S2 -- "Inventory<br/>(purchased materials)" --> S3
        S3 -- "WandResult + Quality Grade" --> S4
        S4 -- "Next Round<br/>(gold & rep persist)" --> S1
    end

    subgraph craft["Scene 3 — Parallel Pipelines"]
        direction LR
        PA["<b>API Pipeline</b><br/>Generate wand description<br/>+ pixel art image"]
        PB["<b>Tracing Minigame</b><br/>3 rounds of rune tracing<br/>(red/blue fill race + key gates)"]
        SYNC(("Both<br/>done?"))
        PA --> SYNC
        PB --> SYNC
        SYNC --> RESULT["Show Result<br/>Grade: A / B / C / F"]
    end

    S3 -.-> craft

    subgraph gm["GameManager (persistent singleton)"]
        STATE["currentCustomer | inventory | playerGold<br/>currentWandResult | craftingQualityGrade | playerReputation"]
    end

    GPT["<b>OpenAI GPT-4o</b>"]
    COMFY["<b>ComfyUI</b><br/>Local Nunchaku INT4<br/>Diffusion Server"]

    GPT -- "#1 Customer profile<br/>(7-field logical chain)" --> S1
    GPT -- "#2 Six materials<br/>(with misdirection)" --> S2
    GPT -- "#3 Wand synthesis<br/>(name, desc, attributes)" --> PA
    GPT -- "#4 Match score 0-100<br/>(verdict & feedback)" --> S4

    COMFY -- "6 material<br/>pixel art images" --> S2
    COMFY -- "Wand<br/>pixel art image" --> PA

    gm ~~~ loop

    style GPT fill:#10a37f,color:#fff,stroke:#0d8c6d
    style COMFY fill:#7c3aed,color:#fff,stroke:#6d28d9
    style SYNC fill:#f59e0b,color:#000,stroke:#d97706
    style RESULT fill:#3b82f6,color:#fff,stroke:#2563eb
    style STATE fill:#1e293b,color:#fff,stroke:#334155
```

### Per-Round AI Calls

| # | Scene | Service | Purpose |
|---|-------|---------|---------|
| 1 | Customer Order | GPT-4o | Generate customer with hidden true goal & constraint |
| 2 | Material Market | GPT-4o | Generate 6 materials (3 cores + 3 woods) with misdirection |
| 3 | Material Market | ComfyUI | Generate 6 pixel art material images (parallel) |
| 4 | Crafting | GPT-4o | Synthesize wand from selected materials |
| 5 | Crafting | ComfyUI | Generate wand pixel art |
| 6 | Evaluation | GPT-4o | Score wand match (0-100) with detailed feedback |

## Tech Stack

- **Engine:** Unity 6 (6000.0.58f2) with URP
- **Language AI:** OpenAI GPT-4o (chat completions, JSON mode)
- **Image AI:** ComfyUI with Nunchaku INT4-quantized Z-Image Turbo diffusion model
- **JSON:** Newtonsoft.Json (`JObject` / `JArray`)
- **UI Text:** TextMeshPro

## Setup

1. Open project in Unity Hub with Unity 6000.0.58f2
2. Place your OpenAI API key in `Assets/StreamingAssets/config.json`:
   ```json
   {"openAIApiKey": "sk-proj-YOUR_KEY_HERE"}
   ```
3. Run [ComfyUI](https://github.com/comfyanonymous/ComfyUI) locally on port 8000 with the Nunchaku INT4 Z-Image Turbo model
4. Press Play in the `CustomerGeneratorTest` scene
