Shader "OldWebMap/V2/Source Surface"
{
    Properties
    {
        _MainTex("Current document state",2D)="white"{}
        _PreviousTex("Previous document state",2D)="white"{}
        _Rect("Source rectangle",Vector)=(0,0,1,1)
        _PreviousRect("Previous source rectangle",Vector)=(0,0,1,1)
        _Blend("State reveal",Range(0,1))=1
        _Opacity("Envelope",Range(0,1))=1
        _Timeline("Seconds",Float)=0
        _Activity("Native highlight",Range(0,1))=0
        _Tint("Tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent+20" "RenderType"="Transparent"}
        Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile __ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"
            sampler2D _MainTex,_PreviousTex;
            float4 _Rect,_PreviousRect,_Tint;
            float _Blend,_Opacity,_Timeline,_Activity;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v){UNITY_SETUP_INSTANCE_ID(v);v2f o;UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=clamp(i.uv,.001,.999);
                fixed4 a=tex2D(_PreviousTex,_PreviousRect.xy+uv*_PreviousRect.zw);
                fixed4 b=tex2D(_MainTex,_Rect.xy+uv*_Rect.zw);
                float reveal=saturate((_Blend-uv.x*.75-(1-uv.y)*.22)*24+1);
                fixed4 c=lerp(a,b,reveal)*_Tint;
                float activity=exp(-pow((uv.x-frac(_Timeline*.13))*20,2))*step(uv.y,.025);
                c.rgb+=activity*_Activity*float3(.08,.24,.21);c.a*=_Opacity;clip(c.a-.02);return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
