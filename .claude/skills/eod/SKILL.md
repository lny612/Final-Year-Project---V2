---
name: eod
description: "End-of-day session wrap-up. Updates memory files, CLAUDE.md, and GDD so the next session can pick up where this one left off."
disable-model-invocation: true
argument-hint: "[optional summary of what to emphasize]"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

I'm calling it a day. Update any relevant .md files before I end the session so I can pick up where I left off in the next session.

## Session context

Recent git activity:
```!
git log --oneline -15
```

Files changed (unstaged + staged):
```!
git diff --name-only HEAD~5 HEAD 2>/dev/null; echo "---unstaged---"; git diff --name-only; echo "---untracked---"; git ls-files --others --exclude-standard
```

Additional context from user: $ARGUMENTS

## Process

1. **Review what changed this session** using the git data above. If needed, read specific changed files to understand what was done.

2. **Update auto-memory** at the project memory directory (find it via `~/.claude/projects/*/memory/MEMORY.md`):
   - Add new memory files for significant learnings (user preferences, project state changes, new references)
   - Update existing memory files if their content is now stale
   - Update `MEMORY.md` index if any memory files were added or removed
   - Follow the memory format: YAML frontmatter (name, description, type) + markdown body

3. **Update `CLAUDE.md`** if any of these changed:
   - Architecture or key patterns
   - New scripts added or existing ones significantly rewritten
   - External dependency changes
   - Convention changes

4. **Update `Docs/GDD.md`** if any design decisions or game mechanics changed.

5. **Print a handoff summary** (short, scannable):
   - What was accomplished this session (2-4 bullets)
   - Current state / what's pending next
   - Any blockers or open questions for next session
