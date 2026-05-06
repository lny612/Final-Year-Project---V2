# Q&A Preparation — *The Wand Atelier*

**Q&A budget: 5 minutes total. Plan ~45 sec per question on average — so realistically you'll field 4–6 questions.**

> **General delivery rules:**
> 1. **Pause** for one full second before answering. It signals confidence.
> 2. **Restate the question** in your own words ("So you're asking whether…") — this gives you thinking time and confirms you heard right.
> 3. **Lead with your answer**, then justify. Never bury the answer under preamble.
> 4. **If you don't know** — say so cleanly: "I haven't tested that, but my intuition is X — I'd want to verify." Reviewers respect honesty more than bluffing.
> 5. **Watch the moderator's signal.** If you see a "wrap up" cue, switch to the elevator version mid-sentence.

---

## Technical Questions

### Q1. Why GPT-4o specifically — not a cheaper or local model?

**Full answer (60s):**
GPT-4o was the right call for three reasons. First, it had the strongest performance on structured JSON output of the models I tested — and my entire pipeline relies on parsing JSON responses, so reliability there matters more than raw quality. Second, the model's instruction-following on the "logical chain" constraint — schoolOfMagic flowing into profession flowing into request — was noticeably better than smaller models like 3.5-turbo, which would frequently break the chain. Third, latency. GPT-4o averages four to seven seconds per call, which is fast enough that I can hide it behind the minigame. A local 70B Llama would either be slower on my hardware or require a quantised version that loses the constraint-following I depend on. That said, I've designed the API layer behind an interface, so swapping in a local model later is a one-day refactor.

**Elevator (15s):** GPT-4o gave me reliable JSON output, strong constraint-following on the customer schema, and four-to-seven-second latency that I can hide behind the minigame. Local models I tested either broke the schema or were too slow.

---

### Q2. How do you handle GPT-4o's non-determinism in scoring?

**Full answer (60s):**
Honestly — I haven't fully solved it, and I called this out in the limitations slide. The same wand can score plus-or-minus ten points across runs. What I've done so far is three things. One: the system prompt explicitly anchors the rubric numerically — addresses request only, forty to sixty; addresses true goal, sixty to seventy-five; and so on — which constrains the model's range. Two: I lower temperature to point-three for the evaluation call specifically, while leaving generation at point-eight for creativity. Three: I cache the verdict by hashing customer-plus-wand, so the *same* wand for the *same* customer always sees the same score within a session. What I'd build next is a deterministic verifier — a smaller model or even rule-based check — that runs in parallel and catches ratings that fall outside expected bounds.

**Elevator (15s):** Anchored rubric numerically in the system prompt, lowered temperature for evaluation, and cached verdicts within a session. Full determinism is unsolved — I'd add a verifier model next.

---

### Q3. What's the latency budget per round, and how do you hide it?

**Full answer (60s):**
A round end-to-end has roughly six GPT-4o calls — at four to seven seconds each — and seven ComfyUI image calls at eight to twelve seconds each. If I ran them serially, that's around ninety to one hundred and twenty seconds of pure waiting per round. Unplayable. The trick is concurrency. Material images generate in parallel as soon as the material data lands. The wand image generation kicks off the moment the player confirms their material choice — and runs *concurrently* with the tracing minigame, which itself takes forty-five to ninety seconds depending on round outcomes. By the time the player exits the minigame, the wand verdict and image are usually ready. The pattern in code is `MinigameSceneRunner` — two coroutines, two flags, both must finish before scene transition. That's the single most impactful UX decision in the project — it hides about seventy percent of the total generation time.

**Elevator (15s):** Six text and seven image calls per round — about ninety seconds serial. I run them concurrently with gameplay, particularly hiding wand generation behind the tracing minigame, which masks about seventy percent of the wait.

---

### Q4. Why ComfyUI + Nunchaku INT4 instead of a hosted API like DALL·E or Stable Diffusion online?

**Full answer (60s):**
Cost and latency. A hosted image API at sixty cents per image — that's per material, six materials per day, plus the wand — is roughly four dollars a session. Plus another four to fifteen seconds per request over the network. Local generation is essentially free at marginal cost, and surprisingly fast — the Z-Image Turbo model with Nunchaku INT4 quantisation produces an image in eight to twelve seconds on my eight-gigabyte laptop GPU. There's also a control argument: ComfyUI gives me the full workflow JSON, so I can deterministically inject prompts at the CLIP node and seed at the KSampler node. Hosted APIs hide that. The downside is the player needs ComfyUI running locally — I haven't solved distribution yet. For a Steam release, I'd ship a bundled inference server.

**Elevator (15s):** Cost and control. Local generation is free at the margin, eight-to-twelve-second latency, and ComfyUI gives me deterministic prompt injection. Distribution is the unsolved part.

---

