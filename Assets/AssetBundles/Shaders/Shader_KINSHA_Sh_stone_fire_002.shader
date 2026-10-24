Shader "Shader_KINSHA/Sh_stone_fire_002" {
	Properties {
		[NoScaleOffset] Texture2D_F28EE6B ("Base_Texture", 2D) = "white" {}
		Color_47B5F14 ("Base_Color (1)", Vector) = (0.5943396,0.4675785,0.4065059,0)
		[HDR] Color_4333C622 ("Noise_Color", Vector) = (2.439048,0.946649,0.4486928,0)
		Vector1_78F5C9D1 ("Noise_Volume", Range(0, 50)) = 18
		Vector1_9707E49 ("Noise_Sub", Range(0, 1)) = 0.6
		Vector1_B295B522 ("Noise_add", Range(0, 1)) = 0.13
		Vector1_A2563442 ("Noise_Range", Range(0, 1)) = 0.8165582
		Vector1_CD9BB81D ("Noise_Fuzziness", Range(0, 1)) = 1
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