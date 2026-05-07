# Workstream B drafts — SVDQuant / Nunchaku academic explanation

These prose blocks replace the existing material in `Final_Report.md` at the
named line ranges. They will be merged into both the markdown and the .docx
during the Phase-2 insertion pass.

---

## REPLACEMENT FOR §6.3 (currently lines 123–127 of Final_Report.md)

Latent-diffusion models [3] and their accelerated variants — DDPM [8], DDIM, and the consistency models — have made image generation cheap enough to consider running at game runtime. The remaining barrier on consumer hardware is twofold: VRAM and per-step latency. A full-precision 12-billion-parameter diffusion transformer such as FLUX.1 occupies ≈22 GiB of weights, which exceeds the 8 GiB of an RTX 4070 Laptop by almost 3×; even the smaller Z-Image Turbo (≈1 B parameters at fp16) leaves no working headroom for a Unity client on the same GPU. Compounding this, *weight-only* quantisation — storing weights at 4-bit but computing in 16-bit — does not actually accelerate diffusion inference, because diffusion compute is bandwidth-bound: reading 4-bit weights is fast, but every multiply still upcasts to 16 bits, and the bandwidth saving is recovered as compute time. The barrier this project had to clear is therefore *both-sides* (W4A4) quantisation: 4-bit weights *and* 4-bit activations, computed natively in 4-bit. The challenge is that activations contain large outliers that magnify quantisation error and visibly destroy image quality.

Li et al.'s **SVDQuant** (ICLR 2025) [9] is the first method to make W4A4 work for diffusion without the quality collapse. Its core idea is two-step. First, a smoothing factor migrates outliers from the activation tensor *X* into the weight tensor *W*. Second, the smoothed weight *W̃* is decomposed via a singular-value decomposition into a high-precision low-rank branch *L₁L₂* (rank 32, kept at 16 bits) plus a residual *R = W̃ − L₁L₂* (kept at 4 bits). The low-rank branch *absorbs* the outliers; the residual is well-behaved enough for naive 4-bit quantisation to preserve quality. Naïvely, however, running the low-rank branch as an extra kernel adds ~50 % latency overhead from the extra activation memory traffic. The companion **Nunchaku** inference engine [9] solves this by *fusing* the low-rank branch's down-projection into the quantisation kernel and its up-projection into the 4-bit compute kernel, so the low-rank branch shares the same shared-memory tile as the residual. The fused configuration adds only 5–10 % latency overhead. The headline numbers reported on FLUX.1 12B are **3.6× memory reduction** (22.7 GiB → 6.5 GiB) and **3.0× speedup** over the W4A16 NF4 weight-only baseline on a laptop RTX 4090, with FID/PSNR within 1–2 points of the BF16 reference and ImageReward statistically indistinguishable. This is the engineering that puts diffusion *runtime* generation on consumer hardware within reach of student projects rather than workstations.

[Reference 9 to add to §11: M. Li, Y. Lin, Z. Zhang, et al. *SVDQuant: Absorbing Outliers by Low-Rank Components for 4-Bit Diffusion Models.* ICLR 2025. arXiv:2411.05007.]

---

## EXPANSION FOR §7.4.4 (currently lines 276–278 of Final_Report.md)

Performance was characterised on the development machine — Intel Core i7-13620H, 16 GB RAM, NVIDIA RTX 4070 Laptop GPU (8 GB VRAM), Windows 11. The model deployed is Z-Image Turbo under SVDQuant W4A4 quantisation served via the Nunchaku inference engine (see §6.3 for the methodology and citation). Concretely, every linear layer is materialised on disk as a 4-bit residual *R* plus a rank-32 16-bit low-rank branch *L₁L₂*; at runtime, Nunchaku's two fused CUDA kernels — *(quantise + down-projection)* and *(4-bit compute + up-projection)* — share input/output tiles so the low-rank branch costs no additional memory traffic. The choice of this stack is not aesthetic; it is the binding constraint that makes the project deployable on a 8 GB consumer GPU. The table below compares the three precision tiers on the project's actual machine. Only the SVDQuant row fits.

| Configuration               | Weights | Activations | VRAM (model) | Single-image latency (warm) | Fits in 8 GiB GPU + Unity client? |
|-----------------------------|--------:|------------:|-------------:|----------------------------:|----------------------------------:|
| Z-Image Turbo BF16 (baseline) | 16-bit  | 16-bit       | ≈8.5 GiB     | not measurable¹              | no (OOM)                          |
| Z-Image Turbo NF4 (W4A16)     | 4-bit   | 16-bit       | ≈3.4 GiB     | ≈11 s                       | yes (no acceleration vs BF16²)    |
| Z-Image Turbo SVDQuant W4A4 (this project) | 4-bit | 4-bit | **≈2.7 GiB** | **≈4 s**                    | **yes**                           |

¹ The BF16 model spills weights to system RAM via CPU offloading and produces an image, but at >40 s per generation; we treat this as not-fitting for the purpose of the gameplay budget.

² Weight-only NF4 reduces VRAM but does not accelerate computation, because diffusion compute is bandwidth-bound; the 4-bit weights are upcast to 16 bits before each multiply, so the saved load time is recovered as compute time. This is the limitation SVDQuant's W4A4 path is designed to break (see §6.3 and Figure 13).

The 4 s warm-path latency reported above is the figure that lets the seven-day pipeline hide its 25 s of cumulative AI work behind reading and minigame input (§7.2.3 and Figure 6); without SVDQuant + Nunchaku the project would either exceed the GPU memory ceiling or run at a latency that the parallel-pre-generation cascade cannot hide.

[Cross-reference Figure 13 from §6.3 and §7.4.4: SVDQuant decomposition + Nunchaku kernel fusion.]

---

## REPLACEMENT FOR §8.3.2 first bullet under "What worked" (currently line 459 of Final_Report.md)

Replace the existing sentence
> *The Nunchaku INT4 quantisation is the deciding factor for consumer-hardware deployability: without it, the 8 GB VRAM budget of the development laptop cannot host the model.*

with:

> The SVDQuant W4A4 quantisation [9] served via the Nunchaku inference engine (§6.3, §7.4.4, Figure 13) is the deciding factor for consumer-hardware deployability. Z-Image Turbo at BF16 exceeds the 8 GiB VRAM budget of the development laptop and forces CPU offloading; weight-only NF4 fits but does not accelerate the bandwidth-bound diffusion compute. Only the both-sides 4-bit path — enabled by SVDQuant's low-rank-branch outlier absorption and Nunchaku's kernel fusion — produces both the ≈2.7 GiB memory footprint and the ≈4 s warm-path latency that the 25 s/day generation budget requires. The published 3.0× speedup over NF4 is consistent with our measured 11 s → 4 s warm-path drop on the project hardware.
