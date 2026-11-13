Shader "LaoDai/BattleSpirits/Field"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Glow ("Glow", Range(0, 1)) = 0
        [HideInInspector] _StageOrigin ("Stage origin", Vector) = (0, 0, 0, 0)
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
                float4 _StageOrigin;
                half _Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 key = normalize(half3(-0.4h, 0.85h, -0.3h));
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half shade = 0.30h + 0.70h * saturate(dot(normal, key));
                // Broad lighting falloff gives the open platform depth without
                // painting textures over the cards or allocating light maps.
                float2 relative = (input.positionWS.xz - _StageOrigin.xz) * float2(0.052, 0.064);
                half pool = 0.88h + 0.20h * exp2(-dot(relative, relative));
                half specular = pow(saturate(dot(normal, SafeNormalize(key + view))), 48.0h) * 0.045h;
                half rim = pow(1.0h - saturate(dot(normal, view)), 3.0h) * 0.025h;
                half3 color = _BaseColor.rgb * lerp(shade * pool, 1.6h, saturate(_Glow));
                color += half3(0.66h, 0.81h, 0.94h) * (specular + rim) * (1.0h - saturate(_Glow));
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
