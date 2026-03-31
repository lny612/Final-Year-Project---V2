---
name: implement-vfx
description: "Implement VFX, particle systems, shaders, or visual effects from a design document. Follows URP conventions and procedural particle patterns."
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
argument-hint: "[design-doc-path]"
---

Implement the visual effects specified in: $ARGUMENTS

## Process

1. **Read the design doc** at the path provided
2. **Identify your owned files** from the File Ownership Map
3. **Implement** following URP constraints and procedural particle patterns
4. **Add `TODO-EDITOR:` comments** for material assignments, layer masks, or prefab wiring
5. **Self-check:**
   - All ParticleSystem components properly stopped/destroyed after use?
   - Using `MaterialPropertyBlock` instead of `material.Set*()` for per-instance changes?
   - Shader uses URP-compatible bases (no legacy shaders)?

## URP Shader Template

For custom shaders, start from:
```hlsl
Shader "Custom/YourShaderName"
{
    Properties { /* ... */ }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            ENDHLSL
        }
    }
}
```

## Procedural Particle Pattern

```csharp
var go = new GameObject("EffectName");
var ps = go.AddComponent<ParticleSystem>();
var main = ps.main;
main.duration = 0.5f;
main.startLifetime = 0.3f;
main.startSize = 0.2f;
main.startColor = Color.cyan;
main.loop = false;
ps.Play();
Destroy(go, main.duration + main.startLifetime.constantMax);
```
