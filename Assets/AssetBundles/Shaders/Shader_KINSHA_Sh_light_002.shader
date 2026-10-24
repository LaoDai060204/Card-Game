Shader "Shader_KINSHA/Sh_light_002" {
	Properties {
		[HDR] Color_520B0D13 ("HDR", Vector) = (1,1,1,1)
		[ToggleUI] Boolean_5BF90668 ("Polygon_or_Nothing", Float) = 1
		[ToggleUI] Boolean_6FF13DA7 ("Noise_or_Nothing", Float) = 1
		Vector1_289AE7BE ("Polygon_Sides", Range(0, 50)) = 6.2
		Vector2_CD0A6898 ("Polygon_w&h", Vector) = (0.9,0.2,0,0)
		Vector1_C368439A ("Vanish_Rotate", Range(-2, 0.7)) = -0.7
		Vector1_EF69481B ("Vanish_Noise", Range(0, 100)) = 30
		Vector2_35E5E5C0 ("Dissolve_Lifetime_Min&Max", Vector) = (0,0.4,0,0)
		Vector1_DF5B16B9 ("Dissolve_Rotate_Speed01", Range(-2, 0.5)) = -0.5
		Vector1_71AFA4F8 ("Dissolve_Noise01", Range(0, 100)) = 15
		Vector1_69F596AF ("Dissolve_Rotate_Speed02", Range(-2, 0.7)) = -0.7
		Vector1_54EB1EA2 ("Dissolve_Noise02", Range(0, 100)) = 30
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