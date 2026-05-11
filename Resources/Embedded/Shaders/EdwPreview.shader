Shader "KusakaFactory/Zatools/EdwPreview"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+49" }
        LOD 100

        Pass
        {
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
                float2 pixel = fi.vertex.xy; // screen pixel coordinate-ish
                float hatch1 = step(0.5, frac((pixel.x + pixel.y) / 8.0));
                float hatch2 = step(0.5, frac((pixel.x - pixel.y) / 8.0));
                float h = min(hatch1, hatch2);
                clip(hatch1 - hatch2);
                return float4(0, 0, 1.0, 1.0);
            }
            ENDCG
        }
    }
}
