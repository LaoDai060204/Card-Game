Shader "VFX_Shader/Particle/fxs_ParticleColorTexture_01_v14_k" {
	Properties {
		_Camera_Offset ("Camera Offset", Float) = 0
		_Alpha ("Alpha", Range(0, 1)) = 1
		[NoScaleOffset] _Texture ("Texture", 2D) = "white" {}
		_Alpha_Channel ("Alpha Channel", Vector) = (0,0,0,1)
		[ToggleUI] _Texture_Clamp ("Texture Clamp", Float) = 0
		_Tiling ("Tiling", Vector) = (1,1,0,0)
		_Offset ("Offset", Vector) = (0,0,0,0)
		_Rotat ("Rotat", Float) = 0
		[ToggleUI] _use_MASK_Texture ("use MASK Texture", Float) = 0
		[ToggleUI] _MASK_Texture_Clamp ("MASK Texture Clamp", Float) = 0
		[NoScaleOffset] _MASK_Texture ("MASK Texture", 2D) = "white" {}
		_MASK_Channel ("MASK Channel", Vector) = (1,0,0,0)
		_MASK_Tilig ("MASK Tilig", Vector) = (1,1,0,0)
		_MASK_Offset ("MASK Offset", Vector) = (0,0,0,0)
		_MASK_Rotat ("MASK Rotat", Float) = 0
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