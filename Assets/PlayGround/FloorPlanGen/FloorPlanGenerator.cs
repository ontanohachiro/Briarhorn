using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using System;
using static UnityEngine.Rendering.DebugUI;
using UnityEditor.XR;

// 部屋の種類 (パブリック、プライベート、廊下など)
public enum RoomType
{
    LivingRoom,
    Kitchen,
    Bedroom,
    Bathroom,
    Hallway,
    Entrance,
    Private,Public
}
// ゾーンまたは部屋を表すクラス
public class RoomDefinition
{
    //初期化の際に代入される情報.プログラム内で変化しない.
    public int ID;//1以上の整数.
    public RoomType Type;
    public float SizeRatio; // 要求される相対的なサイズ比率

    // 実行時に計算される情報
    public Vector2Int? InitialSeedPosition; // 拡張の開始位置
    public RectInt Bounds; // エリアの境界ボックス (拡張後に計算)
    public int CurrentSize = 0; // 拡張中の現在のセル数

    public RoomDefinition(int id,RoomType type, float ratio)//コンストラクタ
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "値は1以上である必要があります。");
        }
        ID = id;
        Type = type;
        SizeRatio = ratio;
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
    /// 部屋定義のリスト (階層構造なし).ID順に並んでいることを前提とする。ただし、IDは1から始まる。
    /// </summary>
    public List<RoomDefinition> RoomDefinitionsList;

    /// <summary>
    /// 直接接続すべき部屋についての、QuikGraphを用いた接続グラフ
    /// </summary>
    public AdjacencyGraph<int, Edge<int>> ConnectivityGraph;

    public int MaxGenerationAttempts = 10; // 隣接制約を満たすための最大試行回数
    public float SeedPlacementWeightDistanceFactor = 0.5f; // 外壁からの距離に基づく重み付け係数 
    public float SeedPlacementAdjacencyBonus = 1.0f; // 隣接制約のある部屋の近くの重みボーナス
    public int SeedExclusionRadius = 2; // 配置されたシードの周囲で他のシードを配置しない半径
    public int SeedInclusionRadius = 3; //隣接制約のある部屋の近くの重みボーナスを適応する半径. SIR > SER

    public  FloorPlanSettings(int[,] footprint, List<RoomDefinition> RDlist, AdjacencyGraph<int, Edge<int>> Graph, int MGA = 10, float SPWDF = 0.5f, float SPAB = 1.0f, int SER = 1, int SIR = 2)
    {
        //配置可能マスが(SER+1)^2 * 部屋数より小さいと部屋が配置不可能になる可能性が存在する.
        InputFootprintGrid = footprint;
        RoomDefinitionsList = RDlist;
        ConnectivityGraph = Graph;
        MaxGenerationAttempts = MGA;
        SeedPlacementWeightDistanceFactor = SPWDF;
        SeedPlacementAdjacencyBonus = SPAB;
        // SeedExclusionRadius = SER;
        //SeedInclusionRadius = SIR;
     }
}

// 生成されたフロアプランの出力
public class GeneratedFloorPlan
{
    public int[,] Grid; // 各セルがどの部屋IDに属するかを示すグリッド (0:壁/外, -1:配置可能だが未割当 >0:部屋ID)
    public List<RoomDefinition> Rooms; // IDと部屋定義のマッピング
    public List<Door> Doors; // 配置されたドアのリスト
}

// ドアを表すクラス
public class Door
{
    // ドアが接続する2つのセルの座標.これらのセルは、グリッドの境界を1だけ超えることがある。{
    public Vector2Int Cell1;
    public Vector2Int Cell2;
    public Tuple<int, int> edge;
}

// --- フロアプラン生成クラス ---

public  partial class FloorPlanGenerator : MonoBehaviour
{
    //出力用設定.
    float[,] matrixToDebug;
    public MatrixVisualizer MVinstance;

    //初期化の際に扱うプロパティ.
    private System.Random _random = new System.Random();
    private bool isconfigured = false;
    public FloorPlanSettings settings;//serializable,今のところはunityのインスペクターで設定.
    public void Setup(FloorPlanSettings inputsettings)
    {
        settings = inputsettings;
        isconfigured = true;
    }
    private Vector2Int _gridSize;
    public AdjacencyGraph<int, Edge<int>> _ConnectivityGraph;

