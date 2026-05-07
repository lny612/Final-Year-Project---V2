"""Run the customer-dossier prompt against six candidate LLMs and store the
outputs as JSON for later scoring.

Models supported (skipped automatically if credentials missing):

    openai:gpt-4o
    openai:gpt-4o-mini
    anthropic:claude-sonnet-4-5     (latest Sonnet at the time of run)
    anthropic:claude-haiku-4-5
    google:gemini-2.5-flash
    google:gemini-2.5-pro
    ollama:llama3.1:8b-instruct-q4_K_M
    ollama:mistral:7b-instruct

The prompt itself is lifted verbatim from `Assets/Scripts/CustomerService.cs`
so every model sees the exact same prompt the in-game customer pipeline uses.
The only nonce we add is a one-line "Generation seed: <id>" appended to the
user prompt, so 30 calls of the same model produce 30 different customers.

Output layout:
    .claude/Final Report/llm_study/
        gpt-4o/
            00.json
            01.json
            ...
        claude-sonnet-4-5/
            ...

Each .json file contains:
    {
      "model":       "openai:gpt-4o",
      "seed":        "Run 01 / silver-fern",
      "system":      "...",
      "user":        "...",
      "raw_response":"... (model's text)",
      "parsed":      {... or null if JSON parse failed},
      "parse_ok":    bool,
      "latency_ms":  int,
      "input_tokens":  int,
      "output_tokens": int,
      "cost_usd":    float
    }

Usage (from the project root):
    py tools/run_llm_study.py --models all          # everything you have keys for
    py tools/run_llm_study.py --models gpt-4o,gpt-4o-mini
    py tools/run_llm_study.py --dry-run             # estimate cost, do not call

Cost estimate is printed before any calls. The script asks for confirmation
unless --yes is passed.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
PROJECT_ROOT = Path(__file__).resolve().parents[1]
STUDY_DIR    = PROJECT_ROOT / ".claude" / "Final Report" / "llm_study"
SEEDS_FILE   = PROJECT_ROOT / "tools" / "llm_study_seeds.json"
ENV_FILE     = STUDY_DIR / ".env"

# ---------------------------------------------------------------------------
# Prompt — must stay byte-for-byte identical to CustomerService.cs
# ---------------------------------------------------------------------------
SYSTEM_PROMPT = """You are a character designer for a fantasy wand-crafting game.
Your job is to generate customer orders for a wand shop.

Each customer is a unique fantasy character who needs a custom wand.
Every field you write must follow a strict internal logic chain:

  schoolOfMagic -> profession -> request -> trueGoal -> constraint

Rules for each field:

customerName
  A fantasy name that feels fitting for this character.

schoolOfMagic
  The element or discipline this person uses.
  Examples: Storm magic, Hydromancy, Shadow magic, Pyromancy, Necromancy.

profession
  Their specific job. It must logically match their school of magic.
  Do not write a generic job title. Write HOW they use their magic in their work.
  Examples:
    - Storm magic -> "Assassin mage who uses lightning for the final blow"
    - Hydromancy  -> "Field medic who uses water magic to perform emergency healing"
    - Shadow magic -> "Bounty hunter who uses shadow magic to track and corner targets"

personality
  Exactly 3 traits. At least one must be in tension with the others.
  Format: "Trait, trait, trait"
  Example: "Outwardly calm, internally panicked, fiercely protective"

request
  What they say out loud in the shop. Written in first person, conversational tone.
  It must describe a specific practical problem they want the wand to solve.
  The problem must be a logical drawback of their school of magic used in their profession.
  Do NOT make this a generic "I want a powerful wand" request.
  Example: "I need a wand that lets me control the exact pressure of my water flow.
            Too much force and I damage the wound instead of closing it."

trueGoal
  What they actually want to achieve - more specific and ambitious than the request.
  Must include the profession's core demand (speed, precision, range, etc.)
  and show why those demands are in slight tension with each other.
  Example: "To perform precise, high-speed healing on multiple patients
            in rapid succession without losing control of the flow"

constraint
  Their magical limitation. It must directly explain WHY the problem in the request happens.
  The cruelest constraints are ones that activate at the worst possible moment given their job.
  Example: "Her water magic amplifies in intensity when she is emotionally distressed -
            exactly when she needs it most delicate"

