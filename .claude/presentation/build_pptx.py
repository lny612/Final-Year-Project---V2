"""
Build the .pptx for The Wand Atelier presentation.
Run: py build_pptx.py
Output: WandAtelier_Presentation.pptx in the same folder.
"""
from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.oxml.ns import qn
from lxml import etree
import os

# --------- Theme ---------
CREAM       = RGBColor(0xF4, 0xEC, 0xD8)
CREAM_LITE  = RGBColor(0xFA, 0xF4, 0xE5)
SEPIA       = RGBColor(0x3B, 0x2F, 0x1E)
SEPIA_LITE  = RGBColor(0x6B, 0x55, 0x36)
GOLD        = RGBColor(0xC9, 0xA2, 0x4A)
GOLD_LITE   = RGBColor(0xE6, 0xC9, 0x7B)
INK_BLUE    = RGBColor(0x2E, 0x4A, 0x6E)
INK_RED     = RGBColor(0x8B, 0x2E, 0x2E)
INK_GREEN   = RGBColor(0x2E, 0x6E, 0x4A)
MAGENTA     = RGBColor(0xC4, 0x3B, 0x8C)
CYAN_TINT   = RGBColor(0x4A, 0x8E, 0xB5)
AMBER_TINT  = RGBColor(0xD9, 0xA0, 0x4A)
WHITE       = RGBColor(0xFF, 0xFF, 0xFF)

SERIF = "Garamond"
SERIF_FALLBACK = "Georgia"
SANS  = "Calibri"
MONO  = "Consolas"

# --------- Setup ---------
prs = Presentation()
prs.slide_width  = Inches(13.333)
prs.slide_height = Inches(7.5)
SW = prs.slide_width
SH = prs.slide_height
BLANK = prs.slide_layouts[6]

# --------- Helpers ---------
def add_slide():
    return prs.slides.add_slide(BLANK)

def fill(shape, rgb):
    shape.fill.solid()
    shape.fill.fore_color.rgb = rgb

def no_line(shape):
    shape.line.fill.background()

def line(shape, rgb, width_pt=1.0):
    shape.line.color.rgb = rgb
    shape.line.width = Pt(width_pt)

def set_bg(slide, rgb):
    bg = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SW, SH)
    fill(bg, rgb)
    no_line(bg)
    bg.shadow.inherit = False
    return bg

def add_text(slide, x, y, w, h, text, font=SANS, size=18, bold=False, italic=False,
             color=SEPIA, align=PP_ALIGN.LEFT, anchor=MSO_ANCHOR.TOP, line_spacing=1.15):
    tb = slide.shapes.add_textbox(x, y, w, h)
    tf = tb.text_frame
    tf.word_wrap = True
    tf.margin_left = Inches(0.05)
    tf.margin_right = Inches(0.05)
    tf.margin_top = Inches(0.02)
    tf.margin_bottom = Inches(0.02)
    tf.vertical_anchor = anchor
    p = tf.paragraphs[0]
    p.alignment = align
    p.line_spacing = line_spacing
    r = p.add_run()
    r.text = text
    r.font.name = font
    r.font.size = Pt(size)
    r.font.bold = bold
    r.font.italic = italic
    r.font.color.rgb = color
    return tb

def add_multitext(slide, x, y, w, h, runs, align=PP_ALIGN.LEFT, anchor=MSO_ANCHOR.TOP, line_spacing=1.2):
    """runs: list of dicts: {text, font, size, bold, italic, color, br (bool, new paragraph after)}"""
    tb = slide.shapes.add_textbox(x, y, w, h)
    tf = tb.text_frame
    tf.word_wrap = True
    tf.margin_left = Inches(0.05); tf.margin_right = Inches(0.05)
    tf.margin_top = Inches(0.02); tf.margin_bottom = Inches(0.02)
    tf.vertical_anchor = anchor
    first = True
    p = tf.paragraphs[0]
    p.alignment = align
    p.line_spacing = line_spacing
    for r_def in runs:
        if r_def.get("new_para") and not first:
            p = tf.add_paragraph()
            p.alignment = r_def.get("align", align)
            p.line_spacing = line_spacing
        r = p.add_run()
        r.text = r_def["text"]
        r.font.name = r_def.get("font", SANS)
        r.font.size = Pt(r_def.get("size", 18))
        r.font.bold = r_def.get("bold", False)
        r.font.italic = r_def.get("italic", False)
        r.font.color.rgb = r_def.get("color", SEPIA)
        first = False
    return tb

def add_rect(slide, x, y, w, h, fill_rgb=None, line_rgb=None, line_w=1.0, shape=MSO_SHAPE.RECTANGLE):
    s = slide.shapes.add_shape(shape, x, y, w, h)
    if fill_rgb is None:
        s.fill.background()
    else:
        fill(s, fill_rgb)
    if line_rgb is None:
        no_line(s)
    else:
        line(s, line_rgb, line_w)
    s.shadow.inherit = False
    return s

def add_line(slide, x1, y1, x2, y2, rgb=GOLD, width_pt=1.5):
    ln = slide.shapes.add_connector(1, x1, y1, x2, y2)
    ln.line.color.rgb = rgb
    ln.line.width = Pt(width_pt)
    return ln

def add_speaker_notes(slide, text):
    nf = slide.notes_slide.notes_text_frame
    nf.text = text

def add_header_bar(slide, title, subtitle_num=None):
    """Top header bar with section title in gold."""
    bar = add_rect(slide, Inches(0.5), Inches(0.35), SW - Inches(1.0), Inches(0.05), fill_rgb=GOLD)
    add_text(slide, Inches(0.5), Inches(0.5), SW - Inches(2.0), Inches(0.55),
             title, font=SERIF, size=28, bold=True, color=SEPIA)
    if subtitle_num is not None:
        add_text(slide, SW - Inches(1.5), Inches(0.5), Inches(1.0), Inches(0.55),
                 f"Slide {subtitle_num}", font=SANS, size=11, color=SEPIA_LITE,
                 align=PP_ALIGN.RIGHT)

def add_footer(slide, idx, total=14):
    add_text(slide, Inches(0.5), SH - Inches(0.45), Inches(6.0), Inches(0.3),
             "The Wand Atelier — FYP Presentation", font=SANS, size=10, color=SEPIA_LITE)
    add_text(slide, SW - Inches(2.0), SH - Inches(0.45), Inches(1.5), Inches(0.3),
             f"{idx} / {total}", font=SANS, size=10, color=SEPIA_LITE, align=PP_ALIGN.RIGHT)

def placeholder_image(slide, x, y, w, h, label, sub=None, fill_rgb=CREAM_LITE):
    """Placeholder for screenshots — labeled box."""
    box = add_rect(slide, x, y, w, h, fill_rgb=fill_rgb, line_rgb=GOLD, line_w=1.5)
    add_text(slide, x, y + h/2 - Inches(0.4), w, Inches(0.4),
             f"[ {label} ]", font=SANS, size=14, bold=True, color=SEPIA_LITE,
             align=PP_ALIGN.CENTER)
    if sub:
        add_text(slide, x, y + h/2 + Inches(0.05), w, Inches(0.4),
                 sub, font=SANS, size=10, italic=True, color=SEPIA_LITE,
                 align=PP_ALIGN.CENTER)
    return box

