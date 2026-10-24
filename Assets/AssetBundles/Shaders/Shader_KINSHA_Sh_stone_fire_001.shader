Shader "Shader_KINSHA/Sh_stone_fire_001" {
	Properties {
		[NoScaleOffset] Texture2D_62A7A083 ("Tex_stone_01", 2D) = "white" {}
		[HDR] Color_E0DFA115 ("Stone color", Vector) = (0.4811321,0.355251,0.2746084,0)
		[NoScaleOffset] Texture2D_20BF0003 ("Tex_stoneAO", 2D) = "white" {}
		[HDR] Color_3F2E21EE ("Shadow color", Vector) = (0.3207547,0.2490388,0.2012282,0)
		Vector1_7086328F ("Shadow Range", Range(0.5, 2)) = 1.5
		Vector1_6636FCC0 ("Shadow Fuzziness", Range(0.1, 1)) = 0.4
		[NoScaleOffset] Texture2D_3F5E030B ("Tex_stone_emi_01", 2D) = "white" {}
		[HDR] Color_B5320166 ("Emission_Color", Vector) = (7.247059,4.705883,3.670588,0)
		Vector1_62BAB55F ("Emission_Range", Range(0.5, 2)) = 0.5
		Vector1_DBCFD2AA ("Emission_Fuzziness", Range(0.1, 3)) = 1.3
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