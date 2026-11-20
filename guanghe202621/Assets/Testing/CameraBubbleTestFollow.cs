using UnityEngine;

/// <summary>仅用于测试相机跟随的接管与恢复，需要填入演出管理器的 Follow Behaviours。</summary>
public sealed class CameraBubbleTestFollow : MonoBehaviour, ICameraFollowPositionProvider
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
    [SerializeField, Min(0f)] private float smoothSpeed = 7f;

    private void LateUpdate()
    {
        if (target == null) return;
        float blend = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset, blend);
    }

    public void SnapToTarget()
    {
        if (target != null) transform.position = target.position + offset;
    }

    public bool TryGetFollowPosition(out Vector3 position)
    {
        position = target != null ? target.position + offset : transform.position;
        return target != null;
    }
}
