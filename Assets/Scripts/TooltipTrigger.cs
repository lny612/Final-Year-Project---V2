using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to any material card or RawImage to show the MaterialTooltip on hover.
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public MaterialData data;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (data != null && MaterialTooltip.Instance != null)
            MaterialTooltip.Instance.Show(data);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (MaterialTooltip.Instance != null)
            MaterialTooltip.Instance.Hide();
    }
}
