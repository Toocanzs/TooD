Shader "Unlit/GridOffset"
{
    Properties
    {
        _Alpha("_Alpha", float) = 0
        _NoiseScale("_NoiseScale", float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            int pixelsPerProbe;
            #include "Assets/Resources/Probe.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 probeAndOffset : TEXCOORD0;
                float4 bary : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 colors[3] : COLOR;
                float3 bary : BARY;
                float4 vertex : SV_POSITION;
            };
            Texture2D<float4> PhiNoise;
            Texture2D<float4> PerProbeAverageTexture;
            int2 probeCounts;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                float2 probePos = v.probeAndOffset.xy;
                v.vertex.xy += probePos/ probeCounts;
                //TODO: Change the grid mesh to a set of quads with 0,1 uvs per quad. Offset quads, use circle, smartblend
                float2 n = PhiNoise[probePosToPixel(0, -1)].xy;
                float2 noise = n - 0.5;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex) + float4(noise, 0, 0) + float4(0.5, 0.5, 0, 0);//0.5 since bottom left isn't at probe center
                o.vertex = mul(unity_MatrixVP, worldPos);
                o.uv = probePos / probeCounts;

                if(int(v.bary.w) == 0)
                {
                    o.colors[0] = PerProbeAverageTexture[probePos + int2(0,0)];
                    o.colors[1] = PerProbeAverageTexture[probePos + int2(0,1)];
                    o.colors[2] = PerProbeAverageTexture[probePos + int2(1,1)];
                }
                else
                {
                    o.colors[0] = PerProbeAverageTexture[probePos + int2(0,0)];
                    o.colors[1] = PerProbeAverageTexture[probePos + int2(1,1)];
                    o.colors[2] = PerProbeAverageTexture[probePos + int2(1,0)];
                }
                o.bary = v.bary.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                i.bary = i.bary * i.bary * (3 - 2 * i.bary);
                float3 color = i.colors[0] * i.bary.x + i.colors[1] * i.bary.y + i.colors[2] * i.bary.z;
                return float4(color, _Alpha);
            }
            ENDCG
        }
    }
}
