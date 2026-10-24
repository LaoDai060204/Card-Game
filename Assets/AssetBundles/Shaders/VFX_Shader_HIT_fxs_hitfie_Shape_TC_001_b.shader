Shader "VFX_Shader/HIT/fxs_hitfie_Shape_TC_001_b" {
	Properties {
		[ToggleUI] _Color_Style ("Color Style", Float) = 0
		_Color_Style_Rotale ("Color Style Rotale", Float) = 0
		[HDR] _Color01 ("Color 01", Vector) = (1,0.3803922,0,0)
		[HDR] _Color02 ("Color 02", Vector) = (1,0,0,0)
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		[HDR] _truth_Emissive ("truth Emissive", Vector) = (0,0,0,0)
		_Texture_Strength ("Texture Strength", Float) = 0.5
		_Texture_Tilling ("Texture Tilling", Vector) = (1,1,0,0)
		_Texture_Channel_Select ("Texture Channel Select", Vector) = (1,0,0,0)
		[NoScaleOffset] _Shape_Texture ("Shape Texture", 2D) = "white" {}
		_Shape_Rotation ("Shape Rotation", Float) = 0
		_Shape_Channel_Select ("Shape Channel Select", Vector) = (1,0,0,0)
		_Shape_Add_Noise_Size ("Shape Add Noise Size", Float) = 30
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