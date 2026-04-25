using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum EndingType { Royal, Rival, Slum, BankruptEarly, BankruptLate }

/// <summary>
/// Persistent singleton holding all shared state across scenes.
/// Add this GO to every scene; duplicates are destroyed automatically.
/// </summary>
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────

    public static GameManager Instance { get; private set; }

    // ── Scene name constants ───────────────────────────────────────

    public const string SCENE_TITLE      = "TitleScene";
    public const string SCENE_CUSTOMER   = "CustomerGeneratorTest";
    public const string SCENE_MARKET     = "MaterialGeneratorTest";
    public const string SCENE_CRAFTING   = "CraftingScene";
    public const string SCENE_EVALUATION = "EvaluationScene";
    public const string SCENE_MORNING    = "MorningScene";
    public const string SCENE_ENDING     = "EndingScene";

    // ── Progression constants (tweak these to tune difficulty) ────

    public const int TOTAL_DAYS        = 7;
    public const int STARTING_GOLD     = 500;
    public const int ROYAL_REP_MIN     = 90;   // ≥ this → Royal ending
    public const int RIVAL_REP_MIN     = 30;   // in [RIVAL_REP_MIN, ROYAL_REP_MIN) → Rival

    // Rent is billed at the END of day 3 (before day 4) and day 6 (before day 7).
    public static readonly int[] RENT_DUE_DAYS = { 3, 6 };
    public static readonly int[] RENT_AMOUNTS  = { 250, 400 };

    // ── Shared round state ───────────────────────────────────────

    [HideInInspector] public CustomerOrder      currentCustomer;
    [HideInInspector] public PlayerMemo         currentMemo;
    [HideInInspector] public List<MaterialData> availableMaterials = new();
    [HideInInspector] public List<MaterialData> inventory          = new();
    [HideInInspector] public int                playerGold         = STARTING_GOLD;
    [HideInInspector] public int                playerReputation   = 0;
    [HideInInspector] public WandResult         currentWandResult;
    [HideInInspector] public char               craftingQualityGrade = 'A';

    // ── Progression state (persists across scenes, resets on new run) ─

    [HideInInspector] public int  currentDay         = 1;
    [HideInInspector] public int  peakReputation     = 0;
    [HideInInspector] public int  wandsCrafted       = 0;
    [HideInInspector] public int  lettersReceived    = 0;
    [HideInInspector] public bool bankruptedOnDay3   = false;

    // ── Unity lifecycle ────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Gold ──────────────────────────────────────────────────────

    public void AddGold(int amount)  => playerGold += amount;
    public void SpendGold(int amount) => playerGold -= amount;
    public bool CanAfford(int price)  => playerGold >= price;

    // ── Inventory ─────────────────────────────────────────────────

    public void AddToInventory(MaterialData material)    => inventory.Add(material);
    public void RemoveFromInventory(MaterialData material) => inventory.Remove(material);

    // ── Navigation ────────────────────────────────────────────────

    public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);

    // ── Round reset (legacy — kept for MinigameTest + any standalone entry) ─

    public void StartNextRound()
    {
        currentCustomer    = null;
        currentMemo        = null;
        availableMaterials = new List<MaterialData>();
        currentWandResult      = null;
        craftingQualityGrade   = 'A';
        LoadScene(SCENE_CUSTOMER);
    }

    // ── Day progression (new flow used by EvaluationManager) ──────

    public void AdvanceToNextDay()
    {
        currentDay++;
        currentCustomer      = null;
        currentMemo          = null;
        availableMaterials   = new List<MaterialData>();
        currentWandResult    = null;
        craftingQualityGrade = 'A';
        LoadScene(SCENE_MORNING);
    }

    public bool IsRentDueToday() => System.Array.IndexOf(RENT_DUE_DAYS, currentDay) >= 0;

    public int GetRentDueToday()
    {
        int idx = System.Array.IndexOf(RENT_DUE_DAYS, currentDay);
        return idx >= 0 ? RENT_AMOUNTS[idx] : 0;
    }

    public EndingType DetermineEnding()
    {
        if (playerReputation >= ROYAL_REP_MIN) return EndingType.Royal;
        if (playerReputation >= RIVAL_REP_MIN) return EndingType.Rival;
        return EndingType.Slum;
    }

    public void ResetForNewPlaythrough()
    {
        currentCustomer      = null;
        currentMemo          = null;
        availableMaterials   = new List<MaterialData>();
        inventory            = new List<MaterialData>();
        playerGold           = STARTING_GOLD;
        playerReputation     = 0;
        currentWandResult    = null;
        craftingQualityGrade = 'A';
        currentDay           = 1;
        peakReputation       = 0;
        wandsCrafted         = 0;
        lettersReceived      = 0;
        bankruptedOnDay3     = false;
    }

    // ── Editor-only dev shortcuts (speed up testing without 7 playthroughs) ─

#if UNITY_EDITOR
    [ContextMenu("Dev/Jump to Day 3 (pre-rent)")]
    private void DevJumpToDay3() { currentDay = 3; }

    [ContextMenu("Dev/Jump to Day 6 (pre-rent)")]
    private void DevJumpToDay6() { currentDay = 6; }

    [ContextMenu("Dev/Jump to Day 7 (pre-ending)")]
    private void DevJumpToDay7() { currentDay = 7; }

    [ContextMenu("Dev/Force Royal-tier reputation (120)")]
    private void DevForceRoyalRep() { playerReputation = 120; peakReputation = 120; }

    [ContextMenu("Dev/Force Rival-tier reputation (55)")]
    private void DevForceRivalRep() { playerReputation = 55; peakReputation = 55; }

    [ContextMenu("Dev/Force Slum-tier reputation (-20)")]
    private void DevForceSlumRep() { playerReputation = -20; }

    [ContextMenu("Dev/Empty wallet (test bankruptcy)")]
    private void DevEmptyWallet() { playerGold = 50; }
#endif
}
