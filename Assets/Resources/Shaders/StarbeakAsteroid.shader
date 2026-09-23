// StarbeakAsteroid.shader — unlit fake-Lambert rock with rim light + vertex AO.
// Designed for MeshFilter+MeshRenderer asteroid instances sharing 3 palette materials.
// All lighting constants are baked in-shader: zero per-object material setup needed.
Shader "Hidden/StarbeakAsteroid"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.3, 0.35, 0.42, 1)
        _RimColor ("Rim Color", Color) = (0.55, 0.8, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-100" }

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RimColor;
            half _RimPower;

            // Fixed key light direction (normalized-ish), from upper-left of screen,
            // slightly toward camera. Constant in the shader => no CPU per-frame setup.
            static const half3 LightDir = half3(-0.45, 0.62, -0.64);

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                half3 viewDir : TEXCOORD1;
                half ao : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = UnityWorldSpaceViewDir(worldPos);

                // Fake AO: darker toward local -Y (bottom of the mesh), a soft gradient.
                half up = saturate(v.normal.y * 0.5 + 0.5);
                o.ao = 0.55 + 0.45 * up;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.worldNormal);
                half3 view = normalize(i.viewDir);

                half ndl = max(dot(n, LightDir), 0.0);
                half lambert = 0.30 + 0.70 * ndl;      // wrapped fake lambert (ambient floor)

                half rim = pow(saturate(1.0 - dot(n, view)), _RimPower);

                fixed3 col = _Color.rgb * lambert * i.ao;
                col += _RimColor.rgb * rim * 0.8;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
