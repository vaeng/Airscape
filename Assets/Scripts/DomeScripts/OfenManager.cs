using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class OfenManager : NetworkBehaviour
{
    private const int MinEfficiency = 1;
    private const int MaxEfficiency = 3;

    [Header("Runtime Variables")]
    [SerializeField] public float CurrentEnergy = 100f;
    [SerializeField] private float currentEfficiency = 1f;
    [SerializeField] private int moneyInsertedCount;
    [SerializeField] private float decayTimer;
    /// <summary>Effective lift efficiency: 0 while the balloon is damaged, otherwise the efficiency level.</summary>
    public float Efficiency => IsDamaged ? 0f : currentEfficiency;
    /// <summary>True while at least one repair point of this oven's balloon is open.</summary>
    public bool IsDamaged => repairSpawner != null && repairSpawner.ActiveCount > 0;

    [Header("Settings")]
    [SerializeField] private float energyConsumptionRate = 5f;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField, Range(MinEfficiency, MaxEfficiency)] private int efficiency = 1;

    [Header("Efficiency")]
    [Tooltip("How many money items must be thrown in to raise the efficiency by one level.")]
    [SerializeField, Min(1)] private int moneyPerEfficiencyLevel = 1;
    [Tooltip("Seconds until the efficiency drops by one level. Resets whenever the level changes.")]
    [SerializeField, Min(0.1f)] private float secondsPerEfficiencyLevel = 30f;
    [Tooltip("Seconds until the efficiency drops by one level while at least one repair point is open.")]
    [SerializeField, Min(0.1f)] private float secondsPerEfficiencyLevelWhileRepairing = 10f;

    [Header("References")]
    [SerializeField] private Transform firePosition;
    [SerializeField] private Transform firePositionTop;
    [SerializeField] private GameObject fireVFXSmall;
    [SerializeField] private GameObject fireVFXMedium;
    [SerializeField] private GameObject fireVFXLarge;
    [SerializeField] private RepairSpawner repairSpawner;
    [SerializeField] private GameObject smokeVFX;

    [Header("Sounds")]
    [SerializeField] private AudioSource moneyBurnSFX;

    private readonly NetworkVariable<int> networkEfficiency = new(1);

    private GameObject[] fireSmall, fireMedium, fireLarge;
    private int currentFireLevel = -1;

    /// <summary>1 = small, 2 = medium, 3 = large fire.</summary>
    public int EfficiencyLevel => Mathf.Clamp(networkEfficiency.Value, MinEfficiency, MaxEfficiency);

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
            SetEfficiency(efficiency);
        }
    }

    private void Update()
    {
        if (IsSpawned && IsServer && networkEfficiency.Value > MinEfficiency)
        {
            float duration = IsDamaged ? secondsPerEfficiencyLevelWhileRepairing : secondsPerEfficiencyLevel;
            // decayTimer is normalized (0..1) so switching duration mid-countdown keeps the progress
            decayTimer += Time.deltaTime / duration;
            if (decayTimer >= 1f)
            {
                SetEfficiency(networkEfficiency.Value - 1);
            }
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

        StartCoroutine(BurnMoneyVFX());

        moneyInsertedCount++;
        if (moneyInsertedCount < moneyPerEfficiencyLevel) return;

        moneyInsertedCount = 0;
        SetEfficiency(networkEfficiency.Value + 1);
    }

    /// <summary>Server only. Sets the efficiency level and restarts the decay timer.</summary>
    private void SetEfficiency(int level)
    {
        networkEfficiency.Value = Mathf.Clamp(level, MinEfficiency, MaxEfficiency);
        decayTimer = 0f;
    }

    public float GetEnergy(float deltaTime = 0f)
    {
        if (deltaTime <= 0f) deltaTime = Time.deltaTime;

        CurrentEnergy -= energyConsumptionRate * deltaTime;
        return CurrentEnergy * Efficiency;
    }

    /// <summary>Spawns one fire instance at every fire position (bottom and top).</summary>
    private GameObject[] SpawnFire(GameObject prefab)
    {
        if (prefab == null) return System.Array.Empty<GameObject>();

        var positions = new[] { firePosition, firePositionTop };
        var fires = new System.Collections.Generic.List<GameObject>();
        foreach (var pos in positions)
        {
            if (pos == null) continue;
            var fire = Instantiate(prefab, pos.position, pos.rotation, pos);
            fire.SetActive(false);
            fires.Add(fire);
        }
        return fires.ToArray();
    }

    private void UpdateFireVisuals()
    {
        int level = EfficiencyLevel;
        if (level == currentFireLevel) return;
        currentFireLevel = level;

        SetFiresActive(fireSmall, level == 1);
        SetFiresActive(fireMedium, level == 2);
        SetFiresActive(fireLarge, level == 3);
    }

    private static void SetFiresActive(GameObject[] fires, bool active)
    {
        if (fires == null) return;
        foreach (var fire in fires)
        {
            if (fire != null) fire.SetActive(active);
        }
    }

    public void PlayMoneyBurnSFX()
    {
        moneyBurnSFX?.Play();
    }

    IEnumerator BurnMoneyVFX()
    {
        var smoke = Instantiate(smokeVFX, firePosition.position, firePosition.rotation, firePosition);
        smoke.SetActive(true);
        yield return new WaitForSeconds(1f);
        if (smoke == null) yield break;

        // Stop emitting but let existing particles finish their lifetime before cleanup.
        var particleSystems = smoke.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        while (smoke != null && System.Array.Exists(particleSystems, ps => ps != null && ps.IsAlive(false)))
        {
            yield return null;
        }

        if (smoke != null) Destroy(smoke);
    }
}
