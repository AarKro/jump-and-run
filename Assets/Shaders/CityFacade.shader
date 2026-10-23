// Hausfassade für die Stadt: Jedes Haus ist nur ein Würfel, das Fensterraster rechnet dieser Shader pro Pixel aus.
// So braucht ein Haus ein einziges Objekt statt ~50 (Fenster-Quads + Fensterbänke).
//
// - Maße: aus der Objekt-Matrix (Scale des Würfels = Hausgröße in Metern), Fenster stehen also immer passend zur Wand.
// - Farbe und welche Fenster leuchten: aus einem Hash der Hausposition. Alle Häuser teilen sich EIN Material
//   (gut für den SRP Batcher). Achtung: Static Batching würde die Würfel zu einem Mesh verschmelzen, dann ist die
//   Objekt-Matrix die Einheitsmatrix und alle Häuser sähen gleich aus. Die Häuser daher nicht "Batching Static" markieren.
// - Weit weg deckt ein Pixel mehrere Meter ab. Dann blendet das Raster zu einer Mischfarbe über, statt zu flimmern.
Shader "JumpAndRun/CityFacade"
{
    Properties
    {
        _Color0 ("Fassade 1", Color) = (0.62, 0.30, 0.22, 1)
        _Color1 ("Fassade 2", Color) = (0.86, 0.77, 0.60, 1)
        _Color2 ("Fassade 3", Color) = (0.56, 0.57, 0.60, 1)
        _Color3 ("Fassade 4", Color) = (0.36, 0.60, 0.60, 1)
        _Color4 ("Fassade 5", Color) = (0.90, 0.76, 0.42, 1)
        _Color5 ("Fassade 6", Color) = (0.86, 0.58, 0.58, 1)
        _RoofColor ("Dach", Color) = (0.30, 0.29, 0.28, 1)
        _TrimColor ("Fensterbank und Gesims", Color) = (0.85, 0.84, 0.80, 1)
        _GlassColor ("Fenster dunkel", Color) = (0.28, 0.38, 0.52, 1)
        _LitColor ("Fenster hell (leuchtet)", Color) = (1, 0.86, 0.55, 1)
        _DoorColor ("Tür", Color) = (0.42, 0.25, 0.13, 1)
        _LitChance ("Anteil heller Fenster", Range(0, 1)) = 0.25
        _GroundFloorShade ("Erdgeschoss abdunkeln", Range(0, 1)) = 0.7
        _FloorHeight ("Etagenhöhe (m)", Float) = 3
        _FirstWindow ("Unterkante unterste Fensterreihe (m)", Float) = 4.1
        _ColumnSpacing ("Fensterabstand (m)", Float) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Für alle Passes gleich: SRP-Batcher-kompatibel ist ein Shader nur, wenn jeder Pass denselben UnityPerMaterial-Puffer hat
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5;
            half4 _RoofColor, _TrimColor, _GlassColor, _LitColor, _DoorColor;
            half _LitChance;
            half _GroundFloorShade;
            float _FloorHeight;
            float _FirstWindow;
            float _ColumnSpacing;
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
                float3 positionOS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Weicher Bereich lo..hi: 1 innen, 0 außen, Kante über eine Pixelbreite (aa) verwischt, damit nichts flimmert
            float Band(float x, float lo, float hi, float aa)
            {
                return saturate((x - lo) / aa + 0.5) * saturate((hi - x) / aa + 0.5);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Länge der Matrix-Spalten = Scale des Würfels = Hausmaße in Metern
                float3 size = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                float3 p = input.positionOS * size;
                float3 n = input.normalOS;
                float3 origin = UNITY_MATRIX_M._m03_m13_m23;
                float seed = Hash21(origin.xz * 0.37 + 11.0);

                half3 palette[6] = { _Color0.rgb, _Color1.rgb, _Color2.rgb, _Color3.rgb, _Color4.rgb, _Color5.rgb };
                half3 wall = palette[min(5, (int)(seed * 6.0))];
                half3 albedo = _RoofColor.rgb;
                half3 emission = 0;

                if (abs(n.y) < 0.5)
                {
                    // Wandkoordinaten: u = Meter von der linken Hauskante, v = Meter über der Hausunterkante
                    bool sideX = abs(n.x) > 0.5;
                    float width = sideX ? size.z : size.x;
                    float u = (sideX ? p.z : p.x) + width * 0.5;
                    float v = p.y + size.y * 0.5;
                    float faceSeed = seed + (sideX ? 0.31 : 0.0) + ((n.x + n.z) > 0.0 ? 0.17 : 0.0);

                    float2 aa = max(fwidth(float2(u, v)), 1e-4);
                    float detail = saturate(1.5 - max(aa.x, aa.y) * 3.0);

                    half3 detailed = wall;
                    half3 glow = 0;
                    if (v < 3.2)
                    {
                        // Erdgeschoss: dunklerer Sockel, Schaufenster, Tür
                        detailed = wall * _GroundFloorShade;
                        bool shopLit = Hash21(float2(faceSeed * 31.0, 7.0)) < 0.5;
                        float shop = Band(u, 0.8, width - 2.4, aa.x) * Band(v, 0.7, 2.4, aa.y);
                        float door = Band(u, width - 1.9, width - 0.8, aa.x) * Band(v, 0.0, 2.2, aa.y);
                        detailed = lerp(detailed, shopLit ? 0 : _GlassColor.rgb, shop);
                        glow = shopLit ? _LitColor.rgb * shop : 0;
                        detailed = lerp(detailed, _DoorColor.rgb, door);
                    }
                    else
                    {
                        float columns = max(1.0, floor((width - 1.0) / _ColumnSpacing));
                        float spacing = width / columns;
                        float column = floor(u / spacing);
                        float cu = u - (column + 0.5) * spacing;

                        float fv = v - _FirstWindow;
                        float row = floor(fv / _FloorHeight);
                        float lv = fv - row * _FloorHeight;
                        // Nur Fensterreihen, die noch 1 m unter das Dach passen
                        float rowFits = step(_FirstWindow + row * _FloorHeight + 1.5, size.y - 1.0) * step(0.0, row);
                        float aboveFits = step(_FirstWindow + (row + 1.0) * _FloorHeight + 1.5, size.y - 1.0) * step(-1.0, row);

                        float window = Band(cu, -0.55, 0.55, aa.x) * Band(lv, 0.0, 1.5, aa.y) * rowFits;
                        float sill = Band(cu, -0.65, 0.65, aa.x) * Band(lv, _FloorHeight - 0.22, _FloorHeight - 0.1, aa.y) * aboveFits;
                        float cornice = Band(v, size.y - 0.45, size.y + 1.0, aa.y);

                        bool lit = Hash21(float2(column + faceSeed * 97.0, row + seed * 53.0)) < _LitChance;
                        detailed = lerp(detailed, lit ? 0 : _GlassColor.rgb, window);
                        glow = lit ? _LitColor.rgb * window : 0;
                        detailed = lerp(detailed, _TrimColor.rgb, saturate(sill + cornice));
                    }

                    // Weit weg: Mischfarbe aus Wand und etwas Glas
                    half3 far = lerp(wall, _GlassColor.rgb, 0.2);
                    albedo = lerp(far, detailed, detail);
                    emission = glow * detail;
                }

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 diffuse = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 color = albedo * (diffuse + SampleSH(normalWS)) + emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Schatten werfen (sonst stehen die Häuser schattenlos da)
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

            // Setzt URP pro schattenwerfendem Licht
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

        // Tiefe für Depth Prepass / Depth Texture
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

        // Tiefe + Normalen, braucht die SSAO im PC-Renderer
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFrag(Varyings input) : SV_Target
            {
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
