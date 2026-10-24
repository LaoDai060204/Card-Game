Shader "VFX_Shader/Exclusive/Exodia/fxs_ExodiaFireScroll_TC_001_b" {
	Properties {
		_Main_Alpha ("Main Alpha", Range(0, 1)) = 1
		_Main_Smoothstep ("Main Smoothstep", Range(0, 1)) = 0.2
		_Main_Smoothstep_Bler ("Main Smoothstep Bler", Range(0, 1)) = 0.17
		[HDR] _Emissive ("Emissive", Vector) = (2,2,2,0)
		_Detail_Noise_Scale ("Detail Noise Scale", Float) = 4
		_Detail_Noise_Speed ("Detail Noise Speed", Range(-1, 1)) = 0.2
		_Detail_Noise_Tiling ("Detail Noise Tiling", Vector) = (2,1,0,0)
		_sub_Detail_Noise_Scale ("sub Detail Noise Scale", Float) = 12
		_sub_Detail_Noise_Speed ("sub Detail Noise Speed", Range(-1, 1)) = 0.5
		_sub_Detail_Noise_Tiling ("sub Detail Noise Tiling", Vector) = (2,1,0,0)
		[HDR] _Color_01 ("Color 01", Vector) = (0.1882353,1,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (1,0,0,0)
		_Color_Smoothstep ("Color Smoothstep", Range(0, 1)) = 0
		_Color_Smoothstep_Bler ("Color Smoothstep Bler", Range(0, 1)) = 0.75
		_Color_Noise_Speed ("Color Noise Speed", Range(-1, 1)) = 0.3
		_Color_Noise_Tiling ("Color Noise Tiling", Vector) = (2,1,0,0)
		_sub_Color_Noise_Speed ("sub Color Noise Speed", Range(-1, 1)) = 0.1
		_sub_Color_Noise_Tiling ("sub Color Noise Tiling", Vector) = (2,1,0,0)
		[NoScaleOffset] _MASK_Texture ("MASK Texture", 2D) = "white" {}
		_MASK_Smooth ("MASK Smooth", Range(-1, 1)) = 0
		_MASK_Smooth_Bler ("MASK Smooth Bler", Range(-1, 1)) = 1
		_MASK_Direction ("MASK Direction", Float) = 0
		[ToggleUI] _use_sub_MASK_Texture ("use sub MASK Texture", Float) = 0
		[NoScaleOffset] _sub_MASK_Texture ("sub MASK Texture", 2D) = "white" {}
		_sub_MASK_Channel_Selection ("sub MASK Channel Selection", Vector) = (1,0,0,0)
		_sub_MASK_Tiling ("sub MASK Tiling", Vector) = (1,1,0,0)
		_sub_MASK_Speed ("sub MASK Speed", Range(-1, 1)) = 0.1
		_UVDef_Tiling ("UVDef Tiling", Vector) = (2,1,0,0)
		_UVDef_Main_Speed ("UVDef Main Speed", Range(-1, 1)) = 0.1
		_UVDef_sub_Speed ("UVDef sub Speed", Range(0, 1)) = 0.1
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