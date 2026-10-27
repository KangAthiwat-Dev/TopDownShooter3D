Shader "Project/Gentle River Water"
{
    Properties
    {
        _WaterColor("Water colour",Color)=(.11,.30,.33,.60)
        _WaveHeight("Ripple height (metres)",Range(0,.08))=.018
        _WaveSpeed("Ripple speed",Range(0,2))=.45
        _NormalStrength("Ripple reflections",Range(0,.4))=.12
        _PreviewOffset("Preview time offset",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _WaterColor;float _WaveHeight,_WaveSpeed,_NormalStrength,_PreviewOffset;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;};
            V Vert(A input)
            {
                V output;input.positionOS.y=0;float3 p=TransformObjectToWorld(input.positionOS.xyz);
                float t=(_Time.y+_PreviewOffset)*_WaveSpeed;
                p.y+=_WaveHeight*(sin(p.x*1.8+p.z*.8+t)+.45*sin(p.z*2.6-p.x*.7-t*.73));
                output.positionWS=p;output.positionCS=TransformWorldToHClip(p);return output;
            }
            half4 Frag(V input):SV_Target
            {
                float3 p=input.positionWS;float t=(_Time.y+_PreviewOffset)*_WaveSpeed;
                float nx=cos(p.x*1.8+p.z*.8+t)+.5*cos(p.x*5-p.z*3+t*.8);
                float nz=cos(p.z*2.6-p.x*.7-t*.73)+.35*cos(p.z*5.5+p.x*2-t*.9);
                half3 n=normalize(float3(nx*_NormalStrength,1,nz*_NormalStrength));
                half3 view=GetWorldSpaceNormalizeViewDir(p);Light sun=GetMainLight();
                half fresnel=pow(1-saturate(dot(n,view)),4);
                half spec=pow(saturate(dot(n,normalize(sun.direction+view))),160)*.35;
                half3 colour=_WaterColor.rgb*(SampleSH(n)+sun.color*(.35+.35*saturate(dot(n,sun.direction))));
                colour=lerp(colour,half3(.30,.46,.50),fresnel*.55)+spec*sun.color;
                return half4(colour,saturate(_WaterColor.a+fresnel*.15));
            }
            ENDHLSL
        }
    }
}
