Shader "Shader Graphs/Sh_fire_002" {
	Properties {
		[HDR] Color_C973B5B8 ("HDR", Vector) = (1,1,1,1)
		Vector1_EBC77AE7 ("Base_Noise_Volume", Range(0, 50)) = 30
		Vector2_528401C ("UV_Tilng", Vector) = (1,1,0,0)
		[NoScaleOffset] Texture2D_A9829A90 ("Alpha_Type_Texture", 2D) = "white" {}
		[ToggleUI] Boolean_4B29550C ("Dissovle_Noise_or_Voronoi", Float) = 1
		Vector1_C4CD9419 ("Voronoi_Offset", Range(0, 10)) = 2
		Vector1_24A3DBAA ("Voronoi_cell_Volume", Range(0, 10)) = 6.5
		Vector1_BC3F624D ("Alpha_Heigtht", Range(0, 2)) = 1.3
		[ToggleUI] Boolean_45567505 ("Real_or_Anime", Float) = 1
		Vector1_B04196E1 ("Anime_Volume", Range(0.1, 1)) = 0.1
		Vector1_1CC5939A ("Noise_Volume", Range(0, 50)) = 3
		Vector1_3BBA30BE ("Scloor_Speed  (+,-)", Float) = 0.5
		[ToggleUI] Boolean_C262563E ("Real_or_Anime(Noise)", Float) = 1
		Vector1_217E5E7E ("Anime_Volume(Noise", Range(0.1, 1)) = 0.3
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