Shader "Shader_KINSHA/Sh_needle_002_re" {
	Properties {
		_Needle_Rotation ("Needle_Rotation", Range(-1, 1)) = -0.3
		_Needle_Volume ("Needle_Volume", Range(0, 200)) = 20
		_Needle_Scale ("Needle Scale", Float) = 0.8
		_Sub_Needle_Rotation ("Sub_Needle_Rotation", Range(-0.2, 1)) = 0.4
		_Sub_Needle_Volume ("Sub_Needle_Volume", Range(0, 200)) = 50
		_Sub_Needle_Scale ("Sub Needle Scale", Float) = 0.5
		_Bounce_01 ("Bounce 01", Range(0, 10)) = 6
		_Bounce_02 ("Bounce 02", Range(0, 10)) = 5
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