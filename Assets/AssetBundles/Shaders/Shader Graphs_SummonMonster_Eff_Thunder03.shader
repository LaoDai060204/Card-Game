Shader "Shader Graphs/SummonMonster_Eff_Thunder03" {
	Properties {
		_Alpha_Smoothstep_Blur ("Alpha Smoothstep Blur", Range(0, 1)) = 0.5
		[HDR] _Color_01 ("Color 01", Vector) = (0,0.1049969,1,0)
		[HDR] _Color_02 ("Color 02", Vector) = (0,0.7030306,1,0)
		_Color_Rate ("Color Rate", Range(0, 1)) = 1
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Rotation ("Rotation", Float) = 0
		_Tiling_X ("Tiling X", Float) = 1
		_Tiling_Y ("Tiling Y", Float) = 1
		_Texture_Cgannel_Selection ("Texture Cgannel Selection", Vector) = (1,1,0,0)
		[ToggleUI] _use_Smoothstep ("use Smoothstep", Float) = 0
		_Texture_Smoothstep ("Texture Smoothstep", Range(0, 1)) = 0
		_Texture_Smoothstep_Blur ("Texture Smoothstep Blur", Range(0, 1)) = 1
		_Scroll_X ("Scroll X", Range(-1, 1)) = 0
		_Scroll_Y ("Scroll Y", Range(-1, 1)) = 0
		[NoScaleOffset] _MASK ("MASK", 2D) = "white" {}
		_MASK_Cgannel_Selection ("MASK Cgannel Selection", Vector) = (1,0,0,0)
		_MASK_Rotation ("MASK Rotation", Float) = 0
		_MASK_Smoothstep ("MASK Smoothstep", Range(0, 1)) = 0
		_MASK_Smoothstep_Blur ("MASK Smoothstep Blur", Range(0, 1)) = 1
		[HDR] _Color_03 ("Color 03", Vector) = (0.254902,0.1764706,0,0)
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