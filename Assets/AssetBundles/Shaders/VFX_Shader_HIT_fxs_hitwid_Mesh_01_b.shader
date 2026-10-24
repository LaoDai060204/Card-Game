Shader "VFX_Shader/HIT/fxs_hitwid_Mesh_01_b" {
	Properties {
		_Direction ("Direction", Float) = 0
		[HDR] _Color_01 ("Color 01", Vector) = (0,0,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (0.2392157,0.1137255,0.8313726,0)
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Texture_Tiling ("Texture Tiling", Vector) = (1,1,0,0)
		_Texture_Offset ("Texture Offset", Vector) = (0,0,0,0)
		_Scroll_Speed ("Scroll Speed", Range(-1, 1)) = -0.3
		_Step_Alpha_Blur ("Step Alpha Blur", Range(0, 1)) = 0.1
		[NoScaleOffset] _Sub_Texture ("Sub Texture", 2D) = "white" {}
		_Sub_Texture_Tiling ("Sub Texture Tiling", Vector) = (1,1,0,0)
		_Sub_Texture_Offset ("Sub Texture Offset", Vector) = (0,0,0,0)
		_Sub_Scroll_Speed ("Sub Scroll Speed", Range(-1, 1)) = -0.5
		[HDR] _Sub_Color ("Sub Color", Vector) = (0.5411765,0.1372549,1,0)
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