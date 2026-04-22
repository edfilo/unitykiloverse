using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class UnicornPugsPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float lookSpeed = 120f;
    [SerializeField] private float gravity = -18f;
    [SerializeField] private Transform cameraPivot;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraPivot == null)
        {
            Camera cameraComponent = GetComponentInChildren<Camera>(true);
            if (cameraComponent != null)
            {
                cameraPivot = cameraComponent.transform;
            }
        }
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        transform.Rotate(Vector3.up, mouseX * lookSpeed * Time.deltaTime);

        if (cameraPivot != null)
        {
            pitch = Mathf.Clamp(pitch - mouseY * lookSpeed * 0.8f * Time.deltaTime, -25f, 35f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector3 move = new(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        move = Vector3.ClampMagnitude(move, 1f);
        move = transform.TransformDirection(move) * moveSpeed;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}
