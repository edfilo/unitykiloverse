using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Big tappable card above the dock showing current transmitter state:
//   Idle        → "CREATE TRANSMISSION"
//   Building    → "TRANSMISSION BUILDING" + status detail
//   Broadcasting → "NOW BROADCASTING"
// Tap always opens the transmitter panel.
public sealed class K1L0TransmitStatusCard : MonoBehaviour
{
    public static K1L0TransmitStatusCard Instance { get; private set; }

    private Image cardBg;
    private TextMeshProUGUI mainLabel;
    private TextMeshProUGUI subLabel;

    private enum CardState { Idle, Building, Broadcasting }
    private CardState _state = CardState.Idle;
    private string _buildStatus = "";
    private float _broadcastUntil = -1f;
    private bool _subscribed;

    private static readonly Color ColIdle          = new Color(0.04f, 0.04f, 0.06f, 0.94f);
    private static readonly Color ColBuilding      = new Color(0.03f, 0.05f, 0.14f, 0.96f);
    private static readonly Color ColBroadcasting  = new Color(0.02f, 0.10f, 0.06f, 0.96f);

    private static readonly Color TintIdle         = new Color(0.45f, 0.50f, 0.60f, 1f);
    private static readonly Color TintBuilding     = new Color(0.45f, 0.60f, 1.00f, 1f);
    private static readonly Color TintBroadcasting = new Color(0.35f, 0.90f, 0.55f, 1f);

    public static K1L0TransmitStatusCard Create(RectTransform parent, TMP_FontAsset font, Action onTap)
    {
        var go = new GameObject("TransmitStatusCard", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var card = go.AddComponent<K1L0TransmitStatusCard>();
        card.Build(font, onTap);
        return card;
    }

    private void Build(TMP_FontAsset font, Action onTap)
    {
        Instance = this;

        var rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(340f, 84f);
        rt.anchoredPosition = new Vector2(0f, 86f);

        cardBg = gameObject.AddComponent<Image>();
        cardBg.sprite = K1L0GlassFactory.ControlRectSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = ColIdle;
        cardBg.raycastTarget = true;

        var btn = gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = cardBg;
        btn.onClick.AddListener(() => onTap?.Invoke());

        mainLabel = MakeLabel("MainLabel", font, 19f, FontStyles.Bold, new Vector2(0f, 0.52f), new Vector2(1f, 1f), Color.white);
        subLabel  = MakeLabel("SubLabel",  font, 13f, FontStyles.Normal, new Vector2(0f, 0f),    new Vector2(1f, 0.48f), TintIdle);

        SubscribeToEvents();
        Refresh();
    }

    private TextMeshProUGUI MakeLabel(string name, TMP_FontAsset font, float size, FontStyles style, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(16f, 2f);
        rt.offsetMax = new Vector2(-16f, -2f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (_subscribed) return;
        var tm = TransmissionManager.Instance;
        if (tm == null) return;
        _subscribed = true;
        tm.OnTransmitV2Progress += HandleV2Progress;
        tm.OnTransmissionReady  += HandleReady;
    }

    private void UnsubscribeFromEvents()
    {
        if (!_subscribed) return;
        _subscribed = false;
        var tm = TransmissionManager.Instance;
        if (tm == null) return;
        tm.OnTransmitV2Progress -= HandleV2Progress;
        tm.OnTransmissionReady  -= HandleReady;
    }

    private void Update()
    {
        if (!_subscribed) SubscribeToEvents();

        if (_state == CardState.Broadcasting && Time.unscaledTime > _broadcastUntil)
        {
            _state = CardState.Idle;
            Refresh();
        }
    }

    private void HandleV2Progress(string jobId, string status)
    {
        if (status == "ready")
        {
            _state = CardState.Broadcasting;
            _broadcastUntil = Time.unscaledTime + 300f;
            _buildStatus = "";
        }
        else if (status == "error")
        {
            _state = CardState.Idle;
            _buildStatus = "";
        }
        else
        {
            _state = CardState.Building;
            _buildStatus = StatusToLabel(status);
        }
        Refresh();
    }

    private void HandleReady(TransmissionData td)
    {
        if (td == null || td.transmissionType != "transmitter") return;
        _state = CardState.Broadcasting;
        _broadcastUntil = Time.unscaledTime + 300f;
        _buildStatus = "";
        Refresh();
    }

    private void Refresh()
    {
        if (cardBg == null) return;
        switch (_state)
        {
            case CardState.Idle:
                cardBg.color   = ColIdle;
                mainLabel.text = "CREATE TRANSMISSION";
                subLabel.text  = "tap to open transmitter";
                subLabel.color = TintIdle;
                break;
            case CardState.Building:
                cardBg.color   = ColBuilding;
                mainLabel.text = "TRANSMISSION BUILDING";
                subLabel.text  = string.IsNullOrEmpty(_buildStatus) ? "starting..." : _buildStatus;
                subLabel.color = TintBuilding;
                break;
            case CardState.Broadcasting:
                cardBg.color   = ColBroadcasting;
                mainLabel.text = "NOW BROADCASTING";
                subLabel.text  = "tap to open transmitter";
                subLabel.color = TintBroadcasting;
                break;
        }
    }

    private static string StatusToLabel(string status)
    {
        switch (status)
        {
            case "gathering":   return "scanning location...";
            case "planning":    return "transmitting...";
            case "planned":     return "generating image...";
            case "image_ready": return "composing video...";
            case "composing":   return "composing video...";
            default:            return status + "...";
        }
    }
}
