Shader "VFX_Shader/fxs_ImpactWave_TC_002_b" {
	Properties {
		_Direction ("Direction", Float) = 0
		[HDR] _Color_01 ("Color 01", Vector) = (0.4852338,0,1,0)
		[HDR] _Color_02 ("Color 02", Vector) = (0,0,0,0)
		_Main_Noise_Amount ("Main Noise Amount", Range(0, 30)) = 5
		_Main_Noise_Tilling_X ("Main Noise Tilling X", Float) = 2
		_Main_Noise_Tilling_Y ("Main Noise Tilling Y", Float) = 0.2
		_Sub_Noise_Amount ("Sub Noise Amount", Range(0, 30)) = 5
		_Sub_Noise_Tilling_X ("Sub Noise Tilling X", Float) = 1
		_Sub_Noise_Tilling_Y ("Sub Noise Tilling Y", Float) = 0.5
		_ADD_MASK_Noise_Amount ("ADD MASK Noise Amount", Range(0, 30)) = 30
		_ADD_MASK_Noise_X ("ADD MASK Noise X", Float) = 1
		_ADD_MASK_Noise_Y ("ADD MASK Noise Y", Float) = 1
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