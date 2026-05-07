# LLM Study — Run Instructions

This document tells you (Nayoung) exactly what to do to produce the multi-LLM
comparison data the report's new §7.3.6 needs. It should take **~30 minutes
of human time** and **~$2 of API spend**.

You can skip any model whose API key you do not have — the runner just logs a
warning and moves on. The minimum useful run is **GPT-4o + GPT-4o-mini + one
local Ollama model**, which is free past the OpenAI cost.

---

## Step 1 — Install Python deps

From the project root, in the venv at `C:\.venv` (or any venv you prefer):

```powershell
C:\.venv\Scripts\Activate.ps1
pip install openai anthropic google-genai requests
```

Total ~30 MB. If `google-genai` complains, `pip install --upgrade google-genai`.

---

## Step 2 — (Optional) Install Ollama for the local-model rows

Skip this step if you only want to compare hosted models. You will lose two
rows of the comparison (Llama-3-8B and Mistral-7B), but the report's main
claim — *commercial models reach much higher prompt-fidelity than open-weight
ones at this size* — is already supported by GPT-4o-mini vs the remaining
local model.

```powershell
# Download installer from https://ollama.com/download/windows and run it.
# Then pull the two models we benchmark:
ollama pull llama3.1:8b-instruct-q4_K_M
ollama pull mistral:7b-instruct
# Make sure the server is running (it usually starts as a Windows service):
ollama serve   # leave this terminal open during the study
```

Each model is ~5 GB on disk and uses ~6 GB VRAM. They will run on the same
RTX 4070 Laptop the game uses, but **stop ComfyUI first** — both compete for
the same 8 GB.

---

## Step 3 — Drop your API keys into `.env`

```powershell
cd ".claude\Final Report\llm_study"
copy .env.template .env
notepad .env
```

Fill in only the keys you actually have. A blank line means "skip that
vendor". The OpenAI key from `Assets/StreamingAssets/config.json` is the same
one you need here — paste it into `OPENAI_API_KEY=`.

---

## Step 4 — Dry-run to confirm cost estimate

```powershell
cd "C:\Unity Projects\Final Year Project - V2"
py tools\run_llm_study.py --dry-run
```

You will see a plan like:

```
============================================================
LLM-study plan
============================================================
  RUN  gpt-4o              (openai,    30 calls)
  RUN  gpt-4o-mini         (openai,    30 calls)
  RUN  claude-sonnet       (anthropic, 30 calls)
  RUN  claude-haiku        (anthropic, 30 calls)
  skip gemini-flash        (missing: GOOGLE_API_KEY)
  skip gemini-pro          (missing: GOOGLE_API_KEY)
  RUN  llama3-8b           (ollama,    30 calls)
  RUN  mistral-7b          (ollama,    30 calls)

Estimated cost: $1.93
```

If the cost looks right, proceed.

---

## Step 5 — Run the study

```powershell
py tools\run_llm_study.py --models all
```

Hit `y` to confirm. Each cloud model takes ~2 s × 30 = ~1 min. Each Ollama
model takes ~6 s × 30 = ~3 min on warm GPU. **Total wall time ≈ 15–25 min.**

You will see one line per call:

```
=== gpt-4o (openai) ===
  [01/30] ok        2143 ms  0.00382 USD  Run 01 / silver-fern
  [02/30] ok        1987 ms  0.00374 USD  Run 02 / amber-finch
  ...
```

`ok` = clean JSON parse. `BAD-JSON` = model returned non-JSON text (counts
against the rubric). `ERR` = network or auth error (re-check the key).

---

## Step 6 — Confirm the outputs landed

```powershell
dir ".claude\Final Report\llm_study"
# expect one folder per model, each containing 30 .json files (00.json … 29.json)
```

That's it. Tell Claude "the LLM study is done" and it will run the scorer,
build the figures, write §7.3.6, and weave the results into the docx.

---

## Troubleshooting

- **"no API key found"** — the runner couldn't read `.env`. Confirm the file
  is at `.claude\Final Report\llm_study\.env` (not `.env.template`) and has
  no quotes around the values: `OPENAI_API_KEY=sk-...` not `="sk-..."`.
- **Anthropic returns 404** — Sonnet/Haiku model IDs change occasionally. The
  runner currently targets `claude-sonnet-4-5` and `claude-haiku-4-5-20251001`.
  Edit `tools/run_llm_study.py` if those names need updating; the CLI prints
  the exact API ID it's calling.
- **Ollama times out** — first call after a cold start triggers VRAM load
  (~30 s). Subsequent calls are 2–6 s. Increase the `timeout=180` in
  `_ollama_invoke` if needed.
- **Gemini returns "RESOURCE_EXHAUSTED"** — you hit the free-tier rate cap.
  Wait 60 s and re-run with `--models gemini-flash,gemini-pro` to fill in
  just the missing rows.
