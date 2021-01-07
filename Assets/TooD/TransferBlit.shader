Shader "Unlit/TransferBlit"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
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

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                sampler2D _MainTex;
                float2 TransferBlitOffset;

                half4 frag(v2f i) : SV_Target
                {
                    float2 uv = i.uv + TransferBlitOffset;
                    if(any(uv < 0) || any(uv > 1))
                        return float4(0,0,0,1);
                    return tex2D(_MainTex, uv);
                }
            ENDCG
        }
    }
}
