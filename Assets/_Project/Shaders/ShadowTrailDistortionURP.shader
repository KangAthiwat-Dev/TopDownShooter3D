Shader "TopDownShooter/VFX/Shadow Trail Distortion URP"
{
    Properties
    {
        _NoiseTexture("Noise", 2D) = "gray" {}
        _Noise01UVS("Noise UV Scale", Vector) = (0.3,0.5,0,0)
        _Noise01UVP("Noise Pan Speed", Vector) = (-0.5,-0.1,0,0)
        _IntensityMask("Intensity Mask", 2D) = "white" {}
        _IntensityMaskUVS("Mask UV Scale", Vector) = (1,1,0,0)
        _IntensityMaskPanSpeed("Mask Pan Speed", Vector) = (-1,0.1,0,0)
        _IntensityMaskOffset("Mask Offset", Vector) = (0,0,0,0)
        _OpacityMask("Opacity Mask", 2D) = "white" {}
        _Erosion("Erosion", Range(0,1)) = 0
        _ErosionSmoothness("Erosion Softness", Float) = 1
        [HideInInspector] _DistortionStrength("Screen UV Offset", Float) = 0.012
        [HideInInspector] _DistortionEnabled("Opaque Texture Available", Float) = 1
        [HideInInspector] _OpacityMultiplier("Instance Opacity", Float) = 1
        [HideInInspector] _AnimationSpeedMultiplier("Instance Animation Speed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Name "ScreenDistortion"
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_NoiseTexture); SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_IntensityMask); SAMPLER(sampler_IntensityMask);
            TEXTURE2D(_OpacityMask); SAMPLER(sampler_OpacityMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _Noise01UVS, _Noise01UVP, _IntensityMaskUVS, _IntensityMaskPanSpeed, _IntensityMaskOffset;
                float _Erosion, _ErosionSmoothness, _DistortionStrength, _DistortionEnabled;
                float _OpacityMultiplier, _AnimationSpeedMultiplier;
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
                half alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.alpha = input.color.a;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_DistortionEnabled - 0.5);
                float time = _Time.y * _AnimationSpeedMultiplier;
                float2 noise = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture,
                    input.uv * _Noise01UVS.xy + time * _Noise01UVP.xy).rg;
                float mask = SAMPLE_TEXTURE2D(_IntensityMask, sampler_IntensityMask,
                    input.uv * _IntensityMaskUVS.xy + time * _IntensityMaskPanSpeed.xy + _IntensityMaskOffset.xy).g;
                mask = smoothstep(_Erosion, _Erosion + max(0.0001, _ErosionSmoothness), mask);
                float opacity = SAMPLE_TEXTURE2D(_OpacityMask, sampler_OpacityMask, input.uv).g;
                half alpha = saturate(mask * opacity * input.alpha * _OpacityMultiplier);
                float2 offset = (noise * 2 - 1) * _DistortionStrength * alpha;
                offset.x *= _ScaledScreenParams.y / _ScaledScreenParams.x;
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS.xy);
                return half4(SampleSceneColor(saturate(uv + offset)), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
