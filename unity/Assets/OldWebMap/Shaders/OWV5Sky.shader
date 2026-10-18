Shader "OldWebMap/V5/Sky"
{
    // Inside-facing dome. Direction comes from the camera ray, so the gradient stays correct for any viewer position.
    // Colours are set by OWV5Atmosphere from the colour script; ordered-noise dither hides 8-bit banding in the dark ramp.
    Properties
    {
        _TopColor ("Zenith", Color) = (0.012,0.018,0.024,1)
        _HorizonColor ("Horizon", Color) = (0.040,0.070,0.078,1)
        _GroundColor ("Below horizon haze", Color) = (0.022,0.039,0.043,1)
        _Curve ("Zenith curve", Range(0.2,1.5)) = 0.5
        _HazeGain ("Horizon haze", Range(0,1)) = 0.3
        _HazeFalloff ("Haze falloff", Range(2,40)) = 11
        _Dither ("Dither (8-bit levels)", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" "PreviewType"="Skybox" }
        Cull Back ZWrite Off ZTest LEqual
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
            float4 _TopColor, _HorizonColor, _GroundColor;
            float _Curve, _HazeGain, _HazeFalloff, _Dither;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float3 wpos:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float e = normalize(i.wpos - _WorldSpaceCameraPos).y;
                float up = pow(saturate(e), _Curve);
                float dn = pow(saturate(-e), 0.5);
                float3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, smoothstep(0, 1, up));
                float3 ground = lerp(_HorizonColor.rgb, _GroundColor.rgb, smoothstep(0, 1, dn));
                float3 col = e >= 0 ? sky : ground;
                col += _HorizonColor.rgb * (exp(-abs(e) * _HazeFalloff) * _HazeGain);
                // Interleaved gradient noise remapped to a triangular PDF, +-1 LSB of the output encoding.
                float n = frac(52.9829189 * frac(dot(i.vertex.xy, float2(0.06711056, 0.00583715))));
                float tri = n < 0.5 ? sqrt(2.0 * n) - 1.0 : 1.0 - sqrt(2.0 - 2.0 * n);
                #if defined(UNITY_COLORSPACE_GAMMA)
                col = max(col + tri * _Dither * (1.0 / 255.0), 0);
                #else
                col = GammaToLinearSpace(max(LinearToGammaSpace(col) + tri * _Dither * (1.0 / 255.0), 0));
                #endif
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
