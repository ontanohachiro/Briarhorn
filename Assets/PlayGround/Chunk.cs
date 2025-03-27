using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


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
public class Chunk : MonoBehaviour
{
    public Material floorMaterial;
    public Material wallMaterial;
    public int3 Size;
    public int3 Position;//�㕔��Terrain�ł̈ʒu.�{�N�Z���̍��W�Ƃ͑Ή����Ȃ�.�P�}�X�ɂ���̃`�����N������C���[�W.
    public Vector3 vecPos;
    int2 AtlasSize;
    float WallHeight;
    GreedyMeshingAlgorithm GMA;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    public  Partition[] floors, walls1, walls2;
    //public byte[] voxels;
    public Mesh FloorMesh, Wall1Mesh, Wall2Mesh;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }
    public void Init(int3 pos,TerrainManager parent) 
    {
        Position = pos;
        Size = parent.chunkSize;
        floorMaterial = parent.floorMaterial;
        wallMaterial = parent.wallMaterial;
        AtlasSize = parent.AtlasSize;
        WallHeight = parent.WallHeight;
        vecPos = (Vector3)math.float3(Position);

        floors = new Partition[Size.x * Size.y * Size.z];
        walls1 = new Partition[Size.x * Size.y * Size.z];
        walls2 = new Partition[Size.x * Size.y * Size.z];
        InitializeRandomly(floors);
        InitializeRandomly(walls1);
        InitializeRandomly(walls2);

        GMA = parent.GMA;
       
        ApplyMeshes();
    }

    private void InitializeRandomly(Partition[] partitions)
    {
        for (int i = 0; i < partitions.Length; i++)
        {
            byte randomByte = (byte)UnityEngine.Random.Range(1, 2);
            partitions[i] = new Partition(randomByte, randomByte);
        }
    }

    private void ApplyMeshes()
    {

        FloorMesh = GMA.FaceGreedyMesh(floors, Size, AtlasSize, 0, WallHeight);
        Wall1Mesh = GMA.FaceGreedyMesh(walls1, Size, AtlasSize, 1, WallHeight);
        Wall2Mesh = GMA.FaceGreedyMesh(walls2, Size, AtlasSize, 2, WallHeight);

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
        //�T�u���b�V�����Ƃɓ����C���f�b�N�X�̃}�e���A�������蓖�Ă���.
        meshRenderer.materials = new Material[] { floorMaterial, wallMaterial, wallMaterial };
    }
}
