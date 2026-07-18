Shader "K1L0/GroundHaze"
{
    Properties
    {
        _SmokeColor("Smoke Color", Color) = (1.0, 0.28, 0.05, 1)
        _PinkAmount("Pink Patches", Range(0,1)) = 0.34
        _WhiteAmount("White Patches", Range(0,1)) = 0.22
        _BlueAmount("Blue Patches", Range(0,1)) = 0.24
        _OrangeAmount("Orange Patches", Range(0,1)) = 0.18
        _Vertical("Vertical Curtain", Range(0,1)) = 0
        _HorizonCurtain("Horizon Seam Curtain", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode("Z Test", Float) = 4
        _Density("Density", Range(0,1)) = 0.34
        _Detail("Detail", Range(0.1,4)) = 1.35
        _Speed("Speed", Range(0,0.5)) = 0.055
        _LayerPhase("Layer Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTestMode]
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SmokeColor;
            float _Density, _Detail, _Speed, _LayerPhase, _PinkAmount, _WhiteAmount;
            float _BlueAmount, _OrangeAmount, _Vertical, _HorizonCurtain;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + 1), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float n = 0.0;
                float a = 0.55;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    n += valueNoise(p) * a;
                    p = mul(float2x2(1.62, 1.18, -1.18, 1.62), p) + 7.13;
                    a *= 0.48;
                }
                return n;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 drift = float2(t, t * .43);
                float2 noiseCoords = lerp(i.world.xz, i.world.xy, _Vertical);
                // Slowly rotate and warp the sampling domain. Unlike a simple
                // scrolling texture, this continuously reforms cloud bodies
                // while keeping motion smooth and using the same FBM budget.
                float angle = sin(t * .19 + _LayerPhase) * .24;
                float sn = sin(angle), cs = cos(angle);
                float2 rotated = mul(float2x2(cs, -sn, sn, cs), noiseCoords);
                float2 breathingWarp = float2(sin(t * .71 + _LayerPhase),
                                              cos(t * .53 - _LayerPhase)) * 1.15;
                float2 p = rotated * (.0065 * _Detail) + drift + breathingWarp + _LayerPhase;
                float broad = fbm(p * .46 + float2(sin(t * .17), cos(t * .13)));
                float curls = fbm(p * 1.7 + broad * 2.1 +
                                  float2(cos(t * .61), sin(t * .47)) * .72);
                float smoke = smoothstep(.43, .78, broad * .64 + curls * .54);
                smoke *= lerp(.52, 1.0, fbm(p * 3.2 - drift * .7));
                float2 edge = smoothstep(0.0, .16, i.uv) * smoothstep(0.0, .16, 1.0 - i.uv);
                float edgeFade = edge.x * edge.y;
                float alpha = smoke * _Density * edgeFade;
                if (_HorizonCurtain > .5)
                {
                    // The procedural sky and world depth meet at a perfectly
                    // straight edge. Shape this overlay into a noisy ridge and
                    // retain a faint, narrow veil through its center so the
                    // one-pixel seam cannot show through gaps in the FBM.
                    float ridgeNoise = saturate(broad * .62 + curls * .38);
                    float ridge = .54 + (ridgeNoise - .5) * .34
                                      + sin(i.uv.x * 15.0 + t * .31 + _LayerPhase) * .035;
                    // The quad's bottom is buried just below world height. Do
                    // not vertically feather there: that fade exposed the
                    // perfectly straight world/sky intersection. Only feather
                    // its side edges and let the noisy ridge form the visible
                    // upper silhouette.
                    // With the controller's 3.4x vertical scale, world height
                    // crosses this quad at UV 0.206. Keep everything below
                    // that transparent, except for a narrow feather centered
                    // on the seam itself. This lets the curtain draw through
                    // the top row of distant ground without tinting the map.
                    float worldHorizon = .206;
                    float lowerFeather = smoothstep(worldHorizon - .085, worldHorizon + .055, i.uv.y);
                    float cloudBody = lowerFeather * (1.0 - smoothstep(ridge, ridge + .17, i.uv.y));
                    float seamVeil = 1.0 - smoothstep(.018, .135, abs(i.uv.y - worldHorizon));
                    float shapedSmoke = smoke * cloudBody + seamVeil * (.34 + smoke * .38);
                    alpha = saturate(shapedSmoke) * _Density * edge.x;
                }
                // Reuse the noise already paid for above to form broad color islands.
                // This keeps the effect one draw per sheet rather than stacking three
                // separately-colored transparent layers.
                // A single cheap value-noise sample decorrelates color from
                // opacity without paying for another FBM stack or draw call.
                float colorNoise = valueNoise(p * .83 + float2(19.2, -7.4));
                fixed3 blue = fixed3(.18, .42, 1.0) * max(.65, _SmokeColor.b + .25);
                fixed3 orange = fixed3(1.0, .30, .07) * max(.65, _SmokeColor.r);
                fixed3 pink = fixed3(1.0, .18, .48) * max(.65, _SmokeColor.r);
                fixed3 softWhite = fixed3(1.0, .86, .78) * max(.65, _SmokeColor.r);
                // Pick one dominant family per broad cloud region. Sequentially
                // blending every family at every pixel averaged the result back
                // into uniform lavender when three sheets overlapped.
                float paletteCoord = frac(colorNoise * .52 + broad * .31 + curls * .17 + t * .012);
                fixed3 targetColor;
                float familyAmount;
                if (paletteCoord < .22)
                {
                    targetColor = blue;
                    familyAmount = _BlueAmount;
                }
                else if (paletteCoord < .42)
                {
                    targetColor = _SmokeColor.rgb; // lavender base family
                    familyAmount = 1.0;
                }
                else if (paletteCoord < .64)
                {
                    targetColor = orange;
                    familyAmount = _OrangeAmount;
                }
                else if (paletteCoord < .86)
                {
                    targetColor = pink;
                    familyAmount = _PinkAmount;
                }
                else
                {
                    targetColor = softWhite;
                    familyAmount = _WhiteAmount;
                }
                // A narrow soft transition at region edges avoids posterized
                // color borders while leaving each cloud visibly distinct.
                float regionEdge = abs(frac(paletteCoord * 5.0) - .5) * 2.0;
                float dominance = familyAmount * smoothstep(.08, .34, regionEdge);
                fixed3 color = lerp(_SmokeColor.rgb, targetColor, dominance);
                color *= lerp(.55, 1.15, curls);
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
