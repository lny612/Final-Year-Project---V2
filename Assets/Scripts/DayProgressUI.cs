using UnityEngine;
using UnityEngine.UI;

// TODO-EDITOR: On EvaluationScene, create a horizontal layout "DayProgressHeader"
//   under Canvas (top-center, anchored top). Add 7 child Image GameObjects "Dot0".."Dot6"
//   (small circles, uniform spacing). Under Dot2 and Dot5, add a child GameObject
//   "RentIcon" holding a 💰 emoji TMP_Text or coin Image.
//   Add DayProgressUI component to DayProgressHeader and wire:
//     dayDots[0..6] = Dot0..Dot6 Images
//     rentIcons[2]  = Dot2/RentIcon     (day 3)
//     rentIcons[5]  = Dot5/RentIcon     (day 6)
//     (other rentIcons[] can be left null)
//   Then assign this component on EvaluationManager.dayProgressUI.

/// <summary>
/// Renders the 7-day calendar dots (● ● ● ○ ○ ○ ○) at top of the
/// evaluation screen. Reads GameManager.currentDay on Refresh().
/// </summary>
[DisallowMultipleComponent]
public class DayProgressUI : MonoBehaviour
{
    [Header("Dots (size 7, one per day)")]
    public Image[] dayDots = new Image[7];

    [Header("Rent icons (only indices 2 and 5 are used — days 3 and 6)")]
    public GameObject[] rentIcons = new GameObject[7];

    [Header("Colors")]
    public Color pastDayColor  = Color.white;
    public Color todayColor    = new Color(1f, 0.85f, 0.3f);
    public Color futureColor   = new Color(1f, 1f, 1f, 0.3f);

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        int today = GameManager.Instance != null ? GameManager.Instance.currentDay : 1;

        for (int i = 0; i < dayDots.Length; i++)
        {
            if (dayDots[i] == null) continue;
            int dayNum = i + 1;
            dayDots[i].color = dayNum < today  ? pastDayColor
                             : dayNum == today ? todayColor
                             : futureColor;
        }

        // Rent icons above days 3 and 6 are always visible when wired;
        // dim them once past.
        for (int i = 0; i < rentIcons.Length; i++)
        {
            if (rentIcons[i] == null) continue;
            int dayNum = i + 1;
            bool isRentDay = System.Array.IndexOf(GameManager.RENT_DUE_DAYS, dayNum) >= 0;
            rentIcons[i].SetActive(isRentDay);
        }
    }
}
