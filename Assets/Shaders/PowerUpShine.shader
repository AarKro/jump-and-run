// Leuchtender Overlay-Effekt für aktive Power-Ups.
// Wird als zusätzliches Material über das normale Material des Players gelegt und
// addiert Licht (Blend One One): pulsierender Rand-Glanz (Fresnel) + diagonaler Lichtstreifen.
// _Intensity steuert PowerUpShine.cs zum Ein-/Ausblenden.
Shader "JumpAndRun/PowerUpShine"
{
    Properties
    {
        [HDR] _ShineColor ("Shine Color", Color) = (0.3, 0.9, 1, 1)
        _Intensity ("Intensity", Range(0, 1)) = 1
        _RimPower ("Rim Power (höher = schmalerer Rand)", Range(0.5, 8)) = 2.5
        _PulseSpeed ("Pulse Speed", Float) = 6
        _SweepInterval ("Sweep Interval (Sekunden pro Streifen)", Float) = 1.2
        _SweepWidth ("Sweep Width", Range(0.02, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Shine"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShineColor;
                half _Intensity;
                half _RimPower;
                half _PulseSpeed;
                half _SweepInterval;
                half _SweepWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 offsetWS : TEXCOORD2; // Position relativ zum Objekt-Mittelpunkt
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.offsetWS = output.positionWS - TransformObjectToWorld(float3(0, 0, 0));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Fresnel: je flacher die Oberfläche zur Kamera steht (Rand), desto heller
                half rim = pow(1.0 - saturate(dot(normal, viewDir)), _RimPower);
                half pulse = 0.7 + 0.3 * sin(_Time.y * _PulseSpeed);

                // Diagonaler Streifen, der von unten links nach oben rechts über den Player wandert.
                // Läuft in Objekt-Nähe mit, damit er beim Bewegen nicht "stehen bleibt".
                float diagonal = input.offsetWS.x + input.offsetWS.y;
                float sweepPosition = lerp(-1.5, 1.5, frac(_Time.y / _SweepInterval));
                half band = 1.0 - smoothstep(0.0, _SweepWidth, abs(diagonal - sweepPosition));

                half3 color = _ShineColor.rgb * (rim * pulse + band * 0.8) * _Intensity;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
