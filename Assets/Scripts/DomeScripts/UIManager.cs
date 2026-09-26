using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text cashTXT;
    [SerializeField] private Slider throwChargeSlider;

    // Screen Space - Camera canvas; rebound to the local player camera once the scene camera is disabled.
    [SerializeField] private Canvas hudCanvas;

    private void Start()
    {
        GameManager.Instance.CurrentCash.OnValueChanged += OnCashChanged;
        UpdateCashText();

        throwChargeSlider.minValue = 0f;
        throwChargeSlider.gameObject.SetActive(false);
    }

    private void Update()
    {
        BindCanvasToPlayerCamera();
        UpdateThrowChargeSlider();
    }

    private void BindCanvasToPlayerCamera()
    {
        if (hudCanvas == null || hudCanvas.renderMode != RenderMode.ScreenSpaceCamera) return;

        var interaction = PlayerInteraction.Local;
        if (interaction == null) return;

        var movement = interaction.GetComponent<PlayerMovement>();
        var cam = movement != null ? movement.ActiveCamera : null;

        if (cam != null && hudCanvas.worldCamera != cam)
            hudCanvas.worldCamera = cam;
    }

    private void UpdateThrowChargeSlider()
    {
        var interaction = PlayerInteraction.Local;
        bool charging = interaction != null && interaction.IsChargingThrow;

        if (throwChargeSlider.gameObject.activeSelf != charging)
            throwChargeSlider.gameObject.SetActive(charging);

        if (!charging) return;

        throwChargeSlider.maxValue = interaction.MaxThrowForce;
        throwChargeSlider.value = interaction.CurrentThrowForce;
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
