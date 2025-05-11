using UnityEngine;
using QuikGraph; // QuikGraph �̖��O��Ԃ��g�p
using QuikGraph.Algorithms;
using System.Collections.Generic;
using System.Linq;

public class NetworkVisualizer : MonoBehaviour
{
    [Header("Graph Settings")]//Unity �Ǝ��̑���,���̍s�̎��ɐ錾�����ϐ����AUnity �� Inspector �E�B���h�E��Ŏw�肵���w�b�_�[���̉��ɂ܂Ƃ߂ĕ\��
    [Range(10, 200)] // �m�[�h���͈̔͂𐧌�.Unity �Ǝ��̑���,�w�肵�� min ���� max �͈̔͂Œl��ݒ�
    public int numberOfNodes = 50;

    [Range(10, 1000)] // �G�b�W���͈̔͂𐧌�
    public int numberOfEdges = 100;

    [Header("Visualization Settings")]
    public GameObject nodePrefab; // �m�[�h�Ƃ��Ďg�p����3D�I�u�W�F�N�g��Prefab
    public Material edgeMaterial; // �G�b�W�̕`��Ɏg�p����}�e���A��
    public float nodeScale = 0.5f; // �m�[�h�̃T�C�Y
    public float graphSpread = 10f; // �m�[�h��z�u����͈�

    // QuikGraph �̃O���t�I�u�W�F�N�g (�m�[�h��int�^�A�G�b�W��Edge<int>�^)
    // ��̓I�ȃN���X�^�Ő錾����
    private UndirectedGraph<int, Edge<int>> graph;//QuikGraph ���L�̃L�[���[�h.�����O���t��\���N���X.
    //<int, Edge<int>> �̕����́u�W�F�l���b�N�v�ƌĂ΂�A���̃O���t���u�m�[�h�Ƃ��� int �^�̒l�������v�A�u�G�b�W�Ƃ��� Edge<int> �^�̒l�������v�Ƃ������Ƃ������Ă���.
    //�����m�[�h(���_),�E���G�b�W.���_��\���f�[�^�\���͂Ȃ�,graph�̃W�F�l���b�N�^�Ƃ��đ�����ꂽ���̂�����ƂȂ�.
    // �O���t�m�[�h��Unity��GameObject��Ή��t���邽�߂̎���
    private Dictionary<int, GameObject> nodeObjects;

    void Start()
    {
        // QuikGraph �̓������������Ă��邩�m�F
        if (!CheckQuikGraphImport())
        {
            Debug.LogError("QuikGraph library is not imported correctly. Please follow the instructions to import QuikGraph and its dependencies DLLs into Assets/Plugins folder.");
            return;
        }

        // �O���t�̐���
        GenerateGraph();

        // �O���t�̉���
        VisualizeGraph();
    }