# =====================================================
# SLIDE 1 — Title
# =====================================================
def slide_1():
    s = add_slide()
    set_bg(s, SEPIA)
    # Hero placeholder
    placeholder_image(s, Inches(0.5), Inches(0.5), SW - Inches(1), SH - Inches(2),
                      "HERO IMAGE", sub="parchment morning letter OR tracing minigame mid-trace",
                      fill_rgb=CREAM)
    # Title bar
    bar = add_rect(s, 0, SH - Inches(2.2), SW, Inches(2.2),
                   fill_rgb=SEPIA)
    bar.shadow.inherit = False
    add_text(s, Inches(0.5), SH - Inches(2.0), SW - Inches(1), Inches(0.95),
             "The Wand Atelier", font=SERIF, size=58, bold=True, color=GOLD,
             align=PP_ALIGN.CENTER)
    add_text(s, Inches(0.5), SH - Inches(1.05), SW - Inches(1), Inches(0.45),
             "An AI-Driven Cozy Crafting Game",
             font=SERIF, size=22, italic=True, color=CREAM, align=PP_ALIGN.CENTER)
    add_text(s, Inches(0.5), SH - Inches(0.6), SW - Inches(1), Inches(0.4),
             "Nayoung Lim   ·   Final Year Project   ·   Supervisor: TBA   ·   2026",
             font=SANS, size=13, color=GOLD_LITE, align=PP_ALIGN.CENTER)
    add_speaker_notes(s,
        "[0:00–0:30] Walk on. Wait for the room to settle. Make eye contact with the back row.\n\n"
        "Good morning. My name is Nayoung Lim, and the project I'm presenting today is called The Wand Atelier.\n\n"
        "Before I tell you what it is, I want to ask you one question.\n\n"
        "What if every NPC you met in a game had something they wouldn't tell you, and your job was to figure out what they actually needed?\n\n"
        "That question is what I spent the last year trying to answer."
    )

# =====================================================
# SLIDE 2 — Abstract
# =====================================================
def slide_2():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "Abstract", 2)
    # Three icon columns
    icon_y = Inches(1.45)
    icon_h = Inches(1.6)
    cols = [
        ("✒", "Deduction"),
        ("⚯", "Crafting"),
        ("◈", "Generation"),
    ]
    col_w = Inches(3.5)
    gap = Inches(0.5)
    total_w = col_w * 3 + gap * 2
    start_x = (SW - total_w) // 2
    for i, (glyph, label) in enumerate(cols):
        x = start_x + i * (col_w + gap)
        box = add_rect(s, x, icon_y, col_w, icon_h, fill_rgb=CREAM_LITE, line_rgb=GOLD, line_w=1.5)
        add_text(s, x, icon_y + Inches(0.15), col_w, Inches(0.85),
                 glyph, font=SERIF, size=54, color=GOLD, align=PP_ALIGN.CENTER)
        add_text(s, x, icon_y + Inches(1.05), col_w, Inches(0.45),
                 label, font=SERIF, size=20, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)

    # Bullets
    by = Inches(3.35)
    bullets = [
        "A 7-day fantasy crafting game in Unity 6 where players read AI-generated customer dossiers and craft wands to fit their hidden needs.",
        "OpenAI GPT-4o generates every customer, material, and judge verdict — no two playthroughs share content.",
        "Local ComfyUI server (Z-Image Turbo, INT4 quantised) generates pixel art at runtime on a laptop GPU.",
        "~8,700 lines of C#  ·  34 scripts  ·  8 scenes  ·  4 branching endings.",
    ]
    runs = []
    for i, b in enumerate(bullets):
        runs.append({"text": "✦  ", "font": SERIF, "size": 16, "color": GOLD, "bold": True,
                     "new_para": (i > 0)})
        runs.append({"text": b, "font": SANS, "size": 16, "color": SEPIA})
    add_multitext(s, Inches(0.8), by, SW - Inches(1.6), Inches(3.5), runs, line_spacing=1.45)

    add_footer(s, 2)
    add_speaker_notes(s,
        "[0:30–1:30] The Wand Atelier is a fantasy crafting game built in Unity 6 where you play a wandmaker over a seven-day arc. Every day, customers walk into your shop with a problem. You read their dossier, buy materials from a market, craft a wand, and an AI judges how well your wand fits their needs.\n\n"
        "What makes this project unusual is HOW the content is made. Three things in this game are not hand-authored. They are generated at runtime, every time you play.\n\n"
        "First — the customers. Their names, professions, personalities, and the problems they bring you are written by OpenAI's GPT-4o the moment they walk in.\n\n"
        "Second — the materials. Six fresh materials are generated every morning, each with their own pixel-art portrait drawn by a local ComfyUI server running a quantised image model on my laptop GPU.\n\n"
        "Third — and this is the contribution I'm most proud of — the evaluation. The same language model that invents the customer also judges whether your wand fits them. The AI is both the storyteller and the critic.\n\n"
        "The full game is around eight thousand seven hundred lines of C# across thirty-four scripts, eight scenes, and four branching endings."
    )

# =====================================================
# SLIDE 3 — Introduction: The Problem
# =====================================================
def slide_3():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "The Problem", 3)

    # Two columns
    left_x = Inches(0.7); right_x = Inches(7.0)
    cw = Inches(5.7); cy = Inches(1.45); ch = Inches(4.6)

    # Left col — traditional
    add_rect(s, left_x, cy, cw, ch, fill_rgb=CREAM_LITE, line_rgb=SEPIA_LITE, line_w=0.75)
    add_text(s, left_x + Inches(0.3), cy + Inches(0.2), cw - Inches(0.6), Inches(0.55),
             "Traditional crafting games", font=SERIF, size=22, bold=True, color=SEPIA)
    bullets = [
        "Recipe lookup tables you memorise",
        "NPCs repeat lines verbatim every playthrough",
        "Players optimise around the system, not with it",
        "Mystery dies the moment you crack the formula",
    ]
    runs = []
    for i, b in enumerate(bullets):
        runs.append({"text": "•  ", "size": 14, "color": SEPIA_LITE, "bold": True, "new_para": (i > 0)})
        runs.append({"text": b, "size": 14, "color": SEPIA})
    add_multitext(s, left_x + Inches(0.4), cy + Inches(0.95), cw - Inches(0.8), Inches(2.0), runs, line_spacing=1.55)
    placeholder_image(s, left_x + Inches(0.4), cy + Inches(2.95), cw - Inches(0.8), Inches(1.45),
                      "Stardew/Don't Starve crafting grid", fill_rgb=CREAM)

    # Right col — this project
    add_rect(s, right_x, cy, cw, ch, fill_rgb=GOLD_LITE, line_rgb=GOLD, line_w=1.5)
    add_text(s, right_x + Inches(0.3), cy + Inches(0.2), cw - Inches(0.6), Inches(0.9),
             "What if logic — not lookup —\ndecided the outcome?",
             font=SERIF, size=20, italic=True, bold=True, color=SEPIA, line_spacing=1.15)
    placeholder_image(s, right_x + Inches(0.4), cy + Inches(1.4), cw - Inches(0.8), Inches(2.95),
                      "Generated customer dossier (prose card)",
                      sub="GPT-4o output rendered in-game", fill_rgb=CREAM)

    add_text(s, Inches(0.5), SH - Inches(0.95), SW - Inches(1.0), Inches(0.4),
             "“The mystery dies the moment you crack the formula.”",
             font=SERIF, size=16, italic=True, color=GOLD, align=PP_ALIGN.CENTER)
    add_footer(s, 3)
    add_speaker_notes(s,
        "[1:30–2:30] So, why this project?\n\n"
        "If you've ever played a crafting game — Stardew Valley, Don't Starve, Minecraft — you know the pattern. You learn the recipes. You memorise that iron plus coal makes steel. After a few hours, the system isn't really challenging you any more. You're just executing a lookup table you have in your head.\n\n"
        "The same goes for NPCs. Most game characters say the same lines on every playthrough. The shopkeeper greets you the same way for the hundredth time.\n\n"
        "The problem is — the player ends up optimising AROUND the game's systems, not WITH them. The mystery dies the moment you crack the formula.\n\n"
        "What I wanted to ask was: what if logic, not lookup, decided the outcome? What if every customer was different enough that no recipe could survive contact with them? And what if the only way to succeed was to read carefully, not memorise?"
    )

