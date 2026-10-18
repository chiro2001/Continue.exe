Shader "OldWebMap/V3/Triggered Oscilloscope"
{
    Properties
    {
        _WaveAtlas ("Real four-stem waveform atlas (linear RGBA)", 2D) = "gray" {}
        _Channel ("Channel (0-3)", Float) = 0
        [HDR] _Color ("Phosphor", Color) = (.25,.85,1,1)
        _Row ("Triggered row UV", Float) = .9998
        _Length ("Width in metres", Float) = 6
        _Amplitude ("Actual signal gain in metres", Float) = .7
        _Width ("Ribbon half width", Float) = .033
        _Bend ("Arch", Float) = 0
        _Curl ("Depth bend", Float) = 0
        _Twist ("Rotation along trace", Float) = 0
        _Timeline ("Performance time", Float) = 0
        _Energy ("Real RMS", Float) = 0
        _Constrain ("Dissolve", Float) = 0
        _Gain ("Master", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            sampler2D _WaveAtlas;
            float4 _WaveAtlas_TexelSize;
            float4 _Color;
            float _Channel, _Row, _Length, _Amplitude, _Width, _Bend, _Curl, _Twist;
            float _Timeline, _Energy, _Gain, _Constrain;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float energy:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float signal(float u)
            {
                float4 packed = tex2Dlod(_WaveAtlas, float4(lerp(.5*_WaveAtlas_TexelSize.x,1-.5*_WaveAtlas_TexelSize.x,u),_Row,0,0));
                float value = _Channel < .5 ? packed.r : _Channel < 1.5 ? packed.g : _Channel < 2.5 ? packed.b : packed.a;
                return (value*255-128)/127;
            }
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o; UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float u = v.uv.x;
                float actual = signal(u);
                float twist = (u-.5)*_Twist*6.2831853;
                float y = actual*_Amplitude;
                float3 p = float3((u-.5)*_Length,
                    y*cos(twist)+sin(u*3.14159265)*_Bend,
                    y*sin(twist)+sin(u*6.2831853)*_Curl);
                // The static ribbon provides a physically thick emissive trace.
                // Never synthesise the waveform; all displacements above use PCM.
                float width = _Width*(.75+saturate(_Energy)*.45);
                p.y += v.vertex.y * width;
                o.vertex = UnityObjectToClipPos(float4(p,1));
                o.uv = float2(u,v.vertex.y); o.energy = abs(actual);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float edge = abs(i.uv.y);
                float core = exp(-edge*edge*38);
                float halo = pow(saturate(1-edge),1.8)*.23;
                float ends = smoothstep(0,.025,i.uv.x)*smoothstep(0,.025,1-i.uv.x);
                float gap = step(.12*_Constrain,frac(i.uv.x*31 + floor(_Timeline*3)*.013));
                float light = (core*1.7+halo)*ends*gap*_Gain;
                return fixed4(_Color.rgb*light*(.65+saturate(_Energy)*.7),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
