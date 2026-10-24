Shader "VFX_Shader/ATK/fxs_atkdve_Trail_01_b" {
	Properties {
		_Animation_Dissolve ("Animation Dissolve", Range(0, 1)) = 0
		_Direction ("Direction", Float) = 90
		[HDR] _Color_01 ("Color 01", Vector) = (0,1,0,1)
		[HDR] _Color_02 ("Color 02", Vector) = (1,0,0,1)
		_use_Ramp_Color_Offset ("use Ramp Color Offset", Range(-1, 1)) = 0
		_use_Ramp_Color_Strength ("use Ramp Color Strength", Range(0, 5)) = 1
		_Distortion_Offset ("Distortion Offset", Float) = 0.5
		_Distortion ("Distortion", Range(0, 2)) = 1
		_Distortion_Speed ("Distortion Speed", Range(0, 10)) = -0.8
		_Distortion_Scroll ("Distortion Scroll", Float) = 1.5
		_Main_Scroll_Speed ("Main Scroll Speed", Float) = 1
		[NoScaleOffset] _Texture_Trail ("Texture Trail", 2D) = "white" {}
		_Alpha_Smoothstep ("Alpha Smoothstep", Range(0, 1)) = 0.1
		_Alpha_Smoothstep_Blur ("Alpha Smoothstep Blur", Range(0, 1)) = 0.3
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