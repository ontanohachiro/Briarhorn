using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using System;

// 部屋の種類 (パブリック、プライベート、廊下など)
public enum RoomType
{
    Public,
    Private,
    Hallway,
    Staircase,
    // 必要に応じて追加
    Outside // 建物外を示すために追加
}

// ゾーンまたは部屋を表すクラス
public class RoomDefinition
{
    public RoomType Type;
    public float SizeRatio; // 要求される相対的なサイズ比率
    public List<int> ConnectivityConstraints; // 直接接続すべき部屋のIDリスト

    // 実行時に計算される情報
    public Vector2Int? InitialSeedPosition; // 拡張の開始位置
    public RectInt Bounds; // エリアの境界ボックス (拡張後に計算)
    public int CurrentSize = 0; // 拡張中の現在のセル数

    public RoomDefinition(RoomType type, float ratio)//コンストラクタ.ConnectivityConstraintsは具体的に設定しない.
    {
        Type = type;
        SizeRatio = ratio;
        ConnectivityConstraints = new List<int>();
    }
}

// フロアプラン生成の入力設定
[System.Serializable]
public class FloorPlanSettings//入力.
{
    /// <summary>
    /// 値の例: 0 = 建物外/穴/使用不可, 1 = 部屋を配置可能なエリア
    /// </summary>
    public int[,] InputFootprintGrid;

    /// <summary>
    /// 部屋定義のリスト (階層構造なし)
    /// </summary>
    public List<RoomDefinition> RoomDefinitionsList;

    /// <summary>
    /// 直接接続すべき部屋についての、QuikGraphを用いた接続グラフ
    /// </summary>
    public AdjacencyGraph<int, Edge<int>> ConnectivityGraph;

    public int MaxGenerationAttempts = 10; // 隣接制約を満たすための最大試行回数
    public float SeedPlacementWeightDistanceFactor = 0.5f; // 外壁からの距離に基づく重み付け係数 
    public float SeedPlacementAdjacencyBonus = 1.0f; // 隣接制約のある部屋の近くの重みボーナス
    public int SeedExclusionRadius = 1; // 配置されたシードの周囲で他のシードを配置しない半径

}

// 生成されたフロアプランの出力
public class GeneratedFloorPlan
{
    public int[,] Grid; // 各セルがどの部屋IDに属するかを示すグリッド (0:壁/外, -1:配置可能だが未割当 >0:部屋ID)
    public Dictionary<int, RoomDefinition> Rooms; // IDと部屋定義のマッピング
    public List<Door> Doors; // 配置されたドアのリスト
}

// ドアを表すクラス
public class Door
{
    // ドアが接続する2つのセルの座標.これらのセルは、グリッドの境界を1だけ超えることがある。
    public Vector2Int Cell1;
    public Vector2Int Cell2;
}

// --- フロアプラン生成クラス ---

public class FloorPlanGenerator : MonoBehaviour
{
    public FloorPlanSettings settings;//serializable,今のところはunityのインスペクターで設定.
    private int[,] _grid; // 0: 建物外/壁/穴, -1: 配置可能だが未割り当て, >0: 部屋ID .出力に使用.
    private Dictionary<int, RoomDefinition> _roomDefinitions; // IDと部屋のマッピング.出力に使用.
    private int _nextRoomID = 1;
    private System.Random _random = new System.Random();
    private Vector2Int _gridSize;
    private int _totalPlaceableCells = 0; // 配置可能なセルの総数

    /// <summary>
    /// フロアプラン生成のメイン関数
    /// </summary>
    public GeneratedFloorPlan Generate()
    {
        if (settings == null || settings.InputFootprintGrid == null || settings.RoomDefinitionsList == null)
        {
            Debug.LogError("FloorPlanSettings or required inputs are not properly set.");
            return null;
        }

        List<RoomDefinition> roomsToPlace = settings.RoomDefinitionsList;

        GeneratedFloorPlan bestPlan = null;
        float bestScore = float.MinValue; // スコアリング実装時に使用

        for (int attempt = 0; attempt < settings.MaxGenerationAttempts; attempt++)
        {
            Debug.Log($"--- Generation Attempt {attempt + 1} ---");
            GeneratedFloorPlan currentPlan = AttemptGeneration(roomsToPlace);

            if (currentPlan != null)
            {
                // TODO: EvaluatePlan を実装し、bestPlan を更新する
                // float currentScore = EvaluatePlan(currentPlan);
                // if (currentScore > bestScore)
                // {
                //     bestScore = currentScore;
                //     bestPlan = currentPlan;
                // }
                // 現状は最初の成功したプランを返す
                bestPlan = currentPlan;
                Debug.Log($"Generation successful on attempt {attempt + 1}");
                break; // 成功したらループを抜ける
            }
            else
            {
                Debug.LogWarning($"Generation attempt {attempt + 1} failed.");
            }
        }

        if (bestPlan == null)
        {
            Debug.LogError($"Failed to generate a valid floor plan after {settings.MaxGenerationAttempts} attempts.");
        }

        return bestPlan;
    }


