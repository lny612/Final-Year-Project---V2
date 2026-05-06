# Hero-Image Generation Prompt — *The Wand Atelier*

For text-to-image models (Z-Image Turbo, SDXL, Flux, Midjourney v6, DALL·E 3). Designed to produce the slide-1 title image and marketing key art that captures the game's elevator pitch in a single frame.

---

## Master prompt (full version)

> **Cozy fantasy wandmaker's atelier interior**, warm late-afternoon sunlight slanting through tall lead-glass shop windows, dust motes floating in golden beams. **Wide three-quarter perspective** showing both the **craftsman's workbench in the foreground** and a **modest customer counter in the middle ground**, with **floor-to-ceiling oak shelves** behind crammed full of finished wands in velvet-lined cases, glass jars of glowing dragon scales, twists of silver wire, bundles of dried hawthorn and yew, neatly labelled drawers, and small ceramic crucibles.
>
> On the **workbench**: a half-finished wand resting on a leather mat, a quill and inkpot, a folded **parchment letter with a broken wax seal** showing a customer's dossier, two **glowing crystalline cores** (one cobalt-blue, one ember-orange), three **slender wood-blanks** with visible grain, brass calipers, a **rune-etched circle softly glowing** as if mid-ritual, scattered shavings.
>
> At the counter: a **hooded fantasy customer** of indeterminate background standing patiently — only their silhouette and gloved hands resting on the counter visible — implying they've just delivered their request and are waiting.
>
> Through the **window in the background**: a glimpse of a **bustling cobblestone market square** with striped awnings, lantern posts, and silhouettes of other townsfolk going about their day, suggesting the broader world the player serves.
>
> A **small chalkboard on the wall** marked with **seven tally strokes** (one circled in chalk) hints at the seven-day rhythm. A **curling stack of unopened letters in a brass tray** suggests more orders waiting.
>
> **Style:** painterly storybook illustration, *Studio Ghibli meets Hogwarts Legacy concept art*, warm earth-tone palette of cream, sepia, oak brown, candle gold, deep forest green and burgundy accents. Soft volumetric lighting, gentle bloom on magical glows, fine ink-line detail, hand-painted texture, slight grain, **no text or letters anywhere on signs or pages**. Aspect ratio **16:9**, ultra-wide cinematic composition, 4K detail, masterpiece quality, atmospheric depth-of-field with the workbench tack-sharp and the market softly blurred.

### Negative prompt

> photo-real, harsh contrast, low quality, blurry, jpeg artefacts, watermark, signature, text, lettering, words, captions, modern technology, electronics, neon colors, anime cell-shading, deformed hands, extra fingers, cluttered chaotic mess, gore, dark horror tone, overly saturated, plastic look, 3d render, cgi.

---

## Style-specific variants

### Z-Image Turbo / SDXL (concise — these models prefer short clear prompts)

```
Cozy fantasy wandmaker's atelier interior, warm afternoon sunlight, wide three-quarter view.
Foreground: wooden workbench with half-finished wand on leather mat, parchment dossier with
broken wax seal, two glowing crystalline cores (cobalt blue and ember orange), three wood-blank
sticks, brass calipers, glowing rune circle. Middle ground: hooded customer silhouette at the
counter, gloved hands resting. Background: oak shelves of wands and labelled jars; window
showing cobblestone market square with striped awnings. Small chalkboard with seven tally
strokes on the wall. Painterly storybook illustration, Studio Ghibli meets Hogwarts Legacy
concept art, warm cream-sepia-oak palette with candle-gold and forest-green accents, soft
volumetric light, dust motes, atmospheric depth, no text, 16:9 cinematic composition,
masterpiece, 4k detail.

Negative: photo-real, harsh contrast, blurry, watermark, text, words, modern tech, neon,
anime, deformed hands, extra fingers, gore, plastic, cgi.
```

### Midjourney v6 / v7

```
Cozy fantasy wandmaker's atelier interior :: half-finished wand on leather workbench, glowing
cobalt and ember crystalline cores, parchment dossier with broken wax seal, three slender
wood-blanks, glowing rune circle :: hooded customer silhouette at counter, gloved hands ::
oak shelves of wands and labelled jars behind, lead-glass window showing cobblestone market
square with striped awnings :: small chalkboard with seven tally strokes :: painterly
storybook concept art, Studio Ghibli + Hogwarts Legacy aesthetic, warm cream-sepia-oak
palette, candle-gold magical glow, dust motes, soft volumetric light --ar 16:9 --style raw
--stylize 350 --no text words letters signature
```

