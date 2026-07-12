using UnityEngine;

public class CameraFollowNode : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -9f);
    [SerializeField] private float smoothTime = 0.35f;

    private Vector3 target;
    private Vector3 velocity;
    private bool hasTarget;

    public void SetTarget(Vector3 worldPosition)
    {
        target = worldPosition;
        if (!hasTarget)
        {
            hasTarget = true;
            transform.position = target + offset;
            transform.rotation = Quaternion.LookRotation(-offset);
        }
    }

    private void LateUpdate()
    {
        if (!hasTarget) return;
        transform.position = Vector3.SmoothDamp(transform.position, target + offset, ref velocity, smoothTime);
    }
}
