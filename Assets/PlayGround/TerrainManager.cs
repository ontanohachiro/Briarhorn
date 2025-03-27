using System;
using System.Collections.Generic;
using Priority_Queue;
using Unity.Mathematics;
using UnityEngine;

public class TerrainManager : MonoBehaviour
{
    public Material floorMaterial;
    public Material wallMaterial;
    public  int2 AtlasSize;
    public float WallHeight;
    public int3 chunkSize;//�`�����N��ɂ��ẮA�p�[�e�B�V�������ɂ��ẴT�C�Y.
    public int3 chunkNumbers;//����Terrain�����`�����N���ɂ��ẴT�C�Y.
    //public Transform target;

    public GreedyMeshingAlgorithm GMA = new GreedyMeshingAlgorithm();
    Dictionary<int3, Chunk> chunks = new Dictionary<int3, Chunk>();
    private void Start()
    {
        for (int i = 0;i < chunkNumbers.x;i++)
        {
            for(int j = 0;j < chunkNumbers.y;j++)
            {
                for (int k = 0;k < chunkNumbers.z; k++)
                {
                    GenerateChunk(new int3(i, j, k));
                }
            }
        }
    }
    Chunk GenerateChunk(int3 chunkPosition)// chunkPosition��Terrain�ł̈ʒu.�{�N�Z���̍��W�Ƃ͑Ή����Ȃ�.�P�}�X�ɂ���̃`�����N������C���[�W.
    {
        // Creates and initializes chunk, if it doesn't exists already
        if (chunks.ContainsKey(chunkPosition))
            return chunks[chunkPosition];

        GameObject chunkGameObject = new GameObject(chunkPosition.ToString());
        chunkGameObject.transform.SetParent(transform);//������transform�͂��̃X�N���v�g���A�^�b�`����Ă��� GameObject�̂���.
        chunkGameObject.transform.position = VoxelHelper.ChunkToWorldDist(chunkPosition, chunkSize);
        //Debug.Log(chunkGameObject.transform.position);
        chunkGameObject.AddComponent<MeshFilter>();
        chunkGameObject.AddComponent<MeshRenderer>();
        Chunk newChunk = chunkGameObject.AddComponent<Chunk>();

        chunks.Add(chunkPosition, newChunk);
        newChunk.Init(chunkPosition, this); 
        return newChunk;
    }
}
