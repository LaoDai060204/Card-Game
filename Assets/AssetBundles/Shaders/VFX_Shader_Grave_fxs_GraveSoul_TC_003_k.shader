Shader "VFX_Shader/Grave/fxs_GraveSoul_TC_003_k" {
	Properties {
		_Direction ("Direction", Float) = 0
		[HDR] _Color ("Color", Vector) = (1,1,1,0)
		[ToggleUI] _Texture_Clamp_Switch ("Texture Clamp Switch", Float) = 1
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Select_channel ("Texture Select Channel", Vector) = (0,0,1,0)
		[ToggleUI] _ADD_Noise_Switch ("ADD Noise Switch", Float) = 1
		_add_noise_Amount ("add noise Amount", Float) = 3
		_add_noise_Speed ("add noise Speed", Range(0, 1)) = 0.2
		_add_noise_Opacity ("add noise Opacity", Range(0, 1)) = 0.5
		_add_noise_Tiling_X ("add noise Tiling X", Float) = 1
		_add_noise_Tiling_Y ("add noise Tiling Y", Float) = 1
		[ToggleUI] _ADD_MASK_Switch ("ADD MASK Switch", Float) = 0
		[NoScaleOffset] _MASK ("MASK", 2D) = "white" {}
		_MASK_Rotate ("MASK Rotate", Float) = 0
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 0
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

			float4 _Color;

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _Color; // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}