Shader "VFX_Shader/Break/DestructionImpactA01" {
	Properties {
		[HDR] _Color01 ("Color 01", Vector) = (1,0.3636364,0,0)
		_Alpha_Noise_Tilling ("Alpha Noise Tilling", Vector) = (5,1,0,0)
		_AnimationSpeedV ("AnimationVSpeed", Range(-5, 5)) = -1
		[NoScaleOffset] _Alpha_Texture ("Alpha Texture", 2D) = "white" {}
		_Channel_Select ("Channel Select", Vector) = (1,0,0,0)
		_AlphaVScroolSpeed ("Alpha V Scrool Speed", Range(-3, 3)) = -0.1
		_Alpha_Strength ("Alpha Strength", Float) = 1
		_Sub_Alpha_Stremgth ("Sub Alpha Stremgth", Range(-2, 2)) = 0
		[NoScaleOffset] _MASK ("MASK", 2D) = "white" {}
		_MASK_Rotation ("MASK Rotation", Float) = 0
		_Rotation ("RotationUV", Float) = 0
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