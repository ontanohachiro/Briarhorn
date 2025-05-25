using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatrixVisualizer : MonoBehaviour
{
    private GameObject Parent = null;
    public FloorPlanSettings inputSettings;

    public FloorPlanGenerator FPG_instance;
    public enum OutputTarget { };
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
    private void PlaceCube(Vector3 Position, float weight)
    {
        // キューブの作成
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(Parent.transform);
        cube.transform.position = Position + new Vector3 (0.5f, 0f, 0.5f);
        // キューブのRendererコンポーネントを取得
        // PrimitiveType.Cube で作成されたGameObjectにはRendererコンポーネントが自動的にアタッチされます
        Renderer renderer = cube.GetComponent<Renderer>();

        // weightの値に基づいて色を決定し、マテリアルに設定
        // weightの取りうる具体的な値の範囲が不明なため、ここでは簡単のために0から1の範囲にクランプして使用します。
        // もしweightが特定の範囲 (例: minWeight から maxWeight) を取る場合は、
        // float t = Mathf.InverseLerp(minWeight, maxWeight, weight); のように正規化してからColor.Lerpを使うと良いでしょう。
        float normalizedWeight = Mathf.Clamp01(weight); // weightを0から1の範囲にクランプ

        // weightが0に近いほど Color.blue, weightが1に近いほど Color.red になります
        Color cubeColor = Color.Lerp(Color.black, Color.white, normalizedWeight);

        // キューブのマテリアルの色を設定
        // material は新しいインスタンスを生成するため、他のオブジェクトに影響しません。
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
        textMeshPro.text = text;
        textMeshPro.fontSize =40;
        textMeshPro.alignment = TextAlignmentOptions.Center;
        textMeshPro.color = Color.black;

        // テキストのスケールを調整
        textObj.transform.localScale = Vector3.one * 0.1f;
    }
    // Start is called before the first frame update
    void Start()
    {
        int[,] footprint = CreateFootprint(xsize, ysize);
        //inputSettings = new FloorPlanSettings(footprint,);
    }
    public void execute(float[,] Matrix)
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
