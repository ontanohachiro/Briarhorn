using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// ステップ1: 部屋の初期シード位置を配置 
    /// </summary>
    private bool PlaceInitialSeeds(List<RoomDefinition> roomsToPlace)
    {
        Debug.Log("Placing initial seeds...");
        Dictionary<int, float[,]> weightGrids = InitializeWeightGrids();//各部屋についての重みマップ.
        float[,] _distanceToWall = CalculateDistanceToWall(); // 事前に計算しておくと効率的.どの部屋の重みマップについても同一.
        // 各部屋のシードを配置 (ID順で配置してみる)
        // 配置順序が結果に影響を与える可能性あり。ランダム化や特定の順序（例：大きな部屋から）も検討可能。
        foreach (var kvp in _roomDefinitions.OrderBy(kv => kv.Key)) // ID順に処理
        {
            int roomId = kvp.Key;
            RoomDefinition room = kvp.Value;

            // 1. この部屋用の重みグリッドを計算
            CalculateWeightsForRoom(roomId, weightGrids, _distanceToWall);

            // 2. 最適なシード位置を選択
            Vector2Int? seedPos = SelectBestSeedPosition(weightGrids[roomId]);

            if (seedPos.HasValue)
            {
                room.InitialSeedPosition = seedPos.Value;
                _grid[seedPos.Value.x, seedPos.Value.y] = roomId; // グリッドに配置
                room.CurrentSize = 1; // シードセルでサイズ1
                Debug.Log($"Placed seed for room {roomId} ({room.Type}) at {seedPos.Value}");


                // 3. 配置したセルの周囲の重みを他の部屋のグリッドで下げる
                UpdateWeightsAroundSeed(weightGrids, seedPos.Value, roomId);
            }
            else
            {
                Debug.LogError($"Could not find suitable seed position for room {roomId} ({room.Type}). Not enough space or constraints too strict?");
                // 配置失敗。リセットしてやり直すか、この試行を失敗とする。
                ResetGridAndSeeds(); // グリッドとシード情報をリセット
                return false;
            }
        }
        Debug.Log("Initial seeds placed successfully.");
        return true;
    }


    /// <summary>
    /// 全ての部屋IDに対する重みグリッドを初期化する,1-0
    /// </summary>
    private Dictionary<int, float[,]> InitializeWeightGrids()
    {
        Dictionary<int, float[,]> weightGrids = new Dictionary<int, float[,]>();
        foreach (int roomId in _roomDefinitions.Keys)
        {
            weightGrids[roomId] = new float[_gridSize.x, _gridSize.y];
            // 初期値は 0 でも良いが、後の計算で上書きされる
        }
        return weightGrids;
    }

    /// <summary>
    /// 特定の部屋IDについての重みグリッドを計算する,1-1
    /// </summary>
    private void CalculateWeightsForRoom(int targetRoomId, Dictionary<int, float[,]> weightGrids, float[,] distanceToWall)
    {
        float[,] weights = weightGrids[targetRoomId];
        RoomDefinition targetRoom = _roomDefinitions[targetRoomId];


        // 目標面積から「理想的な」距離を推定 (平方根でおおよその一辺の長さ)
        float estimatedTargetCells = _totalPlaceableCells * (targetRoom.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio));
        // 0割防止
        if (estimatedTargetCells <= 0) estimatedTargetCells = 1;
        float idealDistanceFromWall = Mathf.Sqrt(estimatedTargetCells) * settings.SeedPlacementWeightDistanceFactor; // 係数で調整.サイズの平方根の1/2

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // 0. 配置不可能な場所、既にシードが置かれた場所は重み0
                if (_grid[x, y] != -1)
                {
                    weights[x, y] = 0f;
                    continue;
                }
                //ここから先は_grid[x, y] == -1のみ到達可能.

                // 1. 初期重み設定 (建物内部は1) 
                float currentWeight = 1.0f; // 配置可能なら基本1

                // 2. 外壁からの距離に基づいて重みを調整
                float dist = distanceToWall[x, y];
                if (dist >= 0) // 内部セル
                {
                    // 壁に近いほど重みを減らす。理想距離でピークになるように調整も可能。
                    // 例: 線形減衰 (壁際0, idealDistance以上で1)
                    currentWeight *= Mathf.Clamp01(dist / idealDistanceFromWall);//0から1の値を返す.
                    // 例: ガウシアン的な分布 (idealDistanceで最大、1となる)
                    // currentWeight *= Mathf.Exp(-(dist - idealDistanceFromWall) * (dist - idealDistanceFromWall));
                }
                else
                {
                    currentWeight = 0f; // 念のため（InitializeGridで0になっているはず）
                }


                // 3. 隣接制約に基づいて重みを調整
                if (targetRoom.ConnectivityConstraints != null)
                {
                    foreach (int constraintRoomId in targetRoom.ConnectivityConstraints)
                    {
                        // 相互接続制約の場合、相手側にも制約があるか確認した方が良いが、ここでは片方向で考慮.ターゲットの部屋が存在していて、かつ位置が確定しているか.
                        if (_roomDefinitions.ContainsKey(constraintRoomId) && _roomDefinitions[constraintRoomId].InitialSeedPosition.HasValue)
                        {
                            Vector2Int neighborSeedPos = _roomDefinitions[constraintRoomId].InitialSeedPosition.Value;
                            // 隣接部屋のシード位置に近いほど重みを加算
                            float distanceToNeighborSeed = Vector2Int.Distance(new Vector2Int(x, y), neighborSeedPos);
                            // 例: 一定範囲内ならボーナスを加える
                            if (distanceToNeighborSeed < idealDistanceFromWall) // 範囲は調整可能
                            {
                                // 距離が近いほど大きなボーナス
                                currentWeight += settings.SeedPlacementAdjacencyBonus * (1.0f - Mathf.Clamp01(distanceToNeighborSeed / (idealDistanceFromWall)));
                            }
                        }
                    }
                }

                // 重みは負にならないように.
                //weights[x, y] = Mathf.Max(0, currentWeight); 
            }
        }
    }


    /// <summary>
    /// 各内部セルから最も近い外部セル(壁/境界)までの距離を計算する (BFSベース)
    /// 外部セルや到達不能セルは -1 を返す,1-1-1
    /// </summary>
    public float[,] CalculateDistanceToWall()
    {
        float[,] distances = new float[_gridSize.x, _gridSize.y];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();//先入れ先出し.
        //BFS:(1)開始頂点をキューに入れる.(2)キューの先頭にある頂点に隣接していて探索済みでない頂点を、キューの先頭の頂点のものに1(もしくはリンクに依存した距離)足した数を割り当て、キューに入れる.
        //(3) (2)を繰り返す.
        // 初期化: 内部セルは MaxValue、外部セルは 0
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (settings.InputFootprintGrid[x, y] == 0) // 外部セル(壁)
                {
                    distances[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                }
                else
                {
                    distances[x, y] = float.MaxValue;
                }
            }
        }

        //斜めも考慮する
        Vector2Int[] neighbors = {
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                                                    };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            float currentDist = distances[current.x, current.y];

            foreach (var offset in neighbors)
            {
                Vector2Int neighbor = current + offset;

                if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                {
                    // 斜めを考慮する場合、距離を調整
                    float newDist = currentDist + (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) > 1 ? 1.414f : 1.0f); // 斜めは約1.4倍.二項演算子,trueなら左、falseなら右.
                    //float newDist = currentDist + 1.0f; // 4方向のみの場合

                    if (distances[neighbor.x, neighbor.y] > newDist)
                    {
                        distances[neighbor.x, neighbor.y] = newDist;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // MaxValueのままのセル（内部だが壁から到達不能、またはグリッド外扱い）を -1 にする
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (distances[x, y] == float.MaxValue)
                {
                    distances[x, y] = -1f;
                }
            }
        }


        return distances;
    }


    /// <summary>
    /// 重みグリッドから最も適切なシード位置を選択する ,1-2
    /// </summary>
    private Vector2Int? SelectBestSeedPosition(float[,] weightGrid)
    {
        float maxWeight = -1f;
        List<Vector2Int> bestPositions = new List<Vector2Int>();

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // 配置可能(-1) かつ 重みが計算されているセルのみ対象
                if (_grid[x, y] == -1 && weightGrid[x, y] >= 0)
                {
                    if (weightGrid[x, y] > maxWeight)
                    {
                        maxWeight = weightGrid[x, y];
                        bestPositions.Clear();
                        bestPositions.Add(new Vector2Int(x, y));
                    }
                    else if (weightGrid[x, y] == maxWeight)
                    {
                        bestPositions.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        if (bestPositions.Count > 0)
        {
            // 最高重みの候補が複数あればランダムに選択
            return bestPositions[_random.Next(bestPositions.Count)];
        }
        else
        {
            return null; // 適切な位置が見つからない
        }
    }

    /// <summary>
    /// 配置されたシードの周囲のセルの重みを、他の部屋のグリッドで下げる,1-3
    /// </summary>
    private void UpdateWeightsAroundSeed(Dictionary<int, float[,]> weightGrids, Vector2Int seedPos, int placedRoomId)
    {
        int radius = settings.SeedExclusionRadius; // 設定から取得

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                // 円形の範囲にする場合 (オプション).このままだと四角形になる.
                // if (dx * dx + dy * dy > radius * radius) continue;

                Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                {
                    // 他の全ての部屋の重みグリッドに対して処理
                    foreach (var kvp in weightGrids)
                    {
                        int otherRoomId = kvp.Key;
                        if (otherRoomId != placedRoomId) // 配置した部屋自身のグリッドは変更しない
                        {
                            kvp.Value[pos.x, pos.y] = float.MinValue; // 重みを大幅に下げる.
                        }
                    }
                }
            }
        }
        // Debug.Log($"Updated weights around {seedPos} for other rooms.");
    }


    /// <summary>
    /// PlaceInitialSeedsが失敗した場合などにグリッドとシード情報を初期状態に戻す,1-4
    /// </summary>
    private void ResetGridAndSeeds()
    {
        InitializeGrid(); // グリッドを-1と0の状態に戻す
        foreach (var room in _roomDefinitions.Values)
        {
            room.InitialSeedPosition = null;
            room.CurrentSize = 0;
            // Boundsもリセットが必要なら行う
        }
        Debug.Log("Grid and seeds reset.");
    }

}
