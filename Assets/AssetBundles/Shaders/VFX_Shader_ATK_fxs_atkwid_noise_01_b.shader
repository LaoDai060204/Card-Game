Shader "VFX_Shader/ATK/fxs_atkwid_noise_01_b" {
	Properties {
		_Direction ("Direction", Float) = 90
		[HDR] _Color_01 ("Color 01", Vector) = (1,0.3636364,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (1,0,0.009901047,0)
		_Offset_X ("Offset X", Float) = 0
		_Offset_Y ("Offset Y", Float) = 0
		_Scroll ("Scroll", Float) = 0.3
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Channel ("Texture Channel", Vector) = (1,0,0,0)
		_Texture_Tiling ("Texture Tiling", Vector) = (1,1,0,0)
		_Color_Texture_Scroll ("Color Texture Scroll", Float) = 1
		[NoScaleOffset] _MASK_Texture ("MASK Texture", 2D) = "white" {}
		_MASK_Rotate ("MASK Rotate", Float) = 0
		_Alpha_Smoothstep_Blur ("Alpha Smoothstep Blur", Range(0, 1)) = 0.5
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 1
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 0
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