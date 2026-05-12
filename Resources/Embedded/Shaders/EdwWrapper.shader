Shader "KusakaFactory/Zatools/EdwWrapper"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+49" }
        LOD 100

        // Writing depth in ForwardBase pass causes typical RQ problem.
        // As EyeholeDepthWrapper's concern is only about _CameraDepthTexture, we can simply omit ForwardBase pass.
        /*
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            ColorMask 0
            ZTest LEqual
            ZWrite On

            CGPROGRAM
            #pragma vertex vert_main
            #pragma fragment frag_main

            #include "UnityCG.cginc"

            struct VertexInput {
                float4 vertex : POSITION;
            };

            struct FragmentInput {
                float4 vertex : SV_Position;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            FragmentInput vert_main(VertexInput vi) {
                FragmentInput fi;
                fi.vertex = UnityObjectToClipPos(vi.vertex);
                return fi;
            }

            float4 frag_main(FragmentInput fi) : SV_Target {
                return 0;
            }
            ENDCG
        }
        */

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZTest LEqual
            ZWrite On

            CGPROGRAM
            #pragma vertex vert_main
            #pragma fragment frag_main

            #include "UnityCG.cginc"

            struct VertexInput {
                float4 vertex : POSITION;
            };

            struct FragmentInput {
                float4 vertex : SV_Position;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            FragmentInput vert_main(VertexInput vi) {
                FragmentInput fi;
                fi.vertex = UnityObjectToClipPos(vi.vertex);
                return fi;
            }

            float4 frag_main(FragmentInput fi) : SV_Target {
                return 0;
            }
            ENDCG
        }
    }
}
