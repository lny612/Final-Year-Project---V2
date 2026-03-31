---
name: review-code
description: "Review implemented code against its design document. Checks correctness, convention compliance, interface contract fulfillment, and completeness."
context: fork
agent: Explore
allowed-tools: Read, Glob, Grep
argument-hint: "[design-doc-path]"
---

Review the implementation against the design document at: $ARGUMENTS

## Review Checklist

Read the design doc, then check every file listed in its File Ownership Map:

### 1. Completeness
- [ ] All methods listed in Public API exist with correct signatures
- [ ] All new files listed in New Files were created
- [ ] All modifications listed in Modified Files were made

### 2. Interface Contracts
- [ ] Every event/method in Interface Contracts is implemented on BOTH sides
- [ ] Signatures match exactly (parameter types, return types, event delegate types)
- [ ] Events subscribed and unsubscribed in correct lifecycle methods

### 3. Project Conventions
- [ ] Singleton access null-checked (`GameManager.Instance?.`)
- [ ] Coroutines used for async (not async/await)
- [ ] `[DisallowMultipleComponent]` on MonoBehaviour managers
- [ ] TextMeshPro used for text (not legacy Text)
- [ ] Newtonsoft.Json.Linq for JSON parsing (not JsonUtility)

### 4. Edge Cases (from design doc)
- [ ] Each numbered edge case is handled in code
- [ ] Null checks on references that could be destroyed at runtime

### 5. Unity Safety
- [ ] No `.unity`, `.prefab`, `.asset`, `.meta` files were modified
- [ ] `TODO-EDITOR:` comments present for any Inspector wiring needed

## Output

Write a review report to `Docs/reviews/review-<feature-name>.md` with:
- PASS/FAIL for each checklist item
- Specific line references for any failures
- Suggested fixes for each failure
