Shader "TooD/Sprite"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
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
            
            float2 G_ProbeAreaOrigin;
            float G_ProbeSeparation;
            float2 G_ProbeCounts;
            
            #define OriginOffset (0.5*G_ProbeSeparation)
            
            TEXTURE2D(G_AverageIrradianceBuffer);
            SAMPLER(samplerG_AverageIrradianceBuffer);
            
            TEXTURE2D(G_CosineIrradianceBuffer);
            SAMPLER(samplerG_CosineIrradianceBuffer);
            
            float2 WorldToProbe(float2 worldPos)
            {
                worldPos += G_ProbeSeparation/2;//Maybe remove this later? Makes it look right atm
                return ((worldPos - G_ProbeAreaOrigin - OriginOffset)/ G_ProbeSeparation);
            }
            
            float2 GetIrradianceUv(float2 worldPos)
            {
                return WorldToProbe(worldPos)/G_ProbeCounts;
            }

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                half4 main = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                
                float4 irradianceColor = SAMPLE_TEXTURE2D(G_AverageIrradianceBuffer, samplerG_AverageIrradianceBuffer, GetIrradianceUv(i.worldPos));
                float4 col = CombinedShapeLightShared(main, mask, i.lightingUV);
                col.rgb *= irradianceColor.rgb;
                return col;
            }
            ENDHLSL
        }
        
        Pass
        {
            Tags { "LightMode" = "TooDLighting" }
            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma vertex vert
            #pragma fragment frag

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
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            half4 _MainTex_ST;

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 clipVertex = o.positionCS / o.positionCS.w;
                o.lightingUV = ComputeScreenPos(clipVertex).xy;
                o.color = v.color;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            
            float3 _EmissionColor;
            float _IsWall;
            float _Clip;

            half4 frag(Varyings i) : SV_Target
            {
                half4 main = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                float4 col =  CombinedShapeLightShared(main, mask, i.lightingUV);
                clip(col.a - _Clip);
                return float4(_EmissionColor, _IsWall);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
