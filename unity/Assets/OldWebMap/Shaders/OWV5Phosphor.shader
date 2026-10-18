Shader "OldWebMap/V5/Phosphor Oscilloscope"
{
    // Drop-in replacement for OldWebMap/V3/Triggered Oscilloscope: every property the V4 runtime
    // writes (_Row, _Length, _Amplitude, _Bend, _Curl, _Twist, _Timeline, _Energy, _Constrain, _Gain)
    // keeps its meaning. Four passes draw the current atlas row and the three earlier rows as
    // phosphor persistence. The beam is a view-facing tube (constant thickness on steep slews),
    // brightness follows beam speed, and everything is real PCM from the atlas.
    Properties
    {
        _NearFadeStart ("Near fade start (m)", Float) = 2
        _NearFadeEnd ("Near fade full (m)", Float) = 4
        _WaveAtlas ("Real four-stem waveform atlas (linear RGBA)", 2D) = "gray" {}
        _Channel ("Channel (0-3)", Float) = 0
        [HDR] _Color ("Phosphor", Color) = (.35,1,.45,1)
        _Row ("Triggered row UV", Float) = .9998
        _Length ("Width in metres", Float) = 6
        _Amplitude ("Actual signal gain in metres", Float) = .7
        _Width ("Ribbon half width (V3 units)", Float) = .04
        _Bend ("Arch", Float) = 0
        _Curl ("Depth bend", Float) = 0
        _Twist ("Rotation along trace", Float) = 0
        _Timeline ("Performance time", Float) = 0
        _Energy ("Real RMS", Float) = 0
        _Constrain ("Dissolve", Float) = 0
        _Gain ("Master", Range(0,2)) = 1
        _RowStep ("Atlas row UV step per persistence frame", Float) = .000244140625
        _Persistence ("Ghost brightness kept per frame", Range(0,1)) = .5
        _GhostWiden ("Ghost width growth per frame", Float) = .2
        _SlewDim ("Beam-speed dimming", Float) = .03
        _SlewFloor ("Minimum beam brightness on steep slews", Range(0,1)) = .35
        _HaloWidth ("Total width vs V3 (halo)", Float) = 1.6
        _CoreK ("Hot-core falloff (higher = thinner)", Float) = 7
        _HotCore ("Core whitening", Range(0,1)) = .55
        _Intensity ("Overall intensity", Float) = 1.25
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend (1 add, 6 soft add)", Float) = 6
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _WaveAtlas;
    float4 _WaveAtlas_TexelSize;
    float4 _Color;
    float _Channel, _Row, _Length, _Amplitude, _Width, _Bend, _Curl, _Twist;
    float _Timeline, _Energy, _Gain, _Constrain;
    float _RowStep, _Persistence, _GhostWiden, _SlewDim, _SlewFloor, _HaloWidth, _CoreK, _HotCore, _Intensity;
    struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float beam:TEXCOORD1; float nearFade:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
    float Signal(float u, float rv)
    {
        float4 packed = tex2Dlod(_WaveAtlas, float4(lerp(.5*_WaveAtlas_TexelSize.x, 1-.5*_WaveAtlas_TexelSize.x, saturate(u)), rv, 0, 0));
        float value = _Channel < .5 ? packed.r : _Channel < 1.5 ? packed.g : _Channel < 2.5 ? packed.b : packed.a;
        return (value*255-128)/127;
    }
    float3 Curve(float u, float rv)
    {
        float twist = (u-.5)*_Twist*6.2831853;
        float y = Signal(u, rv)*_Amplitude;
        return float3((u-.5)*_Length, y*cos(twist)+sin(u*3.14159265)*_Bend, y*sin(twist)+sin(u*6.2831853)*_Curl);
    }
    float _NearFadeStart, _NearFadeEnd;
    v2f VertPass(appdata v, float k)
    {
        UNITY_SETUP_INSTANCE_ID(v);
        v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        // Earlier atlas frames have larger V; frame 0 is the floor of the history.
        float rv = min(_Row + k*_RowStep, 1 - .5*_WaveAtlas_TexelSize.y);
        float u = v.uv.x;
        float du = 1.5*_WaveAtlas_TexelSize.x;
        float3 p = Curve(u, rv);
        float3 pa = Curve(max(u-du,0), rv);
        float3 pb = Curve(min(u+du,1), rv);
        float4 c = UnityObjectToClipPos(float4(p,1));
        float4 ca = UnityObjectToClipPos(float4(pa,1));
        float4 cb = UnityObjectToClipPos(float4(pb,1));
        float p11 = abs(UNITY_MATRIX_P._m11);
        float aspect = p11/max(abs(UNITY_MATRIX_P._m00),1e-5);
        float2 d = (cb.xy/max(abs(cb.w),1e-4) - ca.xy/max(abs(ca.w),1e-4))*float2(aspect,1);
        float dl = length(d);
        d = dl > 1e-6 ? d/dl : float2(1,0);
        float2 n = float2(-d.y, d.x);
        float halfW = _Width*(.75+saturate(_Energy)*.45)*_HaloWidth*(1+_GhostWiden*k);
        // Constant projected thickness whatever the slew: offset along the screen normal.
        c.xy += n*float2(1/aspect,1)*(v.vertex.y*halfW*p11);
        o.vertex = c;
        o.uv = float2(u, v.vertex.y);
        // Beam deposits less light where it moves fast (steep slew / long path per column).
        float3 t = pb - pa;
        float tx = max(abs(t.x), 1e-5);
        float slope2 = dot(t.yz,t.yz)/(tx*tx);
        o.beam = max(_SlewFloor, rsqrt(1 + _SlewDim*slope2));
        // Regional copies that sweep past the viewer during a journey must not flood the view.
        float dist = distance(mul(unity_ObjectToWorld, float4(p,1)).xyz, _WorldSpaceCameraPos);
        o.nearFade = smoothstep(_NearFadeStart, _NearFadeEnd, dist);
        return o;
    }
    fixed4 FragPass(v2f i, float k)
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float e = abs(i.uv.y);
        float core = exp(-e*e*_CoreK);
        float halo = exp(-e*e*3.2)*.34*(1-smoothstep(.72,1,e));
        float ends = smoothstep(0,.025,i.uv.x)*smoothstep(0,.025,1-i.uv.x);
        float gap = step(.12*_Constrain, frac(i.uv.x*31 + floor(_Timeline*3)*.013));
        float w = k < .5 ? 1 : pow(max(_Persistence,.0001), k);
        float3 hot = lerp(_Color.rgb, float3(1,1,1), saturate(core*_HotCore*(1-.5*saturate(k))));
        float light = (core*1.35+halo)*ends*gap*_Gain*_Intensity*w*i.beam*i.nearFade*(.65+saturate(_Energy)*.7);
        // Soft clip keeps overlapping persistence passes and bright skies from blowing out.
        float3 rgb = 1 - exp(-hot*light);
        return fixed4(rgb,0);
    }
    ENDCG
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual Blend One [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert0
            #pragma fragment frag0
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            v2f vert0(appdata v) { return VertPass(v, 0); }
            fixed4 frag0(v2f i):SV_Target { return FragPass(i, 0); }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert1
            #pragma fragment frag1
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            v2f vert1(appdata v) { return VertPass(v, 1); }
            fixed4 frag1(v2f i):SV_Target { return FragPass(i, 1); }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert2
            #pragma fragment frag2
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            v2f vert2(appdata v) { return VertPass(v, 2); }
            fixed4 frag2(v2f i):SV_Target { return FragPass(i, 2); }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert3
            #pragma fragment frag3
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            v2f vert3(appdata v) { return VertPass(v, 3); }
            fixed4 frag3(v2f i):SV_Target { return FragPass(i, 3); }
            ENDCG
        }
    }
    Fallback Off
}
