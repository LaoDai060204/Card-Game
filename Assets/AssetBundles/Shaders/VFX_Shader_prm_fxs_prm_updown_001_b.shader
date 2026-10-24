Shader "VFX_Shader/prm/fxs_prm_updown_001_b" {
	Properties {
		_Color_Gradient ("Color Gradient", Range(0, 5)) = 0.9
		_Main_Noise_Tilling_X ("Main_Noise_Tilling_X", Float) = 7
		_Main_Noise_Tilling_Y ("Main_Noise_Tilling_Y", Float) = 1
		_Main_Alpha_Speed ("Main_Alpha_Speed", Range(-8, 8)) = 1
		_Sub_Noise_Tilling_X ("Sub_Noise_Tilling_X", Float) = 2
		_Sub_Noise_Tilling_Y ("Sub_Noise_Tilling_Y", Float) = 1
		Vector1_F338CC62 ("Sub_Alpha_Speed", Range(-8, 8)) = 2
		[HDR] _truth_Main_Color ("truth Main Color", Vector) = (0,0,0,0)
		[HDR] _truth_Sub_Color ("truth Sub Color", Vector) = (0,0,0,0)
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