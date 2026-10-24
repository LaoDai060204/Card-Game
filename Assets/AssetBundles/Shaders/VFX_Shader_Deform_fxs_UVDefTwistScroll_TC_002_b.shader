Shader "VFX_Shader/Deform/fxs_UVDefTwistScroll_TC_002_b" {
	Properties {
		_Animator_Alpha ("Animator Alpha", Range(0, 1)) = 1
		[ToggleUI] _TEXCOORD_All_SmoothSte ("TEXCOORD All SmoothSte", Float) = 0
		_Twist ("Twist", Float) = 20
		_Spherize ("Spherize", Float) = 20
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Select_Channel ("Texture Select Channel", Vector) = (1,0,0,0)
		_Smooth_Edge01 ("Smooth Edge01", Range(0, 1)) = 0
		_Smooth_Edge02 ("Smooth Edge02", Range(0, 1)) = 1
		_Scroll_Speed_X ("Scroll Speed X", Float) = -0.1
		_Scroll_Speed_Y ("Scroll Speed Y", Float) = 0
		_Tiling_X ("Tiling X", Float) = 0.5
		_Tiling_Y ("Tiling Y", Float) = 1
		_OUT_MASK_Scale ("OUT MASK Scale", Range(0, 1)) = 0.5
		_OUT_MASK_Blur ("OUT MASK Blur", Range(0.1, 1)) = 0.5
		_IN_SIDE_MASK_Scale ("IN SIDE MASK Scale", Range(0, 1)) = 0.2003246
		_IN_SIDE_MASK_Blur ("IN SIDE MASK Blur", Range(0.1, 1)) = 0.3670776
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
		[HDR] _truth_Color_01 ("truth Color 01", Vector) = (0,0,0,0)
		[HDR] _truth_Color_02 ("truth Color 02", Vector) = (0,0,0,0)
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