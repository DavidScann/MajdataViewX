Shader "Custom/NoteIndirect"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct NoteRenderData
            {
                float2 pos;
                float ang;
                float scale;
                uint spriteId;
                float4 color;
                float brightness;
            };

            StructuredBuffer<NoteRenderData> _NoteBuffer;
            StructuredBuffer<float4> _SpriteRects;

            float _AtlasSize;
            float _PixelsPerUnit;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                uint id : TEXCOORD1;
            };

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;

                NoteRenderData note = _NoteBuffer[id];

                float4 rect = _SpriteRects[note.spriteId];
                float2 worldSize = (rect.zw - rect.xy) * _AtlasSize / _PixelsPerUnit;

                // scale
                float2 p = v.vertex.xy * note.scale * worldSize;

                // rotation
                float s = sin(note.ang);
                float c = cos(note.ang);

                float2 r;
                r.x = p.x * c - p.y * s;
                r.y = p.x * s + p.y * c;

                // translate
                r += note.pos;

                o.pos = UnityObjectToClipPos(float4(r, 0, 1));

                o.uv = v.uv;
                o.id = id;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                NoteRenderData note = _NoteBuffer[i.id];

                float4 rect = _SpriteRects[note.spriteId];

                float2 uv = lerp(rect.xy, rect.zw, i.uv);

                float4 col = tex2D(_MainTex, uv);

                col.rgb *= note.color.rgb;
                col.a   *= note.color.a;

                col.rgb *= note.brightness;

                return col;
            }

            ENDHLSL
        }
    }
}