using QuikGraph;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum ToDebug
{
    CalculateDistanceToWall, CalculateWeightsForRoom, SelectBestSeedPosition, ExpandTo22,GrowRect, GrowLShape, FillGaps
}

public class MatrixVisualizer : MonoBehaviour
{
    public TMP_FontAsset mainFontAsset;
    public ToDebug todebug;
    private GameObject Parent = null;
    public FloorPlanSettings inputSettings;

    public FloorPlanGenerator FPG_instance;
    public int xsize, ysize;
    
    
    

    private int[,] CreateFootprint(int x, int y)
    {
        // 配列サイズが小さすぎる場合（外側以外を埋めるために最低3x3が必要）のエラー処理
        if (x <= 2 || y <= 2)
        {
            Debug.LogError("Footprint size must be at least 3x3 for this generation method.");
            return null; // 無効な場合はnullを返す
        }

        int[,] footprint = new int[x, y];

        // ① 配列を初期化(いちばん外側の部分以外を1で埋める)
        for (int i = 0; i < x; i++)
        {
            for (int j = 0; j < y; j++)
            {
                // iが0（一番左）、またはx-1（一番右）、
                // または jが0（一番上）、またはy-1（一番下）の場合は0のまま
                // それ以外の内側のマスを1で埋める
                if (i > 0 && i < x - 1 && j > 0 && j < y - 1)
                {
                    footprint[i, j] = 1; // 1 = 部屋を配置可能なエリア
                }
                // 外側のマスは int のデフォルト値である 0 のままになります。
                // 0 = 建物外/穴/使用不可
            }
        }

        // ② 一回だけ4～9程度の0の長方形をランダムなマスに生成する
        // 長方形の幅と高さを決定 (それぞれ2または3になるように選ぶ)
        // Random.Range(min, max) はminを含みmaxを含まないため、[2, 3] の範囲にするには 2, 4 を指定します。
        int rectWidth = UnityEngine.Random.Range(2, 4);
        int rectHeight = UnityEngine.Random.Range(2, 4);

        // 長方形の左上隅の開始位置をランダムに決定
        // 長方形が配列内に完全に収まるように、開始位置のランダムな範囲を調整します。
        // x方向の開始位置: 0 から (配列の幅 - 長方形の幅) までの範囲
        // y方向の開始位置: 0 から (配列の高さ - 長方形の高さ) までの範囲
        int rectStartX = UnityEngine.Random.Range(0, x - rectWidth + 1);
        int rectStartY = UnityEngine.Random.Range(0, y - rectHeight + 1);

        // 決定した長方形の範囲を0で埋める (柱として設定)
        for (int i = 0; i < rectWidth; i++)
        {
            for (int j = 0; j < rectHeight; j++)
            {
                // 計算した位置 (rectStartX + i, rectStartY + j) が配列の範囲内であることを確認していますが、
                // rectStartX と rectStartY の範囲を適切に計算しているため、常に範囲内になります。
                footprint[rectStartX + i, rectStartY + j] = 0; // 0 = 建物外/穴/使用不可 (柱のイメージ)
            }
        }

        // ③ return
        return footprint;
    }
    public List<RoomDefinition> CreateRoomDefinitionList()
    {
        // RoomDefinitionを格納するListのインスタンスを生成する。
        var roomDefinitions = new List<RoomDefinition>();

        roomDefinitions.Add(new RoomDefinition(
            id: 1, // 部屋のユニークID。
            type: RoomType.Entrance, // 部屋の種類。
            ratio: 10f // 要求サイズ比率。
        ));
        roomDefinitions.Add(new RoomDefinition(
            id: 2,
            type: RoomType.LivingRoom,
            ratio: 30f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 3,
            type: RoomType.Kitchen,
            ratio: 15f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 4,
            type: RoomType.Bedroom,
            ratio: 25f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 5,
            type: RoomType.Bathroom,
            ratio: 10f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 6,
            type: RoomType.Hallway,
            ratio: 15f
        ));
        // 作成した部屋定義のリストを返す。
        return roomDefinitions;
    }

