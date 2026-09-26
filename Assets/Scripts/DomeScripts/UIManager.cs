using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text cashTXT;

    private void Start()
    {
        GameManager.Instance.CurrentCash.OnValueChanged += OnCashChanged;
        UpdateCashText();
    }

    private void OnDestroy()
    {
        GameManager.Instance.CurrentCash.OnValueChanged -= OnCashChanged;
    }

    private void OnCashChanged(int previousValue, int newValue)
    {
        UpdateCashText();
    }

    private void UpdateCashText()
    {
        cashTXT.text = "Current Cash: " + GameManager.Instance.CurrentCash.Value;
    }
}
