using System.Collections;
using UnityEngine;

/// <summary>
/// Lightweight camera shake component that offsets the local position for a short burst.
/// </summary>
public class SimpleCameraShake : MonoBehaviour
{
    [SerializeField] private float defaultDuration = 0.2f;
    [SerializeField] private float defaultIntensity = 0.15f;

    private Vector3 initialLocalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
    }

    private void OnDisable()
    {
        StopShake();
        transform.localPosition = initialLocalPosition;
    }

    /// <summary>
    /// Starts a shake using the default duration and intensity multiplied by the provided factor.
    /// </summary>
    public void Play(float intensityMultiplier)
    {
        StopShake();
        float intensity = defaultIntensity * Mathf.Max(0.1f, intensityMultiplier);
        shakeRoutine = StartCoroutine(ShakeRoutine(defaultDuration, intensity));
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            Vector3 offset = Random.insideUnitSphere * intensity;
            offset.z = 0f;
            transform.localPosition = initialLocalPosition + offset;
            yield return null;
        }

        transform.localPosition = initialLocalPosition;
        shakeRoutine = null;
    }

    private void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
            transform.localPosition = initialLocalPosition;
        }
    }
}
