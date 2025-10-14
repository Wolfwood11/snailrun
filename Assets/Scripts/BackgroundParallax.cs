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
        [Tooltip("Wrap the layer around to create an endless scrolling illusion.")]
        public bool enableLooping = false;
        [Tooltip("Optional manual width used when looping. Leave at 0 to auto detect.")]
        public float loopLengthOverride = 0f;
        [Tooltip("Automatically compute the loop length from the renderer bounds when possible.")]
        public bool autoLoopLength = true;

        private Vector3 initialPosition;
        private float cachedLoopLength;

        public void CacheInitialPosition()
        {
            if (transform != null)
            {
                initialPosition = transform.position + startOffset;
                transform.position = initialPosition;

                if (enableLooping)
                {
                    cachedLoopLength = loopLengthOverride;
                    if (autoLoopLength)
                    {
                        float detected = GetRendererWidth(transform);
                        if (detected > 0.001f)
                        {
                            cachedLoopLength = detected;
                        }
                    }

                    if (cachedLoopLength <= 0.001f)
                    {
                        cachedLoopLength = 1f;
                    }
                }
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
            position.x -= displacement;

            if (lockYAxis)
            {
                position.y = initialPosition.y;
            }

            if (enableLooping && cachedLoopLength > 0.001f)
            {
                float minX = initialPosition.x - cachedLoopLength;
                while (position.x < minX)
                {
                    position.x += cachedLoopLength;
                }
            }

            transform.position = position;
        }

        private float GetRendererWidth(Transform target)
        {
            if (target == null)
            {
                return 0f;
            }

            float width = 0f;

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                width = Mathf.Max(width, renderer.bounds.size.x);
            }

            SpriteRenderer spriteRenderer = target.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                width = Mathf.Max(width, spriteRenderer.bounds.size.x);
            }

            return width;
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
