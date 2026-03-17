using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the UI elements of a single material card.
/// Attach to the MaterialCard prefab root.
/// </summary>
[DisallowMultipleComponent]
public class MaterialCardUI : MonoBehaviour
{
    [Header("Image")]
    [Tooltip("RawImage shown once ComfyUI returns the generated image.")]
    public RawImage materialImage;

    [Tooltip("Placeholder shown while the image is loading.")]
    public Image placeholder;

    [Header("Text Fields")]
    public TMP_Text nameText;
    public TMP_Text priceText;

    [Tooltip("Elemental affinity (core) or personality match (wood).")]
    public TMP_Text field1Text;

    [Tooltip("Attributes — both types.")]
    public TMP_Text field2Text;

    [Tooltip("Special — core only. Hidden automatically for wood.")]
    public TMP_Text field3Text;

    [Header("Buy / Sold Out")]
    public Button   buyButton;
    public TMP_Text soldOutLabel;

    // ── Public API ─────────────────────────────────────────────────

    public void SetData(MaterialData data)
    {
        if (nameText  != null) nameText.text  = data.name;
        if (priceText != null) priceText.text = $"{data.price}g";

        if (data.materialType == "core")
        {
            if (field1Text != null) field1Text.text = $"<b>Affinity:</b>  {data.elementalAffinity}";
            if (field2Text != null) field2Text.text = $"<b>Attributes:</b>  {data.attributes}";
            if (field3Text != null)
            {
                field3Text.gameObject.SetActive(true);
                field3Text.text = $"<b>Special:</b>  {data.special}";
            }
        }
        else // wood
        {
            if (field1Text != null) field1Text.text = $"<b>Personality Match:</b>  {data.personalityMatch}";
            if (field2Text != null) field2Text.text = $"<b>Attributes:</b>  {data.attributes}";
            if (field3Text != null) field3Text.gameObject.SetActive(false);
        }

        ShowPlaceholder();
        if (buyButton    != null) buyButton.gameObject.SetActive(true);
        if (soldOutLabel != null) soldOutLabel.gameObject.SetActive(false);
    }

    public void SetSoldOut()
    {
        if (buyButton    != null) buyButton.gameObject.SetActive(false);
        if (soldOutLabel != null) soldOutLabel.gameObject.SetActive(true);
    }

    public void ShowPlaceholder()
    {
        if (placeholder    != null) placeholder.gameObject.SetActive(true);
        if (materialImage  != null) materialImage.gameObject.SetActive(false);
    }

    public void SetImage(Texture2D tex)
    {
        if (placeholder   != null) placeholder.gameObject.SetActive(false);
        if (materialImage != null)
        {
            materialImage.gameObject.SetActive(true);
            materialImage.texture = tex;
        }
    }
}
