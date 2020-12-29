Shader "Unlit/DebugProbes"
{
    Properties
    {

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

            sampler2D G_IrradianceBand;
            int2 G_IrradianceBand_Size;
            int2 probeCounts;
            int pixelsPerProbe;

            #include "Assets/Resources/Probe.cginc"

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv * probeCounts;
                int2 pixel = i.uv * G_IrradianceBand_Size;
                float2 index = floor(uv);
                uv = frac(uv);
                uv = -1. + 2.* uv;
                float d = length(uv);
                float a = atan2(uv.y, uv.x);
                float a01 = (a + UNITY_PI)* UNITY_INV_TWO_PI;
                float2 p = float2(probeToPixelFloat(index, a01*pixelsPerProbe));
                p += 0.5;
                float2 st = p/G_IrradianceBand_Size;
                fixed4 col = tex2Dgrad(G_IrradianceBand, st, ddx(st), ddy(st));
                if(d > 0.5 || d < 0.4)
                    discard;
                return col;
            }
            ENDCG
        }
    }
}
