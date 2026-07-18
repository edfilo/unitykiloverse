Shader "K1L0/ProjectorLaserBeam"
{
    Properties
    {
        [HDR] _Color ("Laser Color", Color) = (0.25,0.6,1,1)
        _Intensity ("Intensity", Float) = 3
        _TimeOffset ("Time", Float) = 0
        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _BaseOnly ("Ground Base Only", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+40" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            half4 _Color;
            float _Intensity;
            float _TimeOffset;
            float _GlitchAmount;
            float _BaseOnly;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _TimeOffset;

                // Companion horizontal quad: a soft animated projector source
                // on the road surface. Keeping it in this shader avoids another
                // shader/material family while giving the base true perspective.
                if (_BaseOnly > .5)
                {
                    float2 p=(uv-.5)*2.0;
                    float r=length(p);
                    float halo=exp(-r*r*2.35)*smoothstep(1.0,.72,r);
                    float innerRing=exp(-pow((r-.31)*25.0,2.0));
                    float mainRing=exp(-pow((r-.53)*22.0,2.0));
                    float outerRing=exp(-pow((r-.77)*18.0,2.0));
                    float ringBloom=exp(-pow((r-.54)*5.2,2.0));
                    float radialDetail=.90+.10*sin(atan2(p.y,p.x)*13.0-t*.72);
                    float pulse=.92+.08*sin(t*1.32+hash21(floor(p*8.0))*1.7);
                    half3 blue=lerp(_Color.rgb,half3(.08,.42,1.0),.24);
                    half3 cyan=lerp(blue,half3(.05,.92,1.0),.36);
                    half3 baseLight=(blue*halo*.34+cyan*ringBloom*.25)*pulse;
                    baseLight+=cyan*(innerRing*.62+mainRing*1.34+outerRing*.42)*radialDetail;
                    baseLight*= _Intensity*.62;
                    return half4(baseLight,saturate(halo*.62+ringBloom*.35+innerRing+mainRing+outerRing*.55));
                }
                float band = floor(uv.y * 24.0 + t * 5.0);
                float burst = step(0.86, hash21(float2(band, floor(t * 8.0))));
                float tear = (hash21(float2(floor(t * 14.0), band)) - 0.5) * 0.18 * burst;
                float wave = sin(uv.y * 18.0 + t * 2.4) * 0.020 +
                             sin(uv.y * 47.0 - t * 4.1) * 0.009;

                // A straight projector shaft. Its outside edge stays parallel;
                // depth comes from layered haze and moving detail, not a cone
                // that bends or balloons toward the item.
                float coneWidth = 0.78;
                float signedX = ((uv.x + (tear + wave) * _GlitchAmount) * 2.0 - 1.0) / coneWidth;
                float x = abs(signedX);
                float coneMask = 1.0 - smoothstep(0.56, 1.08, x);
                float outerBlur = exp(-x*x*.72)*coneMask;
                float softVolume = exp(-x * x * 1.65) * coneMask;
                float innerHaze = exp(-x * x * 4.8) * 0.07;

                // Keep just a faint projector texture. Strong, tightly packed
                // scan modulation made the entire volume look striped.
                float scan = 0.93 + 0.07 * sin(uv.y * 72.0 - t * 6.0);
                float coarseNoise = lerp(0.58, 1.12,
                    hash21(float2(floor(uv.y * 72.0 - t * 9.0), floor(t * 6.0))));
                float sliceNoise = hash21(float2(band, floor(t * 16.0)));
                float dropout = lerp(1.0, lerp(0.34, 0.68, sliceNoise), burst);
                float brokenSlice = step(0.025, hash21(float2(band + 91.0, floor(t * 7.0))));
                float endFade = smoothstep(0.0, 0.045, uv.y) * smoothstep(1.0, 0.88, uv.y);
                float topBloom = smoothstep(0.64, 0.98, uv.y) * softVolume;
                float glitchModulation = lerp(1.0, scan * coarseNoise * dropout * brokenSlice, _GlitchAmount);
                float fogDetail=.84+.16*sin(uv.y*9.0-t*.68+sin(signedX*5.0+t*.21));
                float energy = (outerBlur*.32+softVolume*.72+innerHaze+topBloom * 0.22) * fogDetail *
                               glitchModulation * endFade;

                // Fine projected filaments drift upward at several speeds.
                // They add depth and motion without spawning particle systems.
                float raySeed=floor((signedX*.5+.5)*19.0);
                float rayPhase=hash21(float2(raySeed,13.7))*6.2831853;
                float rayWidth=lerp(.018,.060,hash21(float2(raySeed,41.2)));
                float rayWander=sin(t*lerp(.28,.62,hash21(float2(raySeed,5.4)))+rayPhase)*.11;
                float rayCenter=(hash21(float2(raySeed,7.9))-.5)*1.45+rayWander;
                float ray=exp(-pow((signedX-rayCenter)/rayWidth,2.0));
                float risingPhase=frac(uv.y*lerp(1.8,3.8,hash21(float2(raySeed,22.1)))
                    -t*lerp(.22,.48,hash21(float2(raySeed,58.4)))+rayPhase*.159);
                float rayPulse=.26+.74*exp(-pow((risingPhase-.5)*2.15,2.0));
                ray*=rayPulse*coneMask*endFade;

                // Broad translucent ribbons travel upward behind the fine rays.
                // Their paths are continuous and sinusoidal—movement, not glitch.
                float ribbonCenterA=sin(uv.y*5.2-t*.72+rayPhase*.13)*.22;
                float ribbonCenterB=sin(uv.y*7.7+t*.49+1.8)*.28;
                float ribbonA=exp(-pow((signedX-ribbonCenterA)/.19,2.0));
                float ribbonB=exp(-pow((signedX-ribbonCenterB)/.24,2.0));
                float ribbonTravel=.58+.42*sin(uv.y*11.0-t*1.35);
                float ribbons=(ribbonA*.20+ribbonB*.13)*ribbonTravel*coneMask*endFade;

                // Sparse motes travel up the projection as tiny soft light
                // packets. Procedural and screen-space: zero GameObjects.
                float moteLane=floor(uv.x*13.0);
                float moteCycle=floor(t*.55+hash21(float2(moteLane,91.0))*5.0);
                float moteY=frac(t*.18+hash21(float2(moteLane,moteCycle))*1.7);
                float moteX=(moteLane+.5)/13.0+(hash21(float2(moteCycle,moteLane+4.0))-.5)*.055;
                float2 moteDelta=float2((uv.x-moteX)*5.5,uv.y-moteY);
                float mote=exp(-dot(moteDelta,moteDelta)*900.0)*step(.73,hash21(float2(moteLane,moteCycle+31.0)));
                mote*=coneMask*endFade;

                // The dedicated horizontal ring owns the emitter base. An
                // older flare drawn into this vertical quad exposed its
                // rectangular geometry at low camera angles.
                energy += ray*.46+ribbons*.82+mote*.72;
                float colorLuma = dot(_Color.rgb, half3(0.299, 0.587, 0.114));
                half3 vividColor = saturate(lerp(colorLuma.xxx, _Color.rgb, 1.65));
                half3 blueColor=lerp(vividColor,half3(.04,.45,1.0),.28);
                half3 cyanColor=lerp(blueColor,half3(.04,.90,1.0),.26);
                half3 projected = blueColor * energy * _Intensity * 0.31;
                projected += cyanColor*ray*_Intensity*.10;
                projected += lerp(blueColor,cyanColor,.55)*ribbons*_Intensity*.16;
                // Small separated chromatic ghosts read as projector alignment
                // errors without combining into a white central pole.
                float ghostA = exp(-pow((signedX - 0.22 - sin(uv.y * 19.0 + t * 3.0) * 0.05) * 7.0, 2.0));
                float ghostB = exp(-pow((signedX + 0.29 - sin(uv.y * 15.0 - t * 2.4) * 0.06) * 6.5, 2.0));
                projected += vividColor.brg * ghostA * coneMask * dropout * endFade * _Intensity * 0.10 * _GlitchAmount;
                projected += vividColor.gbr * ghostB * coneMask * dropout * endFade * _Intensity * 0.08 * _GlitchAmount;
                return half4(projected, saturate(energy * 0.7));
            }
            ENDHLSL
        }
    }
}
