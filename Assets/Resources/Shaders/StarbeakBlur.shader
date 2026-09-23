// StarbeakBlur.shader — 3-sample linearly-optimized 5-tap gaussian blur.
// Direction passed as _BlurDir (already scaled by texel size from C#) so no branching
// is needed to pick horizontally vs vertically. Mobile-friendly, half precision.
Shader "Hidden/StarbeakBlur"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _BlurDir ("Blur direction (XY, texel-scaled)", Vector) = (0, 0, 0, 0)
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
            half4 _BlurDir;

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
                float2 o1 = i.uv + _BlurDir.xy * 1.3846153846;
                float2 o2 = i.uv - _BlurDir.xy * 1.3846153846;
                float2 o3 = i.uv + _BlurDir.xy * 3.2307692308;
                float2 o4 = i.uv - _BlurDir.xy * 3.2307692308;

                fixed4 c = tex2D(_MainTex, i.uv) * 0.2270270270h;
                c += (tex2D(_MainTex, o1) + tex2D(_MainTex, o2)) * 0.3162162162h;
                c += (tex2D(_MainTex, o3) + tex2D(_MainTex, o4)) * 0.0702702703h;
                c.a = 1.0h;
                return c;
            }
            ENDCG
        }
    }

    Fallback Off
}
