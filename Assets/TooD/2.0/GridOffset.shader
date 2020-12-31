Shader "Unlit/GridOffset"
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

            int2 probeCounts;

            v2f vert (appdata v)
            {
                v2f o;
                ProbePos probePos = probePosToPixel(v.probePos, -1);
                float4 n = PhiNoise.Load(int3(probePos, 0));
                float4 noise =  -0.5 + float4(n.xy*1., 0, 0);
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex) + noise;
                o.vertex = mul(unity_MatrixVP, worldPos);
                o.uv = v.uv;
                o.color = n.rgb;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                //TODO: Calculate uvs here somehow and f*f*(3-2*f) for possibly a better blend
                return float4(i.color, 1);
            }
            ENDCG
        }
    }
}