# =====================================================
# SLIDE 4 — Objectives & Requirements
# =====================================================
def slide_4():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "Project Objectives", 4)

    pillars = [
        ("Deduction", "👁",
         ["Reading & inference matter more than memorisation",
          "Customers hide their real need behind a surface request"]),
        ("Creative Flexibility", "✦",
         ["No fixed recipes — any combo is valid if reasoning is sound",
          "AI evaluator judges intent, not key-value match"]),
        ("Ritual Crafting", "✋",
         ["Crafting feels embodied, not a button click",
          "Skill check with timing, attention, commitment"]),
    ]
    py = Inches(1.45)
    ph = Inches(4.0)
    pw = Inches(3.9)
    gap = Inches(0.4)
    total = pw * 3 + gap * 2
    start = (SW - total) // 2

    for i, (title, glyph, bullets) in enumerate(pillars):
        x = start + i * (pw + gap)
        add_rect(s, x, py, pw, ph, fill_rgb=CREAM_LITE, line_rgb=GOLD, line_w=1.5)
        add_text(s, x, py + Inches(0.25), pw, Inches(0.7),
                 glyph, font=SERIF, size=42, color=GOLD, align=PP_ALIGN.CENTER)
        add_text(s, x, py + Inches(1.05), pw, Inches(0.5),
                 title, font=SERIF, size=22, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
        add_text(s, x + Inches(0.2), py + Inches(1.55) + Inches(0.05), pw - Inches(0.4), Inches(0.05),
                 "", font=SANS, size=10)
        # bullets
        runs = []
        for j, b in enumerate(bullets):
            runs.append({"text": "•  ", "size": 13, "color": GOLD, "bold": True, "new_para": (j > 0)})
            runs.append({"text": b, "size": 13, "color": SEPIA})
        add_multitext(s, x + Inches(0.3), py + Inches(1.7), pw - Inches(0.6), Inches(2.2),
                      runs, line_spacing=1.4)

    # Requirements strip
    rby = py + ph + Inches(0.3)
    add_rect(s, Inches(0.7), rby, SW - Inches(1.4), Inches(0.65), fill_rgb=SEPIA)
    add_text(s, Inches(0.7), rby + Inches(0.1), SW - Inches(1.4), Inches(0.5),
             "Functional Requirements   ·   7-day arc   ·   4 endings   ·   2 AI services concurrent   ·   60 FPS on laptop GPU",
             font=SANS, size=14, bold=True, color=GOLD_LITE, align=PP_ALIGN.CENTER)

    add_footer(s, 4)
    add_speaker_notes(s,
        "[2:30–3:30] That question gave me three project objectives.\n\n"
        "Objective one: Deduction over memorisation. I wanted reading and inference to matter more than rote learning. Customers should hide their real needs behind a surface request, and the player should have to dig those out.\n\n"
        "Objective two: Creative flexibility. No fixed recipes. Any combination of materials should be valid if the player's reasoning is sound. The judge — the AI — has to evaluate intent, not match a key in a dictionary.\n\n"
        "Objective three: Crafting as ritual. The act of making a wand should feel embodied. Not a button click — an actual skill check, with timing and attention.\n\n"
        "To deliver on those, I set technical requirements: a seven-day game arc, four branching endings tied to player reputation, integration with two AI services running concurrently, and a frame rate of sixty FPS on a laptop GPU."
    )

# =====================================================
# SLIDE 5 — Background & Literature Review
# =====================================================
def slide_5():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "Influences & Prior Art", 5)

    # Centre tag
    cx = SW / 2; cy_pt = Inches(3.6)
    centre = add_rect(s, cx - Inches(1.6), cy_pt - Inches(0.45), Inches(3.2), Inches(0.9),
                       fill_rgb=GOLD, line_rgb=SEPIA, line_w=1.5)
    add_text(s, cx - Inches(1.6), cy_pt - Inches(0.32), Inches(3.2), Inches(0.65),
             "THE WAND ATELIER", font=SERIF, size=18, bold=True, color=SEPIA,
             align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)

    # 4 boxes in 2x2 around the centre
    box_w = Inches(4.4); box_h = Inches(1.8)
    positions = [
        (Inches(0.7),  Inches(1.35), "Hogwarts Legacy", "WB Games, 2023",
         "Gestural spell-casting → tracing minigame"),
        (SW - Inches(0.7) - box_w, Inches(1.35), "Papers, Please", "Lucas Pope, 2013",
         "Deduction under time pressure → memo gate"),
        (Inches(0.7),  SH - Inches(2.95), "Disco Elysium", "ZA/UM, 2019",
         "Narrative density per NPC → 7-field schema"),
        (SW - Inches(0.7) - box_w, SH - Inches(2.95), "AI Dungeon / Inworld", "various, 2019–2024",
         "LLM-driven NPCs → GPT-4o customer/material gen"),
    ]
    for x, y, name, src, mapping in positions:
        add_rect(s, x, y, box_w, box_h, fill_rgb=CREAM_LITE, line_rgb=GOLD, line_w=1.0)
        add_text(s, x + Inches(0.25), y + Inches(0.15), box_w - Inches(0.5), Inches(0.45),
                 name, font=SERIF, size=18, bold=True, italic=True, color=SEPIA)
        add_text(s, x + Inches(0.25), y + Inches(0.6), box_w - Inches(0.5), Inches(0.35),
                 src, font=SANS, size=11, color=SEPIA_LITE)
        add_text(s, x + Inches(0.25), y + Inches(1.0), box_w - Inches(0.5), Inches(0.7),
                 mapping, font=SANS, size=13, color=SEPIA, line_spacing=1.25)
        # arrow line to centre
        # (visual cue only — connectors are decorative)
        cx_box = x + box_w / 2
        cy_box = y + box_h / 2
        add_line(s, cx_box, cy_box, SW/2, Inches(3.6), rgb=GOLD_LITE, width_pt=1.0)

    add_text(s, Inches(0.7), SH - Inches(0.95), SW - Inches(1.4), Inches(0.4),
             "Existing LLM-NPC games rarely judge the player's response with the same model. This project closes the loop.",
             font=SERIF, size=14, italic=True, color=GOLD, align=PP_ALIGN.CENTER)
    add_footer(s, 5)
    add_speaker_notes(s,
        "[3:30–4:30] I didn't invent any of these ideas in isolation. Four titles shaped this project.\n\n"
        "Hogwarts Legacy showed me that gestural spell-casting — drawing a shape with the mouse — could feel like real magic. That became the tracing minigame.\n\n"
        "Papers, Please showed me that deduction under time pressure is one of the most engaging mechanics in games. It's where the memo gate idea came from — you have to commit to an interpretation of a person, on the clock.\n\n"
        "Disco Elysium showed me how much narrative density you can pack into a single character if you structure their description well. That gave me my seven-field customer schema.\n\n"
        "And on the AI side — projects like AI Dungeon and Inworld demonstrated that LLMs can power believable NPCs.\n\n"
        "But here's the gap. None of those AI-NPC projects close the loop. They generate the character, but they don't evaluate the player's response. My project does both — generation and evaluation, with the same model. That, as far as I can tell from the literature, is the novel piece."
    )

