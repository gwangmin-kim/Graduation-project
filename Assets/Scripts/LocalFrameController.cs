using UnityEngine;

public class LocalFrameController : MonoBehaviour
{
    public void UpdateOrientation(Transform root, Vector3 forward)
    {
        forward.y = 0f;
        forward = forward.normalized;

        var lookRotation = forward == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(forward);
        transform.SetPositionAndRotation(root.position, lookRotation);
    }

    public Vector3 GetLocalDirection(Vector3 globalDirection) => transform.InverseTransformDirection(globalDirection);
    public Vector3 GetLocalPosition(Vector3 globalPosition) => transform.InverseTransformPoint(globalPosition);
    public Quaternion GetLocalRotation(Quaternion globalRotation) => globalRotation * Quaternion.Inverse(transform.rotation);
}
