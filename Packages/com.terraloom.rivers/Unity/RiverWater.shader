Shader "TerraLoom/RiverWater"
{
    Properties
    {
        _BaseColor("Water", Color) = (0.04,0.32,0.40,1)
        _WaveColor("Reflections", Color) = (0.35,0.70,0.73,1)
        _FlowSpeed("Flow speed (metres per second)", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WaveColor;
            float _FlowSpeed;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float along=input.uv.y-_Time.y*_FlowSpeed;
                float wave=sin(along*4.7+sin(input.uv.x*3.1+along*.7))*sin(along*2.9-input.uv.x*2.3);
                float3 view=normalize(_WorldSpaceCameraPos-input.positionWS);
                float fresnel=pow(1-saturate(view.y),3);
                half reflection=saturate(.12+wave*.10+fresnel*.45);
                return half4(lerp(_BaseColor.rgb,_WaveColor.rgb,reflection),1);
            }
            ENDHLSL
        }
    }
}