# =====================================================
# SLIDE 6 — Gap This Project Fills
# =====================================================
def slide_6():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "The Gap This Project Fills", 6)

    # Two-column comparison table
    table_x = Inches(0.8); table_y = Inches(1.45)
    table_w = SW - Inches(1.6); table_h = Inches(3.4)
    col_w = table_w / 2

    # Header row
    add_rect(s, table_x, table_y, col_w, Inches(0.55), fill_rgb=SEPIA)
    add_rect(s, table_x + col_w, table_y, col_w, Inches(0.55), fill_rgb=GOLD)
    add_text(s, table_x, table_y + Inches(0.08), col_w, Inches(0.4),
             "Existing approach", font=SERIF, size=18, bold=True, color=GOLD_LITE, align=PP_ALIGN.CENTER)
    add_text(s, table_x + col_w, table_y + Inches(0.08), col_w, Inches(0.4),
             "This project", font=SERIF, size=18, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)

    rows = [
        ("Recipe lookup tables", "Logic scoring by AI"),
        ("Hand-written NPCs", "LLM-generated dossiers (logical chain)"),
        ("Pre-rendered art", "Runtime pixel art (local GPU)"),
        ("One-true-answer puzzle", "Soft-gate memo (interpretation matters)"),
    ]
    row_h = Inches(0.65)
    for i, (left_t, right_t) in enumerate(rows):
        ry = table_y + Inches(0.55) + i * row_h
        if i % 2 == 0:
            add_rect(s, table_x, ry, table_w, row_h, fill_rgb=CREAM_LITE, line_rgb=SEPIA_LITE, line_w=0.5)
        else:
            add_rect(s, table_x, ry, table_w, row_h, fill_rgb=CREAM, line_rgb=SEPIA_LITE, line_w=0.5)
        add_text(s, table_x + Inches(0.3), ry + Inches(0.1), col_w - Inches(0.6), Inches(0.5),
                 left_t, font=SANS, size=15, color=SEPIA_LITE, anchor=MSO_ANCHOR.MIDDLE)
        add_text(s, table_x + col_w + Inches(0.3), ry + Inches(0.1), col_w - Inches(0.6), Inches(0.5),
                 right_t, font=SANS, size=15, bold=True, color=SEPIA, anchor=MSO_ANCHOR.MIDDLE)

    # Bottom callout — three reasons
    cy = Inches(5.4)
    reasons = [
        ("①", "Replayability without scaling content cost"),
        ("②", "AI as both narrator AND judge — closed feedback loop"),
        ("③", "Viable production pattern for indie devs on cheap GPUs"),
    ]
    rw = (SW - Inches(1.6) - Inches(0.6)) / 3
    rx = Inches(0.8)
    for i, (num, text) in enumerate(reasons):
        x = rx + i * (rw + Inches(0.3))
        add_rect(s, x, cy, rw, Inches(1.3), fill_rgb=GOLD_LITE, line_rgb=GOLD, line_w=1.0)
        add_text(s, x, cy + Inches(0.1), rw, Inches(0.5),
                 num, font=SERIF, size=28, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
        add_text(s, x + Inches(0.15), cy + Inches(0.65), rw - Inches(0.3), Inches(0.6),
                 text, font=SANS, size=12, color=SEPIA, align=PP_ALIGN.CENTER, line_spacing=1.2)

    add_footer(s, 6)
    add_speaker_notes(s,
        "[4:30–5:30] Let me put that gap in a concrete table.\n\n"
        "Where existing crafting games use recipe lookup, my game uses logic scoring by an AI. Where existing games use hand-written NPCs, my game uses LLM-generated dossiers with a strict logical chain. Where existing games use pre-rendered art, my game uses runtime pixel art generated locally. Where existing puzzles have one true answer, my memo system uses a soft gate — your interpretation matters, even if it's wrong.\n\n"
        "Why is any of this desirable? Three reasons.\n\n"
        "One: infinite replayability without scaling content costs. The art and writing budget is essentially zero per playthrough.\n\n"
        "Two: because the AI is both narrator and judge, the player gets feedback that's coherent with the world. The same voice that gave the customer their problem tells you whether you solved it.\n\n"
        "Three: this is a viable production pattern for small studios. I'm running a quantised model on a laptop with eight gigabytes of VRAM. If a final-year student can do this, an indie studio absolutely can."
    )

# =====================================================
# SLIDE 7 — Methodology Overview: Architecture
# =====================================================
def slide_7():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "System Architecture", 7)

    # Architecture diagram — three nodes
    node_w = Inches(2.8); node_h = Inches(1.5)
    cy_n = Inches(1.85)
    # Centre — Unity
    cx_unity = SW / 2 - node_w / 2
    add_rect(s, cx_unity, cy_n, node_w, node_h, fill_rgb=GOLD, line_rgb=SEPIA, line_w=1.5)
    add_text(s, cx_unity, cy_n + Inches(0.2), node_w, Inches(0.5),
             "Unity Client", font=SERIF, size=20, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
    add_text(s, cx_unity, cy_n + Inches(0.7), node_w, Inches(0.4),
             "Unity 6 + URP", font=SANS, size=12, color=SEPIA, align=PP_ALIGN.CENTER)
    add_text(s, cx_unity, cy_n + Inches(1.05), node_w, Inches(0.4),
             "GameManager singleton", font=SANS, size=11, italic=True, color=SEPIA_LITE, align=PP_ALIGN.CENTER)
    # Right — OpenAI
    rx = SW - Inches(0.7) - node_w
    add_rect(s, rx, cy_n, node_w, node_h, fill_rgb=CREAM_LITE, line_rgb=SEPIA, line_w=1.5)
    add_text(s, rx, cy_n + Inches(0.2), node_w, Inches(0.5),
             "OpenAI GPT-4o", font=SERIF, size=18, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
    add_text(s, rx, cy_n + Inches(0.7), node_w, Inches(0.7),
             "~6 calls / day\n customer · material · wand · judge",
             font=SANS, size=11, color=SEPIA_LITE, align=PP_ALIGN.CENTER, line_spacing=1.2)
    # Left — ComfyUI
    lx = Inches(0.7)
    add_rect(s, lx, cy_n, node_w, node_h, fill_rgb=CREAM_LITE, line_rgb=SEPIA, line_w=1.5)
    add_text(s, lx, cy_n + Inches(0.2), node_w, Inches(0.5),
             "ComfyUI (local)", font=SERIF, size=18, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
    add_text(s, lx, cy_n + Inches(0.7), node_w, Inches(0.7),
             "Z-Image Turbo · INT4\n8 GB VRAM laptop GPU",
             font=SANS, size=11, color=SEPIA_LITE, align=PP_ALIGN.CENTER, line_spacing=1.2)

    # Connectors with labels
    add_line(s, cx_unity, cy_n + Inches(0.6), lx + node_w, cy_n + Inches(0.6), GOLD, 2.0)
    add_line(s, cx_unity, cy_n + Inches(0.95), lx + node_w, cy_n + Inches(0.95), GOLD_LITE, 1.0)
    add_text(s, lx + node_w, cy_n - Inches(0.05), Inches(2.0), Inches(0.3),
             "workflow JSON →", font=SANS, size=10, italic=True, color=SEPIA_LITE)
    add_text(s, lx + node_w, cy_n + Inches(1.18), Inches(2.0), Inches(0.3),
             "← PNG bytes", font=SANS, size=10, italic=True, color=SEPIA_LITE)

    add_line(s, cx_unity + node_w, cy_n + Inches(0.6), rx, cy_n + Inches(0.6), GOLD, 2.0)
    add_line(s, cx_unity + node_w, cy_n + Inches(0.95), rx, cy_n + Inches(0.95), GOLD_LITE, 1.0)
    add_text(s, cx_unity + node_w, cy_n - Inches(0.05), Inches(2.5), Inches(0.3),
             "→ prompt + dossier",  font=SANS, size=10, italic=True, color=SEPIA_LITE, align=PP_ALIGN.RIGHT)
    add_text(s, cx_unity + node_w, cy_n + Inches(1.18), Inches(2.5), Inches(0.3),
             "← JSON verdict / dossier", font=SANS, size=10, italic=True, color=SEPIA_LITE, align=PP_ALIGN.RIGHT)

    # Scene flow strip
    sfy = Inches(4.1)
    add_text(s, Inches(0.7), sfy - Inches(0.05), SW - Inches(1.4), Inches(0.4),
             "8-Scene Game Loop", font=SERIF, size=15, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
    scenes = ["Title", "Morning", "Customer", "Material", "Crafting", "Minigame", "Evaluation", "Ending"]
    sw = (SW - Inches(1.4) - Inches(0.7) * 7) / 8
    for i, name in enumerate(scenes):
        x = Inches(0.7) + i * (sw + Inches(0.1))
        is_highlight = (name == "Minigame")
        add_rect(s, x, sfy + Inches(0.4), sw, Inches(0.9),
                 fill_rgb=GOLD if is_highlight else CREAM_LITE,
                 line_rgb=GOLD if is_highlight else SEPIA_LITE, line_w=1.0)
        add_text(s, x, sfy + Inches(0.65), sw, Inches(0.45),
                 name, font=SERIF, size=11, bold=True,
                 color=SEPIA, align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)

    # Stats footer
    add_rect(s, Inches(0.7), SH - Inches(1.5), SW - Inches(1.4), Inches(0.6), fill_rgb=SEPIA)
    add_text(s, Inches(0.7), SH - Inches(1.42), SW - Inches(1.4), Inches(0.45),
             "~8,700 LoC   ·   34 scripts   ·   6 GPT-4o calls/day   ·   7 ComfyUI calls/day   ·   60 FPS @ 1080p",
             font=SANS, size=13, bold=True, color=GOLD_LITE, align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)

    add_footer(s, 7)
    add_speaker_notes(s,
        "[5:30–6:30] Let's go technical.\n\n"
        "The system has three components. The Unity client in the centre, OpenAI GPT-4o on the right, and ComfyUI running locally on the left.\n\n"
        "GPT-4o handles all language work — about six API calls per day in the game. Customer generation, material generation, wand synthesis, and evaluation. Each call is a UnityWebRequest POST to the chat completions endpoint, parsed with Newtonsoft JSON.\n\n"
        "ComfyUI handles all image generation — about seven images per day. I'm using a model called Z-Image Turbo with Nunchaku INT4 quantisation, which is the only way I can run a diffusion model on this hardware fast enough to be usable.\n\n"
        "Cross-scene state lives in a singleton called GameManager — DontDestroyOnLoad, holds everything from the current customer to the player's gold, reputation, and which day it is.\n\n"
        "Eight scenes total: title, morning, customer, market, crafting, the minigame, evaluation, and ending. Eight thousand seven hundred lines of C#. Thirty-four scripts. All flat — no subdirectories — to keep it simple."
    )

# =====================================================
# SLIDE 8 — Customer Deduction Loop
# =====================================================
def slide_8():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "The Customer Deduction Loop", 8)

    # Two panels
    panel_y = Inches(1.45); panel_h = Inches(3.6)
    pw = Inches(5.4)
    lx = Inches(0.7); rx = SW - Inches(0.7) - pw

    # Left — dossier
    placeholder_image(s, lx, panel_y, pw, panel_h,
                      "Dossier panel screenshot",
                      sub="from CustomerGeneratorTest scene")
    add_text(s, lx, panel_y + panel_h + Inches(0.05), pw, Inches(0.4),
             "Generated by GPT-4o · 7-field logical chain",
             font=SANS, size=11, italic=True, color=SEPIA_LITE, align=PP_ALIGN.CENTER)

    # Right — memo card
    placeholder_image(s, rx, panel_y, pw, panel_h,
                      "Memo card with 3 slots filled",
                      sub="Purpose · Personality · Element")
    add_text(s, rx, panel_y + panel_h + Inches(0.05), pw, Inches(0.4),
             "Player's interpretation — what they carry forward",
             font=SANS, size=11, italic=True, color=SEPIA_LITE, align=PP_ALIGN.CENTER)

    # Arrow between
    arrow_y = panel_y + panel_h / 2
    add_text(s, lx + pw, arrow_y - Inches(0.3), rx - (lx + pw), Inches(0.6),
             "→", font=SERIF, size=44, bold=True, color=GOLD,
             align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)
    add_text(s, lx + pw, arrow_y + Inches(0.3), rx - (lx + pw), Inches(0.4),
             "click 3 words", font=SANS, size=11, italic=True, color=SEPIA_LITE,
             align=PP_ALIGN.CENTER)

    # Schema chain
    sy = Inches(5.55)
    add_rect(s, Inches(0.7), sy, SW - Inches(1.4), Inches(0.6), fill_rgb=SEPIA)
    add_text(s, Inches(0.7), sy + Inches(0.08), SW - Inches(1.4), Inches(0.45),
             "schoolOfMagic  →  profession  →  request  →  trueGoal  →  constraint",
             font=MONO, size=14, bold=True, color=GOLD_LITE, align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)

    # Pull quote
    add_text(s, Inches(0.7), SH - Inches(0.85), SW - Inches(1.4), Inches(0.4),
             "“The constraint must be cruelest when it activates exactly when they need control most.” — GDD §3.2",
             font=SERIF, size=13, italic=True, color=GOLD, align=PP_ALIGN.CENTER)

    add_footer(s, 8)
    add_speaker_notes(s,
        "[6:30–7:30] Let me walk you through one customer.\n\n"
        "When a customer walks in, GPT-4o produces a seven-field dossier under a strict structural rule: schoolOfMagic flowing into profession flowing into request flowing into trueGoal flowing into constraint. Each field has to follow logically from the last.\n\n"
        "Here's the design rule I gave the model — and I quote from my GDD — the constraint must be cruelest when it activates exactly when they need control most.\n\n"
        "So you might get a hydromancy field medic whose magic amplifies when she's emotionally distressed. The request says she needs precise pressure control. The true goal is to save more patients. The constraint is that the more lives are at stake, the more her magic spirals out.\n\n"
        "Now — and this is the key mechanic — the player can't carry the full dossier with them. They have to read the prose and click three words into a memo card with three slots: Purpose, Personality, Element.\n\n"
        "That memo — not the original dossier — is the only reference they have in the market.\n\n"
        "A wrong memo misleads every choice that follows. There is no correct memo, just consequences."
    )

# =====================================================
# SLIDE 9 — Tracing Minigame
# =====================================================
def slide_9():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "The Crafting Ritual", 9)

    # Three round screenshots
    sy = Inches(1.4)
    sw = Inches(3.95); sh = Inches(2.4)
    gap = Inches(0.25)
    start_x = (SW - (sw * 3 + gap * 2)) / 2
    rounds = [
        ("Round 1", "5 Tap gates", AMBER_TINT),
        ("Round 2", "4 Tap + 1 Hold", CYAN_TINT),
        ("Round 3", "3 Tap + 1 Hold + 1 Accent", MAGENTA),
    ]
    for i, (name, desc, c) in enumerate(rounds):
        x = start_x + i * (sw + gap)
        placeholder_image(s, x, sy, sw, sh, f"{name} screenshot",
                          sub=desc, fill_rgb=CREAM_LITE)
        # accent strip on top of each
        add_rect(s, x, sy, sw, Inches(0.08), fill_rgb=c)

    # Gate-type icon strip
    gy = sy + sh + Inches(0.3)
    gate_w = Inches(3.95); gate_h = Inches(0.95)
    gates = [
        ("TAP",    "Press key once",                 AMBER_TINT, "■"),
        ("HOLD",   "Press & hold 0.9s",              CYAN_TINT,  "■"),
        ("ACCENT", "Press → flick mouse → return",   MAGENTA,    "◆"),
    ]
    for i, (name, desc, c, glyph) in enumerate(gates):
        x = start_x + i * (gate_w + gap)
        add_rect(s, x, gy, gate_w, gate_h, fill_rgb=CREAM_LITE, line_rgb=c, line_w=2.0)
        # Gate marker
        add_text(s, x + Inches(0.2), gy + Inches(0.15), Inches(0.7), gate_h - Inches(0.3),
                 glyph, font=SERIF, size=32, color=c, align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)
        # Name + desc
        add_text(s, x + Inches(0.95), gy + Inches(0.1), gate_w - Inches(1.1), Inches(0.4),
                 name, font=SERIF, size=18, bold=True, color=SEPIA)
        add_text(s, x + Inches(0.95), gy + Inches(0.5), gate_w - Inches(1.1), Inches(0.4),
                 desc, font=SANS, size=12, color=SEPIA_LITE)

    # Algorithm + reward strip
    by = gy + gate_h + Inches(0.25)
    bw = SW - Inches(1.4)
    add_rect(s, Inches(0.7), by, bw, Inches(0.85), fill_rgb=SEPIA)
    add_text(s, Inches(0.85), by + Inches(0.08), bw - Inches(0.3), Inches(0.4),
             "Procedural path: Catmull-Rom interpolation  ·  Custom arc-length overlap validator (no double-circle re-crossings)",
             font=SANS, size=12, color=GOLD_LITE)
    add_text(s, Inches(0.85), by + Inches(0.45), bw - Inches(0.3), Inches(0.4),
             "Reward multiplier:    A = 1.0×        B = 0.85×        C = 0.7×        F = 0.4×",
             font=MONO, size=12, bold=True, color=GOLD)

    add_footer(s, 9)
    add_speaker_notes(s,
        "[7:30–9:00] After the player chooses materials, they enter the crafting ritual. This is the part that took me the longest to build.\n\n"
        "It's three rounds. Each round, you trace a glowing rune path with your mouse. The trail fills BLUE as you trace it correctly. A RED MIST chases you from the start at a constant speed. If the red catches up before you finish, the round is lost.\n\n"
        "But pure tracing got boring fast in playtests. So I added gates — interactive checkpoints along the path. Three types.\n\n"
        "Tap gates ask you to press a displayed key once. Hold gates ask you to hold a key for nine-tenths of a second while a green ring fills. Accent gates ask you to press a key and FLICK the mouse in one of eight compass directions.\n\n"
        "Round one is five tap gates. Round two adds a hold. Round three adds an accent. The complexity escalates.\n\n"
        "The single most important design rule here: the red mist never pauses while a gate is being resolved. Every Hold and every Accent costs real time. The player can't stand still and think.\n\n"
        "Technically, the path itself is built from six waypoints using Catmull-Rom interpolation for smoothness, and I wrote a custom arc-length overlap validator to guarantee the path never re-crosses itself in a way that would unfairly trap the cursor.\n\n"
        "The final grade — A, B, C, or F — multiplies the reward by 1.0×, 0.85×, 0.7×, or 0.4×."
    )

# =====================================================
# SLIDE 10 — GPT-4o as Judge
# =====================================================
def slide_10():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "GPT-4o as Judge", 10)

    # Left — prompt
    py = Inches(1.45)
    pw = Inches(5.9); ph = Inches(2.7)
    lx = Inches(0.7); rx = SW - Inches(0.7) - pw

    add_rect(s, lx, py, pw, ph, fill_rgb=SEPIA, line_rgb=GOLD, line_w=1.0)
    add_text(s, lx + Inches(0.2), py + Inches(0.15), pw - Inches(0.4), Inches(0.4),
             "System prompt (excerpt)", font=SERIF, size=13, bold=True, italic=True, color=GOLD)
    prompt_text = (
        "You are a senior wandmaker evaluating wand ↔ customer match.\n"
        "Understand the tension between TRUE GOAL and CONSTRAINT.\n\n"
        "Scoring rules:\n"
        "  • Addresses request only         →  40–60\n"
        "  • Addresses true goal            →  60–75\n"
        "  • True goal + accounts for       \n"
        "    constraint                     →  75–95\n"
        "  • Resolves tension creatively    →  90–100\n"
        "  • Conflicts with constraint      →  20–40"
    )
    add_text(s, lx + Inches(0.2), py + Inches(0.55), pw - Inches(0.4), ph - Inches(0.7),
             prompt_text, font=MONO, size=11, color=GOLD_LITE, line_spacing=1.25)

    # Right — JSON
    add_rect(s, rx, py, pw, ph, fill_rgb=CREAM_LITE, line_rgb=GOLD, line_w=1.0)
    add_text(s, rx + Inches(0.2), py + Inches(0.15), pw - Inches(0.4), Inches(0.4),
             "Sample JSON response", font=SERIF, size=13, bold=True, italic=True, color=SEPIA)
    json_text = (
        '{\n'
        '  "matchScore": 73,\n'
        '  "verdict": "A thoughtful choice",\n'
        '  "whatWorked": "The leviathan-scale\n'
        '                 core caps her overflow",\n'
        '  "whatMissed": "Doesn\'t address the\n'
        '                 emotional distress trigger",\n'
        '  "customerReaction":\n'
        '     "This… might actually work."\n'
        '}'
    )
    add_text(s, rx + Inches(0.2), py + Inches(0.55), pw - Inches(0.4), ph - Inches(0.7),
             json_text, font=MONO, size=11, color=SEPIA, line_spacing=1.25)

    # Rubric strip
    ry = py + ph + Inches(0.25)
    add_text(s, Inches(0.7), ry, SW - Inches(1.4), Inches(0.4),
             "Score-band rubric", font=SERIF, size=15, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
    bars = [
        ("40–60",   "Request only",              SEPIA_LITE,   0.20),
        ("60–75",   "True goal addressed",       INK_BLUE,     0.40),
        ("75–95",   "+ Accounts for constraint", INK_GREEN,    0.65),
        ("90–100",  "+ Resolves tension",        GOLD,         0.95),
    ]
    by = ry + Inches(0.45)
    bh = Inches(0.5)
    bw_total = SW - Inches(1.4)
    bx = Inches(0.7)
    seg_w = bw_total / 4
    for i, (band, desc, c, _) in enumerate(bars):
        x = bx + i * seg_w
        add_rect(s, x, by, seg_w, bh, fill_rgb=c, line_rgb=SEPIA, line_w=0.5)
        add_text(s, x, by + Inches(0.05), seg_w, Inches(0.2),
                 band, font=MONO, size=11, bold=True, color=CREAM, align=PP_ALIGN.CENTER)
        add_text(s, x, by + Inches(0.27), seg_w, Inches(0.22),
                 desc, font=SANS, size=10, color=CREAM, align=PP_ALIGN.CENTER)

    # Reward formula footer
    fy = by + bh + Inches(0.2)
    add_rect(s, Inches(0.7), fy, SW - Inches(1.4), Inches(0.55), fill_rgb=GOLD_LITE, line_rgb=GOLD, line_w=1.0)
    add_text(s, Inches(0.7), fy + Inches(0.1), SW - Inches(1.4), Inches(0.4),
             "gold = 150 × (matchScore / 100) × qualityMultiplier        score < 40 → −10 reputation",
             font=MONO, size=13, bold=True, color=SEPIA, align=PP_ALIGN.CENTER, anchor=MSO_ANCHOR.MIDDLE)

    add_footer(s, 10)
    add_speaker_notes(s,
        "[9:00–10:30] Now the most novel part of the project.\n\n"
        "When the player submits their wand, the same AI that invented the customer judges the wand. I send GPT-4o a structured prompt with the customer dossier, the wand name, and three attributes — and I ask for a JSON verdict.\n\n"
        "The scoring rubric is explicit: if the wand only addresses the surface request, score forty to sixty. If it addresses the customer's true goal, sixty to seventy-five. If it addresses the true goal AND accounts for the constraint, seventy-five to ninety-five. If it creatively resolves the tension between true goal and constraint, ninety to one hundred.\n\n"
        "The model returns a JSON object with matchScore, whatWorked, whatMissed, and a customerReaction line — verbatim dialogue from the imagined NPC.\n\n"
        "That score plugs into the reward formula: gold equals one-fifty times score-over-one-hundred times quality multiplier.\n\n"
        "If the score is below forty, you don't just earn nothing — you LOSE ten reputation. Bad wands have consequences."
    )

# =====================================================
# SLIDE 11 — Demo Walkthrough
# =====================================================
def slide_11():
    s = add_slide()
    set_bg(s, SEPIA)
    add_text(s, Inches(0.5), Inches(0.5), SW - Inches(1), Inches(0.7),
             "LIVE DEMO", font=SERIF, size=44, bold=True, color=GOLD,
             align=PP_ALIGN.CENTER)

    # Demo placeholder
    placeholder_image(s, Inches(1.5), Inches(1.5), SW - Inches(3), Inches(4.5),
                      "DEMO  /  90-sec screen capture",
                      sub="full round: morning letter → dossier → memo → market → crafting → tracing → evaluation reveal",
                      fill_rgb=CREAM)

    # Path checklist
    py = SH - Inches(1.4)
    add_text(s, Inches(0.7), py, SW - Inches(1.4), Inches(0.4),
             "Demo path",
             font=SERIF, size=14, bold=True, italic=True, color=GOLD_LITE,
             align=PP_ALIGN.CENTER)
    steps = ["Morning letter", "Dossier", "Memo", "Market", "Crafting", "Tracing", "Evaluation"]
    sw = (SW - Inches(1.4)) / len(steps)
    for i, t in enumerate(steps):
        x = Inches(0.7) + i * sw
        add_text(s, x, py + Inches(0.4), sw, Inches(0.4),
                 t, font=SANS, size=11, color=GOLD_LITE,
                 align=PP_ALIGN.CENTER)
        if i < len(steps) - 1:
            add_text(s, x + sw - Inches(0.15), py + Inches(0.4), Inches(0.3), Inches(0.4),
                     "›", font=SERIF, size=14, bold=True, color=GOLD,
                     align=PP_ALIGN.CENTER)

    add_footer(s, 11)
    add_speaker_notes(s,
        "[10:30–12:00] Let me show you what this looks like in motion.\n\n"
        "[Start the demo OR play the pre-recorded clip. Narrate over it.]\n\n"
        "This is morning of day three. The player gets a letter from their landlord — rent's due tonight.\n\n"
        "Now we're in the customer scene. Notice the dossier on the left. Three slots are empty on the right. The player clicks 'war' into Purpose, 'impatient' into Personality, 'fire' into Element.\n\n"
        "The market — six materials, three cores and three woods. Notice the small glyph next to materials whose attributes match the memo. That's a hint, not a guarantee.\n\n"
        "Now crafting. The player slots two cores and a wood. Confirm.\n\n"
        "The minigame begins. Round one — easy, all taps. Round two — there's the hold gate, the player has to wait while the red mist closes in. Round three — accent gate, mouse flick, just barely makes it.\n\n"
        "And finally — the evaluation reveal. The wand image appears, the score animates up, the AI's verdict prints itself out character by character.\n\n"
        "That verdict was invented in the same generation pass that gave us the customer. The narrative is closed-loop.\n\n"
        "[If live demo fails, fall back to the screen capture — never apologise twice; move on.]"
    )

# =====================================================
# SLIDE 12 — Results & Critique
# =====================================================
def slide_12():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "Results & Critique", 12)

    qy = Inches(1.45)
    qh = Inches(2.55); qw = (SW - Inches(1.4) - Inches(0.3)) / 2
    qx_l = Inches(0.7); qx_r = qx_l + qw + Inches(0.3)

    # Top-left — Quantitative
    add_rect(s, qx_l, qy, qw, qh, fill_rgb=CREAM_LITE, line_rgb=INK_GREEN, line_w=2.0)
    add_text(s, qx_l + Inches(0.25), qy + Inches(0.15), qw - Inches(0.5), Inches(0.4),
             "QUANTITATIVE", font=SERIF, size=14, bold=True, color=INK_GREEN)
    quant = [
        "~8,700 LoC  ·  60 FPS @ 1080p",
        "GPT-4o latency: 4–7s avg",
        "ComfyUI image latency: 8–12s avg",
        "Parallel execution hides ~70% of generation time",
    ]
    runs = []
    for i, b in enumerate(quant):
        runs.append({"text": "▸  ", "size": 13, "color": GOLD, "bold": True, "new_para": (i > 0)})
        runs.append({"text": b, "size": 13, "color": SEPIA})
    add_multitext(s, qx_l + Inches(0.4), qy + Inches(0.6), qw - Inches(0.7), qh - Inches(0.7),
                  runs, line_spacing=1.5)

    # Top-right — Qualitative
    add_rect(s, qx_r, qy, qw, qh, fill_rgb=CREAM_LITE, line_rgb=INK_BLUE, line_w=2.0)
    add_text(s, qx_r + Inches(0.25), qy + Inches(0.15), qw - Inches(0.5), Inches(0.4),
             "QUALITATIVE  ·  playtester quotes", font=SERIF, size=14, bold=True, color=INK_BLUE)
    quotes = [
        ("“Feels like reading a real letter.”", "Memo loop"),
        ("“The mist never letting up was stressful in a good way.”", "Hold gates"),
    ]
    runs = []
    for i, (q, t) in enumerate(quotes):
        if i > 0:
            runs.append({"text": "\n", "size": 8, "color": SEPIA, "new_para": True})
        runs.append({"text": q + "\n", "size": 14, "italic": True, "color": SEPIA, "new_para": (i > 0)})
        runs.append({"text": "— " + t, "size": 11, "color": SEPIA_LITE})
    add_multitext(s, qx_r + Inches(0.4), qy + Inches(0.6), qw - Inches(0.7), qh - Inches(0.7),
                  runs, line_spacing=1.4)

    # Bottom-left — Limitations
    qy2 = qy + qh + Inches(0.2)
    add_rect(s, qx_l, qy2, qw, qh, fill_rgb=CREAM_LITE, line_rgb=INK_RED, line_w=2.0)
    add_text(s, qx_l + Inches(0.25), qy2 + Inches(0.15), qw - Inches(0.5), Inches(0.4),
             "LIMITATIONS", font=SERIF, size=14, bold=True, color=INK_RED)
    lims = [
        "API cost ~$0.04/round",
        "GPT verdicts ±10 points across runs (non-determinism)",
        "7-day arc tight for relationship-building",
    ]
    runs = []
    for i, b in enumerate(lims):
        runs.append({"text": "▸  ", "size": 13, "color": INK_RED, "bold": True, "new_para": (i > 0)})
        runs.append({"text": b, "size": 13, "color": SEPIA})
    add_multitext(s, qx_l + Inches(0.4), qy2 + Inches(0.6), qw - Inches(0.7), qh - Inches(0.7),
                  runs, line_spacing=1.5)

    # Bottom-right — Validation
    add_rect(s, qx_r, qy2, qw, qh, fill_rgb=CREAM_LITE, line_rgb=GOLD, line_w=2.0)
    add_text(s, qx_r + Inches(0.25), qy2 + Inches(0.15), qw - Inches(0.5), Inches(0.4),
             "VALIDATION  ·  small A/B", font=SERIF, size=14, bold=True, color=GOLD)
    val = [
        "Random vs. structured-prompt customers",
        "Structured → 3× more constraint-aware wand submissions",
        "Sample size small; signal directional only",
    ]
    runs = []
    for i, b in enumerate(val):
        runs.append({"text": "▸  ", "size": 13, "color": GOLD, "bold": True, "new_para": (i > 0)})
        runs.append({"text": b, "size": 13, "color": SEPIA})
    add_multitext(s, qx_r + Inches(0.4), qy2 + Inches(0.6), qw - Inches(0.7), qh - Inches(0.7),
                  runs, line_spacing=1.5)

    add_footer(s, 12)
    add_speaker_notes(s,
        "[12:00–13:30] Let me be honest about what worked and what didn't.\n\n"
        "Quantitatively — the build is around eight thousand seven hundred lines of code, runs at sixty FPS on a laptop, and the average GPT-4o latency is four to seven seconds, with image generation between eight and twelve seconds. The single most impactful UX decision in the whole project is the parallel-execution pattern: the wand image generates while the player traces the rune. This hides about seventy percent of the latency.\n\n"
        "Qualitatively — playtesters reported two things consistently. The memo loop feels like reading a real letter, and the Hold gates with the mist closing in were stressful in a good way. That's the design objective being met.\n\n"
        "Limitations — let me name three.\n"
        "One: API cost. Each round costs me about four cents in OpenAI calls. That's not viable for free-to-play.\n"
        "Two: the AI judge is non-deterministic. The same wand can score plus-or-minus ten across runs.\n"
        "Three: the seven-day arc is tight. Players want more time to build a relationship with the world.\n\n"
        "Validation — I ran a small A/B between random-prompt customers and structured-prompt customers. The structured ones produced about three times more constraint-aware wand submissions from playtesters. Small sample, but the signal is real."
    )

# =====================================================
# SLIDE 13 — Conclusion & Future Work
# =====================================================
def slide_13():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "Conclusion & Future Work", 13)

    cy = Inches(1.45)
    ch = Inches(4.5)
    cw = (SW - Inches(1.4) - Inches(0.4)) / 3
    cx0 = Inches(0.7)

    cols = [
        ("✓ DONE", INK_GREEN, [
            "7-day arc + 4 endings",
            "Memo deduction gate",
            "3-gate minigame (Tap/Hold/Accent)",
            "Dual-AI pipeline (GPT-4o + ComfyUI)",
            "Parallel-execution latency masking",
            "UI Toolkit migration (6/8 scenes)",
        ]),
        ("✦ NEXT 6 MONTHS", GOLD, [
            "2 customers/day",
            "Commission system (1.75× orders)",
            "Reputation-tier customer biasing",
            "Deterministic scoring rubric",
            "TTS voice-acted letters",
        ]),
        ("❖ LONG TERM", INK_BLUE, [
            "Procedural rune shapes",
            "Multi-day customer threads",
            "Modder API",
            "Steam distribution (bundled inference)",
        ]),
    ]
    for i, (title, color, items) in enumerate(cols):
        x = cx0 + i * (cw + Inches(0.2))
        add_rect(s, x, cy, cw, ch, fill_rgb=CREAM_LITE, line_rgb=color, line_w=2.0)
        add_text(s, x, cy + Inches(0.2), cw, Inches(0.55),
                 title, font=SERIF, size=18, bold=True, color=color, align=PP_ALIGN.CENTER)
        runs = []
        for j, it in enumerate(items):
            runs.append({"text": "•  ", "size": 13, "color": color, "bold": True, "new_para": (j > 0)})
            runs.append({"text": it, "size": 13, "color": SEPIA})
        add_multitext(s, x + Inches(0.3), cy + Inches(0.95), cw - Inches(0.6), ch - Inches(1.1),
                      runs, line_spacing=1.55)

    # Footer line
    add_text(s, Inches(0.7), SH - Inches(0.95), SW - Inches(1.4), Inches(0.4),
             "“Generative AI as both author and judge — closing the narrative loop.”",
             font=SERIF, size=15, italic=True, color=GOLD, align=PP_ALIGN.CENTER)
    add_footer(s, 13)
    add_speaker_notes(s,
        "[13:30–14:30] The contribution of this project is a working demonstration that small teams CAN use generative AI for both content and evaluation in a real-time game, and that careful UX — specifically parallel execution — can hide the latency cost.\n\n"
        "What's done: the seven-day arc, four endings, the memo gate, the three-gate minigame, the dual-AI pipeline, the parallel-execution pattern, and a near-complete UI Toolkit migration.\n\n"
        "What I'd build in the next six months: two customers per day, a commission system that pays a one-point-seven-five times multiplier for special orders, reputation-tier customer biasing — high-rep players see royalty, low-rep players see brigands — a deterministic scoring rubric to fix the non-determinism problem, and voice-acted morning letters via TTS.\n\n"
        "Long-term — procedural rune shapes for the minigame, multi-day customer threads where the same NPC returns, and a modder API so the community can extend the prompt library."
    )