### DALL·E 3 / GPT-Image-1 (natural language form, since these handle prose well)

```
Create a wide cinematic 16:9 painterly storybook illustration of a cozy fantasy wandmaker's
atelier in late-afternoon light. In the foreground sits a wooden workbench with a
half-finished wand on a leather mat; beside it a parchment letter with a broken wax seal
(the customer's dossier), two glowing crystalline cores — one cobalt blue and one ember
orange — three wood-blank sticks, brass calipers, and a softly glowing rune circle as if
mid-ritual. At a counter behind the bench stands a hooded customer in silhouette with gloved
hands resting on the wood, patient and waiting. Tall oak shelves stretch up the back wall
filled with wand cases, glowing jars, dried herbs, and labelled drawers. Through a lead-glass
window we glimpse a cobblestone market square with striped awnings and townsfolk. A small
chalkboard on the wall shows seven tally strokes — the rhythm of a seven-day work cycle.
Aesthetic: Studio Ghibli meets Hogwarts Legacy concept art, painterly hand-painted texture,
warm cream/sepia/oak/candle-gold palette with deep forest-green and burgundy accents, soft
volumetric beams of light, dust motes, gentle magical bloom on the cores. No text, no
letters, no captions anywhere in the image.
```

---

## Tuning knobs (if the first generation isn't right)

| Issue | Fix |
|---|---|
| Too modern / video-gamey | Add: *"oil painting, traditional illustration, hand-painted, Brian Froud, Alan Lee, Hildebrandt brothers influence"* |
| Too dark / grim | Add: *"bright cosy warm lighting, hopeful tone, inviting"* — and lower negative weight on dark themes |
| Customer looks like a player avatar | Replace *"hooded customer"* with *"a robed traveller"*, *"a young apprentice mage"*, or *"a weary scholar"* — pick one |
| Too cluttered | Remove half the workbench items; keep only the wand-in-progress, the dossier, and one core |
| Magical glow too anime / neon | Add to negative: *"neon, glow stick, anime aura"* and add to positive: *"subtle inner light, candle-warm magical glow"* |
| Wand looks like a sword/blade | Replace *"wand"* with *"slender wooden wand, foot-long polished stick with carved tip"* |
| Want a single character focus instead of scene | Replace the whole prompt with: *"close-up portrait of a wandmaker artisan at her workbench, hands carving a wand under candlelight, painterly storybook style"* |

---

## Variant prompts for *other* slides

### Slide 1 alternative — Tracing minigame hero (action shot)

> **A young wandmaker's hands** in close-up over a workbench, drawing a **glowing rune-circle in the air with a half-finished wand**, the rune trail rendered as a **flowing ribbon of blue light** chased by an encroaching **wisp of red mist** at one edge. Five small **floating glyph markers** (keys / runic stamps) punctuate the rune like beats. Background softly blurred — warm wooden shelves, candlelight. Painterly storybook concept art, dramatic chiaroscuro, warm-cool contrast, 16:9 cinematic.

### Slide 2 alternative — Customer dossier & letter (still life)

> **Top-down still-life of a wandmaker's reading desk.** A **parchment dossier letter** with broken wax seal and quill. Beside it, a **small wooden memo card** with three labelled empty slots: "Purpose", "Personality", "Element". A pair of spectacles, a brass inkwell, scattered dried lavender, a tiny vial of glowing dust. Late afternoon window light. Painterly Ghibli-tone storybook illustration, warm parchment palette, no readable text, 16:9.

### Slide 11 alternative — Evaluation reveal moment (drama)

> **A finished wand suspended mid-air above a velvet display cushion**, glowing softly at its core, sparks of magical light radiating outward. Behind it, a **ghostly hooded customer figure leaning forward in subtle awe**, gloved hand reaching but not yet touching. Volumetric backlight, particles, golden bokeh. Painterly fantasy concept art, theatrical reveal moment, 16:9 cinematic.

---

## ComfyUI inline injection (Z-Image Turbo, matches your existing pipeline)

If you want to use this image in your existing `Assets/StreamingAssets/image_z_image_turbo.json` workflow, inject the **concise SDXL variant** above into the **CLIPTextEncode node `"5"`** (positive prompt) and the **negative prompt** into the corresponding negative-CLIPTextEncode node. Use seed `42` for reproducibility while iterating, then unlock to random for variety.

Output dimensions for slide 1 use: **1920 × 1080** (or render at 1024 × 576 and upscale 1.875× via `ImageUpscaleWithModel` node using a 2× model like `RealESRGAN_x2plus`).
