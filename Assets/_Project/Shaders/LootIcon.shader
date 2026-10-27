Shader "TopDownShooter/Loot Icon"
{
    Properties
    {
        [PerRendererData] _MainTex("Icon", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varying { float4 position : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            Varying Vert(Input v)
            {
                Varying o; o.position=TransformObjectToHClip(v.position.xyz); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            half4 Frag(Varying v) : SV_Target
            {
                half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv)*v.color;
                clip(color.a-.01h); return color;
            }
            ENDHLSL
        }
    }
}
