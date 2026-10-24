Shader "Shader Graphs/GraveCommon01" {
	Properties {
		[NoScaleOffset] _Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _Texture2DLight ("Texture2DLight", 2D) = "white" {}
		_GraveCardExist ("GraveCardExist", Range(0, 1)) = 1
		_GraveMouseOver ("GraveMouseOver", Range(0, 1)) = 1
		_GravePressButton ("GravePressButton", Range(0, 1)) = 1
		_ExcludeCardExist ("ExcludeCardExist", Range(0, 1)) = 1
		_ExcludeMouseOver ("ExcludeMouseOver", Range(0, 1)) = 1
		_ExcludePressButton ("ExcludePressButton", Range(0, 1)) = 1
		_IntensityGraveCardExist ("IntensityGraveCardExist", Float) = 0.4
		_IntensityGraveMouseOver ("IntensityGraveMouseOver", Float) = 0.1
		_IntensityExcludeCardExist ("IntensityExcludeCardExist", Float) = 0.4
		_IntensityExcludeMouseOver ("IntensityExcludeMouseOver", Float) = 0.1
		_BaseColorExclude ("BaseColorExclude", Vector) = (0.8851676,0,1,0)
		_BaseColorGrave ("BaseColorGrave", Vector) = (0,0.1580373,0.9528302,0)
		_HighlightExclude ("HighlightExclude", Vector) = (0.9494373,0.2877358,1,0)
		_HighlightGrave ("HighlightGrave", Vector) = (0,0.4411116,1,0)
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