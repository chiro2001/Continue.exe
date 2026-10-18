Shader "OldWebMap/V5/Grid Floor"
{
    // Stable foot reference: world-space grid (never follows the journey). Lines are analytically anti-aliased,
    // fade out with distance from the camera (no visible disc edge) and by screen footprint (no moire).
    // Premultiplied output: line/fill alpha-blend, the focus "light pool" is purely additive.
    Properties
    {
        _Timeline ("Performance time", Float) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _Tint ("Line colour", Color) = (0.2,0.3,0.4,1)
        _LineAlpha ("Minor line alpha", Range(0,1)) = 0.32
        _MajorAlpha ("Major line alpha", Range(0,1)) = 0.62
        _LineWidth ("Minor line width (px)", Float) = 1.1
        _MajorWidth ("Major line width (px)", Float) = 1.8
        _Cell ("Cell (m)", Float) = 1
        _Major ("Major every (cells)", Float) = 8
        _FadeStart ("Fade start (m)", Float) = 8
        _FadeEnd ("Fade end (m)", Float) = 40
        _FillAlpha ("Floor darkening", Range(0,1)) = 0.1
        _Shimmer ("Shimmer", Range(0,0.5)) = 0.12
        _PoolPos ("Light pool centre (world)", Vector) = (0,0,0,0)
        _PoolColor ("Light pool colour", Color) = (0,0,0,0)
        _PoolRadius ("Light pool radius", Float) = 3.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
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
            float _Timeline, _Opacity, _LineAlpha, _MajorAlpha, _LineWidth, _MajorWidth, _Cell, _Major, _FadeStart, _FadeEnd, _FillAlpha, _Shimmer, _PoolRadius;
            float4 _Tint, _PoolPos, _PoolColor;
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
            float lineCover(float2 p, float2 fw, float cell, float width, float lodLo, float lodHi)
            {
                float2 q = p / cell;
                float2 f = fw / cell;
                float2 g = abs(frac(q - 0.5) - 0.5) / max(f, 1e-5);
                float2 c = saturate(width * 0.5 + 0.5 - g) * (1.0 - smoothstep(lodLo, lodHi, f));
                return max(c.x, c.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p = i.wpos.xz;
                float2 fw = fwidth(p);
                float d = length(p - _WorldSpaceCameraPos.xz);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, d);
                fade *= fade;
                float2 pd = p - _PoolPos.xz;
                float pr = dot(pd, pd) / max(_PoolRadius * _PoolRadius, 1e-4);
                float pool = exp(-pr * 3.0) * (1.0 - smoothstep(0.55, 1.0, pr));
                if (fade <= 0.0005 && pool <= 0.0005) return fixed4(0,0,0,0);

                float minor = lineCover(p, fw, _Cell, _LineWidth, 0.22, 0.55);
                float major = lineCover(p, fw, _Cell * _Major, _MajorWidth, 0.22, 0.55);
                float2 cell = floor(p / _Cell + 0.5);
                float h = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                float2 off = p - cell * _Cell;
                float sparkle = pow(0.5 + 0.5 * sin(_Timeline * (0.8 + h * 1.4) + h * 43.0), 8.0) * step(0.72, h);
                float node = exp(-dot(off, off) * 900.0) * sparkle * (1.0 - smoothstep(0.15, 0.4, max(fw.x, fw.y)));
                float wave = 0.5 + 0.5 * sin(d * 0.5 - _Timeline * 1.1);
                float shimmer = 1.0 + _Shimmer * (wave - 0.5) * 2.0;
                float lineA = saturate((max(minor * _LineAlpha, major * _MajorAlpha) * shimmer + node * 0.8) * fade * _Opacity);
                float fillA = _FillAlpha * fade * _Opacity;
                float alpha = lineA + fillA * (1.0 - lineA);
                float3 rgb = _Tint.rgb * lineA + _Tint.rgb * 0.04 * fillA * (1.0 - lineA);
                rgb += _PoolColor.rgb * pool * smoothstep(0.0, 0.3, _Opacity);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
