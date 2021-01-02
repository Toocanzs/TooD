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
                float2 uv : TEXCOORD0;
                float2 probePos : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 color : COLOR;
                float4 vertex : SV_POSITION;
            };
            Texture2D<float4> PhiNoise;
            Texture2D<float4> PerProbeAverageTexture;
            int2 probeCounts;
            float _NoiseScale;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                //TODO: Change the grid mesh to a set of quads with 0,1 uvs per quad. Offset quads, use circle, smartblend
                float2 n = PhiNoise[probePosToPixel(v.probePos, -1)].xy;
                float2 noise = n - 0.5;
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex) + float4(noise, 0, 0) * _NoiseScale;
                o.vertex = mul(unity_MatrixVP, worldPos);
                o.uv = v.uv;
                o.color = PerProbeAverageTexture[v.probePos];
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                //TODO: Calculate uvs here somehow and f*f*(3-2*f) for possibly a better blend
                return float4(i.color, _Alpha);
            }
            ENDCG
        }
    }
}
