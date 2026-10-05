---
name: planlet-complete
description: Validate and safely complete or archive exactly one active repository-local Planlet with a UTC audit record. Use when a user asks to finish the Planlet lifecycle, archive completed work, resume an interrupted completion, or explicitly override incomplete tasks with a recorded reason.
allowed-tools: Bash(planlet:*)
compatibility: Requires planlet CLI.
license: MIT
---

# Planlet Complete

Complete one planlet without hiding unfinished or invalid work.

## Start the workflow

1. Walk upward from the current directory to the nearest `.git` file or directory. Pass that directory as `--root` and do not walk past it.
2. The `planlet` CLI is required. If no executable is available, install it
   (`npm install -g @vipentti/planlet`) or invoke it through `npx @vipentti/planlet`. If it still
   cannot run, stop and report that, naming the missing executable. Do not reimplement CLI
   operations by editing planlet files.
   Use one available `planlet` executable throughout. Confirm needed operations with `planlet help <command>` and pass `--root "<repository-root>"` to every operational command. Treat angle-bracket runtime values as separate argv values; when invoking through a shell, apply shell-specific escaping instead of interpolating raw text.
3. Resolve exactly one active planlet with `planlet --root "<repository-root>" list`. Accept one valid explicit slug only when that list shows it. With no slug, select and announce sole active planlet; report none; or ask user to choose when several exist. Never select by recency or output order. If the slug is absent from that list, stop. Do not complete a completed archive.
4. Run `planlet --root "<repository-root>" validate <slug>`. Stop on non-zero exit. `validate` exit 0 with `state: completed` means a completed archive; stop. A warning that an active planlet contains a completion record is a resume signal; leave the record untouched. Re-read both files completely with `planlet --root "<repository-root>" --full show <slug> --part plan` and `planlet --root "<repository-root>" --full show <slug> --part tasks`; treat missing, unreadable, or malformed files as invalid.
5. Run `planlet --root "<repository-root>" tasks <slug> --remaining`.

## Decide completion

Read [completion guidance](references/completion-guidance.md) before completing.

Report any optional `## Verification Evidence` section in `tasks.md` as inspected evidence. Treat it as opaque prose: do not parse its semantics, rerun its checks, create missing proof, or accept it in place of a checked task. Most planlets have no such section, and its absence is normal and never blocks completion; never add one during completion. If the plan's strategy names a mandatory external gate that no evidence records, report that gap as an observation only: never uncheck an already-checked task, never edit `tasks.md` for it, and never let it block or downgrade completion.

If `validate` warned that the active planlet contains a completion record, or `complete` returned `write_conflict` with `auditRecorded: true`, run `planlet --root "<repository-root>" complete <slug>` once. Pass no `--allow-incomplete` and no new reason. Leave the recorded timestamp, mode, and reason in place. If that command returns `write_conflict` again, stop and report the code. A conflicting record already stopped at `validate` with `invalid_plan`.

Otherwise inspect remaining tasks. For normal completion, require every task to be checked. If tasks remain, show their IDs and descriptions, warn that completion will archive unfinished work, and obtain explicit confirmation plus a non-empty reason. Do not reuse general implementation approval as an override. For zero remaining tasks, run `planlet --root "<repository-root>" complete <slug>`. For explicitly approved incomplete completion, run `planlet --root "<repository-root>" complete <slug> --allow-incomplete --reason "<reason>"`. Never attempt normal completion first merely to prompt user; `tasks --remaining` supplies decision evidence.

Treat CLI non-zero exit and stable structured error code as authoritative. Do not retry `incomplete_tasks`, `archive_collision`, `completed_plan_exists`, `invalid_plan`, `invalid_slug`, `unsafe_path`, `plan_not_found`, or `write_conflict` unless that `write_conflict` has `auditRecorded: true`, which uses the one resume command above. Do not change the source when any check fails. On success, inspect reported logical slug, mode, timestamp, and archive path, then run `planlet --root "<repository-root>" validate <slug>` to inspect completed storage.

Never append a completion record or move a planlet directory by hand. `complete` captures the UTC
instant, writes the record, and performs the archive move atomically.

Report every warning the `complete` output carries to the user. Those warnings are stderr diagnostics and are absent from the stdout summary. A diagnostic that names a link the command left unchanged (`unresolved target`, `ambiguous target`, `reaches planlet through its parent directory`, or `invalid path`) reports the destination as the CLI printed it, never a rewritten or repaired link, and never complete the fix by hand. A `Could not stage` diagnostic means the archive move is not in the index. Inspect the source with `git ls-files -- <source>`. When the source has index entries, run path-scoped `git add -A -- <source> <destination>` so the source deletion and the destination are staged together. When it has none, run `git add -- <destination>`. Then report the staged state. A lock-release diagnostic means stop further planlet writes and repeat the warning's recovery. Report an incomplete-task override notice and a missing recommended-section notice as observations, and do not edit files to clear them.

Rewritten links are not warnings: `linkRewrites` in the result reports how many relative links
were rewritten in `plan.md` and `tasks.md` for the archived location. Mention those counts
when they are nonzero, and never present them as a problem.

Do not implement remaining tasks, complete several planlets, overwrite a destination, change the logical slug, or delete either primary file.

## Finish

Completion does not itself require a commit. Keep archive and completion changes with the repository state they describe. If the user requested a commit or the surrounding workflow grants commit authority, verified implementation, task updates, and completion changes may share one atomic commit; no separate completion commit is required. Otherwise report the staged archive for the caller to commit. When a `Could not stage` diagnostic fired, recover with that same source-and-destination rule before the commit, then report the staged state. Before performing a push or branch switch, ensure no Planlet state would be separated from the repository state it describes. Report logical slug, recorded UTC timestamp, mode, remaining task IDs for override, final archive path, whether an optional evidence section was present, rewritten link counts, every completion warning, and post-completion validation result. If operation stopped, report exact source state and blocking code.
