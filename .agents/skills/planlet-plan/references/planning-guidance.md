# Planning Guidance

## One ownership rule

`plan.md` owns the shared design and acceptance requirements: intended
outcome, scope and exclusions, approach and invariants, acceptance criteria,
broad verification strategy, and material risks. `tasks.md` names ordered
delivered outcomes: one outcome per task, prerequisites before their
consumers, unique T-IDs, and a short task-specific `Verify:` clause only when
useful. State each material requirement once in its owning file; a task is
understandable after reading `plan.md` and must not duplicate detailed plan
requirements. An
implementer reads both files completely before starting.

When several tasks are governed by one safety, compatibility, migration, or
ordering rule, state that rule once in Approach or Acceptance Criteria.

## Verification evidence is exceptional

`Verification` is strategy, not a run log. Name stable repository commands or
check categories, expected outcomes, external gates, and known limitations;
never paste logs. Do not record execution results in `plan.md`; a later
strategy or scope change is a plan revision, not an execution journal entry.

Committed verification evidence is exceptional and absent by default. Tests,
lint, type-checking, builds, ordinary pull-request review, and branch-protected
CI already hold their own results. Expect a `## Verification Evidence` note
only when the plan foresees a durable fact that ordinary Git, test,
pull-request, or CI history cannot reconstruct adequately: external,
irreversible, non-reproducible, failed, partial, or unavailable verification
whose residual result affects a later decision. Say so explicitly in
Verification when the plan expects one, and stay silent otherwise.

## Revision rules

During revision, keep IDs for semantically unchanged work. Allocate every new
ID above the highest numeric suffix ever present in the current file; do not
renumber after removal or reordering. Keep completed tasks unless the user
explicitly approves a documented change. If revised scope invalidates completed
work, explain the effect and update both files consistently. Avoid catch-all
tasks such as "finish implementation" or "test everything."

## Review before persistence

Present the proposed title, slug, plan content, and task list before writing.
Call out only material assumptions, exclusions, and unresolved decisions; do
not restate the proposed files in a second narrative summary. Treat edits
requested during review as part of the proposal and ask for confirmation of the
final version. Confirmation authorizes only creation or revision of the two
planning files, not product implementation.

After confirmed creation, populate only stubs produced by `planlet create`.
After confirmed revision, re-read current files immediately before editing and
reconcile both documents in one reviewable change. In either case, targeted
CLI validation and full post-write inspection must succeed before reporting a
valid handoff.

## Handle failures

Treat CLI exit status and structured error code as authoritative. Do not depend
on field order, whitespace, or incidental TOON formatting. Stop on unsafe path,
invalid slug, collision, write conflict, or invalid-plan errors; report code
and suggested next action without bypassing CLI. Warnings require review but do
not automatically invalidate a structurally valid planlet.

## Keep the handoff concise

Write the smallest plan that preserves consequential decisions. Avoid exhaustive
implementation walkthroughs, test-case matrices, historical reasoning, and
incidental edit sequences unless they remove material ambiguity.

Tasks must not duplicate detailed plan requirements, but may repeat a small
constraint when needed for task clarity. Prefer one concise outcome sentence,
ideally 25 words or fewer; a complete task normally stays within 50 words
including any `Verify:` clause or nested list. These are planner judgment
targets, not parser or validator thresholds, and no tool enforces them.

Before presenting the proposal, run a compression pass: reread the draft
`tasks.md` against `plan.md` and delete every detail an implementer can recover
unambiguously from `plan.md`. Keep only detail whose removal would leave the
outcome or its ownership genuinely ambiguous. Compression relocates detail,
never drops a requirement.

Task-local metadata is exceptional. A bare outcome line is the default and the
exemplar. The short `Verify:` clause stays available only when useful, when it
makes the task shorter or clearer and the information is not recoverable from
`plan.md`. Broad suite execution belongs to the distinct final verification
task, never to a task-local clause.

Ordinary nested Markdown lists remain allowed sparingly and must not become
another specification surface. If substantial explanation is needed, move it
into `plan.md` rather than keeping it in `tasks.md`.

Task boundaries follow independently meaningful delivered outcomes, the sole
split criterion. A task may contain several independently testable or
reviewable components and several requirements when they together serve one
coherent delivered outcome; keep those requirements in `plan.md`. Split only
when every resulting checkbox can stand as its own coherent delivered outcome,
splitting rather than compressing. Separate implementability or verifiability
is only evidence for that boundary, never sufficient on its own. Do not let
word-count pressure change semantic task boundaries, and do not split
mechanically by file, function, tiny edit, or incidental implementation step.
