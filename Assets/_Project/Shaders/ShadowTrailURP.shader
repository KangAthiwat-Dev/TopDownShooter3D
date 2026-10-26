// URP implementation of the imported Vefects trail's mask/noise/erosion recipe.
// Texture and material property names stay compatible; no Amplify dependency.
Shader "TopDownShooter/VFX/Shadow Trail URP"
{
    Properties
    {
        _EmissiveIntensity("Base Emission", Float) = 5
        _AlphaAffectsOpacity("Vertex Alpha Affects Opacity", Range(0,1)) = 1
        _OverallSpeed("Base Animation Speed", Float) = 1
        _Color("Color Noise", 2D) = "white" {}
        _ColorUVScale("Color UV Scale", Vector) = (1,1,0,0)
        _ColorPanSpeed("Color Pan Speed", Vector) = (0,0,0,0)
        [HDR] _Color01("Color 1", Color) = (1,1,1,1)
        [HDR] _Color02("Color 2", Color) = (1,1,1,1)
        _ColorSmoothstep("Color Threshold", Float) = 0
        _ColorSmoothstepSmoothness("Color Softness", Float) = 1
        _Distortion("UV Noise", 2D) = "gray" {}
        _DistortionUVScale("Noise UV Scale", Vector) = (1,1,0,0)
        _DistortionPanSpeed("Noise Pan Speed", Vector) = (0,0,0,0)
        _DistortionAmount("UV Noise Amount", Float) = 0.1
        _Erosion("Erosion", 2D) = "white" {}
        _ErosionUVScale("Erosion UV Scale", Vector) = (1,1,0,0)
        _ErosionPanSpeed("Erosion Pan Speed", Vector) = (0,0,0,0)
        _ErosionSmoothstep("Erosion Threshold", Float) = 0
        _ErosionSmoothstepSmoothness("Erosion Softness", Float) = 1
        _Mask("Mask", 2D) = "white" {}
        _MaskUVScale("Mask UV Scale", Vector) = (1,1,0,0)
        _MaskPanSpeed("Mask Pan Speed", Vector) = (0,0,0,0)
        _MaskDistortionIntensity("Mask UV Noise", Float) = 1
        _MaskSmoothstep("Mask Threshold", Float) = 0
        _MaskSmoothstepSmoothness("Mask Softness", Float) = 1
        [HideInInspector] _IntensityMultiplier("Instance Intensity", Float) = 1
        [HideInInspector] _OpacityMultiplier("Instance Opacity", Float) = 1
        [HideInInspector] _AnimationSpeedMultiplier("Instance Animation Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ShadowTrail"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Color); SAMPLER(sampler_Color);
            TEXTURE2D(_Distortion); SAMPLER(sampler_Distortion);
            TEXTURE2D(_Erosion); SAMPLER(sampler_Erosion);
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color01, _Color02;
                float4 _ColorUVScale, _ColorPanSpeed, _DistortionUVScale, _DistortionPanSpeed;
                float4 _ErosionUVScale, _ErosionPanSpeed, _MaskUVScale, _MaskPanSpeed;
                float _EmissiveIntensity, _AlphaAffectsOpacity, _OverallSpeed;
                float _ColorSmoothstep, _ColorSmoothstepSmoothness, _DistortionAmount;
                float _ErosionSmoothstep, _ErosionSmoothstepSmoothness, _MaskDistortionIntensity;
                float _MaskSmoothstep, _MaskSmoothstepSmoothness;
                float _IntensityMultiplier, _OpacityMultiplier, _AnimationSpeedMultiplier;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float time = _Time.y * _OverallSpeed * _AnimationSpeedMultiplier;
                float noise = SAMPLE_TEXTURE2D(_Distortion, sampler_Distortion,
                    input.uv * _DistortionUVScale.xy + time * _DistortionPanSpeed.xy).g * _DistortionAmount;
                float maskSample = SAMPLE_TEXTURE2D(_Mask, sampler_Mask,
                    (input.uv + float2(0.28, 0)) * _MaskUVScale.xy + time * _MaskPanSpeed.xy +
                    noise * _MaskDistortionIntensity).g;
                float mask = smoothstep(_MaskSmoothstep, _MaskSmoothstep + max(0.0001, _MaskSmoothstepSmoothness), maskSample);
                float erosionSample = SAMPLE_TEXTURE2D(_Erosion, sampler_Erosion,
                    input.uv * _ErosionUVScale.xy + time * _ErosionPanSpeed.xy + noise).g;
                float erosion = smoothstep(_ErosionSmoothstep, _ErosionSmoothstep + max(0.0001, _ErosionSmoothstepSmoothness), erosionSample);
                float coverage = saturate(mask - saturate(erosion - input.color.a));
                float colorSample = SAMPLE_TEXTURE2D(_Color, sampler_Color,
                    input.uv * _ColorUVScale.xy + time * _ColorPanSpeed.xy).g;
                float mix = smoothstep(_ColorSmoothstep, _ColorSmoothstep + max(0.0001, _ColorSmoothstepSmoothness), colorSample);
                half3 color = input.color.rgb * lerp(_Color01.rgb, _Color02.rgb, mix) * coverage *
                    _EmissiveIntensity * _IntensityMultiplier;
                half alpha = saturate(coverage * lerp(1, input.color.a, saturate(_AlphaAffectsOpacity)) * _OpacityMultiplier);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
