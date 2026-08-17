## Maintaining this file

Keep this file current as you work. Add or update guidance when you discover **durable, non-obvious information** that would help future agents.

- Only document information that is **not readily apparent from the code**.
- Prefer conventions, constraints, rationale, gotchas, and operational knowledge.
- Do not duplicate implementation details, types, APIs, or behavior that can be understood directly from the code.
- Remove or correct guidance that becomes outdated, redundant, or misleading.
- Keep additions concise and specific.

<!-- BEGIN PLANLET AGENTS v:1 hash:0246f0e7 -->
## Planning with Planlet

This repository uses Planlet for focused implementation plans. A planlet is
`plans/<slug>/plan.md` + `tasks.md`; Markdown is the source of truth.

- Propose a planlet before multi-step work; skip it for one-file changes.
- Use the `planlet` CLI for lifecycle state, including task checkboxes and
  completion/archive. Edit plan and task body content directly.
  Commands: `planlet create|show|tasks|status|validate <slug>`,
  `planlet task check <slug> <task-id>`, `planlet complete <slug>`.
- Check each task off only after its verification passes. When the last task is
  checked, run `planlet complete <slug>` to archive it.
- Run `planlet help [command]` before using a command you have not used here.
- The `planlet` CLI is required. If no executable is available, install it
  (`npm install -g @vipentti/planlet`) or invoke it through
  `npx @vipentti/planlet`. If it still cannot run, stop and report that,
  naming the missing executable. Do not reimplement CLI operations by editing
  planlet files.
<!-- END PLANLET AGENTS -->

When implementing a Planlet, use the repository-installed `planlet-workflow` skill.