# =====================================================
# SLIDE 14 — Appendices, References, Thank You
# =====================================================
def slide_14():
    s = add_slide()
    set_bg(s, CREAM)
    add_header_bar(s, "Appendices & References", 14)

    by = Inches(1.45)
    bh = Inches(3.7)
    bw = (SW - Inches(1.4) - Inches(0.4)) / 3
    bx0 = Inches(0.7)

    boxes = [
        ("SOFTWARE STACK", [
            "Unity 6 (6000.0.58f2)",
            "Universal Render Pipeline (URP)",
            "OpenAI GPT-4o",
            "ComfyUI + Nunchaku INT4",
            "Z-Image Turbo",
            "Newtonsoft.Json",
            "UI Toolkit · Input System",
        ]),
        ("CODE", [
            "34 scripts in Assets/Scripts/",
            "~8,700 LoC of C#",
            "Flat structure (no subdirs)",
            "GitHub repository:",
            "[QR / link placeholder]",
        ]),
        ("REFERENCES", [
            "Hogwarts Legacy (Avalanche / WB, 2023)",
            "Papers, Please (Pope, 2013)",
            "Disco Elysium (ZA/UM, 2019)",
            "Catmull & Rom, A Class of Local Interpolating Splines, 1974",
            "OpenAI GPT-4o model card (2024)",
            "Nunchaku quantization — INT4 diffusion",
        ]),
    ]
    for i, (title, items) in enumerate(boxes):
        x = bx0 + i * (bw + Inches(0.2))
        add_rect(s, x, by, bw, bh, fill_rgb=CREAM_LITE, line_rgb=GOLD, line_w=1.5)
        add_text(s, x, by + Inches(0.15), bw, Inches(0.5),
                 title, font=SERIF, size=15, bold=True, color=SEPIA, align=PP_ALIGN.CENTER)
        runs = []
        for j, it in enumerate(items):
            runs.append({"text": "•  ", "size": 12, "color": GOLD, "bold": True, "new_para": (j > 0)})
            runs.append({"text": it, "size": 12, "color": SEPIA})
        add_multitext(s, x + Inches(0.25), by + Inches(0.75), bw - Inches(0.5), bh - Inches(0.9),
                      runs, line_spacing=1.5)

    # Thank you bar
    ty = by + bh + Inches(0.25)
    add_rect(s, Inches(0.7), ty, SW - Inches(1.4), Inches(1.1), fill_rgb=SEPIA)
    add_text(s, Inches(0.7), ty + Inches(0.1), SW - Inches(1.4), Inches(0.55),
             "Thank you.", font=SERIF, size=36, bold=True, color=GOLD, align=PP_ALIGN.CENTER)
    add_text(s, Inches(0.7), ty + Inches(0.65), SW - Inches(1.4), Inches(0.4),
             "Questions?", font=SERIF, size=18, italic=True, color=GOLD_LITE, align=PP_ALIGN.CENTER)
    add_footer(s, 14)
    add_speaker_notes(s,
        "[14:30–15:00] The full software stack is on this slide — Unity 6, URP, GPT-4o, ComfyUI with the Nunchaku INT4 model.\n\n"
        "The full code is in the GitHub repository linked here. Thirty-four scripts, eight thousand seven hundred lines of C#.\n\n"
        "References to the influences I named earlier are at the bottom — Hogwarts Legacy, Papers Please, Disco Elysium, plus the Catmull-Rom and Nunchaku papers.\n\n"
        "Thank you very much for your time. I'm happy to take questions."
    )

# --------- Build all ---------
slide_1()
slide_2()
slide_3()
slide_4()
slide_5()
slide_6()
slide_7()
slide_8()
slide_9()
slide_10()
slide_11()
slide_12()
slide_13()
slide_14()

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "WandAtelier_Presentation.pptx")
prs.save(out)
print(f"Saved: {out}")
print(f"Slides: {len(prs.slides)}")
