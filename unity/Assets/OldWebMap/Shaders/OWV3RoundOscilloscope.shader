Shader "OldWebMap/V3/Round Oscilloscope"
{
    Properties
    {
        _WaveAtlas("PCM waveform atlas",2D)="gray"{} _Channel("Channel",Float)=0
        [HDR]_Color("Phosphor",Color)=(.25,.85,1,1)
        _Row("Row",Float)=.9998 _Length("Length",Float)=6 _Amplitude("Amplitude",Float)=.7
        _Width("Round glow radius",Float)=.045 _Bend("Arch",Float)=0 _Curl("Depth bend",Float)=0 _Twist("Twist",Float)=0
        _Timeline("Time",Float)=0 _Energy("RMS",Float)=0 _Constrain("Dissolve",Float)=0 _Gain("Gain",Float)=1
        _NearFadeStart("Near fade start",Float)=.45 _NearFadeEnd("Near fade end",Float)=1.2
    }
    SubShader
    {
        Tags{"Queue"="Transparent+20" "RenderType"="Transparent"}
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
            sampler2D _WaveAtlas;float4 _WaveAtlas_TexelSize,_Color;
            float _Channel,_Row,_Length,_Amplitude,_Width,_Bend,_Curl,_Twist,_Timeline,_Energy,_Gain,_Constrain,_NearFadeStart,_NearFadeEnd;
            struct appdata{float4 vertex:POSITION;float4 info:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f{float4 vertex:SV_POSITION;float facing:TEXCOORD0;float envelope:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            float signal(float u){float4 p=tex2Dlod(_WaveAtlas,float4(lerp(.5*_WaveAtlas_TexelSize.x,1-.5*_WaveAtlas_TexelSize.x,u),_Row,0,0));return ((_Channel<.5?p.r:_Channel<1.5?p.g:_Channel<2.5?p.b:p.a)*255-128)/127;}
            float3 centre(float u){float y=signal(u)*_Amplitude,a=(u-.5)*_Twist*6.2831853;return float3((u-.5)*_Length,y*cos(a)+sin(u*3.14159265)*_Bend,y*sin(a)+sin(u*6.2831853)*_Curl);}
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p0=centre(v.info.x),p1=centre(v.info.y),axis=normalize(p1-p0+float3(.000001,0,0));
                float3 side=normalize(cross(axis,abs(axis.y)>.93?float3(0,0,1):float3(0,1,0)));float3 up=cross(side,axis);
                float radius=_Width*(.80+saturate(_Energy)*.30);
                // Every real PCM segment has a circular section and hemispheric
                // ends. The overlapping rounded joints glow in all directions.
                float3 radial=side*v.vertex.x+up*v.vertex.y+axis*v.info.w;
                float3 p=lerp(p0,p1,v.info.z)+radial*radius;
                float3 world=mul(unity_ObjectToWorld,float4(p,1)).xyz;
                float3 normal=UnityObjectToWorldNormal(normalize(radial+float3(.000001,0,0)));
                float3 view=normalize(_WorldSpaceCameraPos-world);
                o.facing=saturate(abs(dot(normal,view)));
                float u=lerp(v.info.x,v.info.y,v.info.z);float end=smoothstep(0,.02,u)*smoothstep(0,.02,1-u);
                float gap=step(.12*_Constrain,frac(u*31+floor(_Timeline*3)*.013));
                float near=smoothstep(_NearFadeStart,_NearFadeEnd,distance(world,_WorldSpaceCameraPos));
                o.envelope=end*gap*near*_Gain;o.vertex=UnityObjectToClipPos(float4(p,1));return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float f=saturate(i.facing),core=pow(f,16)*1.8,halo=pow(f,1.5)*.22;
                return fixed4(_Color.rgb*(core+halo)*i.envelope*(.65+saturate(_Energy)*.7),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
