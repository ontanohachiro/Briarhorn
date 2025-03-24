using Unity.Mathematics;
using UnityEngine;

public static class VoxelHelper//ユーティリティクラス:便利なメソッド群をstaticな領域にまとめたもの.
{

    public static readonly int2 AtlasSize = new int2(8, 8);

    /// <Summary>
    ///一次元のインデックスを三次元のものに変換.
    /// </Summary>
    public static int3 To3DIndex(int index, int3 chunkSize)
    {
        return new int3 { z = index % chunkSize.z, y = (index / chunkSize.z) % chunkSize.y, x = index / (chunkSize.y * chunkSize.z) };
    }
    /// <Summary>
    ///三次元のインデックスを一次元のものに変換.
    /// </Summary>
    public static int To1DIndex(int3 index, int3 chunkSize)
    {
        return index.z + index.y * chunkSize.z + index.x * chunkSize.y * chunkSize.z;
    }

    public static int To1DIndex(Vector3Int index, Vector3Int chunkSize)//Vector型のためのもの.
    {
        return To1DIndex(new int3(index.x, index.y, index.z), new int3(chunkSize.x, chunkSize.y, chunkSize.z));
    }
    /// <Summary>
    ///ワールド座標を現在いるチャンク内の位置に変換する.
    /// </Summary>
    public static int3 WorldToChunk(int3 worldGridPosition, int3 chunkSize)
    {
        return new int3
        {
            x = Floor((float)worldGridPosition.x / chunkSize.x),
            y = Floor((float)worldGridPosition.y / chunkSize.y),
            z = Floor((float)worldGridPosition.z / chunkSize.z)
        };
    }

    public static Vector3Int WorldToChunk(Vector3 worldPosition, Vector3Int chunkSize)
    {
        // Chunk position is indexed as a matrix including negative indexes (does not reflect world position)
        return new Vector3Int
        {
            x = Floor(worldPosition.x / chunkSize.x),
            y = Floor(worldPosition.y / chunkSize.y),
            z = Floor(worldPosition.z / chunkSize.z)
        };
    }
    /// <Summary>
    ///
    /// </Summary>
    public static Vector3 ChunkToWorld(Vector3Int chunkPosition, Vector3Int chunkSize)
    {
        return chunkPosition * chunkSize;
    }

    public static Vector3 GridToWorld(Vector3Int gridPosition, Vector3Int chunkPosition, Vector3Int chunkSize)
    {
        return ChunkToWorld(chunkPosition, chunkSize) + gridPosition;
    }

    public static Vector3Int WorldToGrid(Vector3 worldPosition, Vector3Int chunkPosition, Vector3Int chunkSize)
    {
        return ToVector3Int(WorldToGrid(Floor(worldPosition), ToInt3(chunkPosition), ToInt3(chunkSize)));
    }

    public static int3 WorldToGrid(int3 worldGridPosition, int3 chunkPosition, int3 chunkSize)
    {
        // Position relative to chunk bottom-left corner
        int3 posRelChunk = worldGridPosition - chunkPosition * chunkSize;

        return Mod(posRelChunk, chunkSize);
    }

    public static bool BoundaryCheck(int3 position, int3 chunkSize)
    {
        return chunkSize.x > position.x && chunkSize.y > position.y && chunkSize.z > position.z && position.x >= 0 && position.y >= 0 && position.z >= 0;
    }

    public static bool BoundaryCheck(Vector3Int position, Vector3Int chunkSize)
    {
        return chunkSize.x > position.x && chunkSize.y > position.y && chunkSize.z > position.z && position.x >= 0 && position.y >= 0 && position.z >= 0;
    }

    public static Vector3Int ToVector3Int(int3 v) => new Vector3Int(v.x, v.y, v.z);

    public static int3 ToInt3(Vector3Int v) => new int3(v.x, v.y, v.z);

    public static int InvertDirection(int direction)
    {
        int axis = direction / 2; // 0(+x,-x), 1(+y,-y), 2(+z,-z)
        int invDirection = Mathf.Abs(direction - (axis * 2 + 1)) + (axis * 2);

        /*
            direction    x0    abs(x0)    abs(x) + axis * 2 => invDirection
            0            -1    1          1  
            1            0     0          0
            2            -1    1          3
            3            0     0          2
            4            -1    1          5
            5            0     0          4
            */

        return invDirection;
    }

    public static int Mod(int v, int m)
    {
        int r = v % m;
        return r < 0 ? r + m : r;
    }

    public static int3 Mod(int3 v, int3 m)
    {
        return new int3
        {
            x = Mod(v.x, m.x),
            y = Mod(v.y, m.y),
            z = Mod(v.z, m.z)
        };
    }

    public static int Floor(float x)
    {
        int xi = (int)x;
        return x < xi ? xi - 1 : xi;
    }

