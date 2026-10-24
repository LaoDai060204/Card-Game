Shader "VFX_Shader/Particle/fxs_ParticleStar_001_v14_k" {
	Properties {
		_Alpha ("Alpha", Float) = 1
		_Edge_Sharp ("Edge Sharp", Range(1, 10)) = 1
		[HDR] _truth_Emissive ("truth Emissive", Vector) = (0.1989529,2,0.2410559,0)
		_Main_Shape ("Main Shape", Range(0, 1)) = 0.1
		_Sharpness ("Sharpness", Range(0, 1)) = 0.19
		_Sharpness_Blur ("Sharpness Blur", Range(0, 1)) = 0.28
		_Glow_Sharpness ("Glow Sharpness", Range(0, 1)) = 0
		_Glow_Sharpness_Blur ("Glow Sharpness Blur", Range(0, 1)) = 1
		_Glow_Intensity ("Glow Intensity", Range(0, 1)) = 1
		[ToggleUI] _Sub ("Sub", Float) = 1
		_Sub_Angle ("Sub Angle", Float) = 45
		_Sub_Shape ("Sub Shape", Range(0, 1)) = 0.1
		_Sub_Sharpness ("Sub Sharpness", Range(0, 1)) = 0
		_Sub_Sharpness_Blur ("Sub Sharpness Blur", Range(0, 1)) = 1
		[ToggleUI] _Third ("Third", Float) = 1
		_Third_Angle ("Third Angle", Float) = 60
		_Third_Shape ("Third Shape", Range(0, 1)) = 0.1
		_Third_Sharpness ("Third  Sharpness", Range(0, 1)) = 0
		_Third_Sharpness_Blur ("Third  Sharpness Blur", Range(0, 1)) = 1
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 0
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