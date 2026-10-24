Shader "Shader_KINSHA/Sh_smoke_005_re" {
	Properties {
		_Noise_Speed ("Noise Speed", Range(-1, 1)) = 1
		_Noise_Amount ("Noise Amount", Float) = 3
		_Noise_Edge01 ("Noise Edge01", Range(0, 1)) = 0.2
		_Noise_Edge02 ("Noise Edge02", Range(0, 1)) = 0.8
		_Noise_Width ("Noise Width", Range(0, 1)) = 1
		_Noise_Width_Soft ("Noise Width Soft", Float) = 0.2
		_Line_Width ("Line Width", Range(0, 1)) = 0.5
		_Line_Width_Soft ("Line Width Soft", Float) = 0.5
		_Sub_Noise_Speed ("Sub Noise Speed", Range(-1, 1)) = 1
		_Sub_Noise_Amount ("Sub Noise Amount", Float) = 2
		_Sub_Noise_Edge01 ("Sub Noise Edge01", Range(0, 1)) = 0
		_Sub_Noise_Edge02 ("Sub Noise Edge02", Range(0, 1)) = 1
		_Sub_Line_Width ("Sub Line Width", Range(0, 1)) = 1
		_Sub_Line_Width_Soft ("Sub Line Width Soft", Float) = 0.8
		[HDR] _truth_Color_01 ("truth Color 01", Vector) = (0,0,0,0)
		[HDR] _truth_Color_02 ("truth Color 02", Vector) = (0,0,0,0)
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