using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton tooltip panel that floats near the cursor and shows MaterialData.
/// Place one instance in each scene that has material cards.
/// </summary>
[DisallowMultipleComponent]
public class MaterialTooltip : MonoBehaviour
{
    public static MaterialTooltip Instance { get; private set; }

    [Header("Panel")]
    public RectTransform panelRect;
    public Image         background;

    [Header("Text Fields")]
    public TMP_Text nameText;
    public TMP_Text priceText;
    public TMP_Text field1Text;  // affinity or personality
    public TMP_Text field2Text;  // attributes
    public TMP_Text field3Text;  // special (core only)

    private Canvas _canvas;
    private bool   _visible;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    private void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!_visible) return;
        MoveTowardsCursor();
    }

    public void Show(MaterialData data)
    {
        gameObject.SetActive(true);
        _visible = true;

        nameText.text  = $"<b>{data.name}</b>";
        priceText.text = $"{data.price}g";

        if (data.materialType == "core")
        {
            field1Text.text = $"<b>Affinity:</b>  {data.elementalAffinity}";
            field2Text.text = $"<b>Attributes:</b>  {data.attributes}";
            field3Text.gameObject.SetActive(true);
            field3Text.text = $"<b>Special:</b>  {data.special}";
        }
        else
        {
            field1Text.text = $"<b>Personality Match:</b>  {data.personalityMatch}";
            field2Text.text = $"<b>Attributes:</b>  {data.attributes}";
            field3Text.gameObject.SetActive(false);
        }

        MoveTowardsCursor();
    }

    public void Hide()
    {
        _visible = false;
        gameObject.SetActive(false);
    }

    private void MoveTowardsCursor()
    {
        if (_canvas == null) return;

        Vector2 localPt;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.GetComponent<RectTransform>(),
            Input.mousePosition,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
            out localPt);

        Vector2 offset = new Vector2(16f, -16f);
        Vector2 desired = localPt + offset;

        // Clamp so panel never leaves the canvas
        Rect canvasRect = _canvas.GetComponent<RectTransform>().rect;
        float w = panelRect.sizeDelta.x;
        float h = panelRect.sizeDelta.y;
        desired.x = Mathf.Clamp(desired.x, canvasRect.xMin,         canvasRect.xMax - w);
        desired.y = Mathf.Clamp(desired.y, canvasRect.yMin + h,      canvasRect.yMax);

        panelRect.anchoredPosition = desired;
    }
}
