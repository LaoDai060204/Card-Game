Shader "VFX_Shader/Grave/fxs_GraveSoul_TC_001_b" {
	Properties {
		_Direction ("Direction", Float) = 0
		_Noise_Amount ("Noise Amount", Float) = 5
		_Noise_Tiling_X ("Noise Tiling X", Float) = 0.5
		_Noise_Tiling_Y ("Noise Tiling Y", Float) = 1
		_Noise_Speed ("Noise Speed", Range(-1, 1)) = 0.1
		_Width ("Width", Range(0, 0.9)) = 0
		_Width_Squeeze ("Width Squeeze", Range(0, 1)) = 0
		[ToggleUI] _Add_Texture_Switch ("Add Texture Switch", Float) = 0
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Select_channel ("Texture Select channel", Vector) = (1,0,0,0)
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
		[HDR] _truth_Color ("truth Color", Vector) = (0,0,0,0)
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