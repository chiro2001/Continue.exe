Shader "OldWebMap/V3/Attached Focus Halo"
{
    Properties{_Color("Color",Color)=(.2,.5,1,1) _Intensity("Strength",Float)=.35 _Inner("Source edge",Float)=.85 _Timeline("Time",Float)=0}
    SubShader
    {
        Tags{"Queue"="Transparent+1" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"
            float4 _Color;float _Intensity,_Inner,_Timeline;
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            f vert(a v){UNITY_SETUP_INSTANCE_ID(v);f o;UNITY_INITIALIZE_OUTPUT(f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv*2-1;return o;}
            fixed4 frag(f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 q=abs(i.uv);float d=max(q.x,q.y);float outside=smoothstep(_Inner-.008,_Inner+.008,d);
                float fall=pow(saturate((1-d)/max(.001,1-_Inner)),1.6);
                return fixed4(_Color.rgb*outside*fall*_Intensity*(.97+.03*sin(_Timeline*1.9)),0);
            }
            ENDCG
        }
    }
}
