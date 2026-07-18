Shader "K1L0/BeamItemGlitch"
{
    Properties { _MainTex("Item", 2D)="black"{} _MaskTex("Mask",2D)="black"{} _HasMask("Has Mask",Float)=0 _DebugSolid("Debug Solid",Float)=0 _TimeOffset("Time",Float)=0 _GlitchAmount("Glitch Amount",Range(0,1))=0 }
    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        ZTest Always
        Cull Off

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _MaskTex;
        float _TimeOffset, _HasMask, _DebugSolid, _GlitchAmount;
        struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
        struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
        struct ItemSample { fixed3 rgb; float alpha; float shape; float glitchGate; };
        v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }

        ItemSample sampleItem(float2 sourceUV)
        {
            ItemSample o;
            float t=_TimeOffset;
            float cycle=fmod(t,3.2);
            o.glitchGate=smoothstep(2.12,2.20,cycle)*(1.0-smoothstep(2.98,3.12,cycle))*_GlitchAmount;
            float band=floor(sourceUV.y*40.0+t*9.0);
            float burst=step(.62,hash(float2(floor(t*10.0),band)))*o.glitchGate;
            float shift=(hash(float2(band,floor(t*15.0)))-.5)*.075*burst;
            float wave=(sin(sourceUV.y*19.0+t*4.2)*.010 + sin(sourceUV.y*43.0-t*7.0)*.004)*o.glitchGate;
            float verticalTear=(hash(float2(band,floor(t*11.0)))-.5)*.010*burst;
            float2 uv=sourceUV+float2(shift+wave,verticalTear);
            fixed4 c=tex2D(_MainTex,uv);
            fixed mask=tex2D(_MaskTex,uv).r;
            float luma=dot(c.rgb,float3(.299,.587,.114));
            float maskedAlpha=smoothstep(.04,.22,mask);
            float fallbackAlpha=smoothstep(.035,.18,luma);
            o.alpha=lerp(fallbackAlpha,maskedAlpha,step(.5,_HasMask))*c.a;
            fixed r=tex2D(_MainTex,uv+float2(.008*burst,0)).r;
            fixed b=tex2D(_MainTex,uv-float2(.008*burst,0)).b;
            float scan=lerp(1.0,.86+.14*sin(sourceUV.y*900.0+t*18.0),o.glitchGate);
            o.rgb=fixed3(r,c.g,b)*scan;
            // Only use the mask as an emissive shape when a real mask exists.
            o.shape=lerp(fallbackAlpha,maskedAlpha,step(.5,_HasMask));
            return o;
        }
        ENDCG

        // A conventional alpha-composited image supplies the dark values that
        // additive blending cannot show against daylight and bright horizons.
        Pass
        {
            Name "READABLE_BASE"
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBase
            fixed4 fragBase(v2f i):SV_Target
            {
                if (_DebugSolid > .5) return fixed4(.05,1.0,.85,.92);
                ItemSample item=sampleItem(i.uv);
                fixed3 rgb=saturate((item.rgb-.20)*1.25+.20);
                float luminance=dot(rgb,float3(.299,.587,.114));
                rgb=lerp(luminance.xxx,rgb,1.28);
                return fixed4(rgb,item.alpha*.94);
            }
            ENDCG
        }

        // A restrained additive second exposure supplies the cyan holographic
        // bloom and glitch highlights without erasing the underlying artwork.
        Pass
        {
            Name "HOLOGRAM_GLOW"
            Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlow
            fixed4 fragGlow(v2f i):SV_Target
            {
                if (_DebugSolid > .5) return fixed4(0,0,0,0);
                ItemSample item=sampleItem(i.uv);
                float highlight=smoothstep(.45,.92,max(item.rgb.r,max(item.rgb.g,item.rgb.b)));
                float glowAlpha=item.alpha*(.07+.16*item.shape+.18*highlight+.18*item.glitchGate);
                fixed3 glow=item.rgb*.42+fixed3(.03,.38,1.0)*(.34+.28*item.shape);
                return fixed4(glow,glowAlpha);
            }
            ENDCG
        }
    }
}