Return ONLY valid JSON. No markdown. No explanation. No extra text.
Use exactly this structure:
{
  "customerName": string,
  "schoolOfMagic": string,
  "profession": string,
  "personality": string,
  "request": string,
  "trueGoal": string,
  "constraint": string
}"""

USER_PROMPT_BASE = """Generate a new customer. Here are three examples of the correct style.
Study the logical chain in each one before generating a new character.
The new character must use a different school of magic from all three examples.

EXAMPLE 1:
{
  "customerName": "Elyra Voss",
  "schoolOfMagic": "Storm magic",
  "profession": "Assassin mage who uses lightning for the final blow",
  "personality": "Meticulous, deeply self-doubting, quietly competitive",
  "request": "I need something discreet for my job. I want the wand to prevent flashing so I can conjure my magic without visible sign.",
  "trueGoal": "To perform a high-stakes lightning spell quickly at the exact right moment without it being too visible",
  "constraint": "Her magic surges unpredictably when she feels watched"
}

EXAMPLE 2:
{
  "customerName": "Dorian Ashveil",
  "schoolOfMagic": "Shadow magic",
  "profession": "Bounty hunter who uses shadow magic to track and corner targets",
  "personality": "Calculating, emotionally detached, secretly paranoid",
  "request": "I need a wand that keeps my shadow spells from dispersing mid-chase. My bindings keep collapsing the moment my target starts running.",
  "trueGoal": "To cast shadow binding spells reliably at full sprint without losing hold of the spell",
  "constraint": "His magic destabilises when his concentration splits between moving and casting simultaneously"
}

EXAMPLE 3:
{
  "customerName": "Sable Mirehn",
  "schoolOfMagic": "Hydromancy",
  "profession": "Field medic who uses water magic to perform emergency healing",
  "personality": "Outwardly calm, internally panicked, fiercely protective",
  "request": "I need a wand that lets me control the exact pressure of my water flow. Too much force and I damage the wound instead of closing it.",
  "trueGoal": "To perform precise, high-speed healing on multiple patients in rapid succession without losing control of the flow",
  "constraint": "Her water magic amplifies in intensity when she is emotionally distressed - exactly when she needs it most delicate"
}

