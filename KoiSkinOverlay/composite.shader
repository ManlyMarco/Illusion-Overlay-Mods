// Shader created with Shader Forge v1.38 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.38;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,lico:0,lgpr:1,limd:0,spmd:1,trmd:0,grmd:0,uamb:False,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:False,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:3,bdst:7,dpts:2,wrdp:False,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:False,aust:False,igpj:True,qofs:0,qpre:3,rntp:2,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.5,fgcg:0.5,fgcb:0.5,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:8714,x:32719,y:32712,varname:node_8714,prsc:2|diff-3420-OUT,custl-3420-OUT,alpha-6740-OUT;n:type:ShaderForge.SFN_Tex2d,id:6029,x:32001,y:32929,varname:node_6029,prsc:2,tex:da3c81f5610b82f439e06f9fab488b50,ntxv:0,isnm:False|TEX-5399-TEX;n:type:ShaderForge.SFN_Tex2dAsset,id:6240,x:31747,y:32751,ptovrint:False,ptlb:MainTex,ptin:_MainTex,varname:node_6240,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:a9fb48d7500eadb498d49c3eaa76940c,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2dAsset,id:5399,x:31747,y:32986,ptovrint:False,ptlb:Overlay,ptin:_Overlay,varname:node_5399,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,tex:da3c81f5610b82f439e06f9fab488b50,ntxv:0,isnm:False;n:type:ShaderForge.SFN_Tex2d,id:6230,x:31967,y:32751,varname:node_6230,prsc:2,tex:a9fb48d7500eadb498d49c3eaa76940c,ntxv:0,isnm:False|TEX-6240-TEX;n:type:ShaderForge.SFN_Lerp,id:3420,x:32340,y:32758,varname:node_3420,prsc:2|A-6230-RGB,B-6029-RGB,T-6029-A;n:type:ShaderForge.SFN_Max,id:6740,x:32273,y:33024,varname:node_6740,prsc:2|A-6230-A,B-6029-A;proporder:6240-5399;pass:END;sub:END;*/

Shader "Unlit/composite" {
    Properties {
        _MainTex ("MainTex", 2D) = "white" {}
        _Overlay ("Overlay", 2D) = "white" {}
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        LOD 100
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #include "UnityCG.cginc"
            #pragma multi_compile_fwdbase
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 3.0
            uniform sampler2D _MainTex; uniform float4 _MainTex_ST;
            uniform sampler2D _Overlay; uniform float4 _Overlay_ST;
            struct VertexInput {
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.pos = UnityObjectToClipPos( v.vertex );
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
////// Lighting:
                float4 node_6230 = tex2D(_MainTex,TRANSFORM_TEX(i.uv0, _MainTex));
                float4 node_6029 = tex2D(_Overlay,TRANSFORM_TEX(i.uv0, _Overlay));
                float3 node_3420 = lerp(node_6230.rgb,node_6029.rgb,node_6029.a);
                float3 finalColor = node_3420;
                return fixed4(finalColor,max(node_6230.a,node_6029.a));
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
}
