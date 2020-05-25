Shader "Unlit/CopyToFullscreen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
            float2 _Offset;
            sampler2D _FullScreenAverage;
            float HYSTERESIS;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv +  _Offset;
                if(any(bool4(uv > 1, uv < 0)))
                    return float4(0,0,0,0);
                
                float4 original = tex2D(_FullScreenAverage, i.uv);
                float4 newCol = tex2D(_MainTex, uv);
                return lerp(original, newCol, HYSTERESIS);
            }
            ENDCG
        }
    }
}
