using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
/*

public class PlaneGenerator : MonoBehaviour
{
    public int2 AtlasSize;
    public Material Mate;
    // ���ʂ𐶐�����֐�
    public GameObject CreatePlane(Vector3 position, Vector2 size, byte type)
    {
        // �V�����Q�[���I�u�W�F�N�g���쐬���A"GeneratedPlane"�Ɩ��t����
        GameObject plane = new GameObject("GeneratedPlane");

        // �쐬�����Q�[���I�u�W�F�N�g��MeshFilter��MeshRenderer�R���|�[�l���g��ǉ�
        MeshFilter meshFilter = plane.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = plane.AddComponent<MeshRenderer>();

        // ���b�V����V�K�쐬
        Mesh mesh = new Mesh();

        // ���_�̔z���UV�}�b�s���O�̔z����`.
        Vector3[] vertices = new Vector3[4]
      {
            new Vector3(0, 0, 0),
            new Vector3(size.x, 0, 0),
            new Vector3(0, 0, size.y),
            new Vector3(size.x, 0, size.y)
      };

        Vector4[] uv = new Vector4[4];
        for (int i = 0; i < 4; i++)
        {
            int2 atlasPosition = new int2 { x = type % AtlasSize.x, y = type / AtlasSize.x };//2D�}�b�v��ł̈ʒu���v�Z.
            // public static readonly float2[] CubeUVs = { new float2(0f, 0f), new float2(1.0f, 0f), new float2(0f, 1.0f), new float2(1.0f, 1.0f) };
            uv[i] = new Vector4(VoxelHelper.CubeUVs[i].x * size.x, VoxelHelper.CubeUVs[i].y * size.y, atlasPosition.x, atlasPosition.y);//uv�̒��Ƀe�N�X�`���ɂ��Ă̏�񂪓����Ă���.
            //Debug.Log(uv[i]);

        }

        // �O�p�`�̒��_�C���f�b�N�X���`
        int[] triangles = new int[6]
        {
            0, 2, 1, // �����̎O�p�`
            2, 3, 1  // �E��̎O�p�`
        };

        // ���b�V���ɒ��_�A�O�p�`�AUV��ݒ�
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.SetUVs(0, uv, 0, 4);


        // ���b�V�����Čv�Z���čœK��
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // ���b�V���t�B���^�[�Ƀ��b�V�������蓖��
        meshFilter.mesh = mesh;

        // ���ʂ̈ʒu��ݒ�
        plane.transform.position = position;

        meshRenderer.material = Mate;
        return plane;
    }


}
*/
public class GreedyMeshingAlgorithm
{
    private List<Vector3> vertices;
    private List<Vector3> normals;
    private List<Vector4> uvs;
    private List<int> triangles;
    private int rectangleNumber = 0;
    int2 AtlasSize;
    float WallHeight;

    // �u���b�N���������ǂ������`�F�b�N
    private static bool IsTransparent(Byte partition_type)
    {
        /*
        // �`�����N�̋��E�O�͓����Ɣ��f
        if (!VoxelHelper.BoundaryCheck(position, chunkSize))
            return true;

        int index = VoxelHelper.To1DIndex(position, chunkSize);
        */
        return partition_type == 0 ;
    }
    // �ʂ�Quad�����b�V���ɒǉ�
    private void AddFaceQuad(Byte type, int width, int height, int3 position, int direction, int PartitonClass)
    {
        float FloatWidth = (float)(width);
        float FloatHeight = (float)(height);
        float3 FloatPosition = (float3)(position);
        FloatPosition.y *= WallHeight;
        if (PartitonClass != 0) //�ǂł���Ƃ�.
        {
            FloatHeight *= this.WallHeight;
        }
        // ���_�C���f�b�N�X�̊J�n�ʒu
        int vertexOffset = rectangleNumber * 4;

        // atlasPosition�̌v�Z.
        int atlasIndex = type;
        /*
         switch (direction)// Direction = 0,1,4,5:sides 2:up 3:down
        {
            case 0:
            case 1:
            case 4:
            case 5://side
                atlasIndex = (int)partiton.Type * 3;
                break;
            case 2:
                if (partiton.Flipped == true)//���ނ��ƂȂ��Ă���.
                {
                    atlasIndex = (int)partiton.Type * 3 + 2;
                }
                else atlasIndex = (int)partiton.Type * 3 + 1;
                break;
            case 3:
                if (partiton.Flipped == true)//���ނ��ƂȂ��Ă���.
                {
                    atlasIndex = (int)partiton.Type * 3 + 1;
                }
                else atlasIndex = (int)partiton.Type * 3 + 2;
                break;
        }
        */
        int2 atlasPosition = new int2 { x = atlasIndex % AtlasSize.x, y = atlasIndex / AtlasSize.x };//2D�}�b�v��ł̈ʒu���v�Z.
        int3 VDO = VoxelHelper.VoxelDirectionOffsets[direction];
        Vector3 normal = new Vector3(VDO.x, VDO.y, VDO.z);
        if (direction % 2 == 0)//�����Ȃ�.
        {
            position += VoxelHelper.VertexOffsetByPartitons[PartitonClass];
        }
        for (int i = 0; i < 4; i++)  // �l�p�`�̒��_���v�Z
        {
            float3 vertex = VoxelHelper.CubeVertices[VoxelHelper.CubeFaces[i + direction * 4]];
            //width*height�̒����`�Ɋg��.
            vertex[VoxelHelper.DirectionAlignedX[direction]] *= FloatWidth;
            vertex[VoxelHelper.DirectionAlignedY[direction]] *= FloatHeight;
            float4 uv = new float4 { x = VoxelHelper.CubeUVs[i].x * width, y = VoxelHelper.CubeUVs[i].y * height, z = atlasPosition.x, w = atlasPosition.y };//uv�̒��Ƀe�N�X�`���ɂ��Ă̏�񂪓����Ă���.
            vertices.Add(vertex + FloatPosition);
            normals.Add(normal);//�e���_�ɑ΂��Ė@�����K�v
            uvs.Add(uv);
        }
        //int firstIndiceIndex = faceIndex * 6;
        // �C���f�b�N�X��ǉ��i2�̎O�p�`��1�̎l�p�`�j
        for (int i = 0; i < 6; i++)
        {
            // Compare the light sum of each pair of vertices.
            triangles.Add(VoxelHelper.CubeIndices[direction * 6 + i] + vertexOffset);
        }
        rectangleNumber++;
    }

