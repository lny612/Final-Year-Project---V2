"""Read every candidate JSON under .claude/Final Report/llm_study/<model>/ and
emit a single tools/judging_worklist.txt for interactive scoring by the chat
session. Skips records whose `error` is non-null (those represent API failures
and contribute to the parse-failure / wellformedness axis but have no content
to score on the other axes).

Excluded models (per user instruction): `gemini-pro` (free-tier quota errors).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
STUDY_DIR    = PROJECT_ROOT / ".claude" / "Final Report" / "llm_study"
WORKLIST     = PROJECT_ROOT / "tools" / "judging_worklist.txt"

EXCLUDED_MODELS = {"gemini-pro"}


def main():
    blocks: list[str] = []
    blocks.append("=" * 78)
    blocks.append("CANDIDATE DOSSIER WORKLIST FOR INTERACTIVE LLM-AS-JUDGE SCORING")
    blocks.append("=" * 78)
    blocks.append("")
    blocks.append("Rubric (each output scored on five axes):")
    blocks.append("  coherence    [0|1|2]   constraint logically EXPLAINS request")
    blocks.append("  misdirection [0|1|2]   trueGoal differs from request meaningfully")
    blocks.append("  tone         [0|1|2]   reads as cosy fantasy authored prose")
    blocks.append("  playability  [0|1|2]   could a player extract a 4-slot memo in 30-60s")
    blocks.append("  wellformed   [0|1]     parsed cleanly with all 7 required fields")
    blocks.append("")
    blocks.append("API-error rows (error != null in the source JSON) are listed at the")
    blocks.append("end with a single 'API_ERROR' marker; they get wellformed=0 and ")
    blocks.append("0 on every other axis.")
    blocks.append("")
    blocks.append("=" * 78)

    error_rows: list[str] = []
    content_rows: list[str] = []

    for model_dir in sorted(STUDY_DIR.iterdir()):
        if not model_dir.is_dir() or model_dir.name.startswith(".") or model_dir.name in EXCLUDED_MODELS:
            continue
        for fp in sorted(model_dir.glob("*.json")):
            try:
                rec = json.loads(fp.read_text(encoding="utf-8"))
            except Exception as exc:
                error_rows.append(f"{model_dir.name}/{fp.stem}  read-fail: {exc}")
                continue

            if rec.get("error"):
                error_rows.append(f"{model_dir.name}/{fp.stem}  API_ERROR")
                continue

            parsed = rec.get("parsed") or {}
            raw = rec.get("raw_response") or "<empty>"
            block = []
            block.append("")
            block.append("-" * 78)
            block.append(f"MODEL : {model_dir.name}")
            block.append(f"SEED  : {rec.get('seed', '?')}")
            block.append(f"FILE  : {model_dir.name}/{fp.name}")
            block.append(f"PARSE_OK : {rec.get('parse_ok', False)}")
            block.append("")
            if isinstance(parsed, dict) and parsed:
                for key in ("customerName", "schoolOfMagic", "profession",
                            "personality", "request", "trueGoal", "constraint"):
                    if key in parsed:
                        block.append(f"  {key} :")
                        block.append(f"    {parsed[key]}")
            else:
                block.append("  PARSED EMPTY — RAW RESPONSE FOLLOWS")
                block.append("  " + raw[:1500])
            content_rows.append("\n".join(block))

    blocks.append("")
    blocks.append(f"## Content rows ({len(content_rows)})")
    blocks.extend(content_rows)
    blocks.append("")
    blocks.append("=" * 78)
    blocks.append(f"## API-error rows ({len(error_rows)}) — auto-score: wellformed=0, all axes=0")
    blocks.append("=" * 78)
    blocks.extend(error_rows)

    WORKLIST.write_text("\n".join(blocks), encoding="utf-8")
    print(f"wrote {WORKLIST}  ({len(content_rows)} content rows, {len(error_rows)} error rows)")


if __name__ == "__main__":
    sys.exit(main() or 0)
