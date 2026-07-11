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
        _CloudTex ("Photoreal Cloud Density", 2D) = "gray" {}
        _SunUV ("Sun Position", Vector) = (.5,.65,0,0)
        _SunVisibility ("Sun Visibility", Range(0,1)) = 1
        _MoonUV ("Moon Position", Vector) = (.5,.55,0,0)
        _MoonVisibility ("Moon Visibility", Range(0,1)) = 0
        _StarsVisibility ("Stars Visibility", Range(0,1)) = 0
        _NightAmount ("Night", Range(0,1)) = 0
        _RainStrength ("Rain", Range(0,1)) = 0
        _AuroraStrength ("Aurora", Range(0,1)) = 0
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
            float4 _SunUV, _MoonUV;
            float _SunVisibility, _MoonVisibility, _StarsVisibility;
            float _NightAmount, _RainStrength, _AuroraStrength;
            CBUFFER_END
            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            Varyings vert(Attributes i) { Varyings o; o.positionHCS = TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
            float hash21(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y); }
            float fbm(float2 p) { float v=0; v+=noise(p)*.52; p=p*2.03+17.1; v+=noise(p)*.27; p=p*2.01+9.7; v+=noise(p)*.14; p=p*2.04; v+=noise(p)*.07; return v; }
            half4 frag(Varyings i) : SV_Target
            {
                float y=saturate(i.uv.y);
                half3 sky=lerp(_HorizonColor.rgb,_TopColor.rgb,smoothstep(0.02,0.92,y));
                // Portrait correction: repeat the square cloud domain along Y
                // rather than stretching one texture over the tall sky plane.
                float2 p=float2(i.uv.x*2.2,i.uv.y*2.2)*_CloudScale;
                float slowTime=_Time.y*_CloudSpeed*.025;
                p.x += slowTime;
                float farDensity=fbm(p*.58+fbm(p*.31+31.0)*1.8);
                farDensity=saturate((farDensity-.43)*(_CloudContrast*.8));
                // Two asymmetric, offset plates remove the obvious bilateral
                // reflection seam produced by the former mirror-repeat.
                float2 nearUV=p*float2(.48,.48)+float2(slowTime*1.7, -.08);
                nearUV += float2(fbm(p*.42+7.1),fbm(p*.39+19.7))*.075;
                float plateA=SAMPLE_TEXTURE2D(_CloudTex,sampler_CloudTex,frac(nearUV)).r;
                float2 offsetUV=float2(nearUV.y*.83+0.371,-nearUV.x*1.07+0.619);
                float plateB=SAMPLE_TEXTURE2D(_CloudTex,sampler_CloudTex,frac(offsetUV)).r;
                float nearDensity=lerp(plateA,plateB,.38+.18*fbm(p*.27+43.0));
                nearDensity=saturate((nearDensity-.20)*(_CloudContrast*1.15));
                float density=saturate(farDensity*.42+nearDensity*.82);
                // Bring cloud bodies down to the horizon; the previous .30
                // lower fade made them visible mainly when the camera looked up.
                density*=smoothstep(.0,.075,y)*smoothstep(1.05,.78,y);
                // Celestial layer sits behind cloud density.
                sky=lerp(sky,sky*half3(.12,.18,.36),_NightAmount*.88);
                float auroraBand=pow(saturate(1.0-abs(y-(.56+sin(i.uv.x*11.0+_Time.y*.09)*.055))/.24),2.2);
                float auroraNoise=fbm(float2(i.uv.x*7.0+_Time.y*.025,y*3.0));
                sky += lerp(half3(.05,1.0,.48),half3(.55,.16,1.0),saturate(sin(i.uv.x*5.0)*.5+.5)) * half(auroraBand*auroraNoise*_AuroraStrength*_NightAmount*.65);
                float2 starCell=floor(i.uv*float2(620,920));
                float starHash=hash21(starCell);
                float star=step(.9965,starHash)*(0.45+0.55*hash21(starCell+17.3))*_StarsVisibility;
                sky += half3(.72,.82,1.0)*half(star);
                float sunD=length((i.uv-_SunUV.xy)*float2(1.0,1.45));
                float sunDisc=1.0-smoothstep(.018,.024,sunD);
                float sunGlow=1.0-smoothstep(.025,.14,sunD);
                sky += half3(1.0,.72,.38)*half((sunDisc*1.4+sunGlow*.28)*_SunVisibility);
                float moonD=length((i.uv-_MoonUV.xy)*float2(1.0,1.45));
                float moonDisc=1.0-smoothstep(.017,.023,moonD);
                float moonGlow=1.0-smoothstep(.023,.10,moonD);
                sky += half3(.68,.78,1.0)*half((moonDisc*.95+moonGlow*.18)*_MoonVisibility);
                half3 litCloud=lerp(_CloudColor.rgb*.45,_CloudColor.rgb,saturate(y*.75+density*.4));
                half3 result=lerp(sky,litCloud,density*_CloudOpacity);
                float2 rainUV=float2(i.uv.x+i.uv.y*.16,i.uv.y+_Time.y*.72);
                float rainCell=hash21(floor(rainUV*float2(240,38)));
                float rainLine=step(.965,rainCell)*smoothstep(.48,.02,abs(frac(rainUV.y*38)-.5));
                result += half3(.48,.62,.78)*half(rainLine*_RainStrength*(.35+.65*_NightAmount));
                result=lerp(result,result*half3(.62,.69,.78),_RainStrength*.22);
                return half4(result,1);
            }
            ENDHLSL
        }
    }
}
