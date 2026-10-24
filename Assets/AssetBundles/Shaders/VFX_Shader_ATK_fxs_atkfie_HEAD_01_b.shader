Shader "VFX_Shader/ATK/fxs_atkfie_HEAD_01_b" {
	Properties {
		_Direction ("Direction", Float) = 0
		[HDR] _Color_01 ("Color 01", Vector) = (1,0.3636364,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (1,0,0.009901047,0)
		_use_Ramp_Color_Offset ("use Ramp Color Offset", Range(-1, 1)) = 0
		_use_Ramp_Color_Strength ("use Ramp Color Strength", Range(0, 5)) = 1
		_Offset ("Offset", Float) = 0
		_Scroll ("Scroll", Float) = 0.3
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Tiling ("Texture Tiling", Vector) = (1,1,0,0)
		[NoScaleOffset] _MASK_Texture ("MASK Texture", 2D) = "white" {}
		_MASK_Rotate ("MASK Rotate", Float) = 0
		_Alpha_Smoothstep_Blur ("Alpha Smoothstep Blur", Range(0, 1)) = 0.5
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