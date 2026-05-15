---
applyTo: "docs/**/*.md"
---

# Documentation Instructions

FPS documentation is rendered with Docsify from the `docs/` directory.

## Scope

- Treat documentation work as docs-only unless the issue explicitly asks for code or infrastructure changes.
- Keep changes focused on the assigned issue and expected files.
- Do not edit application code, infrastructure code, workflows, package files, secrets, or validation scripts for documentation navigation tasks.

## Navigation

- `docs/_sidebar.md` controls the GitHub Pages left navigation menu.
- Keep sidebar ordering aligned with the architecture story:
  - Versions and decisions
  - Strategy
  - Business Layer
  - Application Layer
  - Technology Layer
  - Security
  - Production
  - Implementation
  - Glossary
- Use Docsify-compatible relative links without the `.md` extension, for example `./business-layer/booking`.
- Add nested links for existing important child pages so users can navigate from GitHub Pages without guessing URLs.
- Verify every sidebar link maps to an existing Markdown file under `docs/`.

## Content Style

- Preserve existing terminology and section names unless the issue asks for a rename.
- Prefer concise, practical documentation over broad rewrites.
- Do not add frontmatter such as `title:` unless an existing page already needs it for rendering.
- When moving or grouping pages, preserve existing URLs where possible.

## Validation

- For sidebar changes, manually check indentation and grouping.
- Confirm all added links resolve to existing files.
- Run the repo validation script when feasible; if not, explain which documentation checks were run.
