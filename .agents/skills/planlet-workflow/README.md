# Planlet Workflow

An opt-in repository workflow for implementing Planlets one task at a time,
with verification and one commit per task.

## Install

Install the skill project-locally for the agents used by the repository:

```bash
npx skills add vipentti/skills --skill planlet-workflow
```

Commit the installed skill files to the repository.

## Opt In

Add this to the repository's `AGENTS.md`/`CLAUDE.md`:

```md
## Development Workflow

Create a Planlet plan before work that is not a simple one-file fix.

When implementing a Planlet, use the repository-installed `planlet-workflow` skill.
```

If the repository already defines when Planlets should be created, only the opt-in line is necessary:

```md
When implementing a Planlet, use the repository-installed `planlet-workflow` skill.
```

Do not copy detailed task, verification, commit, or completion rules into `AGENTS.md`.
Those rules belong in `SKILL.md` so they can be maintained in one place.
