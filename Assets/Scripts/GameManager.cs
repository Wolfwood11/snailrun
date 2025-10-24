using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// High level coordinator that wires the rhythm manager, snail controller and UI feedback together.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private SnailController snailController;
    [SerializeField] private RhythmManager rhythmManager;

    [Header("UI References")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Text comboText;
    [SerializeField] private Text tempoText;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private Text speedText;
    [SerializeField] private Text energyText;
    [SerializeField] private Text energyConsumptionText;

    [Header("Snail Stats")]
    [SerializeField, Min(0.1f)] private float energyConsumptionWindow = 3f;

    [Header("Tap Feedback")]
    [SerializeField] private CanvasGroup tapFlash;
    [SerializeField] private float tapFlashDuration = 0.15f;

    private float tapFlashTimer;
    private float currentEnergy;
    private float maxEnergy;
    private readonly List<EnergySpendSample> energySpendSamples = new List<EnergySpendSample>();

    private struct EnergySpendSample
    {
        public float time;
        public float amount;
    }

    private void Awake()
    {
        if (snailController == null)
        {
            snailController = FindObjectOfType<SnailController>();
        }

        if (rhythmManager == null)
        {
            rhythmManager = FindObjectOfType<RhythmManager>();
        }

        EnsureEventSystemExists();
        EnsureUiExists();
        UpdateStatLabels();
    }

    private void OnEnable()
    {
        if (rhythmManager != null)
        {
            rhythmManager.RhythmUpdated += OnRhythmUpdated;
        }

        if (snailController != null)
        {
            snailController.EnergyChanged += OnEnergyChanged;
            snailController.EnergySpent += OnEnergySpent;
            currentEnergy = snailController.CurrentEnergy;
            maxEnergy = snailController.MaxEnergy;
        }
    }

    private void OnDisable()
    {
        if (rhythmManager != null)
        {
            rhythmManager.RhythmUpdated -= OnRhythmUpdated;
        }

        if (snailController != null)
        {
            snailController.EnergyChanged -= OnEnergyChanged;
            snailController.EnergySpent -= OnEnergySpent;
        }

        energySpendSamples.Clear();
    }

    private void Update()
    {
        UpdateTapFlash();
        UpdateSpeedSlider();
        UpdateStatLabels();
    }

    private void UpdateSpeedSlider()
    {
        if (speedSlider == null || snailController == null)
        {
            return;
        }

        speedSlider.value = snailController.NormalisedSpeed;
    }

    private void UpdateStatLabels()
    {
        if (snailController == null)
        {
            return;
        }

        if (speedText != null)
        {
            speedText.text = $"Скорость: {snailController.CurrentSpeed:0.0} м/с";
        }

        if (energyText != null)
        {
            energyText.text = $"Энергия: {currentEnergy:0}/{maxEnergy:0}";
        }

        if (energyConsumptionText != null)
        {
            float window = Mathf.Max(0.1f, energyConsumptionWindow);
            float now = Time.time;
            float total = 0f;

            for (int i = energySpendSamples.Count - 1; i >= 0; i--)
            {
                if (now - energySpendSamples[i].time > window)
                {
                    energySpendSamples.RemoveAt(i);
                }
                else
                {
                    total += energySpendSamples[i].amount;
                }
            }

            float rate = total / window;
            energyConsumptionText.text = $"Расход: {rate:0.0} ед/с";
        }
    }

    private void UpdateTapFlash()
    {
        if (tapFlash == null)
        {
            return;
        }

        if (tapFlashTimer > 0f)
        {
            tapFlashTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(tapFlashTimer / tapFlashDuration);
            tapFlash.alpha = alpha;
        }
        else if (tapFlash.alpha > 0f)
        {
            tapFlash.alpha = 0f;
        }
    }

    private void OnRhythmUpdated(RhythmManager.RhythmState rhythmState)
    {
        if (comboText != null)
        {
            comboText.text = rhythmState.combo >= 3
                ? $"Combo x{rhythmState.combo}"
                : "Keep the tempo";
        }

        if (tempoText != null)
        {
            if (rhythmState.interval > 0f)
            {
                float bpm = 60f / Mathf.Max(0.0001f, rhythmState.interval);
                tempoText.text = rhythmState.onBeat
                    ? $"Perfect Tempo!\n{bpm:0} BPM"
                    : $"Off beat\n{bpm:0} BPM";
            }
            else
            {
                tempoText.text = "Tap to start";
            }
        }

        if (tapFlash != null)
        {
            tapFlash.alpha = 1f;
            tapFlashTimer = tapFlashDuration;
        }
    }

    private void EnsureUiExists()
    {
        if (uiCanvas != null && comboText != null && tempoText != null && speedSlider != null &&
            speedText != null && energyText != null && energyConsumptionText != null)
        {
            return;
        }

        if (uiCanvas == null)
        {
            GameObject canvasGo = new GameObject("UIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiCanvas = canvasGo.GetComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform canvasRect = uiCanvas.GetComponent<RectTransform>();

        if (comboText == null)
        {
            comboText = CreateTextElement("ComboText", canvasRect, new Vector2(0.5f, 0.9f));
        }

        if (tempoText == null)
        {
            tempoText = CreateTextElement("TempoText", canvasRect, new Vector2(0.5f, 0.82f));
        }

        if (speedSlider == null)
        {
            GameObject sliderGo = new GameObject("SpeedSlider", typeof(RectTransform), typeof(Slider), typeof(Image));
            sliderGo.transform.SetParent(canvasRect, false);
            RectTransform rect = sliderGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.14f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image background = sliderGo.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.4f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform fillRect = fillArea.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.05f, 0.25f);
            fillRect.anchorMax = new Vector2(0.95f, 0.75f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillTransform = fill.GetComponent<RectTransform>();
            fillTransform.anchorMin = Vector2.zero;
            fillTransform.anchorMax = Vector2.one;
            fillTransform.offsetMin = Vector2.zero;
            fillTransform.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.2f, 0.9f, 0.6f, 0.9f);

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.fillRect = fillTransform;
            slider.targetGraphic = fillImage;

            speedSlider = slider;
        }

        if (tapFlash == null)
        {
            GameObject tapFlashGo = new GameObject("TapFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            tapFlashGo.transform.SetParent(uiCanvas.transform, false);
            RectTransform rect = tapFlashGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = tapFlashGo.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.2f);

            tapFlash = tapFlashGo.GetComponent<CanvasGroup>();
            tapFlash.alpha = 0f;
        }

        if (speedText == null)
        {
            speedText = CreateTextElement("SpeedText", canvasRect, new Vector2(0f, 1f));
            RectTransform rect = speedText.rectTransform;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -40f);
            rect.sizeDelta = new Vector2(520f, 60f);
            speedText.alignment = TextAnchor.UpperLeft;
            speedText.fontSize = 36;
            speedText.text = "Скорость: 0.0 м/с";
        }

        if (energyText == null)
        {
            energyText = CreateTextElement("EnergyText", canvasRect, new Vector2(0f, 1f));
            RectTransform rect = energyText.rectTransform;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -100f);
            rect.sizeDelta = new Vector2(520f, 60f);
            energyText.alignment = TextAnchor.UpperLeft;
            energyText.fontSize = 36;
            energyText.text = "Энергия: 0/0";
        }

        if (energyConsumptionText == null)
        {
            energyConsumptionText = CreateTextElement("EnergyConsumptionText", canvasRect, new Vector2(0f, 1f));
            RectTransform rect = energyConsumptionText.rectTransform;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -160f);
            rect.sizeDelta = new Vector2(520f, 60f);
            energyConsumptionText.alignment = TextAnchor.UpperLeft;
            energyConsumptionText.fontSize = 36;
            energyConsumptionText.text = "Расход: 0.0 ед/с";
        }
    }

    private Text CreateTextElement(string name, RectTransform parent, Vector2 anchor)
    {
        GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(parent, false);
        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(600f, 120f);
        rect.anchoredPosition = Vector2.zero;

        Text text = textGo.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 48;
        text.color = Color.white;
        text.text = name;

        return text;
    }

    private void OnEnergyChanged(float current, float max)
    {
        currentEnergy = current;
        maxEnergy = max;
    }

    private void OnEnergySpent(float amount)
    {
        energySpendSamples.Add(new EnergySpendSample
        {
            time = Time.time,
            amount = amount
        });
    }

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemGo.transform.SetParent(transform);
    }
}
