Shader "OldWebMap/V3/EDGE TEAM Pixel Cylinder"
{
    Properties{_Turn("Rotation radians",Float)=0 _Radius("Cylinder radius",Float)=1.7 _Pitch("Row pitch",Float)=.135 _Cell("Cell size",Float)=.11 _Fill("Dark translucent fill",Range(0,1))=.78 _Pulse("Musical accent",Float)=0 _Billboard("Face viewer",Range(0,1))=0}
    SubShader
    {
        Tags{"Queue"="Transparent+5" "RenderType"="Transparent"}
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
            float _Turn,_Radius,_Pitch,_Cell,_Fill,_Pulse,_Billboard;
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 info:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float front:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float a=v.info.x+_Turn;float3 centre=float3(sin(a)*_Radius,(3-v.info.y)*_Pitch,cos(a)*_Radius);
                float3 right=float3(-cos(a),0,sin(a)),up=float3(0,1,0);
                float3 camera=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
                float3 view=normalize(camera-centre);float3 facingRight=normalize(cross(up,view));right=normalize(lerp(right,facingRight,_Billboard));
                float size=_Cell*(1+_Pulse*.025);float3 p=centre+(right*v.vertex.x+up*v.vertex.y)*size;
                o.vertex=UnityObjectToClipPos(float4(p,1));o.uv=v.uv;o.front=smoothstep(-.4,.4,dot(normalize(centre*float3(1,0,1)),view));return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));float aa=max(fwidth(edge),.003);
                float border=1-smoothstep(.075-aa,.075+aa,edge);
                float3 col=lerp(float3(.003,.009,.025),float3(.08,.43,1.15)*(1+_Pulse*.2),border);
                float alpha=lerp(_Fill,.96,border)*lerp(.16,1,i.front);
                return fixed4(col,alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
