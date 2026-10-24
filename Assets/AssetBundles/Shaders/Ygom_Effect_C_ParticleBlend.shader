Shader "Ygom/Effect/C_ParticleBlend" {
    Properties {
        _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _InvFade ("Soft Particles Factor", Range(0.01, 300)) = 1
        [KeywordEnum(Always,NotEqual,Equal,GEqual,LEqual,Greater,Less)] _ZTest ("ZTest", Float) = 4
    }
    
    SubShader {
        Tags {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }
        
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            Cull Off
            Lighting Off
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile _ZTEST_ALWAYS _ZTEST_NOTEQUAL _ZTEST_EQUAL _ZTEST_GEQUAL _ZTEST_LEQUAL _ZTEST_GREATER _ZTEST_LESS SOFTPARTICLES_ON
            
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            fixed4 _TintColor;
            sampler2D_float _CameraDepthTexture;
            float _InvFade;
            
            struct appdata_t {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            
            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                #ifdef SOFTPARTICLES_ON
                float4 projPos : TEXCOORD1;
                #endif
            };
            
            // ZTest 关键字处理
            #if defined(_ZTEST_ALWAYS)
                #define DEPTH_TEST_MODE Always
            #elif defined(_ZTEST_NOTEQUAL)
                #define DEPTH_TEST_MODE NotEqual
            #elif defined(_ZTEST_EQUAL)
                #define DEPTH_TEST_MODE Equal
            #elif defined(_ZTEST_GEQUAL)
                #define DEPTH_TEST_MODE GEqual
            #elif defined(_ZTEST_LEQUAL)
                #define DEPTH_TEST_MODE LEqual
            #elif defined(_ZTEST_GREATER)
                #define DEPTH_TEST_MODE Greater
            #elif defined(_ZTEST_LESS)
                #define DEPTH_TEST_MODE Less
            #else
                #define DEPTH_TEST_MODE LEqual  // 默认值
            #endif
            
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = v.texcoord;
                
                #ifdef SOFTPARTICLES_ON
                o.projPos = ComputeScreenPos(o.vertex);
                COMPUTE_EYEDEPTH(o.projPos.z);
                #endif
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 采样纹理
                fixed4 col = 2.0f * i.color * _TintColor * tex2D(_MainTex, i.texcoord);
                
                // 软粒子处理
                #ifdef SOFTPARTICLES_ON
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                float partZ = i.projPos.z;
                float fade = saturate(_InvFade * (sceneZ - partZ));
                col.a *= fade;
                #endif
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Particles/Alpha Blended"
}