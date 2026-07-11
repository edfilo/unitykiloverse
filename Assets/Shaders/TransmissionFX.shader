Shader "Custom/TransmissionFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Crop / flip
        _CropRect ("Crop XYWH (UV)", Vector) = (0,0,1,1)
        _Flip ("HFlip,VFlip", Vector) = (0,0,0,0)

        // Color
        _Invert ("Invert", Float) = 0
        _Saturation ("Saturation", Float) = 1
        _Contrast ("Contrast", Float) = 1
        _Brightness ("Brightness", Float) = 0
        _Posterize ("Posterize Levels (0=off)", Float) = 0

        // Chromatic shift (R offset xy, B offset xy) in pixels
        _ChromaShift ("Chroma R.xy B.xy (px)", Vector) = (0,0,0,0)

        // Blur radius in pixels
        _Blur ("Blur (px)", Float) = 0

        // Noise grain
        _NoiseAmount ("Noise (0-1)", Float) = 0

        // Wavy displacement (px amp, period px, speed rad/s)
        _Wave ("Wave Amp,Period,Speed", Vector) = (0,40,2.5,0)

        // Flash (color rgb, amount a)
        _Flash ("Flash Color+Amount", Color) = (1,1,1,0)

        // Vignette amount 0-1
        _Vignette ("Vignette", Float) = 0

        // Source size for px → uv conversion
        _SrcSize ("Source W,H", Vector) = (576,1024,0,0)

        // Fizzy edges toggle
        _FizzyEdges ("Fizzy Edges Enabled", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            float4 _CropRect;
            float4 _Flip;
            float  _Invert;
            float  _Saturation;
            float  _Contrast;
            float  _Brightness;
            float  _Posterize;
            float4 _ChromaShift;
            float  _Blur;
            float  _NoiseAmount;
            float4 _Wave;
            fixed4 _Flash;
            float  _Vignette;
            float4 _SrcSize;
            float  _FizzyEdges;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed3 sampleSrc(float2 uv)
            {
                // crop+flip remap: outputUV [0..1] maps to source rect inside _CropRect
                float2 cuv;
                cuv.x = _CropRect.x + uv.x * _CropRect.z;
                cuv.y = _CropRect.y + uv.y * _CropRect.w;
                if (_Flip.x > 0.5) cuv.x = (_CropRect.x + _CropRect.z) - (cuv.x - _CropRect.x);
                if (_Flip.y > 0.5) cuv.y = (_CropRect.y + _CropRect.w) - (cuv.y - _CropRect.y);
                return tex2D(_MainTex, cuv).rgb;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 px = 1.0 / max(_SrcSize.xy, float2(1,1));

                // Wavy displacement (geq emulation)
                if (_Wave.x > 0.001)
                {
                    float period = max(_Wave.y, 1.0);
                    float ang = (uv.y * _SrcSize.y / period) * 6.28318 + _Time.y * _Wave.z;
                    uv.x += sin(ang) * _Wave.x * px.x;
                }

                // Chromatic shift sampling
                float2 rOff = _ChromaShift.xy * px;
                float2 bOff = _ChromaShift.zw * px;

                fixed3 col;
                if (_Blur > 0.01)
                {
                    // Cheap 5-tap blur
                    float2 b = _Blur * px;
                    fixed3 sum = sampleSrc(uv);
                    sum += sampleSrc(uv + float2( b.x, 0));
                    sum += sampleSrc(uv + float2(-b.x, 0));
                    sum += sampleSrc(uv + float2(0,  b.y));
                    sum += sampleSrc(uv + float2(0, -b.y));
                    col = sum / 5.0;
                }
                else if (abs(_ChromaShift.x)+abs(_ChromaShift.y)+abs(_ChromaShift.z)+abs(_ChromaShift.w) > 0.1)
                {
                    col.r = sampleSrc(uv + rOff).r;
                    col.g = sampleSrc(uv).g;
                    col.b = sampleSrc(uv + bOff).b;
                }
                else
                {
                    col = sampleSrc(uv);
                }

                // Invert
                col = lerp(col, 1.0 - col, saturate(_Invert));

                // Saturation
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(float3(luma, luma, luma), col, _Saturation);

                // Contrast / brightness
                col = (col - 0.5) * _Contrast + 0.5 + _Brightness;

                // Posterize
                if (_Posterize > 1.5)
                {
                    col = floor(col * _Posterize) / _Posterize;
                }

                // Noise grain
                if (_NoiseAmount > 0.001)
                {
                    float n = hash(uv * _SrcSize.xy + _Time.y * 60.0) - 0.5;
                    col += n * _NoiseAmount;
                }

                // Vignette
                if (_Vignette > 0.001)
                {
                    float2 v = uv - 0.5;
                    float vig = smoothstep(0.75, 0.2, dot(v, v) * 2.0);
                    col *= lerp(1.0, vig, _Vignette);
                }

                // Flash
                col = lerp(col, _Flash.rgb, saturate(_Flash.a));

                col = saturate(col);
                fixed4 outCol = fixed4(col, 1.0) * IN.color;

                if (_FizzyEdges > 0.5)
                {
                    float2 distToEdge = min(uv, 1.0 - uv);
                    float minDist = min(distToEdge.x, distToEdge.y);
                    float edgeWidth = 0.05;
                    if (minDist < edgeWidth)
                    {
                        float factor = minDist / edgeWidth;
                        float n = hash(uv * _SrcSize.xy + _Time.y * 60.0);
                        float threshold = smoothstep(0.0, 1.0, factor);
                        if (threshold < n)
                        {
                            outCol.a *= threshold * 0.5;
                        }
                    }
                }

                #ifdef UNITY_UI_CLIP_RECT
                outCol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return outCol;
            }
            ENDCG
        }
    }
}
