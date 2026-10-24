Shader "VFX_Shader/HIT/fxs_hitfie_impfire_TC_001_b" {
	Properties {
		[HDR] _Color_01 ("Color 01", Vector) = (1,0,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (2.670157,2.670157,0,0)
		[NoScaleOffset] _Main_Texture ("Main Texture", 2D) = "white" {}
		_Main_Texture_Channel ("Main Texture Channel", Vector) = (0,1,0,0)
		_Main_Tiling_X ("Main Tiling X", Float) = 1
		_Main_Tiling_Y ("Main Tiling Y", Float) = 0.5
		_Main_Scroll_Y ("Main Scroll Y", Range(-1, 1)) = 0.1
		[NoScaleOffset] _MASK_Texure ("MASK Texure", 2D) = "white" {}
		_MASK_Tiling_X ("MASK Tiling X", Float) = 1
		_MASK_Tiling_Y ("MASK Tiling Y", Float) = 0.5
		_MASK_Offset_Y ("MASK Offset Y", Float) = -0.5
		Vector1_7c9e21a7096140a5a33b906bc78da4cc ("Float (1)", Float) = -0.5
		_MASK_Smooth ("MASK Smooth", Range(0, 1)) = 0
		_MASK_SmoothBlur ("MASK SmoothBlur", Range(0, 1)) = 1
		[NoScaleOffset] _Texture2DAsset_9252fd92ad23462a979f11bfaf8c0d13_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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