    //実行中に変化するプロパティ
    private int[,] _grid; // 0: 建物外/壁/穴, -1: 配置可能だが未割り当て, >0: 部屋ID .出力に使用.
    private List<RoomDefinition> _roomDefinitions;//部屋の特性.
    private int _totalPlaceableCells = 0; // 配置可能なセルの総数
    private List<Door> _doors = new List<Door>();//ドアのリスト.

    /// <summary>
    /// フロアプラン生成のメイン関数
    /// </summary> 
    public GeneratedFloorPlan Generate()
    {
        if (isconfigured == false)
        {
            Debug.LogError("FloorPlanSettings or required inputs are not properly set.");
            return null;
        }

        _roomDefinitions = settings.RoomDefinitionsList;//よくないコピーの仕方.
        _ConnectivityGraph = settings.ConnectivityGraph;

        GeneratedFloorPlan bestPlan = null;
        float bestScore = float.MinValue; // スコアリング実装時に使用

        for (int attempt = 0; attempt < settings.MaxGenerationAttempts; attempt++)
        {
            Debug.Log($"--- Generation Attempt {attempt + 1} ---");
            GeneratedFloorPlan currentPlan = AttemptGeneration();

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

    public void DebugAttempt()
    {
        _roomDefinitions = settings.RoomDefinitionsList;
        _ConnectivityGraph = settings.ConnectivityGraph;
        AttemptGeneration();
    }
    /// <summary>
    /// 1回のフロアプラン生成試行をする.
    /// </summary>
    private GeneratedFloorPlan AttemptGeneration()
    {
        InitializeGrid();
        matrixToDebug = SetFloatMatrix();
        if (_totalPlaceableCells == 0)
        {
            Debug.LogError("No placeable cells found in the InputFootprintGrid.");
            return null;
        }
        // 1. 部屋の初期位置決定 (Room Placement)
        if (!PlaceInitialSeeds())
        {
            Debug.LogError("Failed during PlaceInitialSeeds.");
            return null;
        }
         
        // 2. 部屋の拡張 (Room Expansion)
        if (!ExpandRooms())
        {
            Debug.LogError("Failed during ExpandRooms.");
            return null;
        }
       
        


        // 3. 部屋の接続性 (Room Connectivity)
        if (!DetermineConnectivity())
        {
            Debug.LogError("Failed during DetermineConnectivity.");
            return null;
        }
        if(MVinstance.todebug == ToDebug.DetermineConnectivity)
        {
                MVinstance.VisualizeDoor(_doors);
        }
        MVinstance.Execute(matrixToDebug);
        MVinstance.VisualizeNetwork(ConvertToUndirectedEdgeList(_ConnectivityGraph), _roomDefinitions);
        return null;
        //ここまでしか出来てない
        /*
        // 成功した場合、結果を構築
        GeneratedFloorPlan plan = new GeneratedFloorPlan
        {
            Grid = (int[,])_grid.Clone(),
            Rooms = _roomDefinitions, // RoomDefinitionもディープコピーが必要なら別途対応
            Doors = doors,
        };
        

        return plan;
        */
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
        //ここで_gridSize が初期化されるため、InitializeGrid()以前でsetfloatMatrixを使ってはいけない.
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
        // foreach(var room in plan.Rooms) {
        //      float targetRatio = room.Value.SizeRatio / plan.Rooms.Values.Sum(r => r.SizeRatio);
        //      float actualRatio = GetRoomArea(plan.Grid, room.Key) / totalArea;
        //      ratioError += Mathf.Abs(targetRatio - actualRatio);
        // }
        // score *= Mathf.Clamp01(1.0f - ratioError); // 誤差が大きいほど減点

        return score;
    }

    /// <summary>
    /// 与えられたgridと同じサイズで各要素が0.0fの行列を作成.
    /// </summary>
    public float[,] SetFloatMatrix()
    {
        float[,] returnMatrix = new float[_gridSize.x, _gridSize.y];
        for (int i = 0; i < _gridSize.x; i++)
        {
            for (int j = 0; j < _gridSize.y; j++)
            {
                returnMatrix[i,j] = 0.0f;
            }
        }
        return returnMatrix;
    }

    /// <summary>
    /// この関数は、int型の2次元配列と変換したい型名(type)を文字列で受け取り、
    /// 指定された型の2次元配列に変換した結果を返す.
    /// </summary>
    /// <param name="grid">変換元のint型2次元配列</param>
    /// <param name="type">変換先の型名を示す文字列（現在は"float"のみサポート）</param>
    /// <returns>変換後のfloat型2次元配列。サポートされていない型が指定された場合はnullを返します。</returns>
    public float[,] ConvertMatrix(int[,] grid, string type)
    {
        // 変換先の型として"float"が指定されているかを確認します。
        if (type == "float")
        {
            // 元の配列の行数を取得します。
            int rows = grid.GetLength(0);
            // 元の配列の列数を取得します。
            int cols = grid.GetLength(1);
            // 返却値として使用する、gridと同じサイズのfloat型2次元配列を新たに作成します。
            float[,] floatMatrix = new float[rows, cols];

            // forループを2つ使用して、gridの全ての要素にアクセスします。
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // gridのint型要素をfloat型に明示的にキャスト（型変換）し、新しい配列の対応する位置に格納します。
                    floatMatrix[i, j] = (float)grid[i, j];
                }
            }
            // 型変換が完了したfloat型の2次元配列を関数の結果として返します。
            return floatMatrix;
        }

        // "float"以外の型名が指定された場合、現時点ではサポートしていないためnullを返します。
        return null;
    }

