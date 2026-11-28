// Rago Extract - a real mirror for VR (3.55). Each eye gets its own reflection, rendered by Mirror.cs into _LeftTex /
// _RightTex; this samples the one for the eye being drawn at the pixel's screen position, so the reflection has proper
// depth in a headset instead of looking like a flat picture. Single-pass instanced / multiview aware.
Shader "Rago/Mirror"
{
    Properties
    {
        _LeftTex ("Left eye", 2D) = "black" {}
        _RightTex ("Right eye", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.93, 0.95, 0.97, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _LeftTex;
            sampler2D _RightTex;
            half4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 sp : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.sp = ComputeNonStereoScreenPos(o.pos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.sp.xy / i.sp.w;
                half4 c;
            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                if (unity_StereoEyeIndex == 0) c = tex2D(_LeftTex, uv); else c = tex2D(_RightTex, uv);
            #else
                c = tex2D(_LeftTex, uv);
            #endif
                c *= _Tint;
                c.a = 1;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