    // QuikGraph ���������C���|�[�g����Ă��邩�ȈՓI�Ɋm�F
    private bool CheckQuikGraphImport()
    {
        try
        {
            // QuikGraph �̃N���X���g�p���Ă݂ė�O���������Ȃ����m�F
            var testGraph = new UndirectedGraph<int, Edge<int>>();
            testGraph.AddVertex(0); // AddVertex ���\�b�h�����݂��邩�m�F
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }


    // �w�肳�ꂽ�m�[�h���ƃG�b�W�����������_���O���t�𐶐�
    private void GenerateGraph()
    {
        // �����O���t���쐬
        // graph �ϐ��� UndirectedGraph �^�Ƃ��Đ錾����Ă��邽�߁A�����ŃC���X�^���X����
        graph = new UndirectedGraph<int, Edge<int>>();
        nodeObjects = new Dictionary<int, GameObject>();

        // �m�[�h��ǉ�
        for (int i = 0; i < numberOfNodes; i++)
        {
            // UndirectedGraph �N���X�� AddVertex ���\�b�h���Ăяo��
            graph.AddVertex(i);//�������V�����m�[�h�Ƃ��ēo�^�����
        }

        // �G�b�W��ǉ� (�����_���ɁA�d���E���ȃ��[�v�Ȃ�)
        int edgesAdded = 0;
        // �ő�\�ȃG�b�W�� (�����O���t)
        int maxPossibleEdges = numberOfNodes * (numberOfNodes - 1) / 2;
        // �w�肳�ꂽ�G�b�W�����ő�\���𒴂���ꍇ�͒���
        if (numberOfEdges > maxPossibleEdges)
        {
            Debug.LogWarning($"Requested number of edges ({numberOfEdges}) exceeds the maximum possible edges ({maxPossibleEdges}) for {numberOfNodes} nodes. Setting number of edges to {maxPossibleEdges}.");
            numberOfEdges = maxPossibleEdges;
        }

        // �G�b�W��ǉ�������̃y�A�𐶐� (�d���E���ȃ��[�v�Ȃ�)
        var possibleEdges = new List<KeyValuePair<int, int>>();
        for (int i = 0; i < numberOfNodes; i++)
        {
            for (int j = i + 1; j < numberOfNodes; j++) // j = i + 1 ����n�߂邱�ƂŎ��ȃ��[�v�Əd���������
            {
                possibleEdges.Add(new KeyValuePair<int, int>(i, j));
            }
        }

        // �G�b�W�����V���b�t��
        //�e�v�f�����o�����тɁA���ꂼ��ɑ΂��đS���V�����A�����_���ȕ��������_�����v�Z���A���̃����_���Ȑ��l�����̗v�f�̕��בւ��̃L�[�Ƃ��Ďg�p����.
        possibleEdges = possibleEdges.OrderBy(x => Random.value).ToList();

        // �V���b�t�����ꂽ��₩��w�肳�ꂽ�������G�b�W��ǉ�
        foreach (var edgePair in possibleEdges.Take(numberOfEdges))//Take�̓R���N�V�����̐擪����A�w�肵�����̗v�f���������o��.
        {
            // QuikGraph �̃G�b�W�I�u�W�F�N�g���쐬
            var edge = new Edge<int>(edgePair.Key, edgePair.Value);
            // UndirectedGraph �N���X�� AddEdge ���\�b�h���Ăяo��
            // AddEdge �͎����I�ɏd�����m�F���Ă���܂�
            if (graph.AddEdge(edge))
            {
                edgesAdded++;
            }

            if (edgesAdded >= numberOfEdges) break; // �w�肳�ꂽ���ɂȂ�����I��
        }

        Debug.Log($"Generated graph with {graph.VertexCount} nodes and {graph.EdgeCount} edges using QuikGraph.");
    }

    // �O���t��Unity��3D�I�u�W�F�N�g�Ƃ��ĉ���
    private void VisualizeGraph()
    {
        // �m�[�h�I�u�W�F�N�g�𐶐����A�����_���Ȉʒu�ɔz�u
        foreach (int node in graph.Vertices)
        {
            Vector3 randomPosition = new Vector3(
                Random.Range(-graphSpread, graphSpread),
                Random.Range(-graphSpread, graphSpread),
                Random.Range(-graphSpread, graphSpread)
            );

            GameObject nodeObj = Instantiate(nodePrefab, randomPosition, Quaternion.identity);
            nodeObj.transform.localScale = Vector3.one * nodeScale; // �T�C�Y����
            nodeObj.name = "Node_" + node.ToString(); // �I�u�W�F�N�g���̐ݒ�
            nodeObjects.Add(node, nodeObj); // �����ɓo�^
        }

        // �G�b�W��`��
        foreach (var edge in graph.Edges)
        {
        // �G�b�W�̗��[�̃m�[�h�ɑΉ�����GameObject���擾
        //Source: �G�b�W�̎n�_�ƂȂ�m�[�h.Target:�G�b�W�̏I�_�ƂȂ�m�[�h.
            if (nodeObjects.TryGetValue(edge.Source, out GameObject sourceNodeObj) &&
                nodeObjects.TryGetValue(edge.Target, out GameObject targetNodeObj))
            {
                // �G�b�W��`�悷��GameObject���쐬
                GameObject edgeObj = new GameObject($"Edge_{edge.Source}-{edge.Target}");
                edgeObj.transform.SetParent(transform); // NetworkVisualizer�I�u�W�F�N�g�̎q�ɂ���

                // Line Renderer�R���|�[�l���g��ǉ�
                LineRenderer lineRenderer = edgeObj.AddComponent<LineRenderer>();

                // �}�e���A���ƐF��ݒ�
                lineRenderer.material = edgeMaterial;
                lineRenderer.startColor = Color.gray;
                lineRenderer.endColor = Color.gray;

                // ����ݒ�
                lineRenderer.startWidth = 0.1f;
                lineRenderer.endWidth = 0.1f;

                // Line Renderer�̈ʒu��ݒ�i�G�b�W�̗��[�̃m�[�h�̈ʒu�j
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, sourceNodeObj.transform.position);
                lineRenderer.SetPosition(1, targetNodeObj.transform.position);

                // Line Renderer�̃����_�����O�ݒ�i�C�Ӂj
                lineRenderer.useWorldSpace = true;
            }
            else
            {
                Debug.LogWarning($"Could not find node objects for edge between {edge.Source} and {edge.Target}.");
            }
        }
    }

    // �K�v�ɉ����āA���s���ɃO���t��������X�V���郁�\�b�h�Ȃǂ�ǉ��ł��܂�
}