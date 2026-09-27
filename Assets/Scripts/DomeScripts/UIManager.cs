using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text cashTXT;
    [SerializeField] private Slider throwChargeSlider;

    // Screen Space - Camera canvas; rebound to the local player camera once the scene camera is disabled.
    [SerializeField] private Canvas hudCanvas;

    [SerializeField] private Slider progressSlider;
    [Tooltip("Seconds the progress slider needs to fill up completely once the game is running.")]
    [SerializeField, Min(0.1f)] private float progressDuration = 120f;
    [SerializeField] private float progressTimer;

    private void Start()
    {
        GameManager.Instance.CurrentCash.OnValueChanged += OnCashChanged;
        UpdateCashText();

        throwChargeSlider.minValue = 0f;
        throwChargeSlider.gameObject.SetActive(false);

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
        }
    }

    private void Update()
    {
        BindCanvasToPlayerCamera();
        UpdateThrowChargeSlider();
        UpdateProgressSlider();
    }

    private void UpdateProgressSlider()
    {
        if (progressSlider == null) return;
        // Only fills while the game is running.
        if (GameManager.Instance == null || GameManager.Instance.CurrentState.Value != GameState.Playing) return;

        progressTimer = Mathf.Min(progressTimer + Time.deltaTime, progressDuration);
        progressSlider.value = progressTimer / progressDuration;
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
        bool visible = interaction != null && interaction.IsChargeBarVisible;

        if (throwChargeSlider.gameObject.activeSelf != visible)
            throwChargeSlider.gameObject.SetActive(visible);

        if (!visible) return;

        // Shared between throw charge and repair progress.
        throwChargeSlider.maxValue = 1f;
        throwChargeSlider.value = interaction.ChargeBar01;
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
