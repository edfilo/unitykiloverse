using UnityEngine;

public class HelmetFollower : MonoBehaviour
{
    public Transform target;
    public Vector3 offset;
    public Quaternion rotationOffset = Quaternion.identity;

    void LateUpdate()
    {
        if (target == null) return;

        // Copy position and rotation from the target (Head bone)
        // Apply offset rotated by target's rotation
        transform.position = target.position + (target.rotation * offset);
        transform.rotation = target.rotation * rotationOffset;

        // Force the target (original head) to stay hidden by scaling it down every frame.
        // This fights against the Animator resetting the scale.
        target.localScale = Vector3.one * 0.0001f;
    }
}
