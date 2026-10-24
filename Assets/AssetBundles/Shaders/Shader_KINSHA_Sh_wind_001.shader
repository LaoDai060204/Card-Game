Shader "Shader_KINSHA/Sh_wind_001" {
	Properties {
		[ToggleUI] Boolean_49AC333E ("Bokeh or Sharp", Float) = 1
		Vector1_A8043260 ("Bokeh_Power", Range(-1, 5)) = 1
		Vector4_77B28860 ("Color_Noise_Pattern", Vector) = (1,0,0,0)
		Vector1_6EDA6455 ("Noise_Volime", Range(0, 1)) = 0.6
		Vector1_7FFA2A66 ("Power_Volume", Range(0, 3)) = 1
		[NoScaleOffset] Texture2D_14448C21 ("Dissolve_Alpha_Tex", 2D) = "white" {}
		Vector1_7A1E64EE ("Dissolve_Volume", Range(0, 1)) = 0.2
		Vector4_1B3759FA ("Dissolve_Noise_Switch", Vector) = (0,1,0,0)
		Vector2_17046502 ("Dissolve_Noise_Tiling", Vector) = (2.5,0.5,0,0)
		Vector2_E9347964 ("Dissovle_Rotate&Speed", Vector) = (0,-0.23,0,0)
		[HDR] _truth_HDR ("truth HDR", Vector) = (0,0,0,0)
		[HDR] _truth_Color ("truth Color", Vector) = (0,0,0,0)
		[HDR] _truth_Color_Sub ("truth Color_Sub", Vector) = (0,0,0,0)
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