using System;
using UnityEngine;

/// <summary>
/// Represents a consumable item that the snail can eat.
/// Provides configuration for the consumption reward and plays
/// optional feedback when eaten.
/// </summary>
public class FoodItem : MonoBehaviour
{
    public enum FoodType
    {
        Berry,
        Fruit
    }

    [Header("Food")]
    [SerializeField] private FoodType type = FoodType.Berry;
    [Tooltip("Speed bonus granted to the snail when this item is consumed.")]
    [Min(0f)]
    [SerializeField] private float speedBonus = 1f;

    [Header("Feedback")]
    [SerializeField] private ParticleSystem consumeEffect;
    [SerializeField] private AudioClip consumeSound;
    [SerializeField] private bool destroyOnConsume = true;

    /// <summary>
    /// Raised when the item is consumed by the snail.
    /// </summary>
    public event Action<FoodItem> Consumed;

    /// <summary>
    /// Gets the configured food type.
    /// </summary>
    public FoodType Type => type;

    /// <summary>
    /// Gets the speed bonus provided by this item.
    /// </summary>
    public float SpeedBonus => speedBonus;

    /// <summary>
    /// Consumes the food item, raising the consumption event and triggering
    /// any configured feedback.
    /// </summary>
    public void Consume()
    {
        Consumed?.Invoke(this);

        PlayFeedback();

        if (destroyOnConsume)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void PlayFeedback()
    {
        if (consumeEffect != null)
        {
            ParticleSystem instance = Instantiate(consumeEffect, transform.position, Quaternion.identity);
            var mainModule = instance.main;
            float lifetime = mainModule.duration + mainModule.startLifetime.constantMax;
            Destroy(instance.gameObject, Mathf.Max(0.1f, lifetime));
        }

        if (consumeSound != null)
        {
            AudioSource.PlayClipAtPoint(consumeSound, transform.position);
        }
    }
}
