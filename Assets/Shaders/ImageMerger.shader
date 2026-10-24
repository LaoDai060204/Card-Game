Shader "Hidden/ImageMerger"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _OverlayTex ("Overlay (RGB)", 2D) = "white" {}
        _OverlayUVRect ("Overlay UV Rect", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            sampler2D _OverlayTex;
            float4 _OverlayUVRect; // x,y,w,h

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 bg = tex2D(_MainTex, i.uv);
                // 判断当前 uv 是否在 overlay 矩形区域内
                if (i.uv.x >= _OverlayUVRect.x && i.uv.x <= _OverlayUVRect.x + _OverlayUVRect.z &&
                    i.uv.y >= _OverlayUVRect.y && i.uv.y <= _OverlayUVRect.y + _OverlayUVRect.w)
                {
                    float2 overlayUV = (i.uv - _OverlayUVRect.xy) / _OverlayUVRect.zw;
                    fixed4 overlay = tex2D(_OverlayTex, overlayUV);
        
                    // 如果 Face 该点的 alpha 不为 0，输出 Face 颜色；否则输出 Body 颜色
                    if (overlay.a > 0.0)   // 大于 0 即视为不透明
                        return overlay;
                    else
                        return bg;
                }
                return bg;
            }
            ENDCG
        }
    }
}