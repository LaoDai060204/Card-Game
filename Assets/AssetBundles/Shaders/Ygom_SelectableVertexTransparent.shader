Shader "Ygom/SelectableVertexTransparent" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		[KeywordEnum(Off, On)] __ZWrite ("ZWrite", Float) = 0
		[KeywordEnum(Always,NotEqual,Equal,GEqual,LEqual,Greater,Less)] _ZTest ("ZTest", Float) = 4
		[KeywordEnum(None, Front, Back)] _Cull ("Culling", Float) = 2
		[Enum(UnityEngine.Rendering.BlendMode)] _BlendSrcFactor ("Blend SrcFactor", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _BlendDstFactor ("Blend DstFactor", Float) = 10
		[Toggle] _FogEnabled ("Fog Enabled", Float) = 1
		_ColorMask ("Color Mask", Float) = 15
		[Toggle] _FallOff ("Fall Off", Float) = 0
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
	Fallback "Unlit/Transparent"
}