Shader "TooD/SmartBlendedBlit"
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

                Texture2D<float4> _MainTex;
                Texture2D<float4> OldColor;
                SamplerState sampler_linear_repeat;
                float Hysteresis;

                float2 DarknessBias;

                float4 smartBlend(float4 newColor, float4 oldColor, float hysteresis)
                {
                    //expects col*a, a premultiplied
                    //Lerp but with a premultiplied newColor
                    newColor.rgb = pow(newColor.rgb, 2./3);
                    oldColor.rgb = pow(oldColor.rgb, 2./3);
                    float3 blend = newColor.rgb + (1.-newColor.a)*oldColor.rgb;
                    //Bias so towards darkness so that darkness recedes faster
                    float factor = lerp(hysteresis, 0, smoothstep(DarknessBias.x, DarknessBias.y, length(newColor)));
                    float3 c = lerp(blend, oldColor.rgb, factor);
                    c.rgb = pow(c.rgb, 3./2);
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
                    float4 oldColor = OldColor.Sample(sampler_linear_repeat, i.uv);
                    float4 newColor = _MainTex.Sample(sampler_linear_repeat, i.uv);
                    return smartBlend(newColor, oldColor, Hysteresis);
                }
            ENDCG
        }
    }
}