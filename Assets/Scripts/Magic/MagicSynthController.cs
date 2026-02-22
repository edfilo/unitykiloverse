using UnityEngine;

namespace Kiloverse.Magic
{
    /// <summary>
    /// Lightweight wavetable synth that reacts to pottery gestures.
    /// </summary>
    public class MagicSynthController : MonoBehaviour
    {
        [SerializeField] private float minFrequency = 110f;
        [SerializeField] private float maxFrequency = 520f;
        [SerializeField] private float gain = 0.22f;
        [SerializeField] private float frequencyLerp = 0.01f;
        [SerializeField] private float attackTime = 0.8f;
        [SerializeField] private float releaseTime = 2.5f;
        [SerializeField] private float vibratoRate = 4f;
        [SerializeField] private float vibratoDepth = 0.4f;

        private float sampleRate;
        private float phase;
        private float vibratoPhase;
        private float currentFrequency;
        private float targetFrequency;
        private float envelope;
        private bool isActive;
        private float morphAmount;
        private const int TableSize = 512;
        private readonly float[][] tables = new float[4][];

        private void OnEnable()
        {
            sampleRate = AudioSettings.outputSampleRate;
            BuildTables();
            targetFrequency = minFrequency;
            currentFrequency = minFrequency;
        }

        private void OnDisable()
        {
            isActive = false;
            envelope = 0f;
        }

        public void BeginStroke(float normalizedRadius)
        {
            isActive = true;
            UpdateStroke(normalizedRadius);
        }

        public void UpdateStroke(float normalizedRadius)
        {
            normalizedRadius = Mathf.Clamp01(normalizedRadius);
            targetFrequency = Mathf.Lerp(minFrequency, maxFrequency, normalizedRadius);
            morphAmount = normalizedRadius;
        }

        public void EndStroke()
        {
            isActive = false;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            // sampleRate is initialized in OnEnable; no need to query AudioSettings here.

            var deltaFreq = Mathf.Clamp01(frequencyLerp);
            var attackDelta = attackTime <= 0f ? 1f : 1f / (attackTime * sampleRate);
            var releaseDelta = releaseTime <= 0f ? 1f : 1f / (releaseTime * sampleRate);

            for (var i = 0; i < data.Length; i += channels)
            {
                currentFrequency = Mathf.Lerp(currentFrequency, targetFrequency, deltaFreq);
                var vibrato = Mathf.Sin(vibratoPhase) * vibratoDepth;
                vibratoPhase += (vibratoRate / sampleRate) * Mathf.PI * 2f;
                if (vibratoPhase > Mathf.PI * 2f)
                {
                    vibratoPhase -= Mathf.PI * 2f;
                }

                var effectiveFrequency = Mathf.Max(20f, currentFrequency + vibrato * 10f);
                phase += effectiveFrequency / sampleRate;
                phase -= Mathf.Floor(phase);

                var sample = SampleTables(phase, morphAmount);

                // Target amplitude is 1 while active, 0 when released
                var targetAmp = isActive ? 1f : 0f;
                var delta = isActive ? attackDelta : releaseDelta;
                
                // Smoothly move envelope towards target
                envelope = Mathf.MoveTowards(envelope, targetAmp, delta);
                
                var value = sample * envelope * gain;

                for (var c = 0; c < channels; c++)
                {
                    data[i + c] += value;
                }
            }
        }

        private float SampleTables(float normalizedPhase, float morph)
        {
            var scaled = morph * (tables.Length - 1);
            var indexA = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, tables.Length - 1);
            var indexB = Mathf.Clamp(indexA + 1, 0, tables.Length - 1);
            var t = scaled - indexA;
            var sampleA = SampleTable(tables[indexA], normalizedPhase);
            var sampleB = SampleTable(tables[indexB], normalizedPhase);
            return Mathf.Lerp(sampleA, sampleB, t);
        }

        private static float SampleTable(float[] table, float normalizedPhase)
        {
            if (table == null || table.Length == 0)
            {
                return 0f;
            }

            var position = normalizedPhase * table.Length;
            var index = Mathf.FloorToInt(position);
            if (index >= table.Length)
            {
                index -= table.Length;
            }
            var frac = position - index;
            var next = (index + 1) % table.Length;
            return Mathf.Lerp(table[index], table[next], frac);
        }

        private void BuildTables()
        {
            for (var i = 0; i < tables.Length; i++)
            {
                tables[i] ??= new float[TableSize];
            }

            for (var n = 0; n < TableSize; n++)
            {
                var p = (float)n / TableSize;
                var angle = p * Mathf.PI * 2f;
                tables[0][n] = Mathf.Sin(angle); // sine
                tables[1][n] = Mathf.Sin(angle) + 0.5f * Mathf.Sin(angle * 2f); // soft organ
                tables[2][n] = Mathf.Abs(2f * (p - Mathf.Floor(p + 0.5f))) * 2f - 1f; // triangle (softer than saw)
                tables[3][n] = Mathf.Sin(angle) * Mathf.Exp(-2f * p); // plucked sine
            }
        }
    }
}
