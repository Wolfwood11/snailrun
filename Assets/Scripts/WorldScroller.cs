using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scrolls a collection of world objects in the opposite direction of the snail movement.
/// Objects that move past the left boundary are recycled to the right, providing an
/// endless loop illusion without incurring instantiation costs.
/// </summary>
public class WorldScroller : MonoBehaviour
{
    [Serializable]
    private class ScrollElement
    {
        [Tooltip("Transform representing a piece of the scrolling world (ground, props, etc.).")]
        public Transform transform;

        [Tooltip("Optional manual length override used when no renderer is present.")]
        public float lengthOverride = 0f;

        [Tooltip("Automatically determine the length from a Renderer or SpriteRenderer.")]
        public bool autoCalculateLength = true;

        private float cachedLength;
        private bool initialised;

        public void Initialise()
        {
            if (transform == null)
            {
                cachedLength = 0f;
                initialised = true;
                return;
            }

            cachedLength = lengthOverride;

            if (autoCalculateLength)
            {
                float detectedLength = TryGetRendererLength(transform);
                if (detectedLength > 0.001f)
                {
                    cachedLength = detectedLength;
                }
            }

            if (cachedLength <= 0.001f)
            {
                cachedLength = 1f;
            }

            initialised = true;
        }

        private float TryGetRendererLength(Transform target)
        {
            if (target == null)
            {
                return 0f;
            }

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds.size.x;
            }

            SpriteRenderer spriteRenderer = target.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                return spriteRenderer.bounds.size.x;
            }

            return 0f;
        }

        public float Length
        {
            get
            {
                if (!initialised)
                {
                    Initialise();
                }

                return cachedLength;
            }
        }

        public float HalfLength => Length * 0.5f;
    }

    [SerializeField] private SnailController targetSnail;
    [SerializeField] private Transform anchor;

    [Tooltip("Approximate width of the visible play space. Determines when elements recycle.")]
    [Min(1f)]
    [SerializeField] private float visibleWidth = 30f;

    [Tooltip("Extra padding added to the recycle bounds to avoid popping on screen edges.")]
    [SerializeField] private float recyclePadding = 5f;

    [SerializeField] private List<ScrollElement> scrollElements = new List<ScrollElement>();

    private readonly List<ScrollElement> activeElements = new List<ScrollElement>();

    private void Awake()
    {
        if (targetSnail == null)
        {
            targetSnail = FindObjectOfType<SnailController>();
        }

        if (anchor == null && targetSnail != null)
        {
            anchor = targetSnail.transform;
        }

        activeElements.Clear();
        foreach (ScrollElement element in scrollElements)
        {
            if (element == null)
            {
                continue;
            }

            element.Initialise();
            if (element.transform != null)
            {
                activeElements.Add(element);
            }
        }

        activeElements.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        EnsureElementsWithinBounds();
    }

    private void LateUpdate()
    {
        if (targetSnail == null || activeElements.Count == 0)
        {
            return;
        }

        float speed = targetSnail.CurrentSpeed;
        if (speed <= 0f)
        {
            return;
        }

        float displacement = speed * Time.deltaTime;
        float leftBoundary = GetLeftBoundary();
        float rightEdge = GetRightmostEdge();

        for (int i = 0; i < activeElements.Count; i++)
        {
            ScrollElement element = activeElements[i];
            Transform elementTransform = element.transform;
            if (elementTransform == null)
            {
                continue;
            }

            Vector3 position = elementTransform.position;
            position.x -= displacement;

            if (position.x + element.HalfLength < leftBoundary)
            {
                float targetPositionX = rightEdge + element.HalfLength;
                position.x = targetPositionX;
                rightEdge = targetPositionX + element.HalfLength;
            }

            elementTransform.position = position;
        }
    }

    private void EnsureElementsWithinBounds()
    {
        if (activeElements.Count == 0)
        {
            return;
        }

        float leftBoundary = GetLeftBoundary();
        float rightEdge = GetRightmostEdge();

        foreach (ScrollElement element in activeElements)
        {
            Transform elementTransform = element.transform;
            if (elementTransform == null)
            {
                continue;
            }

            Vector3 position = elementTransform.position;
            if (position.x + element.HalfLength < leftBoundary)
            {
                float targetPositionX = rightEdge + element.HalfLength;
                position.x = targetPositionX;
                rightEdge = targetPositionX + element.HalfLength;
                elementTransform.position = position;
            }
        }
    }

    private float GetAnchorX()
    {
        return anchor != null ? anchor.position.x : 0f;
    }

    private float GetLeftBoundary()
    {
        return GetAnchorX() - (visibleWidth * 0.5f) - recyclePadding;
    }

    private float GetRightmostEdge()
    {
        float rightEdge = GetAnchorX() + (visibleWidth * 0.5f) + recyclePadding;

        foreach (ScrollElement element in activeElements)
        {
            Transform elementTransform = element.transform;
            if (elementTransform == null)
            {
                continue;
            }

            float edge = elementTransform.position.x + element.HalfLength;
            if (edge > rightEdge)
            {
                rightEdge = edge;
            }
        }

        return rightEdge;
    }
}
