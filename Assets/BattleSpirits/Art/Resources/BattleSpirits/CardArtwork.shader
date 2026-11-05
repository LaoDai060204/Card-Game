Shader "LaoDai/BattleSpirits/CardArtwork"
{
    Properties
    {
        _MainTex ("Official card texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Accent ("Effect color", Color) = (0.1,0.8,1,1)
        _UvRect ("Illustration region", Vector) = (0,0,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Glow ("Glow", Range(0,1)) = 0
        _Feather ("Soft artwork edge", Range(0,0.2)) = 0
        _Ring ("Summon halo", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _Accent;
                float4 _UvRect;
                half _Opacity, _Glow, _Feather, _Ring;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, _UvRect.xy + input.uv * _UvRect.zw);
                half alpha = tex.a * _BaseColor.a * _Opacity;
                if (_Feather > 0)
                {
                    float2 edge = min(input.uv, 1 - input.uv);
                    alpha *= smoothstep(0, _Feather, min(edge.x, edge.y));
                }
                if (_Ring > 0.5h)
                {
                    float radius = length(input.uv - 0.5) * 2;
                    alpha *= smoothstep(0.70, 0.83, radius) * (1 - smoothstep(0.87, 1.0, radius));
                }
                clip(alpha - 0.002h);
                return half4(tex.rgb * _BaseColor.rgb + _Accent.rgb * _Glow * 0.25h, alpha);
            }
            ENDHLSL
        }
    }
}
