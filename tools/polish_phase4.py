"""Phase 4 — proofread polish: fix cross-reference numbering bugs, trim the
giant abstract paragraph, tighten the long §9.3 LLM-replacement bullet, and
clean up small awkward phrasings.

Backup written to Final_Report.docx.bak5.
"""
from __future__ import annotations

import re
import shutil
import sys
from pathlib import Path

from docx import Document
from docx.oxml.ns import qn
from docx.shared import Pt

PROJECT_ROOT = Path(__file__).resolve().parents[1]
DOCX_PATH    = PROJECT_ROOT / ".claude" / "Final Report" / "Final_Report.docx"
BACKUP_PATH  = PROJECT_ROOT / ".claude" / "Final Report" / "Final_Report.docx.bak5"

BODY_FONT = "Times New Roman"


def set_run_font(run, *, bold=False, italic=False, size=None):
    run.font.name = BODY_FONT
    rPr = run._r.get_or_add_rPr()
    rFonts = rPr.find(qn('w:rFonts'))
    if rFonts is None:
        rFonts = rPr.makeelement(qn('w:rFonts'), {})
        rPr.append(rFonts)
    for attr in ('w:ascii', 'w:hAnsi', 'w:cs', 'w:eastAsia'):
        rFonts.set(qn(attr), BODY_FONT)
    if bold:   run.bold = True
    if italic: run.italic = True
    if size:   run.font.size = size


def add_run(p, text, **kw):
    r = p.add_run(text)
    set_run_font(r, **kw)
    return r


def find_para(doc, prefix):
    for i, p in enumerate(doc.paragraphs):
        if p.text.startswith(prefix):
            return i, p
    raise LookupError(f"no para starts with: {prefix!r}")


def replace_runs(p, runs):
    for r in list(p.runs):
        r._r.getparent().remove(r._r)
    for text, kw in runs:
        add_run(p, text, **kw)


def replace_text_in_runs(p, replacements: dict[str, str]):
    """Edit run text in place when run.text is short enough to find a target.
    Falls back to a full-paragraph rebuild if any target is split across runs."""
    full = "".join(r.text for r in p.runs)
    new_full = full
    for old, new in replacements.items():
        new_full = new_full.replace(old, new)
    if new_full == full:
        return False
    # If the paragraph is single-run, just rewrite that run's text.
    if len(p.runs) == 1:
        p.runs[0].text = new_full
        return True
    # Otherwise, try to do a per-run replacement (works when target lives inside
    # one run). If we can't, fall back to a single-run rebuild that preserves
    # font but loses internal italics/bolds.
    done = False
    for old, new in replacements.items():
        for r in p.runs:
            if old in r.text:
                r.text = r.text.replace(old, new)
                done = True
    if done:
        return True
    # Fallback rebuild
    replace_runs(p, [(new_full, {})])
    return True


