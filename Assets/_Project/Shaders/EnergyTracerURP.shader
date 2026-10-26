Shader "TopDownShooter/VFX/Energy Tracer URP"
{
    Properties { [HDR] _Color("Glow Color", Color) = (3,2,0.6,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float edge = pow(saturate(1 - abs(i.uv.y * 2 - 1)), 1.7);
                float ends = smoothstep(0,0.12,i.uv.x) * (1 - smoothstep(0.88,1,i.uv.x));
                return half4(_Color.rgb * i.color.rgb, _Color.a * i.color.a * edge * ends);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
