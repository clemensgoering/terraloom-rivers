Shader "TerraLoom/Prototype/WaterfallLandscapeWater"
{
 Properties
 {
  _BaseColor("Deep water",Color)=(.025,.18,.16,1)
  _WaveColor("Surface light",Color)=(.22,.43,.36,1)
  _Fall("Falling strip",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
  Cull Off
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor,_WaveColor;float _Fall;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;};
   V Vert(A i){V o;o.world=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.world);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.fog=ComputeFogFactor(o.p.z);return o;}
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);}
   half4 Frag(V i):SV_Target
   {
    float speed=lerp(.8,3.2,_Fall),phase=i.uv.y+_Time.y*speed;
    float n=Noise(float2(i.uv.x*19,phase*lerp(2.3,.38,_Fall)));
    float ripple=sin(phase*13+sin(i.uv.x*23)*1.2)*.5+.5;
    float foam=smoothstep(.61,.91,n)*_Fall*.8;
    // Narrow bank fringe instead of broad white rails; falling water keeps streaks.
    foam=max(foam,smoothstep(.94,1,abs(i.uv.x))*.28);
    float impact=exp(-pow((i.world.z-85.8)*1.2,2))*exp(-i.uv.x*i.uv.x*1.8);
    foam=max(foam,impact*(.18+n*.42)*(1-_Fall));
    half3 color=lerp(_BaseColor.rgb,_WaveColor.rgb,.18+ripple*.16+n*.2);
    color=lerp(color,half3(.69,.80,.75),foam);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    color*=.48+.6*sun.shadowAttenuation;
    float3 view=normalize(GetCameraPositionWS()-i.world);
    float spec=pow(saturate(dot(reflect(-sun.direction,normalize(i.n)),view)),80)*.22;
    color+=sun.color*spec;color=MixFog(color,i.fog);return half4(color,1);
   }
   ENDHLSL
  }
 }
}
