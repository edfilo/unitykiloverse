using UnityEngine;

public class iOSResolution : MonoBehaviour
{
    void Start()
    {
        // Set to iPhone 14/15 resolution (portrait)
        Screen.SetResolution(390, 844, false);
    }
}