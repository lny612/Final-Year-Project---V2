using System.Collections;
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

    // Runtime-created hint glyph (shown when this material matches the player's memo).
    private GameObject _hintGlyph;

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

    /// <summary>
    /// Toggle the "matches your memo" hint glyph on this card. The glyph is
    /// created lazily on first enable so no prefab changes are needed.
    /// </summary>
    public void SetHintGlyph(bool active)
    {
        if (!active && _hintGlyph == null) return;
        if (_hintGlyph == null) _hintGlyph = BuildHintGlyph();
        if (_hintGlyph != null) _hintGlyph.SetActive(active);
    }

    private GameObject BuildHintGlyph()
    {
        var go = new GameObject("HintGlyph", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-6f, -6f);
        rt.sizeDelta = new Vector2(28f, 28f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text              = "✦";        // ✦ four-pointed star
        tmp.fontSize          = 24f;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = new Color(1f, 0.85f, 0.35f, 0.95f);
        tmp.raycastTarget     = false;
        tmp.enableVertexGradient = false;
        return go;
    }

    public void ShowPlaceholder()
    {
        if (placeholder    != null) placeholder.gameObject.SetActive(true);
        if (materialImage  != null) materialImage.gameObject.SetActive(false);
    }

    public void SetImage(Texture2D tex)
    {
        if (materialImage == null) return;

        materialImage.texture = tex;
        materialImage.gameObject.SetActive(true);

        // Our own GameObject must be active to host the fade coroutine. Cards
        // are sometimes constructed inactive then activated by the layout pass.
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        var c = materialImage.color;
        if (gameObject.activeInHierarchy)
        {
            c.a = 0f;
            materialImage.color = c;
            StartCoroutine(CrossfadeImage());
        }
        else
        {
            // Parent inactive (legacy uGUI canvas disabled, or off-screen list).
            // Skip the fade — snap to opaque so the texture is visible the moment
            // the parent activates.
            c.a = 1f;
            materialImage.color = c;
            if (placeholder != null) placeholder.gameObject.SetActive(false);
        }
    }

    private IEnumerator CrossfadeImage()
    {
        const float duration = 0.3f;
        float elapsed = 0f;
        Color c = materialImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Clamp01(elapsed / duration);
            materialImage.color = c;
            yield return null;
        }

        c.a = 1f;
        materialImage.color = c;
        // Hide placeholder once real image is fully visible
        if (placeholder != null) placeholder.gameObject.SetActive(false);
    }
}