# ---------------------------------------------------------------------------
def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not DOCX_PATH.exists():
        sys.exit(f"missing docx: {DOCX_PATH}")
    print(f"backup -> {BACKUP_PATH}")
    shutil.copy2(DOCX_PATH, BACKUP_PATH)

    doc = Document(str(DOCX_PATH))
    print(f"opened {DOCX_PATH} ({len(doc.paragraphs)} paragraphs)")

    # -----------------------------------------------------------------------
    # 1. Fix cross-reference numbering bugs.
    #    Headings are §3.1–3.4, §6.x, §7.x, §8.x, §9.x. Body refs to §5.3 / §5.4
    #    are leftovers; "Section 4" declaration ref should be "Section 2".
    # -----------------------------------------------------------------------
    fixes = {
        "§5.3": "§3.3",
        "§5.4": "§3.4",
        "Section 5.3": "Section 3.3",
        "Section 5.4": "Section 3.4",
        "Section 4 declares": "Section 2 declares",
    }
    n_fixed = 0
    for i, p in enumerate(doc.paragraphs):
        if any(k in p.text for k in fixes):
            full = "".join(r.text for r in p.runs)
            new = full
            for k, v in fixes.items():
                new = new.replace(k, v)
            if new != full:
                # Per-run replacement when possible
                for k, v in fixes.items():
                    for r in p.runs:
                        if k in r.text:
                            r.text = r.text.replace(k, v)
                # Verify
                rebuilt = "".join(r.text for r in p.runs)
                if rebuilt != new:
                    # Couldn't replace cleanly — full paragraph rebuild
                    replace_runs(p, [(new, {})])
                n_fixed += 1
                print(f"  [refs] fixed para {i}")
    print(f"  [refs] total fixed: {n_fixed}")

    # -----------------------------------------------------------------------
    # 2. Trim the abstract — split the dense paragraph into a 3-sentence lead
    # plus a 2-sentence headline-contributions block.
    # -----------------------------------------------------------------------
    i, p = find_para(doc, "The Wand Atelier is a single-player cosy crafting game")
    print(f"  [abstract] tightening para {i}")
    replace_runs(p, [
        ("The Wand Atelier", {"bold": True}),
        (" is a single-player cosy crafting game in Unity 6 that integrates two "
         "generative-AI services — OpenAI GPT-4o for narrative content and a "
         "locally hosted ComfyUI server running an SVDQuant-quantised Z-Image "
         "Turbo diffusion model for runtime stylized illustration — into a "
         "tightly authored seven-day session loop. Each day the player reads "
         "an AI-generated customer dossier with a hidden tension between what "
         "they say and what they need, distils it into a four-slot memo through "
         "a drag-to-highlight reading mechanic, hunts among six AI-generated "
         "materials laced with intentional misdirection, performs a three-round "
         "tracing minigame whose escalation introduces three distinct gate "
         "types (Tap, Hold, Accent), receives a unique stylized wand "
         "illustration, and is graded against the customer's true goal.", {}),
    ])
    # Insert a second abstract paragraph immediately after.
    target = doc.paragraphs[i + 1]  # Section 2 heading
    new = target.insert_paragraph_before("", style="Body Text")
    add_run(new, "Headline contributions. ", bold=True)
    add_run(new,
            "A parallel pre-generation cascade hides ≈25 s of cumulative "
            "AI latency per day behind scene transitions and player input, so "
            "all four GPT-4o calls and seven diffusion image jobs are "
            "perceptually free; a logical-chain prompt design forces dossiers "
            "and material-sets into semantically interlocked structures that "
            "the evaluator prompt can score on subtext rather than surface "
            "match; and a 30-seed × 6-candidate LLM-comparison study confirms "
            "GPT-4o as the operationally-correct production model. The full "
            "seven-day loop, five branching endings, and full gameplay-AI "
            "integration ship and run reproducibly on a consumer 8 GB laptop "
            "GPU.")

    # -----------------------------------------------------------------------
    # 3. Tighten the long §9.3 stronger-content LLM bullet.
    # -----------------------------------------------------------------------
    i, p = find_para(doc, "Stronger-content or vendor-neutral LLM.")
    print(f"  [§9.3] tightening LLM-replacement bullet {i}")
    replace_runs(p, [
        ("Stronger-content or vendor-neutral LLM. ", {"bold": True}),
        ("§7.3.6 identifies Claude Opus 4.7 as the highest-content-quality "
         "cloud option (higher per-call cost) and Llama 3 8B / Mistral 7B as "
         "the only $0-marginal options (with a coherence regression). "
         "Migration would slot the candidate into production, re-run the "
         "five-axis rubric on a fresh 30-seed corpus, and add an external "
         "blind judge pass to convert the §7.3.6 single-pass score into a "
         "calibrated inter-rater number. Estimated effort: 2 weeks.", {}),
    ])

    # -----------------------------------------------------------------------
    # 4. §8.5 player-consent bullet — minor trim.
    # -----------------------------------------------------------------------
    i, p = find_para(doc, "Player consent. The project sends no player input")
    print(f"  [§8.5] tightening player-consent bullet {i}")
    replace_runs(p, [
        ("Player consent. ", {"bold": True}),
        ("Only AI-generated content (the dossier, the wand description) "
         "crosses the OpenAI boundary; the player's memo and material choices "
         "stay local. A README disclosure of which fields cross the network "
         "is in Appendix A.", {}),
    ])

    # -----------------------------------------------------------------------
    # 5. §7.3.4 evaluator-prompt — small clarity fix.
    # -----------------------------------------------------------------------
    i, p = find_para(doc, "The evaluator prompt encodes the explicit scoring rubric")
    print(f"  [§7.3.4] light edit on para {i}")
    replace_runs(p, [
        ("The evaluator prompt encodes the explicit scoring rubric reproduced "
         "in §7.5.4. Critically, it reframes itself in the second person — "
         "\"You are a senior wandmaker evaluating whether a finished wand "
         "suits a specific customer\" — which produces materially more "
         "in-character verdicts than a third-person \"score this wand\" "
         "framing.", {}),
    ])

    # -----------------------------------------------------------------------
    # 6. §7.3.1 — drop the awkward "subsystem call 4" parenthetical.
    # -----------------------------------------------------------------------
    i, p = find_para(doc, "The few-shot block is doing real work.")
    print(f"  [§7.3.1] light edit on para {i}")
    replace_runs(p, [
        ("The few-shot block is doing real work. Without it, GPT-4o's customer "
         "fields correlate (they sound plausible together) but do not interlock "
         "(changing one would not break another). With it, the model produces "
         "dossiers where flipping the constraint flips the request, which lets "
         "the evaluator prompt (§7.3.4) score the player on subtext rather "
         "than surface match.", {}),
    ])

    # -----------------------------------------------------------------------
    # 7. §6.3 third paragraph (decision to run image generation locally) —
    # tighten slightly.
    # -----------------------------------------------------------------------
    i, p = find_para(doc, "The decision to run image generation locally")
    print(f"  [§6.3] tightening local-vs-hosted para {i}")
    replace_runs(p, [
        ("Image generation runs locally rather than via a hosted API (DALL·E 3, "
         "Stable Diffusion API, Recraft) for three reasons: (a) ", {}),
        ("cost", {"italic": True}),
        (" — at six images per day across seven days, hosted APIs would charge "
         "≈$3 per session; (b) ", {}),
        ("latency", {"italic": True}),
        (" — hosted round-trip is 4–8 s on a residential connection versus "
         "3–6 s on local hardware once warm; (c) ", {}),
        ("determinism control", {"italic": True}),
        (" — local hosting fully exposes the workflow graph, seed, and model "
         "identity, none of which hosted APIs surface.", {}),
    ])

    # -----------------------------------------------------------------------
    # 8. §3.4 contributions intro — already tightened by Phase 2 ("seven
    # contributions"). No change.
    # -----------------------------------------------------------------------

    # -----------------------------------------------------------------------
    print(f"saving -> {DOCX_PATH}")
    doc.save(str(DOCX_PATH))
    print("done")


if __name__ == "__main__":
    sys.exit(main() or 0)
