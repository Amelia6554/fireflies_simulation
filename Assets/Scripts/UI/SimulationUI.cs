using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SimulationUI : MonoBehaviour
{
    [Serializable]
    public struct SliderBinding
    {
        public Slider slider;
        public TMP_Text valueText;
        public string format; 
    }

    [Header("Panel Visibility")]
    public GameObject panelRoot;
    public Button togglePanelButton;
    public TMP_Text togglePanelButtonText;

    [Header("Kuramoto Sliders")]
    public SliderBinding couplingSlider;
    public SliderBinding visionRadiusSlider;
    public SliderBinding baseFrequencySlider;
    public SliderBinding frequencySpreadSlider;

    [Header("Population & Movement Sliders")]
    public SliderBinding populationSlider;
    public SliderBinding boundsRadiusSlider;
    public SliderBinding moveSpeedSlider;
    public SliderBinding wanderStrengthSlider;
    public SliderBinding simSpeedSlider;

    [Header("Action Buttons")]
    public Button pauseButton;
    public TMP_Text pauseButtonText;
    public Button randomizePhasesButton;
    public Button resetDefaultsButton;

    [Header("Live Telemetry")]
    public TMP_Text orderParameterText;
    public Slider orderParameterBar; // Pasek postêpu 

    private float telemetryTimer;

    private void Start()
    {
        var mgr = SimulationManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("SimulationUI: Brak SimulationManager na scenie!");
            enabled = false;
            return;
        }

        // Konfiguracja suwaków na podstawie limitów z SimulationManager
        BindSlider(couplingSlider, SimulationManager.MinCoupling, SimulationManager.MaxCoupling, false, mgr.SetCouplingStrength);
        BindSlider(visionRadiusSlider, SimulationManager.MinVisionRadius, SimulationManager.MaxVisionRadius, false, mgr.SetVisionRadius);
        BindSlider(baseFrequencySlider, SimulationManager.MinBaseFrequency, SimulationManager.MaxBaseFrequency, false, mgr.SetBaseFrequency);
        BindSlider(frequencySpreadSlider, SimulationManager.MinFrequencySpread, SimulationManager.MaxFrequencySpread, false, mgr.SetFrequencySpread);

        BindSlider(populationSlider, SimulationManager.MinPopulation, SimulationManager.MaxPopulation, true, mgr.SetPopulationSize);
        BindSlider(boundsRadiusSlider, SimulationManager.MinBoundsRadius, SimulationManager.MaxBoundsRadius, false, mgr.SetBoundsRadius);
        BindSlider(moveSpeedSlider, SimulationManager.MinMoveSpeed, SimulationManager.MaxMoveSpeed, false, mgr.SetMoveSpeed);
        BindSlider(wanderStrengthSlider, SimulationManager.MinWanderStrength, SimulationManager.MaxWanderStrength, false, mgr.SetWanderStrength);
        BindSlider(simSpeedSlider, SimulationManager.MinSimSpeed, SimulationManager.MaxSimSpeed, false, mgr.SetSimulationSpeed);

        // Przyciski
        if (pauseButton != null) pauseButton.onClick.AddListener(mgr.TogglePause);
        if (randomizePhasesButton != null) randomizePhasesButton.onClick.AddListener(mgr.RandomizeAllPhases);
        if (resetDefaultsButton != null) resetDefaultsButton.onClick.AddListener(mgr.ResetToDefaults);
        if (togglePanelButton != null) togglePanelButton.onClick.AddListener(TogglePanelVisibility);

        if (orderParameterBar != null)
        {
            orderParameterBar.minValue = 0f;
            orderParameterBar.maxValue = 1f;
            orderParameterBar.interactable = false;
        }

        mgr.OnParametersChanged += RefreshAllControls;
        RefreshAllControls();
    }

    private void OnDestroy()
    {
        if (SimulationManager.Instance != null)
        {
            SimulationManager.Instance.OnParametersChanged -= RefreshAllControls;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            TogglePanelVisibility();
        }

        // Odœwie¿anie telemetrii 10x na sekundê, ¿eby nie obci¹¿aæ UI TextMeshPro
        telemetryTimer += Time.unscaledDeltaTime;
        if (telemetryTimer >= 0.1f && SimulationManager.Instance != null)
        {
            telemetryTimer = 0f;
            float r = SimulationManager.Instance.OrderParameter;

            if (orderParameterText != null)
            {
                orderParameterText.text = $"Synchronizacja (r): {r:F2}";
            }

            if (orderParameterBar != null)
            {
                orderParameterBar.value = r;
            }
        }
    }

    private void BindSlider(SliderBinding binding, float min, float max, bool wholeNumbers, Action<float> setter)
    {
        if (binding.slider == null) return;

        binding.slider.minValue = min;
        binding.slider.maxValue = max;
        binding.slider.wholeNumbers = wholeNumbers;

        string fmt = string.IsNullOrEmpty(binding.format) ? (wholeNumbers ? "F0" : "F2") : binding.format;

        binding.slider.onValueChanged.AddListener(val =>
        {
            setter(val);
            if (binding.valueText != null)
            {
                binding.valueText.text = val.ToString(fmt);
            }
        });
    }

    private void RefreshAllControls()
    {
        var mgr = SimulationManager.Instance;
        if (mgr == null) return;

        SetSliderVisual(couplingSlider, mgr.couplingStrength, false);
        SetSliderVisual(visionRadiusSlider, mgr.visionRadius, false);
        SetSliderVisual(baseFrequencySlider, mgr.baseFrequency, false);
        SetSliderVisual(frequencySpreadSlider, mgr.frequencySpread, false);

        SetSliderVisual(populationSlider, mgr.populationSize, true);
        SetSliderVisual(boundsRadiusSlider, mgr.boundsRadius, false);
        SetSliderVisual(moveSpeedSlider, mgr.moveSpeed, false);
        SetSliderVisual(wanderStrengthSlider, mgr.wanderStrength, false);
        SetSliderVisual(simSpeedSlider, mgr.simulationSpeed, false);

        if (pauseButtonText != null)
        {
            pauseButtonText.text = mgr.isPaused ? "Wznów" : "Pauza";
        }
    }

    private void SetSliderVisual(SliderBinding binding, float value, bool wholeNumbers)
    {
        if (binding.slider == null) return;

        binding.slider.SetValueWithoutNotify(value);

        if (binding.valueText != null)
        {
            string fmt = string.IsNullOrEmpty(binding.format) ? (wholeNumbers ? "F0" : "F2") : binding.format;
            binding.valueText.text = value.ToString(fmt);
        }
    }

    public void TogglePanelVisibility()
    {
        if (panelRoot == null) return;

        bool nextState = !panelRoot.activeSelf;
        panelRoot.SetActive(nextState);

        if (togglePanelButtonText != null)
        {
            togglePanelButtonText.text = nextState ? "Ukryj UI [Tab]" : "Poka¿ UI [Tab]";
        }
    }
}