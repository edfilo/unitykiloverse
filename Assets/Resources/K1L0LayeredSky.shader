Shader "K1L0/Experimental Layered Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.08,0.12,0.32,1)
        _MidColor ("Middle", Color) = (0.24,0.16,0.48,1)
        _HorizonColor ("Horizon", Color) = (0.9,0.36,0.52,1)
        _HorizonHeight ("Horizon Height", Range(0,1)) = 0.0
        _CloudColor ("Cloud", Color) = (0.95,0.92,0.9,1)
        _CloudShadeColor ("Cloud Shadow", Color) = (0.66,0.68,0.74,1)
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.7
        _CloudCoverage ("Cloud Coverage", Range(0,1)) = 0.35
        _CloudSpeed ("Cloud Speed", Range(-2,2)) = 0.0
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
        [HDR] _NightHorizonGlowColor ("Night Horizon Glow", Color) = (.08,.22,.65,1)
        _NightHorizonGlow ("Night Horizon Strength", Range(0,2)) = .55
        _SunDirection ("Sun Direction", Vector) = (0,1,0,0)
        _MoonDirection ("Moon Direction", Vector) = (0,-1,0,0)
        _SkyYawOffset ("Sky Yaw Offset", Float) = 0
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
            half4 _TopColor, _MidColor, _HorizonColor, _CloudColor, _CloudShadeColor, _NightHorizonGlowColor;
            float _CloudOpacity, _CloudCoverage, _CloudSpeed, _CloudScale, _CloudContrast, _HorizonHeight, _SkyYawOffset;
            float4 _SunUV, _MoonUV;
            float _SunVisibility, _MoonVisibility, _StarsVisibility;
            float _NightAmount, _RainStrength, _SnowStrength, _AuroraStrength, _StormStrength, _NightBlackness, _NightHorizonGlow;
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
                float3 viewDir=normalize(i.worldDir);
                // In Sky Mode Unity supplies the inverse change in compass
                // heading. This freezes spatial orientation without freezing
                // _Time-driven clouds, weather, stars, or aurora animation.
                float yawCos=cos(_SkyYawOffset), yawSin=sin(_SkyYawOffset);
                float2 originalXZ=viewDir.xz;
                viewDir=float3(
                    yawCos*originalXZ.x+yawSin*originalXZ.y,
                    viewDir.y,
                    -yawSin*originalXZ.x+yawCos*originalXZ.y);
                // True altitude above the dome equator: zero is the horizon,
                // one is zenith. Spherical UV.y places the horizon at .5 and
                // previously let mid/zenith color overpower it.
                // Extend the horizon-color region upward before smoothly
                // remapping toward mid-sky and zenith.
                float y=saturate((viewDir.y-_HorizonHeight)/max(.05,1.0-_HorizonHeight));
                half3 lowerSky=lerp(_HorizonColor.rgb,_MidColor.rgb,smoothstep(0.015,0.19,y));
                half3 sky=lerp(lowerSky,_TopColor.rgb,smoothstep(0.38,0.84,y));
                float horizonAir=pow(saturate(1.0-abs(viewDir.y)),3.0);
                float sunMu=saturate(dot(viewDir,normalize(_SunDirection.xyz))*.5+.5);
                sky += half3(.22,.34,.62)*half(horizonAir*(1.0-_NightAmount)*.28);
                sky += half3(1.0,.58,.24)*half(pow(sunMu,28.0)*horizonAir*_SunVisibility*.08);
                // Visible atmospheric drift without the time-lapse look.
                float slowTime=_Time.y*_CloudSpeed*.12;
                float coverage=saturate(_CloudCoverage);
                float coverageScale=lerp(1.08,.62,coverage);
                float3 weights=pow(abs(viewDir),4.0); weights/=max(.001,weights.x+weights.y+weights.z);
                float cloudScale=_CloudScale*coverageScale;
                float farDensity=fbm(viewDir.yz*cloudScale*.9+slowTime)*weights.x+
                    fbm(viewDir.xz*cloudScale*.9+float2(7.3,slowTime*.7))*weights.y+
                    fbm(viewDir.xy*cloudScale*.9+float2(slowTime*.4,13.1))*weights.z;
                float farThreshold=lerp(.58,.30,coverage);
                farDensity=saturate((farDensity-farThreshold)*(_CloudContrast*1.08));
                float nearDensity=cloudTriplanar(viewDir,cloudScale*1.16,slowTime*1.7);
                float nearThreshold=lerp(.52,.24,coverage);
                nearDensity=saturate((nearDensity-nearThreshold)*(_CloudContrast*1.04));
                float density=saturate(farDensity*.48+nearDensity*.68);
                float highThreshold=lerp(.61,.36,coverage);
                float highCloud=saturate((cloudTriplanar(viewDir,cloudScale*.47,-slowTime*.35)-highThreshold)*(_CloudContrast*.78));
                float broadOvercast=fbm(viewDir.xz*cloudScale*.22+float2(slowTime*.12,19.7));
                float overcastFill=smoothstep(.78,1.0,coverage)*saturate(.16+(broadOvercast-.18)*.85);
                density=smoothstep(.015,lerp(.72,.82,coverage),
                    saturate(density+highCloud*lerp(.12,.24,coverage)+overcastFill));
                // Bring cloud bodies down to the horizon; the previous .30
                // lower fade made them visible mainly when the camera looked up.
                density*=smoothstep(lerp(.055,.008,coverage),lerp(.19,.07,coverage),y)*smoothstep(1.05,.82,y);
                // Celestial layer sits behind cloud density.
                half3 nightSky=sky*lerp(half3(.18,.22,.38),half3(.008,.012,.025),_NightBlackness);
                sky=lerp(sky,nightSky,_NightAmount*.94);
                // Add the distant city/atmospheric light after night darkening,
                // otherwise the night multiplier crushes the glow back to black.
                float nightHorizonBand=exp(-pow(abs(viewDir.y)/.055,2.0));
                sky += _NightHorizonGlowColor.rgb*half(nightHorizonBand*_NightHorizonGlow*_NightAmount);
                // Keep the aurora visible from the normal map camera, not only
                // when looking toward the dome's zenith in Sky Mode.
                // Integer azimuth harmonics join exactly at the spherical wrap;
                // the former raw UV frequencies produced a sharp vertical seam.
                float az=atan2(viewDir.x,viewDir.z);
                float curtainCenter=.39+sin(az*3.0+_Time.y*.11)*.035+sin(az*7.0-_Time.y*.07)*.014;
                float ribbonA=exp(-pow((y-curtainCenter)/.028,2.0));
                float ribbonB=exp(-pow((y-curtainCenter+.075)/.045,2.0))*.48;
                float azNoise=noise(float2(cos(az)*3.0+_Time.y*.018,sin(az)*3.0));
                float folds=.28+.72*pow(saturate(sin(az*12.0+azNoise*5.0)*.5+.5),2.0);
                float aurora=(ribbonA+ribbonB)*folds*_AuroraStrength*_NightAmount;
                sky += lerp(half3(.04,1.0,.38),half3(.48,.12,1.0),saturate(sin(az*2.0)*.5+.5))*half(aurora*.82);
                // Two sparse populations avoid the old regular field of equally
                // sized blue dots. Most stars are sub-pixel and dim; a much
                // smaller set is brighter, temperature-varied, and twinkles
                // gently at an independent rate. Keep this procedural so the
                // night sky costs no particles, textures, or draw calls.
                float starAltitude=smoothstep(-.015,.12,viewDir.y);
                float2 fineGrid=i.uv*float2(1640,820);
                float2 fineCell=floor(fineGrid);
                float fineHash=hash21(fineCell);
                float fineRadius=lerp(.16,.29,hash21(fineCell+31.7));
                float fineDot=1.0-smoothstep(fineRadius*.42,fineRadius,length(frac(fineGrid)-.5));
                float fineStar=step(.99805,fineHash)*fineDot*lerp(.18,.62,hash21(fineCell+9.1));

                float2 brightGrid=i.uv*float2(940,470)+.37;
                float2 brightCell=floor(brightGrid);
                float brightHash=hash21(brightCell+73.4);
                float brightDot=1.0-smoothstep(.07,.25,length(frac(brightGrid)-.5));
                float brightMask=step(.99935,brightHash);
                float phase=hash21(brightCell+18.2)*6.2831853;
                float twinkle=.86+.14*sin(_Time.y*lerp(.35,.85,hash21(brightCell+44.8))+phase);
                float brightStar=brightMask*brightDot*twinkle*lerp(.72,1.28,hash21(brightCell+5.6));
                float temperature=hash21(brightCell+101.9);
                half3 starColor=temperature<.20 ? half3(1.0,.72,.52) :
                    (temperature>.76 ? half3(.58,.76,1.0) : half3(.92,.94,1.0));
                sky += (half3(.72,.82,1.0)*half(fineStar)+starColor*half(brightStar))
                    *half(_StarsVisibility*starAltitude);
                float sunD=acos(clamp(dot(viewDir,normalize(_SunDirection.xyz)),-1,1));
                float sunDisc=1.0-smoothstep(.012,.016,sunD);
                float sunInner=1.0-smoothstep(.016,.032,sunD);
                float sunGlow=1.0-smoothstep(.025,.078,sunD);
                sky += half3(1.0,.86,.56)*half(sunDisc*2.0*_SunVisibility);
                sky += half3(1.0,.54,.16)*half((sunInner*.64+sunGlow*.18)*_SunVisibility);
                float moonD=acos(clamp(dot(viewDir,normalize(_MoonDirection.xyz)),-1,1));
                float moonDisc=1.0-smoothstep(.008,.012,moonD);
                float moonGlow=1.0-smoothstep(.013,.07,moonD);
                sky += half3(.68,.78,1.0)*half((moonDisc*.95+moonGlow*.18)*_MoonVisibility);
                float cloudLight=saturate(.16+y*.42+farDensity*.18+nearDensity*.12-highCloud*.08);
                half3 litCloud=lerp(_CloudShadeColor.rgb,_CloudColor.rgb,half(cloudLight));
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
