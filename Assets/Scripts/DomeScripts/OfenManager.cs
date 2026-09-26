using UnityEngine;

public class OfenManager : MonoBehaviour
{
    [Header("Runtime Variables")]
    public float CurrentEnergy { get; private set; } = 100f;
    [SerializeField] private float currentEfficiency = 1f;

    [Header("Settings")]
    [SerializeField] private float energyConsumptionRate = 5f;
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float efficiency = 1f;

    private void Awake()
    {
        CurrentEnergy = 100f;
        currentEfficiency = efficiency;
    }

    public void AddEnergy(float amount)
    {
        Debug.Log($"Really honestly adding {amount} energy to the oven.");
        CurrentEnergy += amount;
        CurrentEnergy = Mathf.Clamp(CurrentEnergy, 0f, maxEnergy);
    }

    public float GetEnergy(float deltaTime = 0f)
    {
        if (deltaTime <= 0f) deltaTime = Time.deltaTime;

        CurrentEnergy -= energyConsumptionRate * deltaTime;
        return CurrentEnergy * currentEfficiency;
    }
}
