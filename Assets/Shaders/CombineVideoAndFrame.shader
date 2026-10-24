Shader "MDPro3/CombineVideoAndFrame"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "black" {}
        _FrameTex ("Frame Texture", 2D) = "white" {}
        _VideoRect ("Video Rect (x,y,w,h)", Vector) = (0,0,1,1)
    }
    
    SubShader
    {
        ZTest Always
        Cull Off
        ZWrite Off

        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            sampler2D _FrameTex;
            float4 _VideoRect; // x: posX, y: posY, z: scaleX, w: scaleY
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 采样卡框纹理
                fixed4 frameCol = tex2D(_FrameTex, i.uv);
                float frameAlpha = frameCol.a;
                
                // 计算视频UV（考虑位置和缩放）
                float2 videoUV = (i.uv - float2(_VideoRect.x, _VideoRect.y)) / float2(_VideoRect.z, _VideoRect.w);
                
                // 判断UV是否在视频区域内
                fixed4 videoCol = fixed4(0,0,0,0);
                if (all(videoUV >= 0) && all(videoUV <= 1))
                {
                    videoCol = tex2D(_MainTex, videoUV);
                }

                // 合成：使用 Alpha 混合
                fixed4 col;
                col.rgb = frameCol.rgb * frameAlpha + videoCol.rgb * (1 - frameAlpha);
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
}