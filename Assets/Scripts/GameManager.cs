using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

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

    [Header("Tap Feedback")]
    [SerializeField] private CanvasGroup tapFlash;
    [SerializeField] private float tapFlashDuration = 0.15f;

    private float tapFlashTimer;

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
    }

    private void OnEnable()
    {
        if (rhythmManager != null)
        {
            rhythmManager.RhythmUpdated += OnRhythmUpdated;
        }
    }

    private void OnDisable()
    {
        if (rhythmManager != null)
        {
            rhythmManager.RhythmUpdated -= OnRhythmUpdated;
        }
    }

    private void Update()
    {
        UpdateTapFlash();
        UpdateSpeedSlider();
    }

    private void UpdateSpeedSlider()
    {
        if (speedSlider == null || snailController == null)
        {
            return;
        }

        speedSlider.value = snailController.NormalisedSpeed;
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
        if (uiCanvas != null && comboText != null && tempoText != null && speedSlider != null)
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

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemGo;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
        eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        eventSystemGo.transform.SetParent(transform);
    }
}
