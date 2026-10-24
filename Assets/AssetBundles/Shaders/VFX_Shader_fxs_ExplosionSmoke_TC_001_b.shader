Shader "VFX_Shader/fxs_ExplosionSmoke_TC_001_b" {
	Properties {
		[HDR] _Color_01 ("Color 01", Vector) = (1,0,0,0)
		[HDR] _Color_02 ("Color 02", Vector) = (0,1,0,0)
		_Color_Range ("Color Range", Float) = 1
		_Angle_Offset ("Angle Offset", Range(0, 1)) = 0.5
		_Main_Scroll_Speed ("Main Scroll Speed", Range(-3, 3)) = -0.1
		_Main_Amount ("Main Amount", Float) = 3
		_Main_Tile_Y ("Main Tile Y", Float) = 1
		_Main_Tile_X ("Main Tile X", Float) = 1
		_Main_Range ("Main Range", Range(0, 5)) = 2
		_Sub_Scroll_Speed ("Sub Scroll Speed", Range(-3, 3)) = -0.1
		_Sub_Amount ("Sub Amount", Float) = 2
		_Sub_Tile_Y ("Sub Tile Y", Float) = 2
		_Sub_Tile_X ("Sub Tile X", Float) = 1
		_Sub_Range ("Sub Range", Range(0, 5)) = 1
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