using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TODO-EDITOR: On EvaluationScene, create a full-screen UI Panel "RentPaymentPanel"
//   under Canvas (anchored stretch-fill, initially inactive). Dim background via a
//   semi-transparent Image behind. Inside, add:
//     - DialogueText (TMP_Text, centered) — with a TypewriterText component targeting it
//     - GoldStatusText (TMP_Text, below dialogue, small)
//     - PayButton (Button with TMP label "Pay rent")
//     - PleadButton (Button with TMP label "Plead poverty" — flavor, same outcome)
//   Add RentPaymentUI component to RentPaymentPanel and wire all fields.
//   Then assign this component on EvaluationManager.rentPaymentUI.

/// <summary>
/// Modal that appears on the evaluation screen at the end of day 3 and day 6.
/// If the player has enough gold, they pay and continue. If not, they go
/// bankrupt — triggers the Bankrupt ending.
/// </summary>
[DisallowMultipleComponent]
public class RentPaymentUI : MonoBehaviour
{
    public enum RentResult { Paid, Bankrupt }

    [Header("Panel")]
    [Tooltip("Root GameObject of the modal panel, inactive at start.")]
    public GameObject panel;

    [Header("Dialogue")]
    public TypewriterText dialogueTypewriter;
    public TMP_Text       goldStatusText;

    [Header("Buttons")]
    public Button payButton;
    public Button pleadButton;        // flavor-only for the Paid branch; may be null
    public Button acceptFateButton;   // shown only on Bankrupt branch; may be null

    [Header("Landlord lines")]
    [TextArea] public string payLine =
        "Mr. Grimsby tips his hat (barely). \"On time. Rare. Refreshing.\" "
        + "He takes the coin and is already walking away.";
    [TextArea] public string bankruptLine =
        "Mr. Grimsby does not raise his voice. He does not need to. "
        + "\"The gnomes will be here at dawn.\" Outside, something small and brass is already knocking.";

    private Action<RentResult> _callback;
    private bool               _wired;

    private void EnsureWired()
    {
        if (_wired) return;
        _wired = true;
        payButton?.onClick.AddListener(OnPay);
        pleadButton?.onClick.AddListener(OnPay);        // plead → pay, flavor only
        acceptFateButton?.onClick.AddListener(OnAcceptFate);
    }

    /// <summary>Show the rent modal. <paramref name="onResolved"/> fires when
    /// the player has dismissed it (either paid or accepted bankruptcy).</summary>
    public void Show(int rentAmount, Action<RentResult> onResolved)
    {
        EnsureWired();
        _callback = onResolved;
        if (panel != null) panel.SetActive(true);

        int gold = GameManager.Instance != null ? GameManager.Instance.playerGold : 0;
        bool canPay = gold >= rentAmount;

        if (goldStatusText != null)
        {
            goldStatusText.text = canPay
                ? $"You have {gold}g · Rent {rentAmount}g · After rent: {gold - rentAmount}g"
                : $"You have {gold}g · Rent {rentAmount}g · <color=#D44>Short by {rentAmount - gold}g</color>";
        }

        if (payButton         != null) payButton.gameObject.SetActive(canPay);
        if (pleadButton       != null) pleadButton.gameObject.SetActive(canPay);
        if (acceptFateButton  != null) acceptFateButton.gameObject.SetActive(!canPay);

        string line = canPay ? payLine : bankruptLine;
        if (dialogueTypewriter != null) StartCoroutine(dialogueTypewriter.Play(line));
    }

    private void OnPay()
    {
        if (_callback == null) return;
        int rent = GameManager.Instance != null ? GameManager.Instance.GetRentDueToday() : 0;
        GameManager.Instance?.SpendGold(rent);
        Close();
        _callback?.Invoke(RentResult.Paid);
    }

    private void OnAcceptFate()
    {
        if (_callback == null) return;
        if (GameManager.Instance != null)
            GameManager.Instance.bankruptedOnDay3 = (GameManager.Instance.currentDay == 3);
        Close();
        _callback?.Invoke(RentResult.Bankrupt);
    }

    private void Close()
    {
        if (panel != null) panel.SetActive(false);
        _callback = null;
    }
}
