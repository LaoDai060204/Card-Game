Shader "Shader Graphs/ShineStyleOF3D" {
	Properties {
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		[NoScaleOffset] _LoadingTex ("LoadingTex", 2D) = "white" {}
		_LoadingBlend ("LoadingBlend", Range(0, 1)) = 0
		[NoScaleOffset] _MonsterNameTex ("MonsterNameTex", 2D) = "black" {}
		_MonsterNameTextBold ("MonsterNameTextBold", Float) = 1.3
		[NoScaleOffset] _AttributeTex ("AttributeTex", 2D) = "white" {}
		_MonsterNameNormalPower ("MonsterNameNormalPower", Float) = 4
		_AttributeTile ("AttributeTile", Float) = 1
		_AttributeSize_Pos ("AttributeSize&Pos", Vector) = (9.85,13.9,-3.7,-5.81)
		_MainTexMetal ("MainTexMetal", Float) = 0
		[NoScaleOffset] [Normal] _MainNormal ("MainNormal", 2D) = "bump" {}
		_FrameMetal ("FrameMetal", Float) = 1
		[NoScaleOffset] _CardMask ("CardMask", 2D) = "white" {}
		_MainNormalPower ("MainNormalPower", Float) = 0.86
		_EnvironmentPower ("EnvironmentPower", Float) = 1
		_EnvironmentColor ("EnvironmentColor", Vector) = (1,1,1,0)
		[HDR] _CardNameColor ("CardNameColor", Vector) = (1,1,1,0)
		[HDR] _KiraColor ("KiraColor", Vector) = (0.3764706,0.4,0.5568628,0)
		[NoScaleOffset] _KiraColorTexture ("KiraColorTexture", 2D) = "white" {}
		_KiraMetal ("KiraMetal", Float) = 1
		[NoScaleOffset] _KiraNormal01 ("Kira01", 2D) = "white" {}
		_Kira01Tile ("Kira01Tile", Float) = 1
		_KiraNormal01Power ("Kira01Power", Float) = 1
		_Kira02Tile ("Kira02Tile", Float) = 1
		_KiraNormal02Power ("Kira02Power", Float) = 1
		_CardDistortion ("CardDistortion", Float) = 0
		[NoScaleOffset] _HoloTex ("HoloTex", 2D) = "white" {}
		_HoloShift ("HoloShift", Float) = 1
		_HoloBrightness ("HoloBrightness", Float) = 0.7
		_SpecularColor ("SpecularColor", Vector) = (0.7647059,0.7647059,0.7647059,1)
		_Smoothness ("Smoothness", Float) = 0.5
		_AmbientColor ("AmbientColor", Vector) = (0.0754717,0.0754717,0.0754717,1)
		_AddColor ("AddColor", Vector) = (0.5019608,0.5019608,0.5019608,1)
		_Monochrome ("Monochrome", Range(0, 1)) = 0
		_DirectionalLightAmp ("DirectionalLightAmp", Float) = 1
		_ScrollSpeed ("ScrollSpeed", Float) = 1.5
		_HighlightAmp ("HighlightAmp", Float) = 1
		_HighlightScrollOffset ("HighlightScrollOffset", Float) = 0
		_HighlightRotation ("HighlightRotation", Float) = -30
		_IllustHoloPower ("IllustHoloPower", Float) = 1
		_IllustContrast ("IllustContrast", Float) = 0
		_IllustBrightness ("IllustBrightness", Float) = 0
		_FakeLightDirection ("FakeLightDirection", Vector) = (0.75,-0.75,0,0)
		_FakeLightColor ("FakeLightColor", Vector) = (0,0,0,0)
		_FakeViewDirection ("FakeViewDirection", Vector) = (0.75,0.75,0,0)
		_FakeNormalDirection ("FakeNormalDirection", Vector) = (0.75,-0.75,0,0)
		_FakeBlend ("FakeBlend", Range(0, 1)) = 1
		_TintColor ("TintColor", Vector) = (1,1,1,1)
		_EnhanceColorThreshold ("EnhanceColorThreshold", Float) = 2
		_IllustRanbowPower ("IllustRanbowPower", Float) = 1
		[ToggleUI] _LinkOn_Off ("LinkOn_Off", Float) = 0
		[ToggleUI] _isFade ("isFade", Float) = 0
		[NoScaleOffset] _OverFrameMask ("OverFrameMask", 2D) = "white" {}
		[NoScaleOffset] _Texture2DAsset_90c6e35ef4304f289c279037152a03b7_Out_0_Texture2D ("Texture2D", 2D) = "white" {}
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

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}