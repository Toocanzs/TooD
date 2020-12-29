Shader "Unlit/PhiNoise"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
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

            //hash from https://www.shadertoy.com/view/4ssXzX
            float2 bbsopt(in float2 a){
	            return frac(a*a*(1./1739.))*1739.;
            }
            float4 hash(in float2 pos){
	            float2 a0 = frac(pos*UNITY_PI)*1024;
	            float2 a1 = bbsopt(a0);
	            float2 a2 = a1.yx + bbsopt(a1);
	            float2 a3 = a2.yx + bbsopt(a2);
	            return frac((a2.xyxy + a3.xxyy + a1.xyyx)*(1./1739.));
            }

            static float phi = 1.6180339887498948482045868343656;
            #pragma multi_compile_local __ UPDATE

            sampler2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                #ifdef UPDATE
                return frac(tex2D(_MainTex, i.uv) + phi);
                #else
                return hash(i.uv);
                #endif
            }
            ENDCG
        }
    }
}
