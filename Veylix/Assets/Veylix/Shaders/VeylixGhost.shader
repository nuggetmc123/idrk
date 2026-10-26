// The time-trial ghost: see-through with a bright rim so it reads clearly without hiding the track.
Shader "Veylix/Ghost"
{
    Properties { _Color ("Color", Color) = (0.5,0.8,1,0.25) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float rim : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 vd = normalize(WorldSpaceViewDir(v.vertex));
                o.rim = 1 - saturate(abs(dot(n, vd)));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb, _Color.a * (0.25 + i.rim * i.rim * 2.0));
            }
            ENDCG
        }
    }
}
