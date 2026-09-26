using UnityEngine;
using Unity.Netcode;

public class MoneyBehaviour : NetworkBehaviour
{
    [SerializeField] private int value = 1000;
    public int Value => value;
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !collision.gameObject.CompareTag("Ofen")) return;

        GameManager.Instance.LoseCashRPC(value);
        EventManager.CashAmountChanged();
        NetworkObject.Despawn();
    }
}
