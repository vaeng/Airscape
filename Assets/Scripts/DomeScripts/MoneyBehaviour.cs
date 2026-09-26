using UnityEngine;

public class MoneyBehaviour : MonoBehaviour
{
    [SerializeField] private int value = 1000;
    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Ofen"))
        {
            GameManager.Instance.LoseCashRPC(value);
            EventManager.CashAmountChanged();
            Destroy(gameObject);
        }
    }
}
