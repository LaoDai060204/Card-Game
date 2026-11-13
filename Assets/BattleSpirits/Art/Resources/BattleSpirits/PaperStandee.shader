Shader "LaoDai/BattleSpirits/PaperStandee"
{
    Properties
    {
        _MainTex ("Transparent character artwork", 2D) = "white" {}
        _BaseColor ("Paper or base tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Silhouette alpha cutoff", Range(0.01, 0.8)) = 0.12
        _Opacity ("Presentation opacity", Range(0, 1)) = 1
        _Glow ("Action edge glow", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "PaperStandeeForward"
            Tags { "LightMode"="UniversalForward" }
            // The mesh supplies separately wound front/back artwork faces and
            // physical side walls. Transparent pixels must not write depth.
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cutoff, _Opacity, _Glow;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half opacity = saturate(_Opacity * _BaseColor.a);
                clip(opacity - 0.0001h);
                // Endpoint-safe dither fades the complete physical standee
                // without changing its alpha-test/depth-order contract.
                if (opacity < 0.999h)
                {
                    float2 pixel = floor(input.positionCS.xy);
                    half coverage = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                    clip(opacity - coverage);
                }
                // COLOR.a is a mesh part flag, not artwork transparency:
                // one on the paper faces, zero on side walls and the oval base.
                if (input.color.a > 0.5h)
                {
                    half4 artwork = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    clip(artwork.a - _Cutoff);
                    // Preserve the original printed illustration rather than
                    // shading/recoloring the character like a sculpted model.
                    return half4(artwork.rgb * input.color.rgb * _BaseColor.rgb, 1);
                }
                half3 normal = normalize(input.normalWS);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light main = GetMainLight();
                half hasKey = step(0.001h, dot(main.color, half3(1, 1, 1)));
                half3 key = SafeNormalize(lerp(half3(-0.42h, 0.82h, -0.40h), main.direction, hasKey));
                half3 keyColor = lerp(half3(1.0h, 0.96h, 0.92h), main.color * 0.74h, hasKey);
                half diffuse = 0.30h + 0.70h * saturate(dot(normal, key));
                half sky = saturate(normal.y * 0.5h + 0.5h);
                half3 ambient = lerp(half3(0.10h, 0.13h, 0.17h), half3(0.20h, 0.25h, 0.30h), sky);
                half3 baseColor = input.color.rgb * _BaseColor.rgb;
                half3 color = baseColor * (ambient + keyColor * diffuse);
                half specular = pow(saturate(dot(normal, SafeNormalize(key + view))), 48.0h)
                    * saturate(dot(normal, key)) * 0.14h;
                half rim = pow(1.0h - saturate(dot(normal, view)), 3.0h);
                color += half3(0.55h, 0.72h, 0.89h) * (specular + rim * 0.07h);
                color += baseColor * saturate(_Glow) * 0.25h;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
