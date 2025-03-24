using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Priority_Queue;

public struct Partition
{
    private byte _type; //0 �` 255.
    private byte _fliptype;
    public byte Type
    {
        get { return _type; }
        set { _type = value; }
    }
    public byte FlipType//���ʂ̃^�C�v.
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

    Partition[] floors; Partition[] walls1; Partition[] walls2;//1�����z��.
    public Mesh FloorMesh;
    public Mesh Wall1Mesh;//Wall1,2��1*WallHeight(0.5)�̕���.
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
        GreedyMeshingAlgorithm GMA = new GreedyMeshingAlgorithm();//���̃N���X�̊֐����烁�b�V�����擾���郁�b�V���̎擾�݂̂Ɋ֌W����.
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
        // GameObject �� MeshFilter �� MeshRenderer ��ǉ�
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // ���b�V���𓝍����邽�߂̃I�u�W�F�N�g���쐬
        Mesh combinedMesh = new Mesh();
        CombineInstance[] combine = new CombineInstance[3];

        // �e���b�V���𓝍��p�̔z��Ɋi�[
        combine[0].mesh = FloorMesh;
        combine[0].transform = Matrix4x4.identity;
        combine[1].mesh = Wall1Mesh;
        combine[1].transform = Matrix4x4.identity;
        combine[2].mesh = Wall2Mesh;
        combine[2].transform = Matrix4x4.identity;

        // ���b�V����1�ɓ����ifalse �ɂ��邱�ƂŃT�u���b�V�����ێ��j
        combinedMesh.CombineMeshes(combine, false, false);
        meshFilter.mesh = combinedMesh;

        // �}�e���A����ݒ�i���ƕǂňقȂ�}�e���A�����g�p�j
        //3�ڂ̃T�u���b�V���iWall2�j�ɂ́A�����I��2�ڂ� wallMaterial ���K�p �����.
        meshRenderer.materials = new Material[] { floorMaterial, wallMaterial };
    }
}
