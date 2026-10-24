Shader "Shader Graphs/RoyalStyleOFUI" {
	Properties {
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		[NoScaleOffset] _LoadingTex ("LoadingTex", 2D) = "white" {}
		_LoadingBlend ("LoadingBlend", Range(0, 1)) = 0
		[NoScaleOffset] _MonsterNameTex ("MonsterNameTex", 2D) = "black" {}
		_MonsterNameTextBold ("MonsterNameTextBold", Float) = 1.3
		_MonsterNameNormalPower ("MonsterNameNormalPower", Float) = 3
		[NoScaleOffset] _AttributeTex ("AttributeTex", 2D) = "white" {}
		_AttributeTile ("AttributeTile", Float) = 0
		_AttributeSize_Pos ("AttributeSize&Pos", Vector) = (9.85,13.9,-3.7,-5.81)
		_MainTexMetal ("MainTexMetal", Float) = 1
		[NoScaleOffset] _MainNormal ("MainNormal", 2D) = "white" {}
		_MainNormalPower ("MainNormalPower", Float) = 1
		_FrameMetal ("FrameMetal", Float) = 0
		_CubemapPower ("CubemapPower", Float) = 2
		_CubemapColor ("CubemapColor", Vector) = (0.8980392,0.8666667,0.3254902,0)
		[NoScaleOffset] _KiraColorTexture ("KiraColorTexture", 2D) = "white" {}
		_KiraMetal01 ("KiraMetal01", Float) = -0.33
		_Kira01_01Tile ("Kira01_01Tile", Float) = 0.5
		_KiraNormal01_01Power ("Kira01_01Power", Float) = 1
		[NoScaleOffset] _KiraNormal01_02 ("Kira01_02", 2D) = "white" {}
		_Kira01_02Tile ("Kira01_02Tile", Float) = 2
		_Kira01_02Power ("Kira01_02Power", Float) = -2.19
		_CardDistortion01 ("CardDistortion01", Float) = 1
		[NoScaleOffset] _HoloTex ("HoloTex01", 2D) = "white" {}
		[HDR] _KiraColor02 ("KiraColor02", Vector) = (0.4352941,0.3960784,0.6588235,0)
		_HoloShift01 ("HoloShift01", Float) = 2.18
		_HoloBrightness ("HoloBrightness", Float) = 1
		_KiraMetal02 ("KiraMetal02", Float) = 3
		[NoScaleOffset] [Normal] _Kira02_01 ("Kira02_01", 2D) = "bump" {}
		_Kira02_01Tile ("Kira02_01Tile", Float) = 1
		_Kira02_01Power ("Kira02_01Power", Float) = -3.47
		_Kira2_02Tile ("Kira2_02Tile", Float) = 1
		_Kira02_02Power ("Kira02_02Power", Float) = 0.49
		_CardDistortion02 ("CardDistortion02", Float) = 1
		[NoScaleOffset] _CardMask ("CardMask", 2D) = "white" {}
		_HoloBrightness02 ("HoloBrightness02", Float) = 1.29
		_HoloShift02 ("HoloShift02", Float) = 1
		_SpecularColor ("SpecularColor", Vector) = (0.1019608,0.1019608,0.1019608,1)
		_Smoothness ("Smoothness", Float) = 0.5
		_AmbientColor ("AmbientColor", Vector) = (0.1019608,0.1019608,0.1019608,1)
		_AddColor ("AddColor", Vector) = (0.5019608,0.5019608,0.5019608,1)
		_Monochrome ("Monochrome", Range(0, 1)) = 0
		_DirectionalLightAmp ("DirectionalLightAmp", Float) = 0.88
		_Color ("Color", Vector) = (1,1,1,0)
		_ScrollSpeed ("ScrollSpeed", Float) = 1.5
		_HighlightAmp ("HighlightAmp", Float) = 1.8
		_HighlightScrollOffset ("HighlightScrollOffset", Float) = 0
		_HighlightRotation ("HighlightRotation", Float) = -30
		[NoScaleOffset] _HighlightTex ("HighlightTex", 2D) = "white" {}
		[NoScaleOffset] _HighlightNormal ("HighlightNormal", 2D) = "white" {}
		_EnhanceColorThreshold ("EnhanceColorThreshold", Float) = 1
		_RanbowPower ("RanbowPower", Float) = 0
		_IllustBright ("IllustBright", Float) = 0
		_IlluustRanbowPower ("IlluustRanbowPower", Float) = 0
		_FakeLightColor ("FakeLightColor", Vector) = (0,0,0,0)
		_FakeLightDirection ("FakeLightDirection", Vector) = (0,0,0,0)
		_FakeViewDirection ("FakeViewDirection", Vector) = (0,0,0,0)
		_FakeNormalDirection ("FakeNormalDirection", Vector) = (0,0,0,0)
		_FakeBlend ("FakeBlend", Range(0, 1)) = 0
		_TintColor ("TintColor", Vector) = (1,1,1,1)
		[NoScaleOffset] _OverFrameMask ("OverFrameMask", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_90c6e35ef4304f289c279037152a03b7_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
		[HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _Stencil ("Stencil ID", Float) = 0
		[HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
		[HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
		[HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
		[HideInInspector] _ColorMask ("ColorMask", Float) = 15
		[HideInInspector] _ClipRect ("ClipRect", Vector) = (0,0,0,0)
		[HideInInspector] _UIMaskSoftnessX ("UIMaskSoftnessX", Float) = 1
		[HideInInspector] _UIMaskSoftnessY ("UIMaskSoftnessY", Float) = 1
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
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;
			float4 _Color;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy) * _Color;
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}