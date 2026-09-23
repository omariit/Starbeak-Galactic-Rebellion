// StarbeakBright.shader — bright-pass extraction for the custom bloom stack.
// Lives in Resources/ so Shader.Find("Hidden/StarbeakBright") never strips in builds.
Shader "Hidden/StarbeakBright"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Threshold ("Threshold", Range(0, 2)) = 0.75
        _Knee ("Soft knee", Range(0.001, 1)) = 0.3
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
            half _Threshold;
            half _Knee;

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
                fixed4 c = tex2D(_MainTex, i.uv);
                half lum = dot(c.rgb, half3(0.2125, 0.7154, 0.0721));

                // Soft knee bright-pass: feathered ramp centred on _Threshold.
                half contrib = lum - _Threshold + _Knee;
                half soft = clamp(contrib / (2.0 * _Knee + 0.0001), 0.0, 1.0);
                half brightness = max(contrib * soft, lum - _Threshold);

                fixed4 o;
                o.rgb = c.rgb * (brightness / max(lum, 0.0001));
                o.a = 1.0;
                return o;
            }
            ENDCG
        }
    }

    Fallback Off
}
