using UnityEngine;
using Unity.Netcode;

public class OfenManager : NetworkBehaviour
{
    private const float MinEfficiency = 1f;
    private const float MaxEfficiency = 3f;

    [Header("Runtime Variables")]
    [SerializeField] public float CurrentEnergy = 100f;
    [SerializeField] private float currentEfficiency = 1f;
    [SerializeField] private int moneyInsertedCount;

    [Header("Settings")]
    [SerializeField] private float energyConsumptionRate = 5f;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField, Range(MinEfficiency, MaxEfficiency)] private float efficiency = 1f;

    [Header("Efficiency")]
    [Tooltip("How many money items must be thrown in to raise the efficiency by one level.")]
    [SerializeField, Min(1)] private int moneyPerEfficiencyLevel = 1;
    [Tooltip("Efficiency lost per second.")]
    [SerializeField] private float efficiencyDecayRate = 0.05f;

    [Header("References")]
    [SerializeField] private Transform firePosition;
    [SerializeField] private GameObject fireVFXSmall;
    [SerializeField] private GameObject fireVFXMedium;
    [SerializeField] private GameObject fireVFXLarge;

    private readonly NetworkVariable<float> networkEfficiency = new(1f);

    private GameObject fireSmall, fireMedium, fireLarge;
    private int currentFireLevel = -1;

    /// <summary>1 = small, 2 = medium, 3 = large fire.</summary>
    public int EfficiencyLevel => Mathf.Clamp(Mathf.CeilToInt(networkEfficiency.Value), 1, 3);

    private void Awake()
    {
        CurrentEnergy = 100f;
        currentEfficiency = efficiency;

        fireSmall = SpawnFire(fireVFXSmall);
        fireMedium = SpawnFire(fireVFXMedium);
        fireLarge = SpawnFire(fireVFXLarge);
        UpdateFireVisuals();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkEfficiency.Value = Mathf.Clamp(efficiency, MinEfficiency, MaxEfficiency);
        }
    }

    private void Update()
    {
        if (IsSpawned && IsServer && networkEfficiency.Value > MinEfficiency)
        {
            networkEfficiency.Value = Mathf.Max(MinEfficiency, networkEfficiency.Value - efficiencyDecayRate * Time.deltaTime);
        }

        currentEfficiency = networkEfficiency.Value;
        UpdateFireVisuals();
    }

    public void AddEnergy(float amount)
    {
        Debug.Log($"Really honestly adding {amount} energy to the oven.");
        CurrentEnergy += amount;
        CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0f, maxEnergy);
    }

    /// <summary>Server only. Counts a thrown-in money item and raises the efficiency by one level once enough money was inserted.</summary>
    public void InsertMoney()
    {
        if (!IsServer) return;

        moneyInsertedCount++;
        if (moneyInsertedCount < moneyPerEfficiencyLevel) return;

        moneyInsertedCount = 0;
        networkEfficiency.Value = Mathf.Min(MaxEfficiency, networkEfficiency.Value + 1f);
    }

    public float GetEnergy(float deltaTime = 0f)
    {
        if (deltaTime <= 0f) deltaTime = Time.deltaTime;

        CurrentEnergy -= energyConsumptionRate * deltaTime;
        return CurrentEnergy * currentEfficiency;
    }

    private GameObject SpawnFire(GameObject prefab)
    {
        if (prefab == null) return null;

        var fire = Instantiate(prefab, firePosition.position, firePosition.rotation, firePosition);
        fire.SetActive(false);
        return fire;
    }

    private void UpdateFireVisuals()
    {
        int level = EfficiencyLevel;
        if (level == currentFireLevel) return;
        currentFireLevel = level;

        if (fireSmall != null) fireSmall.SetActive(level == 1);
        if (fireMedium != null) fireMedium.SetActive(level == 2);
        if (fireLarge != null) fireLarge.SetActive(level == 3);
    }
}
