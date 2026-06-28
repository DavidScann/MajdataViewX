Shader "Custom/NoteRich"
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

            struct NotesRenderData
            {
                float2 pos;
                float angRad;
                float2 scale;
                uint spriteId;
                float4 color;
                float brightness;
                uint exSprite;
                float4 exColor;
                float2 sliceBorder;   // (topFrac, botFrac), (0,0) = normal
                uint sort;
            };

            StructuredBuffer<NotesRenderData> _NoteBuffer;
            StructuredBuffer<float4> _SpriteRects;
            float _AtlasSize;
            float _PixelsPerUnit;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f   { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; uint id : TEXCOORD1; };

            v2f vert(appdata v, uint id : SV_InstanceID)
            {
                v2f o;
                NotesRenderData note = _NoteBuffer[id];
                float4 rect = _SpriteRects[note.spriteId];
                float2 worldSize = (rect.zw - rect.xy) * _AtlasSize / _PixelsPerUnit;
                float2 p = v.vertex.xy * note.scale * worldSize;
                float s = sin(note.angRad); float c = cos(note.angRad);
                float2 r = float2(p.x*c - p.y*s, p.x*s + p.y*c);
                r += note.pos;
                o.pos = UnityObjectToClipPos(float4(r, 0, 1));
                o.uv = v.uv;
                o.id = id;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                NotesRenderData note = _NoteBuffer[i.id];
                float4 rect = _SpriteRects[note.spriteId];

                // ---- 3-slice Y-axis UV remap ----
                float2 uv;
                if (note.sliceBorder.x + note.sliceBorder.y > 0.0)
                {
                    float spriteH_uv = rect.w - rect.y;
                    float nativeH = spriteH_uv * _AtlasSize / _PixelsPerUnit;
                    float renderedH = nativeH * note.scale.y;
                    float topCapFrac = (note.sliceBorder.x * nativeH) / renderedH;
                    float botCapFrac = (note.sliceBorder.y * nativeH) / renderedH;
                    float middleFrac = 1.0 - topCapFrac - botCapFrac;
                    float sliceMid = 1.0 - note.sliceBorder.x - note.sliceBorder.y;

                    float uvY = i.uv.y;
                    float remapY;
                    if (uvY >= 1.0 - topCapFrac)
                    {
                        // Top cap: map to top of sprite
                        float t = (uvY - (1.0 - topCapFrac)) / topCapFrac;
                        remapY = (1.0 - note.sliceBorder.x) + t * note.sliceBorder.x;
                    }
                    else if (uvY <= botCapFrac)
                    {
                        // Bottom cap: map to bottom of sprite
                        float t = uvY / botCapFrac;
                        remapY = t * note.sliceBorder.y;
                    }
                    else
                    {
                        // Middle stretch: map to stretchable middle of sprite
                        float t = (uvY - botCapFrac) / middleFrac;
                        remapY = note.sliceBorder.y + t * sliceMid;
                    }
                    uv = float2(lerp(rect.x, rect.z, i.uv.x), lerp(rect.y, rect.w, remapY));
                }
                else
                {
                    uv = lerp(rect.xy, rect.zw, i.uv);
                }

                float4 col = tex2D(_MainTex, uv);

                // ---- EX frame overlay (from NoteIndirect.shader) ----
                if (note.exSprite != 0)
                {
                    float2 uvFrame = lerp(_SpriteRects[note.exSprite].xy, _SpriteRects[note.exSprite].zw, i.uv);
                    float4 frame = tex2D(_MainTex, uvFrame);
                    frame.rgb *= note.exColor.rgb;
                    frame.a   *= note.exColor.a;
                    col.rgb = frame.rgb * frame.a + col.rgb * (1.0 - frame.a);
                    col.a   = frame.a   + col.a   * (1.0 - frame.a);
                }

                // ---- Brightness + color ----
                col.rgb *= note.color.rgb * note.brightness;
                col.a   *= note.color.a;
                col.rgb *= col.a; // premultiply
                return col;
            }
            ENDHLSL
        }
    }
}
