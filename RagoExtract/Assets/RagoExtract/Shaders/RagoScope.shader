// Rago Extract - magnified scope eyepiece.
// Shows what the scope's own camera sees (a render texture), cut to a round field of view, with a black
// crosshair (or an illuminated dot for prism sights). Like a real scope there is an "eye box": move your eye
// off the scope's axis or too far back and a black shadow closes in from the edge.
Shader "Rago/ScopeLens"
{
    Properties
    {
        _MainTex ("View", 2D) = "black" {}
        [HDR] _DotColor ("Illuminated dot", Color) = (0,0,0,0)
        _Style ("Reticle (0 duplex crosshair, 1 mil-dot, 2 prism chevron)", Float) = 0
        _LensR ("Lens radius (m)", Float) = 0.0125
        _Active ("Image live (0 = dark glass)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _DotColor;
            float _Style, _LensR, _Active;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 opos : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.opos = v.vertex.xyz;
                return o;
            }

            float lineMask(float d, float w) { return 1 - smoothstep(w * 0.6, w, d); }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 c = (i.uv - 0.5) * 2;                    // -1..1 across the lens
                float r = length(c);
                half3 img = tex2D(_MainTex, i.uv).rgb * _Active;
                // eye box: where is this eye relative to the scope's axis?
                float3 eye = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float2 off = eye.xy / _LensR;                   // lens radii off axis
                float back = max(-eye.z, 0.0);                  // eye relief (the lens faces -Z)
                // a forgiving eye box (in VR one eye is always a few cm off the head's centre): just a soft dark edge
                // creeping in when you're well off to the side or far back - the picture is never blacked out
                float tol = 1.35 - 0.35 * saturate((back - 0.18) * 5);
                float shadow = length(c + off * 0.25);
                float vis = lerp(0.25, 1.0, 1 - smoothstep(tol - 0.35, tol, shadow));
                vis *= 1 - smoothstep(0.9, 1.0, r);
                // reticle
                float k = 0;
                float w = 0.012;
                if (_Style < 0.5)
                {
                    float thick = r > 0.45 ? 0.05 : w;          // duplex: heavy posts outside, fine wires in the middle
                    k = max(lineMask(abs(c.x), thick) * step(abs(c.y), 1), lineMask(abs(c.y), thick));
                    k *= step(r, 0.98);
                }
                else if (_Style < 1.5)
                {
                    k = max(lineMask(abs(c.x), w), lineMask(abs(c.y), w));
                    for (int n = 1; n <= 4; n++)
                    {
                        k = max(k, 1 - smoothstep(0.018, 0.026, length(c - float2(n * 0.18, 0))));
                        k = max(k, 1 - smoothstep(0.018, 0.026, length(c + float2(n * 0.18, 0))));
                        k = max(k, 1 - smoothstep(0.018, 0.026, length(c - float2(0, n * 0.18))));
                        k = max(k, 1 - smoothstep(0.018, 0.026, length(c + float2(0, n * 0.18))));
                    }
                }
                else
                {
                    float a = abs(c.x) * 1.4 + c.y;             // chevron tip at the centre
                    k = lineMask(abs(a), 0.03) * step(-0.18, c.y) * step(c.y, 0.02) * step(abs(c.x), 0.14);
                    k = max(k, lineMask(abs(c.y + 0.5), 0.014) * step(abs(c.x), 0.02));
                }
                half3 col = lerp(img, half3(0, 0, 0), k);
                col += _DotColor.rgb * (1 - smoothstep(0.025, 0.04, r)) * (_Style > 1.5 ? 0 : 1);
                col += _DotColor.rgb * k * (_Style > 1.5 ? 1 : 0);
                return half4(col * vis, 1);
            }
            ENDCG
        }
    }
}
