// Material shader for the TBSplineS example scene. It renders in the Built-in pipeline, URP and HDRP from the
// same material, so the examples look right whichever pipeline the project uses. Unity picks the first
// SubShader that matches the active pipeline: the URP and HDRP ones are skipped (and not even compiled) unless
// their package is installed, and the last one is a regular Standard surface shader for the Built-in pipeline.
//
// The spline components themselves do not depend on this shader: they generate plain meshes and work with any
// material you assign.
Shader "TBSplineS/Demo Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [ToggleUI] _VertexColor ("Multiply by Vertex Color", Float) = 0
    }

    // ----------------------------------------------------------------------------------------------------------
    // Universal Render Pipeline
    // ----------------------------------------------------------------------------------------------------------
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        // Every pass declares its own includes and material constants. An HLSLINCLUDE block here would be
        // prepended to the HDRP programs below as well, even when this SubShader is excluded, and break them.
        // The UnityPerMaterial layout must stay identical in all passes for the SRP Batcher.

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _EmissionColor;
                half _VertexColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = lerp(half4(1, 1, 1, 1), input.color, _VertexColor);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * input.color;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.metallic = _Metallic;
                surface.smoothness = _Glossiness;
                surface.specular = half3(0, 0, 0);
                surface.normalTS = half3(0, 0, 1);
                surface.emission = _EmissionColor.rgb;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _EmissionColor;
                half _VertexColor;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _EmissionColor;
                half _VertexColor;
            CBUFFER_END

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #if defined(_GBUFFER_NORMALS_OCT)
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _EmissionColor;
                half _VertexColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octahedral = PackNormalOctQuadEncode(normalWS);
                return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0);
            #else
                return half4(normalWS, 0);
            #endif
            }
            ENDHLSL
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // High Definition Render Pipeline
    //
    // HDRP measures light in physical units, so a scene authored for the Built-in pipeline would render almost
    // black under it. This pass lights the examples with a fixed sun that matches the scene's directional light
    // plus a sky and ground ambient term, which keeps them readable under any exposure settings.
    // ----------------------------------------------------------------------------------------------------------
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition": "14.0"
        }
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "HDRenderPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Glossiness;
                half _Metallic;
                half4 _EmissionColor;
                half _VertexColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            static const float3 SunDirection = float3(0.321, 0.766, -0.557);
            static const half3 SkyAmbient = half3(0.46, 0.5, 0.58);
            static const half3 GroundAmbient = half3(0.24, 0.23, 0.22);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionRWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionRWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = lerp(half4(1, 1, 1, 1), input.color, _VertexColor);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * input.color).rgb;
                float3 normalWS = normalize(input.normalWS);
                half3 ambient = lerp(GroundAmbient, SkyAmbient, normalWS.y * 0.5 + 0.5);
                half diffuse = saturate(dot(normalWS, SunDirection)) * 0.8;
                half3 color = albedo * (ambient + diffuse) * (1 - _Metallic * 0.5) + _EmissionColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    // ----------------------------------------------------------------------------------------------------------
    // Built-in Render Pipeline
    // ----------------------------------------------------------------------------------------------------------
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;
        half _VertexColor;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void Surf(Input input, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, input.uv_MainTex) * _Color * lerp(float4(1, 1, 1, 1), input.color, _VertexColor);
            o.Albedo = albedo.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
