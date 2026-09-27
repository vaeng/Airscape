using UnityEngine;
using System;
public class EventManager : MonoBehaviour
{
    public static event Action OnCashAmountChanged;

    public static void CashAmountChanged()
    {
        OnCashAmountChanged?.Invoke();
    }
}
