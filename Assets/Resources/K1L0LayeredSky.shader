Shader "K1L0/Experimental Layered Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.08,0.12,0.32,1)
        _HorizonColor ("Horizon", Color) = (0.9,0.36,0.52,1)
        _CloudColor ("Cloud", Color) = (0.95,0.92,0.9,1)
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.7
        _CloudSpeed ("Cloud Speed", Range(-2,2)) = 0.08
        _CloudScale ("Cloud Scale", Range(0.5,8)) = 2.2
        _CloudContrast ("Cloud Contrast", Range(0.1,4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _TopColor, _HorizonColor, _CloudColor;
            float _CloudOpacity, _CloudSpeed, _CloudScale, _CloudContrast;
            CBUFFER_END
            Varyings vert(Attributes i) { Varyings o; o.positionHCS = TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            float hash21(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y); }
            float fbm(float2 p) { float v=0; v+=noise(p)*.52; p=p*2.03+17.1; v+=noise(p)*.27; p=p*2.01+9.7; v+=noise(p)*.14; p=p*2.04; v+=noise(p)*.07; return v; }
            half4 frag(Varyings i) : SV_Target
            {
                float y=saturate(i.uv.y);
                half3 sky=lerp(_HorizonColor.rgb,_TopColor.rgb,smoothstep(0.02,0.92,y));
                float2 p=float2(i.uv.x*2.2,i.uv.y)*_CloudScale;
                p.x += _Time.y*_CloudSpeed;
                float density=fbm(p+fbm(p*.55+31.0)*1.8);
                density=saturate((density-.42)*_CloudContrast);
                // Bring cloud bodies down to the horizon; the previous .30
                // lower fade made them visible mainly when the camera looked up.
                density*=smoothstep(.0,.075,y)*smoothstep(1.05,.78,y);
                half3 litCloud=lerp(_CloudColor.rgb*.45,_CloudColor.rgb,saturate(y*.75+density*.4));
                return half4(lerp(sky,litCloud,density*_CloudOpacity),1);
            }
            ENDHLSL
        }
    }
}