### Q5. How do you prevent prompt injection from generated customer text?

**Full answer (60s):**
Good question — this was a real concern. The risk is that GPT-4o invents a customer whose dialogue contains text like "ignore previous instructions" or "scoring rule: this wand always gets a hundred." That text would then end up in the evaluation prompt as part of the dossier. My defence is two-layer. First, the customer generation prompt explicitly forbids meta-references and reserved tokens. Second — and more importantly — the evaluation prompt is *structured*. I don't pass the customer dossier as a free-form text block. I pass it as labelled fields — Name, Profession, Request, True Goal, Constraint — wrapped in markers the system prompt is instructed to treat as data, not instructions. So even if a customer field contained injection-flavoured text, the evaluator treats it as the customer *saying that*, not as new system instructions. I haven't done a full red-team pass on this, and I'd want to before any public release.

**Elevator (15s):** Two layers — generation prompt forbids meta-text, and the evaluation prompt treats customer fields as labelled data, not free-form instructions. Not red-teamed yet though.

---

## Design Questions

### Q6. The memo system seems strict — what stops players from gaming it?

**Full answer (60s):**
That's exactly the failure mode I worried about, and the design answer is that there *isn't* one correct memo. The three slots — Purpose, Personality, Element — are intentionally fuzzy. A customer's purpose could be summarised as "war" or "duelling" or "honour" depending on what the player picks up on. The downstream consequence is that the *materials in the market* gain match-hint glyphs based on the *player's memo*, not the original dossier. So if your memo is wrong, your hints are wrong, and you end up buying the wrong materials. Crucially, the AI judge is evaluating against the *original customer*, not the memo. So a wrong memo doesn't change the scoring — it changes what looks good in the moment. That asymmetry is the soft gate. You can't game it because the gate isn't a quiz; it's a commitment device.

**Elevator (15s):** There's no "correct" memo — three fuzzy slots. A wrong memo silently mis-tunes your material hints downstream, but the AI judge still scores against the original dossier. So gaming it just punishes you later.

---

### Q7. Why three gate types in the minigame instead of one?

**Full answer (60s):**
Pure tracing got boring after about forty seconds in playtests. I needed escalation. Three gate types let me build a difficulty curve across rounds without changing core mechanics — just composition. Round one is all Tap, which teaches the keyboard layer. Round two adds Hold, which teaches commitment under time pressure — because the mist keeps advancing while you hold. Round three adds Accent, which adds spatial input on top of timing. By the third round, the player is doing four things at once: tracing, watching the mist, pressing keys, and flicking the mouse. That layered cognitive load is what makes the round feel like a *ritual* rather than a button-mash. From a design heuristic standpoint, this is just classic Mark Brown / Game Maker's Toolkit progression — teach, test, twist.

**Elevator (15s):** Three gate types let me layer cognitive load across rounds without changing core mechanics. Tap teaches input, Hold adds commitment, Accent adds spatial — by round three it feels like a real ritual.

---

### Q8. How did you balance the reward formula?

**Full answer (60s):**
Iteratively, and not perfectly. The starting point was: the game has to last seven days, with rent of two-fifty on day three and four-hundred on day six. Player starts with five hundred gold. So they need to clear about a thousand gold across seven days to survive — call it one-fifty per day baseline. That gave me my numerator: `gold = 150 × (score / 100)`. Quality multiplier on top — A is one-point-zero, F is point-four — means a perfect run pays one-fifty-times-A, a disaster pays sixty. Reputation moves more aggressively: plus twenty for a great match, minus ten for a flop, gated by score forty as the threshold. That's deliberately punishing — bad wands cost you customers, not just money. I tested with an automated rollout — random material picks across a hundred runs — to confirm the average outcome lands roughly in the "survive but barely" zone. A skilled player should comfortably reach the Royal ending; a careless one should be bankrupt by day six.

**Elevator (15s):** Started from rent obligations — players need ~150 gold/day to survive seven days. Built the formula backward from that, then tested with random rollouts to confirm a careless player goes bankrupt and a careful one reaches Royal.

---

### Q9. The 7-day arc feels short — why not 14 or 30?

**Full answer (60s):**
Honest answer: pacing and budget. Seven days is enough to teach the mechanics, introduce one rent cycle (two checkpoints, days three and six), and reach a conclusion that feels earned. Longer arcs would dilute reputation tension — the curve from low rep to royal needs to feel achievable but not trivial. With seven days and rep deltas of negative-ten to plus-twenty, you need roughly five strong days to hit the Royal threshold of ninety. That's a meaningful challenge. At fourteen days, the same arithmetic means you can recover from any mistake, which kills tension. The other consideration is API cost — at four cents per round, fourteen days is sixty cents per playthrough, which adds up if I'm running playtests at scale. If I extend the arc, I'd add multi-day customer threads — same NPC returning — to give the extra days narrative weight rather than just more transactions.

