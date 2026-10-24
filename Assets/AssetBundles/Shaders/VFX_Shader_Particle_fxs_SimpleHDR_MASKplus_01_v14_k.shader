Shader "VFX_Shader/Particle/fxs_SimpleHDR_MASKplus_01_v14_k" {
	Properties {
		_Alpha ("Alpha", Range(0, 1)) = 1
		_CameraOffset ("CameraOffset", Float) = 0
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Rotation ("Rotation", Float) = 0
		_Channel_Selection ("Channel Selection", Vector) = (1,0,0,0)
		_Tiling_Y ("Tiling Y", Float) = 1
		_Tiling_Z ("Tiling Z", Float) = 1
		[HDR] _Color_01 ("Color 01", Vector) = (0,1,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (1,0,0,0)
		_Color_Smoothstep ("Color Smoothstep", Range(0, 1)) = 0
		_Color_SmoothstepBlur ("Color SmoothstepBlur", Range(0, 1)) = 1
		_Alpha_Smoothstep ("Alpha Smoothstep", Range(0, 1)) = 0
		_Alpha_SmoothstepBlur ("Alpha SmoothstepBlur", Range(0, 1)) = 1
		[ToggleUI] _MASK_switch_ON ("MASK switch ON", Float) = 1
		[NoScaleOffset] _MASK ("MASK", 2D) = "white" {}
		_MASK_Rotation ("MASK Rotation", Float) = 0
		_MASK_Channel_Selection ("MASK Channel Selection", Vector) = (1,0,0,0)
		_MASK_Tiling_Y ("MASK Tiling Y", Float) = 2
		_MASK_Tiling_Z ("MASK Tiling Z", Float) = 2
		_MASK_Alpha_Smoothstep ("MASK Alpha Smoothstep", Range(0, 1)) = 0
		_MASK_Alpha_SmoothstepBlur ("MASK Alpha SmoothstepBlur", Range(0, 1)) = 1
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 1
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