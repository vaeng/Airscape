using UnityEngine;
using Unity.Netcode;

public class MoneyBehaviour : NetworkBehaviour
{
    [SerializeField] private int value = 1000;
    [SerializeField] private float energyAmount = 20f;

    public int Value => value;
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Money collided with {collision.gameObject.name}");

        if (!IsServer)
        {
            return;
        }

        var ofenManager = collision.collider.gameObject.GetComponentInParent<OfenManager>();
        if (ofenManager == null)
        {
            return;
        }
        else
        {
            ofenManager.AddEnergy(energyAmount);
            ofenManager.InsertMoney();
            Debug.Log($"Added {energyAmount} energy to the oven.");
            ReduceCashAmount();
        }

        NetworkObject.Despawn();
    }

    public void ReduceCashAmount()
    {
        if (!IsServer)
        {
            return;
        }

        GameManager.Instance.LoseCashRPC(value);
        EventManager.CashAmountChanged();
    }
}
