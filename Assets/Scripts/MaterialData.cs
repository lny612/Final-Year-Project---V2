using System;
using UnityEngine;

/// <summary>
/// Runtime-only data class for a generated wand material.
/// materialType is either "wood" or "core".
/// </summary>
[Serializable]
public class MaterialData
{
    public string materialType;      // "wood" or "core"
    public string name;
    public int    price;
    public string imagePrompt;       // sent to ComfyUI
    public string attributes;        // both types

    // Wood-only
    public string personalityMatch;

    // Core-only
    public string elementalAffinity;
    public string special;

    // Runtime image — survives scene transitions via GameManager inventory
    [NonSerialized] public Texture2D generatedImage;
}
