// A glyph that burns: the Om on the survivor's locket. Additive and HDR, so bloom turns it
// into firelight rather than a printed decal. The pulse runs on the GPU; LocketPendant only
// sets how fast and how deep it breathes, through a property block, as the game state changes.
Shader "EMBER/Glyph"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Glyph (alpha)", 2D) = "white" {}
        [HDR] _GlowColor ("Glow", Color) = (3.2, 1.4, 0.4, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _PulseRate ("Pulse Rate (Hz)", Range(0, 4)) = 0.35
        _PulseDepth ("Pulse Depth", Range(0, 1)) = 0.5
        _Flicker ("Ember Flicker", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Glyph"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _Intensity, _PulseRate, _PulseDepth, _Flicker;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                float t = _Time.y;
                // Slow breath, plus a faint uneven ember flicker so it never looks like a timer.
                half breath = 1.0h - _PulseDepth * (0.5h - 0.5h * cos(6.2831853 * _PulseRate * t));
                half ember  = 1.0h - _Flicker * (0.5h + 0.5h * sin(t * 13.7 + sin(t * 5.3) * 2.0));
                half3 c = _GlowColor.rgb * (_Intensity * breath * ember);
                return half4(c, a * saturate(_Intensity * 2.0h));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
