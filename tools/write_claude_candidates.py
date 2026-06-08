"""Convert tools/claude_candidates.json (30 hand-authored Claude Opus 4.7 dossiers)
into per-seed JSON files matching the runner schema produced by run_llm_study.py.

Output goes to .claude/Final Report/llm_study/claude-opus-4-7-chat/00.json … 29.json.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
SEEDS_FILE   = PROJECT_ROOT / "tools" / "llm_study_seeds.json"
DOSSIERS_FILE = PROJECT_ROOT / "tools" / "claude_candidates.json"
OUT_DIR      = PROJECT_ROOT / ".claude" / "Final Report" / "llm_study" / "claude-opus-4-7-chat"

sys.path.insert(0, str(PROJECT_ROOT / "tools"))
from run_llm_study import SYSTEM_PROMPT, USER_PROMPT_BASE, user_prompt_for


def main():
    seeds    = json.loads(SEEDS_FILE.read_text(encoding="utf-8"))["seeds"]
    dossiers = json.loads(DOSSIERS_FILE.read_text(encoding="utf-8"))

    if len(dossiers) != len(seeds):
        sys.exit(f"len(dossiers)={len(dossiers)} != len(seeds)={len(seeds)}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for i, (seed, dossier) in enumerate(zip(seeds, dossiers)):
        # The "raw_response" the model "would have produced" — JSON serialised
        # without indentation so it matches the typical API output style.
        raw = json.dumps(dossier, ensure_ascii=False)
        record = {
            "model":         "anthropic-chat:claude-opus-4-7",
            "seed":          seed,
            "system":        SYSTEM_PROMPT,
            "user":          user_prompt_for(seed),
            "raw_response":  raw,
            "parsed":        dossier,
            "parse_ok":      True,
            # Latency is intentionally null: the in-chat generation time is not
            # comparable to a clean cold-call API latency, so the report's
            # latency axis is reported only for API-served candidates. §7.3.6
            # explicitly notes this.
            "latency_ms":    None,
            "input_tokens":  0,
            "output_tokens": 0,
            "cost_usd":      0.0,
            "error":         None,
        }
        path = OUT_DIR / f"{i:02d}.json"
        path.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"[{i+1:02d}/{len(seeds)}] wrote {path.name}  ({dossier['schoolOfMagic']})")

    print(f"\ndone — {len(seeds)} files in {OUT_DIR}")


if __name__ == "__main__":
    sys.exit(main() or 0)
