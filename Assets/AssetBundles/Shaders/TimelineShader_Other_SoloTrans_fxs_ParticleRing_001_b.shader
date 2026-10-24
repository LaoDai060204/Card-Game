Shader "TimelineShader/Other/SoloTrans_fxs_ParticleRing_001_b" {
	Properties {
		_Animator_Alpha ("Animator Alpha", Range(0, 1)) = 1
		_Radial_Scale ("Radial Scale", Float) = 1
		_BaseBlur ("Base Blur", Range(0.1, 5)) = 1
		_Alpha ("Alpha", Range(0, 3)) = 1
		_Alphapow ("Alpha pow", Range(0.1, 3)) = 1
		[HDR] _Emission ("Emission", Vector) = (1,1,1,1)
		[ToggleUI] _RING_ON ("RING ON", Float) = 0
		_RING_Radial ("RING Radial", Range(-3, 1)) = 0
		_RING_Blur ("RING Blur", Range(-1, 30)) = 5
		[ToggleUI] _Step ("Step", Float) = 0
		[ToggleUI] _Step_Minus ("Step Minus", Float) = 0
		_Step_Slider ("Step Slider", Range(0, 1)) = 0.5
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
		[NoScaleOffset] _MainTex ("_MainTex", 2D) = "white" {}
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}