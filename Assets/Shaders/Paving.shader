// Steinplatten für die Strandpromenade: Die Platten rechnet dieser Shader pro Pixel aus der WELT-Position aus,
// nicht aus UV-Koordinaten. Darum läuft das Muster nahtlos weiter, auch wenn ein Level die Promenade per CityCutout
// in Stücke schneidet (jedes Stück ist ein neuer Würfel mit eigenen UVs, die Welt-Position bleibt aber dieselbe).
//
// - Platten im Verband: jede zweite Reihe um eine halbe Platte versetzt, wie gemauert.
// - Jede Platte bekommt aus einem Hash ihrer Rasterzelle einen etwas anderen Farbton.
// - Fugen sind dunkle Linien. Weit weg blendet das Muster zur Mischfarbe über, statt zu flimmern.
// - Seitenflächen (Kanten der Platte) sind einfach einfarbig.
Shader "JumpAndRun/Paving"
{
    Properties
    {
        _ColorA ("Platte hell", Color) = (0.86, 0.83, 0.76, 1)
        _ColorB ("Platte dunkel", Color) = (0.74, 0.71, 0.65, 1)
        _JointColor ("Fuge", Color) = (0.52, 0.5, 0.46, 1)
        _TileLength ("Plattenlänge in X (m)", Float) = 1.2
        _TileWidth ("Plattenbreite in Z (m)", Float) = 0.8
        _JointWidth ("Fugenbreite (m)", Float) = 0.035
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorA, _ColorB, _JointColor;
            float _TileLength;
            float _TileWidth;
            float _JointWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                half3 albedo = _ColorB.rgb * 0.92;

                if (normalWS.y > 0.5)
                {
                    // Rasterkoordinaten in "Platten": x entlang der Promenade, y quer dazu
                    float2 tile = input.positionWS.xz / float2(_TileLength, _TileWidth);
                    float row = floor(tile.y);
                    // Verband: ungerade Reihen um eine halbe Platte verschieben
                    tile.x += frac(row * 0.5) * 1.0;
                    float2 cell = floor(tile);
                    float2 inCell = frac(tile);

                    // Abstand zum nächsten Plattenrand in Metern (damit die Fuge überall gleich breit ist)
                    float2 toEdge = min(inCell, 1.0 - inCell) * float2(_TileLength, _TileWidth);
                    float edge = min(toEdge.x, toEdge.y);
                    // fwidth: wie viele Meter ein Pixel hier abdeckt. Damit wird die Fugenkante ~1 Pixel weich
                    float pixel = max(fwidth(input.positionWS.x), fwidth(input.positionWS.z));
                    float joint = 1.0 - smoothstep(_JointWidth - pixel, _JointWidth + pixel, edge);

                    half3 stone = lerp(_ColorA.rgb, _ColorB.rgb, Hash21(cell + 17.0));
                    half3 detailed = lerp(stone, _JointColor.rgb, joint);
                    // Weit weg (ein Pixel größer als eine Fuge): Mischfarbe statt flimmernder Fugen
                    half3 far = lerp((_ColorA.rgb + _ColorB.rgb) * 0.5, _JointColor.rgb, 0.1);
                    albedo = lerp(far, detailed, saturate(1.5 - pixel / _JointWidth * 0.5));
                }

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 diffuse = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 color = albedo * (diffuse + SampleSH(normalWS));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Schatten werfen (z.B. der Plattenweg auf den Sand davor)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
