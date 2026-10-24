Shader "VFX_Shader/Deform/fxs_UVDeformation_TC_001_b" {
	Properties {
		[HDR] _Emissive ("Emissive", Vector) = (1,1,1,0)
		_Twirl ("Twirl", Range(-30, 30)) = 5
		_Spherize ("Spherize", Range(-100, 100)) = 0
		_Color ("Color", Vector) = (1,0,0,0)
		_Color_Scal ("Color Scal", Range(0, 10)) = 10
		[ToggleUI] _Noise_Tex_Switch ("Noise Tex Switch", Float) = 0
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Tillng_X_1 ("Tillng X", Float) = 1
		_Tillng_X ("Tillng Y", Float) = 1
		_Texture_Channel_Select ("Texture Channel Select", Vector) = (1,0,0,0)
		_Noise_Select ("Noise Select", Vector) = (1,0,0,0)
		_Noise_Amount ("Noise Amount", Float) = 3
		_Main_Scale ("Main Scale", Range(1, 5)) = 1
		_Main_Blur ("Main Blur", Range(0, 1)) = 1
		_Main_Opacity ("Main Opacity", Range(0, 10)) = 1
		[ToggleUI] _Center ("Center", Float) = 0
		Vector1_1E5CBC21 ("Center Size", Range(-0.5, 0.5)) = 0.21
		Vector1_2C6311EA ("Center Blur", Range(0, 1)) = 0
		_Alpha_Scal ("Alpha Scal", Range(0, 10)) = 4.168014
		_Alpha_Pow ("Alpha Pow", Range(0, 10)) = 2.632148
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
		Tags { "RenderType"="Opaque" }
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

			float4 _Color;

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _Color; // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}