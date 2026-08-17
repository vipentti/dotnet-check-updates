# Completion Guidance

## Trust the CLI as the target authority

`validate`, `list`, `tasks`, and `status` are the only authorities on whether a planlet exists, is
active, and is well formed. Do not re-derive slug rules, headings, or task-line grammar by reading
the files; a non-zero exit is a workflow failure to report, never a reason to parse Markdown
yourself.

## Require explicit incomplete approval

List each remaining task ID and description before asking. Explain that an override moves the planlet while retaining unchecked tasks. Require an explicit confirmation directed at this planlet and a non-empty reason suitable for the audit trail. If either is absent, stop without editing.

Use reason exactly as approved except necessary surrounding-whitespace trimming. Never invent, generalize, or reuse reason from another planlet.

## Read the completion record

`complete` appends one completion section to `tasks.md` in this shape:

```markdown
## Completion

- Completed at: <captured UTC timestamp>
- Mode: normal
```

For an approved override, the shape is:

```markdown
## Completion

- Completed at: <captured UTC timestamp>
- Mode: incomplete override
- Remaining tasks: T2, T4
- Reason: <user-approved reason>
```

The templates above are read-only reference: report what `complete` wrote and recognize a
conflicting record from it, but never write the section by hand.

Refuse a pre-existing or conflicting completion record rather than silently rewriting history.

The completion record is a lifecycle audit: it proves when and under what authority the planlet moved, never that verification passed. Leave any optional `## Verification Evidence` section untouched and archive it as written; do not merge it into the completion record, extend the record with verification fields, or add evidence during completion. Such a section is exceptional, so a planlet without one is complete as it stands.
