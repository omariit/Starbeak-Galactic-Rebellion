// StarbeakComposite.shader — final original + bloom * intensity additive composite.
Shader "Hidden/StarbeakComposite"
{
    Properties
    {
        _MainTex ("Original (RGB)", 2D) = "white" {}
        _BloomTex ("Bloom (RGB)", 2D) = "black" {}
        _Intensity ("Intensity", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BloomTex;
            half _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 original = tex2D(_MainTex, i.uv);
                fixed3 bloom = tex2D(_BloomTex, i.uv).rgb;
                // Add the glow on top; alpha untouched so the screen copy keeps its coverage.
                return fixed4(original.rgb + bloom * _Intensity, original.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
