# Completion Guidance

## Trust the CLI as the target authority

`validate`, `list`, `tasks`, and `status` are the only authorities on whether a planlet exists, is
active, and is well formed. Do not re-derive slug rules, headings, or task-line grammar by reading
the files; a non-zero exit is a workflow failure to report, never a reason to parse Markdown
yourself.

## Require explicit incomplete approval

This approval applies only when tasks remain and the active planlet has no completion record. A `validate` warning that an active planlet contains a completion record, or a `write_conflict` whose details contain `auditRecorded: true`, is resumed by one `complete <slug>` with no `--allow-incomplete` and no new reason. Leave that record untouched. `validate` exiting non-zero with `invalid_plan` is a conflicting record; stop.

List each remaining task ID and description before asking. Explain that an override moves the planlet while retaining unchecked tasks. Require an explicit confirmation directed at this planlet and a non-empty single-line reason suitable for the audit trail. If either is absent, stop without editing.

Use reason exactly as approved except necessary surrounding-whitespace trimming. If the approved reason contains a line break, stop and ask for a single-line reason. Do not fold it yourself. Never invent, generalize, or reuse reason from another planlet.

## Completion record

`complete` writes the completion record. Never write, edit, or delete that section by hand. The completion record is a lifecycle audit: it proves when and under what authority the planlet moved, never that verification passed. Leave any optional `## Verification Evidence` section untouched and archive it as written; do not merge it into the completion record, extend the record with verification fields, or add evidence during completion. Such a section is exceptional, so a planlet without one is complete as it stands.
