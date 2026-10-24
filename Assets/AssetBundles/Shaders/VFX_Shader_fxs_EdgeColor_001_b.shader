Shader "VFX_Shader/fxs_EdgeColor_001_b" {
	Properties {
		_Camera_Offset ("Camera Offset", Float) = 0
		[HDR] _Emissive ("Emissive", Vector) = (2,2,2,0)
		[HDR] _Color_01 ("Color 01", Vector) = (2,0,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (0,0,0,0)
		[NoScaleOffset] _Main_Texture ("Main Texture", 2D) = "white" {}
		_Main_Texture_Rotation ("Main Texture Rotation", Float) = 0
		_Main_Texture_Tiling_X ("Main Texture Tiling X", Float) = 1
		_Main_Texture_Tiling_Y ("Main Texture Tiling Y", Float) = 1
		_Main_Texture_Channel ("Main Texture Channel", Vector) = (1,0,0,0)
		_Main_Smoothstep ("Main Smoothstep", Range(-1, 1)) = 0.5
		_Main_Smoothstep_Blur ("Main Smoothstep Blur", Range(-1, 1)) = 0.5
		[NoScaleOffset] _MASK_Texture ("MASK Texture", 2D) = "white" {}
		_MASK_Texture_Rotation ("MASK Texture Rotation", Float) = 0
		_MASK_Texture_Tiling_X ("MASK Texture Tiling X", Float) = 1
		_MASK_Texture_Tiling_Y ("MASK Texture Tiling Y", Float) = 1
		_MASK_Texture_Channel ("MASK Texture Channel", Vector) = (1,0,0,0)
		_MASK_Smoothstep ("MASK Smoothstep", Range(-1, 1)) = 0
		_MASK_Smoothstep_Blur ("MASK Smoothstep Blur", Range(-1, 1)) = 0
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