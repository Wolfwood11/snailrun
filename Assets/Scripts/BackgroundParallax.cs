using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves background layers at different speeds to create a parallax illusion based on
/// the current snail speed.
/// </summary>
public class BackgroundParallax : MonoBehaviour
{
    [Serializable]
    private class ParallaxLayer
    {
        public Transform transform;
        [Range(0f, 2f)] public float speedMultiplier = 0.5f;
        public bool lockYAxis = true;
        [Tooltip("Optional offset applied to the initial position so layers do not stack on top of each other.")]
        public Vector3 startOffset = Vector3.zero;

        private Vector3 initialPosition;

        public void CacheInitialPosition()
        {
            if (transform != null)
            {
                initialPosition = transform.position + startOffset;
            }
        }

        public void Apply(float baseSpeed, float deltaTime)
        {
            if (transform == null)
            {
                return;
            }

            float displacement = baseSpeed * speedMultiplier * deltaTime;
            Vector3 position = transform.position;
            position.x += displacement;

            if (lockYAxis)
            {
                position.y = initialPosition.y;
            }

            transform.position = position;
        }
    }

    [SerializeField] private SnailController targetSnail;
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    private void Awake()
    {
        foreach (ParallaxLayer layer in layers)
        {
            layer?.CacheInitialPosition();
        }

        if (targetSnail == null)
        {
            targetSnail = FindObjectOfType<SnailController>();
        }
    }

    private void LateUpdate()
    {
        if (targetSnail == null)
        {
            return;
        }

        float speed = targetSnail.CurrentSpeed;
        float delta = Time.deltaTime;

        foreach (ParallaxLayer layer in layers)
        {
            layer?.Apply(speed, delta);
        }
    }
}
