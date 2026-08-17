# Implementation Guidance

## Trust the CLI as the target authority

`validate`, `list`, `tasks`, and `status` are the only authorities on whether a planlet exists, is
active, and is well formed. Do not re-derive slug rules, headings, or task-line grammar by reading
the files; a non-zero exit is a workflow failure to report, never a reason to parse Markdown
yourself.

## Evaluate drift

Repository change since planning is expected. Continue when current code merely changes incidental file locations or makes an equivalent implementation adjustment obvious. Explain the adjustment in the final summary.

Treat drift as material when it invalidates the stated approach, changes public behavior or acceptance criteria, introduces a migration or compatibility decision, removes an assumed dependency, or makes planned work harmful or redundant. Pause with concrete evidence and recommend a plan revision.

## Complete tasks truthfully

Before checking a task, confirm that its whole described outcome exists and that relevant verification passed. A code edit alone is not completion. Use targeted checks during implementation and broader checks when the plan or repository requires them.

If a check fails, distinguish an in-scope defect from unrelated existing failure. Fix in-scope defects when the plan authorizes it. Otherwise report the failing command and evidence, leave the task unchecked, and continue only when independent remaining work is safe.

Treat CLI exit status and stable structured error code as authoritative. Do not parse field order, whitespace, or incidental TOON layout. After a successful task check, inspect canonical task and status results instead of inferring progress from command prose.

For newly discovered necessary work, determine whether it is a small implementation detail or a material scope addition. Incorporate small details transparently. For material additions, propose consistent edits to both `plan.md` and `tasks.md`; preserve existing IDs and allocate new IDs above the highest current numeric suffix. The CLI has no revision operation, so these are direct file edits.

When editing `tasks.md` for an approved scope revision or an exceptional evidence note, re-read
immediately before editing, avoid rewriting unrelated Markdown, and never touch a checkbox marker.
No CLI command performs either write; `task check` and `task uncheck` own only checkbox state.

## Record evidence only when it is exceptional

Most planlets need no `## Verification Evidence` section, and its absence is the normal outcome. Tests, lint, type-checking, builds, ordinary pull-request review, and branch-protected CI are sufficient in their own systems; duplicating them into a committed task file adds maintenance without adding trust. Write a note only when a durable fact would otherwise be lost: verification that was external, irreversible, or non-reproducible, or that failed, stayed partial, or was unavailable, and whose residual result affects a later decision. Preserve a failed or unavailable line when it explains partial progress instead of rewriting it green.

Anything recorded must be effectively write-once, because a committed line that needs later edits stops being evidence. Never write a current-head or otherwise self-referential commit SHA, a moving branch, `latest`, or dashboard link, a bare run identifier, a transient log or command output, a secret, a stack trace, a large listing, or a local filesystem path. Prefer a final artifact version or digest, or a stable external record, and only when it is material; a provider record that already binds to its own source does not need a duplicate SHA.

Keep each permitted line to one short outcome, and name the affected task IDs only when they are not obvious. Write evidence before running completion, because task mutation is refused once a completion record exists. Write every line as a plain bullet or prose, never as a `- [ ]` or `- [x]` checkbox bullet, because the task parser reads any top-level checkbox bullet as a task line and rejects the planlet as malformed or duplicated.