Now generate one new customer following the same logical chain.
Return ONLY the JSON object."""


def user_prompt_for(seed: str) -> str:
    return f"{USER_PROMPT_BASE}\n\nGeneration seed: {seed}"


# ---------------------------------------------------------------------------
# Model registry
# ---------------------------------------------------------------------------
@dataclass
class Model:
    short_name: str
    vendor: str
    api_id: str
    input_per_1k_usd: float
    output_per_1k_usd: float
    invoke: Callable[["Model", str, str], dict] | None = field(default=None)
    needs_env: list[str] = field(default_factory=list)

    @property
    def folder_name(self) -> str:
        return self.short_name.replace(":", "_").replace("/", "_")


# Pricing — USD per 1K tokens. Approximate at time of writing; the runner
# stores per-call token counts so the user can recompute exact costs.
MODELS: list[Model] = []


def _register(short, vendor, api_id, in_p, out_p, invoker, env):
    MODELS.append(Model(short, vendor, api_id, in_p, out_p, invoker, env))


# ---- OpenAI ---------------------------------------------------------------
def _openai_invoke(model: Model, system: str, user: str) -> dict:
    from openai import OpenAI
    client = OpenAI(api_key=os.environ["OPENAI_API_KEY"])
    t0 = time.perf_counter()
    resp = client.chat.completions.create(
        model=model.api_id,
        messages=[{"role": "system", "content": system}, {"role": "user", "content": user}],
        temperature=1.0,
    )
    elapsed = (time.perf_counter() - t0) * 1000
    return {
        "raw_response": resp.choices[0].message.content,
        "latency_ms":   int(elapsed),
        "input_tokens": resp.usage.prompt_tokens,
        "output_tokens": resp.usage.completion_tokens,
    }


# ---- Anthropic ------------------------------------------------------------
def _anthropic_invoke(model: Model, system: str, user: str) -> dict:
    import anthropic
    client = anthropic.Anthropic(api_key=os.environ["ANTHROPIC_API_KEY"])
    t0 = time.perf_counter()
    resp = client.messages.create(
        model=model.api_id,
        max_tokens=1024,
        system=system,
        messages=[{"role": "user", "content": user}],
    )
    elapsed = (time.perf_counter() - t0) * 1000
    text = "".join(b.text for b in resp.content if hasattr(b, "text"))
    return {
        "raw_response": text,
        "latency_ms":   int(elapsed),
        "input_tokens": resp.usage.input_tokens,
        "output_tokens": resp.usage.output_tokens,
    }


# ---- Google Gemini --------------------------------------------------------
def _gemini_invoke(model: Model, system: str, user: str) -> dict:
    from google import genai
    from google.genai import types
    client = genai.Client(api_key=os.environ["GOOGLE_API_KEY"])
    t0 = time.perf_counter()
    resp = client.models.generate_content(
        model=model.api_id,
        contents=user,
        config=types.GenerateContentConfig(system_instruction=system, temperature=1.0),
    )
    elapsed = (time.perf_counter() - t0) * 1000
    usage = resp.usage_metadata
    return {
        "raw_response": resp.text,
        "latency_ms":   int(elapsed),
        "input_tokens": usage.prompt_token_count if usage else 0,
        "output_tokens": (usage.candidates_token_count or 0) if usage else 0,
    }


# ---- Ollama (local) -------------------------------------------------------
def _ollama_invoke(model: Model, system: str, user: str) -> dict:
    import requests
    host = os.environ.get("OLLAMA_HOST", "http://localhost:11434").rstrip("/")
    t0 = time.perf_counter()
    r = requests.post(
        f"{host}/api/chat",
        json={
            "model": model.api_id,
            "stream": False,
            "messages": [
                {"role": "system", "content": system},
                {"role": "user",   "content": user},
            ],
            "options": {"temperature": 1.0},
        },
        timeout=180,
    )
    r.raise_for_status()
    elapsed = (time.perf_counter() - t0) * 1000
    data = r.json()
    return {
        "raw_response": data["message"]["content"],
        "latency_ms":   int(elapsed),
        "input_tokens": data.get("prompt_eval_count", 0),
        "output_tokens": data.get("eval_count", 0),
    }


_register("gpt-4o",          "openai",    "gpt-4o",                   0.0025,   0.01,    _openai_invoke,    ["OPENAI_API_KEY"])
_register("gpt-4o-mini",     "openai",    "gpt-4o-mini",              0.00015,  0.0006,  _openai_invoke,    ["OPENAI_API_KEY"])
_register("claude-sonnet",   "anthropic", "claude-sonnet-4-5",        0.003,    0.015,   _anthropic_invoke, ["ANTHROPIC_API_KEY"])
_register("claude-haiku",    "anthropic", "claude-haiku-4-5-20251001",0.001,    0.005,   _anthropic_invoke, ["ANTHROPIC_API_KEY"])
_register("gemini-flash",    "google",    "gemini-2.5-flash",         0.000075, 0.0003,  _gemini_invoke,    ["GOOGLE_API_KEY"])
_register("gemini-pro",      "google",    "gemini-2.5-pro",           0.00125,  0.005,   _gemini_invoke,    ["GOOGLE_API_KEY"])
_register("llama3-8b",       "ollama",    "llama3.1:8b-instruct-q4_K_M", 0.0,    0.0,    _ollama_invoke,    [])
_register("mistral-7b",      "ollama",    "mistral:7b-instruct",      0.0,      0.0,     _ollama_invoke,    [])


# ---------------------------------------------------------------------------
def load_env():
    """Minimal .env loader (no python-dotenv dependency).

    Prefers `.env`; falls back to `.env.template` if `.env` is absent (so that
    a user who edited the template directly is not silently locked out).
    Values from the file *override* anything already set in the OS env, so a
    stale system-wide `OPENAI_API_KEY` cannot win over the file's value.
    """
    target = ENV_FILE if ENV_FILE.exists() else ENV_FILE.with_name(".env.template")
    if not target.exists():
        print(f"[env] no {ENV_FILE.name} or .env.template at {STUDY_DIR}; nothing to load.")
        return
    if target.name == ".env.template":
        print(f"[env] `.env` not found, falling back to `.env.template`. "
              f"Recommend: copy `.env.template` to `.env` once it is correct.")
    loaded = []
    for line in target.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        k, v = k.strip(), v.strip()
        if v:
            os.environ[k] = v       # OVERRIDE inherited system env
            loaded.append(k)
    if loaded:
        print(f"[env] loaded from {target.name}: {', '.join(loaded)}")


def strip_code_fences(text: str) -> str:
    text = text.strip()
    if not text.startswith("```"):
        return text
    parts = text.split("\n", 1)
    if len(parts) < 2:
        return text
    body = parts[1]
    if body.rstrip().endswith("```"):
        body = body.rstrip()[: -3]
    return body.strip()


def try_parse_json(text: str):
    cleaned = strip_code_fences(text)
    try:
        return json.loads(cleaned), True
    except Exception:
        m = re.search(r"\{.*\}", cleaned, re.S)
        if m:
            try:
                return json.loads(m.group(0)), True
            except Exception:
                pass
    return None, False


def estimate_total_cost(models: list[Model], per_model: int) -> float:
    # Customer prompt is ~700 input tokens, output ~200 tokens (rough average).
    in_t, out_t = 700, 200
    return sum(per_model * (in_t / 1000 * m.input_per_1k_usd + out_t / 1000 * m.output_per_1k_usd)
               for m in models)


def run_one(m: Model, seed: str) -> dict:
    record = {
        "model":      f"{m.vendor}:{m.short_name}",
        "seed":       seed,
        "system":     SYSTEM_PROMPT,
        "user":       user_prompt_for(seed),
        "raw_response": None,
        "parsed":     None,
        "parse_ok":   False,
        "latency_ms": 0,
        "input_tokens":  0,
        "output_tokens": 0,
        "cost_usd":   0.0,
        "error":      None,
    }
    try:
        result = m.invoke(m, SYSTEM_PROMPT, user_prompt_for(seed))
        record.update(result)
        parsed, ok = try_parse_json(result["raw_response"])
        record["parsed"] = parsed
        record["parse_ok"] = ok
        record["cost_usd"] = round(
            result["input_tokens"] / 1000 * m.input_per_1k_usd
            + result["output_tokens"] / 1000 * m.output_per_1k_usd,
            6,
        )
    except Exception as exc:
        record["error"] = f"{type(exc).__name__}: {exc}"
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--models", default="all",
                        help="comma-separated list of model short names, or 'all' (default)")
    parser.add_argument("--per-model", type=int, default=30, help="generations per model (default 30)")
    parser.add_argument("--dry-run", action="store_true", help="print plan and cost estimate, then exit")
    parser.add_argument("--yes", action="store_true", help="skip confirmation prompt")
    args = parser.parse_args()

    load_env()

    if args.models == "all":
        chosen = list(MODELS)
    else:
        wanted = {s.strip() for s in args.models.split(",") if s.strip()}
        chosen = [m for m in MODELS if m.short_name in wanted]

    seeds = json.loads(SEEDS_FILE.read_text(encoding="utf-8"))["seeds"][: args.per_model]

    skip = []
    runnable = []
    for m in chosen:
        missing = [k for k in m.needs_env if not os.environ.get(k)]
        if missing:
            skip.append((m, missing))
        else:
            runnable.append(m)

    print("=" * 60)
    print("LLM-study plan")
    print("=" * 60)
    for m in runnable:
        print(f"  RUN  {m.short_name:18s}  ({m.vendor:9s}, {len(seeds)} calls)")
    for m, miss in skip:
        print(f"  skip {m.short_name:18s}  (missing: {', '.join(miss)})")
    cost = estimate_total_cost(runnable, len(seeds))
    print(f"\nEstimated cost: ${cost:.2f}  (rough — actual cost is per-call summed in results.csv)")

    if args.dry_run:
        print("\n--dry-run — exiting without calls.")
        return

    if not runnable:
        print("\nnothing to run; add API keys to `.env` and retry.")
        return

    if not args.yes:
        ans = input("\nProceed? [y/N] ").strip().lower()
        if ans not in {"y", "yes"}:
            print("aborted.")
            return

    for m in runnable:
        out_dir = STUDY_DIR / m.folder_name
        out_dir.mkdir(parents=True, exist_ok=True)
        print(f"\n=== {m.short_name} ({m.vendor}) ===")
        for i, seed in enumerate(seeds):
            t0 = time.perf_counter()
            rec = run_one(m, seed)
            (out_dir / f"{i:02d}.json").write_text(
                json.dumps(rec, ensure_ascii=False, indent=2),
                encoding="utf-8",
            )
            wall = (time.perf_counter() - t0) * 1000
            status = "ok" if rec["parse_ok"] else ("ERR" if rec["error"] else "BAD-JSON")
            print(f"  [{i+1:02d}/{len(seeds)}] {status:8s}  {wall:6.0f} ms  {rec['cost_usd']:.5f} USD  {seed}")

    print("\ndone. raw outputs in:", STUDY_DIR)


if __name__ == "__main__":
    sys.exit(main() or 0)
