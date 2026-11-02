// Rago Extract - leaves, needles, grass and ferns (Built-in render pipeline).
// Alpha-cutout cards, lit from both sides, with a gentle wind sway: vertex alpha says how much a vertex
// may move (0 at a branch / the ground, 1 at the tips). Lambert lighting: cheap enough for Quest.
Shader "Rago/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Leaves (RGB) Coverage (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.45
        _Wind ("Wind Strength", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Wind;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float facing : VFACE;
        };

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y;
            float sway = sin(t * 1.6 + wp.x * 0.31 + wp.z * 0.23) * 0.6 + sin(t * 3.7 + wp.x * 1.7 + wp.y * 1.1) * 0.25;
            float w = v.color.a * _Wind * 0.07;
            float3 offs = float3(sway, sway * 0.15, sway * 0.55) * w;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offs);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            fixed3 c = tex.rgb * _Color.rgb * IN.color.rgb;
            o.Albedo = c;
            o.Alpha = tex.a;
            o.Normal = float3(0, 0, IN.facing > 0 ? 1 : -1);   // both sides lit like the front
            o.Emission = c * 0.12;                               // a little light coming through the leaves
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
