using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Periodically spawns <see cref="FoodItem"/> instances ahead of the snail.
/// Manages active items to avoid overcrowding the scene and to recycle when
/// the snail consumes them.
/// </summary>
public class FoodSpawner : MonoBehaviour
{
    [SerializeField] private SnailController targetSnail;
    [Tooltip("Prefabs that will be spawned. A random one is chosen each time.")]
    [SerializeField] private List<FoodItem> foodPrefabs = new List<FoodItem>();
    [Tooltip("Minimum and maximum time in seconds between spawns.")]
    [SerializeField] private Vector2 spawnInterval = new Vector2(3f, 6f);
    [Tooltip("Horizontal distance range in front of the snail where food can appear.")]
    [SerializeField] private Vector2 forwardSpawnRange = new Vector2(6f, 12f);
    [Tooltip("Vertical offset range relative to the snail's position.")]
    [SerializeField] private Vector2 heightRange = new Vector2(-1.5f, 1.5f);
    [Tooltip("Maximum number of food items that can exist simultaneously.")]
    [SerializeField] private int maxActiveItems = 6;
    [Tooltip("Optional parent transform used for spawned items. Defaults to this transform.")]
    [SerializeField] private Transform spawnParent;

    private readonly List<FoodItem> activeItems = new List<FoodItem>();
    private float spawnTimer;

    private void Awake()
    {
        if (targetSnail == null)
        {
            targetSnail = FindObjectOfType<SnailController>();
        }

        if (spawnParent == null)
        {
            spawnParent = transform;
        }
    }

    private void OnEnable()
    {
        ResetTimer();
    }

    private void OnDisable()
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            FoodItem item = activeItems[i];
            if (item != null)
            {
                item.Consumed -= HandleFoodConsumed;
            }
        }

        activeItems.Clear();
    }

    private void Update()
    {
        CleanupActiveItems();

        if (targetSnail == null || foodPrefabs.Count == 0)
        {
            return;
        }

        if (activeItems.Count >= Mathf.Max(0, maxActiveItems))
        {
            return;
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnFood();
            ResetTimer();
        }
    }

    private void SpawnFood()
    {
        FoodItem prefab = foodPrefabs[Random.Range(0, foodPrefabs.Count)];
        if (prefab == null)
        {
            return;
        }

        float minForward = Mathf.Min(forwardSpawnRange.x, forwardSpawnRange.y);
        float maxForward = Mathf.Max(forwardSpawnRange.x, forwardSpawnRange.y);
        minForward = Mathf.Max(0.1f, minForward);
        maxForward = Mathf.Max(minForward, maxForward);
        float offsetX = Random.Range(minForward, maxForward);
        float minHeight = Mathf.Min(heightRange.x, heightRange.y);
        float maxHeight = Mathf.Max(heightRange.x, heightRange.y);
        float offsetY = Random.Range(minHeight, maxHeight);

        Vector3 spawnPosition = targetSnail.transform.position;
        spawnPosition.x += offsetX;
        spawnPosition.y += offsetY;

        FoodItem instance = Instantiate(prefab, spawnPosition, Quaternion.identity, spawnParent);
        instance.Consumed += HandleFoodConsumed;
        activeItems.Add(instance);
    }

    private void HandleFoodConsumed(FoodItem item)
    {
        if (item == null)
        {
            return;
        }

        item.Consumed -= HandleFoodConsumed;
        activeItems.Remove(item);
    }

    private void CleanupActiveItems()
    {
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            FoodItem item = activeItems[i];
            if (item == null)
            {
                activeItems.RemoveAt(i);
                continue;
            }

            if (!item.gameObject.activeInHierarchy)
            {
                item.Consumed -= HandleFoodConsumed;
                activeItems.RemoveAt(i);
            }
        }
    }

    private void ResetTimer()
    {
        float min = Mathf.Max(0.1f, spawnInterval.x);
        float max = Mathf.Max(min, spawnInterval.y);
        spawnTimer = Random.Range(min, max);
    }
}
