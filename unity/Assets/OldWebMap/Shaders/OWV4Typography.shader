Shader "OldWebMap/V4/Extruded Chrome Typography"
{
    Properties { _Pulse ("Real onset accent",Range(0,1))=0 _Grid ("Blue white face pattern",Range(0,1))=.6 }
    SubShader
    {
        Tags {"Queue"="Geometry+30" "RenderType"="Opaque"}
        Cull Back ZWrite On ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float _Pulse,_Grid;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float3 local:TEXCOORD2;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.local=v.vertex.xyz;o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),eye=normalize(_WorldSpaceCameraPos-i.world);
                float3 key=normalize(float3(-.35,.72,-.6)),fill=normalize(float3(.7,.15,-.4));
                float diffuse=.22+.55*saturate(dot(n,key))+.16*saturate(dot(n,fill));
                float spec=pow(saturate(dot(n,normalize(key+eye))),64)*(1.1+_Pulse);
                float checker=fmod(floor((i.local.x+8)*22)+floor((i.local.y+8)*22),2);
                float3 blueWhite=lerp(float3(.09,.38,.8),float3(.86,.96,1),checker);
                float3 base=lerp(i.color.rgb,blueWhite,_Grid*i.color.a);
                float rim=pow(1-saturate(dot(n,eye)),3)*.17;
                return fixed4(base*diffuse+float3(.87,.96,1)*spec+float3(.05,.22,.4)*rim,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
