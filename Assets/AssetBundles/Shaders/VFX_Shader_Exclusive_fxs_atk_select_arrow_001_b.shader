Shader "VFX_Shader/Exclusive/fxs_atk_select_arrow_001_b" {
	Properties {
		[ToggleUI] _Rollover ("Rollover", Float) = 0
		[HDR] _truth_Default_Color_01 ("truth Default Color 01", Vector) = (0,0.6039216,0.01568628,0)
		[HDR] _truth_Default_Color_02 ("truth Default Color 02", Vector) = (0.1568628,1,0.05098039,1)
		[HDR] _truth_Selected_Color_01 ("truth Selected Color 01", Vector) = (0.01765593,0.02752538,1.247688,0)
		[HDR] _truth_Selected_Color_02 ("truth Selected Color 02", Vector) = (0.3853689,3.175104,3.552095,1)
		[NoScaleOffset] _Texture2DAsset_5b426b3b88fc4e3c873ed973f68902bd_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_32775df679384275b23b5efed70b243e_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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