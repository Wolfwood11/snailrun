using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the playable snail character by reacting to tap input, driving movement,
/// animation frames and visual feedback like the slime trail or camera shake.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SnailController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float tapAcceleration = 1.65f;
    [SerializeField] private float smoothRunBonus = 0.75f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float baseDecay = 1.4f;
    [SerializeField] private float smoothDecay = 0.6f;

    [Header("Energy")]
    [SerializeField, Min(0f)] private float maxEnergy = 100f;
    [SerializeField, Min(0f)] private float baseTapEnergyCost = 5f;
    [SerializeField, Range(0f, 100f), Tooltip("Percentage (0-100) window considered a slight speed up when tapping faster than the rhythm.")]
    private float slightFastThresholdPercent = 10f;
    [SerializeField, Range(0f, 100f), Tooltip("Maximum percentage (0-100) speed increase granted when tapping slightly faster than the rhythm.")]
    private float slightFastMaxSpeedIncreasePercent = 1f;
    [SerializeField, Range(0f, 100f), Tooltip("Maximum percentage (0-100) extra energy consumed when tapping slightly faster than the rhythm.")]
    private float slightFastMaxEnergyIncreasePercent = 5f;
    [SerializeField, Range(0f, 100f), Tooltip("Percentage (0-100) speed increase when tapping much faster than the rhythm.")]
    private float veryFastSpeedIncreasePercent = 5f;
    [SerializeField, Range(0f, 100f), Tooltip("Percentage (0-100) extra energy consumed when tapping much faster than the rhythm.")]
    private float veryFastEnergyIncreasePercent = 10f;
    [SerializeField, Range(0f, 100f), Tooltip("Percentage (0-100) interval deviation that grants the full slower tap energy reduction.")]
    private float slowEnergyReductionThresholdPercent = 10f;
    [SerializeField, Range(0f, 100f), Tooltip("Maximum percentage (0-100) reduction in energy consumption when tapping slower than the rhythm.")]
    private float slowMaxEnergyReductionPercent = 5f;
    [SerializeField, Min(0f), Tooltip("Percentage of current speed removed per percentage the tap lags behind the rhythm.")]
    private float slowSpeedDropPerPercent = 0.5f;

    [Header("Animation")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private List<Sprite> moveSprites = new List<Sprite>();
    [SerializeField] private float moveFrameRate = 16f;

    [Header("Food Consumption")]
    [SerializeField] private float minimumFoodSpeedBonus = 0.5f;

    [Header("References")]
    [SerializeField] private RhythmManager rhythmManager;
    [SerializeField] private TrailRenderer slimeTrail;
    [SerializeField] private ParticleSystem smoothRunParticles;
    [SerializeField] private SimpleCameraShake cameraShake;
    [SerializeField] private bool translateTransform = false;

    private SpriteRenderer spriteRenderer;
    private float currentSpeed;
    private float lastTapTime = -10f;
    private int currentFrameIndex;
    private float frameTimer;
    private bool playingMoveAnimation;
    private RhythmManager.RhythmState lastRhythmState;
    private Vector3 initialPosition;
    private float currentEnergy;

    /// <summary>
    /// Current planar speed of the snail.
    /// </summary>
    public float CurrentSpeed => currentSpeed;

    /// <summary>
    /// Speed value normalised to the configured maximum speed.
    /// </summary>
    public float NormalisedSpeed => maxSpeed <= 0.001f ? 0f : Mathf.Clamp01(currentSpeed / maxSpeed);

    /// <summary>
    /// Current energy remaining for the snail.
    /// </summary>
    public float CurrentEnergy => currentEnergy;

    /// <summary>
    /// Energy normalised to the configured maximum.
    /// </summary>
    public float NormalisedEnergy => maxEnergy <= 0.001f ? 0f : Mathf.Clamp01(currentEnergy / maxEnergy);

    /// <summary>
    /// Accumulated distance the snail has conceptually travelled while the world scrolls.
    /// </summary>
    public float TravelledDistance { get; private set; }

    /// <summary>
    /// Event raised whenever the snail consumes a <see cref="FoodItem"/>.
    /// </summary>
    public event Action<FoodItem> FoodConsumed;

    /// <summary>
    /// Event raised whenever the energy value changes. Provides the current and maximum energy.
    /// </summary>
    public event Action<float, float> EnergyChanged;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialPosition = transform.position;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (!UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }
#endif

        if (rhythmManager == null)
        {
            rhythmManager = FindObjectOfType<RhythmManager>();
        }

        if (spriteRenderer != null && idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }

        if (slimeTrail != null)
        {
            slimeTrail.emitting = false;
        }

        if (smoothRunParticles != null)
        {
            smoothRunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        currentEnergy = Mathf.Max(0f, maxEnergy);
        EnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    private void OnEnable()
    {
        if (rhythmManager != null)
        {
            rhythmManager.RhythmUpdated += HandleRhythmUpdated;
        }
    }

    private void OnDisable()
    {
        if (rhythmManager != null)
        {
            rhythmManager.RhythmUpdated -= HandleRhythmUpdated;
        }
    }

    private void Update()
    {
        PollInput();
        ApplyPassiveDecay();
        UpdateAnimation(Time.deltaTime);
        UpdateMovement();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TryConsumeFood(collision.GetComponent<FoodItem>());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        TryConsumeFood(other.GetComponent<FoodItem>());
    }

    private void PollInput()
    {
        bool tapDetected = false;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        tapDetected = PollInputSystemTap();
#else
        tapDetected = PollLegacyInputTap();
#endif

        if (tapDetected)
        {
            RegisterTap();
        }
    }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    private bool PollInputSystemTap()
    {
        bool tapDetected = false;

#if ENABLE_INPUT_SYSTEM
        if (!tapDetected && UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
        {
            foreach (var touch in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapDetected = true;
                    break;
                }
            }
        }

        if (!tapDetected && UnityEngine.InputSystem.Touchscreen.current != null)
        {
            foreach (var touch in UnityEngine.InputSystem.Touchscreen.current.touches)
            {
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapDetected = true;
                    break;
                }
            }
        }

        if (!tapDetected && UnityEngine.InputSystem.Mouse.current != null)
        {
            tapDetected = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        }

        if (!tapDetected && UnityEngine.InputSystem.Keyboard.current != null)
        {
            tapDetected = UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#endif

        return tapDetected;
    }
#endif

#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
    private bool PollLegacyInputTap()
    {
        bool tapDetected = false;

        if (Input.touchSupported)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    tapDetected = true;
                    break;
                }
            }
        }

        if (!tapDetected)
        {
            tapDetected = Input.GetMouseButtonDown(0);
        }

        return tapDetected;
    }
