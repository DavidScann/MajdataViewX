using UnityEngine;

public static class MeshBuilder
{
    /// <summary>
    /// 生成一个包裹圆形的八边形网格 (用于所有Note)
    /// 相比于 Quad，可减少约 17% 的透明像素 Overdraw
    /// </summary>
    public static Mesh CreateOctagonMesh()
    {
        Mesh mesh = new() { name = "Octagon" };

        // 完美包裹半径为 0.5 的圆形的八边形顶点坐标
        // 0.2071f 来源于 0.5 * tan(22.5度)
        const float a = 0.5f;
        const float b = 0.2071f;

        Vector3[] vertices = new Vector3[8]
        {
            new(-b,  a, 0), // 0: 上左
            new( b,  a, 0), // 1: 上右
            new( a,  b, 0), // 2: 右上
            new( a, -b, 0), // 3: 右下
            new( b, -a, 0), // 4: 下右
            new(-b, -a, 0), // 5: 下左
            new(-a, -b, 0), // 6: 左下
            new(-a,  b, 0)  // 7: 左上
        };

        // UV 坐标就是把坐标从 [-0.5, 0.5] 映射到 [0, 1]
        Vector2[] uvs = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            uvs[i] = new Vector2(vertices[i].x + 0.5f, vertices[i].y + 0.5f);
        }

        // 以顶点 0 为中心构建三角扇面 (Triangle Fan)
        int[] triangles = new int[18]
        {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 5,
            0, 5, 6,
            0, 6, 7
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // 生成必要的边界信息供底层 Culling 使用
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 生成长条形网格 (用于 TapLine)
    /// </summary>
    /// <param name="widthTrim">左右两侧裁剪的空白比例</param>
    /// <param name="heightTrim">上下两侧裁剪的空白比例</param>
    public static Mesh CreateTightQuad(float widthTrim = 0f, float heightTrim = 0f)
    {
        Mesh mesh = new() { name = "TightQuad" };

        // 计算裁剪后的实际顶点范围
        float minX = -0.5f + widthTrim;
        float maxX = 0.5f - widthTrim;
        float minY = -0.5f + heightTrim;
        float maxY = 0.5f - heightTrim;

        Vector3[] vertices = new Vector3[4]
        {
            new(minX, minY, 0), // 0: 左下
            new(maxX, minY, 0), // 1: 右下
            new(minX, maxY, 0), // 2: 左上
            new(maxX, maxY, 0)  // 3: 右上
        };

        // UV 必须和顶点的裁剪比例完全一致，保证图集采样依然正确
        Vector2[] uvs = new Vector2[4]
        {
            new(widthTrim, heightTrim),
            new(1f - widthTrim, heightTrim),
            new(widthTrim, 1f - heightTrim),
            new(1f - widthTrim, 1f - heightTrim)
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
}