    /// <summary>
    /// 1回のフロアプラン生成試行をする.
    /// </summary>
    private GeneratedFloorPlan AttemptGeneration(List<RoomDefinition> roomsToPlace)
    {
        InitializeGrid();
        if (_totalPlaceableCells == 0)
        {
            Debug.LogError("No placeable cells found in the InputFootprintGrid.");
            return null;
        }
        AssignRoomIDs(roomsToPlace);

        // 1. 部屋の初期位置決定 (Room Placement)
        if (!PlaceInitialSeeds(roomsToPlace))
        {
            Debug.LogError("Failed during PlaceInitialSeeds.");
            return null;
        }

        // 2. 部屋の拡張 (Room Expansion)
        if (!ExpandRooms(roomsToPlace))
        {
            Debug.LogError("Failed during ExpandRooms.");
            return null;
        }

        // 隣接制約の検証
        if (!VerifyAdjacencyConstraints(roomsToPlace))
        {
            Debug.LogWarning("Adjacency constraints not fully met in this attempt.");
            // 失敗とするか、スコアを下げるかなどの判断
            // return null; // ここで失敗とすることも可能
        }

        // 3. 部屋の接続性 (Room Connectivity)
        List<Door> doors = DetermineConnectivity(roomsToPlace, settings.ConnectivityGraph);
        if (doors == null) // DetermineConnectivity内でエラーログが出るはず
        {
            Debug.LogError("Failed during DetermineConnectivity (door list is null).");
            return null;
        }
        if (!VerifyReachability(roomsToPlace, doors))
        {
            Debug.LogWarning("Reachability constraints not met. Attempting to fix...");
            // TODO: 到達可能性を修正するロジック (DetermineConnectivity内で部分的に対応済み)
            // ここで再度 DetermineConnectivity を呼ぶか、専用の修正関数を呼ぶ
            // return null; // 修正不可なら失敗とする
        }


        // 成功した場合、結果を構築
        GeneratedFloorPlan plan = new GeneratedFloorPlan
        {
            Grid = (int[,])_grid.Clone(),
            Rooms = new Dictionary<int, RoomDefinition>(_roomDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)), // RoomDefinitionもディープコピーが必要なら別途対応
            Doors = doors,
        };

        // Boundsを計算して格納 (オプション)
        CalculateRoomBounds(plan);

