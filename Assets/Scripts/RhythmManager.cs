using System;
using UnityEngine;

/// <summary>
/// Evaluates the player's tap rhythm and exposes information about tempo consistency,
/// combos and speed bonuses for the snail controller.
/// </summary>
public class RhythmManager : MonoBehaviour
{
    [Serializable]
    public struct RhythmState
    {
        public static RhythmState Default => new RhythmState
        {
            combo = 1,
            interval = -1f,
            accuracy = 0f,
            onBeat = false,
            inSmoothRun = false,
            speedMultiplier = 1f
        };

        public int combo;
        public float interval;
        public float accuracy;
        public bool onBeat;
        public bool inSmoothRun;
        public float speedMultiplier;
    }

    [Header("Tempo Settings")]
    [Tooltip("Target time between taps that keeps the snail at its ideal rhythm.")]
    [SerializeField] private float targetInterval = 0.5f;
    [Tooltip("Allowed percentage (0-1) deviation from the target interval to count as on beat.")]
    [SerializeField, Range(0.05f, 0.3f)] private float allowedDeviation = 0.12f;
    [SerializeField] private int smoothRunComboRequirement = 3;

    [Header("Speed Modifiers")]
    [SerializeField] private float onBeatMultiplier = 1.15f;
    [SerializeField] private float offBeatMultiplier = 0.6f;
    [SerializeField] private float smoothRunBonusMultiplier = 0.35f;

    private float lastTapTime = -1f;
    private int combo;
    private bool inSmoothRun;
    private RhythmState latestState = RhythmState.Default;

    /// <summary>
    /// Raised whenever a tap is evaluated so other systems (UI, VFX) can react.
    /// </summary>
    public event Action<RhythmState> RhythmUpdated;

    /// <summary>
    /// Register a new player tap, updating combo, smooth run state and returning the
    /// resulting rhythm metrics.
    /// </summary>
    public RhythmState RegisterTap(float tapTime)
    {
        float interval = lastTapTime > 0f ? tapTime - lastTapTime : -1f;
        bool onBeat = false;
        float accuracy = 0f;

        if (interval > 0f)
        {
            float deviation = Mathf.Abs(interval - targetInterval);
            float tolerance = targetInterval * allowedDeviation;
            onBeat = deviation <= tolerance;
            accuracy = Mathf.Clamp01(1f - deviation / Mathf.Max(0.0001f, tolerance));
        }
        else
        {
            onBeat = true; // Treat first tap as on beat so the snail can start moving.
            accuracy = 1f;
        }

        if (onBeat)
        {
            combo = Mathf.Min(combo + 1, 999);
        }
        else
        {
            combo = 1;
        }

        bool previousSmoothRun = inSmoothRun;
        inSmoothRun = combo >= smoothRunComboRequirement && onBeat;

        float multiplier = onBeat ? onBeatMultiplier : offBeatMultiplier;
        if (inSmoothRun)
        {
            multiplier += smoothRunBonusMultiplier;
        }

        lastTapTime = tapTime;

        latestState = new RhythmState
        {
            combo = combo,
            interval = interval,
            accuracy = accuracy,
            onBeat = onBeat,
            inSmoothRun = inSmoothRun,
            speedMultiplier = multiplier
        };

        RhythmUpdated?.Invoke(latestState);

        if (!inSmoothRun && previousSmoothRun)
        {
            combo = 1; // reset combo when leaving smooth run to avoid sticky state
        }

        return latestState;
    }

    /// <summary>
    /// Returns a value between 0 and 1 describing how well the player maintained the rhythm recently.
    /// Higher values mean the snail should keep more of its momentum.
    /// </summary>
    public float GetRetentionFactor(float currentTime)
    {
        if (lastTapTime < 0f)
        {
            return 0f;
        }

        float timeSinceTap = currentTime - lastTapTime;
        if (timeSinceTap <= targetInterval * 1.1f)
        {
            return Mathf.Clamp01(latestState.onBeat ? 1f : 0.5f);
        }

        float falloffStart = targetInterval * 1.1f;
        float falloffEnd = targetInterval * 3f;
        float t = Mathf.InverseLerp(falloffEnd, falloffStart, timeSinceTap);
        return Mathf.Clamp01(t);
    }

    /// <summary>
    /// Returns the most recent rhythm state information.
    /// </summary>
    public RhythmState CurrentState => latestState;

    /// <summary>
    /// Target interval in seconds that defines the perfect rhythm window.
    /// </summary>
    public float TargetInterval => targetInterval;

    /// <summary>
    /// Resets the rhythm tracking and combo data.
    /// </summary>
    public void ResetRhythm()
    {
        combo = 0;
        inSmoothRun = false;
        latestState = RhythmState.Default;
        lastTapTime = -1f;
    }
}
