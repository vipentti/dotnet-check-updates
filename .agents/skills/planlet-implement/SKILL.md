---
name: planlet-implement
description: Implement and verify exactly one active repository-local Planlet while updating its task checklist incrementally. Use when a user asks to execute a persisted planlet, continue its implementation, or report and advance its remaining work without archiving it.
allowed-tools: Bash(planlet:*)
compatibility: Requires planlet CLI.
license: MIT
---

# Planlet Implement

Implement one persisted planlet and keep its progress truthful.

## Start the workflow

1. Discover the repository root without traversing above its boundary.
2. Use one available `planlet` executable throughout. Confirm needed operations with `planlet help <command>` and pass `--root "<repository-root>"` to every operational command. Treat angle-bracket runtime values as separate argv values; when invoking through a shell, apply shell-specific escaping instead of interpolating raw text.
3. Resolve exactly one active planlet with `planlet --root "<repository-root>" list`. Accept one valid explicit active slug. With no slug, select and announce sole active planlet; report none; or ask user to choose when several exist. Never select by recency or output order.
4. Run `planlet --root "<repository-root>" validate <slug>`. Stop on non-zero exit.
5. Re-read both files completely with `planlet --root "<repository-root>" --full show <slug> --part plan` and `planlet --root "<repository-root>" --full show <slug> --part tasks`; use `planlet --root "<repository-root>" tasks <slug>` and `planlet --root "<repository-root>" status <slug>` for canonical progress. When the harness exposes a dedicated file-reading capability, also read each file with it before editing that file directly, because such a harness can reject an edit to a file it has not read and may not count a shell read.

Treat `plan.md` as the authoritative change-specific design and acceptance
contract for this planlet and `tasks.md` as its execution index. A concise task
inherits every applicable scope, approach, acceptance, and verification
requirement from `plan.md`; absence of repeated detail in the task does not make
that requirement optional. Applicable repository instructions and higher-level
design contracts still take precedence when they explicitly constrain the plan.

6. The `planlet` CLI is required. If no executable is available, install it
   (`npm install -g @vipentti/planlet`) or invoke it through `npx @vipentti/planlet`. If it still
   cannot run, stop and report that, naming the missing executable. Do not reimplement CLI
   operations by editing planlet files.

## Implement

1. Inspect current repository instructions, code, tests, and working-tree changes relevant to the plan. Preserve user work.
2. Compare current conditions with the persisted plan. Read [implementation guidance](references/implementation-guidance.md) for drift, task, and pause decisions.
3. Work through tasks in a sensible dependency order. Limit mutations to this planlet and its implementation scope.
4. Verify each task with checks proportionate to its outcome. Mark it complete immediately after both implementation and relevant verification succeed; leave failed or unverified tasks unchecked.
5. Run `planlet --root "<repository-root>" task check <slug> <task-id>` only after whole task outcome and relevant verification succeed. Treat successful `changed: false` as idempotent completion, not failure.
6. Immediately run `planlet --root "<repository-root>" tasks <slug>` and `planlet --root "<repository-root>" status <slug>` after each check. Confirm expected task and counts before continuing. When `task check` reports `state: ready_to_complete`, say so in the same turn and state that the planlet is ready for the separate completion workflow; do not archive it here.
7. Run `task check` before any commit that includes the implementation it records. Planlet state must never trail the repository state it describes across a commit, push, or branch boundary. This constrains commit contents, not commit authority or granularity: do not create or rewrite commits unless the user requested it or the surrounding workflow already grants that authority. When committing, include corresponding staged Planlet changes; multiple tasks and subsequent completion/archive changes may share one commit. When not committing, leave Planlet changes staged for the eventual commit and report that relationship before handing control back.
8. Leave `tasks.md` without a `## Verification Evidence` section by default. Add one, before completion, only for a durable fact that ordinary Git, test, pull-request, or CI history cannot reconstruct adequately: external, irreversible, non-reproducible, failed, partial, or unavailable verification whose residual result affects a later decision. Never copy routine test, lint, type-check, build, review, or CI results into it. No CLI command writes or edits an evidence section; the agent adds it directly. Read [implementation guidance](references/implementation-guidance.md) before writing one, because every recorded line must be write-once and must survive later commits unchanged.
9. Never edit a checkbox in `tasks.md` by hand. `task check` and `task uncheck` own checkbox state; treat a `task_not_found`, `invalid_plan`, `unsafe_path`, or `write_conflict` error as a stop, not as authorization to edit the file.
10. If new work materially expands scope, update plan and tasks directly (the CLI has no revision command) only with user approval or pause for direction.

Pause rather than guess when the plan is materially stale, a task has multiple consequential interpretations, verification fails without an in-scope remedy, required authority is missing, or safe progress would expand scope. Record evidence and keep affected tasks unchecked.

Do not implement multiple planlets, infer completion from malformed or missing files, or archive the planlet unless the user explicitly requested a separate completion workflow.

## Finish

Run final `planlet --root "<repository-root>" tasks <slug> --remaining` and `planlet --root "<repository-root>" status <slug>`. Before reporting, inspect `git status --short`. Staged Planlet changes may remain when committing is intentionally left to the user or another workflow; report them and the repository changes they must accompany. If performing a push or branch switch, first ensure it will not separate Planlet state from the repository state it describes. Report logical slug, outcomes, task IDs checked during this run, exact verification and results, deviations or blockers, remaining task IDs, and canonical state. State whether state is `ready_to_complete`.
