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

static inline float hash12(float2 p);

struct K1L0PointUniforms {
    float time;
    float aspect;
    float pointSize;
    float hasTextures;
    float textureAspect;
    float particleScale;
    float spacing;
    float zSpread;
    float brightness;
    float rotationDegrees;
    float rotationSpeed;
    float perspective;
    float cameraDistance;
    float sparkle;
    float2 foregroundCenter;
};
struct K1L0PointOut { float4 position [[position]]; float pointSize [[point_size]]; half4 color; float sparkle; };
struct K1L0ItemGlitchOut { float4 position [[position]]; float2 uv; };

vertex K1L0ItemGlitchOut k1l0ItemGlitchVertex(uint id [[vertex_id]]) {
    const float2 positions[4] = { {-1,-1}, {1,-1}, {-1,1}, {1,1} };
    const float2 uvs[4] = { {0,1}, {1,1}, {0,0}, {1,0} };
    return { float4(positions[id], 0, 1), uvs[id] };
}

fragment half4 k1l0ItemGlitchFragment(K1L0ItemGlitchOut in [[stage_in]], texture2d<half> colorTex [[texture(0)]], texture2d<half> maskTex [[texture(1)]], constant K1L0PointUniforms& u [[buffer(0)]]) {
    constexpr sampler s(address::clamp_to_edge, filter::linear);
    float2 uv = in.uv;
    float band = floor(uv.y * 36.0 + u.time * 8.0);
    float burst = step(.76, hash12(float2(floor(u.time * 9.0), band)));
    uv.x += (hash12(float2(band, floor(u.time * 13.0))) - .5) * .085 * burst;
    half4 c = colorTex.sample(s, uv); half mask = maskTex.sample(s, uv).r;
    float luma = dot(float3(c.rgb), float3(.299,.587,.114));
    float alpha = smoothstep(.035, .18, max(luma, float(mask))) * float(c.a);
    half r = colorTex.sample(s, uv + float2(.009 * burst, 0)).r;
    half b = colorTex.sample(s, uv - float2(.009 * burst, 0)).b;
    float scan = .80 + .20 * sin(uv.y * 980.0 + u.time * 20.0);
    half3 rgb = half3(r,c.g,b) * half(scan*u.brightness) + half3(.05,.55,1.0) * pow(mask,half(.7)) * half(.35+.15*u.sparkle);
    return half4(rgb, half(alpha*.88));
}

// Item hologram: two depth layers per texel of a 192×192 resampling.
// Depth mask (grayscale, near=bright) displaces points in z; the cloud sways
// so the parallax reads as 3D. Background texels (transparent/near-black) are
// culled by clipping them behind the camera. hasTextures=0 falls back to the
// original cyan diagnostic grid so a texture-load failure stays visible
// instead of rendering pure black.
#define K1L0_POINT_GRID 192u
#define K1L0_STAR_COUNT 56u

