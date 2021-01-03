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

                sampler2D _MainTex;
                Texture2D<float4> PreviousColor;
                SamplerState sampler_linear_repeat;
                float Hysteresis;

                float4 smartBlend(float4 newColor, float4 oldColor, float hysteresis)
                {
                    //expects col*a, a premultiplied
                    float3 blend = newColor.rgb + (1.-newColor.a)*oldColor.rgb;
                    float3 c = lerp(blend, oldColor.rgb, hysteresis);
                    return float4(c, 1.);
                }

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                half4 frag(v2f i) : SV_Target
                {
                    float4 oldColor = PreviousColor.Sample(sampler_linear_repeat, i.uv);
                    float4 c = tex2D(_MainTex, i.uv);
                    return smartBlend(c, oldColor, Hysteresis);
                }
            ENDCG
        }
    }
}