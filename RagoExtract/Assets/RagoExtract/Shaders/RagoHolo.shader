// Rago Extract - holographic / red-dot sight lens.
// The reticle is projected at infinity: it is computed from the per-eye view direction in the
// sight's local space, so it stays on target while your head moves (no fake "painted" dot).
Shader "Rago/HoloReticle"
{
    Properties
    {
        [HDR] _ReticleColor ("Reticle Color", Color) = (4,0.2,0.15,1)
        _GlassTint ("Glass Tint", Color) = (0.25,0.45,0.6,0.12)
        _Style ("Style (0 ring+dot,1 dot,2 chevron,3 triangle,4 cross)", Float) = 0
        _Size ("Reticle Size (radians)", Float) = 0.012
        _Thickness ("Line Thickness (radians)", Float) = 0.0011
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            half4 _ReticleColor, _GlassTint;
            float _Style, _Size, _Thickness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                return o;
            }

            float seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 viewW = normalize(i.wpos - _WorldSpaceCameraPos);
                float3 d = normalize(mul((float3x3)unity_WorldToObject, viewW));
                float2 p = d.xy / max(d.z, 1e-3);          // tangent-space angle
                float s = _Size, t = _Thickness;
                float dist = 1e3;
                int style = (int)(_Style + 0.5);
                float dotR = t * 1.15;
                float dDot = length(p) - dotR;
                if (style == 0)      dist = min(abs(length(p) - s) - t * 0.5, dDot);
                else if (style == 1) dist = length(p) - t * 1.6;
                else if (style == 2) dist = min(seg(p, float2(-s*0.55, -s*0.55), float2(0,0)), seg(p, float2(s*0.55, -s*0.55), float2(0,0))) - t * 0.5;
                else if (style == 3)
                {
                    float a = seg(p, float2(0, 0), float2(-s*0.45, -s*0.75));
                    float b = seg(p, float2(0, 0), float2(s*0.45, -s*0.75));
                    float c = seg(p, float2(-s*0.45, -s*0.75), float2(s*0.45, -s*0.75));
                    dist = min(min(a,b),c) - t * 0.45;
                }
                else
                {
                    float h = min(seg(p, float2(-s, 0), float2(-s*0.3, 0)), seg(p, float2(s*0.3, 0), float2(s, 0)));
                    float v = min(seg(p, float2(0, -s), float2(0, -s*0.3)), seg(p, float2(0, s*0.3), float2(0, s)));
                    dist = min(min(h, v) - t * 0.4, dDot);
                }
                float aa = max(fwidth(dist), 1e-5);
                float r = 1 - smoothstep(-aa, aa, dist);
                float glow = exp(-max(dist, 0) / (t * 1.8)) * 0.25;
                // fade reticle when looking through the glass at a steep angle (realistic window clipping)
                float facing = saturate((d.z - 0.96) * 30);
                half4 col = _ReticleColor * (r + glow) * facing;
                // subtle coated glass tint + edge reflection
                float2 e = abs(i.uv - 0.5) * 2;
                float edge = pow(saturate(max(e.x, e.y)), 6);
                half3 glass = _GlassTint.rgb * (_GlassTint.a + edge * 0.35);
                half a = saturate(col.a * (r + glow) + _GlassTint.a + edge * 0.2);
                return half4(col.rgb + glass, a * 0.5);
            }
            ENDCG
        }
    }
}
