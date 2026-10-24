Shader "VFX_Shader/Other/CardHighlighVer02" {
	Properties {
		[HDR] _TintColor ("Tint Color", Vector) = (0,0.1019608,1,0.5019608)
		[HDR] _TintColor_1 ("Tint Color01_02", Vector) = (0.6,0.1215686,0,0.5019608)
		[NoScaleOffset] _MainTex ("Main Tex", 2D) = "white" {}
		[NoScaleOffset] _MainTex02 ("MainTex02", 2D) = "white" {}
		[HDR] _TintColor02 ("TintColor02", Vector) = (0.5,0.5,0.5,0.5019608)
		[HDR] _TintColor02_02 ("TintColor02_02", Vector) = (1,0.8567152,0,0.5019608)
		[NoScaleOffset] _MaskTex ("MaskTex", 2D) = "white" {}
		_Uanim ("Uanim", Range(-1, 1)) = 0.3009769
		_Vanim ("Vanim", Range(-1, 1)) = 0.2945887
		_InvFade ("Inv Fade", Range(0.01, 300)) = 1
		_Haze ("Haze", Float) = 1
		_TextureScaleUV ("TextureScaleUV", Vector) = (1,1,0,0)
		_UVRotateColor ("UVRotateColor", Float) = 215
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