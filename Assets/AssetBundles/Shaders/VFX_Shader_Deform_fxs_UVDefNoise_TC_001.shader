Shader "VFX_Shader/Deform/fxs_UVDefNoise_TC_001" {
	Properties {
		_UV_Rotate ("UV Rotate", Float) = 0
		_Distortion_Rotate ("Distortion Rotate", Float) = 0
		_Distortion_Scale ("Distortion Scale", Range(0, 1)) = 0.2
		_Distortion_Noise_Amount ("Distortion Noise Amount", Range(0, 10)) = 5
		_Distortion_Scroll_X ("Distortion Scroll X", Float) = 0.1
		_Distortion_Scroll_Y ("Distortion Scroll Y", Float) = 0.1
		_Color_Value ("Color Value", Range(-1, 1)) = 0
		_Color_Step ("Color Step", Range(0, 1)) = 0.5
		_Color_Noise_Amount ("Color Noise Amount", Float) = 5
		_Color_Noise_Scroll_X ("Color Noise Scroll X", Float) = 0.05
		_Color_Noise_Scroll_Y ("Color Noise Scroll Y", Float) = 0.05
		_Voronoi_Amount ("Voronoi Amount", Float) = 2.6
		_Voronoi_Speed ("Voronoi Speed", Float) = 2
		_MASK_Level ("MASK Level", Range(0, 1)) = 0
		_MASK_Level_Step ("MASK Level Step", Range(0, 1)) = 0.5
		_ALL_MASK_Size ("ALL MASK Size", Range(0, 2)) = 1
		_ALL_MASK_Step ("ALL MASK Step", Range(0, 1)) = 0.8
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