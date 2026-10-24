Shader "Shader Graphs/Timer_c001_02" {
	Properties {
		_MaxTime ("MaxTime", Range(0, 1)) = 1
		_AddTime ("AddTime", Range(0, 1)) = 1
		_Active ("Active", Range(0, 1)) = 1
		_MaxTimeColor01 ("MaxTimeColor01", Vector) = (0.2117647,0.6422562,1,0)
		_MaxTimeColor02 ("MaxTimeColor02", Vector) = (0,0.4096916,1,0)
		_AddTimeColorBar01 ("AddTimeColor01", Vector) = (1,0.807896,0,0)
		_AddTimeColor02 ("AddTimeColor02", Vector) = (1,0.8039216,0,0)
		_SwitchAddToMax ("SwitchAddToMax", Range(0, 1)) = 1
		_BarBaseColor ("BarBaseColor", Vector) = (0.1960784,0.1960784,0.1960784,0)
		_BarTickness ("BarTickness", Range(0, 1)) = 0.8
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