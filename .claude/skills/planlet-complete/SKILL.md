---
name: planlet-complete
description: Validate and safely complete or archive exactly one active repository-local Planlet with a UTC audit record. Use when a user asks to finish the Planlet lifecycle, archive completed work, or explicitly override incomplete tasks with a recorded reason.
allowed-tools: Bash(planlet:*)
compatibility: Requires planlet CLI.
license: MIT
---

# Planlet Complete

Complete one planlet without hiding unfinished or invalid work.

## Start the workflow

1. Discover the repository root without traversing above its boundary.
2. Use one available `planlet` executable throughout. Confirm needed operations with `planlet help <command>` and pass `--root "<repository-root>"` to every operational command. Treat angle-bracket runtime values as separate argv values; when invoking through a shell, apply shell-specific escaping instead of interpolating raw text.
3. Resolve exactly one active planlet with `planlet --root "<repository-root>" list`. Accept one valid explicit active slug. With no slug, select and announce sole active planlet; report none; or ask user to choose when several exist.
4. Run `planlet --root "<repository-root>" validate <slug>`. Stop on non-zero exit. Re-read both files completely with `planlet --root "<repository-root>" --full show <slug> --part plan` and `planlet --root "<repository-root>" --full show <slug> --part tasks`; treat missing, unreadable, or malformed files as invalid.
5. Run `planlet --root "<repository-root>" tasks <slug> --remaining`.
6. The `planlet` CLI is required. If no executable is available, install it
   (`npm install -g @vipentti/planlet`) or invoke it through `npx @vipentti/planlet`. If it still
   cannot run, stop and report that, naming the missing executable. Do not reimplement CLI
   operations by editing planlet files.

## Decide completion

Inspect all recognized tasks. For normal completion, require every task to be checked. If tasks remain, show their IDs and descriptions, warn that completion will archive unfinished work, and obtain explicit confirmation plus a non-empty reason. Do not reuse general implementation approval as an override.

Report any optional `## Verification Evidence` section in `tasks.md` as inspected evidence. Treat it as opaque prose: do not parse its semantics, rerun its checks, create missing proof, or accept it in place of a checked task. Most planlets have no such section, and its absence is normal and never blocks completion; never add one during completion. If the plan's strategy names a mandatory external gate that no evidence records, report that gap as an observation only: never uncheck an already-checked task, never edit `tasks.md` for it, and never let it block or downgrade completion.

Read [completion guidance](references/completion-guidance.md) before completing. Refuse unsafe paths, invalid slugs, an existing completed planlet with the same logical slug, or an occupied destination. Do not change the source when any check fails.

For zero remaining tasks, run `planlet --root "<repository-root>" complete <slug>`. For explicitly approved incomplete completion, run `planlet --root "<repository-root>" complete <slug> --allow-incomplete --reason "<reason>"`. Never attempt normal completion first merely to prompt user; `tasks --remaining` supplies decision evidence.

Treat CLI non-zero exit and stable structured error code as authoritative. Do not retry around `incomplete_tasks`, collision, invalid-plan, unsafe-path, or write-conflict failures. On success, inspect reported logical slug, mode, timestamp, and archive path, then run `planlet --root "<repository-root>" validate <slug>` to inspect completed storage.

Never append a completion record or move a planlet directory by hand. `complete` captures the UTC
instant, writes the record, and performs the archive move atomically.

Do not implement remaining tasks, complete several planlets, overwrite a destination, change the logical slug, or delete either primary file.

## Finish

Completion does not itself require a commit. Keep archive and completion changes with the repository state they describe. If the user requested a commit or the surrounding workflow grants commit authority, verified implementation, task updates, and completion changes may share one atomic commit; no separate completion commit is required. Otherwise leave the intended changes staged and report them for the caller to commit. Before performing a push or branch switch, ensure no Planlet state would be separated from the repository state it describes. Report logical slug, recorded UTC timestamp, mode, remaining task IDs for override, final archive path, whether an optional evidence section was present, and post-completion validation result. If operation stopped, report exact source state and blocking code.
