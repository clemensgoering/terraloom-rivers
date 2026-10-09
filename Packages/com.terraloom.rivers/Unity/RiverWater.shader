Shader "TerraLoom/RiverWater"
{
    Properties
    {
        _BaseColor("Water", Color) = (0.04,0.32,0.40,1)
        _WaveColor("Reflections", Color) = (0.35,0.70,0.73,1)
        _FlowSpeed("Flow speed (metres per second)", Float) = 0.8
        _IceAmount("Ice amount (visual only)", Range(0,1)) = 0
        _IceColor("Ice color", Color) = (0.65,0.82,0.90,1)
        [ToggleUI] _UseWorldRegions("Use example north winter region", Float) = 0
        _WinterBoundaryZ("Winter boundary (world Z)", Float) = 48
        _RegionBlend("Region transition width (metres)", Float) = 6
        _FoamColor("Bank foam", Color) = (0.75,0.88,0.86,1)
        _FoamWidth("Bank foam width (metres)", Float) = 0.18
        _FoamStrength("Bank foam strength", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Lit" "Queue"="Geometry+10" }
        Cull Back
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WaveColor, _IceColor, _FoamColor;
            float _FlowSpeed, _IceAmount, _UseWorldRegions, _WinterBoundaryZ, _RegionBlend, _FoamWidth, _FoamStrength;
        CBUFFER_END
        #if defined(UNITY_INSTANCING_ENABLED)
        UNITY_INSTANCING_BUFFER_START(WaterOverrides)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _WaveColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _IceColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _IceAmount)
            UNITY_DEFINE_INSTANCED_PROP(float, _FlowSpeed)
        UNITY_INSTANCING_BUFFER_END(WaterOverrides)
        #endif
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 uv : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 fogAndVertexLight : TEXCOORD3;
            float halfWidth : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionWS = position.positionWS;
            output.positionCS = position.positionCS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            // RiverGeometry uses signed across metres and along metres. Both edge vertices have the
            // same abs(across), so interpolation preserves the actual half width without another API input.
            output.halfWidth = abs(input.uv.x);
            output.fogAndVertexLight = half4(ComputeFogFactor(position.positionCS.z), VertexLighting(position.positionWS, output.normalWS));
            return output;
        }
        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half4 baseColor = _BaseColor, waveColor = _WaveColor, iceColor = _IceColor;
            float iceAmount = _IceAmount, speed = _FlowSpeed;
            #if defined(UNITY_INSTANCING_ENABLED)
                baseColor = UNITY_ACCESS_INSTANCED_PROP(WaterOverrides, _BaseColor);
                waveColor = UNITY_ACCESS_INSTANCED_PROP(WaterOverrides, _WaveColor);
                iceColor = UNITY_ACCESS_INSTANCED_PROP(WaterOverrides, _IceColor);
                iceAmount = UNITY_ACCESS_INSTANCED_PROP(WaterOverrides, _IceAmount);
                speed = UNITY_ACCESS_INSTANCED_PROP(WaterOverrides, _FlowSpeed);
            #endif
            float blend = max(abs(_RegionBlend), 0.001);
            float northWinter = smoothstep(_WinterBoundaryZ - blend * 0.5, _WinterBoundaryZ + blend * 0.5, input.positionWS.z);
            // Explicit IceAmount=1 freezes the whole material, including the example region's summer side.
            float ice = saturate(max(iceAmount, northWinter * saturate(_UseWorldRegions)));
            float liquid = 1.0 - ice;
            float along = input.uv.y - _Time.y * speed * liquid;
            float wave = sin(along * 4.7 + sin(input.uv.x * 3.1 + along * 0.7)) * sin(along * 2.9 - input.uv.x * 2.3);
            half3 normal = NormalizeNormalPerPixel(input.normalWS);
            half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
            float fresnel = pow(1.0 - saturate(dot(view, normal)), 3.0);
            half reflection = saturate(0.12 + wave * 0.10 * liquid + fresnel * 0.45);
            half3 water = lerp(baseColor.rgb, waveColor.rgb, reflection);
            float bankDistance = max(0.0, input.halfWidth - abs(input.uv.x));
            float bankFoam = 1.0 - smoothstep(0.0, max(_FoamWidth, 0.001), bankDistance);
            float foamPulse = 0.7 + 0.3 * sin(along * 3.2 + input.uv.x * 1.4);
            water = lerp(water, _FoamColor.rgb, saturate(bankFoam * foamPulse * _FoamStrength) * liquid);
            // Static crystalline variation: no time-dependent contribution survives at ice=1.
            float crystal = sin(input.positionWS.x * 2.1 + sin(input.positionWS.z * 1.3)) * sin(input.positionWS.z * 2.7);
            half3 frozen = iceColor.rgb * (0.94 + crystal * 0.06) + waveColor.rgb * fresnel * 0.12;
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = lerp(water, frozen, ice);
            surface.alpha = 1;
            surface.smoothness = lerp(0.88h, 0.65h, ice);
            surface.normalTS = half3(0,0,1);
            surface.occlusion = 1;
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.positionCS = input.positionCS;
            lighting.normalWS = normal;
            lighting.viewDirectionWS = view;
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
            #else
                lighting.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #endif
            lighting.bakedGI = SampleSHPixel(half3(0,0,0), normal);
            lighting.vertexLighting = input.fogAndVertexLight.yzw;
            lighting.shadowMask = half4(1,1,1,1);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            half4 color = UniversalFragmentPBR(lighting, surface);
            color.rgb = MixFog(color.rgb, input.fogAndVertexLight.x);
            return half4(color.rgb, 1);
        }
        float3 _LightDirection;
        float3 _LightPosition;
        Varyings ShadowVert(Attributes input)
        {
            Varyings output = Vert(input);
            float3 direction = _LightDirection;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                direction = normalize(_LightPosition - output.positionWS);
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction)));
            return output;
        }
        half4 DepthFrag(Varyings input) : SV_Target { return 0; }
        half4 NormalsFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 normal = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}
