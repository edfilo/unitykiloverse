using UnityEngine;

public class SimpleTestRotator : MonoBehaviour
{
    [SerializeField] private float m_rotationSpeed = 30f;
    
    private void Start()
    {
        Debug.Log("[SimpleTestRotator] Scene started successfully!");
        Debug.Log($"[SimpleTestRotator] Platform: {Application.platform}");
        Debug.Log($"[SimpleTestRotator] Device: {SystemInfo.deviceModel}");
    }
    
    private void Update()
    {
        transform.Rotate(Vector3.up, m_rotationSpeed * Time.deltaTime);
    }
}