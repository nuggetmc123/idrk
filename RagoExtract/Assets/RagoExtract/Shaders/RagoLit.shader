// Rago Extract - main lit surface shader (Built-in render pipeline).
// No shader keywords on purpose: every variant is always compiled, so materials created at
// runtime look identical in the editor and in a Quest build.
Shader "Rago/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _DetailTex ("Grime / Detail (R=grime G=scratch B=ao)", 2D) = "gray" {}
        _Grime ("Grime Amount", Range(0,1)) = 0.35
        _Scratch ("Scratch Amount", Range(0,1)) = 0.3
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.4
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Triplanar ("Triplanar World Mapping", Float) = 0
        _TriScale ("Triplanar Scale (tiles per metre)", Float) = 0.5
        _VColor ("Use Vertex Color", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap, _DetailTex;
        fixed4 _Color;
        half _BumpScale, _Grime, _Scratch, _Metallic, _Glossiness, _Triplanar, _TriScale, _VColor;
        half4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            float4 color : COLOR;
            INTERNAL_DATA
        };

        float4 Tri(sampler2D tex, float3 p, float3 n)
        {
            float3 w = pow(abs(n), 4);
            w /= (w.x + w.y + w.z + 1e-4);
            return tex2D(tex, p.zy) * w.x + tex2D(tex, p.xz) * w.y + tex2D(tex, p.xy) * w.z;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = WorldNormalVector(IN, float3(0,0,1));
            float3 tp = IN.worldPos * _TriScale;
            fixed4 albedo;
            fixed4 detail;
            if (_Triplanar > 0.5)
            {
                albedo = Tri(_MainTex, tp, wn);
                detail = Tri(_DetailTex, tp * 0.37, wn);
            }
            else
            {
                albedo = tex2D(_MainTex, IN.uv_MainTex);
                detail = tex2D(_DetailTex, IN.uv_MainTex * 0.73 + IN.worldPos.xz * 0.05);
            }
            fixed3 vc = lerp(fixed3(1,1,1), IN.color.rgb, _VColor);
            fixed3 c = albedo.rgb * _Color.rgb * vc;
            // grime darkens and desaturates, scratches brighten edges of metal/paint
            half grime = saturate((detail.r - 0.45) * 2.2) * _Grime;
            c = lerp(c, c * fixed3(0.55, 0.5, 0.45), grime);
            half scratch = saturate((detail.g - 0.72) * 4.0) * _Scratch;
            c = lerp(c, fixed3(0.62, 0.6, 0.57), scratch * 0.6);
            half ao = lerp(1, detail.b * 1.4, 0.35);
            o.Albedo = c * ao;
            if (_Triplanar < 0.5)
            {
                fixed3 nrm = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
                nrm.xy *= _BumpScale;
                o.Normal = normalize(nrm);
            }
            o.Metallic = saturate(_Metallic + scratch * 0.4);
            o.Smoothness = saturate(_Glossiness * (1 - grime * 0.8) + scratch * 0.2) * albedo.a;
            o.Emission = _EmissionColor.rgb;
            o.Occlusion = ao * lerp(1, IN.color.a, _VColor);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
