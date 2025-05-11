using UnityEngine;
using QuikGraph; // QuikGraph の名前空間を使用
using QuikGraph.Algorithms;
using System.Collections.Generic;
using System.Linq;

public class NetworkVisualizer : MonoBehaviour
{
    [Header("Graph Settings")]//Unity 独自の属性,この行の次に宣言される変数を、Unity の Inspector ウィンドウ上で指定したヘッダー名の下にまとめて表示
    [Range(10, 200)] // ノード数の範囲を制限.Unity 独自の属性,指定した min から max の範囲で値を設定
    public int numberOfNodes = 50;

    [Range(10, 1000)] // エッジ数の範囲を制限
    public int numberOfEdges = 100;

    [Header("Visualization Settings")]
    public GameObject nodePrefab; // ノードとして使用する3DオブジェクトのPrefab
    public Material edgeMaterial; // エッジの描画に使用するマテリアル
    public float nodeScale = 0.5f; // ノードのサイズ
    public float graphSpread = 10f; // ノードを配置する範囲

    // QuikGraph のグラフオブジェクト (ノードはint型、エッジはEdge<int>型)
    // 具体的なクラス型で宣言する
    private UndirectedGraph<int, Edge<int>> graph;//QuikGraph 特有のキーワード.無向グラフを表すクラス.
    //<int, Edge<int>> の部分は「ジェネリック」と呼ばれ、このグラフが「ノードとして int 型の値を扱い」、「エッジとして Edge<int> 型の値を扱う」ということを示している.
    //左がノード(頂点),右がエッジ.頂点を表すデータ構造はなく,graphのジェネリック型として代入されたものがそれとなる.
    // グラフノードとUnityのGameObjectを対応付けるための辞書
    private Dictionary<int, GameObject> nodeObjects;

    void Start()
    {
        // QuikGraph の導入が完了しているか確認
        if (!CheckQuikGraphImport())
        {
            Debug.LogError("QuikGraph library is not imported correctly. Please follow the instructions to import QuikGraph and its dependencies DLLs into Assets/Plugins folder.");
            return;
        }

        // グラフの生成
        GenerateGraph();

        // グラフの可視化
        VisualizeGraph();
    }

    // QuikGraph が正しくインポートされているか簡易的に確認
    private bool CheckQuikGraphImport()
    {
        try
        {
            // QuikGraph のクラスを使用してみて例外が発生しないか確認
            var testGraph = new UndirectedGraph<int, Edge<int>>();
            testGraph.AddVertex(0); // AddVertex メソッドが存在するか確認
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }


    // 指定されたノード数とエッジ数を持つランダムグラフを生成
    private void GenerateGraph()
    {
        // 無向グラフを作成
        // graph 変数が UndirectedGraph 型として宣言されているため、ここでインスタンスを代入
        graph = new UndirectedGraph<int, Edge<int>>();
        nodeObjects = new Dictionary<int, GameObject>();

        // ノードを追加
        for (int i = 0; i < numberOfNodes; i++)
        {
            // UndirectedGraph クラスの AddVertex メソッドを呼び出す
            graph.AddVertex(i);//引数が新しいノードとして登録される
        }

        // エッジを追加 (ランダムに、重複・自己ループなし)
        int edgesAdded = 0;
        // 最大可能なエッジ数 (無向グラフ)
        int maxPossibleEdges = numberOfNodes * (numberOfNodes - 1) / 2;
        // 指定されたエッジ数が最大可能数を超える場合は調整
        if (numberOfEdges > maxPossibleEdges)
        {
            Debug.LogWarning($"Requested number of edges ({numberOfEdges}) exceeds the maximum possible edges ({maxPossibleEdges}) for {numberOfNodes} nodes. Setting number of edges to {maxPossibleEdges}.");
            numberOfEdges = maxPossibleEdges;
        }

        // エッジを追加する候補のペアを生成 (重複・自己ループなし)
        var possibleEdges = new List<KeyValuePair<int, int>>();
        for (int i = 0; i < numberOfNodes; i++)
        {
            for (int j = i + 1; j < numberOfNodes; j++) // j = i + 1 から始めることで自己ループと重複を避ける
            {
                possibleEdges.Add(new KeyValuePair<int, int>(i, j));
            }
        }

        // エッジ候補をシャッフル
        //各要素を取り出すたびに、それぞれに対して全く新しい、ランダムな浮動小数点数を計算し、そのランダムな数値をその要素の並べ替えのキーとして使用する.
        possibleEdges = possibleEdges.OrderBy(x => Random.value).ToList();

        // シャッフルされた候補から指定された数だけエッジを追加
        foreach (var edgePair in possibleEdges.Take(numberOfEdges))//Takeはコレクションの先頭から、指定した数の要素だけを取り出す.
        {
            // QuikGraph のエッジオブジェクトを作成
            var edge = new Edge<int>(edgePair.Key, edgePair.Value);
            // UndirectedGraph クラスの AddEdge メソッドを呼び出す
            // AddEdge は自動的に重複を確認してくれます
            if (graph.AddEdge(edge))
            {
                edgesAdded++;
            }

            if (edgesAdded >= numberOfEdges) break; // 指定された数になったら終了
        }

        Debug.Log($"Generated graph with {graph.VertexCount} nodes and {graph.EdgeCount} edges using QuikGraph.");
    }

    // グラフをUnityの3Dオブジェクトとして可視化
    private void VisualizeGraph()
    {
        // ノードオブジェクトを生成し、ランダムな位置に配置
        foreach (int node in graph.Vertices)
        {
            Vector3 randomPosition = new Vector3(
                Random.Range(-graphSpread, graphSpread),
                Random.Range(-graphSpread, graphSpread),
                Random.Range(-graphSpread, graphSpread)
            );

            GameObject nodeObj = Instantiate(nodePrefab, randomPosition, Quaternion.identity);
            nodeObj.transform.localScale = Vector3.one * nodeScale; // サイズ調整
            nodeObj.name = "Node_" + node.ToString(); // オブジェクト名の設定
            nodeObjects.Add(node, nodeObj); // 辞書に登録
        }

        // エッジを描画
        foreach (var edge in graph.Edges)
        {
        // エッジの両端のノードに対応するGameObjectを取得
        //Source: エッジの始点となるノード.Target:エッジの終点となるノード.
            if (nodeObjects.TryGetValue(edge.Source, out GameObject sourceNodeObj) &&
                nodeObjects.TryGetValue(edge.Target, out GameObject targetNodeObj))
            {
                // エッジを描画するGameObjectを作成
                GameObject edgeObj = new GameObject($"Edge_{edge.Source}-{edge.Target}");
                edgeObj.transform.SetParent(transform); // NetworkVisualizerオブジェクトの子にする

                // Line Rendererコンポーネントを追加
                LineRenderer lineRenderer = edgeObj.AddComponent<LineRenderer>();

                // マテリアルと色を設定
                lineRenderer.material = edgeMaterial;
                lineRenderer.startColor = Color.gray;
                lineRenderer.endColor = Color.gray;

                // 幅を設定
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;

                // Line Rendererの位置を設定（エッジの両端のノードの位置）
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, sourceNodeObj.transform.position);
                lineRenderer.SetPosition(1, targetNodeObj.transform.position);

                // Line Rendererのレンダリング設定（任意）
                lineRenderer.useWorldSpace = true;
            }
            else
            {
                Debug.LogWarning($"Could not find node objects for edge between {edge.Source} and {edge.Target}.");
            }
        }
    }

    // 必要に応じて、実行中にグラフや可視化を更新するメソッドなどを追加できます
}