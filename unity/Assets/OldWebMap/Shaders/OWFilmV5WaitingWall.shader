Shader "OldWebMap/FilmV5/Waiting Room Walls"
{
    Properties { _Color("Same paint on every wall",Color)=(.145,.185,.225,1) }
    SubShader
    {
        Tags {"Queue"="Geometry" "RenderType"="Opaque"}
        ZWrite On Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal);
                float light=.60+.38*saturate(dot(n,normalize(float3(.35,-.48,.72))));
                float height=.80+.20*saturate(i.world.y/5.4);
                float3 paint=_Color.rgb*light*height;
                float vertical=1-abs(n.y);
                float base=(1-smoothstep(.010,.027,abs(i.world.y-.18)))*vertical;
                float cornice=(1-smoothstep(.014,.033,abs(i.world.y-5.20)))*vertical;
                paint+=float3(.075,.10,.11)*(base+cornice)*.5;
                return fixed4(paint,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
