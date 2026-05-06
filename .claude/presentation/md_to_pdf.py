"""
Convert presentation_script.md to a styled PDF.
Run: py md_to_pdf.py
Output: presentation_script.pdf in the same folder.
"""
import os
import re
import markdown
from xhtml2pdf import pisa

HERE = os.path.dirname(os.path.abspath(__file__))
SRC  = os.path.join(HERE, "presentation_script.md")
DST  = os.path.join(HERE, "presentation_script.pdf")

# --------- Read markdown ---------
with open(SRC, "r", encoding="utf-8") as f:
    md_text = f.read()

# --------- Markdown → HTML body ---------
html_body = markdown.markdown(
    md_text,
    extensions=["extra", "sane_lists", "tables", "fenced_code", "toc"],
)

# Add page-break hint before each H2 ("Slide N — ...") so each slide starts
# on its own page. xhtml2pdf understands -pdf-keep-with-next / page-break-before.
# We post-process the HTML to inject a page-break class on H2 elements (after
# the very first one).
parts = re.split(r'(<h2[^>]*>)', html_body)
out = []
seen_h2 = False
for p in parts:
    if p.startswith("<h2"):
        if seen_h2:
            # Inject page-break-before by wrapping with a div, since xhtml2pdf
            # honors page-break-before on block elements.
            out.append('<div class="page-break"></div>')
        seen_h2 = True
    out.append(p)
html_body = "".join(out)

# --------- HTML scaffold + CSS ---------
title = "The Wand Atelier — Presentation Script"
html_doc = f"""<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8" />
<title>{title}</title>
<style>
  @page {{
    size: A4;
    margin: 18mm 16mm 16mm 16mm;
    @frame footer {{
      -pdf-frame-content: footer-content;
      bottom: 8mm;
      left: 16mm;
      right: 16mm;
      height: 8mm;
    }}
  }}

  body {{
    font-family: "Helvetica", "Arial", sans-serif;
    font-size: 10.5pt;
    line-height: 1.45;
    color: #2b1f10;
  }}

  h1 {{
    font-family: "Times New Roman", "Georgia", serif;
    font-size: 22pt;
    color: #6b4824;
    border-bottom: 2pt solid #c9a24a;
    padding-bottom: 4pt;
    margin-bottom: 14pt;
  }}

  h2 {{
    font-family: "Times New Roman", "Georgia", serif;
    font-size: 16pt;
    color: #6b4824;
    border-bottom: 1pt solid #c9a24a;
    padding-bottom: 3pt;
    margin-top: 18pt;
    margin-bottom: 10pt;
  }}

  h3 {{
    font-family: "Times New Roman", "Georgia", serif;
    font-size: 13pt;
    color: #4a3318;
    margin-top: 14pt;
    margin-bottom: 6pt;
  }}

  p {{
    margin: 6pt 0;
    text-align: left;
  }}

  strong, b {{ color: #2b1f10; }}
  em, i {{ color: #4a3318; }}

  ul, ol {{ margin-left: 18pt; }}
  li {{ margin-bottom: 3pt; }}

  blockquote {{
    border-left: 3pt solid #c9a24a;
    padding: 4pt 10pt;
    margin: 8pt 0 8pt 0;
    background-color: #faf4e5;
    color: #4a3318;
    font-style: italic;
  }}

  code {{
    font-family: "Courier New", "Consolas", monospace;
    background-color: #f3ead4;
    padding: 1pt 3pt;
    border-radius: 2pt;
    font-size: 10pt;
  }}

  pre {{
    font-family: "Courier New", "Consolas", monospace;
    background-color: #f3ead4;
    padding: 6pt 8pt;
    border-radius: 4pt;
    border-left: 2pt solid #c9a24a;
    font-size: 9.5pt;
    white-space: pre-wrap;
  }}

  hr {{
    border: 0;
    border-top: 1pt dotted #c9a24a;
    margin: 10pt 0;
  }}

  table {{
    border-collapse: collapse;
    width: 100%;
    margin: 8pt 0;
  }}
  th, td {{
    border: 0.5pt solid #c9a24a;
    padding: 4pt 6pt;
    text-align: left;
    font-size: 10pt;
  }}
  th {{ background-color: #f3ead4; }}

  .page-break {{
    page-break-before: always;
  }}

  /* "[stage direction]" lines tend to be entire paragraphs that start with [.
     We can't easily target those in the markdown HTML; users can rely on the
     bracket character to spot them visually. */

  .footer-content {{
    text-align: center;
    color: #6b4824;
    font-size: 8.5pt;
    font-style: italic;
  }}
</style>
</head>
<body>

{html_body}

<div id="footer-content" class="footer-content">
  The Wand Atelier — Presentation Script &nbsp;·&nbsp; Page <pdf:pagenumber/> of <pdf:pagecount/>
</div>

</body>
</html>
"""

# --------- Render to PDF ---------
with open(DST, "wb") as out_pdf:
    result = pisa.CreatePDF(src=html_doc, dest=out_pdf, encoding="utf-8")

if result.err:
    print(f"PDF generation failed with {result.err} errors")
    raise SystemExit(1)

size = os.path.getsize(DST)
print(f"OK  {DST}  ({size:,} bytes)")
