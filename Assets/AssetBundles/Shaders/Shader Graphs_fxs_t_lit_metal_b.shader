Shader "Shader Graphs/fxs_t_lit_metal_b" {
	Properties {
		_BaseColor ("BaseColor", Vector) = (1,1,1,1)
		[NoScaleOffset] _BaseMap ("BaseMap", 2D) = "white" {}
		_BaseMapTilingOffset ("BaseMapTilingOffset", Vector) = (1,1,0,0)
		_BumpMapTilingOffset ("BumpMapTilingOffset", Vector) = (1,1,0,0)
		_LightAmp ("LightAmp", Float) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		[NoScaleOffset] [Normal] _BumpMap ("BumpMap", 2D) = "bump" {}
		_MainNormalPower ("MainNormalPower", Float) = 1
		_SpeculerColor ("SpeculerColor", Vector) = (0.5647059,0.5647059,0.5647059,1)
		_FakeAmbColor ("FakeAmbColor", Vector) = (0.1490196,0.2941177,0.2666667,1)
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