using UnityEngine;
using UnityEngine.UI;
using System.Collections;

using KiloWorld.Rendering.Systems;

/// <summary>
/// Displays current city/town/village name in top-left UI.
/// City name is updated from ping response (server-side reverse geocoding).
/// Shows profile default location in editor or before first ping.
/// </summary>
public class LocationLabelUI : MonoBehaviour
{
    [Header("UI")]
    public Text locationText;

    void Start()
    {
        StartCoroutine(InitNextFrame());
    }

    IEnumerator InitNextFrame()
    {
        yield return null;

        // Show profile default location until first ping returns a city
        var renderManager = FindFirstObjectByType<RenderManager>();
        if (renderManager != null && renderManager.profile != null && locationText != null)
        {
            string defaultName = renderManager.profile.startupLocation.GetStartupLocationDisplayName();
            locationText.text = defaultName;
            Debug.Log($"[LocationLabelUI] Default location: {defaultName}");
        }
    }

    public string GetCityName()
    {
        return locationText != null ? locationText.text : "";
    }
}
