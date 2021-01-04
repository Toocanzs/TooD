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
        Blend One One

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
                //float3 colors[3] : COLOR;
                //float3 bary : BARY;
                float3 color : COLOR;
                float4 vertex : SV_POSITION;
                nointerpolation float sizeNoise : NOISE;
            };
            Texture2D<float4> PhiNoise;
            Texture2D<float4> PerProbeAverageTexture;
            int2 G_ProbeCounts;
            int G_PixelsPerProbe;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                float2 probePos = v.probeAndOffset.xy;
                //v.vertex.xy += probePos/ probeCounts;
                float4 sampleNoise = PhiNoise[probePosToPixel(probePos, -1)];
                o.uv = v.vertex.xy * G_ProbeCounts;
                v.vertex.xy *= G_ProbeCounts;//0-1
                float what = (sampleNoise.z*0 + 2);
                v.vertex.xy = (-0.5 + v.vertex.xy) * what;
                v.vertex.xy /= G_ProbeCounts;
                //TODO: Change the grid mesh to a set of quads with 0,1 uvs per quad. Offset quads, use circle, smartblend

                float2 noise = sampleNoise.xy*G_ProbeCounts;
                o.sizeNoise = sampleNoise.z;
                
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex) + float4(noise + what/4, 0, 0);// + float4(0.5, 0.5, 0, 0);//0.5 since bottom left isn't at probe center
                o.vertex = mul(unity_MatrixVP, worldPos);
                //o.uv = probePos / probeCounts;
                
                

                /*if(int(v.bary.w) == 0)
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
                o.bary = v.bary.xyz;*/
                o.color = PerProbeAverageTexture[probePos + int2(0,0)];
                return o;
            }

            int TEST;
            fixed4 frag (v2f i) : SV_Target
            {
                //i.bary = i.bary * i.bary * (3 - 2 * i.bary);
                //float3 color = i.colors[0] * i.bary.x + i.colors[1] * i.bary.y + i.colors[2] * i.bary.z;
                i.uv = -1 + 2 * i.uv;
                float d = length(i.uv*i.uv);
                //d = (abs(i.uv.x) + abs(i.uv.y));
                d = smoothstep(1., 0., d);
                return float4(d * i.color, d);
            }
            ENDCG
        }
    }
}
