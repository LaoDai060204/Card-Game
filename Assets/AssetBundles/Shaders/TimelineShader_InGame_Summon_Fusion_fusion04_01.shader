Shader "TimelineShader/InGame/Summon/Fusion/fusion04_01" {
	Properties {
		_ColorA ("ColorA", Vector) = (1,0.5,0.5,0.5019608)
		[NoScaleOffset] _CardFrameA ("CardFrameA", 2D) = "black" {}
		_ColorB ("ColorB", Vector) = (0.5507813,0.5,1,0.5019608)
		[NoScaleOffset] _CardFrameB ("CardFrameB", 2D) = "black" {}
		_ColorC ("ColorC", Vector) = (0.5019608,1,0.5638883,0.5019608)
		[NoScaleOffset] _CardFrameC ("CardFrameC", 2D) = "white" {}
		_ColorD ("ColorD", Vector) = (0.7000312,0.7264151,0.1987362,0.5019608)
		[NoScaleOffset] _CardFrameD ("CardFrameD", 2D) = "white" {}
		_Scale ("Scale", Range(1, 10)) = 1
		_Rotate ("Rotate", Float) = 0
		_TwistStrength ("TwistStrength", Float) = 0
		[NoScaleOffset] _RoundedRectangleCard ("RoundedRectangleCard", 2D) = "white" {}
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