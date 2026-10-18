Shader "OldWebMap/V2/Depth Copper"
{
    Properties
    {
        _Timeline("Performance seconds",Float)=0
        _Idle("Use idle clock",Float)=0
        _Mode("Stars 0, copper 1, contours 2",Float)=0
        _Opacity("Opacity",Range(0,1))=1
        _Pulse("Actual note accents",Vector)=(0,0,0,0)
        _Tint("Tint",Color)=(.4,.7,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent+40" "RenderType"="Transparent"}
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"
            float _Timeline,_Idle,_Mode,_Opacity;float4 _Pulse,_Tint;
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 info:TEXCOORD1;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float level:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t=lerp(_Timeline,_Time.y,_Idle),seed=v.info.x,ch=fmod(seed,4);
                float pulse=ch<.5?_Pulse.x:ch<1.5?_Pulse.y:ch<2.5?_Pulse.z:_Pulse.w;
                float3 p=v.vertex.xyz;
                if(_Mode<.5){p.x+=sin(t*.27+seed)*.045;p.y+=cos(t*.23+seed)*.025;}
                else if(_Mode<1.5){p.y+=sin(t*.7+seed*1.4)*.055;p.z+=sin(t*.33+seed)*.025;}
                else if(_Mode<2.5){p*=1+pulse*.014;p.y+=sin(t*.36+p.x*.22+seed)*.04;}
                else {p*=1+pulse*.001;}
                o.vertex=UnityObjectToClipPos(float4(p,1));o.uv=v.uv;o.color=v.color;o.level=.65+pulse*.35;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float body=_Mode<.5?pow(saturate(1-length((i.uv-.5)*2)),1.4):_Mode<1.5?pow(saturate(1-abs(i.uv.y-.5)*2),1.2):1;
                float sheen=_Mode>.5&&_Mode<1.5?pow(saturate(1-abs(i.uv.y-.62)*8),4):0;
                float3 c=i.color.rgb*_Tint.rgb*(.7+i.level*.3)+sheen*.7;
                return fixed4(c,body*_Opacity*i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
