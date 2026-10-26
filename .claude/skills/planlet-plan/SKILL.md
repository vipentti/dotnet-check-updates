---
name: planlet-plan
description: Explore, propose, create, or revise one repository-local Planlet without implementing product changes. Use when a user wants to investigate and persist a focused plan, refine an existing planlet, or prepare a reviewable implementation handoff.
allowed-tools: Bash(planlet:*)
compatibility: Requires planlet CLI.
license: MIT
---

# Planlet Plan

Create or revise one focused planlet while keeping planning separate from implementation.

## Start the workflow

1. Walk upward from the current directory to the nearest `.git` file or directory. Pass that directory as `--root` and do not walk past it.
2. When the user explicitly asks only to brainstorm, compare options, or keep a
   proposal conversational without saving, skip the remaining start steps and
   the CLI entirely: read applicable repository instructions and relevant code,
   answer in conversation, and stop with the repository unchanged.
3. The `planlet` CLI is required. If no executable is available, install it
   (`npm install -g @vipentti/planlet`) or invoke it through `npx @vipentti/planlet`. If it still
   cannot run, stop and report that, naming the missing executable. Do not reimplement CLI
   operations by editing planlet files.
   Use one available `planlet` executable throughout the workflow. Confirm each needed operation with `planlet help <command>`; do not infer support from this skill. Pass `--root "<repository-root>"` to every operational command. Treat angle-bracket runtime values as separate argv values; when invoking through a shell, apply shell-specific escaping instead of interpolating raw text.
4. Use `planlet --root "<repository-root>" list` to inspect active logical slugs and `planlet --root "<repository-root>" list --completed` to inspect completed logical slugs. For a revision, accept one explicit active slug from the active list. With no slug, select and announce the sole active planlet, or ask the user to choose when several exist. Never select by recency or list order. If no active planlet matches, stop without writing. Do not revise a completed archive. When a revision slug is resolved, run `planlet --root "<repository-root>" validate <slug>`, and read both files completely with `planlet --root "<repository-root>" --full show <slug> --part plan` and `planlet --root "<repository-root>" --full show <slug> --part tasks`.
5. Read applicable repository instructions when present.

## Develop the proposal

1. Inspect the repository before recommending an approach.
2. Look up repository facts instead of asking.
   - Separate the underlying problem, desired outcome, real constraints, and any solution the user proposes. Treat a proposed solution as a hypothesis unless the user states it as a requirement.
   - Check a proposed solution against repository evidence. When evidence contradicts its premise, even one the user gave as the reason, or a simpler, better-targeted option exists, say so and recommend that option. A solution the user explicitly requires is a constraint: plan within it, raise any evidence-backed objection once, and never silently override it. Do not challenge decisions just to generate discussion.
   - For a vague or incomplete request, surface material open decisions that affect outcome, boundaries, constraints, acceptance, verification, or task sizing. Ask about outcome and scope first, and hold a question until the answers it depends on are settled.
   - Ask in small related batches (prefer about 2–4 related decisions; one-at-a-time only when answers depend on each other) with a recommended answer for each decision. Give each recommendation a brief rationale from repository findings, name an alternative only when a meaningful one exists, and let the user accept one recommendation or the whole batch. Do not invent alternatives or assume unstated preferences.
   - In `plan.md`, keep the user's decisions distinct from assumptions: label every recommendation the user did not explicitly confirm, including a side suggestion left unanswered, as an assumption.
   - Stop asking once a fresh implementation agent could understand the outcome, scope, approach, acceptance criteria, and verification without making consequential product decisions. Settle minor ambiguity with a stated assumption.
   - If the request is already precise, proceed without ceremonial questions.
3. Define the outcome, scope, exclusions, approach, acceptance criteria,
   verification, and meaningful risks. Compare options only when the choice
   matters. Keep `plan.md` static: verification records strategy, never results
   of a past or future run.

   Treat `plan.md` as the authoritative change-specific design and acceptance
   contract for this planlet, subject to applicable repository instructions and
   higher-level design documents. State each material requirement once in the
   most appropriate section instead of repeating it across Scope, Approach,
   Acceptance Criteria, Verification, and tasks.

4. Propose a descriptive slug matching `^[a-z0-9]+(?:-[a-z0-9]+)*$`, containing at least one letter, and not starting with `YYYY-MM-DD-`. Verify that its logical slug is unused among active and completed planlets.
5. Turn the proposal into `plan.md` and a compact execution index in
   `tasks.md`. An implementer is expected to read both files before starting:
   `plan.md` owns design decisions, boundaries, invariants, acceptance criteria,
   and broad verification strategy; `tasks.md` orders the delivered outcomes
   needed to realize that plan.

   Keep each task small enough that a typical agent can implement and verify
   one outcome without guessing its ownership. Do not duplicate detailed plan
   requirements in tasks. Prefer one concise task sentence; include likely
   components or task-specific verification only when they materially reduce
   ambiguity. If a task needs a long explanation or many independent
   requirements, move shared detail into `plan.md`.

   Read [planning guidance](references/planning-guidance.md) and use the
   templates in [plan-template.md](assets/plan-template.md) and
   [tasks-template.md](assets/tasks-template.md).

6. Before persisting the planlet, run a compression pass over the draft
   `tasks.md` against `plan.md` to remove recoverable detail; see
   [planning guidance](references/planning-guidance.md) for the full
   procedure.

7. Persist without a confirmation round. Once the decisions needed for a
   fresh-session handoff are settled, or the request is already precise, write
   the planlet as described under Persist or revise. Do not present the plan or
   task list in conversation and do not ask for confirmation to write; the
   persisted files are reviewable drafts, not approval to implement. Ask
   unresolved material decisions before creating files, never after.

## Persist or revise

Write `plan.md` and `tasks.md` only under `<plansDir>/<slug>/`. Take `plansDir` from the earlier `list` output; it is `plans` when unset.

For a new planlet:

1. Run `planlet --root "<repository-root>" create <slug> --title "<title>"`. Treat non-zero exit as no authorization to write around a slug, path, or collision failure.
2. Confirm CLI created only H1 stubs in that directory. When the harness exposes a dedicated file-reading capability, read each created file with it rather than through a shell command, because such a harness can reject a write to a file it has not read and may not count a shell read. Then replace those two stubs with the drafted `plan.md` and `tasks.md` content, because `create` writes H1 stubs only and no CLI command accepts plan or task body content. Never use `create` for revision or overwrite an existing planlet.
3. Run `planlet --root "<repository-root>" validate <slug>`, then re-read both files with `planlet --root "<repository-root>" --full show <slug> --part plan` and `planlet --root "<repository-root>" --full show <slug> --part tasks`; inspect exact persisted content.

For a requested revision, edit both existing files in that directory directly because CLI has no semantic revision operation. When the harness exposes a dedicated file-reading capability, read each file with it before editing, because such a harness can reject an edit to a file it has not read and may not count a shell read. Preserve IDs for unchanged tasks, assign each new ID as one greater than the highest numeric suffix present in the file, and never silently remove completed work. Then run targeted `validate` and full `show` inspection as above.

Do not modify product code, create extra planning documents by default, or begin implementation unless the user separately requests it.

## Finish

Report selected logical slug, paths written or revised, exact CLI validation result, warnings, and only consequential assumptions or unresolved decisions. Do not reproduce the plan or task list, and do not ask for confirmation to write. The user reviews and revises the files directly; implementation needs a separate request.
