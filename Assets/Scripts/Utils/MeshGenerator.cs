using UnityEngine;

namespace MajdataViewX.Utils
{
    internal static class MeshGenerator
    {
        /// <summary>
        /// 生成一个圆形mesh
        /// </summary>
        /// <param name="segments">近似分段数量</param>
        public static Mesh CreateCircleMesh(
            int segments,
            float radius = 0.5f,
            bool circumscribe = false)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;

                float adjustedRadius = radius;
                if (circumscribe)
                {
                    adjustedRadius = radius / Mathf.Cos(Mathf.PI / segments);
                }

                float x = Mathf.Cos(angle) * adjustedRadius;
                float y = Mathf.Sin(angle) * adjustedRadius;

                vertices[i + 1] = new Vector3(x, y, 0);

                uv[i + 1] = new Vector2(
                    x / radius * 0.5f + 0.5f,
                    y / radius * 0.5f + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();

            return mesh;
        }
        /// <summary>
        /// 生成一个圆环形mesh
        /// </summary>
        /// <param name="segments">近似分段数量</param>
        public static Mesh CreateRingMesh(
            int segments,
            float outerRadius,
            float innerRadius)
        {
            var mesh = new Mesh();

            var vertices = new Vector3[segments * 2];
            var uv = new Vector2[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;

                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);

                vertices[i * 2 + 0] =
                    new Vector3(c * outerRadius, s * outerRadius);

                vertices[i * 2 + 1] =
                    new Vector3(c * innerRadius, s * innerRadius);

                uv[i * 2 + 0] = new Vector2(
                    c * 0.5f + 0.5f,
                    s * 0.5f + 0.5f);

                uv[i * 2 + 1] = new Vector2(
                    c * innerRadius / outerRadius * 0.5f + 0.5f,
                    s * innerRadius / outerRadius * 0.5f + 0.5f);
            }

            int t = 0;

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                int o0 = i * 2;
                int i0 = i * 2 + 1;

                int o1 = next * 2;
                int i1 = next * 2 + 1;

                triangles[t++] = o0;
                triangles[t++] = o1;
                triangles[t++] = i0;

                triangles[t++] = i0;
                triangles[t++] = o1;
                triangles[t++] = i1;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            mesh.RecalculateBounds();

            return mesh;
        }
    }
}