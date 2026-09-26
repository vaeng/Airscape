using UnityEngine;
using Unity.Netcode;

public class MoneyBehaviour : NetworkBehaviour
{
    [SerializeField] private int value = 1000;
    [SerializeField] private float energyAmount = 20f;
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer ||
            !collision.gameObject.TryGetComponent<OfenManager>(out OfenManager ofenManager))
        {
            return;
        }

        ofenManager.AddEnergy(energyAmount);
        Debug.Log($"Added {energyAmount} energy to the oven.");

        GameManager.Instance.LoseCashRPC(value);
        EventManager.CashAmountChanged();
        NetworkObject.Despawn();
    }
}
