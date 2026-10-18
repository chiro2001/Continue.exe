Shader "OldWebMap/V3/Window Interior"
{
    Properties
    {
        _Timeline("Clock",Float)=0
        _Idle("Idle clock",Float)=1
        _Gain("Brightness",Range(0,2))=.9
        _Tint("Deep space",Color)=(.035,.11,.25,1)
    }
    SubShader
    {
        Tags{"Queue"="Geometry+5" "RenderType"="Opaque"}
        Cull Off ZWrite On
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
            float _Timeline,_Idle,_Gain;float4 _Tint;
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f{float4 vertex:SV_POSITION;float3 local:TEXCOORD0;float2 uv:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz;o.uv=v.uv;return o;}
            float2 turn(float2 p,float a){float s=sin(a),c=cos(a);return mul(float2x2(c,-s,s,c),p);}
            float scene(float3 p,float t)
            {
                float wall=.51-length(p.xy*float2(1,1.2));
                float floorD=p.y+.36;
                float z=frac((p.z+t*.10)/.62)*.62-.31;
                float2 q=float2(length(turn(p.xy,p.z*.18+t*.1)*float2(1,1.2))-.43,z);
                float ribs=length(q)-.012;
                float3 centre=float3(.10*sin(t*.39+p.z),.055*cos(t*.31),2.6);
                float3 f=p-centre;f.xy=turn(f.xy,t*.22);
                float halo=length(float2(length(f.xy)-.16,f.z))-.012;
                return min(min(wall,floorD),min(ribs,halo));
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t=lerp(_Timeline,_Time.y,_Idle);
                // Each eye starts its own ray at its actual world camera position.
                // Geometry beyond this aperture has binocular and head parallax.
                float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
                float3 rd=normalize(i.local-ro),p=i.local+rd*.008;
                float travelled=0,d=0;float glow=0;bool hit=false;
                [loop]for(int n=0;n<48;n++)
                {
                    d=scene(p,t);glow+=.00028/max(abs(d),.008);
                    if(d<.0015){hit=true;break;}
                    float stepD=clamp(d*.72,.002,.18);p+=rd*stepD;travelled+=stepD;
                    if(travelled>6.5)break;
                }
                float3 col=_Tint.rgb*(.2+.35*saturate(rd.y+.4));
                if(hit)
                {
                    float e=.0018;float3 normal=normalize(float3(scene(p+float3(e,0,0),t)-scene(p-float3(e,0,0),t),scene(p+float3(0,e,0),t)-scene(p-float3(0,e,0),t),scene(p+float3(0,0,e),t)-scene(p-float3(0,0,e),t)));
                    float diffuse=.18+.82*saturate(dot(normal,normalize(float3(-.2,.6,-.4))));
                    float2 floorGrid=abs(frac(p.xz*float2(14,4))-.5);
                    float gridLine=1-smoothstep(.012,.028,min(floorGrid.x,floorGrid.y));
                    float rib=exp(-abs(frac((p.z+t*.10)/.62)*.62-.31)*80);
                    col=lerp(float3(.018,.035,.062)*diffuse,float3(.06,.38,.80),max(gridLine*.35,rib));
                    col*=exp(-travelled*.16);
                }
                col+=glow*float3(.025,.12,.30);
                float star=pow(saturate(sin(rd.x*181+rd.y*61)*sin(rd.y*143-rd.x*37)),45);
                if(!hit)col+=star*float3(.4,.65,1);
                return fixed4(col*_Gain,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
