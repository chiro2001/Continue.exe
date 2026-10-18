Shader "OldWebMap/V3/Pixel Cursor Swarm"
{
    Properties{_MainTex("Pixel arrow",2D)="white"{} _Timeline("Clock",Float)=0 _Density("Density",Range(0,1))=0 _Opacity("Opacity",Range(0,1))=1 _Pulse("Channels",Vector)=(0,0,0,0)}
    SubShader
    {
        Tags{"Queue"="Transparent+25" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
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
            sampler2D _MainTex;float _Timeline,_Density,_Opacity;float4 _Pulse;
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 info:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            f vert(a v)
            {
                UNITY_SETUP_INSTANCE_ID(v);f o;UNITY_INITIALIZE_OUTPUT(f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float id=v.info.x,phase=id*2.39996,t=_Timeline-133.12;
                float angle=phase+t*(.12+frac(id*.37)*.22);float near=frac(id*.619);
                float r=4.8+frac(id*.371)*8;float z=-2.8+cos(angle)*r;
                float3 p=float3(sin(angle)*r,1.0+frac(id*.237)*5.6+sin(t*.6+phase)*.4,z);
                // Keep the central reading cone open; cursors cross above/below it.
                if(p.z>0&&abs(p.x)<2.5&&p.y>1.0&&p.y<3.4)p.y=3.7+frac(id*.271)*1.8;
                float ch=fmod(id,4);float accent=ch<.5?_Pulse.x:ch<1.5?_Pulse.y:ch<2.5?_Pulse.z:_Pulse.w;
                float s=(.18+near*.36)*(1+accent*.08);float roll=sin(t*.7+phase)*.35;
                float2 xy=mul(float2x2(cos(roll),-sin(roll),sin(roll),cos(roll)),v.vertex.xy*s);
                p+=float3(xy,0);o.vertex=UnityObjectToClipPos(float4(p,1));o.uv=v.uv;o.alpha=_Opacity*smoothstep(v.info.y-.05,v.info.y+.05,_Density);return o;
            }
            fixed4 frag(f i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);fixed4 c=tex2D(_MainTex,i.uv);c.a*=i.alpha;clip(c.a-.01);return c;}
            ENDCG
        }
    }
}
