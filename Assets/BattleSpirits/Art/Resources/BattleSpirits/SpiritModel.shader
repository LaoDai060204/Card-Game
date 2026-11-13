Shader "LaoDai/BattleSpirits/SpiritModel"
{
    Properties
    {
        _BaseColor ("Model tint", Color) = (1, 1, 1, 1)
        _Opacity ("Summon opacity", Range(0, 1)) = 1
        _Glow ("Action glow", Range(0, 1)) = 0
        _Smoothness ("Highlight smoothness", Range(0, 1)) = 0.52
        _Metallic ("Armor highlight", Range(0, 1)) = 0.18
        _RimColor ("Cool edge light", Color) = (0.22, 0.48, 0.68, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "SpiritForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _RimColor;
                half _Opacity, _Glow, _Smoothness, _Metallic;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half height : TEXCOORD2;
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
                output.color = input.color;
                output.height = input.positionOS.y;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Opaque dithering retains depth and does not produce the
                // sorting artifacts of translucent overlapping skinned limbs.
                float2 pixel = floor(input.positionCS.xy);
                half coverage = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
                clip(_Opacity - 0.0001h);
                if (_Opacity < 0.999h) clip(saturate(_Opacity) - coverage);
                half3 normal = normalize(input.normalWS);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light main = GetMainLight();
                half hasKey = step(0.001h, dot(main.color, half3(1, 1, 1)));
                half3 key = SafeNormalize(lerp(half3(-0.42h, 0.82h, -0.4h), main.direction, hasKey));
                half3 keyColor = lerp(half3(1.0h, 0.96h, 0.90h), main.color * 0.78h, hasKey);
                half ndl = dot(normal, key);
                // A softened cel transition keeps eyes, fur and armor readable
                // from the table camera while preserving their real volume.
                half diffuse = 0.22h + 0.78h * smoothstep(-0.24h, 0.86h, ndl);
                half fill = saturate(dot(normal, normalize(half3(0.68h, 0.28h, 0.65h)))) * 0.17h;
                half sky = saturate(normal.y * 0.5h + 0.5h);
                half3 ambient = lerp(half3(0.105h, 0.12h, 0.15h), half3(0.22h, 0.26h, 0.31h), sky);
                half contact = lerp(0.87h, 1.0h, saturate(input.height * 0.65h));
                half3 baseColor = input.color.rgb * _BaseColor.rgb;
                half3 color = baseColor * (ambient + keyColor * diffuse + half3(0.30h, 0.56h, 0.83h) * fill) * contact;
                half exponent = lerp(20.0h, 92.0h, saturate(_Smoothness));
                half specular = pow(saturate(dot(normal, SafeNormalize(key + view))), exponent) * saturate(ndl);
                half3 specColor = lerp(half3(0.55h, 0.60h, 0.66h), baseColor, saturate(_Metallic));
                color += specColor * specular * lerp(0.12h, 0.42h, saturate(_Metallic));
                half rim = pow(1.0h - saturate(dot(normal, view)), 3.1h);
                color += _RimColor.rgb * rim * (0.18h + sky * 0.10h);
                color += baseColor * saturate(_Glow) * 0.32h;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
