---
paths:
  - "Assets/Scripts/**/*.cs"
---

# C# Conventions for This Project

## Naming
- `PascalCase` for public fields, methods, properties, classes, enums
- `camelCase` for private/local variables and parameters
- `_camelCase` prefix for private backing fields (e.g. `_slot1`, `_busy`, `_corePool`)
- UPPER_SNAKE_CASE for constants (e.g. `SCENE_CUSTOMER`, `SCENE_MARKET`)

## Singleton Pattern
GameManager uses `DontDestroyOnLoad` with duplicate-destroy guard:
```csharp
public static GameManager Instance { get; private set; }

private void Awake()
{
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}
```
Access via `GameManager.Instance`. Always null-check before use in scene managers.

## Class Attributes
- `[DisallowMultipleComponent]` on all MonoBehaviour managers and UI controllers

## Events
- No event system currently used — scene managers communicate via `GameManager.Instance` shared state and `SceneManager.LoadScene()`
- If adding events, use `System.Action<T>` (not UnityEvent)

## Async
- **Coroutines** (`IEnumerator` + `StartCoroutine`) for all async operations
- API call pattern: coroutine yields `UnityWebRequest.SendWebRequest()`, uses callback `Action<T>` for results
- ComfyUI polling: `WaitForSeconds(1.5f)` loop with 60s timeout

## Inspector Fields
- `public` fields for Inspector-exposed references (matches existing style)
- `[Header("Section")]` to group related fields
- `[Tooltip("...")]` for non-obvious fields
- `[HideInInspector]` on public fields that shouldn't appear in Inspector (e.g. GameManager shared state)

## API Call Pattern
All OpenAI/ComfyUI calls follow this structure:
1. Load API key from `StreamingAssets/config.json` via `LoadApiKey` coroutine
2. Build `JObject` request body with `Newtonsoft.Json.Linq`
3. POST via `UnityWebRequest`, yield `SendWebRequest()`
4. Parse response with `JObject.Parse()`, extract content
5. Use `StripCodeFences()` before JSON parsing (GPT sometimes wraps in markdown)
6. Callback with result or null on failure

## JSON Handling
- Use `Newtonsoft.Json.Linq` (`JObject`, `JArray`) — not `JsonUtility`
- All OpenAI responses expected as raw JSON (no markdown), parsed with `JObject.Parse()`

## Scene Navigation
- Use `GameManager.Instance.LoadScene(GameManager.SCENE_*)` with string constants
- Scene names: `CustomerGeneratorTest`, `MaterialGeneratorTest`, `CraftingScene`, `EvaluationScene`

## UI Construction
- Prefab-based for repeated elements (MaterialCard prefab)
- Procedural `GameObject` creation for dynamic lists (inventory rows in CraftingManager)
- TextMeshPro (`TMP_Text`, `TextMeshProUGUI`) for all text — no legacy `Text`
