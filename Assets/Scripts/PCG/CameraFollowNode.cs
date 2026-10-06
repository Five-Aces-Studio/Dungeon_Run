using UnityEngine;

public class CameraFollowNode : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
    [SerializeField] private float smoothTime = 0.35f;

    private Vector3 target;
    private Vector3 velocity;
    private Transform trackedTarget;
    private bool hasTarget;

    public void SetTarget(Vector3 worldPosition)
    {
        trackedTarget = null;
        SetPositionTarget(worldPosition);
    }

    public void SetTarget(Transform targetTransform)
    {
        trackedTarget = targetTransform;

        if (trackedTarget != null)
            SetPositionTarget(trackedTarget.position);
    }

    public void ClearTarget()
    {
        trackedTarget = null;
        hasTarget = false;
        velocity = Vector3.zero;
    }

    private void SetPositionTarget(Vector3 worldPosition)
    {
        target = worldPosition;

        if (hasTarget)
            return;

        hasTarget = true;
        velocity = Vector3.zero;

        transform.position = target + offset;
        transform.rotation = Quaternion.LookRotation(-offset);
    }

    private void LateUpdate()
    {
        if (!hasTarget)
            return;

        if (trackedTarget != null)
            target = trackedTarget.position;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            target + offset,
            ref velocity,
            smoothTime);
    }
}