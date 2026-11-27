Shader "MDPro3/BattleSpirits/Field"
{
    Properties
    {
        _BaseColor ("Field color", Color) = (0.025, 0.055, 0.085, 1)
        _Detail ("Graphite table weave", Range(0,1)) = 0
        _Silver ("Silver inlay sheen", Range(0,1)) = 0
        _Backdrop ("Radial Spirit stage", Range(0,1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        Pass
        {
            Name "BattleFieldForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Detail;
                half _Silver;
                half _Backdrop;
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
                float2 tablePosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.tablePosition = TransformObjectToWorld(input.positionOS.xyz).xz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float radius = length(input.uv - .5) * 1.42;
                half3 graphite = lerp(half3(.112, .131, .149), half3(.020, .027, .037), saturate(radius));
                half weave = pow(max(0, sin((input.uv.x + input.uv.y * .4) * 390)), 38) * .008;
                half fineThreads = pow(max(0, sin(input.uv.y * 940 + sin(input.uv.x * 80) * .7)), 70) * .008;
                half sheen = .72h + .28h * sin(dot(input.tablePosition, float2(.22, .31)) + .8);
                half3 baseColor = _BaseColor.rgb * lerp(1.0h, sheen, _Silver);
                baseColor *= lerp(1.0h, .12h + exp(-radius * radius * 3.5), _Backdrop);
                return half4(lerp(baseColor, graphite + weave + fineThreads, _Detail), 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
