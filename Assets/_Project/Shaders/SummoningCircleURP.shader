Shader "TopDownShooter/VFX/Summoning Circle URP"
{
    Properties
    {
        [MainTexture] _MainTex("Pack Texture", 2D) = "white" {}
        _SourceColor("Original Material Brightness", Color) = (1,1,1,1)
        [HDR] _EffectTint("Summoning Color", Color) = (0.15,0.5,1,1)
        _Intensity("Brightness", Float) = 2
        _Opacity("Sequence Fade", Range(0,1)) = 0
    }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _SourceColor, _EffectTint;
                half _Intensity, _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Retain the pack's animated brightness/alpha, but allow a true
                // color override rather than multiplying a new hue by old blue.
                half3 original = input.color.rgb * _SourceColor.rgb;
                half brightness = max(original.r, max(original.g, original.b));
                half alpha = tex.a * input.color.a * _SourceColor.a * _EffectTint.a * _Opacity;
                return half4(tex.rgb * brightness * _EffectTint.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
