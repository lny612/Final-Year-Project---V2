using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    public const string SCENE_CUSTOMER   = "CustomerGeneratorTest";
    public const string SCENE_MARKET     = "MaterialGeneratorTest";
    public const string SCENE_CRAFTING   = "CraftingScene";
    public const string SCENE_EVALUATION = "EvaluationScene";

    // ── Shared state ──────────────────────────────────────────────

    [HideInInspector] public CustomerOrder      currentCustomer;
    [HideInInspector] public List<MaterialData> availableMaterials = new();
    [HideInInspector] public List<MaterialData> inventory          = new();
    [HideInInspector] public int                playerGold         = 500;
    [HideInInspector] public int                playerReputation   = 0;
    [HideInInspector] public WandResult         currentWandResult;

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

    // ── Round reset (called by EvaluationManager "Next Customer") ─

    public void StartNextRound()
    {
        currentCustomer    = null;
        availableMaterials = new List<MaterialData>();
        currentWandResult  = null;
        // inventory, playerGold, playerReputation are preserved
        LoadScene(SCENE_CUSTOMER);
    }
}
