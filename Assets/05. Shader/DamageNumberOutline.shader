Shader "Idle Project/DamageNumberOutline"
{
    // 기존 TextMesh가 쓰던 GUI/Text Shader와 동일한 알파 텍스처(폰트 아틀라스)를 그대로 읽되,
    // 주변 텍셀의 알파를 함께 샘플링해 글자 바깥쪽을 검정으로 덧칠하는 방식으로 테두리를 낸다
    // (별도 GameObject/Material 복제 없이 셰이더 한 장으로 처리 - DamageNumber가 자주 스폰되는
    // 풀링 오브젝트라 인스턴스당 드로우콜을 늘리지 않기 위함).
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineSize ("Outline Size (texels)", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _OutlineSize;
                fixed centerAlpha = tex2D(_MainTex, i.uv).a;

                fixed outlineAlpha = 0;
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv + float2(texel.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv - float2(texel.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv + float2(0, texel.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv - float2(0, texel.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv + float2(texel.x, texel.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv + float2(-texel.x, texel.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv + float2(texel.x, -texel.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, i.uv + float2(-texel.x, -texel.y)).a);

                fixed4 fillColor = fixed4(i.color.rgb, i.color.a * centerAlpha);
                fixed4 outlineColor = fixed4(_OutlineColor.rgb, _OutlineColor.a * outlineAlpha * i.color.a);

                fixed4 result = lerp(outlineColor, fillColor, centerAlpha);
                result.a = max(fillColor.a, outlineColor.a);
                return result;
            }
            ENDCG
        }
    }
}
