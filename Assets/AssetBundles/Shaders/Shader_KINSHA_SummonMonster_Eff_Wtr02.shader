Shader "Shader_KINSHA/SummonMonster_Eff_Wtr02" {
	Properties {
		_Rotate ("Rotate", Float) = 90
		_MainCircle_Pow ("MainCircle_Pow", Float) = 3
		_MainAlpha_TillingXY ("MainAlpha_TillingXY", Vector) = (1.3,2.5,0,0)
		_MainAlpha_Speed ("MainAlpha_Speed", Range(-2, 2)) = -1.5
		_SubAlpha_TillingXY ("SubAlpha_TillingXY", Vector) = (3,1,0,0)
		_SubAlpha_Speed ("SubAlpha_Speed", Range(-2, 2)) = -0.5
		_Blend ("Blend", Float) = 0.98
		_STEP ("STEP", Float) = 0.06
		_SmoothSTEP01 ("SmoothSTEP01", Range(-1, 2)) = 0.32
		_SmoothSTEP02 ("SmoothSTEP02", Range(-1, 2)) = 0.4025974
		_AlphaCut_TillingXY ("AlphaCut_TillingXY", Vector) = (0.8,1,0,0)
		_AlphaCut_Speed ("AlphaCut_Speed", Vector) = (1,0,0,0)
		[HDR] _truth_ColorMain ("truth ColorMain", Vector) = (0,0,0,0)
		[HDR] _truth_ColorSub ("truth ColorSub", Vector) = (0,0,0,0)
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