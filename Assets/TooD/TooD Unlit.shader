Shader "TooD/Unlit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        [HDr]
        _EmissionColor("Emission Color", Color) = (0,0,0,1)
        [Toggle]
        _IsWall("Is Wall Or Light", int) = 0
        _Clip("Alpha Clip", float) = 0.1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                float4  color       : COLOR;
                float2	uv          : TEXCOORD0;
                float2	lightingUV  : TEXCOORD1;
                float2  worldPos     : TEXCOORD2;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            half4 _MainTex_ST;
            
            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 clipVertex = o.positionCS / o.positionCS.w;
                o.lightingUV = ComputeScreenPos(clipVertex).xy;
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, float4(v.positionOS,1)).xy;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            
            float2 _ProbeAreaOrigin;
            float _ProbeSeparation;
            float4x4 worldDirectionToBufferDirection;
            
            #define OriginOffset (0.5*_ProbeSeparation)
            
            TEXTURE2D(_AverageIrradienceBuffer);
            SAMPLER(sampler_AverageIrradienceBuffer);
            
            int directionCount;
            int gutterSize;
            float2 ProbeCounts;
            
            uint2 GetNearestProbe(float2 worldPos)
            {
                return (uint2)floor((worldPos - _ProbeAreaOrigin - OriginOffset)/ _ProbeSeparation);
            }
            
            float2 GetProbeWorldPos(uint2 probePos)
            {
                return _ProbeAreaOrigin + OriginOffset + float2(probePos) * _ProbeSeparation;
            }
            
            float2 GetIrradianceUv(float2 worldPos)
            {
                return ((worldPos - _ProbeAreaOrigin - OriginOffset)/ _ProbeSeparation)/ProbeCounts;
            }
            
            //TODO:RREMOVE;
            /*TEXTURE2D(_SDF);
            SAMPLER(sampler_SDF);
            
            float4 getCol(float d)
            {
                float3 col = 1.0 - sign(d)*float3(0.1,0.4,0.7);
                col *= 1.0 - exp(-3.0*abs(d));
                col *= 0.8 + 0.2*cos(150.0*d);
                col = lerp( col, 1.0, 1.0-smoothstep(0.0,0.01,abs(d)) );
                return float4(col,1.0);
            }*/

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