    private void GreedyByDirection(Partition[] partitions, int3 chunkSize, int PartitionClass,int direction)//�Ώە�����Greedy Meshing���s���֐�.
    {
        bool flipping = false;
        if (direction % 2 == 1)//dir����Ȃ痠�ʂ�`�悷�邱�ƂɂȂ�
        {
            flipping = true;
        }
        // �����ς݂̃u���b�N���L�^���鎫��
        Dictionary<int3, bool> inspected = new Dictionary<int3, bool>();

        // ��ʕ����ɍ��킹�����W�n�ł̎��ݒ�.dirX,Y,Z�͑ΏۂƂȂ�ʂ𐳖ʂ��猩�����̍��W�n�i���s����Z�ƂȂ�j
        int dirX = VoxelHelper.DirectionAlignedX[direction]; //  X��
        int dirY = VoxelHelper.DirectionAlignedY[direction]; // Y��
        int dirZ = VoxelHelper.DirectionAlignedZ[direction]; // Z��

        // �[�������𑖍�
        for (int z = 0; z < chunkSize[dirZ]; z++)
        {
            // XY���ʂ𑖍�
            for (int x = 0; x < chunkSize[dirX]; x++)
            {
                for (int y = 0; y < chunkSize[dirY];)
                {
                    // �O���b�h�ʒu���쐬
                    int3 gridPos = new int3 { [dirX] = x, [dirY] = y, [dirZ] = z };
                    //�C���f�N�T�[���g�p������.dirX,Y,Z�ɑΉ�����C���f�b�N�X�ɂ��̎��_�ł�x,y,z����������

                    // �{�N�Z���ʒu��1�����C���f�b�N�X�ɕϊ�
                    int partitionIndex = VoxelHelper.To1DIndex(gridPos, chunkSize);
                    byte partitionType = partitions[partitionIndex].GetType(flipping);

                    // ����(�`�悵�Ȃ�)�������ς݂Ȃ�X�L�b�v.ContainsKey��key�������Ȃ�dictionary���������鎞�Ɍ����I.
                    if (IsTransparent(partitionType) || inspected.ContainsKey(gridPos))
                    {
                        y++;
                        continue;
                    }
                    /*
                    // ������̗אڃu���b�N���擾
                    int3 neighborPos = gridPos + VoxelHelper.VoxelDirectionOffsets[direction];//depth�̕����ɉ���.
                                                                                              //�אڃ`�����N�̏��͕K�v�ƂȂ�Ȃ�.
                                                                                              //������̃u���b�N�������ł�������A�`�悷��K�v������.
                    if (!IsTransparent(partitions, neighborPos, chunkSize))//�����łȂ�������A�`�悷��K�v�͂Ȃ�.
                    {
                        z++;
                        continue;
                    }
                    */
                    // ���݂̃u���b�N�������ς݂ƃ}�[�N
                    inspected[gridPos] = true;

                    int height;
                    for (height = 1; height + y < chunkSize[dirY]; height++)//chunksize�̐�������heigth>=1����n�܂�.
                    {
                        int3 nextPos = gridPos;
                        nextPos[dirY] += height;

                        Byte nextpartitionType = partitions[VoxelHelper.To1DIndex(nextPos, chunkSize)].GetType(flipping);

                        if (partitionType != nextpartitionType)
                        {
                            break;
                        }

                        if (inspected.ContainsKey(nextPos))
                            break;

                        inspected[nextPos] = true;
                    }//�ŏI�I�ɂ́Aposition����y������height���A����������voxel�����Ԃ��Ƃ�������.
                     //���ӂ��Ȃ��Ă͂Ȃ�Ȃ��̂́Ay+height�ł͂Ȃ�(y+height) -1�܂ł��A����������voxel�ł���Ƃ�������.

                    bool isDone = false;
                    int width;
                    for (width = 1; width + x < chunkSize[dirX]; width++)
                    {
                        for (int dy = 0; dy < height; dy++)//0<=dy<=(height-1).
                        {
                            int3 nextPos = gridPos;
                            nextPos[dirX] += width;
                            nextPos[dirY] += dy;

                            Byte nextpartitionType = partitions[VoxelHelper.To1DIndex(nextPos, chunkSize)].GetType(flipping);

                            if (partitionType != nextpartitionType || inspected.ContainsKey(nextPos))
                            {
                                isDone = true;
                                break;
                            }
                        }
                        //������width�̗�ł̘A������Voxel�����ڂ̍���(height)�����Ⴉ������A���̎��_�ŕK�� isDone = true�ƂȂ��Ă��āA���[�v�͑ł��؂�ƂȂ�.
                        if (isDone)
                        {
                            break;
                        }
                        //�����ɂ��ꂽ�Ƃ������Ƃ́A������width�̗��y����(y+height) -1�܂ŘA����������voxel�ł���A����Ɉ�x���̂��̂Ɏ�荞�܂ꂽ�킯�ł��Ȃ�.
                        for (int dy = 0; dy < height; dy++)//�^�O�t��.inspected�ɂ���.
                        {
                            int3 nextPos = gridPos;
                            nextPos[dirX] += width;
                            nextPos[dirY] += dy;
                            inspected[nextPos] = true;
                        }
                    }
                    //�ŏI�I�ɂ́A(x,y,z)����(x+width-1 , y+height-1.z)�̒����`�ň͂܂�镔���͓���voxel�������Ă���ƕ�����.
                    // �}�[�W���ꂽQuad�����b�V���ɒǉ�
                    AddFaceQuad(partitionType, width, height, gridPos, direction,PartitionClass);
                    y += height;// ���������u���b�N������y��i�߂�.
                }
            }
        }
    }
    /// <summary>
    ///PartitonClass:Partition�̎�ނ�\��.0:Floor,1:Wall1,2:Wall2
    /// </summary>
    public Mesh FaceGreedyMesh(Partition[] partitions, int3 chunkSize, int2 atlas_size,int PartitionClass,float wall_height)//Greedy Meshing���s���֐�.
    {
        //���I�Ƀ��b�V����ύX���邽�߂ɂ��̃v���p�e�B����x���X�g�Ƃ��č��A�S�̓I�ȃA���S���Y���̏I�����ɂ܂Ƃ߂đ������.
        vertices = new List<Vector3>(1000);
        normals = new List<Vector3>(1000);
        uvs = new List<Vector4>(1000);
        triangles = new List<int>(1000);
        rectangleNumber = 0;
        this.AtlasSize = atlas_size;
        this.WallHeight = wall_height;
        /*  direction��  
            - 0 (0-4): �E���� (+X����)  
            - 1 (5-8): ������ (-X����)  
            - 2 (9-12): ��� (+Y����)  
            - 3 (13-16): ��� (-Y����)  
            - 4 (17-20): ���̑��� (+Z����)  
            - 5 (21-24): ��O�̑��� (-Z����)  */
        switch (PartitionClass)
        {
            case 0://Floor
                GreedyByDirection(partitions, chunkSize, PartitionClass, 2);
                GreedyByDirection(partitions, chunkSize, PartitionClass, 3);
                break;
            case 1://Wall1
                GreedyByDirection(partitions, chunkSize, PartitionClass, 4);
                GreedyByDirection(partitions, chunkSize, PartitionClass, 5);
                break;
            case 2://Wall2
                GreedyByDirection(partitions, chunkSize, PartitionClass, 0);
                GreedyByDirection(partitions, chunkSize, PartitionClass, 1);
                break;
        } 
       
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.SetUVs(0, uvs);
        mesh.triangles = triangles.ToArray();

        Debug.Log($"rectangleNumber: {rectangleNumber}");
        Debug.Log($"Vertices: {vertices.Count}, UVs: {uvs.Count}");
        return mesh;

    }

}




    
   
    
