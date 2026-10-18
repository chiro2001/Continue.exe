Shader "OldWebMap/FilmV4/Continue Assembly"
{
    Properties{_Timeline("Seconds",Float)=0 _Reveal("Assembly envelope",Range(0,1))=0 _Opacity("Envelope",Range(0,1))=1 _Pulse("Accent",Float)=0}
    SubShader
    {
        Tags{"Queue"="Transparent-10" "RenderType"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile __ UNITY_SINGLE_PASS_STEREO
            #include "UnityCG.cginc"
            float _Timeline,_Reveal,_Opacity,_Pulse;
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;
                float vignette=exp(-dot(p*float2(.9,1.4),p*float2(.9,1.4))*2);
                float backing=vignette*.70;
                float scan=exp(-pow((i.uv.x-_Reveal)*18,2))*exp(-abs(p.y)*2)*sin(_Reveal*3.14159);
                float side=1-abs(p.x),cells=floor(i.uv.y*32);
                float travel=frac(sin(cells*37.7)*43758.54);
                float piece=exp(-pow((side-lerp(travel,.9,_Reveal))*45,2))*step(.64,travel)*(1-_Reveal);
                float edge=exp(-pow((abs(p.y)-.36)*45,2))*smoothstep(0,.15,1-abs(p.x));
                float light=scan*.42+piece*.65+edge*.16*(.6+_Pulse*.4);
                float3 colour=lerp(float3(.008,.016,.026),float3(.49,.89,1),saturate(light*2));
                return fixed4(colour,(backing+light)*_Opacity);
            }
            ENDCG
        }
    }
    Fallback Off
}
