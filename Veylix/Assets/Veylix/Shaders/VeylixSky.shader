// The sky: a colour gradient from horizon to zenith, glow around the sun, the sun and moon discs, stars at night,
// and two layers of drifting clouds lit from the sun's side. Every colour and amount is set by Sky.cs as the time of
// day and the weather change. Works in VR (single-pass instanced and multi-pass).
Shader "Veylix/Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.18,0.36,0.72,1)
        _Horizon ("Horizon", Color) = (0.62,0.75,0.9,1)
        _Ground ("Below the horizon", Color) = (0.32,0.33,0.34,1)
        _SunDir ("Sun direction (towards the sun)", Vector) = (0.3,0.6,0.7,0)
        _SunColor ("Sun colour", Color) = (1,0.95,0.88,1)
        _SunCos ("Sun disc size (cosine of its radius)", Float) = 0.99985
        _SunGlow ("Sun glow", Float) = 1
        _MoonDir ("Moon direction", Vector) = (-0.3,0.5,-0.7,0)
        _MoonColor ("Moon colour", Color) = (0.8,0.85,0.95,1)
        _Night ("Night (stars)", Range(0,1)) = 0
        _CloudTex ("Cloud noise", 2D) = "gray" {}
        _CloudCover ("Cloud cover", Range(0,1)) = 0.3
        _CloudLit ("Cloud colour (sun side)", Color) = (1,1,1,1)
        _CloudShade ("Cloud colour (shadow)", Color) = (0.62,0.66,0.72,1)
        _CloudScale ("Cloud scale", Float) = 0.35
        _CloudSpeed ("Cloud drift", Vector) = (0.004,0.0015,0,0)
        _CloudOffset ("Cloud offset", Vector) = (0,0,0,0)
        _Haze ("Horizon haze", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            half4 _Zenith, _Horizon, _Ground, _SunColor, _MoonColor, _CloudLit, _CloudShade;
            float4 _SunDir, _MoonDir, _CloudSpeed, _CloudOffset;
            float _SunCos, _SunGlow, _Night, _CloudCover, _CloudScale, _Haze;
            sampler2D _CloudTex;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                float h = d.y;
                float up = saturate(h);

                // Gradient, and a soft fade to the ground colour below the horizon.
                float3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(up, 0.5));
                col = lerp(col, _Ground.rgb, saturate(-h * 5.0));

                // Glow around the sun (wide and warm near the horizon at sunrise and sunset).
                float3 sunDir = normalize(_SunDir.xyz);
                float sd = dot(d, sunDir);
                float s01 = saturate(sd);
                col += _SunColor.rgb * (pow(s01, 8.0) * 0.12 + pow(s01, 64.0) * 0.3) * _SunGlow;

                // Stars: one in a few hundred cells of a fine grid on the sky, twinkling a little.
                float3 sp = d * 260.0;
                float3 cell = floor(sp);
                float r = hash13(cell);
                float3 f = frac(sp) - 0.5;
                float star = step(0.986, r) * smoothstep(0.22, 0.0, length(f)) * (0.6 + 0.4 * sin(_Time.y * (2.0 + r * 5.0) + r * 40.0));
                star *= _Night * smoothstep(0.02, 0.2, h);

                // Clouds: two noise layers projected onto a dome, drifting with the wind.
                float2 uv = d.xz / max(h + 0.14, 0.06) * _CloudScale + _CloudOffset.xy + _Time.y * _CloudSpeed.xy;
                float n = tex2D(_CloudTex, uv).r * 0.62 + tex2D(_CloudTex, uv * 2.7 + 0.37).r * 0.28 + tex2D(_CloudTex, uv * 6.3 - 0.21).r * 0.1;
                n = saturate((n - 0.28) / 0.44); // stretch the noise to the full 0..1 range
                float edge = 1.0 - _CloudCover;
                float c = smoothstep(edge - 0.08, edge + 0.22, n) * smoothstep(-0.02, 0.1, h);
                c = saturate(c * (0.6 + _CloudCover * 0.6));
                float3 cloud = lerp(_CloudShade.rgb, _CloudLit.rgb, saturate((n - edge) * 2.2 + 0.25));
                cloud += _SunColor.rgb * pow(s01, 5.0) * 0.45 * _SunGlow;

                // Sun and moon discs, behind the clouds.
                float sunDisc = smoothstep(_SunCos, _SunCos + (1.0 - _SunCos) * 0.35, sd) * smoothstep(-0.02, 0.02, h);
                sunDisc *= saturate((0.85 - _CloudCover) * 4.0); // gone behind a closed cloud sheet
                float md = dot(d, normalize(_MoonDir.xyz));
                float moonDisc = smoothstep(0.99975, 0.99985, md) * smoothstep(-0.02, 0.02, h);
                col += _SunColor.rgb * sunDisc * 6.0 + _MoonColor.rgb * moonDisc * _Night + star;

                col = lerp(col, cloud, c);

                // Haze where the sky meets the land.
                col = lerp(col, _Horizon.rgb, exp(-abs(h) * 14.0) * _Haze * 0.6);
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
