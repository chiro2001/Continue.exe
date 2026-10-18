Shader "OldWebMap/V5/Starfield"
{
    // One billboard quad per star (4 vertices share the centre). Per-star data:
    //   uv0 = corner (+-1,+-1), uv1 = (phase 0..1, size factor), uv2 = (twinkle Hz, brightness), colour = (tint rgb, twinkle depth).
    // _Wrap > 0 repeats the field in a cube around the camera (near/mid layers travel with the world frame, the
    // camera wraps through them). _Velocity = viewer velocity in object space (m/s, = derivative of the virtual
    // position); stars move the opposite way in the world and are stretched into a trail behind them.
    // All animation uses _Timeline (performance clock), never _Time.
    Properties
    {
        _Timeline ("Performance time", Float) = 0
        _Intensity ("Layer intensity", Float) = 1
        _Tint ("Layer tint", Color) = (1,1,1,1)
        _Velocity ("Viewer velocity (xyz m/s, w speed)", Vector) = (0,0,0,0)
        _Wrap ("Cube period (0 = fixed)", Float) = 0
        _Fade ("Near in0,in1 / far out0,out1", Vector) = (0,1,100000,200000)
        _AngSize ("Half size (radians)", Float) = 0.002
        _Trail ("Trail seconds", Float) = 0
        _StreakMax ("Max streak / depth", Float) = 0.4
        _Boost ("Brightness gain at full speed", Float) = 1
        _SpeedRef ("Full speed (m/s)", Float) = 20
        _Twinkle ("Twinkle scale", Range(0,1)) = 1
        _FloorY ("Floor height", Float) = -0.02
        _BelowFloor ("Brightness below floor", Range(0,1)) = 0.4
        _MinPixels ("Minimum half size in pixels", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent-5" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            float _Timeline, _Intensity, _Wrap, _AngSize, _Trail, _StreakMax, _Boost, _SpeedRef, _Twinkle, _FloorY, _BelowFloor, _MinPixels;
            float4 _Tint, _Velocity, _Fade;
            struct appdata { float4 vertex:POSITION; float2 corner:TEXCOORD0; float2 rnd:TEXCOORD1; float2 rnd2:TEXCOORD2; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float3 col:TEXCOORD0; float3 prof:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = float4(0,0,2,1);
                float3 cam = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz;
                float3 d = v.vertex.xyz - cam;
                if (_Wrap > 0) d -= _Wrap * floor(d / _Wrap + 0.5);
                float3 c = cam + d;
                float dist = length(d);
                float cheb = max(max(abs(d.x), abs(d.y)), abs(d.z));
                float fade = smoothstep(_Fade.x, _Fade.y, dist) * (1.0 - smoothstep(_Fade.z, _Fade.w, cheb));
                float3 wA = mul(unity_ObjectToWorld, float4(c,1)).xyz;
                float3 toA = wA - _WorldSpaceCameraPos;
                float depth = -mul(UNITY_MATRIX_V, float4(wA,1)).z;
                if (depth < 0.05 || fade <= 0.002) return o;

                // Motion: the frame moves opposite to the viewer, so stars travel with -_Velocity. Trail extends back along their path.
                float3 u = mul((float3x3)unity_ObjectToWorld, -_Velocity.xyz);
                float speed = length(u);
                float len = min(speed * _Trail, _StreakMax * depth);
                float3 dirT = speed > 1e-3 ? u / speed : float3(0,0,0);
                float3 wB = wA - dirT * len;

                float raw = _AngSize * v.rnd.y * depth;
                float hw = max(raw, _MinPixels * 2.0 * depth / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y));
                float3 camR = UNITY_MATRIX_V[0].xyz;
                float3 camU = UNITY_MATRIX_V[1].xyz;
                float3 ax = len > 1e-3 ? dirT : camU;
                float3 sd = cross(ax, toA);
                float sl = length(sd);
                sd = (len > 1e-3 && sl > 1e-3 * length(toA)) ? sd / sl : camR;
                float3 axp = normalize(cross(sd, toA));
                axp *= dot(axp, ax) >= 0 ? 1.0 : -1.0;
                float head = v.corner.y > 0 ? 1.0 : 0.0;
                float3 p = (head > 0.5 ? wA + axp * hw : wB - axp * hw) + sd * (v.corner.x * hw);
                o.vertex = mul(UNITY_MATRIX_VP, float4(p,1));
                float lenN = len > 1e-3 ? max(dot(wA - wB, axp), 0) / hw : 0;
                o.prof = float3(v.corner.x, head > 0.5 ? -1.0 : lenN + 1.0, lenN);

                float rate = v.rnd2.x;
                float s1 = 0.5 + 0.5 * sin(6.2831853 * (_Timeline * rate + v.rnd.x));
                float s2 = 0.5 + 0.5 * sin(6.2831853 * (_Timeline * rate * 2.37 + v.rnd.x * 3.1 + 0.3));
                float tw = 1.0 - v.color.a * _Twinkle * (0.55 * s1 + 0.45 * s2);
                float speedN = saturate(_Velocity.w / max(_SpeedRef, 1e-3));
                float above = smoothstep(_FloorY - 0.04 * dist, _FloorY + 0.04 * dist, wA.y);
                float bright = v.rnd2.y * tw * fade * (1.0 + _Boost * speedN) * lerp(_BelowFloor, 1.0, above);
                bright *= saturate(raw / hw) * rsqrt(1.0 + 0.25 * max(lenN, 0));
                o.col = v.color.rgb * _Tint.rgb * (bright * _Intensity);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float dx = max(max(-i.prof.y, i.prof.y - i.prof.z), 0);
                float r2 = dx * dx + i.prof.x * i.prof.x;
                float core = max(exp(-r2 * 4.0) - 0.0183, 0) * 1.0187;
                float s = saturate(i.prof.y / max(i.prof.z, 1e-3));
                float trail = lerp(1.0, 0.1 + 0.9 * (1.0 - s) * (1.0 - s), saturate(i.prof.z * 0.5));
                return fixed4(i.col * (core * trail), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
