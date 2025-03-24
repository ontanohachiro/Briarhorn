using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Priority_Queue;

public struct Partition
{
    private byte _type; //0 ～ 255.
    private byte _fliptype;
    public byte Type
    {
        get { return _type; }
        set { _type = value; }
    }
    public byte FlipType//裏面のタイプ.
    {
        get { return _fliptype; }
        set { _fliptype = value; }
    }
    public Partition(byte type,byte fliptype)
    {
        _type = type;
        _fliptype = fliptype;
    }
    public byte GetType(bool flipping)
    {
        if (flipping) return FlipType;
        else return Type;
    }
}
/*
public class Chunk 
{
    public Material mate;
    public int3 Size;
    public int3 Position;
    public Vector3 vecPos;
    int2 AtlasSize;
    float WallHeight;

    Partition[] floors; Partition[] walls1; Partition[] walls2;//1次元配列.
    public Mesh FloorMesh;
    public Mesh Wall1Mesh;//Wall1,2は1*WallHeight(0.5)の平面.
    public Mesh Wall2Mesh;
    public Chunk(int3 Pos , int3 chunkSize,Material material, int2 atlasSize,float wall_height)
    {
        Position = Pos;
        Size = chunkSize;
        mate = material;
        AtlasSize = atlasSize;
        WallHeight = wall_height;
        
        floors = new Partition[Size.x * Size.y * Size.z];
        walls1 = new Partition[Size.x * Size.y * Size.z];
        walls2 = new Partition[Size.x * Size.y * Size.z];
        InitializePartitions(floors);
        InitializePartitions(walls1);
        InitializePartitions(walls2);
        GreedyMeshingAlgorithm GMA = new GreedyMeshingAlgorithm();//このクラスの関数からメッシュを取得するメッシュの取得のみに関係する.
        FloorMesh = GMA.FaceGreedyMesh(floors, Size, AtlasSize,0, WallHeight);
        Wall1Mesh = GMA.FaceGreedyMesh(walls1, Size, AtlasSize, 1, WallHeight);
        Wall2Mesh = GMA.FaceGreedyMesh(walls2 , Size, AtlasSize, 2, WallHeight);
        vecPos = (Vector3)math.float3(Position);
    }
    private void InitializePartitions(Partition[] partitions)
    {
        for (int i = 0; i < partitions.Length; i++)
        {
            byte randomByte = (byte)UnityEngine.Random.Range(0, 2);
            partitions[i] = new Partition(randomByte,randomByte);
        }
    }
}
*/
public class Chunk : MonoBehaviour
{
    public Material floorMaterial;
    public Material wallMaterial;
    public int3 Size;
    public int3 Position;
    public Vector3 vecPos;
    int2 AtlasSize;
    float WallHeight;

    Partition[] floors, walls1, walls2;
    public Mesh FloorMesh, Wall1Mesh, Wall2Mesh;

    public void Initialize(int3 pos, int3 chunkSize, Material floorMat, Material wallMat, int2 atlasSize, float wallHeight)
    {
        Position = pos;
        Size = chunkSize;
        floorMaterial = floorMat;
        wallMaterial = wallMat;
        AtlasSize = atlasSize;
        WallHeight = wallHeight;

        floors = new Partition[Size.x * Size.y * Size.z];
        walls1 = new Partition[Size.x * Size.y * Size.z];
        walls2 = new Partition[Size.x * Size.y * Size.z];
        InitializePartitions(floors);
        InitializePartitions(walls1);
        InitializePartitions(walls2);

        GreedyMeshingAlgorithm GMA = new GreedyMeshingAlgorithm();
        FloorMesh = GMA.FaceGreedyMesh(floors, Size, AtlasSize, 0, WallHeight);
        Wall1Mesh = GMA.FaceGreedyMesh(walls1, Size, AtlasSize, 1, WallHeight);
        Wall2Mesh = GMA.FaceGreedyMesh(walls2, Size, AtlasSize, 2, WallHeight);

        vecPos = (Vector3)math.float3(Position);

        ApplyMeshes();
    }

    private void InitializePartitions(Partition[] partitions)
    {
        for (int i = 0; i < partitions.Length; i++)
        {
            byte randomByte = (byte)UnityEngine.Random.Range(0, 2);
            partitions[i] = new Partition(randomByte, randomByte);
        }
    }

    private void ApplyMeshes()
    {
        // GameObject に MeshFilter と MeshRenderer を追加
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // メッシュを統合するためのオブジェクトを作成
        Mesh combinedMesh = new Mesh();
        CombineInstance[] combine = new CombineInstance[3];

        // 各メッシュを統合用の配列に格納
        combine[0].mesh = FloorMesh;
        combine[0].transform = Matrix4x4.identity;
        combine[1].mesh = Wall1Mesh;
        combine[1].transform = Matrix4x4.identity;
        combine[2].mesh = Wall2Mesh;
        combine[2].transform = Matrix4x4.identity;

        // メッシュを1つに統合（false にすることでサブメッシュを維持）
        combinedMesh.CombineMeshes(combine, false, false);
        meshFilter.mesh = combinedMesh;

        // マテリアルを設定（床と壁で異なるマテリアルを使用）
        //3つ目のサブメッシュ（Wall2）には、自動的に2つ目の wallMaterial が適用 される.
        meshRenderer.materials = new Material[] { floorMaterial, wallMaterial };
    }
}
