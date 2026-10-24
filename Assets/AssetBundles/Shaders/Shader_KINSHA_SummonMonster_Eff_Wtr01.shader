Shader "Shader_KINSHA/SummonMonster_Eff_Wtr01" {
	Properties {
		[HDR] _ColorMain ("ColorMain", Vector) = (0.09019608,0.372549,0.6588235,1)
		[HDR] _ColorSub ("ColorSub", Vector) = (0.6196079,0.7294118,0.7372549,1)
		[HDR] _ColorTEX ("ColorTEX", Vector) = (0.2915101,0.902741,1.796079,1)
		[NoScaleOffset] _ColorSub_TEX ("ColorSub_TEX", 2D) = "white" {}
		_ColorSub_TillingXY ("ColorSub_TillingXY", Vector) = (1,2.5,0,0)
		_ColorSub_Rotate ("ColorSub_Rotate", Float) = 0.24
		_ColorSub_Speed ("ColorSub_Speed", Float) = 2
		[NoScaleOffset] _AlphaGradient ("AlphaGradient", 2D) = "white" {}
		_GradientNoise_Scale ("GradientNoise_Scale", Float) = 20
		_Voronoi_Scale ("Voronoi_Scale", Float) = 15
		_NiseTillingXY ("NiseTillingXY", Vector) = (0.8,0.5,0,0)
		_AlphaWave_Speed ("AlphaWave_Speed", Float) = 3
		_MainSpeed ("MainSpeed", Float) = 1
		[NoScaleOffset] _RGBchannel_TEX ("RGBchannel_TEX", 2D) = "white" {}
		_RGBchannela ("RGBchannela", Vector) = (1,0,0,0)
		_AlphaCut_TillingXY ("AlphaCut_TillingXY", Vector) = (0.3,1,0,0)
		[ToggleUI] _SpeedXY_Reverse ("SpeedXY_Reverse", Float) = 0
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