using UnityEngine;

/// <summary>
/// Simple enemy behaviour that relentlessly chases the snail.
/// The chaser attempts to maintain a configurable offset behind the snail
/// while matching its vertical position for a more dynamic pursuit.
/// </summary>
public class EnemyChaser : MonoBehaviour
{
    [SerializeField] private SnailController targetSnail;
    [Min(0f)]
    [SerializeField] private float acceleration = 3f;
    [Min(0f)]
    [SerializeField] private float maxSpeed = 6f;
    [Tooltip("Desired distance to keep from the snail on the X axis (positive keeps the enemy behind).")]
    [SerializeField] private float desiredOffset = 3f;
    [Tooltip("How aggressively the enemy corrects the offset error.")]
    [Min(0f)]
    [SerializeField] private float catchUpStrength = 1.5f;
    [Tooltip("How quickly the enemy aligns vertically with the snail.")]
    [Min(0f)]
    [SerializeField] private float verticalFollowSpeed = 4f;

    private float currentSpeed;

    private void Awake()
    {
        if (targetSnail == null)
        {
            targetSnail = FindObjectOfType<SnailController>();
        }
    }

    private void Update()
    {
        if (targetSnail == null)
        {
            return;
        }

        FollowSnail(Time.deltaTime);
    }

    private void FollowSnail(float deltaTime)
    {
        Vector3 snailPosition = targetSnail.transform.position;
        Vector3 position = transform.position;

        float desiredX = snailPosition.x - desiredOffset;
        float distanceError = desiredX - position.x;

        float targetSpeed = Mathf.Clamp(distanceError * catchUpStrength, -maxSpeed, maxSpeed);
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * deltaTime);
        position.x += currentSpeed * deltaTime;

        float targetY = snailPosition.y;
        position.y = Mathf.MoveTowards(position.y, targetY, verticalFollowSpeed * deltaTime);

        transform.position = position;
    }

    private void OnDrawGizmosSelected()
    {
        if (targetSnail == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Vector3 snailPosition = targetSnail.transform.position;
        Vector3 desiredPosition = new Vector3(snailPosition.x - desiredOffset, snailPosition.y, snailPosition.z);
        Gizmos.DrawLine(transform.position, desiredPosition);
        Gizmos.DrawSphere(desiredPosition, 0.1f);
    }
}
