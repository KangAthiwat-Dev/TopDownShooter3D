Shader "TopDownShooter/VFX/Cartoon Shadow Wind"
{
    Properties
    {
        _MainColor ("Wind", Color) = (0.9,0.86,1,1)
        _AccentColor ("Edge", Color) = (0.45,0.2,0.8,1)
        _Cover ("Cover", Range(0,1)) = 1
        _IsCover ("Transformation Shell", Float) = 0
        _MotionTime ("Motion Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest+10" }
        Pass
        {
            Name "CartoonWind"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _MainColor, _AccentColor;
                float _Cover, _IsCover, _MotionTime;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float edge = step(0.08, uv.y) * step(uv.y, 0.94);
                if (_IsCover > 0.5)
                {
                    // Curved bands progressively close the gaps. At Cover=1
                    // the shell is genuinely opaque before swapping the body.
                    float band = frac(uv.x * 5 + uv.y * 3 + sin(uv.y * 12 - _MotionTime * 7) * 0.1 - _MotionTime * 2);
                    clip(_Cover + 0.0001 - band);
                    edge = step(0.16, band / max(_Cover, 0.001));
                }
                return half4(lerp(_AccentColor.rgb, _MainColor.rgb, edge), 1);
            }
            ENDHLSL
        }
    }
}