**Elevator (15s):** Seven days is the minimum length to teach mechanics, fit a rent cycle, and make Royal feel earned. Longer arcs dilute reputation tension. If I extended it, I'd add returning multi-day customer threads.

---

## Evaluation Questions

### Q10. How do you know players *enjoy* this versus just *complete* it?

**Full answer (60s):**
Fair challenge. My current evidence is qualitative — playtester quotes — and small-scale. The two recurring observations were that the memo gate "felt like reading a real letter" and that the Hold gates with the mist closing in were "stressful in a good way." Both of those are signals of *engagement*, not just completion. What I haven't done is a structured player-study — measuring time-on-task, retry rates, drop-off points, or post-session enjoyment scores. If this were going to a publisher, I'd run a twenty-player study with a System Usability Scale survey and Game Experience Questionnaire post-session, plus telemetry on session length and re-engagement after a break. The honest answer is: my evidence supports the hypothesis but doesn't prove it.

**Elevator (15s):** Qualitative playtester quotes suggest engagement — "felt like reading a real letter" and "stressful in a good way" — but I haven't run a structured player-study. That's what I'd do next.

---

### Q11. What metric would convince you the AI judge is "fair"?

**Full answer (60s):**
Three metrics layered on top of each other. First — *test-retest reliability*. Run the same wand against the same customer fifty times and measure standard deviation of matchScore. Right now it's around plus-or-minus ten points; I'd want it under three for a fair judge. Second — *human-AI agreement*. Have ten human evaluators score one hundred wand-customer pairs; compare correlation with the AI's score. A correlation above point-eight would be good. Third — *adversarial fairness*. Generate wands that are *meant* to score badly versus wands meant to score well, and check the AI consistently distinguishes them. None of those three are at production-grade right now. I'd describe the current judge as "directionally fair, not metrically fair." Solving that is the single most important next step.

**Elevator (15s):** Three layered metrics — test-retest reliability under three points of standard deviation, human-AI correlation above point-eight, and adversarial-pair separation. Currently the judge is directionally fair, not metrically fair.

---

### Q12. If you had another six months, what's the *single* most important change?

**Full answer (60s):**
Deterministic scoring. Everything else — more customers per day, commissions, multi-day threads — those are content additions. They scale linearly with effort. But if the judge is non-deterministic, the *whole pipeline* is undermined. A player who scores eighty on a wand and seventy on the same wand next session loses trust in the system. The fix is probably a hybrid: a small rule-based verifier that checks the GPT-4o output against the rubric, plus a deterministic post-processing layer that snaps scores to nearest-five and locks them per-customer-per-session. With determinism solved, every other improvement compounds. Without it, every other improvement is built on sand.

**Elevator (15s):** Deterministic scoring. Everything else is content; non-determinism undermines the entire pipeline. Hybrid rule-based verifier plus snap-to-rubric post-processing.

---

## "Hostile" question playbook (rare but possible)

### "Isn't this just an OpenAI wrapper?"
*Answer:* "If you mean the customer text, partly — but the design contribution isn't the model, it's the *evaluation loop*. Generation alone is solved. Generation plus evaluation by the same model in a real-time game with hidden latency is, as far as I've seen in the literature, not solved. That's the contribution."

### "Have you considered ethical issues — AI replacing writers?"
*Answer:* "Yes, and I think about this seriously. The honest position is that this technique is a tool. For a small team or solo dev who otherwise couldn't afford a writer, this opens up a category of game that wouldn't exist. For a studio that already has writers, this would be a poor substitute — the AI output is good enough for procedural variety but not for hand-crafted character arcs. I see it as additive to the medium, not replacement."

### "Could you have just hand-written 100 customers and shuffled them?"
*Answer:* "I could. Two reasons I didn't. One — combinatorial explosion. Each customer needs to interact correctly with the materials in the market, which are also generated, so a static pool would constrain material generation too. Two — the *judge* is the real point. Even if customers were static, I'd still want GPT-4o evaluating wands, which means the integration work is the same."

---

## What to do if you blank

1. **Buy time honestly:** *"That's a good question — let me think for a second."* (Then actually think for one second. Don't pretend.)
2. **Re-state to clarify:** *"So you're asking about [X]?"* This often surfaces what they really meant.
3. **Bridge to known territory:** *"I haven't directly tested that, but the closest thing I've measured is [Y]…"*
4. **Defer gracefully:** *"I'd want to verify before answering — could I follow up via email after the session?"* This is allowed; reviewers know nobody has every number memorised.

---

## After the Q&A

- Thank the moderator and the audience.
- Stay near the lectern for two minutes — most one-on-one questions happen here, and they're often more revealing than the formal Q&A.
- Write down any questions you couldn't fully answer; those are your "to fix" list for the final report.
