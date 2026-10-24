Shader "VFX_Shader/fxs_FlareRing_001_b" {
	Properties {
		[HDR] _Emissive ("Emissive", Vector) = (1,1,1,0)
		_Rainbow_Scale ("Rainbow Scale", Range(0, 1)) = 0.5
		_Rainbow_Blur ("Rainbow Blur", Range(0, 1)) = 0.5
		_Rainbow_Add_Whit ("Rainbow Add Whit", Vector) = (0,0,0,0)
		[ToggleUI] _use_Rainbow ("_use Rainbow", Float) = 1
		[ToggleUI] _rainbow_invert ("_rainbow invert", Float) = 0
		[HDR] _Color_01 ("Color 01", Vector) = (0,0,1,1)
		[HDR] _Color_02 ("Color 02", Vector) = (1,1,0,1)
		_Color_Step ("Color Step", Range(0, 1)) = 1
		_Color_Step_Blur ("Color Step Blur", Range(0, 1)) = 0.5
		_Flare_Rotation ("Flare Rotation", Float) = 0
		_Flare_Scale ("Flare Scale", Range(0, 1)) = 1
		_Flare_Ray_Move ("Flare Ray Move", Float) = 0.1
		_Flare_Ray_Amount ("Flare Ray Amount", Float) = 60
		_Flare_Ray_Amplitude ("Flare Ray Amplitude", Range(0, 5)) = 0
		_Circle_Scale ("Circle Scale", Range(0, 2)) = 1
		_Circle_Scale_Blur ("Circle Scale Blur", Range(0, 1)) = 0.5
		_Hole_Scale ("Hole Scale", Range(0, 5)) = 1
		_Hole_Scale_Blur ("Hole Scale Blur", Range(0, 1)) = 0.2
		_Simple_Noise_Scale ("Simple Noise Scale", Range(0.1, 30)) = 2
		_Gradient_Noise_Scale ("Gradient Noise Scale", Range(0.1, 30)) = 2
		_Voronoi_Scale ("Voronoi Scale", Range(0.1, 30)) = 2
		_Noise_Combine ("Noise Combine", Vector) = (0.1,0.8,0.5,0)
		_Alpha ("Alpha", Range(0, 1)) = 1
		_Alpha_Blur ("Alpha Blur", Range(0, 1)) = 0.5
		[ToggleUI] _Soft_Particle ("Soft Particle", Float) = 0
		_Soft_Particle_Near ("Soft Particle Near", Range(0, 1)) = 0
		_Soft_Particle_Far ("Soft Particle Far", Range(0, 15)) = 1
		[NoScaleOffset] _Texture2DAsset_3e4e754e8e574f9bab9bd1c9a475f786_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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