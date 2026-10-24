Shader "VFX_Shader/Mask/fxs_MASKNoiseScroll_TC_01_v14_k" {
	Properties {
		_Animation_Alpha ("Animation Alpha", Float) = 1
		_Camera_Offset ("Camera Offset", Float) = 0
		[HDR] _Emissive_truth ("Emissive truth", Vector) = (0.4716981,0.2291741,0.2291741,0)
		[NoScaleOffset] _Main_Texture ("Main_Texture", 2D) = "white" {}
		_Main_Texture_Channel_Select ("Main_Texture Channel Select", Vector) = (1,0,0,0)
		_Main_Tile_X ("Main Tile X", Float) = 1
		_Main_Tile_Y ("Main Tile Y", Float) = 1
		_Offset_X ("Offset X", Float) = 0
		_Offset_Y ("Offset Y", Float) = 0
		_Main_Noise_Scroll_X ("Main_Noise Scroll X", Range(-1, 1)) = 0
		_Main_Noise_Scroll_Y ("Main_Noise Scroll Y", Range(-1, 1)) = 0
		[ToggleUI] _Textur_Switch ("Textur_Switch -ON,  others  OFF-", Float) = 0
		[ToggleUI] _Simple_Noise ("Simple Noise", Float) = 1
		[ToggleUI] _Gradient_Noise ("Gradient Noise", Float) = 0
		[ToggleUI] _Voronoi ("Voronoi", Float) = 0
		_MAIN_Noise_Amount ("Main_Noise Amount", Float) = 20
		[ToggleUI] _Main_Smoothstep_Switch ("Main_Smoothstep_Switch", Float) = 0
		_Main_Edge1 ("Main_Edge1", Float) = 0
		_Main_Edge2 ("Main_Edge2", Float) = 1
		_Main_Thickness_white ("Main_Thickness white", Range(0, 1)) = 0
		_Main_Thickness_black ("Main_Thickness black", Range(0, 1)) = 1
		[NoScaleOffset] _MASK_Texture ("MASK Texture", 2D) = "white" {}
		_Mask_Channel_Select ("Mask Channel Select", Vector) = (1,0,0,0)
		[ToggleUI] _MASK_Smoothstep_Switch ("MASK Smoothstep_Switch", Float) = 0
		_Edge1 ("Edge1", Float) = 0
		_Edge2 ("Edge2", Float) = 1
		_MASK_Texture_Rotation ("MASK Texture Rotation", Float) = 0
		[ToggleUI] _ADD_Noise_Switch ("ADD Noise Switch -This is ON One is ON-", Float) = 1
		[ToggleUI] _ADD_Simple_Noise ("ADD Simple Noise", Float) = 0
		[ToggleUI] _ADD_Gradient_Noise ("ADD Gradient Noise", Float) = 1
		[ToggleUI] _ADD_Voronoi ("ADD Voronoi", Float) = 0
		_MASK_Add_Noise_Amount ("MASK Add Noise Amount", Float) = 3
		_MASK_Add_Noise_Thickness_white ("MASK Add Noise Thickness white", Range(0, 1)) = 0
		_MASK_Add_Noise_Thickness_black ("MASK Add Noise Thickness black", Range(0, 1)) = 1
		_MASK_Add_Noise_Tile_X ("MASK Add Noise Tile X", Float) = 1
		_MASK_Add_Noise_Tile_Y ("MASK Add Noise Tile Y", Float) = 1
		_MASK_Add_Noise_Scroll_X ("MASK Add Noise Scroll X", Range(-1, 1)) = 0
		_MASK_Add_Noise_Scroll_Y ("MASK Add Noise Scroll Y", Range(-1, 1)) = 0
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
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