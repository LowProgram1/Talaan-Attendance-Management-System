"""Build the GitHub Pages copy from the repository's single documentation source."""

from pathlib import Path


root = Path(__file__).resolve().parent.parent
source = (root / "DOCUMENTATIONS.md").read_text(encoding="utf-8")
page = source.replace("(docs/module-screenshots/", "(module-screenshots/")
page = page.replace(
    "[GitHub Pages copy](docs/index.md)",
    "[GitHub repository copy](https://github.com/LowProgram1/Talaan-Attendance-Management-System/blob/main/DOCUMENTATIONS.md)",
)
page = page.replace('<details><summary>', '<details markdown="1"><summary>')
(root / "docs" / "index.md").write_text(
    "---\nlayout: default\ntitle: Talaan Documentation\n---\n\n" + page,
    encoding="utf-8",
)
