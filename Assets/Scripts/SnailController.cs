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

    [Header("Animation")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private List<Sprite> moveSprites = new List<Sprite>();
    [SerializeField] private float moveFrameRate = 16f;

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

    /// <summary>
    /// Current planar speed of the snail.
    /// </summary>
    public float CurrentSpeed => currentSpeed;

    /// <summary>
    /// Speed value normalised to the configured maximum speed.
    /// </summary>
    public float NormalisedSpeed => maxSpeed <= 0.001f ? 0f : Mathf.Clamp01(currentSpeed / maxSpeed);

    /// <summary>
    /// Accumulated distance the snail has conceptually travelled while the world scrolls.
    /// </summary>
    public float TravelledDistance { get; private set; }

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

        float acceleration = tapAcceleration;
        if (rhythmState.onBeat)
        {
            acceleration *= 1.15f;
        }

        if (rhythmState.inSmoothRun)
        {
            acceleration += smoothRunBonus;
            PlaySmoothRunFeedback();
        }
        else
        {
            StopSmoothRunFeedback();
        }

        currentSpeed = Mathf.Min(maxSpeed, currentSpeed + acceleration);
        playingMoveAnimation = true;
        frameTimer = 0f;
        currentFrameIndex = 0;

        if (moveSprites.Count > 0)
        {
            spriteRenderer.sprite = moveSprites[0];
        }

        lastRhythmState = rhythmState;
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

    private void UpdateAnimation(float deltaTime)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (!playingMoveAnimation || moveSprites.Count == 0 || currentSpeed <= 0f)
        {
            spriteRenderer.sprite = idleSprite;
            playingMoveAnimation = false;
            return;
        }

        frameTimer += deltaTime;
        if (frameTimer >= 1f / Mathf.Max(1f, moveFrameRate))
        {
            frameTimer -= 1f / Mathf.Max(1f, moveFrameRate);
            currentFrameIndex = (currentFrameIndex + 1) % moveSprites.Count;
            spriteRenderer.sprite = moveSprites[currentFrameIndex];
        }

        if (slimeTrail != null)
        {
            slimeTrail.emitting = true;
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
