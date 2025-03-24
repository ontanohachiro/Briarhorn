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
    // 平面を生成する関数
    public GameObject CreatePlane(Vector3 position, Vector2 size, byte type)
    {
        // 新しいゲームオブジェクトを作成し、"GeneratedPlane"と名付ける
        GameObject plane = new GameObject("GeneratedPlane");

        // 作成したゲームオブジェクトにMeshFilterとMeshRendererコンポーネントを追加
        MeshFilter meshFilter = plane.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = plane.AddComponent<MeshRenderer>();

        // メッシュを新規作成
        Mesh mesh = new Mesh();

        // 頂点の配列とUVマッピングの配列を定義.
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
            int2 atlasPosition = new int2 { x = type % AtlasSize.x, y = type / AtlasSize.x };//2Dマップ上での位置を計算.
            // public static readonly float2[] CubeUVs = { new float2(0f, 0f), new float2(1.0f, 0f), new float2(0f, 1.0f), new float2(1.0f, 1.0f) };
            uv[i] = new Vector4(VoxelHelper.CubeUVs[i].x * size.x, VoxelHelper.CubeUVs[i].y * size.y, atlasPosition.x, atlasPosition.y);//uvの中にテクスチャについての情報が入っている.
            //Debug.Log(uv[i]);

        }

        // 三角形の頂点インデックスを定義
        int[] triangles = new int[6]
        {
            0, 2, 1, // 左下の三角形
            2, 3, 1  // 右上の三角形
        };

        // メッシュに頂点、三角形、UVを設定
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.SetUVs(0, uv, 0, 4);


        // メッシュを再計算して最適化
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // メッシュフィルターにメッシュを割り当て
        meshFilter.mesh = mesh;

        // 平面の位置を設定
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

    // ブロックが透明かどうかをチェック
    private static bool IsTransparent(Byte partition_type)
    {
        /*
        // チャンクの境界外は透明と判断
        if (!VoxelHelper.BoundaryCheck(position, chunkSize))
            return true;

        int index = VoxelHelper.To1DIndex(position, chunkSize);
        */
        return partition_type == 0 ;
    }
    // 面のQuadをメッシュに追加
    private void AddFaceQuad(Byte type, int width, int height, int3 position, int direction, int PartitonClass)
    {
        float FloatWidth = (float)(width);
        float FloatHeight = (float)(height);
        float3 FloatPosition = (float3)(position);
        FloatPosition.y *= WallHeight;
        if (PartitonClass != 0) //壁であるとき.
        {
            FloatHeight *= this.WallHeight;
        }
        // 頂点インデックスの開始位置
        int vertexOffset = rectangleNumber * 4;

        // atlasPositionの計算.
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
                if (partiton.Flipped == true)//裏むきとなっている.
                {
                    atlasIndex = (int)partiton.Type * 3 + 2;
                }
                else atlasIndex = (int)partiton.Type * 3 + 1;
                break;
            case 3:
                if (partiton.Flipped == true)//裏むきとなっている.
                {
                    atlasIndex = (int)partiton.Type * 3 + 1;
                }
                else atlasIndex = (int)partiton.Type * 3 + 2;
                break;
        }
        */
        int2 atlasPosition = new int2 { x = atlasIndex % AtlasSize.x, y = atlasIndex / AtlasSize.x };//2Dマップ上での位置を計算.
        int3 VDO = VoxelHelper.VoxelDirectionOffsets[direction];
        Vector3 normal = new Vector3(VDO.x, VDO.y, VDO.z);
        if (direction % 2 == 0)//偶数なら.
        {
            position += VoxelHelper.VertexOffsetByPartitons[PartitonClass];
        }
        for (int i = 0; i < 4; i++)  // 四角形の頂点を計算
        {
            float3 vertex = VoxelHelper.CubeVertices[VoxelHelper.CubeFaces[i + direction * 4]];
            //width*heightの長方形に拡大.
            vertex[VoxelHelper.DirectionAlignedX[direction]] *= FloatWidth;
            vertex[VoxelHelper.DirectionAlignedY[direction]] *= FloatHeight;
            float4 uv = new float4 { x = VoxelHelper.CubeUVs[i].x * width, y = VoxelHelper.CubeUVs[i].y * height, z = atlasPosition.x, w = atlasPosition.y };//uvの中にテクスチャについての情報が入っている.
            vertices.Add(vertex + FloatPosition);
            normals.Add(normal);//各頂点に対して法線が必要
            uvs.Add(uv);
        }
        //int firstIndiceIndex = faceIndex * 6;
        // インデックスを追加（2つの三角形で1つの四角形）
        for (int i = 0; i < 6; i++)
        {
            // Compare the light sum of each pair of vertices.
            triangles.Add(VoxelHelper.CubeIndices[direction * 6 + i] + vertexOffset);
        }
        rectangleNumber++;
    }

    private void GreedyByDirection(Partition[] partitions, int3 chunkSize, int PartitionClass,int direction)//対象方向のGreedy Meshingを行う関数.
    {
        bool flipping = false;
        if (direction % 2 == 1)//dirが奇数なら裏面を描画することになる
        {
            flipping = true;
        }
        // 検査済みのブロックを記録する辞書
        Dictionary<int3, bool> inspected = new Dictionary<int3, bool>();

        // 上面方向に合わせた座標系での軸設定.dirX,Y,Zは対象となる面を正面から見た時の座標系（奥行きがZとなる）
        int dirX = VoxelHelper.DirectionAlignedX[direction]; //  X軸
        int dirY = VoxelHelper.DirectionAlignedY[direction]; // Y軸
        int dirZ = VoxelHelper.DirectionAlignedZ[direction]; // Z軸

        // 深さ方向を走査
        for (int z = 0; z < chunkSize[dirZ]; z++)
        {
            // XY平面を走査
            for (int x = 0; x < chunkSize[dirX]; x++)
            {
                for (int y = 0; y < chunkSize[dirY];)
                {
                    // グリッド位置を作成
                    int3 gridPos = new int3 { [dirX] = x, [dirY] = y, [dirZ] = z };
                    //インデクサーを使用する代入.dirX,Y,Zに対応するインデックスにその時点でのx,y,zが代入される

                    // ボクセル位置を1次元インデックスに変換
                    int partitionIndex = VoxelHelper.To1DIndex(gridPos, chunkSize);
                    byte partitionType = partitions[partitionIndex].GetType(flipping);

                    // 透明(描画しない)か検査済みならスキップ.ContainsKeyはkeyを持たないdictionaryを検索する時に効率的.
                    if (IsTransparent(partitionType) || inspected.ContainsKey(gridPos))
                    {
                        y++;
                        continue;
                    }
                    /*
                    // 上方向の隣接ブロックを取得
                    int3 neighborPos = gridPos + VoxelHelper.VoxelDirectionOffsets[direction];//depthの方向に沿う.
                                                                                              //隣接チャンクの情報は必要とならない.
                                                                                              //上方向のブロックが透明であったら、描画する必要がある.
                    if (!IsTransparent(partitions, neighborPos, chunkSize))//透明でなかったら、描画する必要はない.
                    {
                        z++;
                        continue;
                    }
                    */
                    // 現在のブロックを検査済みとマーク
                    inspected[gridPos] = true;

                    int height;
                    for (height = 1; height + y < chunkSize[dirY]; height++)//chunksizeの性質からheigth>=1から始まる.
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
                    }//最終的には、positionからy方向にheight分連続した同じvoxelが並ぶことが分かる.
                     //注意しなくてはならないのは、y+heightではなく(y+height) -1までが連続した同じvoxelであるということ.

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
                        //今いるwidthの列での連続するVoxelが一列目の高さ(height)よりも低かったら、この時点で必ず isDone = trueとなっていて、ループは打ち切りとなる.
                        if (isDone)
                        {
                            break;
                        }
                        //ここにこれたということは、今いるwidthの列はyから(y+height) -1まで連続した同じvoxelであり、さらに一度他のものに取り込まれたわけでもない.
                        for (int dy = 0; dy < height; dy++)//タグ付け.inspectedにする.
                        {
                            int3 nextPos = gridPos;
                            nextPos[dirX] += width;
                            nextPos[dirY] += dy;
                            inspected[nextPos] = true;
                        }
                    }
                    //最終的には、(x,y,z)から(x+width-1 , y+height-1.z)の長方形で囲まれる部分は同じvoxelを持っていると分かる.
                    // マージされたQuadをメッシュに追加
                    AddFaceQuad(partitionType, width, height, gridPos, direction,PartitionClass);
                    y += height;// 処理したブロック分だけyを進める.
                }
            }
        }
    }
    /// <summary>
    ///PartitonClass:Partitionの種類を表す.0:Floor,1:Wall1,2:Wall2
    /// </summary>
    public Mesh FaceGreedyMesh(Partition[] partitions, int3 chunkSize, int2 atlas_size,int PartitionClass,float wall_height)//Greedy Meshingを行う関数.
    {
        //動的にメッシュを変更するためにそのプロパティを一度リストとして作り、全体的なアルゴリズムの終了時にまとめて代入する.
        vertices = new List<Vector3>(1000);
        normals = new List<Vector3>(1000);
        uvs = new List<Vector4>(1000);
        triangles = new List<int>(1000);
        rectangleNumber = 0;
        this.AtlasSize = atlas_size;
        this.WallHeight = wall_height;
        /*  directionが  
            - 0 (0-4): 右側面 (+X方向)  
            - 1 (5-8): 左側面 (-X方向)  
            - 2 (9-12): 上面 (+Y方向)  
            - 3 (13-16): 底面 (-Y方向)  
            - 4 (17-20): 奥の側面 (+Z方向)  
            - 5 (21-24): 手前の側面 (-Z方向)  */
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




    
   
    
