using UnityEngine;

public class MoneyChest : MonoBehaviour
{
    private int startAmount = GameManager.Instance.CurrentCash.Value;
    private int currentAmount;

    [SerializeField] private GameObject moneyPrefab;

    private void Start()
    {
        startAmount = GameManager.Instance.CurrentCash.Value;
        currentAmount = startAmount;
    }

    private void TakeCash()
    {
        currentAmount -= moneyPrefab.GetComponent<MoneyBehaviour>().Value;
    }
}
