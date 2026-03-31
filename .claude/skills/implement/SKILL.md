---
name: implement
description: "Implement a C# feature from a design document. Reads the design doc, identifies owned files, implements changes, and self-checks against the spec."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
argument-hint: "[design-doc-path]"
---

Implement the feature specified in: $ARGUMENTS

## Process

1. **Read the design doc** at the path provided
2. **Identify your owned files** from the File Ownership Map — only edit files assigned to you
3. **Read all files you will modify** to understand their current state
4. **Read interface contracts** — implement your side exactly as specified (do not change signatures)
5. **Implement** new files first, then modifications to existing files
6. **Add `TODO-EDITOR:` comments** for Inspector wiring needed
7. **Self-check against the design doc:**
   - All methods from Public API section exist with correct signatures?
   - All interface contracts implemented on your side?
   - Event subscriptions have matching unsubscriptions?
   - Edge cases from the design doc handled?

## Conventions

- Coroutines for async operations (not async/await)
- `Newtonsoft.Json.Linq` for JSON parsing (not JsonUtility)
- `GameManager.Instance` for cross-scene state (null-check before use)
- `[DisallowMultipleComponent]` on all MonoBehaviour managers
- `Debug.LogError("[ClassName] message")` for error logging
- TextMeshPro for all text UI
- Public fields for Inspector references, `[Header]` for grouping