    public RectInt MergeRectInt(RectInt rect1, RectInt rect2)
    {
        // 新しいRectIntのx座標は、二つのRectIntのx座標の小さい方になります。
        int minX = Mathf.Min(rect1.x, rect2.x);
        // 新しいRectIntのy座標は、二つのRectIntのy座標の小さい方になります。
        int minY = Mathf.Min(rect1.y, rect2.y);

        // 新しいRectIntの右端は、二つのRectIntの右端の大きい方になります。
        int maxX = Mathf.Max(rect1.xMax, rect2.xMax);//xmaxはx+widthを返す.
        // 新しいRectIntの下端は、二つのRectIntの下端の大きい方になります。
        int maxY = Mathf.Max(rect1.yMax, rect2.yMax);

        // minX, minYを起点とし、width, heightを算出します。
        // widthはmaxX - minXで計算されます。
        int width = maxX - minX;
        // heightはmaxY - minYで計算されます。
        int height = maxY - minY;

        return new RectInt(minX, minY, width, height);
    }
    /// <summary>
    /// _grid[x, y] が targetNumであればtrueを返し、それ以外ではfalseを返す
    /// </summary>
    private bool CheckGrid(int x,int y,int targetNum)
    {
        if (x < 0 || x >= _gridSize.x || y < 0 || y >= _gridSize.y) return false;
        else return (_grid[x, y] == targetNum);
    }

    /// <summary>
    /// 与えられた接続グラフから、方向を考慮しない辺のリストを生成する関数。
    /// </summary>
    /// <returns>方向を考慮しない辺のタプルのリスト。各タプルは (部屋ID1, 部屋ID2) を示す。</returns>
    public List<Tuple<int, int>> ConvertToUndirectedEdgeList(AdjacencyGraph<int, Edge<int>> graph)
    {
        // 重複する辺を効率的に管理するためにHashSetを使用する。
        // (1, 2) と (2, 1) を同じ辺として扱うために、必ず小さい方のIDがItem1に来るように正規化して格納する。
        var undirectedEdges = new HashSet<Tuple<int, int>>();

        // グラフに含まれる全ての辺をループで処理する
        foreach (var edge in graph.Edges)
        {
            // 辺の始点と終点のIDを取得
            var source = edge.Source;
            var target = edge.Target;

            // IDの順序を正規化し、常に (小さいID, 大きいID) の組を作る 三項演算子.
            var normalizedEdge = source < target
                ? Tuple.Create(source, target)
                : Tuple.Create(target, source);

            // 正規化した辺をHashSetに追加する。重複は自動的に無視される。
            undirectedEdges.Add(normalizedEdge);
        }

        // HashSetをListに変換して返す
        return undirectedEdges.ToList();
    }
}