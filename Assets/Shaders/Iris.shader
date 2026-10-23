// Kreis-Blende ("Iris") für die UI: Schwarz über dem ganzen Bildschirm, nur in einem Kreis durchsichtig.
// Liegt auf einem bildschirmfüllenden UI-Image. ScreenIris.cs setzt Mittelpunkt und Radius jeden Frame.
// Die UV des Images läuft von 0 bis 1 über den ganzen Bildschirm. Damit der Kreis rund bleibt (und kein Oval wird),
// wird die x-Richtung mit dem Seitenverhältnis gestreckt: Radius 1 = eine Bildschirmhöhe.
// UI-Shader im Stil von Unitys UI/Default (CG): Den nutzt auch URP für Canvas-Elemente.
Shader "JumpAndRun/UI/Iris"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Center ("Mittelpunkt (Bildschirm 0-1)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius (Anteil der Bildschirmhöhe)", Float) = 1
        _Aspect ("Seitenverhältnis Breite/Höhe", Float) = 1.7778
        _Softness ("Weiche Kante (zusätzlich zur Kantenglättung)", Float) = 0.003
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Center;
            float _Radius;
            float _Aspect;
            float _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Farbe des Image-Components (schwarz) kommt als Vertex-Farbe
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Abstand zum Mittelpunkt in "Bildschirmhöhen", x gestreckt, damit der Kreis rund ist
                float2 delta = (i.uv - _Center.xy) * float2(_Aspect, 1.0);
                float distance = length(delta);
                // fwidth = wie stark sich der Wert zum Nachbarpixel ändert, also etwa "ein Pixel". So ist der Rand
                // immer ~1 Pixel weich, egal bei welcher Auflösung: keine Treppenstufen
                float edge = fwidth(distance) + _Softness;
                // 0 im Kreis (durchsichtig), 1 außerhalb (schwarz)
                float outside = smoothstep(_Radius - edge, _Radius + edge, distance);

                fixed4 color = i.color;
                color.a *= outside;
                return color;
            }
            ENDCG
        }
    }
}
