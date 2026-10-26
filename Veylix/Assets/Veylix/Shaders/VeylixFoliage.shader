// Leaves and needles: alpha-cut, double-sided, lit with the crown-shaped normals baked into the mesh, a soft vertex
// AO from the mesh colours, gentle wind sway, GPU instancing.
Shader "Veylix/Foliage"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Leaves (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.45
        _Wind ("Wind", Range(0,1)) = 0.25
        _Translucency ("Translucency", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Foliage alphatest:_Cutoff vertex:vert addshadow
        #pragma multi_compile_instancing
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Wind, _Translucency;
        struct Input { float2 uv_MainTex; fixed4 color : COLOR; };

        half4 LightingFoliage (SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half diff = saturate(ndl * 0.65 + 0.35);            // wrapped: leaves never go fully black
            half back = saturate(-ndl) * _Translucency;          // light shining through from behind
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (diff + back) * atten;
            c.a = s.Alpha;
            return c;
        }

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
            float h = saturate(v.vertex.y * 0.08);
            float t = _Time.y * 1.3 + wp.x * 0.11 + wp.z * 0.07;
            v.vertex.x += sin(t) * _Wind * 0.08 * h;
            v.vertex.z += sin(t * 0.73 + 1.7) * _Wind * 0.06 * h;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb * IN.color.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
