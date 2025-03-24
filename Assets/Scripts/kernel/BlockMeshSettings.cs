using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class BlockMeshSettings
{
    public static byte[,] vertexindex;
    static BlockMeshSettings()
    {
        vertexindex = new byte[,] { { 0, 13, 23 }, { 1, 14, 16 }, { 2, 8, 22 }, { 3, 9, 17 }, { 4, 10, 21 }, { 5, 11, 18 }, { 6, 12, 20 }, { 7, 15, 19 } };
    }
    /// <Summary>
    ///�傫��1�̗�����,��\������W�͒��S�ɂ���.
    /// </Summary>
    public static Mesh CreateCubeMesh()
    {
        Mesh CubeMesh = new Mesh();
        //0.50f, -0.50f, 0.50f      :0,13,23
        //-0.50f, -0.50f, 0.50f     :1,14,16
        //0.50f, 0.50f, 0.50f        :2,8,22
        //-0.50f, 0.50f, 0.50f      :3,9,17
        //0.50f, 0.50f, -0.50f      :4,10,21
        //-0.50f, 0.50f, -0.50f    :5,11,18
        //0.50f, -0.50f, -0.50f    :6,12,20
        //-0.50f, -0.50f, -0.50f  :7,15,19
        Vector3[] vertices = new Vector3[]
   {
            new Vector3(0.50f, -0.50f, 0.50f), // Vertex 0
            new Vector3(-0.50f, -0.50f, 0.50f), // Vertex 1
            new Vector3(0.50f, 0.50f, 0.50f), // Vertex 2
            new Vector3(-0.50f, 0.50f, 0.50f), // Vertex 3
            new Vector3(0.50f, 0.50f, -0.50f), //Vertex 4
            new Vector3(-0.50f, 0.50f, -0.50f), // Vertex 5
            new Vector3(0.50f, -0.50f, -0.50f), // Vertex 6
            new Vector3(-0.50f, -0.50f, -0.50f), // Vertex 7
            new Vector3(0.50f, 0.50f, 0.50f), // Vertex 8
            new Vector3(-0.50f, 0.50f, 0.50f), // Vertex 9
            new Vector3(0.50f, 0.50f, -0.50f), // Vertex 10
            new Vector3(-0.50f, 0.50f, -0.50f), // Vertex 11
            new Vector3(0.50f, -0.50f, -0.50f), //Vertex 12
            new Vector3(0.50f, -0.50f, 0.50f), // Vertex 13
            new Vector3(-0.50f, -0.50f, 0.50f), // Vertex 14
            new Vector3(-0.50f, -0.50f, -0.50f),  // Vertex 15
            new Vector3(-0.50f, -0.50f, 0.50f), // Vertex 16
            new Vector3(-0.50f, 0.50f, 0.50f), // Vertex 17
            new Vector3(-0.50f, 0.50f, -0.50f), // Vertex 18
            new Vector3(-0.50f, -0.50f, -0.50f), // Vertex 19
            new Vector3(0.50f, -0.50f, -0.50f), //Vertex 20
            new Vector3(0.50f, 0.50f, -0.50f), // Vertex 21
            new Vector3(0.50f, 0.50f, 0.50f), // Vertex 22
            new Vector3(0.50f, -0.50f, 0.50f)  // Vertex 23
   };//24��.
        int[] triangles = new int[]
{
      0, 2, 3,
      0, 3, 1,
      8, 4, 5,
      8, 5, 9,
      10, 6, 7,
      10, 7, 11,
      12, 13, 14,
      12, 14, 15,
      16, 17, 18,
      16, 18, 19,
      20, 21, 22,
      20, 22, 23

};//12��.
        Vector2[] uv = new Vector2[]
{
    new Vector2(0, 0), //0
    new Vector2(1, 0), //1
    new Vector2(0, 1),//2
    new Vector2(1,1),//3
    new Vector2(0, 1), //4
    new Vector2(1,1), //5
    new Vector2(0, 1), //6
    new Vector2(1,1), //7
    new Vector2(0, 0), //8
    new Vector2(1, 0), //9
    new Vector2(0, 0), //10
    new Vector2(1, 0), //11
    new Vector2(0, 0), //12
    new Vector2(0, 1), //13
    new Vector2(1,1), //14
    new Vector2(1,0), //15
    new Vector2(0, 0), //16
    new Vector2(0, 1), //17
    new Vector2(1, 1), //18
    new Vector2(1, 0), //19
    new Vector2(0, 0), //20
    new Vector2(0, 1), //21
    new Vector2(1,1), //22
    new Vector2(1, 0), //23
};//24��.

        CubeMesh.vertices = vertices;
        CubeMesh.uv = uv;
        CubeMesh.triangles = triangles;
        CubeMesh.RecalculateNormals();

        return CubeMesh;
    }

    /// <Summary>
    ///1*(y_distor)*(z_distor)�̒����̂����.
    /// </Summary>
    public static Mesh CreateRectangMesh(float y_distor = 1, float z_distor = 1)
    {
        Mesh RectangMesh = CreateCubeMesh();
        Vector3[] vertices = RectangMesh.vertices;
        int columns = vertexindex.GetLength(1);

        if (y_distor != 1)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    vertices[vertexindex[i, j]][1] *= y_distor;
                }
            }
        } //2,3,4,5�͉��ɁA0,1,6,7�͏��.
        if (z_distor != 1)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    vertices[vertexindex[i, j]][2] *= z_distor;
                }
            }
        }//0,1,2,3�͌��ɁA4,5,6,7�͑O��.

        RectangMesh.vertices = vertices;
        return RectangMesh;
    }
    /// <Summary>
    ///1*(thickness)*1�̒����̂����.
    /// </Summary>
    public static Mesh CreateFloorMesh(float floor_thickness)
    {
        Mesh FloorMesh = CreateCubeMesh();
        Vector3[] vertices = FloorMesh.vertices;
        byte[,] vertexindex = new byte[,] { { 0, 13, 23 }, { 1, 14, 16 }, { 2, 8, 22 }, { 3, 9, 17 }, { 4, 10, 21 }, { 5, 11, 18 }, { 6, 12, 20 }, { 7, 15, 19 } }; //�W�s3��.
        //2,3,4,5�͉��ɁA0,1,6,7�͏��.
        int columns = vertexindex.GetLength(1);
        for (int i = 2; i < 6; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                vertices[vertexindex[i, j]][1] = (floor_thickness) / 2;
            }
        }
        for (int j = 0; j < columns; j++)//0
        {
            vertices[vertexindex[0, j]][1] = (-1 * floor_thickness) / 2;
        }
        for (int j = 0; j < columns; j++)//1
        {
            vertices[vertexindex[1, j]][1] = (-1 * floor_thickness) / 2;
        }
        for (int j = 0; j < columns; j++)//6
        {
            vertices[vertexindex[6, j]][1] = (-1 * floor_thickness) / 2;
        }
        for (int j = 0; j < columns; j++)//7
        {
            vertices[vertexindex[7, j]][1] = (-1 * floor_thickness) / 2;
        }

        FloorMesh.vertices = vertices;
        return FloorMesh;
    }
    /// <Summary>
    ///Block_Map�œK�������Cube��Mesh���쐬����.
    /// </Summary>
    public static Mesh SetBasicCubeMesh(bool[] Neighboring, byte type, byte state, float height_distortion, float floor_thickness, float wall_thickness)//Neighboring��6�v�f.
    {
        if (type == 0) return null;
        else
        {
            int[,] syori = new int[,] { { 2, 3, 4, 5, 1, -1 }, { 0, 1, 6, 7, 1, 1 }, { 4, 5, 6, 7, 2, 1 }, { 0, 2, 4, 6, 0, -1 }, { 0, 1, 2, 3, 2, -1 }, { 1, 3, 5, 7, 0, 1 } };//0~3:���_,4;x��y��z��,5;�v���X���}�C�i�X��.
            Mesh CubeMesh = CreateRectangMesh(height_distortion);
            Vector3[] vertices = CubeMesh.vertices;
            int columns = vertexindex.GetLength(1);

            for (int i = 0; i < 6; i++)//6�̖�.
            {
                if (Neighboring[i])
                {
                    float thickness;
                    if (i < 2) thickness = floor_thickness;
                    else thickness = wall_thickness;

                    for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                    {
                        for (int k = 0; k < columns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                        {
                            vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += syori[i, 5] * thickness / 2;
                        }
                    }
                }

            }
            CubeMesh.vertices = vertices;
            return CubeMesh;

        }
    }
    public static Mesh SetBasicWallMesh(bool[] Neighboring, byte type, byte state, float height_distortion, float floor_thickness, float wall_thickness)//Neighboring��12�v�f.
    {
        if (type == 0) return null;
        else
        {
            int[,] syori = new int[,] { { 2, 3, 4, 5, 1, -1 }, { 0, 2, 4, 6, 0, -1 }, { 0, 1, 6, 7, 1, 1 }, { 1, 3, 5, 7, 0, 1 } };
            //0,1:��ɐڂ���ӂ̒��_.2,3:���ɐڂ���ӂ̒��_.0~3:�ڂ���ʂ̒��_,4;�ύX������W��x(0)��y(1)��z(2)��,5;�v�Z����Ƃ��v���X���}�C�i�X���i�ʂ��}�C�i�X�̈ʒu�ɂ����1�ɁA�v���X�̈ʒu�ɂ����-1�j.

            Mesh WallMesh = CreateRectangMesh(height_distortion, wall_thickness);
            Vector3[] vertices = WallMesh.vertices;
            int syoritimes = syori.GetLength(0);
            int vertexindexcolumns = vertexindex.GetLength(1);
            float thickness;
            for (int i = 0; i < syoritimes; i++)//�S�����ɂ��Ă̏���.
            {
                if (i % 2 == 0) thickness = floor_thickness;
                else thickness = wall_thickness;

                if (Neighboring[(i * 3)] && Neighboring[(i * 3) + 1])//5,8.
                {
                    for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                    {
                        for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                        {
                            vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += syori[i, 5] * thickness / 2;
                        }
                    }
                }
                //wall�ɂ��Ă�neighboring,wall�������Ƃ��ɏ������s��.
                else if (!Neighboring[(i * 3) + 2])
                {
                    if (Neighboring[(i * 3)])
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            int reverse = 1;
                            if (2 <= j) reverse = -1;
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += reverse * syori[i, 5] * thickness / 2;
                            }
                        }
                    }
                    else if (Neighboring[(i * 3) + 1])
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            int reverse = 1;
                            if (j <= 1) reverse = -1;
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += reverse * syori[i, 5] * thickness / 2;
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            int reverse = -1;
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += reverse * syori[i, 5] * thickness / 2;
                            }
                        }
                    }
                }
                }
            WallMesh.vertices = vertices;
            return WallMesh;
        }
    }
    public static Mesh SetBasicFloorMesh(bool[] Neighboring, byte type, byte state, float height_distortion, float floor_thickness, float wall_thickness)//Neighboring��12�v�f.
    {
        if (type == 0) return null;
        else
        {
            int[,] syori = new int[,] { { 4, 5, 6, 7, 2, 1 }, { 2, 4, 0, 6, 0, -1 }, { 2, 3, 0, 1, 2, -1 }, { 3, 5, 1, 7, 0, 1 } };
            //0,1:��ɐڂ���ӂ̒��_.2,3:���ɐڂ���ӂ̒��_.0~3:�ڂ���ʂ̒��_,4;�ύX������W��x(0)��y(1)��z(2)��,5;�v�Z����Ƃ��v���X���}�C�i�X���i�ʂ��}�C�i�X�̈ʒu�ɂ����1�ɁA�v���X�̈ʒu�ɂ����-1�j.

            Mesh FloorMesh = CreateRectangMesh(floor_thickness);
            Vector3[] vertices = FloorMesh.vertices;
            int syoritimes = syori.GetLength(0);
            int vertexindexcolumns = vertexindex.GetLength(1);

            for (int i = 0; i < syoritimes; i++)//�S�����ɂ��Ă̏���.
            {
                //floor�ɂ��Ă�neighboring,floor�������Ƃ��ɏ������s��.
                if (!Neighboring[(i * 3) + 2])
                {
                    if (Neighboring[(i * 3)] && Neighboring[(i * 3) + 1])
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += syori[i, 5] * wall_thickness / 2;
                            }
                        }
                    }
                    else if (Neighboring[(i * 3)])
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            int reverse = 1;
                            if (2 <= j) reverse = -1;
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += reverse * syori[i, 5] * wall_thickness / 2;
                            }
                        }
                    }
                    else if (Neighboring[(i * 3) + 1])
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            int reverse = 1;
                            if (j <= 1) reverse = -1;
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += reverse * syori[i, 5] * wall_thickness / 2;
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < 4; j++)//4�̒��_�O���[�v.
                        {
                            int reverse = -1;
                            for (int k = 0; k < vertexindexcolumns; k++)//�e���_�O���[�v�ɑ����钸�_������.
                            {
                                vertices[vertexindex[syori[i, j], k]][syori[i, 4]] += reverse * syori[i, 5] * wall_thickness / 2;
                            }
                        }
                    }

                }

            }
            FloorMesh.vertices = vertices;
            return FloorMesh;
        }
    }


    /// <Summary>
    ///0:Cube,1:Wall1,2:Wall1,3:Floor.
    /// </Summary>
    public static Mesh SetBasicBlockMesh(bool[] Neighboring, byte type, byte state, float height_distortion, float floor_thickness, float wall_thickness, byte blocktype)
    {
        switch (blocktype)
        {
            case 0:
                return SetBasicCubeMesh(Neighboring, type, state, height_distortion, floor_thickness, wall_thickness);
            case 1:
                return SetBasicWallMesh(Neighboring, type, state, height_distortion, floor_thickness, wall_thickness);
            case 2:
                return SetBasicWallMesh(Neighboring, type, state, height_distortion, floor_thickness, wall_thickness);
            case 3:
                return SetBasicFloorMesh(Neighboring, type, state, height_distortion, floor_thickness, wall_thickness);
            default:
                return null;
        }
    }
}
