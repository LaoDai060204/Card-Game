Shader "Shader Graphs/PlayableGuide_c001_change" {
	Properties {
		_Opacity ("Opacity", Range(0, 1)) = 1
		_ColorP1 ("ColorP1", Vector) = (0,0.5272732,1,0)
		_ColorP2 ("ColorP2", Vector) = (1,0,0.01776171,0)
		[NoScaleOffset] _Texture2D ("Texture", 2D) = "white" {}
		_Opacity02 ("Opacity02", Float) = 0
		_Move01 ("Move01", Float) = 0
		_Move02 ("Move02", Float) = 0
		_Scale ("Scale", Float) = 1
		[ToggleUI] _Switch ("Switch", Float) = 1
		[NoScaleOffset] _SampleTexture2D_72db3480b1bdea8583e6b7743c79a377_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _SampleTexture2D_861272eb7bffee86adb165d5f444d6c7_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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