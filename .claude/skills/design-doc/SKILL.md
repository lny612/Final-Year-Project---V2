---
name: design-doc
description: "Write a structured game design document for a feature, mechanic, or system. Produces implementation-ready specs with file ownership maps and interface contracts for parallel programmer execution."
context: fork
agent: designer
allowed-tools: Read, Write, Glob, Grep, Bash
argument-hint: "[feature name]"
---

Write a design document for: $ARGUMENTS

## Process

1. Read `CLAUDE.md` for project architecture
2. Read `Docs/GDD.md` for game design context
3. Search existing scripts related to this feature using Grep/Glob
4. Read the relevant source files to understand integration points
5. Write the design document to `Docs/designs/$1.md` using the template at `${CLAUDE_SKILL_DIR}/templates/feature-design.md`

## Critical Requirements

- Every file change MUST be assigned to exactly one programmer in the File Ownership Map
- Every cross-domain dependency MUST have an Interface Contract with exact C# signatures
- The Manual Test Plan must be step-by-step instructions verifiable in Unity Editor
- Mark unresolved decisions with `[DECISION NEEDED]`
