"""Latency / cost / VRAM benchmark for the in-game generative-AI pipeline.

Replicates the four GPT-4o calls and one ComfyUI image-gen call the game
makes per round, measured outside of Unity for clean timing. Writes:

    .claude/Final Report/llm_study/benchmarks.csv   one row per (operation, sample)
    .claude/Final Report/llm_study/benchmarks_summary.csv  one row per operation

For each operation we report mean, stdev, p50, p95 over N samples plus a
cost figure (OpenAI tokens × pricing, ComfyUI = 0). Peak VRAM is sampled at
500 ms intervals via `nvidia-smi` during the customer + image roundtrip and
reported as a single peak number.

Usage:
    py tools/benchmarks.py --n 30                # full run (~$2)
    py tools/benchmarks.py --n 5 --no-comfy      # cheap smoke test, OpenAI only
    py tools/benchmarks.py --n 30 --no-vram      # skip nvidia-smi polling

Requires ComfyUI to be reachable on http://127.0.0.1:8000 unless --no-comfy.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import statistics
import subprocess
import sys
import threading
import time
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
STUDY_DIR    = PROJECT_ROOT / ".claude" / "Final Report" / "llm_study"
WORKFLOW     = PROJECT_ROOT / "Assets" / "StreamingAssets" / "image_z_image_turbo.json"
ENV_FILE     = STUDY_DIR / ".env"

sys.path.insert(0, str(PROJECT_ROOT / "tools"))
from run_llm_study import (
    SYSTEM_PROMPT as CUSTOMER_SYSTEM,
    USER_PROMPT_BASE as CUSTOMER_USER_BASE,
    load_env,
)


# ---------------------------------------------------------------------------
# OpenAI (customer call, used as the GPT-4o latency proxy)
# ---------------------------------------------------------------------------
def call_openai(model: str = "gpt-4o") -> dict:
    from openai import OpenAI
    client = OpenAI(api_key=os.environ["OPENAI_API_KEY"])
    t0 = time.perf_counter()
    resp = client.chat.completions.create(
        model=model,
        messages=[{"role": "system", "content": CUSTOMER_SYSTEM},
                  {"role": "user",   "content": CUSTOMER_USER_BASE}],
        temperature=1.0,
    )
    dt = (time.perf_counter() - t0) * 1000
    cost = (resp.usage.prompt_tokens / 1000 * 0.0025
            + resp.usage.completion_tokens / 1000 * 0.01)
    return {"latency_ms": dt, "cost_usd": cost,
            "input_tokens": resp.usage.prompt_tokens,
            "output_tokens": resp.usage.completion_tokens}


# ---------------------------------------------------------------------------
# ComfyUI (image generation latency)
# ---------------------------------------------------------------------------
def call_comfy(prompt_text: str = "a wooden wand with amber accents, pixel art, 128x128") -> dict:
    import requests
    if not WORKFLOW.exists():
        raise FileNotFoundError(WORKFLOW)
    workflow = json.loads(WORKFLOW.read_text(encoding="utf-8"))
    # Patch node 5 (CLIPTextEncode positive) and node 4 (KSampler seed) the
    # same way MaterialService does.
    for node_id in ("5", "5_positive", "positive"):
        if node_id in workflow and "inputs" in workflow[node_id]:
            workflow[node_id]["inputs"]["text"] = prompt_text
            break
    else:
        for nid, node in workflow.items():
            if isinstance(node, dict) and node.get("class_type") == "CLIPTextEncode":
                inputs = node.get("inputs", {})
                # take the first one we find as positive
                if "text" in inputs:
                    inputs["text"] = prompt_text
                    break
    for nid, node in workflow.items():
        if isinstance(node, dict) and node.get("class_type") == "KSampler":
            node.setdefault("inputs", {})["seed"] = int(time.time() * 1000) % (2**31)
            break

    t0 = time.perf_counter()
    r = requests.post("http://127.0.0.1:8000/prompt", json={"prompt": workflow}, timeout=20)
    r.raise_for_status()
    pid = r.json()["prompt_id"]
    # Poll
    while True:
        h = requests.get(f"http://127.0.0.1:8000/history/{pid}", timeout=10).json()
        if pid in h and h[pid].get("status", {}).get("completed"):
            break
        if time.perf_counter() - t0 > 90:
            raise TimeoutError("ComfyUI generation > 90 s")
        time.sleep(1.0)
    dt = (time.perf_counter() - t0) * 1000
    return {"latency_ms": dt, "cost_usd": 0.0}


# ---------------------------------------------------------------------------
# VRAM peak sampler (nvidia-smi)
# ---------------------------------------------------------------------------
class VramSampler:
    def __init__(self, interval_s: float = 0.5):
        self.interval = interval_s
        self.peak_mib = 0
        self._stop = threading.Event()
        self._thread = threading.Thread(target=self._loop, daemon=True)

    def _read_vram_mib(self) -> int | None:
        try:
            out = subprocess.run(
                ["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"],
                check=True, capture_output=True, text=True, timeout=2,
            ).stdout.strip().splitlines()
            return max(int(x.strip()) for x in out if x.strip())
        except Exception:
            return None

    def _loop(self):
        while not self._stop.is_set():
            v = self._read_vram_mib()
            if v is not None:
                self.peak_mib = max(self.peak_mib, v)
            self._stop.wait(self.interval)

    def __enter__(self):
        self._thread.start()
        return self

    def __exit__(self, *a):
        self._stop.set()
        self._thread.join(timeout=2)


# ---------------------------------------------------------------------------
def percentile(xs: list[float], p: float) -> float:
    if not xs: return 0.0
    s = sorted(xs)
    k = max(0, min(len(s) - 1, int(round(p / 100 * (len(s) - 1)))))
    return s[k]


def stat_block(name: str, samples: list[dict]) -> dict:
    lats = [s["latency_ms"] for s in samples]
    costs = [s.get("cost_usd", 0.0) for s in samples]
    return {
        "operation": name,
        "n": len(samples),
        "latency_mean_ms": round(statistics.mean(lats), 1) if lats else 0,
        "latency_stdev_ms": round(statistics.stdev(lats), 1) if len(lats) > 1 else 0,
        "latency_p50_ms":  round(percentile(lats, 50), 1),
        "latency_p95_ms":  round(percentile(lats, 95), 1),
        "cost_per_call_usd": round(statistics.mean(costs), 5) if costs else 0.0,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--n", type=int, default=30, help="samples per operation (default 30)")
    parser.add_argument("--no-comfy", action="store_true", help="skip ComfyUI calls")
    parser.add_argument("--no-vram", action="store_true", help="skip VRAM polling")
    parser.add_argument("--model", default="gpt-4o", help="OpenAI model (default gpt-4o)")
    args = parser.parse_args()

    load_env()
    if "OPENAI_API_KEY" not in os.environ:
        sys.exit("OPENAI_API_KEY not set; copy `.env.template` to `.env` and fill it in.")

    # ── Phase 1: OpenAI customer call ×N
    print(f"=== OpenAI ({args.model}) × {args.n} ===")
    openai_samples: list[dict] = []
    with (VramSampler() if not args.no_vram else _Null()) as vram_oai:
        for i in range(args.n):
            try:
                s = call_openai(args.model)
                openai_samples.append(s)
                print(f"  [{i+1:02d}/{args.n}] {s['latency_ms']:6.0f} ms  ${s['cost_usd']:.5f}")
            except Exception as exc:
                print(f"  [{i+1:02d}/{args.n}] FAIL {exc}")

    # ── Phase 2: ComfyUI ×N
    comfy_samples: list[dict] = []
    if not args.no_comfy:
        print(f"\n=== ComfyUI × {args.n} ===")
        with (VramSampler() if not args.no_vram else _Null()) as vram_comfy:
            for i in range(args.n):
                try:
                    s = call_comfy()
                    comfy_samples.append(s)
                    print(f"  [{i+1:02d}/{args.n}] {s['latency_ms']:6.0f} ms")
                except Exception as exc:
                    print(f"  [{i+1:02d}/{args.n}] FAIL {exc}")
    else:
        vram_comfy = None

    # ── Per-call CSV
    rows = []
    for i, s in enumerate(openai_samples):
        rows.append({"operation": "openai_customer", "sample": i,
                     "latency_ms": s["latency_ms"], "cost_usd": s["cost_usd"]})
    for i, s in enumerate(comfy_samples):
        rows.append({"operation": "comfyui_image", "sample": i,
                     "latency_ms": s["latency_ms"], "cost_usd": 0.0})
    long_path = STUDY_DIR / "benchmarks.csv"
    with long_path.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["operation", "sample", "latency_ms", "cost_usd"])
        w.writeheader(); w.writerows(rows)
    print(f"\nwrote {long_path}")

    # ── Summary CSV
    summary = []
    if openai_samples: summary.append(stat_block("openai_customer", openai_samples))
    if comfy_samples:  summary.append(stat_block("comfyui_image", comfy_samples))
    if not args.no_vram:
        if openai_samples: summary[0]["peak_vram_mib"] = vram_oai.peak_mib
        if comfy_samples:  summary[1]["peak_vram_mib"] = vram_comfy.peak_mib if vram_comfy else 0
    summary_path = STUDY_DIR / "benchmarks_summary.csv"
    if summary:
        keys = sorted({k for r in summary for k in r.keys()})
        with summary_path.open("w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(f, fieldnames=keys); w.writeheader(); w.writerows(summary)
        print(f"wrote {summary_path}")
        print("\nsummary:")
        for r in summary:
            print(f"  {r['operation']:20s}  n={r['n']:3d}  mean={r['latency_mean_ms']} ms  "
                  f"p95={r['latency_p95_ms']} ms  cost=${r['cost_per_call_usd']:.5f}")


class _Null:
    """No-op context manager when --no-vram."""
    def __enter__(self): return self
    def __exit__(self, *a): pass
    peak_mib = 0


if __name__ == "__main__":
    sys.exit(main() or 0)