vertex K1L0PointOut k1l0ItemPointVertex(uint id [[vertex_id]],
                                        constant K1L0PointUniforms& u [[buffer(0)]],
                                        texture2d<half> colorTex [[texture(0)]],
                                        texture2d<half> depthTex [[texture(1)]]) {
    K1L0PointOut out;
    if (u.hasTextures < 0.5) {
        // Diagnostic pattern: independent of network images/depth maps.
        // If this appears, the MTKView, metallib and render loop all work.
        constexpr uint grid = 9;
        float2 uv = (float2(id % grid, id / grid) + .5) / float(grid);
        float seed = hash12(float2(id, id * 7u));
        float2 screen = (uv - .5) * 1.55;
        float angle = u.time * .35;
        screen = float2(cos(angle) * screen.x - sin(angle) * screen.y,
                        sin(angle) * screen.x + cos(angle) * screen.y);
        screen += float2(sin(u.time * 1.7 + seed * 20.0),
                         cos(u.time * 1.3 + seed * 17.0)) * .025;
        out.position = float4(screen.x / max(u.aspect, .01), screen.y, 0, 1);
        out.sparkle = 1.0;
        out.pointSize = u.pointSize * (1.0 + .35 * sin(u.time * 2.0 + seed * 12.0));
        out.color = half4(half3(.05, .9, 1.0), 1.0);
        return out;
    }

    constexpr sampler s(address::clamp_to_edge, filter::linear);
    constexpr uint grid = K1L0_POINT_GRID;
    constexpr uint frontPointCount = grid * grid;
    constexpr uint itemPointCount = frontPointCount * 2u;

    // A sparse, independent stellar shell surrounds the item. Keeping these
    // vertices in the same draw call makes the whole hologram one cheap pass.
    if (id >= itemPointCount) {
        uint starID = id - itemPointCount;
        float seed = hash12(float2(starID * 19u, starID * 73u));
        float angle = seed * 6.2831853 + u.time * (.012 + hash12(float2(starID, 4u)) * .018);
        float radius = .76 + hash12(float2(starID, 11u)) * .25;
        float2 star = float2(cos(angle), sin(angle)) * radius;
        star.y *= .92;
        star += float2(hash12(float2(starID, 23u)) - .5,
                       hash12(float2(starID, 31u)) - .5) * .10;
        float twinkle = .58 + .42 * sin(u.time * (1.4 + seed * 3.8) + seed * 90.0);
        float rareSpark = pow(max(0.0, sin(u.time * 2.8 + seed * 140.0)), 18.0);
        out.position = float4(star.x / max(u.aspect, .01), star.y, .2, 1);
        out.pointSize = u.pointSize * (.55 + seed * .65 + rareSpark * .45);
        out.sparkle = twinkle;
        float starBlink = pow(max(0.0, sin(u.time * (2.0 + seed * 3.0) + seed * 130.0)), 12.0);
        float starGain = 1.0 + starBlink * max(0.0, u.sparkle) * 1.8;
        out.color = half4(mix(half3(.18, .62, 1.0), half3(1.0), half(twinkle)) * half(u.brightness * starGain),
                          half(.5 + .5 * twinkle));
        return out;
    }

    bool backLayer = id >= frontPointCount;
    uint sampleID = id % frontPointCount;
    float2 uv = (float2(sampleID % grid, sampleID / grid) + .5) / float(grid);
    half4 c = colorTex.sample(s, uv, level(0));
    float luma = dot(float3(c.rgb), float3(.299, .587, .114));
    if (c.a < .15h || luma < .045) {
        // Background texel: clip the point away entirely.
        out.position = float4(0, 0, -2, 1);
        out.pointSize = 0;
        out.color = half4(0);
        out.sparkle = 0;
        return out;
    }

    float depthValue = float(depthTex.sample(s, uv, level(0)).r);
    float seed = hash12(float2(sampleID % grid, sampleID / grid));
    float3 p;
    // Normalized UVs otherwise distort non-square source images. Convert the
    // source's pixel aspect back into display space before perspective.
    p.x = (uv.x - u.foregroundCenter.x) * 1.5 * u.textureAspect * u.spacing;
    // Core Graphics uploads the normalized RGBA buffer bottom-to-top relative
    // to Metal's sampled UV convention. Map sampled rows in the same direction
    // here so the reconstructed item is upright; the independent star ring is
    // already screen-oriented and must not be flipped.
    p.y = (uv.y - u.foregroundCenter.y) * 1.5 * u.spacing;
    // The generated mask describes the visible/front surface. Reflect it into
    // a dimmer rear shell so a complete turn reads as a volume, not a card.
    // The generated grayscale maps encode the nearer surface darker. Flip the
    // earlier interpretation so dark pixels project toward the viewer.
    float frontZ = (.38 - depthValue) * .72;
    p.z = (backLayer ? -frontZ - .12 : frontZ) * u.zSpread;

    // Centered sixty-degree sway in either direction: enough parallax to read
    // the depth while keeping the recognizable front silhouette in view.
    float rotationRadians = clamp(u.rotationDegrees, 10.0, 360.0) * 0.01745329252;
    float angle = sin(u.time * .42 * max(.05, u.rotationSpeed)) * rotationRadians;
    float ca = cos(angle), sa = sin(angle);
    float x = ca * p.x - sa * p.z;
    float z = sa * p.x + ca * p.z;
    // Tiny per-point drift keeps the cloud alive.
    x += sin(u.time * 1.4 + seed * 21.0) * .012;
    float y = p.y + cos(u.time * 1.1 + seed * 15.0) * .012;

    // Camera distance controls framing; perspective independently controls how
    // strongly depth changes apparent size.
    float cameraDistance = clamp(u.cameraDistance, .55, 3.5);
    float perspectiveDepth = z * clamp(u.perspective, 0.0, 2.0);
    float persp = 1.0 / max(.42, cameraDistance - perspectiveDepth);
    out.position = float4(x * persp / max(u.aspect, .01), y * persp, 0, 1);
    float shimmer = .88 + .12 * sin(u.time * (1.6 + seed * 2.8) + seed * 20.0);
    float sizeVariation = .72 + seed * .48;
    float glint = pow(max(0.0, sin(u.time * 3.1 + seed * 180.0)), 24.0);
    float blink = pow(max(0.0, sin(u.time * (1.8 + seed * 4.2) + seed * 210.0)), 10.0);
    float blinkGain = 1.0 + blink * max(0.0, u.sparkle) * 2.2;
    // Glints affect luminance only. Letting them affect diameter caused rare,
    // distracting giant particle flashes.
    out.pointSize = u.pointSize * u.particleScale * persp * sizeVariation * shimmer;
    out.sparkle = shimmer;
    half3 hologramColor = mix(c.rgb, half3(.56, .86, 1.0), half(.10 + glint * .18));
    half layerBrightness = backLayer ? half(.38) : half(.82);
    out.color = half4(hologramColor * layerBrightness * half(.82 + .18 * shimmer) * half(u.brightness * blinkGain),
                      (backLayer ? half(.30) : half(.78)) * half(.90 + .10 * shimmer));
    return out;
}

