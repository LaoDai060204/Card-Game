Shader "MDPro3/BattleSpirits/Surface"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Printed card or transparent character", 2D) = "white" {}
        _BaseColor ("Artwork tint", Color) = (1, 1, 1, 1)
        _UvRect ("Visible image UV: left, bottom, width, height", Vector) = (0, 0, 1, 1)
        _Opacity ("Presentation opacity", Range(0, 1)) = 1
        _Accent ("Presentation accent", Color) = (0.35, 0.75, 1, 1)
        _Glow ("Silhouette edge glow", Range(0, 1)) = 0
        _Reveal ("Bottom to top reveal", Range(0, 1)) = 1
        _Motion ("Scanline motion", Range(0, 1)) = 0
        _Clock ("Local presentation clock", Float) = 0
        _Cutoff ("Empty texel alpha cutoff", Range(0.001, 0.5)) = 0.025
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }
        Pass
        {
            Name "BattleSurfaceForward"
            Tags { "LightMode" = "UniversalForward" }
            // Empty pixels never write depth. The surviving card / silhouette
            // writes depth so it can sit among other cards without transparent
            // object sorting reversing the front and back players.
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _UvRect;
                half4 _BaseColor;
                half4 _Accent;
                half _Opacity;
                half _Glow;
                half _Reveal;
                half _Motion;
                float _Clock;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half ReadAlpha(float2 uv)
            {
                // Do not sample a neighboring part of the uncropped source at
                // the metadata bounds; the original PNG remains untouched.
                float2 inside = step(_UvRect.xy, uv)
                    * step(uv, _UvRect.xy + _UvRect.zw);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a
                    * inside.x * inside.y;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = _UvRect.xy + saturate(input.uv) * _UvRect.zw;
                half4 art = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half reveal = 1.0h;
                if (_Reveal < 0.999h)
                    reveal = 1.0h - smoothstep(_Reveal - 0.02h,
                        _Reveal + 0.02h, input.uv.y);

                half outline = 0.0h;
                if (_Glow > 0.001h)
                {
                    float2 texel = abs(_MainTex_TexelSize.xy) * 1.5;
                    half neighbor = max(max(ReadAlpha(uv + float2(texel.x, 0)),
                        ReadAlpha(uv - float2(texel.x, 0))),
                        max(ReadAlpha(uv + float2(0, texel.y)),
                        ReadAlpha(uv - float2(0, texel.y))));
                    outline = saturate(neighbor - art.a) * _Glow * 0.45h;
                }
                half alpha = max(art.a, outline) * reveal
                    * saturate(_Opacity * _BaseColor.a);
                clip(alpha - _Cutoff);

                // Unity's sRGB texture sampling supplies linear artwork colors.
                // No relighting or recoloring is applied to the printed face.
                half3 rgb = art.rgb * _BaseColor.rgb;
                half scan = 1.0h - saturate(_Motion) * 0.025h
                    * (0.5h + 0.5h * sin(input.uv.y * 340.0h - _Clock * 4.0));
                rgb *= scan;
                half frontier = _Reveal < 0.999h
                    ? exp(-abs(input.uv.y - _Reveal) * 80.0h) : 0.0h;
                rgb += _Accent.rgb * frontier * _Glow * art.a * 0.45h;
                rgb = lerp(_Accent.rgb, rgb, smoothstep(0.01h, 0.20h, art.a));
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
