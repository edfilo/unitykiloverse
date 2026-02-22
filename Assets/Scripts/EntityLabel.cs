// EntityLabel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component on the UI prefab that represents a floating label.
/// It holds references to the UI elements that will be updated at runtime.
/// </summary>
public class EntityLabel : MonoBehaviour
{
    [Header("UI Elements")] 
    public TextMeshProUGUI nameText;   // Player / object name
    public Image healthBar;            // Image with Fill Method = Horizontal
    public Image icon;                 // Optional icon (power‑up, transmitter, etc.)

    /// <summary>Set the displayed name.</summary>
    public void SetName(string txt)
    {
        if (nameText != null) nameText.text = txt;
    }

    /// <summary>Set health as a normalized value (0‑1).</summary>
    public void SetHealth(float normalized)
    {
        if (healthBar != null) healthBar.fillAmount = Mathf.Clamp01(normalized);
    }

    /// <summary>Set the icon sprite; hide the Image if null.</summary>
    public void SetIcon(Sprite s)
    {
        if (icon != null)
        {
            icon.sprite = s;
            icon.enabled = s != null;
        }
    }
}
