Shader "VFX_Shader/HIT/fxs_hiteah_DecalBurnt_TC_001_b" {
	Properties {
		[NoScaleOffset] _Main_Texture ("Main Texture", 2D) = "white" {}
		[HDR] _truth_Burn_Out_Color ("truth Burn Out Color", Vector) = (0,0,0,0)
		_Burn_Out_Scal ("Burn Out Scal", Range(0, 5)) = 1
		[NoScaleOffset] _Burn_Out_Texture ("Burn Out Texture", 2D) = "white" {}
		_Bum_Out_Channel ("Bum Out Channel", Vector) = (1,0,0,0)
		[NoScaleOffset] _RGBA_Texture ("RGBA Texture", 2D) = "white" {}
		_Alpha_Shape ("Alpha Shape", Range(0, 1)) = 0
		_Alpha_Shape_02 ("Alpha Shape 02", Range(0, 1)) = 1
		_Alpha_Noise ("Alpha Noise", Float) = 30
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}