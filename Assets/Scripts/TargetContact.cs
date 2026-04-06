using UnityEngine;

[DisallowMultipleComponent]

public class TargetContact : MonoBehaviour
{
    [Header("Detect Targets")]
    public bool isTouchingTarget;
    const string _targetTag = "Target";

    private void OnCollisionEnter(Collision other)
    {
        if (other.transform.CompareTag(_targetTag))
        {
            isTouchingTarget = true;
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (other.transform.CompareTag(_targetTag))
        {
            isTouchingTarget = false;
        }
    }
}