        return plan;
    }

    /// <summary>
    /// グリッドの初期化,割り当て. 配置可能セル数を計算._gridSize設定.
    /// </summary>
    public void InitializeGrid()
    {
        if (settings.InputFootprintGrid == null)
        {
            Debug.LogError("InputFootprintGrid is null!");
            return;
        }
        _gridSize = new Vector2Int(settings.InputFootprintGrid.GetLength(0), settings.InputFootprintGrid.GetLength(1));
        _grid = new int[_gridSize.x, _gridSize.y];
        _totalPlaceableCells = 0;

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (settings.InputFootprintGrid[x, y] == 1) // 配置可能エリア
                {
                    _grid[x, y] = -1; // 未割り当てを示す値 (-1)
                    _totalPlaceableCells++;
                }
                else // 配置不可能エリア
                {
                    _grid[x, y] = 0; // 0を建物外/壁/穴として扱う
                }
            }
        }
        Debug.Log($"Grid initialized. Size: {_gridSize}, Placeable cells: {_totalPlaceableCells}");
    }

    /// <summary>
    /// 各部屋定義にユニークな整数IDを割り当て. _roomDefinitionsの完全な作成.
    /// </summary>
    private void AssignRoomIDs(List<RoomDefinition> rooms)
    {
        _roomDefinitions = new Dictionary<int, RoomDefinition>();
        _nextRoomID = 1;
        foreach (var roomDef in rooms)
        {
            // RoomDefinitionのインスタンスをコピーして辞書に追加
            // (元のリストのインスタンスを直接使うと、試行間で状態が引き継がれてしまうため)
            RoomDefinition newRoomInstance = new RoomDefinition(roomDef.Type, roomDef.SizeRatio)
            {
                ConnectivityConstraints = new List<int>(roomDef.ConnectivityConstraints),
                InitialSeedPosition = null, // リセット
                CurrentSize = 0 //リセット
                                // Boundsは後で計算
            };
            _roomDefinitions.Add(_nextRoomID, newRoomInstance);
            _nextRoomID++;
        }
        Debug.Log($"{_roomDefinitions.Count} rooms assigned IDs.");
    }

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


    // --- ステップ2: ExpandRooms とその補助関数 ---

    private bool ExpandRooms(List<RoomDefinition> roomsToPlace)
    {
        Debug.Log("Expanding rooms...");

        // --- フェーズ1: 矩形拡張 (Rectangular Growth) [cite: 119, 128] ---
        HashSet<int> availableRoomsPhase1 = new HashSet<int>(_roomDefinitions.Keys);
        int totalPlaceable = _totalPlaceableCells; // 全体の配置可能セル数
        int cellsAssigned = _roomDefinitions.Count; // 初期シード分
        int maxIterationsPhase1 = totalPlaceable * 2; // 無限ループ防止用
        int iterationPhase1 = 0;


        while (availableRoomsPhase1.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase1 < maxIterationsPhase1)
        {
            iterationPhase1++;
            // 面積比率に基づいて次に拡張する部屋を選択
            int roomId = SelectRoomToExpand(availableRoomsPhase1, true);
            if (roomId == -1) break; // 選択できる部屋がない

            // 矩形拡張を試みる
            bool grew = GrowRect(roomId);

            if (!grew)
            {
                availableRoomsPhase1.Remove(roomId); // これ以上矩形拡張できない
            }
            else
            {
                // 拡張に成功した場合、再度拡張の候補に入れることで、連続的な拡張を促す
                // (ただし、無限ループにならないように注意が必要。SelectRoomToExpandの確率的選択である程度緩和される)
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数更新
                                                                             // Debug.Log($"Phase 1, Iter {iterationPhase1}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}");

        }
        if (iterationPhase1 >= maxIterationsPhase1) Debug.LogWarning("Phase 1 reached max iterations.");


        // --- フェーズ2: L字型拡張 (L-Shape Growth) ---
        HashSet<int> availableRoomsPhase2 = new HashSet<int>(_roomDefinitions.Keys);
        int maxIterationsPhase2 = (totalPlaceable - cellsAssigned) * _roomDefinitions.Count; // 隙間埋め的な処理なので多めに見積もる
        int iterationPhase2 = 0;

        while (availableRoomsPhase2.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase2 < maxIterationsPhase2)
        {
            iterationPhase2++;
            // 面積比率はもうあまり考慮せず、拡張可能な部屋を優先？ [cite: 138] では最大サイズ考慮せず
            int roomId = SelectRoomToExpand(availableRoomsPhase2, false); // 比率考慮なしで選択
            if (roomId == -1) break;

            // L字型拡張を試みる [cite: 120, 137, 139]
            bool grew = GrowLShape(roomId);

            if (!grew)
            {
                availableRoomsPhase2.Remove(roomId); // これ以上L字拡張できない
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数更新
                                                                             // Debug.Log($"Phase 2, Iter {iterationPhase2}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}");
        }
        if (iterationPhase2 >= maxIterationsPhase2) Debug.LogWarning("Phase 2 reached max iterations.");


        // --- フェーズ3: 隙間埋め (Fill Gaps) [cite: 121, 140] ---
        FillGaps();
        cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // 割り当て済みセル数更新


        // 最終チェック: 未割り当てセルがないか？
        bool allCellsFilled = AreAllCellsFilled();
        Debug.Log($"Room expansion finished. Assigned cells: {cellsAssigned}/{totalPlaceable}. All filled: {allCellsFilled}");

        // 全てのセルが埋まらなかった場合、失敗とみなすか、許容するかは要件次第
        // if (!allCellsFilled) return false;

        return true;
    }


    /// <summary>
    /// 拡張フェーズで次に拡張する部屋を選択する
    /// </summary>
    /// <param name="availableRoomIds">拡張可能な部屋IDのセット</param>
    /// <param name="useSizeRatio">面積比率を考慮するかどうか</param>
    /// <returns>選択された部屋ID、候補がなければ-1</returns>
    private int SelectRoomToExpand(HashSet<int> availableRoomIds, bool useSizeRatio)
    {
        if (availableRoomIds.Count == 0) return -1;

        // 現在のサイズと目標サイズの差に基づいて選択するアプローチも考えられる
        // float GetPriority(int id) {
        //     RoomDefinition room = _roomDefinitions[id];
        //     float targetRatio = room.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio);
        //     float currentRatio = (float)room.CurrentSize / _totalPlaceableCells;
        //     return Mathf.Max(0, targetRatio - currentRatio); // 目標に達していないほど優先度高
        // }
        // var candidates = availableRoomIds.Select(id => new { id, priority = GetPriority(id) }).Where(x => x.priority > 0).ToList();
        // if (candidates.Count == 0) return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count)); // 優先度0ならランダム
        // float totalPriority = candidates.Sum(x => x.priority);
        // double randomValue = _random.NextDouble() * totalPriority;
        // float cumulative = 0;
        // foreach (var candidate in candidates) {
        //      cumulative += candidate.priority;
        //      if (randomValue < cumulative) return candidate.id;
        // }
        // return candidates.Last().id; // フォールバック


        // 論文 に近い実装 (面積比率に基づく確率的選択)
        if (useSizeRatio)
        {
            var candidates = availableRoomIds.ToList();
            float totalRatio = candidates.Sum(id => _roomDefinitions[id].SizeRatio);

            if (totalRatio <= 0)
            {
                // 比率がない場合はランダムに選択
                return candidates[_random.Next(candidates.Count)];
            }

            double randomValue = _random.NextDouble() * totalRatio;
            float cumulativeRatio = 0;

            foreach (int roomId in candidates)
            {
                cumulativeRatio += _roomDefinitions[roomId].SizeRatio;
                if (randomValue < cumulativeRatio)
                {
                    return roomId;
                }
            }
            return candidates.Last(); // フォールバック
        }
        else
        {
            // 比率を考慮しない場合はランダムに選択
            return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count));
        }
    }


    private bool GrowRect(int roomId)
    {
        // TODO: 実装
        // 1. roomIdに属するセルに隣接する空きセル(-1)を探す
        // 2. 空きセルを水平・垂直方向の「ライン」としてグループ化
        // 3. 各ラインについて、拡張した場合に部屋が矩形を維持できるかチェック
        // 4. 矩形を維持できるラインの中で最も長いものを選択 (複数あればランダム)
        // 5. 選択したラインのセルをroomIdに割り当て、_gridとroom.CurrentSizeを更新
        // 6. 目標サイズ(推定)に達したら拡張を止めるオプション [cite: 133] (フェーズ1のみ)
        // 7. 拡張できたらtrue、できなければfalse
        return false; // ダミー
    }

    private bool GrowLShape(int roomId)
    {
        // TODO: 実装
        // 1. roomIdに属するセルに隣接する空きセル(-1)を探す
        // 2. 空きセルを水平・垂直方向の「ライン」としてグループ化
        // 3. 各ラインについて、拡張した場合にL字型になるか、または既存のL字型を維持するかチェック
        // 4. U字型にならないようにチェック [cite: 139]
        // 5. 拡張可能なラインの中で最も長いものを選択 (複数あればランダム) [cite: 137]
        // 6. 選択したラインのセルをroomIdに割り当て、_gridとroom.CurrentSizeを更新
        // 7. 拡張できたらtrue、できなければfalse
        return false; // ダミー
    }

    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps...");
        List<Vector2Int> gaps = FindEmptyCells();
        int filledCount = 0;

        // 各未割り当てセルについて処理 [cite: 140]
        foreach (Vector2Int gapPos in gaps)
        {
            if (_grid[gapPos.x, gapPos.y] != -1) continue; // すでに埋まっている場合

            Dictionary<int, int> neighborCounts = new Dictionary<int, int>();
            int maxCount = 0;
            int bestNeighborId = -1;

            Vector2Int[] neighbors = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            // 隣接する部屋IDをカウント
            foreach (var offset in neighbors)
            {
                Vector2Int nPos = gapPos + offset;
                if (nPos.x >= 0 && nPos.x < _gridSize.x && nPos.y >= 0 && nPos.y < _gridSize.y)
                {
                    int neighborId = _grid[nPos.x, nPos.y];
                    if (neighborId > 0) // 有効な部屋ID
                    {
                        if (!neighborCounts.ContainsKey(neighborId)) neighborCounts[neighborId] = 0;
                        neighborCounts[neighborId]++;

                        if (neighborCounts[neighborId] > maxCount)
                        {
                            maxCount = neighborCounts[neighborId];
                            bestNeighborId = neighborId;
                        }
                        // 同数の場合は、より面積の小さい部屋を優先するなどのルールも考えられる
                        else if (neighborCounts[neighborId] == maxCount && bestNeighborId != -1)
                        {
                            // 例: 面積比率が小さい方を優先 (隙間は小さい部屋が吸収しやすい?)
                            if (_roomDefinitions[neighborId].SizeRatio < _roomDefinitions[bestNeighborId].SizeRatio)
                            {
                                bestNeighborId = neighborId;
                            }
                        }
                    }
                }
            }

            // 最も多く隣接している部屋に割り当て
            if (bestNeighborId != -1)
            {
                _grid[gapPos.x, gapPos.y] = bestNeighborId;
                if (_roomDefinitions.ContainsKey(bestNeighborId))
                {
                    _roomDefinitions[bestNeighborId].CurrentSize++;
                }
                filledCount++;
            }
            else
            {
                Debug.LogWarning($"Could not fill gap at {gapPos} - no valid neighbors found.");
            }
        }
        Debug.Log($"Filled {filledCount} gap cells.");
    }


    /// <summary>
    /// グリッド内に未割り当てセル(-1)が残っているか確認
    /// </summary>
    private bool AreAllCellsFilled()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1)
                {
                    return false; // 未割り当てセル発見
                }
            }
        }
        return true; // 全て割り当て済み
    }

    /// <summary>
    /// グリッド内の未割り当てセル(-1)のリストを取得
    /// </summary>
    private List<Vector2Int> FindEmptyCells()
    {
        List<Vector2Int> emptyCells = new List<Vector2Int>();
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1)
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
        }
        return emptyCells;
    }


    // --- ステップ3: DetermineConnectivity とその補助関数 ---

    private List<Door> DetermineConnectivity(List<RoomDefinition> roomsToPlace, AdjacencyGraph<int, Edge<int>> connectivityGraph)
    {
        Debug.Log("Determining connectivity...");
        List<Door> doors = new List<Door>();
        HashSet<Tuple<int, int>> connectionsMade = new HashSet<Tuple<int, int>>(); // (id1, id2) where id1 < id2

        // --- 1. 入力グラフに基づく必須接続 ---
        if (connectivityGraph != null && connectivityGraph.Edges != null)
        {
            foreach (var edge in connectivityGraph.Edges)
            {
                int roomID1 = edge.Source;
                int roomID2 = edge.Target;

                if (roomID1 == roomID2 || !_roomDefinitions.ContainsKey(roomID1) || !_roomDefinitions.ContainsKey(roomID2))
                {
                    // Debug.LogWarning($"Skipping invalid or self-connecting edge from graph: {roomID1} -> {roomID2}");
                    continue;
                }

                var pair = roomID1 < roomID2 ? Tuple.Create(roomID1, roomID2) : Tuple.Create(roomID2, roomID1);
                if (connectionsMade.Contains(pair)) continue;

                if (PlaceDoorBetweenRooms(roomID1, roomID2, doors))
                {
                    connectionsMade.Add(pair);
                    Debug.Log($"Placed required door between {roomID1} and {roomID2}");
                }
                else
                {
                    // 必須接続が隣接していないため配置できなかった
                    Debug.LogError($"REQUIRED connection between {roomID1} and {roomID2} FAILED. Rooms are not adjacent.");
                    // ここで生成失敗とするべきか？ 要件による
                    return null; // 必須接続が満たせないなら失敗
                }
            }
        }

        // --- 2. 論文アルゴリズムに基づく接続ルール ---

        // 2a. 廊下(Hallway)と隣接する全てのパブリック(Public)部屋を接続 [cite: 163]
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway))
        {
            int hallwayId = kvp1.Key;
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public))
            {
                int publicId = kvp2.Key;
                var pair = hallwayId < publicId ? Tuple.Create(hallwayId, publicId) : Tuple.Create(publicId, hallwayId);
                if (connectionsMade.Contains(pair)) continue;

                if (AreRoomsAdjacent(hallwayId, publicId))
                {
                    if (PlaceDoorBetweenRooms(hallwayId, publicId, doors))
                    {
                        connectionsMade.Add(pair);
                        Debug.Log($"Placed Hallway-Public door between {hallwayId} and {publicId}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to place optional door between Hallway {hallwayId} and Public {publicId} despite adjacency.");
                    }
                }
            }
        }

        // 2b. 未接続のプライベート(Private)部屋を、隣接するパブリック部屋に接続 (可能なら) [cite: 164]
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Private))
        {
            int privateId = kvp1.Key;
            if (IsRoomConnected(privateId, connectionsMade)) continue; // 既にどこかに繋がっている

            // 隣接するパブリック部屋を探す
            int connectedTo = -1;
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public))
            {
                int publicId = kvp2.Key;
                if (AreRoomsAdjacent(privateId, publicId))
                {
                    var pair = privateId < publicId ? Tuple.Create(privateId, publicId) : Tuple.Create(publicId, privateId);
                    if (PlaceDoorBetweenRooms(privateId, publicId, doors))
                    {
                        connectionsMade.Add(pair);
                        connectedTo = publicId;
                        Debug.Log($"Connected Private {privateId} to Public {publicId}");
                        break; // 1つ接続できればOK
                    }
                }
            }
            // もしパブリックに接続できなかったら、廊下に接続できないか試す (論文にはないが、実用的かも)
            if (connectedTo == -1)
            {
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway))
                {
                    int hallwayId = kvp2.Key;
                    if (AreRoomsAdjacent(privateId, hallwayId))
                    {
                        var pair = privateId < hallwayId ? Tuple.Create(privateId, hallwayId) : Tuple.Create(hallwayId, privateId);
                        if (PlaceDoorBetweenRooms(privateId, hallwayId, doors))
                        {
                            connectionsMade.Add(pair);
                            connectedTo = hallwayId;
                            Debug.Log($"Connected Private {privateId} to Hallway {hallwayId} (fallback)");
                            break;
                        }
                    }
                }
            }

            if (connectedTo == -1)
            {
                Debug.LogWarning($"Private room {privateId} remains unconnected after trying Public/Hallway.");
            }
        }

        // 2c. 未接続のパブリック部屋を、隣接する他のパブリック部屋に接続 (可能なら) [cite: 165]
        // (ただし、通常は廊下との接続でカバーされることが多い)
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public))
        {
            int publicId1 = kvp1.Key;
            if (IsRoomConnected(publicId1, connectionsMade)) continue;

            int connectedTo = -1;
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public && r.Key != publicId1))
            {
                int publicId2 = kvp2.Key;
                if (AreRoomsAdjacent(publicId1, publicId2))
                {
                    var pair = publicId1 < publicId2 ? Tuple.Create(publicId1, publicId2) : Tuple.Create(publicId2, publicId1);
                    if (!connectionsMade.Contains(pair)) // まだ接続されていなければ
                    {
                        if (PlaceDoorBetweenRooms(publicId1, publicId2, doors))
                        {
                            connectionsMade.Add(pair);
                            connectedTo = publicId2;
                            Debug.Log($"Connected Public {publicId1} to Public {publicId2}");
                            break;
                        }
                    }
                    else
                    {
                        // 既に接続があるなら、この部屋も接続済みとみなせる
                        connectedTo = publicId2;
                        break;
                    }
                }
            }
            if (connectedTo == -1)
            {
                // 他のPublicにも繋がらない場合、廊下に接続を試みる（通常はこちらが優先されるはず）
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway))
                {
                    int hallwayId = kvp2.Key;
                    if (AreRoomsAdjacent(publicId1, hallwayId))
                    {
                        var pair = publicId1 < hallwayId ? Tuple.Create(publicId1, hallwayId) : Tuple.Create(hallwayId, publicId1);
                        if (!connectionsMade.Contains(pair))
                        {
                            if (PlaceDoorBetweenRooms(publicId1, hallwayId, doors))
                            {
                                connectionsMade.Add(pair);
                                connectedTo = hallwayId;
                                Debug.Log($"Connected Public {publicId1} to Hallway {hallwayId} (fallback)");
                                break;
                            }
                        }
                        else
                        {
                            connectedTo = hallwayId;
                            break;
                        }
                    }
                }
            }


            if (connectedTo == -1)
            {
                Debug.LogWarning($"Public room {publicId1} remains unconnected after trying Public/Hallway.");
            }
        }


        // --- 3. 到達可能性の最終チェックと修正 [cite: 166] ---
        // (VerifyReachability内で行い、必要ならここで修正ドアを追加するロジックを呼ぶ)
        if (!VerifyReachability(roomsToPlace, doors))
        {
            Debug.LogWarning("Reachability check failed AFTER initial door placement. Attempting to force connections...");
            // TODO: 強制的に接続を追加するロジック
            // 例: 到達不能な部屋からBFSで到達可能な部屋を探し、その間の最短経路上の隣接ペアにドアを設置
            // この修正ロジックは複雑になる可能性がある
            // ForceReachability(roomsToPlace, doors, connectionsMade);
        }


        Debug.Log($"Connectivity determined. Placed {doors.Count} doors.");
        return doors;
    }

    /// <summary>
    /// 指定された部屋が既に何らかの接続を持っているか確認
    /// </summary>
    private bool IsRoomConnected(int roomId, HashSet<Tuple<int, int>> connectionsMade)
    {
        foreach (var pair in connectionsMade)
        {
            if (pair.Item1 == roomId || pair.Item2 == roomId)
            {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// 指定された2つの部屋の間にドアを配置しようと試みる。
    /// 成功したらdoorsリストに追加し、trueを返す。
    /// </summary>
    private bool PlaceDoorBetweenRooms(int roomID1, int roomID2, List<Door> doors)
    {
        Vector2Int? cell1, cell2;
        if (FindAdjacentWallSegment(roomID1, roomID2, out cell1, out cell2))
        {
            if (cell1.HasValue && cell2.HasValue)
            {
                Door newDoor = new Door
                {
                    Cell1 = cell1.Value,
                    Cell2 = cell2.Value,
                };
                doors.Add(newDoor);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 指定された2つの部屋IDがグリッド上で隣接しているかどうかを判定する
    /// </summary>
    private bool AreRoomsAdjacent(int roomID1, int roomID2)
    {
        Vector2Int? c1, c2;
        // FindAdjacentWallSegmentは隣接箇所を見つける関数なので、
        // これがtrueを返せば隣接していると判断できる。
        return FindAdjacentWallSegment(roomID1, roomID2, out c1, out c2);
    }


    // --- 補助関数 ---

    /// <summary>
    /// 指定された2つの部屋IDが隣接する壁セグメントを探し、
    /// ドアを設置するのに適した隣接セルペア(cell1, cell2)を返す。
    /// 複数の候補がある場合はランダムに選択する。
    /// </summary>
    private bool FindAdjacentWallSegment(int roomID1, int roomID2, out Vector2Int? cell1, out Vector2Int? cell2)
    {
        cell1 = null;
        cell2 = null;
        List<Tuple<Vector2Int, Vector2Int>> potentialDoorLocations = new List<Tuple<Vector2Int, Vector2Int>>();

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == roomID1)
                {
                    Vector2Int currentCell = new Vector2Int(x, y);
                    Vector2Int[] neighbors = {
                        new Vector2Int(x + 1, y), new Vector2Int(x - 1, y),
                        new Vector2Int(x, y + 1), new Vector2Int(x, y - 1)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                        {
                            if (_grid[neighbor.x, neighbor.y] == roomID2)
                            {
                                potentialDoorLocations.Add(Tuple.Create(currentCell, neighbor));
                            }
                        }
                    }
                }
            }
        }

        if (potentialDoorLocations.Count > 0)
        {
            // 複数の候補からランダムに選択 [cite: 172]
            var chosenLocation = potentialDoorLocations[_random.Next(potentialDoorLocations.Count)];
            cell1 = chosenLocation.Item1;
            cell2 = chosenLocation.Item2;
            return true;
        }

        return false; // 隣接箇所が見つからなかった
    }


    private bool VerifyAdjacencyConstraints(List<RoomDefinition> rooms)
    {
        // TODO: 実装 (PlaceInitialSeeds と DetermineConnectivity で隣接は考慮済みだが、最終確認として)
        // 各部屋について、ConnectivityConstraints に含まれる部屋が実際に隣接しているかチェック
        bool allConstraintsMet = true;
        foreach (var kvp in _roomDefinitions)
        {
            int roomID1 = kvp.Key;
            RoomDefinition room1 = kvp.Value;
            if (room1.ConnectivityConstraints == null) continue;

            foreach (int requiredNeighborId in room1.ConnectivityConstraints)
            {
                if (!_roomDefinitions.ContainsKey(requiredNeighborId)) continue; // 相手が存在しない

                if (!AreRoomsAdjacent(roomID1, requiredNeighborId))
                {
                    Debug.LogWarning($"Adjacency constraint NOT MET: Room {roomID1} ({room1.Type}) and Room {requiredNeighborId} ({_roomDefinitions[requiredNeighborId].Type}) are not adjacent.");
                    allConstraintsMet = false;
                    // ここで false を返して試行失敗にするか、警告に留めるかは選択
                }
            }
        }
        if (allConstraintsMet)
        {
            // Debug.Log("All adjacency constraints are met.");
        }
        return allConstraintsMet;
    }

    private bool VerifyReachability(List<RoomDefinition> rooms, List<Door> doors)
    {
        if (_roomDefinitions == null || _roomDefinitions.Count <= 1 || doors == null)
        {
            // 部屋が1つ以下、またはドアがない場合は到達可能 (またはチェック不要)
            return true;
        }

        // ドア情報から隣接グラフを構築
        var reachabilityGraph = new AdjacencyGraph<int, Edge<int>>();
        foreach (int roomId in _roomDefinitions.Keys)
        {
            reachabilityGraph.AddVertex(roomId); // 全ての部屋を頂点として追加
        }

        HashSet<Tuple<int, int>> addedEdges = new HashSet<Tuple<int, int>>();
        foreach (var door in doors)
        {
            int id1 = _grid[door.Cell1.x, door.Cell1.y];
            int id2 = _grid[door.Cell2.x, door.Cell2.y];

            if (id1 > 0 && id2 > 0 && id1 != id2)
            {
                var pair = id1 < id2 ? Tuple.Create(id1, id2) : Tuple.Create(id2, id1);
                if (!addedEdges.Contains(pair)) // 同じドアによる重複エッジを防ぐ
                {
                    reachabilityGraph.AddEdge(new Edge<int>(id1, id2));
                    reachabilityGraph.AddEdge(new Edge<int>(id2, id1)); // 無向グラフとして扱うため逆方向も追加
                    addedEdges.Add(pair);
                }
            }
        }

        // 連結成分をチェック (BFS/DFS)
        if (reachabilityGraph.VertexCount == 0) return true; // 頂点がないならOK

        HashSet<int> visited = new HashSet<int>();
        Queue<int> queue = new Queue<int>();

        // 開始点を選択 (例: 最初の部屋ID、または特定の部屋タイプがあればそれ)
        int startNode = reachabilityGraph.Vertices.First();
        // または、Entrance や Hallway があればそこから開始
        var startCandidates = _roomDefinitions.Where(kvp => kvp.Value.Type == RoomType.Hallway || kvp.Value.Type == RoomType.Public).Select(kvp => kvp.Key);
        if (startCandidates.Any())
        {
            startNode = startCandidates.First();
        }


        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (reachabilityGraph.TryGetOutEdges(current, out var outEdges))
            {
                foreach (var edge in outEdges)
                {
                    int neighbor = edge.Target;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // 全ての部屋IDが訪問済みかチェック
        bool allReachable = visited.Count == reachabilityGraph.VertexCount;

        if (!allReachable)
        {
            var unreachable = reachabilityGraph.Vertices.Except(visited).ToList();
            Debug.LogWarning($"Reachability check failed. Unreachable rooms: {string.Join(", ", unreachable)} (started from {startNode})");
        }
        else
        {
            // Debug.Log("Reachability check passed.");
        }

        return allReachable;
    }


    /// <summary>
    /// 生成されたプランの評価 (スコアリング) - ダミー実装
    /// </summary>
    private float EvaluatePlan(GeneratedFloorPlan plan)
    {
        // TODO: 実装
        // - 制約充足度 (隣接、到達可能性)
        // - 面積比率の達成度
        // - 部屋の形状 (矩形度、凹凸の少なさ)
        // - 廊下の効率性 (面積、長さ)
        // - ドアの数、位置
        float score = 1.0f;

        // 例: 隣接制約が満たされていない場合はスコアを下げる
        // if (!VerifyAdjacencyConstraints(plan.Rooms.Values.ToList())) score *= 0.5f;

        // 例: 到達可能性が満たされていない場合はスコアを大幅に下げる
        // if (!VerifyReachability(plan.Rooms.Values.ToList(), plan.Doors)) score = 0f; // 到達不能は致命的

        // 例: 面積比率の誤差が大きいほどスコアを下げる
        // float totalArea = plan.Rooms.Values.Sum(r => GetRoomArea(plan.Grid, r.Key)); // GetRoomAreaは要実装
        // float ratioError = 0f;
        // foreach(var kvp in plan.Rooms) {
        //      float targetRatio = kvp.Value.SizeRatio / plan.Rooms.Values.Sum(r => r.SizeRatio);
        //      float actualRatio = GetRoomArea(plan.Grid, kvp.Key) / totalArea;
        //      ratioError += Mathf.Abs(targetRatio - actualRatio);
        // }
        // score *= Mathf.Clamp01(1.0f - ratioError); // 誤差が大きいほど減点

        return score;
    }


    /// <summary>
    /// 各部屋のバウンディングボックスを計算してRoomDefinitionに格納
    /// </summary>
    private void CalculateRoomBounds(GeneratedFloorPlan plan)
    {
        foreach (var kvp in plan.Rooms)
        {
            int roomId = kvp.Key;
            RoomDefinition room = kvp.Value;
            int minX = _gridSize.x, minY = _gridSize.y, maxX = -1, maxY = -1;
            bool roomFound = false;

            for (int x = 0; x < _gridSize.x; x++)
            {
                for (int y = 0; y < _gridSize.y; y++)
                {
                    if (plan.Grid[x, y] == roomId)
                    {
                        roomFound = true;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (roomFound)
            {
                room.Bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            else
            {
                // 部屋がグリッド上に存在しない？ エラーまたはデフォルト値を設定
                room.Bounds = new RectInt(0, 0, 0, 0);
                Debug.LogWarning($"Room {roomId} not found in the final grid when calculating bounds.");
            }
        }
    }



    // --- Unity Editor での実行例 ---
    [ContextMenu("Generate Floor Plan")]
    void GenerateFromEditor()
    {
        if (settings == null)
        {
            Debug.LogError("FloorPlanSettings is not assigned in the Inspector.");
            return;
        }

        // --- 生成実行 ---
        GeneratedFloorPlan plan = Generate();

    }

}