// The lantern flame. Shapes a primitive sphere into a licking teardrop on the GPU and shades
// it hot white-gold at the heart, falling off to orange and then nothing at the rim.
//
// _BaseColor is the outer flame colour and brightness. LanternLight drives it every frame
// through a property block from the fuel level and flicker, so the flame dims and gutters
// with the lantern exactly as before; this shader only adds the motion and the gradient.
Shader "EMBER/LanternFlame"
{
    Properties
    {
        [HDR] _BaseColor ("Flame (driven by LanternLight)", Color) = (4, 1.9, 0.55, 1)
        _CoreHeat ("Core Heat (whiteness)", Range(0, 2)) = 0.6
        _Taper ("Tip Taper", Range(0, 1)) = 0.75
        _Wobble ("Wobble", Range(0, 0.4)) = 0.12
        _Speed ("Lick Speed", Range(0, 20)) = 9
    }

    SubShader
    {
        // Drawn just before the glass, so the panes sit in front of the fire.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-1" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Flame"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _CoreHeat, _Taper, _Wobble, _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewWS     : TEXCOORD1;
                float  height     : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                float3 p = v.positionOS.xyz;              // unit sphere, radius 0.5
                float h = saturate(p.y + 0.5);            // 0 at the base, 1 at the tip
                float t = _Time.y * _Speed;

                // Pinch the top into a point, and let the tip lick about more than the base.
                p.xz *= lerp(1.0, 1.0 - _Taper, h * h);
                float sway = h * h * _Wobble;
                p.x += sin(t + h * 3.1) * sway;
                p.z += cos(t * 0.83 + h * 2.3) * sway * 0.7;
                p.y += sin(t * 1.7) * 0.03 * h;

                Varyings o;
                float3 ws = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceNormalizeViewDir(ws);
                o.height = h;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half heart = pow(facing, 3.0h) * (1.0h - i.height * 0.6h);
                // The heart burns whiter, not just brighter. Adding the same energy to every channel,
                // scaled by the flame's own strength, keeps it dimming with the fuel.
                half3 core = _BaseColor.rgb + _BaseColor.r * _CoreHeat;
                half3 c = lerp(_BaseColor.rgb, core, heart);
                // Soft rim, and a tip that thins to nothing.
                half fade = pow(facing, 1.4h) * (1.0h - smoothstep(0.65h, 1.0h, i.height));
                return half4(c * fade, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
