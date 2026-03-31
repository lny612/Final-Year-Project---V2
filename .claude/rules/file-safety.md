---
paths:
  - "Assets/**/*"
---

# Unity File Safety Rules

## NEVER Create or Modify
- `.unity` scene files (binary/serialized YAML)
- `.prefab` prefab files
- `.asset` ScriptableObject/settings files
- `.meta` files (Unity auto-generates these when it detects new files)
- Anything under `Library/`, `Temp/`, `obj/`, `Logs/`

## When Inspector Wiring Is Needed
Add a `TODO-EDITOR:` comment with exact instructions:
```csharp
// TODO-EDITOR: Add [ComponentName] component to [PrefabName] prefab (Assets/Prefabs/[path])
// TODO-EDITOR: Assign [fieldName] field in Inspector (create URP/Lit material, [color] tint)
// TODO-EDITOR: Create new UI element under Canvas > [hierarchy path] (Image, anchored [position])
```

## New Script Files
- Place in `Assets/Scripts/` (flat structure — matches existing project layout)
- Unity generates the `.meta` file automatically on next Editor focus
- Do NOT create `.meta` files manually

## Scene Setup
- Document required scene changes in `TODO-EDITOR:` comments or design docs
- Include: GameObject name, component to add, field values, hierarchy position
