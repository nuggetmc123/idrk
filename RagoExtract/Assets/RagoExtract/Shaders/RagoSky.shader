// Rago Extract - gradient skybox with sun disc, horizon haze and soft procedural clouds.
Shader "Rago/Sky"
{
    Properties
    {
        _Top ("Zenith", Color) = (0.22,0.35,0.55,1)
        _Horizon ("Horizon", Color) = (0.75,0.72,0.66,1)
        _Bottom ("Ground", Color) = (0.18,0.17,0.16,1)
        _SunColor ("Sun", Color) = (1,0.85,0.6,1)
        _SunDir ("Sun Direction", Vector) = (0.3,0.4,0.5,0)
        _SunSize ("Sun Size", Float) = 0.9995
        _Clouds ("Cloud Amount", Range(0,1)) = 0.4
        _CloudColor ("Cloud Color", Color) = (0.9,0.9,0.92,1)
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
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Top, _Horizon, _Bottom, _SunColor, _CloudColor;
            float4 _SunDir;
            float _SunSize, _Clouds;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

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

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            float fbm(float2 p) { float s = 0, a = 0.5; for (int k = 0; k < 4; k++) { s += noise(p) * a; p *= 2.07; a *= 0.5; } return s; }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 c = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(y), 0.55)) : lerp(_Horizon.rgb, _Bottom.rgb, saturate(-y * 4));
                float3 sd = normalize(_SunDir.xyz);
                float sdot = dot(d, sd);
                c += _SunColor.rgb * smoothstep(_SunSize, _SunSize + 0.0004, sdot) * 6;
                c += _SunColor.rgb * pow(saturate(sdot), 64) * 0.5 + _SunColor.rgb * pow(saturate(sdot), 6) * 0.18;
                if (y > 0.01)
                {
                    float2 uv = d.xz / (y + 0.12) * 1.3 + _Time.x * float2(0.15, 0.05);
                    float n = fbm(uv);
                    float cl = smoothstep(1 - _Clouds, 1.05 - _Clouds * 0.5, n) * saturate(y * 6);
                    half3 cc = _CloudColor.rgb * (0.75 + 0.35 * pow(saturate(sdot), 3));
                    c = lerp(c, cc, cl * 0.85);
                }
                return half4(c, 1);
            }
            ENDCG
        }
    }
}
