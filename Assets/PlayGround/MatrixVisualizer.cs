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
    CalculateDistanceToWall, CalculateWeightsForRoom, SelectBestSeedPosition, ExpandTo22,GrowRect, GrowLShape, FillGaps
}

public class MatrixVisualizer : MonoBehaviour
{
    public TMP_FontAsset mainFontAsset;
    public ToDebug todebug;
    private GameObject Parent = null;
    private GameObject LineParent = null;
    public FloorPlanSettings inputSettings;

    public FloorPlanGenerator FPG_instance;
    public int xsize, ysize;
    [Tooltip("�`�悷����̑���")]
    [SerializeField] private float lineWidth = 0.1f;

    [Tooltip("�`�悷����̐F")]
    [SerializeField] private Color lineColor = Color.blue;



    private int[,] CreateFootprint(int x, int y)
    {
        // �z��T�C�Y������������ꍇ�i�O���ȊO�𖄂߂邽�߂ɍŒ�3x3���K�v�j�̃G���[����
        if (x <= 2 || y <= 2)
        {
            Debug.LogError("Footprint size must be at least 3x3 for this generation method.");
            return null; // �����ȏꍇ��null��Ԃ�
        }

        int[,] footprint = new int[x, y];

        // �@ �z���������(�����΂�O���̕����ȊO��1�Ŗ��߂�)
        for (int i = 0; i < x; i++)
        {
            for (int j = 0; j < y; j++)
            {
                // i��0�i��ԍ��j�A�܂���x-1�i��ԉE�j�A
                // �܂��� j��0�i��ԏ�j�A�܂���y-1�i��ԉ��j�̏ꍇ��0�̂܂�
                // ����ȊO�̓����̃}�X��1�Ŗ��߂�
                if (i > 0 && i < x - 1 && j > 0 && j < y - 1)
                {
                    footprint[i, j] = 1; // 1 = ������z�u�\�ȃG���A
                }
                // �O���̃}�X�� int �̃f�t�H���g�l�ł��� 0 �̂܂܂ɂȂ�܂��B
                // 0 = �����O/��/�g�p�s��
            }
        }

        // �A ��񂾂�4�`9���x��0�̒����`�������_���ȃ}�X�ɐ�������
        // �����`�̕��ƍ��������� (���ꂼ��2�܂���3�ɂȂ�悤�ɑI��)
        // Random.Range(min, max) ��min���܂�max���܂܂Ȃ����߁A[2, 3] �͈̔͂ɂ���ɂ� 2, 4 ���w�肵�܂��B
        int rectWidth = UnityEngine.Random.Range(2, 4);
        int rectHeight = UnityEngine.Random.Range(2, 4);

        // �����`�̍�����̊J�n�ʒu�������_���Ɍ���
        // �����`���z����Ɋ��S�Ɏ��܂�悤�ɁA�J�n�ʒu�̃����_���Ȕ͈͂𒲐����܂��B
        // x�����̊J�n�ʒu: 0 ���� (�z��̕� - �����`�̕�) �܂ł͈̔�
        // y�����̊J�n�ʒu: 0 ���� (�z��̍��� - �����`�̍���) �܂ł͈̔�
        int rectStartX = UnityEngine.Random.Range(0, x - rectWidth + 1);
        int rectStartY = UnityEngine.Random.Range(0, y - rectHeight + 1);

        // ���肵�������`�͈̔͂�0�Ŗ��߂� (���Ƃ��Đݒ�)
        for (int i = 0; i < rectWidth; i++)
        {
            for (int j = 0; j < rectHeight; j++)
            {
                // �v�Z�����ʒu (rectStartX + i, rectStartY + j) ���z��͈͓̔��ł��邱�Ƃ��m�F���Ă��܂����A
                // rectStartX �� rectStartY �͈̔͂�K�؂Ɍv�Z���Ă��邽�߁A��ɔ͈͓��ɂȂ�܂��B
                footprint[rectStartX + i, rectStartY + j] = 0; // 0 = �����O/��/�g�p�s�� (���̃C���[�W)
            }
        }

        // �B return
        return footprint;
    }
    public List<RoomDefinition> CreateRoomDefinitionList()
    {
        // RoomDefinition���i�[����List�̃C���X�^���X�𐶐�����B
        var roomDefinitions = new List<RoomDefinition>();

        roomDefinitions.Add(new RoomDefinition(
            id: 1, // �����̃��j�[�NID�B
            type: RoomType.Entrance, // �����̎�ށB
            ratio: 10f // �v���T�C�Y�䗦�B
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
        // �쐬����������`�̃��X�g��Ԃ��B
        return roomDefinitions;
    }

    // ConnectivityGraph (AdjacencyGraph<int, Edge<int>>) �̃C���X�^���X���쐬����֐��B
    public AdjacencyGraph<int, Edge<int>> CreateConnectivityGraph()
    {
        // AdjacencyGraph�̃C���X�^���X�𐶐�����B
        var graph = new AdjacencyGraph<int, Edge<int>>();
        for (int i = 1; i <= 6; i++)
        {
            graph.AddVertex(i);
        }
        // �O���t�ɕӂ�ǉ����Ă����B�ӂ�ǉ�����ƁA�֘A���钸�_�������I�ɒǉ������B
        // Edge<int> �́A�n�_�ƏI�_�̕���ID�����B
        // �����ł�ID�́ACreateRoomDefinitionList�Œ�`����������ID�ɑΉ�����B

        // ���� (ID:1) �� ���r���O (ID:2) �ɐڑ�����B
        graph.AddEdge(new Edge<int>(1,2));
        graph.AddEdge(new Edge<int>(2,1));
        // ���r���O (ID:2) �� �L�b�`�� (ID3) �ɐڑ�����B
        graph.AddEdge(new Edge<int>(2,3));
        graph.AddEdge(new Edge<int>(3,2));
        // ���r���O (ID:2) �� �L�� (ID:6) �ɐڑ�����B
        graph.AddEdge(new Edge<int>(2,6));
        graph.AddEdge(new Edge<int>(6,2));
        // �L�� (ID:6) �� �Q�� (ID:4) �ɐڑ�����B
        graph.AddEdge(new Edge<int>(6,4));
        graph.AddEdge(new Edge<int>(4,6));
        // �L�� (ID:6) �� �o�X���[�� (ID:5) �ɐڑ�����B
        graph.AddEdge(new Edge<int>(6,5));
        graph.AddEdge(new Edge<int>(5,6));
        // �����O���t�Ƃ��Ĉ��������ꍇ�A�t�����̕ӂ���`����
        return graph;
    }
    private void PlaceCube(Vector3 Position, float weight)
    {
        // �L���[�u�̍쐬
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(Parent.transform);
        cube.transform.position = Position + new Vector3 (0.5f, 0f, 0.5f);
        // �L���[�u��Renderer�R���|�[�l���g���擾
        // PrimitiveType.Cube �ō쐬���ꂽGameObject�ɂ�Renderer�R���|�[�l���g�������I�ɃA�^�b�`����܂�
        Renderer renderer = cube.GetComponent<Renderer>();

        Color cubeColor;
        Color textColor;
        // weight�̒l�ɉ����ăL���[�u�ƃe�L�X�g�̐F�����肵�܂�
        if (weight > 0)
        {
            cubeColor = Color.white;
            textColor = Color.HSVToRGB((weight * 0.1f) % 1.0f, 0.7f, 0.7f);
        }
        else if ((int)weight == 0)
        {
            // weight��0�̎��i-1 < weight < 1 �͈̔́j�A�L���[�u�͍��A�e�L�X�g�͔��ɐݒ�
            cubeColor = Color.black;
            textColor = Color.white;
        }
        else //weight < 0
        {
            // weight��-1�̎��i-2 < weight <= -1 �͈̔́j�A�L���[�u�͔��A�e�L�X�g�͍��ɐݒ�
            cubeColor = Color.white;
            textColor = Color.black;
        }
        renderer.material.color = cubeColor;

        string text = weight.ToString();
        // TextMeshPro��3D�e�L�X�g�I�u�W�F�N�g���쐬
        GameObject textObj = new GameObject("CubeText");
        textObj.transform.SetParent(cube.transform);

        // �e�L�X�g�̈ʒu�Ɖ�]�𒲐��i�L���[�u�̏�ʂɔz�u�j
        textObj.transform.localPosition = new Vector3(0, 0.51f, 0);
        textObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        // TextMeshPro�R���|�[�l���g��ǉ�
        TextMeshPro textMeshPro = textObj.AddComponent<TextMeshPro>();
        if (mainFontAsset != null)
        {
            textMeshPro.font = mainFontAsset;
        }
        else
        {
            // �t�H���g���ݒ肳��Ă��Ȃ��ꍇ�̓G���[���o���A�����𒆒f���܂�
            Debug.LogError("Main Font Asset���ݒ肳��Ă��܂���I�C���X�y�N�^����ݒ肵�Ă��������B");
            return;
        }
        textMeshPro.text = text;
        textMeshPro.fontSize =50;
        textMeshPro.alignment = TextAlignmentOptions.Center;
        textMeshPro.color = textColor;

        // �e�L�X�g�̃X�P�[���𒲐�
        textObj.transform.localScale = Vector3.one * 0.1f;
    }

    private void DrawThickLine(Vector3 startPos, Vector3 endPos, Transform parent)
    {
        // ����`�悷�邽�߂̐V�����Q�[���I�u�W�F�N�g���쐬
        GameObject lineObject = new GameObject("ConnectionLine");
        // �e�I�u�W�F�N�g��ݒ肵�āA�q�G�����L�[�𐮗�
        lineObject.transform.SetParent(parent);

        // Line Renderer�R���|�[�l���g��ǉ�
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

        // --- Line Renderer �̐ݒ� ---
        lineRenderer.positionCount = 2; // ���_�̐���ݒ�
        lineRenderer.SetPosition(0, startPos); // �n�_��ݒ�
        lineRenderer.SetPosition(1, endPos); // �I�_��ݒ�

        // �C���X�y�N�^�[�Őݒ肵�������ƐF��K�p
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        // �}�e���A����ݒ肵�Ȃ��ƕ\������Ȃ����߁A�V���v���ȃf�t�H���g�}�e���A�������蓖�Ă�
        // ���̃V�F�[�_�[��Unity�ɕW���Ŋ܂܂�Ă�����̂ł�
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
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
            Destroy(Parent);//Mesh�iMeshFilter.sharedMesh�j, Material�iRenderer.sharedMaterial,Texture,TextMeshPro�̃t�H���g�A�Z�b�g��}�e���A���͉������Ȃ�.
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
            Destroy(LineParent);//Mesh�iMeshFilter.sharedMesh�j, Material�iRenderer.sharedMaterial,Texture,TextMeshPro�̃t�H���g�A�Z�b�g��}�e���A���͉������Ȃ�.
        }
        LineParent = new GameObject();
        Dictionary<int, Vector2Int> roomPositions = roomDefinitions
            .Where(room => room.InitialSeedPosition.HasValue) // InitialSeedPosition��null�łȂ������݂̂��t�B���^�����O
            .ToDictionary(
                room => room.ID,                           // �L�[�ɂ͑啶���́uID�v�v���p�e�B���g�p
                room => room.InitialSeedPosition.Value);   // �l�ɂ�.Value�Ŕ�null���e�^�ɕϊ����Ďg�p


        //  �ӂɏ]���ĕ������m����Ō��� 
        foreach (var edge in edges)
        {
            var id1 = edge.Item1;
            var id2 = edge.Item2;

            // �������痼���̕����̍��W���擾�ł��邩�m�F����
            // (�����̕����̈ʒu���m�肵�Ă���ꍇ�̂ݐ�������)
            if (roomPositions.ContainsKey(id1) && roomPositions.ContainsKey(id2))
            {
                // Vector2Int���W��Vector3�ɕϊ�����
                Vector3 startPoint = new Vector3(roomPositions[id1].x, 0, roomPositions[id1].y ) + new Vector3(0.5f,0.5f,0.5f);
                Vector3 endPoint = new Vector3(roomPositions[id2].x, 0, roomPositions[id2].y ) +new Vector3(0.5f, 0.5f, 0.5f);

                DrawThickLine(startPoint, endPoint, LineParent.transform);
            }
        }

    }
    // Update is called once per frame
    void Update()
    {

    }
}