    // ConnectivityGraph (AdjacencyGraph<int, Edge<int>>) のインスタンスを作成する関数。
    public AdjacencyGraph<int, Edge<int>> CreateConnectivityGraph()
    {
        // AdjacencyGraphのインスタンスを生成する。
        var graph = new AdjacencyGraph<int, Edge<int>>();
        for (int i = 1; i <= 6; i++)
        {
            graph.AddVertex(i);
        }
        // グラフに辺を追加していく。辺を追加すると、関連する頂点も自動的に追加される。
        // Edge<int> は、始点と終点の部屋IDを持つ。
        // ここでのIDは、CreateRoomDefinitionListで定義した部屋のIDに対応する。

        // 玄関 (ID:1) は リビング (ID:2) に接続する。
        graph.AddEdge(new Edge<int>(1,2));
        graph.AddEdge(new Edge<int>(2,1));
        // リビング (ID:2) は キッチン (ID3) に接続する。
        graph.AddEdge(new Edge<int>(2,3));
        graph.AddEdge(new Edge<int>(3,2));
        // リビング (ID:2) は 廊下 (ID:6) に接続する。
        graph.AddEdge(new Edge<int>(2,6));
        graph.AddEdge(new Edge<int>(6,2));
        // 廊下 (ID:6) は 寝室 (ID:4) に接続する。
        graph.AddEdge(new Edge<int>(6,4));
        graph.AddEdge(new Edge<int>(4,6));
        // 廊下 (ID:6) は バスルーム (ID:5) に接続する。
        graph.AddEdge(new Edge<int>(6,5));
        graph.AddEdge(new Edge<int>(5,6));
        // 無向グラフとして扱いたい場合、逆方向の辺も定義する
        return graph;
    }
    private void PlaceCube(Vector3 Position, float weight)
    {
        // キューブの作成
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(Parent.transform);
        cube.transform.position = Position + new Vector3 (0.5f, 0f, 0.5f);
        // キューブのRendererコンポーネントを取得
        // PrimitiveType.Cube で作成されたGameObjectにはRendererコンポーネントが自動的にアタッチされます
        Renderer renderer = cube.GetComponent<Renderer>();

        Color cubeColor;
        Color textColor;
        // weightの値に応じてキューブとテキストの色を決定します
        if (weight > 0)
        {
            cubeColor = Color.white;
            textColor = Color.HSVToRGB((weight * 0.1f) % 1.0f, 0.7f, 0.7f);
        }
        else if ((int)weight == 0)
        {
            // weightが0の時（-1 < weight < 1 の範囲）、キューブは黒、テキストは白に設定
            cubeColor = Color.black;
            textColor = Color.white;
        }
        else //weight < 0
        {
            // weightが-1の時（-2 < weight <= -1 の範囲）、キューブは白、テキストは黒に設定
            cubeColor = Color.white;
            textColor = Color.black;
        }
        renderer.material.color = cubeColor;

        string text = weight.ToString();
        // TextMeshProの3Dテキストオブジェクトを作成
        GameObject textObj = new GameObject("CubeText");
        textObj.transform.SetParent(cube.transform);

        // テキストの位置と回転を調整（キューブの上面に配置）
        textObj.transform.localPosition = new Vector3(0, 0.51f, 0);
        textObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        // TextMeshProコンポーネントを追加
        TextMeshPro textMeshPro = textObj.AddComponent<TextMeshPro>();
        if (mainFontAsset != null)
        {
            textMeshPro.font = mainFontAsset;
        }
        else
        {
            // フォントが設定されていない場合はエラーを出し、処理を中断します
            Debug.LogError("Main Font Assetが設定されていません！インスペクタから設定してください。");
            return;
        }
        textMeshPro.text = text;
        textMeshPro.fontSize =50;
        textMeshPro.alignment = TextAlignmentOptions.Center;
        textMeshPro.color = textColor;

        // テキストのスケールを調整
        textObj.transform.localScale = Vector3.one * 0.1f;
    }
    
    void Start()
    {
        inputSettings = new FloorPlanSettings(CreateFootprint(xsize, ysize),CreateRoomDefinitionList(),CreateConnectivityGraph());
        FPG_instance.Setup(inputSettings);
    }
    public void Execute(float[,] Matrix)
    {
        if(Parent != null)
        {
            Destroy(Parent);//Mesh（MeshFilter.sharedMesh）, Material（Renderer.sharedMaterial,Texture,TextMeshProのフォントアセットやマテリアルは解放されない.
        }
        Parent = new GameObject();
        for (int i = 0; i < Matrix.GetLength(0); i++)
        {
            for (int j = 0; j < Matrix.GetLength(1); j++)
            {
                Vector3 vecPos = new Vector3((float)i, 0, (float)j);
                PlaceCube(vecPos, Matrix[i, j]);
            }
        }
    }
    // Update is called once per frame
    void Update()
    {

    }
}
