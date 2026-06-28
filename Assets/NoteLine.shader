Shader "Custom/NoteLine"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;

            struct LineRenderData
            {
                float angRad;
                float scale;
                uint spriteId;
                uint sort;
            };

            StructuredBuffer<LineRenderData> _NoteBuffer;
            StructuredBuffer<float4> _SpriteRects;
            float _AtlasSize;
            float _PixelsPerUnit;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f   { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; uint id : TEXCOORD1; };

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;
                LineRenderData note = _NoteBuffer[id];
                float4 rect = _SpriteRects[note.spriteId];
                float2 worldSize = (rect.zw - rect.xy) * _AtlasSize / _PixelsPerUnit;
                float2 p = v.vertex.xy * note.scale * worldSize;
                float s = sin(note.angRad); float c = cos(note.angRad);
                float2 r = float2(p.x*c - p.y*s, p.x*s + p.y*c);
                o.pos = UnityObjectToClipPos(float4(r, 0, 1));
                o.uv = v.uv;
                o.id = id;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                LineRenderData note = _NoteBuffer[i.id];
                float4 rect = _SpriteRects[note.spriteId];
                float2 uv = lerp(rect.xy, rect.zw, i.uv);
                float4 col = tex2D(_MainTex, uv);
                col.rgb *= col.a; // premultiply (color = white by default)
                return col;
            }
            ENDHLSL
        }
    }
}
