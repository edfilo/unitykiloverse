using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Drives a Material instance of Custom/TransmissionFX, mirroring the per-cut
// chaos that transmit_v2_composite.py used to bake server-side: beat-synced
// cut boundaries, random crop/zoom ROIs, speed ramps, treatment cycling,
// occasional flash inserts. Re-rolls a schedule on every Play() so each
// transmission view looks fresh.
public class TransmissionFXScheduler : MonoBehaviour
{
    [System.Serializable]
    private struct Cut
    {
        public float t;            // start time in playback seconds
        public float speed;        // playback speed multiplier
        public Vector4 crop;       // xywh in UV
        public bool hflip;
        public bool vflip;
        public Treatment fx;
        public bool flashIn;       // 1-frame flash at the boundary
        public Color flashColor;
    }

    [System.Serializable]
    private struct Treatment
    {
        public Vector4 chromaShift; // R.xy B.xy in px
        public float invert;
        public float saturation;
        public float contrast;
        public float brightness;
        public float posterize;
        public float blur;
        public float noise;
        public Vector3 wave;        // amp, period, speed
        public float vignette;
    }

    public Material fxMaterial { get; private set; }
    public VideoPlayer videoPlayer;
    public RawImage videoImage;
    public float bpm = 72f;
    public Vector2 sourceSize = new Vector2(576f, 1024f);
    public Vector2 faceCenter = new Vector2(0.40f, 0.34f); // normalized

    private List<Cut> schedule;
    private int cutIdx;
    private float flashUntil;
    private Color flashColor;

    void Awake()
    {
        if (Shader.Find("Custom/TransmissionFX") is Shader s)
        {
            fxMaterial = new Material(s);
            fxMaterial.SetVector("_SrcSize", new Vector4(sourceSize.x, sourceSize.y, 0, 0));
            ApplyTreatment(NeutralTreatment(), new Vector4(0, 0, 1, 1), false, false);
        }
        else
        {
            Debug.LogWarning("[TransmissionFXScheduler] Shader Custom/TransmissionFX not found — FX disabled");
        }
    }

    public void Bind(RawImage image, VideoPlayer player)
    {
        videoImage = image;
        videoPlayer = player;
        if (videoImage != null && fxMaterial != null) videoImage.material = fxMaterial;
    }

    public void RollSchedule(float clipLengthSeconds)
    {
        schedule = new List<Cut>();
        if (fxMaterial == null) return;

        float beat = 60f / Mathf.Max(bpm, 30f);
        // weighted cut durations roughly matching the python comp
        float[] choices = { 0.25f, 0.4f, 0.5f, 0.7f, 1.0f, 1.5f, 2.0f };
        float[] weights = { 1f, 2f, 3f, 3f, 2f, 1f, 0.5f };

        float t = 0f;
        int safety = 64;
        while (t < clipLengthSeconds && safety-- > 0)
        {
            float dur = beat * WeightedPick(choices, weights);
            schedule.Add(MakeCut(t));
            t += dur;
        }
        cutIdx = -1;
    }

    Cut MakeCut(float t)
    {
        // Crop ROI: 60% chance face zoom, 30% random crop, 10% full frame
        Vector4 crop;
        float r = Random.value;
        if (r < 0.6f)
        {
            float zoom = Random.Range(0.35f, 0.75f);
            crop = ClampCrop(faceCenter.x - zoom * 0.5f, faceCenter.y - zoom * 0.5f, zoom, zoom);
        }
        else if (r < 0.9f)
        {
            float zoom = Random.Range(0.5f, 0.85f);
            crop = ClampCrop(Random.Range(0f, 1f - zoom), Random.Range(0f, 1f - zoom), zoom, zoom);
        }
        else
        {
            crop = new Vector4(0, 0, 1, 1);
        }

        float speed = WeightedPick(
            new float[] { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 2.0f, 2.5f },
            new float[] { 0.5f, 1f, 3f, 1f, 1f, 0.5f, 0.3f });

        return new Cut
        {
            t = t,
            speed = speed,
            crop = crop,
            hflip = Random.value < 0.1f,
            vflip = Random.value < 0.04f,
            fx = RandomTreatment(),
            flashIn = Random.value < 0.18f,
            flashColor = Random.value < 0.7f ? Color.white : Color.black,
        };
    }

