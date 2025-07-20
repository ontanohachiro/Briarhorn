using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// QuikGraph���C�u�����̖��O��ԁB�O���t�\�����������߂ɕK�v�ł��B
using QuikGraph;

// --- �X�e�b�v3: DetermineConnectivity �Ƃ��̕⏕�֐� ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// �N���X���ێ�����ڑ��O���t(_ConnectivityGraph)�Ɋ�Â��A�����ԂɃh�A��ݒu����ǂ����肷��֐��B
    /// </summary>
    public bool DetermineConnectivity()
    {
        // --- (0) �ӂ̃��X�g�𐶐� ---
        // �N���X�����L���O���t�𖳌��O���t�̕Ӄ��X�g�ɕϊ�
        _doors.Clear();
        List<Tuple<int, int>> connections = ConvertToUndirectedEdgeList(_ConnectivityGraph);
      

        // �O���[�o���ȏd�݌����}�b�v�B�h�A�ݒu�ɂ��e����~�ς���B
        var doorWeightsReductionH = new float[_gridSize.x, _gridSize.y + 1];
        var doorWeightsReductionV = new float[_gridSize.x + 1, _gridSize.y];

        // --- (2, 3, 4) �h�A�̌���A�ݒu�A���ӏd�݂̍X�V���e�ڑ��ɂ��čs�� ---
        foreach (var connection in connections)
        {
            // --- (�ӂ��ƂɌŗL��)�X�e�b�v2: �ǋ�Ԃɏd�݂�t���� ---
            var positiveWeightsH = new float[_gridSize.x, _gridSize.y + 1];
            var positiveWeightsV = new float[_gridSize.x + 1, _gridSize.y];

            RoomDefinition room1 = GetRoomById(connection.Item1);
            RoomDefinition room2 = GetRoomById(connection.Item2);

            if (room1 == null || room2 == null)
            {
                Debug.LogError($"Error: Room not found for connection ({connection.Item1}, {connection.Item2})");
                continue;
            }

            // �O���b�h�𑖍����A���̐ڑ��y�A�ɊY�����鋤�L�ǂɂ̂ݏd��(+1)��t����
            for (int y = 0; y < _gridSize.y; y++)
            {
                for (int x = 0; x < _gridSize.x; x++)
                {
                    int id1 = _grid[x, y];
                    // �E�ׂ̃Z���Ƃ̋��E���`�F�b�N
                    if (x + 1 < _gridSize.x)
                    {
                        int id2 = _grid[x + 1, y];
                        // ���݂̐ڑ��y�A(room1, room2)�̋��E���`�F�b�N
                        if ((id1 == room1.ID && id2 == room2.ID) || (id1 == room2.ID && id2 == room1.ID))
                        {
                            positiveWeightsV[x + 1, y] += 1.0f;
                        }
                    }
                    // ���ׂ̃Z���Ƃ̋��E���`�F�b�N
                    if (y + 1 < _gridSize.y)
                    {
                        int id2 = _grid[x, y + 1];
                        // ���݂̐ڑ��y�A(room1, room2)�̋��E���`�F�b�N
                        if ((id1 == room1.ID && id2 == room2.ID) || (id1 == room2.ID && id2 == room1.ID))
                        {
                            positiveWeightsH[x, y + 1] += 1.0f;
                        }
                    }
                }
            }

            // --- (�ӂ��ƂɌŗL��)�X�e�b�v3: �h�A�̌��� ---
            // �h�A���ƂȂ�ǂ̃��X�g
            var potentialDoors = new List<Tuple<Vector2Int, bool>>();//��ڂ�bool�l�́AV��H����\��.true=V
            float maxEffectiveWeight = -1f;

            // ���̐ڑ������L����\���̂���ǂ����ׂă`�F�b�N
            // �iBounds�͊��S�ł͂Ȃ����A�T���͈͂����肷��ɂ͏\���j
            RectInt combinedBounds = new RectInt(
                Mathf.Min(room1.Bounds.xMin, room2.Bounds.xMin),
                Mathf.Min(room1.Bounds.yMin, room2.Bounds.yMin),
                Mathf.Max(room1.Bounds.xMax, room2.Bounds.xMax) - Mathf.Min(room1.Bounds.xMin, room2.Bounds.xMin),
                Mathf.Max(room1.Bounds.yMax, room2.Bounds.yMax) - Mathf.Min(room1.Bounds.yMin, room2.Bounds.yMin)
            );

            // �����ȕǂ̕]��
            for (int y = combinedBounds.yMin; y < combinedBounds.yMax; y++)
            {
                for (int x = combinedBounds.xMin; x < combinedBounds.xMax + 1; x++)
                {
                    if (positiveWeightsV[x, y] > 0)
                    {
                        float effectiveWeight = positiveWeightsV[x, y] + doorWeightsReductionV[x, y];
                        if (effectiveWeight > maxEffectiveWeight)
                        {
                            maxEffectiveWeight = effectiveWeight;
                            potentialDoors.Clear();
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), true));
                        }
                        else if (effectiveWeight == maxEffectiveWeight)
                        {
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), true));
                        }
                    }
                }
            }

            // �����ȕǂ̕]��
            for (int y = combinedBounds.yMin; y < combinedBounds.yMax + 1; y++)
            {
                for (int x = combinedBounds.xMin; x < combinedBounds.xMax; x++)
                {
                    if (positiveWeightsH[x, y] > 0)
                    {
                        float effectiveWeight = positiveWeightsH[x, y] + doorWeightsReductionH[x, y];
                        if (effectiveWeight > maxEffectiveWeight)
                        {
                            maxEffectiveWeight = effectiveWeight;
                            potentialDoors.Clear();
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), false));
                        }
                        else if (effectiveWeight == maxEffectiveWeight)
                        {
                            potentialDoors.Add(new Tuple<Vector2Int, bool>(new Vector2Int(x, y), false));
                        }
                    }
                }
            }

            // �h�A��ݒu�ł���ǂ�������Ȃ������ꍇ
            if (potentialDoors.Count == 0)
            {
                return false;
            }

            // �d�݂��ő�̕ǂ̒����烉���_����1��I�����ăh�A��ݒu
            var chosenDoor = potentialDoors[UnityEngine.Random.Range(0, potentialDoors.Count)];
            Vector2Int doorPos = chosenDoor.Item1;
            bool isVertical = chosenDoor.Item2;

            Door newDoor = new Door();
            if (isVertical)
            {
                newDoor.Cell1 = new Vector2Int(doorPos.x - 1, doorPos.y);
                newDoor.Cell2 = new Vector2Int(doorPos.x, doorPos.y);
            }
            else
            {
                newDoor.Cell1 = new Vector2Int(doorPos.x, doorPos.y - 1);
                newDoor.Cell2 = new Vector2Int(doorPos.x, doorPos.y);
            }
            newDoor.edge = connection;
            _doors.Add(newDoor);

            // --- �X�e�b�v4: ����̕ǂ̏d�݂������� ---
            // �O���[�o���ȏd�݌����}�b�v���X�V����
            DecreaseSurroundingWeights(doorPos.x, doorPos.y, isVertical, -0.1f, doorWeightsReductionV, doorWeightsReductionH);
        }
        Debug.Log("Complete Determine Conectivity");
        return true;
    }

    

    /// <summary>
    /// ���̊֐��́A�h�A�̎���ɂ���8�̕ǂ̏d�݂�������⏕�֐��B
    /// </summary>
    private void DecreaseSurroundingWeights(int doorX, int doorY, bool isVertical, float reduction, float[,] reductionV, float[,] reductionH)
    {
        // �w�肳�ꂽ�d�݌����}�b�v�ɑ΂��āA�ǂ̏d�݂��X�V����
        Action<int, int, bool, float> AddReductionWeight = (x, y, isVert, val) =>
        {
            if (isVert)
            {
                if (x >= 0 && x < _gridSize.x + 1 && y >= 0 && y < _gridSize.y)
                    reductionV[x, y] += val;
            }
            else
            {
                if (x >= 0 && x < _gridSize.x && y >= 0 && y < _gridSize.y + 1)
                    reductionH[x, y] += val;
            }
        };

        if (isVertical)
        {
            AddReductionWeight(doorX, doorY - 1, true, reduction);
            AddReductionWeight(doorX, doorY + 1, true, reduction);
            AddReductionWeight(doorX - 1, doorY, false, reduction);
            AddReductionWeight(doorX - 1, doorY + 1, false, reduction);
            AddReductionWeight(doorX - 1, doorY, true, reduction);
            AddReductionWeight(doorX, doorY, false, reduction);
            AddReductionWeight(doorX, doorY + 1, false, reduction);
            AddReductionWeight(doorX + 1, doorY, true, reduction);
        }
        else
        {
            AddReductionWeight(doorX - 1, doorY, false, reduction);
            AddReductionWeight(doorX + 1, doorY, false, reduction);
            AddReductionWeight(doorX, doorY - 1, false, reduction);
            AddReductionWeight(doorX, doorY - 1, true, reduction);
            AddReductionWeight(doorX + 1, doorY - 1, true, reduction);
            AddReductionWeight(doorX, doorY + 1, false, reduction);
            AddReductionWeight(doorX, doorY, true, reduction);
            AddReductionWeight(doorX + 1, doorY, true, reduction);
        }
    }

    /// <summary>
    /// _roomDefinitions�̒�����w�肳�ꂽID����������Ԃ��⏕�֐��B
    /// </summary>
    private RoomDefinition GetRoomById(int id)
    {
        return _roomDefinitions.FirstOrDefault(r => r.ID == id);
    }
}