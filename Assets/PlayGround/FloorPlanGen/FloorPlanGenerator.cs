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
        InitialSeedPosition = null;
        Bounds = new RectInt(0,0,0,0);
        CurrentSize = 0;

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
    /// <summary>
    /// デバッグ用に単一のフロアプラン生成を試行し、その結果（成功または失敗）を評価してログに出力します。
    /// </summary>
    public void DebugAttempt()
    {
        // フロアプランの生成を試行します。
        GeneratedFloorPlan plan = AttemptGeneration();

        // planがnullでない、つまり生成に成功した場合
        if (plan != null)
        {
            // 生成されたプランを評価し、デバッグファイルに書き出します。
            // isdebug: true を渡すことで、EvaluatePlan内でファイルへの書き込みが実行されます。
            float score = EvaluatePlan(_totalPlaceableCells, plan, true);

            // 評価スコアをコンソールにも出力します。
            Debug.Log($"Debug Attempt Succeeded. Plan score (variance): {score}");
        }
        // planがnull、つまり生成に失敗した場合
        else
        {
            // 失敗した情報をログファイルに書き込みます。
            string logFilePath = "FloorPlanGenerationLog.txt";
            string failureData = $"Timestamp: {DateTime.Now}\n" +
                                 $"Generation Attempt: FAILED\n" +
                                 "--------------------------------------------------\n";
            try
            {
                // File.AppendAllTextを使い、既存のログファイルに追記します。
                System.IO.File.AppendAllText(logFilePath, failureData);
            }
            catch (Exception e)
            {
                // 書き込みに失敗した場合はエラーログを出力します。
                Debug.LogError($"Failed to write failure log to file: {e.Message}");
            }

            // 生成に失敗したことをコンソールにも出力します。
            Debug.LogWarning("Debug Attempt Failed: AttemptGeneration returned null.");
        }
    }
    /// <summary>
    /// 1回のフロアプラン生成試行をする.
    /// </summary>
    private GeneratedFloorPlan AttemptGeneration()
    {
        _roomDefinitions = DeepCopyHelper.DeepCopyRoomDefinitions(settings.RoomDefinitionsList);
        _ConnectivityGraph = settings.ConnectivityGraph.Clone();
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
        
        
        // 成功した場合、結果を構築
        GeneratedFloorPlan plan = new GeneratedFloorPlan
        {
            //ディープコピー.
            Grid = DeepCopyHelper.DeepCopyGrid(_grid),
            Rooms = DeepCopyHelper.DeepCopyRoomDefinitions(_roomDefinitions), 
            Doors = DeepCopyHelper.DeepCopyDoors(_doors)
        };
        

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
    /// 生成されたプランを評価し、スコアを計算する関数。
    /// 各部屋の実際の面積比と、正規化された目標の面積比との差の分散を計算します。スコアは低いほど良い評価となります。
    /// </summary>
    /// <param name="totalPlaceableCells">配置可能なセルの総数</param>
    /// <param name="plan">評価対象のGeneratedFloorPlan</param>
    /// <param name="isdebug">trueの場合、評価結果をデバッグファイルに書き出すフラグ</param>
    /// <returns>評価スコア（分散）。値が小さいほど、目標の面積比に近い良いプランと評価される。</returns>
    private float EvaluatePlan(int totalPlaceableCells, GeneratedFloorPlan plan, bool isdebug)
    {
        int roomCount = plan.Rooms.Count;

        // 部屋が存在しない、または配置可能セルがない場合は評価不能とする（スコアは最大値）
        if (roomCount == 0 || totalPlaceableCells == 0)
        {
            return float.MaxValue;
        }

        // --- ここからが追加部分 ---
        // 1. 全ての部屋のSizeRatioの合計を計算し、正規化の分母として使用する
        float totalSizeRatioSum = 0f;
        foreach (var room in plan.Rooms)
        {
            totalSizeRatioSum += room.SizeRatio;
        }

        // SizeRatioの合計が0の場合、目標比率を計算できないため評価不能とする
        if (totalSizeRatioSum == 0f)
        {
            Debug.LogWarning("Sum of SizeRatio is 0. Cannot calculate target ratios for evaluation.");
            return float.MaxValue;
        }
        // --- ここまでが追加部分 ---

        // 分散計算のための、差の2乗の合計を格納する変数
        float sumOfSquaredDifferences = 0f;

        // planに含まれる各部屋についてループ処理
        foreach (var room in plan.Rooms)
        {
            // 実際の面積比を計算
            // room.CurrentSize は ExpandRooms で計算された部屋の実際のセル数
            float actualRatio = (float)room.CurrentSize / totalPlaceableCells;

            // --- ここからが修正部分 ---
            // 正規化された目標の面積比を計算
            // (この部屋のSizeRatio) / (全部屋のSizeRatioの合計)
            float targetRatio = room.SizeRatio / totalSizeRatioSum;
            // --- ここまでが修正部分 ---

            // 実際の面積比と目標の面積比の差を計算
            float difference = actualRatio - targetRatio;

            // 差の2乗を合計に加算
            sumOfSquaredDifferences += difference * difference;
        }

        // 分散を計算 (差の2乗の平均)
        float variance = sumOfSquaredDifferences / roomCount;

        // isdebugフラグがtrueの場合、デバッグ情報をファイルに書き込む
        if (isdebug)
        {
            string logFilePath = "FloorPlanGenerationLog.txt";
            string debugData = $"Timestamp: {DateTime.Now}\n" +
                               $"Plan Score (Variance): {variance}\n" +
                               $"Total Placeable Cells: {totalPlaceableCells}\n" +
                               $"Total SizeRatio Sum: {totalSizeRatioSum}\n";

            foreach (var room in plan.Rooms)
            {
                float actualRatio = (float)room.CurrentSize / totalPlaceableCells;
                float targetRatio = room.SizeRatio / totalSizeRatioSum;
                // P2フォーマットでパーセンテージ表示
                debugData += $"  Room ID: {room.ID}, Type: {room.Type}, TargetRatio: {targetRatio:P2} (Raw: {room.SizeRatio}), ActualRatio: {actualRatio:P2}, ActualSize: {room.CurrentSize}\n";
            }
            debugData += "--------------------------------------------------\n";

            try
            {
                System.IO.File.AppendAllText(logFilePath, debugData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write to debug log file: {e.Message}");
            }
        }

        return variance;
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