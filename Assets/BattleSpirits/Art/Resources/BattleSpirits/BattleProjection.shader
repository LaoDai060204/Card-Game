Shader "MDPro3/BattleSpirits/Projection"
{
    Properties
    {
        _Accent ("Projection color", Color) = (0.5, 0.75, 1, 1)
        _Kind ("Ring 0, beam 1, shimmer 2, motes 3", Float) = 0
        _Clock ("Local presentation clock", Float) = 0
        _Ready ("Projection visibility", Range(0,1)) = 1
        _Motion ("Motion enabled", Range(0,1)) = 1
        _Pulse ("Summon pulse", Range(0,1)) = 1
        _Height ("Mote height", Float) = 2.2
        _Radius ("Mote radius", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "BattleProjectionForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Accent;
                float _Kind, _Clock, _Ready, _Motion, _Pulse, _Height, _Radius;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 seed : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fade : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float Gaussian(float value) { return exp(-value * value); }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p = input.positionOS.xyz;
                output.fade = 1;
                if (_Kind > 2.5)
                {
                    float life = frac(input.seed.x + _Clock * (.085 + input.seed.y * .07));
                    float angle = input.seed.z * 6.2831853 + _Clock * (.07 + input.seed.y * .06);
                    float radius = sqrt(input.seed.w) * _Radius;
                    p += float3(cos(angle) * radius, .065 + life * _Height, sin(angle) * radius);
                    output.fade = sin(life * 3.14159265) * (.4 + .6 * input.seed.y);
                }
                output.positionCS = TransformObjectToHClip(p);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = (input.uv - .5) * 2;
                half alpha;
                if (_Kind < .5)
                {
                    float radius = length(p);
                    float angle = atan2(p.y, p.x);
                    float outer = Gaussian((radius - .78) * 110);
                    float soft = Gaussian((radius - .78) * 17) * .18;
                    float inner = Gaussian((radius - .59) * 120) * .25;
                    float tick = pow(max(0, cos(angle * 32 + _Clock * .16 * _Motion)), 22);
                    tick *= smoothstep(.65, .69, radius) * (1 - smoothstep(.72, .74, radius)) * .46;
                    float pulse = Gaussian((radius - (.12 + _Pulse * .85)) * 40);
                    alpha = (outer + soft + inner + tick) * .21;
                    alpha += pulse * (1 - _Pulse) * step(.001, _Pulse) * _Motion * .25;
                }
                else if (_Kind < 1.5)
                    alpha = pow(1 - input.uv.y, 3.5) * smoothstep(0, .06, input.uv.y)
                        * (.82 + .18 * sin(input.uv.y * 44 - _Clock * 2.2)) * .055 * _Motion;
                else if (_Kind < 2.5)
                {
                    float diagonal = input.uv.x * .65 + input.uv.y * .35;
                    alpha = Gaussian((diagonal - frac(_Clock * .12) * 1.8 + .4) * 12) * .035 * _Motion;
                }
                else
                    alpha = exp(-dot(p,p) * 4.8) * (1 - smoothstep(.6, 1, length(p))) * input.fade * .34 * _Motion;
                alpha *= _Ready * _Accent.a;
                clip(alpha - .002);
                return half4(_Accent.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
