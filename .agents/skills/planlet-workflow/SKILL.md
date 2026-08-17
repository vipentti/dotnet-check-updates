---
name: planlet-workflow
description: >
  Use only when explicitly requested or repository instructions opt in. Execute
  Planlet tasks in order, verify and commit each task separately, then complete
  the Planlet in a separate final commit.
license: MIT
---

# Planlet Workflow

Use this workflow only when the user or repository instructions explicitly opt
into `planlet-workflow`. Planlet's own skills and CLI remain authoritative for
lifecycle operations, file formats, validation, task state, and completion.

## Prepare

Select exactly one active Planlet and announce its slug before changing files.

- If the user provides a slug, use it only if it is active and valid.
- Otherwise, use the sole active Planlet, or an unambiguous match when several
  are active.
- If none are active, or the choice is ambiguous, stop and ask the user.

Before the first task, read all of `plan.md` and `tasks.md`, inspect applicable
repository instructions and working-tree changes, and preserve unrelated work.
The plan is the implementation and acceptance contract; tasks are its ordered
execution list.

## Execute Tasks

Repeat until no unchecked tasks remain:

1. Take the first unchecked task in `tasks.md`.
2. Implement only its documented outcome. Do not reorder, batch, anticipate
   later work, or materially expand scope.
3. Run its `Verify:` requirement and all applicable checks required by the
   plan. Fix in-scope failures before continuing.
4. If verification cannot run or fails, leave the task unchecked and report the
   blocker.
5. Mark the task complete only through the Planlet CLI, then confirm its
   reported task and lifecycle state.
6. Create exactly one commit containing that task's implementation and its
   Planlet state change. Commit no unrelated files or hunks.
7. If the commit fails, do not start another task. Resolve it and commit, or
   stop and report the blocker while keeping the implementation and its Planlet
   state together for the eventual commit.
8. Report the task ID, outcome, verification, commit, and next task. Where
   progress updates cannot be sent while continuing, include these details for
   every task in the final report.

Tasks inherit relevant plan requirements even when their text does not repeat
them. A task may touch multiple files, but it still receives one implementation
commit. Use the repository's commit-message convention unless instructed
otherwise.

## Complete

After the final task is implemented, verified, checked through the Planlet CLI,
and committed, use Planlet's normal completion workflow. Do not manually move
the Planlet or write completion metadata.

Create one separate commit containing only completion or archive state. If that
commit fails, do no other work until it succeeds or the blocker is reported.

## Pause for Drift

Stop and ask rather than guess when the plan is stale, a task has consequential
ambiguity, required work materially exceeds the plan, verification has no clear
in-scope fix, unrelated work would be mixed or overwritten, or authority is
missing. Small discoveries clearly within the documented outcome are allowed.

Do not weaken Planlet's validation, safety, verification, or state-management
rules to satisfy this workflow.
