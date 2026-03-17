using System;
using UnityEngine;

/// <summary>
/// Holds the result of a wand crafting OpenAI call plus the generated pixel art image.
/// Stored in GameManager.Instance.currentWandResult.
/// </summary>
[Serializable]
public class WandResult
{
    public string    wandName;
    public string    description;
    public string[]  attributes;
    public string    imagePrompt;
    public Texture2D wandImage;   // filled after ComfyUI returns
}
