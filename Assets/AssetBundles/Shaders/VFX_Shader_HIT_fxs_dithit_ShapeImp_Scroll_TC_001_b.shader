Shader "VFX_Shader/HIT/fxs_dithit_ShapeImp_Scroll_TC_001_b" {
	Properties {
		[HDR] _truth_Color_01 ("truth Color 01", Vector) = (1.414214,0.101968,0.08885112,1)
		[HDR] _truth_Color_02 ("truth Color 02", Vector) = (1.231144,0.7863854,0.7863854,0)
		_Direction ("Direction", Float) = 0
		_Color_Gradation ("Color Gradation", Range(0, 1)) = 0.5
		_Color_Smooth ("Color Smooth", Float) = 0.11
		_Color_Step ("Color Step", Float) = 1.02
		_Noise_Tillng_X ("Noise Tillng X", Float) = 1
		_Noise_Tillng_Y ("Noise Tillng Y", Float) = 3
		_Noise_Scroll_Speed ("Noise Scroll Speed", Range(0, 1)) = 1
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