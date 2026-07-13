Shader "LastJumpCrew/Skybox/Panoramic"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "black" {}
        _Exposure ("Exposure", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.direction = normalize(input.vertex.xyz);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv;
                uv.x = atan2(input.direction.z, input.direction.x) * (0.5 / UNITY_PI) + 0.5;
                uv.y = asin(input.direction.y) / UNITY_PI + 0.5;
                return tex2D(_MainTex, uv) * _Exposure;
            }
            ENDCG
        }
    }
}