    Treatment RandomTreatment()
    {
        // Pick one of ~9 looks per the python composite
        int p = Random.Range(0, 9);
        Treatment fx = NeutralTreatment();
        switch (p)
        {
            case 0: // clean
                break;
            case 1: // chroma shift horizontal
                fx.chromaShift = new Vector4(6, 0, -6, 0);
                break;
            case 2: // chroma shift diagonal extreme
                fx.chromaShift = new Vector4(14, 4, -14, -4);
                fx.noise = 0.08f;
                break;
            case 3: // negate
                fx.invert = 1f;
                break;
            case 4: // desat + contrast
                fx.saturation = 0f; fx.contrast = 1.7f; fx.brightness = -0.05f;
                break;
            case 5: // posterize
                fx.posterize = 5f; fx.contrast = 1.2f;
                break;
            case 6: // blur
                fx.blur = 2f; fx.contrast = 1.3f;
                break;
            case 7: // noise heavy
                fx.noise = 0.18f; fx.contrast = 1.3f;
                break;
            case 8: // wavy displacement (response grade vibe)
                fx.wave = new Vector3(4f, 55f, 2.5f);
                fx.noise = 0.10f;
                fx.chromaShift = new Vector4(5, 1, -5, -1);
                break;
        }
        // Light vignette on most cuts
        if (Random.value < 0.6f) fx.vignette = Random.Range(0.15f, 0.45f);
        return fx;
    }

    Treatment NeutralTreatment()
    {
        return new Treatment
        {
            chromaShift = Vector4.zero,
            invert = 0f,
            saturation = 1f,
            contrast = 1f,
            brightness = 0f,
            posterize = 0f,
            blur = 0f,
            noise = 0f,
            wave = Vector3.zero,
            vignette = 0f,
        };
    }

    Vector4 ClampCrop(float x, float y, float w, float h)
    {
        x = Mathf.Clamp(x, 0f, 1f - w);
        y = Mathf.Clamp(y, 0f, 1f - h);
        return new Vector4(x, y, w, h);
    }

    float WeightedPick(float[] values, float[] weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += weights[i];
        float pick = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            acc += weights[i];
            if (pick <= acc) return values[i];
        }
        return values[values.Length - 1];
    }

    void Update()
    {
        if (fxMaterial == null || schedule == null || schedule.Count == 0 || videoPlayer == null) return;
        if (!videoPlayer.isPlaying) return;

        float t = (float)videoPlayer.time;
        // Advance to the current cut
        int newIdx = cutIdx;
        for (int i = newIdx + 1; i < schedule.Count; i++)
        {
            if (schedule[i].t <= t) newIdx = i;
            else break;
        }

        if (newIdx != cutIdx && newIdx >= 0)
        {
            cutIdx = newIdx;
            var c = schedule[cutIdx];
            videoPlayer.playbackSpeed = c.speed;
            ApplyTreatment(c.fx, c.crop, c.hflip, c.vflip);
            if (c.flashIn)
            {
                flashColor = c.flashColor;
                flashUntil = Time.time + 0.08f;
            }
        }

        // Flash overlay decays
        float flashAmt = Mathf.Max(0f, (flashUntil - Time.time) / 0.08f);
        fxMaterial.SetColor("_Flash", new Color(flashColor.r, flashColor.g, flashColor.b, flashAmt));
    }

    void ApplyTreatment(Treatment fx, Vector4 crop, bool hflip, bool vflip)
    {
        if (fxMaterial == null) return;
        fxMaterial.SetVector("_CropRect", crop);
        fxMaterial.SetVector("_Flip", new Vector4(hflip ? 1f : 0f, vflip ? 1f : 0f, 0, 0));
        fxMaterial.SetVector("_ChromaShift", fx.chromaShift);
        fxMaterial.SetFloat("_Invert", fx.invert);
        fxMaterial.SetFloat("_Saturation", fx.saturation);
        fxMaterial.SetFloat("_Contrast", fx.contrast);
        fxMaterial.SetFloat("_Brightness", fx.brightness);
        fxMaterial.SetFloat("_Posterize", fx.posterize);
        fxMaterial.SetFloat("_Blur", fx.blur);
        fxMaterial.SetFloat("_NoiseAmount", fx.noise);
        fxMaterial.SetVector("_Wave", new Vector4(fx.wave.x, fx.wave.y, fx.wave.z, 0));
        fxMaterial.SetFloat("_Vignette", fx.vignette);
        fxMaterial.SetColor("_Flash", new Color(0, 0, 0, 0));
        
        bool fizzyEnabled = PlayerPrefs.GetInt("k1lo_transmissionFizzyEdges", 1) == 1;
        fxMaterial.SetFloat("_FizzyEdges", fizzyEnabled ? 1f : 0f);
    }

    public void Stop()
    {
        if (fxMaterial != null) ApplyTreatment(NeutralTreatment(), new Vector4(0, 0, 1, 1), false, false);
        if (videoPlayer != null) videoPlayer.playbackSpeed = 1f;
        schedule = null;
        cutIdx = -1;
    }
}