fragment half4 k1l0ItemPointFragment(K1L0PointOut in [[stage_in]], float2 pc [[point_coord]]) {
    float radius = length(pc - .5);
    if (radius > .5) discard_fragment();
    // A clear solid core keeps the denser cloud from reading as fine dust.
    float glow = radius < .28 ? 1.0 : smoothstep(.5, .28, radius);
    return half4(in.color.rgb * half(glow), in.color.a * half(glow));
}

static inline float hash12(float2 p);

struct K1L0VideoOut { float4 position [[position]]; float2 uv; };
struct K1L0VideoUniforms { float2 viewport; float2 texture; float time; float intensity; };

vertex K1L0VideoOut k1l0VideoVertex(uint id [[vertex_id]]) {
    const float2 positions[4] = { {-1,-1}, {1,-1}, {-1,1}, {1,1} };
    const float2 uvs[4] = { {0,1}, {1,1}, {0,0}, {1,0} };
    return { float4(positions[id], 0, 1), uvs[id] };
}

fragment half4 k1l0VideoFragment(K1L0VideoOut in [[stage_in]],
                                  texture2d<half> frame [[texture(0)]],
                                  constant K1L0VideoUniforms& u [[buffer(0)]]) {
    constexpr sampler s(address::clamp_to_edge, filter::linear);
    float2 uv = in.uv;
    float viewAspect = u.viewport.x / max(u.viewport.y, 1.0);
    float texAspect = u.texture.x / max(u.texture.y, 1.0);
    if (texAspect > viewAspect) uv.x = (uv.x - 0.5) * (viewAspect / texAspect) + 0.5;
    else uv.y = (uv.y - 0.5) * (texAspect / viewAspect) + 0.5;

    float amount = clamp(u.intensity, 0.0, 1.0);
    float row = floor(uv.y * u.texture.y * 0.22);
    float jitter = (hash12(float2(row, floor(u.time * 18.0))) - 0.5) * 0.012 * amount;
    float band = smoothstep(0.08, 0.0, abs(fract(uv.y - u.time * 0.16) - 0.5));
    uv.x += jitter + band * 0.018 * amount;
    float chroma = (0.0015 + 0.005 * hash12(float2(floor(u.time * 3.0), row))) * amount;
    half4 base = frame.sample(s, uv);
    half3 rgb = half3(frame.sample(s, uv + float2(chroma, 0)).r,
                      base.g,
                      frame.sample(s, uv - float2(chroma, 0)).b);
    float grain = hash12(floor(in.uv * u.viewport) + floor(u.time * 24.0) * 19.7) - 0.5;
    rgb += half3(grain * 0.10 * amount);
    rgb *= half(1.0 - (0.035 + 0.07 * amount) * (0.5 + 0.5 * sin(in.uv.y * u.viewport.y * 3.14159)));
    float vignette = smoothstep(0.8, 0.25, length(in.uv - 0.5));
    rgb *= half(mix(1.0, vignette, 0.22 * amount));
    float flash = step(0.985, hash12(float2(floor(u.time * 2.0), 41.0))) * exp(-fract(u.time * 2.0) * 15.0);
    rgb = mix(rgb, half3(1.0), half(flash * 0.45 * amount));

    float edge = min(min(in.uv.x, 1.0 - in.uv.x), min(in.uv.y, 1.0 - in.uv.y));
    float rag = 0.005 + 0.008 * hash12(floor(in.uv * u.viewport * 0.18) + floor(u.time * 8.0));
    // Hard alpha cut: preserve the animated tattered silhouette without the
    // semitransparent gray fringe that a feathered edge creates over black.
    float alpha = edge >= rag ? 1.0 : 0.0;
    // The first opaque source pixels can themselves be pale and read as a
    // one-pixel outline. Fade only the *inside* of the torn cut to black so
    // every source frame meets the black player background invisibly.
    float interiorFade = smoothstep(rag, rag + 0.014, edge);
    rgb *= half(interiorFade);
    return half4(rgb, half(base.a * alpha));
}

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
