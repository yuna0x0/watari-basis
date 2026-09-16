#!/usr/bin/env python3
"""Fails on prose the project rules do not allow: a paragraph over 70 words in the docs, READMEs
or CONTRIBUTING, a changelog entry over two lines, or a report message over 35 words. Tables, code, headings and list items are
not measured. Run before a release; the Checks workflow runs it on every push."""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
PAGES = sorted((ROOT / "docs" / "docs").rglob("*.md")) + [
    ROOT / "README.md",
    ROOT / "Packages" / "com.yuna0x0.basis.convert" / "README.md",
    ROOT / "CONTRIBUTING.md",
]
CHANGELOG = ROOT / "Packages" / "com.yuna0x0.basis.convert" / "CHANGELOG.md"
MAX_PARAGRAPH_WORDS = 70
MAX_ENTRY_LINES = 2

failures = []

for page in PAGES:
    if not page.exists():
        continue
    text = re.sub(r"^---.*?---\n", "", page.read_text(), flags=re.S)
    text = re.sub(r"```.*?```", "", text, flags=re.S)
    for paragraph in re.split(r"\n\s*\n", text):
        stripped = paragraph.strip()
        if not stripped or stripped.startswith(("#", "|", "-", "*", "<", "import", "1.", "2.", "3.")):
            continue
        words = len(stripped.split())
        if words > MAX_PARAGRAPH_WORDS:
            failures.append(f"{page.relative_to(ROOT)}: paragraph of {words} words: {stripped[:70]!r}")

version = "?"
for line in CHANGELOG.read_text().splitlines():
    heading = re.match(r"^## \[([^\]]+)\]", line)
    if heading:
        version = heading.group(1)
if CHANGELOG.exists():
    version = "?"
    entry = []
    def flush():
        if len(entry) > MAX_ENTRY_LINES:
            failures.append(f"CHANGELOG {version}: entry of {len(entry)} lines: {entry[0][:70]!r}")
    for line in CHANGELOG.read_text().splitlines():
        heading = re.match(r"^## \[([^\]]+)\]", line)
        if heading:
            flush(); entry = []; version = heading.group(1); continue
        if line.startswith("- "):
            flush(); entry = [line]
        elif line.startswith("  ") and entry:
            entry.append(line)
        else:
            flush(); entry = []
    flush()

MAX_MESSAGE_WORDS = 35
EDITOR = ROOT / "Packages" / "com.yuna0x0.basis.convert" / "Editor"
CALL = re.compile(
    r'(?:Diagnostics\.Add|ToggleDiagnostics\.Add|MotionDiagnostics\.Add|log\.Add|new ConversionDiagnostic)'
    r'\((?:\s*DiagnosticSeverity\.\w+,\s*)?\s*"([^"]+)",\s*(.*?)\);', re.S)
for source in sorted(EDITOR.rglob("*.cs")):
    text = source.read_text()
    for call in CALL.finditer(text):
        code, expression = call.group(1), call.group(2)
        # A ternary carries two messages; measure each branch on its own.
        for branch in re.split(r"\n\s*[?:]\s*(?=\$?\")", expression):
            message = " ".join(re.findall(r'"((?:[^"\\]|\\.)*)"', branch))
            words = len(re.sub(r"\{[^}]*\}", "X", message).split())
            if words > MAX_MESSAGE_WORDS:
                failures.append(f"{source.relative_to(ROOT)}: {code} message of {words} words")

for failure in failures:
    print(failure)
print(f"{len(failures)} prose failures" if failures else "prose ok")
sys.exit(1 if failures else 0)
