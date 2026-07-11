Shader "K1L0/Experimental Layered Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.08,0.12,0.32,1)
        _MidColor ("Middle", Color) = (0.24,0.16,0.48,1)
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
        _SnowStrength ("Snow", Range(0,1)) = 0
        _AuroraStrength ("Aurora", Range(0,1)) = 0
        _StormStrength ("Storm", Range(0,1)) = 0
        _NightBlackness ("Night Blackness", Range(0,1)) = .72
        _SunDirection ("Sun Direction", Vector) = (0,1,0,0)
        _MoonDirection ("Moon Direction", Vector) = (0,-1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float3 worldDir : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _TopColor, _MidColor, _HorizonColor, _CloudColor;
            float _CloudOpacity, _CloudSpeed, _CloudScale, _CloudContrast;
            float4 _SunUV, _MoonUV;
            float _SunVisibility, _MoonVisibility, _StarsVisibility;
            float _NightAmount, _RainStrength, _SnowStrength, _AuroraStrength, _StormStrength, _NightBlackness;
            float4 _SunDirection, _MoonDirection;
            CBUFFER_END
            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            Varyings vert(Attributes i) { Varyings o; float3 w=TransformObjectToWorld(i.positionOS.xyz); o.positionHCS=TransformWorldToHClip(w); o.worldDir=normalize(w-_WorldSpaceCameraPos); float3 d=o.worldDir; o.uv=float2(atan2(d.x,d.z)/6.2831853+.5,asin(clamp(d.y,-1,1))/3.14159265+.5); return o; }
            float hash21(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y); }
            float fbm(float2 p) { float v=0; v+=noise(p)*.52; p=p*2.03+17.1; v+=noise(p)*.27; p=p*2.01+9.7; v+=noise(p)*.14; p=p*2.04; v+=noise(p)*.07; return v; }
            float cloudTriplanar(float3 d,float scale,float drift)
            {
                float3 w=pow(abs(d),4.0); w/=max(.001,w.x+w.y+w.z);
                float a=SAMPLE_TEXTURE2D(_CloudTex,sampler_CloudTex,frac(d.yz*scale+float2(drift,.17))).r;
                float b=SAMPLE_TEXTURE2D(_CloudTex,sampler_CloudTex,frac(d.xz*scale+float2(-.31,drift*.73))).r;
                float c=SAMPLE_TEXTURE2D(_CloudTex,sampler_CloudTex,frac(d.xy*scale+float2(drift*.41,.53))).r;
                return a*w.x+b*w.y+c*w.z;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float y=saturate(i.uv.y);
                float3 viewDir=normalize(i.worldDir);
                half3 lowerSky=lerp(_HorizonColor.rgb,_MidColor.rgb,smoothstep(0.02,0.46,y));
                half3 sky=lerp(lowerSky,_TopColor.rgb,smoothstep(0.24,0.68,y));
                float horizonAir=pow(saturate(1.0-abs(viewDir.y)),3.0);
                float sunMu=saturate(dot(viewDir,normalize(_SunDirection.xyz))*.5+.5);
                sky += half3(.22,.34,.62)*half(horizonAir*(1.0-_NightAmount)*.28);
                sky += half3(1.0,.48,.18)*half(pow(sunMu,18.0)*horizonAir*_SunVisibility*.16);
                float slowTime=_Time.y*_CloudSpeed*.025;
                float3 weights=pow(abs(viewDir),4.0); weights/=max(.001,weights.x+weights.y+weights.z);
                float farDensity=fbm(viewDir.yz*_CloudScale*.9+slowTime)*weights.x+
                    fbm(viewDir.xz*_CloudScale*.9+float2(7.3,slowTime*.7))*weights.y+
                    fbm(viewDir.xy*_CloudScale*.9+float2(slowTime*.4,13.1))*weights.z;
                farDensity=saturate((farDensity-.43)*(_CloudContrast*.8));
                float nearDensity=cloudTriplanar(viewDir,_CloudScale*1.16,slowTime*1.7);
                nearDensity=saturate((nearDensity-.20)*(_CloudContrast*1.15));
                float density=saturate(farDensity*.42+nearDensity*.82);
                float highCloud=saturate((cloudTriplanar(viewDir,_CloudScale*.47,-slowTime*.35)-.49)*(_CloudContrast*.7));
                density=saturate(density+highCloud*.32);
                // Bring cloud bodies down to the horizon; the previous .30
                // lower fade made them visible mainly when the camera looked up.
                density*=smoothstep(.0,.075,y)*smoothstep(1.05,.78,y);
                // Celestial layer sits behind cloud density.
                half3 nightSky=sky*lerp(half3(.18,.22,.38),half3(.008,.012,.025),_NightBlackness);
                sky=lerp(sky,nightSky,_NightAmount*.94);
                float curtainCenter=.61+sin(i.uv.x*19.0+_Time.y*.11)*.035+sin(i.uv.x*43.0-_Time.y*.07)*.014;
                float ribbonA=exp(-pow((y-curtainCenter)/.028,2.0));
                float ribbonB=exp(-pow((y-curtainCenter+.075)/.045,2.0))*.48;
                float folds=.28+.72*pow(saturate(sin(i.uv.x*71.0+fbm(float2(i.uv.x*8.0,_Time.y*.025))*5.0)*.5+.5),2.0);
                float aurora=(ribbonA+ribbonB)*folds*_AuroraStrength*_NightAmount;
                sky += lerp(half3(.04,1.0,.38),half3(.48,.12,1.0),saturate(sin(i.uv.x*9.0)*.5+.5))*half(aurora*.82);
                float2 starGrid=i.uv*float2(1250,625);
                float2 starCell=floor(starGrid);
                float starHash=hash21(starCell);
                float starDot=1.0-smoothstep(.035,.16,length(frac(starGrid)-.5));
                float star=step(.9972,starHash)*starDot*(0.45+0.55*hash21(starCell+17.3))*_StarsVisibility;
                sky += half3(.72,.82,1.0)*half(star);
                float sunD=acos(clamp(dot(viewDir,normalize(_SunDirection.xyz)),-1,1));
                float sunDisc=1.0-smoothstep(.008,.011,sunD);
                float sunGlow=1.0-smoothstep(.012,.11,sunD);
                sky += half3(1.0,.72,.38)*half((sunDisc*1.4+sunGlow*.28)*_SunVisibility);
                float moonD=acos(clamp(dot(viewDir,normalize(_MoonDirection.xyz)),-1,1));
                float moonDisc=1.0-smoothstep(.008,.012,moonD);
                float moonGlow=1.0-smoothstep(.013,.07,moonD);
                sky += half3(.68,.78,1.0)*half((moonDisc*.95+moonGlow*.18)*_MoonVisibility);
                half3 litCloud=lerp(_CloudColor.rgb*.45,_CloudColor.rgb,saturate(y*.75+density*.4));
                half3 result=lerp(sky,litCloud,density*_CloudOpacity);
                float lightning=pow(saturate(sin(_Time.y*1.7+hash21(floor(_Time.y*.22))*19.0)),38.0)*_StormStrength;
                result += half3(.58,.68,1.0)*half(lightning*.75);
                result=lerp(result,result*half3(.62,.69,.78),max(_RainStrength*.22,_StormStrength*.38));
                return half4(result,1);
            }
            ENDHLSL
        }
    }
}
