Shader "OldWebMap/V5/Focus Halo"
{
    // Soft additive glow quad, 1.35x the focus image and 5 cm behind it. The quad uv maps to [-1,1];
    // the image occupies |uv| < _Inner, the glow falls to zero at the quad border (rounded corners).
    // The opaque image occludes the middle through the depth test, so only the surrounding glow shows.
    Properties
    {
        _Color ("Glow colour", Color) = (0.5,0.7,1,1)
        _Intensity ("Envelope", Float) = 0
        _Inner ("Image half extent in quad uv", Range(0.1,0.95)) = 0.7407
        _Power ("Falloff power", Range(0.5,4)) = 1.8
        _Timeline ("Performance time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual Blend One One
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            float4 _Color;
            float _Intensity, _Inner, _Power, _Timeline;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 ex = saturate((abs(i.uv) - _Inner) / max(1.0 - _Inner, 1e-3));
                float t = length(ex);
                float g = pow(saturate(1.0 - t), _Power);
                // Slow deterministic breathing so the glow is not a flat sticker.
                float breath = 0.95 + 0.05 * sin(_Timeline * 1.9);
                return fixed4(_Color.rgb * (g * _Intensity * breath), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
