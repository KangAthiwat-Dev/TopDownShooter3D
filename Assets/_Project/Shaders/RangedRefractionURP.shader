Shader "TopDownShooter/VFX/Ranged Refraction URP"
{
    Properties
    {
        _Strength("Distortion Strength", Range(0,0.02)) = 0.002
        _Opacity("Opacity", Range(0,1)) = 0.5
        [HideInInspector] _Available("Opaque Texture Available", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Strength, _Opacity, _Available;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                clip(_Available - 0.5);
                float2 p = i.uv * 2 - 1;
                float radius = length(p);
                float edge = 1 - smoothstep(0.65, 1.0, radius);
                float ring = smoothstep(0.02, 0.35, radius) * edge;
                float2 noise = float2(sin(p.y * 17 + p.x * 9 + _Time.y * 13),
                                     cos(p.x * 19 - p.y * 7 - _Time.y * 11));
                float2 radial = p / max(radius, 0.05);
                float2 offset = (radial * 0.7 + noise * 0.3) * ring * _Strength;
                offset.x *= _ScaledScreenParams.y / _ScaledScreenParams.x;
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS.xy);
                return half4(SampleSceneColor(saturate(uv + offset)), ring * _Opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
