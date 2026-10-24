Shader "VFX_Shader/HIT/fxs_hiteah_ImpactWave_TC_001_b" {
	Properties {
		[HDR] _Sub_Color ("Sub Color", Vector) = (1,0,0,0)
		[HDR] _truth_Color_01 ("truth Color 01", Vector) = (0,0,0,0)
		[HDR] _truth_Color_02 ("truth Color 02", Vector) = (0,0,0,0)
		_Color_Direction ("Color Direction", Float) = 180
		_Color_Direction_Strength ("Color Direction Strength", Range(0, 5)) = 1
		_Main_Noise ("Main Noise", Vector) = (0,0,1,0)
		_Main_Noise_Tilling_X ("Main Noise Tilling X", Float) = 3
		_Main_Noise_Tilling_Y ("Main Noise Tilling Y", Float) = 0.1
		_Sub_Noise ("Sub Noise", Vector) = (1,1,1,0)
		_Sub_Noise_Tilling_X ("Sub Noise Tilling_X", Float) = 2
		_Sub_Noise_Tilling_Y ("Sub Noise Tilling_Y", Float) = 0.74
		_Sub_Gradient_Strength ("Sub Gradient Strength", Range(0, 5)) = 2
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
		[NoScaleOffset] Texture2D_bfe6c9ea9c4c4ce4956e0d2e3f551883 ("fxt_nis_010", 2D) = "white" {}
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