// Sea and harbour water: two scrolling normal maps in world space, glossy Standard lighting so it reflects the sky.
Shader "Veylix/Water"
{
    Properties
    {
        _Color ("Deep colour", Color) = (0.03,0.16,0.22,1)
        _Shallow ("Shallow colour", Color) = (0.08,0.3,0.32,1)
        _BumpMap ("Waves (normal map)", 2D) = "bump" {}
        _Scale ("Wave scale", Float) = 0.035
        _Speed ("Wave speed", Float) = 0.6
        _Glossiness ("Smoothness", Range(0,1)) = 0.93
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _BumpMap;
        fixed4 _Color, _Shallow;
        half _Scale, _Speed, _Glossiness;
        struct Input { float3 worldPos; float3 viewDir; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz * _Scale;
            float t = _Time.y * _Speed * 0.02;
            float3 n1 = UnpackNormal(tex2D(_BumpMap, uv + t * float2(1.0, 0.6)));
            float3 n2 = UnpackNormal(tex2D(_BumpMap, uv * 2.3 - t * float2(0.4, 1.3)));
            float3 n3 = UnpackNormal(tex2D(_BumpMap, uv * 0.25 + t * float2(-0.3, 0.2)));
            o.Normal = normalize(float3(n1.xy + n2.xy * 0.6 + n3.xy * 0.8, n1.z * n2.z * n3.z));
            float facing = saturate(dot(normalize(IN.viewDir), float3(0, 0, 1)));
            o.Albedo = lerp(_Color.rgb, _Shallow.rgb, facing * 0.4);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Standard"
}
