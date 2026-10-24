Shader "Shader Graphs/Timer_c001_01" {
	Properties {
		[NoScaleOffset] _Texture2D ("Texture2D", 2D) = "white" {}
		_SwitchTurn ("SwitchTurn", Range(0, 1)) = 0
		_Active ("Active", Range(0, 1)) = 1
		_MouseOver ("MouseOver", Range(0, 1)) = 1
		_PressButton ("PressButton", Range(0, 1)) = 0
		_ColorMouseOverNear ("ColorMouseOverNear", Vector) = (0,0.6753247,1,1)
		_ColorPlayNear ("ColorPlayNear", Vector) = (0,0.3607843,1,0)
		_InactiveIntensityNear ("InactiveIntensityNear", Float) = 0.5
		_ColorMouseOverFar ("ColorMouseOverFar", Vector) = (0.5566038,0,0,1)
		_ColorPlayFar ("ColorPlayFar", Vector) = (0.1137255,0.03921569,0.03921569,0)
		_InactiveIntensityFar ("InactiveIntensityFar", Float) = 0.5
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