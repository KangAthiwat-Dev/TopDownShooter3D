Shader "Project/Village Ground Overlay"
{
    Properties { _Tint ("Dirt colour", Color) = (.39,.28,.16,.65) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output; VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS; output.positionWS=p.positionWS; output.color=input.color; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half grain=frac(sin(dot(floor(input.positionWS.xz*23),float2(12.9898,78.233)))*43758.5453);
                half3 light=SampleSH(half3(0,1,0))+sun.color*saturate(sun.direction.y)*sun.shadowAttenuation;
                return half4(_Tint.rgb*(.91+grain*.18)*light,_Tint.a*input.color.a);
            }
            ENDHLSL
        }
    }
}
