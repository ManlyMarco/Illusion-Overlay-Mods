Shader "Unlit/composite"
{
    Properties
    {
        _MainTex("MainTex", 2D) = "white" {}
        _Overlay("Overlay", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha
            ZClip Off
            ZWrite Off
            Name "Unlit"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            uniform sampler2D _MainTex;
            uniform sampler2D _Overlay;
            
            struct VertexInput {
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
            };
            
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
            };

            VertexOutput vert (VertexInput v) {
                VertexOutput o;
                o.uv0 = v.texcoord0;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag (VertexOutput i) : COLOR
            {
                float4 A = tex2D(_Overlay, i.uv0);
                float4 B = tex2D(_MainTex, i.uv0);

                float4 Out;
                Out.w = A.w + B.w*(1-A.w);
                Out.xyz = (A.xyz*A.w + B.xyz*B.w*(1-A.w))/Out.w;

                return Out;
            }
            ENDCG
        }
    }
    CustomEditor "ASEMaterialInspector"
}
