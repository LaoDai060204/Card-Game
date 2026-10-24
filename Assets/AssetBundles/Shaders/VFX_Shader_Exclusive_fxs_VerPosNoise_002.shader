Shader "VFX_Shader/Exclusive/fxs_VerPosNoise_002" {
	Properties {
		_Line_Alpha ("Line Alpha", Float) = 1
		_Line_Number ("Line Number", Float) = 4
		_Line_Width ("Line Width", Range(0, 3)) = 0.2
		_Position_Strength ("Position Strength", Range(0, 2)) = 1
		_Main_UV_Rot ("Main UV Rot", Range(-100, 100)) = 10
		_Main_Noise_Rot ("Main Noise Rot", Range(-100, 100)) = 5
		_Frequency ("Frequency", Float) = 1
		_Deform_Scale ("Deform Scale", Float) = 3
		[HDR] _truth_Emissive ("truth Emissive", Vector) = (1,1,1,0)
		[HDR] _Color_01_v1 ("Color 01 v1", Vector) = (0,0.2588235,1.498039,0)
		[HDR] _Color_01_v2 ("Color 01 v2", Vector) = (2,0,2,0)
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