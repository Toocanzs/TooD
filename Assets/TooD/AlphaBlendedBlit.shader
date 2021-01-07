Shader "TooD/AlphaBlendedBlit"
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

                Texture2D<float4> _MainTex;//The addtive buffer
                Texture2D<float4> OldColor;//old fullscreen colors
                SamplerState sampler_linear_repeat;
                float Hysteresis;

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                half4 frag(v2f i) : SV_Target
                {
                    float4 A = _MainTex.Sample(sampler_linear_repeat, i.uv);
                    float4 B = OldColor.Sample(sampler_linear_repeat, i.uv);
                    float3 c = lerp(B.rgb, A.rgb, Hysteresis);
                    return float4(c, 1);
                }
            ENDCG
        }
    }
}