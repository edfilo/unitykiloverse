using UnityEngine;

public class iOSResolution : MonoBehaviour
{
    void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // Set to iPhone 14/15 resolution (portrait)
        Screen.SetResolution(390, 844, false);
#endif
    }
}