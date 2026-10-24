Shader "VFX_Shader/Dissolve/fxs_TexDissolve_TC_001_b" {
	Properties {
		[HDR] _Emissive ("Emissive", Vector) = (1,1,1,0)
		[HDR] _Color_01 ("Color 01", Vector) = (0,1,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (1,0,0,0)
		_Tiling_X ("Tiling X", Float) = 1
		_Tiling_Y ("Tiling Y", Float) = 1
		_Offset_X ("Offset X", Float) = 0
		_Offset_Y ("Offset Y", Float) = 0
		[NoScaleOffset] _Main_Texture ("Main Texture", 2D) = "white" {}
		_Main_Texture_Channel_Select ("Main Texture Channel Select", Vector) = (1,0,0,0)
		[ToggleUI] _use_Smoothstep ("use Smoothstep", Float) = 0
		_Smoothstep ("Smoothstep", Range(0, 1)) = 0
		_Smoothstep_Bler ("Smoothstep Bler", Range(0, 1)) = 0.5
		[NoScaleOffset] _Mask_Texture ("Mask Texture", 2D) = "white" {}
		_Mask_Texture_Rotate ("Mask Texture Rotate", Float) = 90
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
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