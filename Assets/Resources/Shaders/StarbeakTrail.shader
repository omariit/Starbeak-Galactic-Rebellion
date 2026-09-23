// StarbeakTrail.shader — additive trail ribbon material for TrailRenderer meshes.
// Honors vertex colors (TrailRenderer.time/color gradients) and a tint uniform.
// A pure-white _MainTex works fine; the Resources VFX/glow texture gives soft edges.
Shader "Hidden/StarbeakTrail"
{
    Properties
    {
        _MainTex ("Texture (RGB/A)", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 o;
                o.rgb = tex.rgb * i.color.rgb * _TintColor.rgb;
                o.a = tex.a * i.color.a * _TintColor.a;
                return o;
            }
            ENDCG
        }
    }

    Fallback Off
}
