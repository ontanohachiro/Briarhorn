using QuikGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public enum ToDebug
{
    Empty ,CalculateDistanceToWall, CalculateWeightsForRoom, SelectBestSeedPosition, ExpandTo22,GrowRect, GrowLShape, FillGaps
    ,DetermineConnectivity
}

public class MatrixVisualizer : MonoBehaviour
{
    public TMP_FontAsset mainFontAsset;
    public ToDebug todebug;
    private GameObject Parent = null;
    private GameObject LineParent = null;
    private GameObject DoorParent = null;
    public FloorPlanSettings inputSettings;
    public int GenerationTime;

    public FloorPlanGenerator FPG_instance;
    public int xsize, ysize;
    [Tooltip("描画する線の太さ")]
    [SerializeField] private float lineWidth = 0.1f;

    [Tooltip("描画する線の色")]
    [SerializeField] private Color lineColor = Color.blue;



    private int[,] CreateFootprint(int x, int y)
    {
        // 配列サイズが小さすぎる場合（外側以外を埋めるために最低3x3が必要）のエラー処理
        if (x <= 2 || y <= 2)
        {
            Debug.LogError("Footprint size must be at least 3x3 for this generation method.");
            return null; // 無効な場合はnullを返す
        }

        int[,] footprint = new int[x, y];

        // �@ 配列を初期化(いちばん外側の部分以外を1で埋める)
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

        // �A 一回だけ4〜9程度の0の長方形をランダムなマスに生成する
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

        // �B return
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
            ratio: 10f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 3,
            type: RoomType.Kitchen,
            ratio: 10f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 4,
            type: RoomType.Bedroom,
            ratio: 10f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 5,
            type: RoomType.Bathroom,
            ratio: 10f
        ));

        roomDefinitions.Add(new RoomDefinition(
            id: 6,
            type: RoomType.Hallway,
            ratio: 10f
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

    private void DrawThickLine(Vector3 startPos, Vector3 endPos, Transform parent)
    {
        // 線を描画するための新しいゲームオブジェクトを作成
        GameObject lineObject = new GameObject("ConnectionLine");
        // 親オブジェクトを設定して、ヒエラルキーを整理
        lineObject.transform.SetParent(parent);

        // Line Rendererコンポーネントを追加
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

        // --- Line Renderer の設定 ---
        lineRenderer.positionCount = 2; // 頂点の数を設定
        lineRenderer.SetPosition(0, startPos); // 始点を設定
        lineRenderer.SetPosition(1, endPos); // 終点を設定

        // インスペクターで設定した太さと色を適用
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        // マテリアルを設定しないと表示されないため、シンプルなデフォルトマテリアルを割り当てる
        // このシェーダーはUnityに標準で含まれているものです
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
    }

    public void DrawDoor(Door door, Transform parent)
    {

        Vector2Int point1 = door.Cell1;
        Vector2Int point2 = door.Cell2;
        // 1. Cubeプリミティブをシーンに生成する
        GameObject rectangleObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rectangleObject.name = "Generated Door";
        rectangleObject.transform.SetParent(parent);
        // 2. 中心位置を計算し、地面に配置する
        Vector3 offset = new Vector3(0.5f, 0, 0.5f);
        Vector2 center = ((Vector2)point1 + (Vector2)point2) / 2f;
        rectangleObject.transform.position = new Vector3(center.x, 0.5f, center.y) + offset;
            // 3. 2点間のベクトルと、その全長(長さ)を計算する
            Vector2 delta = (Vector2)point2 - (Vector2)point1;

        // 4. deltaの向きに応じてlocalScaleを設定する
        // deltaのx成分の絶対値がy成分の絶対値より大きいか(横方向か)どうかを判断
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            // (1,0)ならscaleが(0.1,1,1) 
            rectangleObject.transform.localScale = new Vector3(0.1f, 1f, 1f);
        }
        else
        {
            // (0,1)ならscaleが(1,1,0.1) 
            rectangleObject.transform.localScale = new Vector3(1, 1, 0.1f);
        }

        // 5. 色を茶色に設定する
        Renderer renderer = rectangleObject.GetComponent<Renderer>();
        renderer.material.color = new Color(139f / 255f, 69f / 255f, 19f / 255f);

        // 6. 自動で追加されるコライダーは不要なため破棄する
        UnityEngine.Object.Destroy(rectangleObject.GetComponent<Collider>());
    }
    void Start()
    {
        inputSettings = new FloorPlanSettings(CreateFootprint(xsize, ysize),CreateRoomDefinitionList(),CreateConnectivityGraph());
        FPG_instance.Setup(inputSettings);
        for (int i = 0; i <GenerationTime; i++)
        {
            FPG_instance.DebugAttempt();
        }
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
    public void VisualizeNetwork(List<Tuple<int, int>> edges,List<RoomDefinition> roomDefinitions)
    {
        if (LineParent != null)
        {
            Destroy(LineParent);//Mesh（MeshFilter.sharedMesh）, Material（Renderer.sharedMaterial,Texture,TextMeshProのフォントアセットやマテリアルは解放されない.
        }
        LineParent = new GameObject();
        Dictionary<int, Vector2Int> roomPositions = roomDefinitions
            .Where(room => room.InitialSeedPosition.HasValue) // InitialSeedPositionがnullでない部屋のみをフィルタリング
            .ToDictionary(
                room => room.ID,                           // キーには大文字の「ID」プロパティを使用
                room => room.InitialSeedPosition.Value);   // 値には.Valueで非null許容型に変換して使用


        //  辺に従って部屋同士を線で結ぶ 
        foreach (var edge in edges)
        {
            var id1 = edge.Item1;
            var id2 = edge.Item2;

            // 辞書から両方の部屋の座標を取得できるか確認する
            // (両方の部屋の位置が確定している場合のみ線を引く)
            if (roomPositions.ContainsKey(id1) && roomPositions.ContainsKey(id2))
            {
                // Vector2Int座標をVector3に変換する
                Vector3 startPoint = new Vector3(roomPositions[id1].x, 0, roomPositions[id1].y ) + new Vector3(0.5f,0.5f,0.5f);
                Vector3 endPoint = new Vector3(roomPositions[id2].x, 0, roomPositions[id2].y ) +new Vector3(0.5f, 0.5f, 0.5f);

                DrawThickLine(startPoint, endPoint, LineParent.transform);
            }
        }

    }
    public void VisualizeDoor(List <Door> _doors)
    {
        if (DoorParent != null)
        {
            Destroy(DoorParent);
        }
        DoorParent = new GameObject();
        foreach (var door in _doors)
        {
            DrawDoor(door, DoorParent.transform);
        }
    }
}
