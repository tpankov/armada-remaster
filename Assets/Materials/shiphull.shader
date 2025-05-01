Shader "Custom/ReusableLightingShader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Tint ("Color Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows

        sampler2D _MainTex;
        fixed4 _Tint;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 col = tex2D(_MainTex, IN.uv_MainTex) * _Tint;
            o.Albedo = col.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;
        }
        ENDCG
    }
    FallBack "Standard"
}