    public static int3 Floor(float3 v)
    {
        return new int3
        {
            x = Floor(v.x),
            y = Floor(v.y),
            z = Floor(v.z)
        };
    }
    /// <Summary>
    ///{ 2, 2, 0, 0, 0, 0 }.疑似的な回転を行うための配列.
    /// </Summary>
    public static readonly int[] DirectionAlignedX = { 2, 2, 0, 0, 0, 0 };
    /// <Summary>
    ///{ 1, 1, 2, 2, 1, 1 }.疑似的な回転を行うための配列.
    /// </Summary>
    public static readonly int[] DirectionAlignedY = { 1, 1, 2, 2, 1, 1 };
    /// <Summary>
    /// { 0, 0, 1, 1, 2, 2 }.疑似的な回転を行うための配列.
    /// </Summary>
    public static readonly int[] DirectionAlignedZ = { 0, 0, 1, 1, 2, 2 };
    /// <Summary>
    ///{ 1, -1, 1, -1, 1, -1 }.疑似的な回転を行うための配列.
    /// </Summary>
    public static readonly int[] DirectionAlignedSign = { 1, -1, 1, -1, 1, -1 };
    /// <Summary>
    /// 0(right):(1, 0, 0), 1(left):(-1, 0, 0), 2(top):(0, 1, 0), 3(bottom):(0, -1, 0), 4(front):(0, 0, 1), 5(back): (0, 0, -1).
    /// </Summary>
    public static readonly int3[] VoxelDirectionOffsets =
    {
        new int3(1, 0, 0), // right
        new int3(-1, 0, 0), // left
        new int3(0, 1, 0), // top
        new int3(0, -1, 0), // bottom
        new int3(0, 0, 1), // front
        new int3(0, 0, -1), // back
    };
    /// <Summary>
    ///立方体の各頂点の座標. (0f, 0f, 0f), (1f, 0f, 0f), (1f, 0f, 1f), (0f, 0f, 1f), (0f, 1f, 0f), (1f, 1f, 0f), (1f, 1f, 1f), (0f, 1f, 1f)
    /// </Summary>
    public static readonly float3[] CubeVertices =
    {
        new float3(0f, 0f, 0f),
        new float3(1f, 0f, 0f),
        new float3(1f, 0f, 1f),
        new float3(0f, 0f, 1f),
        new float3(0f, 1f, 0f),
        new float3(1f, 1f, 0f),
        new float3(1f, 1f, 1f),
        new float3(0f, 1f, 1f)
    };
    /// <Summary>
    ///立方体の各面をなす点のインデックス.directionが0(0-4):右側面.1(5-8):左側面,2(9-12):上面.3(13-16):底面.4(17-20):奥の側面.5(21-24):手前の側面.
    /// </Summary>
    public static readonly int[] CubeFaces =
    {
        1, 2, 5, 6, // right
        0, 3, 4, 7, // left
        4, 5, 7, 6, // top
        0, 1, 3, 2, // bottom
        3, 2, 7, 6, // front
        0, 1, 4, 5, // back
    };
    /// <Summary>
    ///正方形のUV座標. 0 (0f, 0f), 1 (1.0f, 0f), 2 (0f, 1.0f), 3 (1.0f, 1.0f)
    /// </Summary>
    public static readonly float2[] CubeUVs =
    {
        new float2(0f, 0f), new float2(1.0f, 0f), new float2(0f, 1.0f), new float2(1.0f, 1.0f)
    };
    /// <Summary>
    ///立方体の各面をなす点のインデックス.メッシュのために二つの三角形に分割されている。
    /// </Summary>
    public static readonly int[] CubeIndices =
    {
        0, 3, 1,
        0, 2, 3, //face right
        1, 3, 0,
        3, 2, 0, //face left
        0, 3, 1,
        0, 2, 3, //face top
        1, 3, 0,
        3, 2, 0, //face bottom
        1, 3, 0,
        3, 2, 0, //face front
        0, 3, 1,
        0, 2, 3, //face back
    };
    /// <Summary>
    ///使えるかも.要調査.
    /// </Summary>
    public static readonly int[] CubeFlipedIndices =
    {
        0, 2, 1,
        1, 2, 3, //face right
        1, 2, 0,
        3, 2, 1, //face left
        0, 2, 1,
        1, 2, 3, //face top
        1, 2, 0,
        3, 2, 1, //face bottom
        1, 2, 0,
        3, 2, 1, //face front
        0, 2, 1,
        1, 2, 3, //face back
    };

    public static readonly int[] AONeighborOffsets =
    {
        0, 1, 2,
        6, 7, 0,
        2, 3, 4,
        4, 5, 6,
    };

    /// <Summary>
    /// int3が指定範囲内にあるかを判定する関数.
    /// </Summary>
    public static bool IsInside(int3 point, int3 max, int3 min)
    {
        return math.all(point >= min & point <= max);
        //math.all() は、bool3 や bool4 などのブールベクトルのすべての要素が true であるかを判定する関数.
        //int3の比較は、各要素ごとに比較を行い、結果を bool3 型として返す.
        //& はビット単位の AND 演算子で、対応するビットが両方とも 1 の場合に 1 を返す。Unity.Mathematics では、ベクトル型同士の要素ごとの論理積（AND）を計算する際にも使用される.
    }
    /// <Summary>
    ///元のプログラムを流用するための措置として、各立方体の出っ張っている部分(奥、右、上)をへこませて、反対側のものと同じ位置にする.0:Floor,1:Wall1,2:Wall2.
    /// </Summary>
    public static readonly int3[] VertexOffsetByPartitons =
    {
        new int3(0,-1,0),new int3(0,0,-1),new int3(-1,0,0)
    };
}