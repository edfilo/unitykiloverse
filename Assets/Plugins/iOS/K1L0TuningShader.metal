//
// K1L0TuningShader.metal
//
// Detuned-analog-TV layer effect for the incoming-transmission thumbnail.
// Driven by SwiftUI's .layerEffect (iOS 17+). One knob: progress 0→1.
// At 0 the signal is pure noise; at 1 the image is clean.
//

#include <metal_stdlib>
#include <SwiftUI/SwiftUI_Metal.h>
using namespace metal;

// ── Hash / noise helpers ────────────────────────────────────────────────

static inline float hash12(float2 p) {
    float3 p3 = fract(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

static inline float valueNoise(float2 p) {
    float2 i = floor(p);
    float2 f = fract(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = hash12(i);
    float b = hash12(i + float2(1.0, 0.0));
    float c = hash12(i + float2(0.0, 1.0));
    float d = hash12(i + float2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// fbm: two octaves is plenty at thumbnail scale
static inline float fbm(float2 p) {
    return 0.65 * valueNoise(p) + 0.35 * valueNoise(p * 2.7 + 13.1);
}

static inline float k1l0TearDepth(float p, float seed, float time, float maxDepth) {
    float shape = 0.5 + 0.5 * sin(p * 0.12 + seed + time * 1.6);
    float mid = 0.5 + 0.5 * sin(p * 0.39 - time * 3.1 + seed * 1.7);
    float jit = 0.5 + 0.5 * sin(p * 1.85 + time * 9.5 + seed * 2.3);
    float n = fbm(float2(p * 0.035 + seed, time * 2.0 + seed * 0.31));
    float rip = sin(p * 0.06 + time * 0.7 + seed) > 0.87 ? 1.85 : 1.0;
    return maxDepth * (shape * 0.48 + mid * 0.24 + jit * 0.13 + n * 0.15) * rip;
}

// ── Main layer effect ───────────────────────────────────────────────────
//
// position: pixel coords in the layer
// layer:    source layer sampler
// size:     layer size in pixels
// time:     seconds (drives all animation)
// progress: tuning progress 0..1

[[ stitchable ]] half4 k1l0TuningStatic(float2 position,
                                        SwiftUI::Layer layer,
                                        float2 size,
                                        float time,
                                        float progress) {
    float clamped = clamp(progress, 0.0, 1.0);
    float unresolved = 1.0 - clamped;
    float2 uv = position / max(size, float2(1.0));

    // 1. Pixelation: quantize sampling grid, coarse when detuned.
    //    3px-wide image at 0% → full res at 100%.
    float blocks = mix(6.0, size.x, smoothstep(0.0, 1.0, clamped * clamped));
    float2 quv = (floor(uv * blocks) + 0.5) / blocks;

    // 2. Horizontal sync tear: rows slip sideways. A slow rolling band plus
    //    per-row jitter, both fading with tuning.
    float band = fbm(float2(uv.y * 3.0 - time * 0.9, time * 0.35));
    float rowHash = hash12(float2(floor(uv.y * size.y * 0.5), floor(time * 24.0)));
    float tear = (band - 0.5) * 0.55 + (rowHash - 0.5) * 0.22;
    quv.x += tear * unresolved * unresolved;

    // 3. Vertical hold slip: occasional whole-frame roll when badly detuned.
    float rollGate = step(0.82, fbm(float2(time * 0.6, 7.7))) * unresolved;
    quv.y = fract(quv.y + rollGate * fract(time * 1.7) );

    // 4. Chromatic detune: sample channels at diverging offsets.
    float chroma = 0.020 * unresolved;
    float2 dir = float2(1.0, 0.0);
    half4 cr = layer.sample((quv + dir * chroma) * size);
    half4 cg = layer.sample(quv * size);
    half4 cb = layer.sample((quv - dir * chroma) * size);
    half3 rgb = half3(cr.r, cg.g, cb.b);

    // 5. Static: white noise snow, heavier + faster when detuned.
    float snowScale = mix(220.0, 90.0, unresolved);
    float snow = hash12(floor(uv * snowScale) + floor(time * (18.0 + 30.0 * unresolved)) * 17.31);
    float snowAmt = unresolved * (0.55 + 0.25 * band);
    rgb = mix(rgb, half3(snow), half(snowAmt * 0.75));

    // 6. Scanlines + slight green phosphor cast while tuning.
    float scan = 0.5 + 0.5 * sin(uv.y * size.y * 3.14159 * 0.9 + time * 9.0);
    rgb *= half(1.0 - 0.24 * scan * unresolved);
    rgb += half3(0.0, 0.05, 0.02) * half(unresolved * (0.4 + 0.6 * snow));

    // 7. Ghost image: faint time-shifted double exposure while detuned.
    half4 ghost = layer.sample(fract(quv + float2(0.035 * sin(time * 2.3), 0.0)) * size);
    rgb = mix(rgb, rgb * 0.7 + ghost.rgb * 0.5, half(0.35 * unresolved));

    // 8. Flicker: overall luminance instability.
    float flicker = 1.0 - 0.14 * unresolved * hash12(float2(floor(time * 13.0), 3.3));
    rgb *= half(flicker);

    return half4(rgb, cg.a);
}

[[ stitchable ]] half4 k1l0FizzyEdges(float2 position,
                                      SwiftUI::Layer layer,
                                      float2 size,
                                      float time,
                                      float enabled) {
    half4 color = layer.sample(position);
    if (enabled < 0.5) {
        return color;
    }

    float2 safeSize = max(size, float2(1.0));
    float minSide = max(1.0, min(safeSize.x, safeSize.y));
    float maxDepth = clamp(minSide * 0.065, 18.0, 42.0);
    float feather = clamp(minSide * 0.024, 7.0, 18.0);

    float top = k1l0TearDepth(position.x, 0.0, time, maxDepth);
    float bottom = k1l0TearDepth(position.x, 5.5, time, maxDepth);
    float left = k1l0TearDepth(position.y, 2.3, time, maxDepth);
    float right = k1l0TearDepth(position.y, 7.9, time, maxDepth);

    float topMask = smoothstep(0.0, feather, position.y - top);
    float bottomMask = smoothstep(0.0, feather, safeSize.y - position.y - bottom);
    float leftMask = smoothstep(0.0, feather, position.x - left);
    float rightMask = smoothstep(0.0, feather, safeSize.x - position.x - right);
    float edgeMask = min(min(topMask, bottomMask), min(leftMask, rightMask));

    float dither = hash12(floor(position * 0.75) + floor(time * 30.0) * 11.13);
    float dissolve = smoothstep(dither * 0.55, 1.0, edgeMask);
    color.a *= half(mix(0.10, 1.0, dissolve));

    float frontier = 0.0;
    frontier = max(frontier, 1.0 - smoothstep(0.0, feather * 0.95, abs(position.y - top)));
    frontier = max(frontier, 1.0 - smoothstep(0.0, feather * 0.95, abs(safeSize.y - position.y - bottom)));
    frontier = max(frontier, 1.0 - smoothstep(0.0, feather * 0.95, abs(position.x - left)));
    frontier = max(frontier, 1.0 - smoothstep(0.0, feather * 0.95, abs(safeSize.x - position.x - right)));
    float fizz = frontier * (0.35 + 0.65 * dither);
    color.rgb = mix(color.rgb, half3(0.36, 1.0, 0.56), half(fizz * 0.28));

    return color;
}
