Shader "TopDownShooter/VFX/Ice Trail Comet Head"
{
    Properties
    {
        [HDR] _CoreColor("Core", Color) = (0.7,0.85,1,1)
        [HDR] _EdgeColor("Ice Flame Edge", Color) = (0.2,0.87,1,1)
        _Noise("Ice Trail Noise", 2D) = "gray" {}
        _Intensity("Intensity", Float) = 1
        _Opacity("Opacity", Range(0,1)) = 0.9
        _MotionSpeed("Motion Speed", Float) = 1
        _Flutter("Flutter", Float) = 0.045
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+1" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _EdgeColor;
                float _Intensity, _Opacity, _MotionSpeed, _Flutter;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float axial:TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float time = _Time.y * _MotionSpeed;
                float rear = saturate(0.5 - input.positionOS.z);
                float wave = sin(input.positionOS.z * 18 - time * 16 + input.positionOS.y * 13) *
                    cos(input.positionOS.x * 12 + time * 9) * _Flutter * rear;
                float3 position = input.positionOS.xyz;
                // Broad rounded nose, narrowing and stretching into the tail
                // at the back: a flowing comet, not a rigid sphere on a string.
                position.xy *= lerp(0.38, 1, smoothstep(-0.5, 0.05, position.z));
                position.z -= (1 - smoothstep(-0.5, 0.15, position.z)) * 0.25;
                position += input.normalOS * wave;
                output.positionWS = TransformObjectToWorld(position);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv; output.axial = input.positionOS.z + 0.5;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float time = _Time.y * _MotionSpeed;
                float n = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, input.uv * float2(2,3) + float2(time * 0.65, -time * 0.35)).g;
                float facing = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float rim = pow(1 - facing, 1.7);
                half3 color = lerp(_CoreColor.rgb * 2.1, _EdgeColor.rgb * 1.1, saturate(rim + n * 0.25));
                // Rounded intact front, softer turbulent back, and no flat
                // billboard edge. Reuses the imported Ice material's noise.
                float rear = 1 - smoothstep(0.15, 0.8, input.axial);
                float alpha = smoothstep(0, 0.28, facing) * lerp(1, 0.4 + n * 0.6, rear) * _Opacity;
                return half4(color * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