#endif

    private void RegisterTap()
    {
        lastTapTime = Time.time;
        RhythmManager.RhythmState rhythmState = rhythmManager != null
            ? rhythmManager.RegisterTap(Time.time)
            : RhythmManager.RhythmState.Default;

        float energyCost = Mathf.Max(0f, baseTapEnergyCost);
        float speedMultiplier = 1f;
        float penaltyMultiplier = 1f;

        if (rhythmState.inSmoothRun)
        {
            PlaySmoothRunFeedback();
        }
        else
        {
            StopSmoothRunFeedback();
        }

        if (rhythmManager != null)
        {
            float targetInterval = rhythmManager.TargetInterval;
            float interval = rhythmState.interval > 0f ? rhythmState.interval : targetInterval;

            if (targetInterval > 0.0001f && interval > 0f)
            {
                float deviationPercent = ((interval - targetInterval) / targetInterval) * 100f;

                if (!rhythmState.onBeat)
                {
                    if (deviationPercent < 0f)
                    {
                        float fastMagnitude = Mathf.Abs(deviationPercent);
                        if (fastMagnitude <= slightFastThresholdPercent)
                        {
                            float t = slightFastThresholdPercent > 0.0001f
                                ? Mathf.Clamp01(fastMagnitude / slightFastThresholdPercent)
                                : 1f;
                            float speedIncrease = Mathf.Lerp(0f, Mathf.Max(0f, slightFastMaxSpeedIncreasePercent), t) / 100f;
                            float energyIncrease = Mathf.Lerp(0f, Mathf.Max(0f, slightFastMaxEnergyIncreasePercent), t) / 100f;
                            speedMultiplier += speedIncrease;
                            energyCost *= 1f + energyIncrease;
                        }
                        else
                        {
                            speedMultiplier += Mathf.Max(0f, veryFastSpeedIncreasePercent) / 100f;
                            energyCost *= 1f + Mathf.Max(0f, veryFastEnergyIncreasePercent) / 100f;
                        }
                    }
                    else if (deviationPercent > 0f)
                    {
                        float slowMagnitude = deviationPercent;
                        float energyReductionThreshold = Mathf.Max(0.0001f, slowEnergyReductionThresholdPercent);
                        float energyReductionFactor = Mathf.Clamp01(slowMagnitude / energyReductionThreshold);
                        float energyReduction = Mathf.Clamp(slowMaxEnergyReductionPercent, 0f, 100f) / 100f;
                        energyCost *= 1f - energyReduction * energyReductionFactor;

                        float speedDropPercent = slowMagnitude * Mathf.Max(0f, slowSpeedDropPerPercent);
                        float dropMultiplier = Mathf.Clamp01(speedDropPercent / 100f);
                        penaltyMultiplier = Mathf.Clamp01(1f - dropMultiplier);
                    }
                }
            }
        }

        float acceleration = tapAcceleration;
        if (rhythmState.inSmoothRun)
        {
            acceleration += smoothRunBonus;
        }

        currentSpeed = Mathf.Min(maxSpeed, currentSpeed + acceleration);
        currentSpeed = Mathf.Min(maxSpeed, currentSpeed * Mathf.Max(0f, speedMultiplier));
        currentSpeed *= penaltyMultiplier;

        lastRhythmState = rhythmState;

        SpendEnergy(Mathf.Max(0f, energyCost));
    }

    private void ApplyPassiveDecay()
    {
        if (currentSpeed <= 0f)
        {
            return;
        }

        float decay = baseDecay;
        float timeSinceTap = Time.time - lastTapTime;

        if (rhythmManager != null)
        {
            float retention = rhythmManager.GetRetentionFactor(Time.time);
            decay = Mathf.Lerp(baseDecay, smoothDecay, retention);
        }
        else if (timeSinceTap <= 0.6f)
        {
            decay = Mathf.Lerp(baseDecay, smoothDecay, 0.5f);
        }

        currentSpeed = Mathf.Max(0f, currentSpeed - decay * Time.deltaTime);

        if (currentSpeed <= 0.01f)
        {
            currentSpeed = 0f;
            playingMoveAnimation = false;
            StopSmoothRunFeedback();
        }
    }

    private void UpdateMovement()
    {
        if (currentSpeed <= 0f)
        {
            if (!translateTransform && transform.position != initialPosition)
            {
                transform.position = initialPosition;
            }

            return;
        }

        float displacement = currentSpeed * Time.deltaTime;
        TravelledDistance += displacement;

        if (translateTransform)
        {
            transform.Translate(Vector3.right * displacement);
        }
        else if (transform.position != initialPosition)
        {
            transform.position = initialPosition;
        }
    }

    private void TryConsumeFood(FoodItem food)
    {
        if (food == null)
        {
            return;
        }

        float speedBonus = Mathf.Max(minimumFoodSpeedBonus, food.SpeedBonus);
        currentSpeed = Mathf.Min(maxSpeed, currentSpeed + speedBonus);
        if (currentSpeed > 0.01f)
        {
            PlaySmoothRunFeedback();
        }

        FoodConsumed?.Invoke(food);
        food.Consume();
    }

    private void UpdateAnimation(float deltaTime)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        bool wasPlaying = playingMoveAnimation;
        bool hasMoveFrames = moveSprites.Count > 0;
        bool isMoving = hasMoveFrames && currentSpeed > 0.01f;

        playingMoveAnimation = isMoving;

        if (!isMoving)
        {
            if (spriteRenderer.sprite != idleSprite)
            {
                spriteRenderer.sprite = idleSprite;
            }

            frameTimer = 0f;
            currentFrameIndex = 0;

            if (slimeTrail != null && slimeTrail.emitting)
            {
                slimeTrail.emitting = false;
            }

            return;
        }

        if (!wasPlaying)
        {
            currentFrameIndex = 0;
            frameTimer = 0f;
            spriteRenderer.sprite = moveSprites[currentFrameIndex];
        }

        float frameDuration = 1f / Mathf.Max(1f, moveFrameRate);
        frameTimer += deltaTime;
        if (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrameIndex = (currentFrameIndex + 1) % moveSprites.Count;
            spriteRenderer.sprite = moveSprites[currentFrameIndex];
        }

        if (slimeTrail != null && !slimeTrail.emitting)
        {
            slimeTrail.emitting = true;
        }
    }

    private void SpendEnergy(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float previousEnergy = currentEnergy;
        currentEnergy = Mathf.Max(0f, currentEnergy - amount);

        if (!Mathf.Approximately(previousEnergy, currentEnergy))
        {
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }
    }

    /// <summary>
    /// Restores energy to the snail, clamped to the configured maximum.
    /// </summary>
    public void RestoreEnergy(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        float previousEnergy = currentEnergy;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);

        if (!Mathf.Approximately(previousEnergy, currentEnergy))
        {
            EnergyChanged?.Invoke(currentEnergy, maxEnergy);
        }
    }

    private void PlaySmoothRunFeedback()
    {
        if (slimeTrail != null)
        {
            slimeTrail.emitting = true;
        }

        if (smoothRunParticles != null && !smoothRunParticles.isEmitting)
        {
            smoothRunParticles.Play();
        }

        if (cameraShake != null)
        {
            cameraShake.Play(1f);
        }
    }

    private void StopSmoothRunFeedback()
    {
        if (smoothRunParticles != null && smoothRunParticles.isEmitting)
        {
            smoothRunParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void HandleRhythmUpdated(RhythmManager.RhythmState state)
    {
        lastRhythmState = state;

        if (!state.inSmoothRun && state.combo <= 1)
        {
            StopSmoothRunFeedback();
            if (slimeTrail != null)
            {
                slimeTrail.emitting = currentSpeed > 0.1f;
            }
        }
    }
}
