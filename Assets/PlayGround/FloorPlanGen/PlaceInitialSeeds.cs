using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// ステップ1: 部屋の初期シード位置を配置.
    /// </summary>
    private bool PlaceInitialSeeds() 
    {
        Debug.Log("Placing initial seeds...");
        // 各部屋のIDをキーとした重みマップの辞書を初期化します。
        Dictionary<int, float[,]> weightGrids = InitializeWeightGrids();
        // 壁までの距離を事前に計算します。これは全室共通の重み計算に使用されます。
        float[,] _distanceToWall = CalculateDistanceToWall();

        if (MVinstance.todebug == ToDebug.CalculateDistanceToWall)
        {
            matrixToDebug = _distanceToWall;
        }

        // 各部屋のシードを配置します。
        foreach (var room in _roomDefinitions)
        {
            // 1. この部屋（room）用の重みグリッドを計算します。
            // CalculateWeightsForRoomメソッドは、指定されたroom.IDに基づいて、対応するweightGrids内の重みマップを更新します。
            CalculateWeightsForRoom(room.ID, weightGrids, _distanceToWall);
        }
        foreach (var room in _roomDefinitions.OrderBy(r => r.ID)) // RoomDefinitionのIDプロパティでソートして処理します。
        {
            // 2. 計算された重みマップ（weightGrids[room.ID]）に基づいて、最適なシード位置を選択します。
            Vector2Int? seedPos = SelectBestSeedPosition(weightGrids[room.ID]);

            if (seedPos.HasValue)
            {
                room.InitialSeedPosition = seedPos.Value; // 選択された位置を部屋の初期シード位置として記録します。
                _grid[seedPos.Value.x, seedPos.Value.y] = room.ID; // グリッド上の対応するセルに部屋のIDを割り当てます。
                room.CurrentSize = 1; // シードセルが配置されたので、部屋の現在のサイズを1とします。
                Debug.Log($"Placed seed for room {room.ID} ({room.Type}) at {seedPos.Value}");
                if (MVinstance.todebug == ToDebug.SelectBestSeedPosition)
                {
                    //前、ここが初期化されてないことがあった.
                    matrixToDebug[seedPos.Value.x, seedPos.Value.y] = (float)(room.ID);
                }
                // 3. 配置したシードセルの周囲の重みを、他の部屋の重みグリッドで下げます。
                // UpdateWeightsAroundSeedメソッドは、配置された部屋（room.ID）以外の全ての部屋の重みマップを更新します。
                UpdateWeightsAroundSeed(weightGrids, seedPos.Value, room.ID);
            }
            else
            {
                Debug.LogError($"Could not find suitable seed position for room {room.ID} ({room.Type}). Not enough space or constraints too strict?");
                // 配置失敗。リセットしてやり直すか、この試行を失敗とします。
                ResetGridAndSeeds(); // グリッドとシード情報をリセットします。
                return false; // シード配置失敗のためfalseを返します。
            }
        }
        ExpandTo22(_distanceToWall);
        if (MVinstance.todebug == ToDebug.ExpandTo22)
        {
            matrixToDebug = ConvertMatrix(_grid, "float");
        }
        Debug.Log("Initial seeds placed successfully.");
        return true; // 全てのシードが正常に配置されたためtrueを返します。
    }


    /// <summary>
    /// 全ての部屋IDに対する重みグリッドを初期化する関数。
    /// 各部屋のIDをキーとし、値として新しいfloat型の2次元配列（重みマップ）を持つ辞書を作成します。1-0
    /// </summary>
    private Dictionary<int, float[,]> InitializeWeightGrids()
    {
        // 部屋IDをキー、重みマップを値とする辞書を初期化します。
        Dictionary<int, float[,]> weightGrids = new Dictionary<int, float[,]>();
        // _roomDefinitionsに格納されている全ての部屋定義に対して処理を行います。
        foreach (var room in _roomDefinitions) // RoomDefinitionのIDをキーとして使用します。
        {
            // 新しい重みマップ（float型の2次元配列）を作成し、辞書に追加します。
            // グリッドサイズは_gridSize.x と _gridSize.yに基づきます。
            weightGrids[room.ID] = new float[_gridSize.x, _gridSize.y];
            // 初期値は 0 でも良いですが、後のCalculateWeightsForRoomで上書きされます。
        }
        return weightGrids; // 初期化された重みグリッドの辞書を返します。
    }

    /// <summary>
    /// 特定の部屋IDについての重みグリッドを計算する関数。1-1
    /// </summary>
    /// <param name="targetRoomId">重みを計算する対象の部屋のID。</param>
    /// <param name="weightGrids">全室の重みグリッドが格納された辞書。</param>
    /// <param name="distanceToWall">各セルから最も近い壁までの距離を格納した2次元配列。</param>
    private void CalculateWeightsForRoom(int targetRoomId, Dictionary<int, float[,]> weightGrids, float[,] distanceToWall)
    {
        // 対象の部屋の重みマップへの参照を取得します。
        float[,] weights = weightGrids[targetRoomId];
        // 対象の部屋の定義情報を取得します。ID>=1より、id１の部屋がリストの0要素.注意.
        RoomDefinition targetRoom = _roomDefinitions[targetRoomId-1];

        // 目標面積から「理想的な」壁からの距離を推定します。
        // 全配置可能セル数と部屋のサイズ比率から、この部屋が占めるべきおおよそのセル数を計算します。
        float estimatedTargetCells = _totalPlaceableCells * (targetRoom.SizeRatio / _roomDefinitions.Sum(r => r.SizeRatio));
        // 0除算を防ぐため、estimatedTargetCellsが0以下の場合は1とします。
        if (estimatedTargetCells <= 0) estimatedTargetCells = 1;
        // 理想的な壁からの距離を、推定セル数の平方根に係数を乗じて求めます。これは部屋の一辺のおおよその長さの半分程度を意図しています。
        float idealDistanceFromWall = Mathf.Sqrt(estimatedTargetCells) * settings.SeedPlacementWeightDistanceFactor;

        // グリッドの全セルを走査します。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // 0. 配置不可能な場所（_grid[x,y]が0）や、既に他のシードが置かれた場所（_grid[x,y]が正の値）は重み0とします。
                if (_grid[x, y] != -1) // -1は未割り当ての配置可能セルを示します。
                {
                    weights[x, y] = 0f;
                    continue; // 次のセルへ。
                }
                //ここから先は_grid[x, y] == -1（未割り当ての配置可能セル）のみ到達可能です。

                // 1. 初期重み設定 (配置可能な内部セルは基本重み1.0)
                float currentWeight = 1.0f;

                // 2. 外壁からの距離に基づいて重みを調整します。
                float dist = distanceToWall[x, y]; // 事前に計算された壁までの距離。
                if (dist >= 0) // 内部セルの場合（壁までの距離が計算できている）。
                {
                    // 壁に近いほど重みを減らすか、理想距離でピークになるように調整します。
                    // 例: 線形減衰 (壁際で0、idealDistanceFromWall以上で1になるようにクランプ)。
                    currentWeight += Mathf.Clamp01(dist / idealDistanceFromWall);
                    // 例: ガウシアン的な分布 (idealDistanceで最大値1となるように)。
                    // currentWeight *= Mathf.Exp(-(dist - idealDistanceFromWall) * (dist - idealDistanceFromWall));
                }
                else // 壁から到達不能、またはグリッド外扱いの場合。
                {
                    currentWeight = 0f; // 念のため（InitializeGridで0になっているはずですが、ここでも0にします）。
                }

                weights[x, y] = Mathf.Max(0, currentWeight);
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
        float maxWeight = -1f; // 最大の重みを記録する変数。初期値は-1f。
        List<Vector2Int> bestPositions = new List<Vector2Int>(); // 最大重みを持つ位置の候補リスト。

        // グリッド全体を走査して最適な位置を探します。
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // セルが配置可能（_grid[x,y]が-1）かつ、重みが計算済み（weightGrid[x,y]が0以上）の場合のみ対象とします。
                if (_grid[x, y] == -1 && weightGrid[x, y] >= 0)
                {
                    // 現在のセルの重みが、これまでの最大重みより大きい場合。
                    if (weightGrid[x, y] > maxWeight)
                    {
                        maxWeight = weightGrid[x, y]; // 最大重みを更新。
                        bestPositions.Clear(); // 候補リストをクリア。
                        bestPositions.Add(new Vector2Int(x, y)); // 現在の位置を唯一の候補として追加。
                    }
                    // 現在のセルの重みが、これまでの最大重みと同じ場合。
                    else if (weightGrid[x, y] == maxWeight)
                    {
                        bestPositions.Add(new Vector2Int(x, y)); // 現在の位置を候補に追加。
                    }
                }
            }
        }
        // 最適な位置の候補が見つかった場合。
        if (bestPositions.Count > 0)
        {
            // 最高重みの候補が複数あればランダムに1つを選択して返します。
            return bestPositions[_random.Next(bestPositions.Count)];
        }
        else
        {
            // 適切な位置が見つからなかった場合はnullを返します。
            return null;
        }
    }

    /// <summary>
    /// 配置されたシードの周囲のセルの重みを、他の部屋のグリッドで下げる関数。1-3
    /// </summary>
    /// <param name="weightGrids">全室の重みグリッドが格納された辞書。</param>
    /// <param name="seedPos">配置されたシードの位置。</param>
    /// <param name="placedRoomId">配置された部屋のID。</param>
    private void UpdateWeightsAroundSeed(Dictionary<int, float[,]> weightGrids, Vector2Int seedPos, int placedRoomId)
    {
        // --- 1. 引力プロセス (Attraction Process) ---
        // 先に、隣接が必要な部屋に引力を適用します。
        int inclusionRadius = settings.SeedInclusionRadius;
        float attractionWeight = settings.SeedPlacementAdjacencyBonus;

        // 隣接が必要な部屋の重みマップのみを更新
        foreach (var kvp in weightGrids)
        {
            int otherRoomId = kvp.Key;
            if (otherRoomId == placedRoomId) continue;

            // _ConnectivityGraph を使って隣接関係を確認
            if (_ConnectivityGraph.ContainsEdge(placedRoomId, otherRoomId))
            {
                // 引力半径の範囲を走査
                for (int dx = -inclusionRadius; dx <= inclusionRadius; dx++)
                {
                    for (int dy = -inclusionRadius; dy <= inclusionRadius; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                        if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                        {
                            // 有効なセル（配置可能）であれば、重みを加算
                            if (_grid[pos.x, pos.y] == -1)
                            {
                                kvp.Value[pos.x, pos.y] += attractionWeight;
                            }
                        }
                    }
                }
            }
        }

        // --- 2. 斥力プロセス (Repulsion Process) ---
        // 次に、全ての他部屋に対して、斥力を適用します。
        // これにより、引力範囲と斥力範囲が重なる場合、斥力が優先（上書き）されます。
        int exclusionRadius = settings.SeedExclusionRadius;
        for (int dx = -exclusionRadius; dx <= exclusionRadius; dx++)
        {
            for (int dy = -exclusionRadius; dy <= exclusionRadius; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                {
                    foreach (var kvp in weightGrids)
                    {
                        if (kvp.Key != placedRoomId)
                        {
                            kvp.Value[pos.x, pos.y] = float.MinValue;
                        }
                    }
                }
            }
        }
        Debug.Log($"Updated weights around {seedPos} for other rooms.");
    }

    /// <summary>
    /// 配置されたシードを、2×2の部屋に拡張する関数。1-4
    /// 拡張に成功した場合、_grid[,]の更新、room.Bounds,room.currentsize,room.InitialSeedPositionの更新を行う。
    /// </summary>
    private void ExpandTo22(float[,] distancetowall)
    {
        // _roomDefinitionsをIDの昇順に並べ替えて、各部屋を処理します。
        foreach (var room in _roomDefinitions.OrderBy(r => r.ID))
        {
            // InitialSeedPositionはVector2Int型で、部屋の中心ではなく、2x2の部屋の左下の開始点として扱います。
            int startX = room.InitialSeedPosition.Value.x;
            int startY = room.InitialSeedPosition.Value.y;

            int bestOffsetX = 0;
            int bestOffsetY = 0;
            float maxDistanceSum = -1f; // 壁までの距離の合計の最大値

            // 候補となる2x2の部屋の左下の開始点を4パターン試します
            // currentSeedPositionが2x2の部屋のどこかに属しているという前提で、その2x2の左下の座標を探索
            bool canExpandTo22 = false;
            for (int offsetX = -1; offsetX <= 0; offsetX++) // X方向のオフセット（-1または0）
            {
                for (int offsetY = -1; offsetY <= 0; offsetY++) // Y方向のオフセット（-1または0）
                {
                    int currentRoomStartX = startX + offsetX;
                    int currentRoomStartY = startY + offsetY;

                    // 2x2の部屋がグリッドの範囲内に収まるかを確認
                    if (currentRoomStartX >= 0 && currentRoomStartY >= 0 &&
                        currentRoomStartX + 1 < _gridSize.x && currentRoomStartY + 1 < _gridSize.y)
                    {
                        float currentDistanceSum = 0f;
                        bool canPlaceRoom = true;

                        // 2x2の各マスについて評価
                        for (int x = 0; x < 2; x++)
                        {
                            for (int y = 0; y < 2; y++)
                            {
                                int targetX = currentRoomStartX + x;
                                int targetY = currentRoomStartY + y;

                                // 部屋の配置が可能か（例: 既に他の部屋で占有されていないか、壁でないか
                                if (_grid[targetX, targetY] != -1 && _grid[targetX, targetY] != room.ID)
                                {
                                    canPlaceRoom = false; // グリッド範囲外
                                    break;
                                }
                                currentDistanceSum += distancetowall[targetX, targetY];
                            }
                            if (!canPlaceRoom) break;
                        }

                        // 配置可能で、かつこれまでの最大距離合計よりも大きい場合、最適な位置を更新
                        if (canPlaceRoom && currentDistanceSum > maxDistanceSum)
                        {
                            canExpandTo22 = true;//一度でも2*2に拡張できる組み合わせが見つかればtrueになる
                            maxDistanceSum = currentDistanceSum;
                            bestOffsetX = offsetX;
                            bestOffsetY = offsetY;
                        }
                    }
                }
            }
            //拡張できるなら、 最適な2x2の部屋の開始点を決定し、_grid[,]、room.Bounds,room.currentsize,room.InitialSeedPositionの更新
            if (canExpandTo22)
            {
                room.InitialSeedPosition = new Vector2Int(startX + bestOffsetX, startY + bestOffsetY);
                for (int x = 0;x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        int posx = room.InitialSeedPosition.Value.x + x;
                        int posy = room.InitialSeedPosition.Value.y + y;
                        _grid[posx, posy] = room.ID;
                    }
                }
                room.CurrentSize = 4;
                room.Bounds = new RectInt(room.InitialSeedPosition.Value.x, room.InitialSeedPosition.Value.y, 2, 2);
            }
            else Debug.Log($" failed to Expand room{room.ID} To22");
        }
    }
    /// <summary>
    /// PlaceInitialSeedsが失敗した場合などにグリッドとシード情報を初期状態に戻す,1-5
    /// </summary>
    private void ResetGridAndSeeds()
    {
        InitializeGrid(); // グリッドを-1（未割り当て配置可能）と0（配置不可）の状態に戻します。
        // 全ての部屋定義について、初期シード位置と現在のサイズをリセットします。
        foreach (var room in _roomDefinitions)
        {
            room.InitialSeedPosition = null; // 初期シード位置をnullに設定。
            room.CurrentSize = 0; // 現在のサイズを0に設定。
            // Bounds（境界情報）もリセットが必要な場合はここで行います。
        }
        Debug.Log("Grid and seeds reset."); // リセット完了のログを出力します。
    }
}