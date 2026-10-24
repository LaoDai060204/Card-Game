Shader "VFX_Shader/ATK/fxs_atkeah_Trail_02_b" {
	Properties {
		_Animation_Dissolve ("Animation Dissolve", Range(0, 1)) = 0
		_Direction ("Direction", Float) = 90
		[HDR] _Color_01 ("Color 01", Vector) = (0,1,0,1)
		_Distortion ("Distortion", Range(0, 2)) = 0.5
		_Distortion_Speed ("Distortion Speed", Range(0, 10)) = 0.5
		_Distortion_Scroll ("Distortion Scroll", Float) = 1
		_Main_Wave ("Main Wave", Range(0, 1)) = 0
		_Main_Wave_Speed ("Main Wave Speed", Float) = 8
		_Main_Wave_Frequency ("Main Wave Frequency", Float) = 12
		_Main_Scroll_Speed ("Main Scroll Speed", Float) = 1
		_Main_Wave_Scale ("Main Wave Scale", Range(1, 10)) = 1
		[NoScaleOffset] _Texture_Trail ("Texture Trail", 2D) = "white" {}
		_Alpha_Smoothstep ("Alpha Smoothstep", Range(0, 1)) = 0
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