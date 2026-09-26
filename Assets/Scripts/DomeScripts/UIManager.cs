using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text cashTXT;

    private void OnEnable()
    {
        EventManager.OnCashAmountChanged += UpdateCashText;
    }

    //Unity will das unsubscriben in der OnDestroy idk
    private void OnDestroy()
    {
        EventManager.OnCashAmountChanged -= UpdateCashText;
    }

    private void Start()
    {
        UpdateCashText();
    }

    private void UpdateCashText()
    {
        cashTXT.text = "Current Cash: " + GameManager.Instance.CurrentCash.Value;
    }
}
