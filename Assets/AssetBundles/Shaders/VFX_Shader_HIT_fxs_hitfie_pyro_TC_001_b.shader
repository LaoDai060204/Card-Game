Shader "VFX_Shader/HIT/fxs_hitfie_pyro_TC_001_b" {
	Properties {
		[HDR] _Color_01 ("Color 01", Vector) = (1,0,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (0.003632545,1,0,0)
		[HDR] _Texture_Color ("Texture Color", Vector) = (0.8509804,0.6078432,0,0)
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Tilling ("Texture Tilling", Vector) = (1,1,0,0)
		[NoScaleOffset] _Alpha_Texture ("Alpha Texture", 2D) = "white" {}
		_Alpha_Tilling ("Alpha Tilling", Vector) = (1,1,0,0)
		_Alpha_Speed ("Alpha Speed", Range(-3, 3)) = 0.1
		[NoScaleOffset] _Texture2DAsset_a4494807a9c04f71b6e76d52978fb420_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_f4805a4f32764b429a4ff0f39f8e49a2_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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