Shader "Unlit/GridOffset"
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
                float2 vPos : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D<float4> PhiNoise;

            v2f vert (appdata v)
            {
                v2f o;
                int w, h;
                PhiNoise.GetDimensions(w,h);
                float4 noise = PhiNoise.Load(int3(v.vPos.x, v.vPos.y, 0));
                noise =  -0.5 + float4(noise.xy*1., 0, 0);
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex) + noise;
                o.vertex = mul(unity_MatrixVP, worldPos);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                int w, h;
                PhiNoise.GetDimensions(w,h);
                return PhiNoise.Load(int3(i.uv.x*w, i.uv.y*h, 0));
            }
            ENDCG
        }
    }
}
