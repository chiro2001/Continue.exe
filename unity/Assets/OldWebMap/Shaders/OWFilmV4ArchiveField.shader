Shader "OldWebMap/FilmV4/Archive Field"
{
    Properties
    {
        _MainTex("Source image atlas",2D)="white"{}
        _Timeline("Performance seconds",Float)=0
        _Density("Archive accumulation",Range(0,1))=0
        _Pressure("Local pressure",Range(0,1))=0
        _Fade("Envelope",Range(0,1))=0
        _Pulse("Four actual note onsets",Vector)=(0,0,0,0)
        _Count("Independent image cells",Float)=64
        _Columns("Atlas columns",Float)=16
        _Rows("Atlas rows",Float)=8
    }
    SubShader
    {
        Tags {"Queue"="Geometry+60" "RenderType"="Opaque"}
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Timeline,_Density,_Pressure,_Fade,_Count,_Columns,_Rows;
            float4 _Pulse;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 info:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 info:TEXCOORD1;float fade:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float id=v.info.x,phase=id*2.39996,channel=fmod(id,4);
                float pulse=channel<.5?_Pulse.x:channel<1.5?_Pulse.y:channel<2.5?_Pulse.z:_Pulse.w;
                float presence=smoothstep(v.info.z-.12,v.info.z+.10,_Density);
                float3 p=v.vertex.xyz;
                // Every corner of a document receives the same small offset.
                // Screen rectangles remain readable and never breathe through one another.
                p+=float3(sin(_Timeline*.37+phase)*.065,cos(_Timeline*.29+phase)*.05,0)*presence;
                p.y+=pulse*.045*(.2+_Pressure*.8);
                float a=sin(_Timeline*.16+phase)*.018*_Pressure;
                float2 xz=p.xz-float2(0,-2.8);xz=mul(float2x2(cos(a),-sin(a),sin(a),cos(a)),xz);p.xz=xz+float2(0,-2.8);
                p.z+=smoothstep(181.76,186.88,_Timeline)*(id%3)*1.7;
                float withdrawal=1-smoothstep(181.76+(1-v.info.y)*5,183.68+(1-v.info.y)*5,_Timeline);
                o.vertex=UnityObjectToClipPos(float4(p,1));o.uv=v.uv;o.info=v.info;o.fade=presence*_Fade*withdrawal;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);clip(i.fade-.05);
                float rate=(1+floor(i.info.w/.32+.5))*.32;
                float tick=floor((_Timeline-133.12+.0001)/rate);
                float cell=floor(fmod(floor(i.info.x+.5)+tick*7,_Count)+.5);
                float2 grid=float2(_Columns,_Rows);
                float2 origin=float2(fmod(cell,_Columns),_Rows-1-floor(cell/_Columns))/grid;
                float2 uv=i.uv;
                float tear=step(.985,frac(sin(floor(uv.y*38)+floor(_Timeline*12)+i.info.x)*43758.5453))*_Pressure;
                uv.x=frac(uv.x+tear*.018);
                float2 atlas=origin+(clamp(uv,.012,.988))/grid;
                float4 col=tex2D(_MainTex,atlas);
                float edge=step(min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)),.012);
                float3 chrome=lerp(float3(.08,.17,.22),float3(.42,.71,.79),step(.975,uv.y));
                // No artificial border or contain-padding; tile fills its document.
                col.rgb=col.rgb;
                col.rgb*=lerp(.58,1,i.info.y)*(.96+.04*cos(uv.y*340));
                col.rgb=lerp(float3(.012,.025,.038),col.rgb,i.fade);
                return fixed4(col.rgb,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
