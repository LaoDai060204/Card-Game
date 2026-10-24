Shader "VFX_Shader/Exclusive/fxs_Target_arrow_001_b" {
	Properties {
		[ToggleUI] _Rollover ("Rollover", Float) = 0
		[HDR] _Selected_Color_01 ("Selected Color 01", Vector) = (0,0.2828427,1.059274,1)
		[HDR] _Selected_Color_02 ("Selected Color 02", Vector) = (0.6637754,1.528214,1.655534,1)
		[HDR] _Default_Color_01 ("Default Color 01", Vector) = (0.2783019,0.6047384,1,1)
		[HDR] _Default_Color_2 ("Default Color 02", Vector) = (0.3066038,0.952507,1,1)
		_Tiling ("Tiling", Float) = 3
		[NoScaleOffset] _Texture2DAsset_b6d1fd99174c608f800b61fcd5471719_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_46a0b6b632b7ad8a9a0dbeab8e0a7fa5_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_4f4a26709ec6ff8d9e69ef02918507d2_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_ba8237ebbd5d078c896d47d3e15b10dc_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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