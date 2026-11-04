Shader "LaoDai/BattleSpirits/Field"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Glow ("Glow", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half shade = 0.42h + 0.58h * saturate(dot(normalize(input.normalWS), normalize(half3(-0.4h, 0.85h, -0.3h))));
                return half4(_BaseColor.rgb * lerp(shade, 1.6h, _Glow), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
