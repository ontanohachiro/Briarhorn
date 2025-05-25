using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// --- �X�e�b�v2: ExpandRooms �Ƃ��̕⏕�֐� ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    private bool ExpandRooms(List<RoomDefinition> roomsToPlace)
    {
        Debug.Log("Expanding rooms...");

        // --- �t�F�[�Y1: ��`�g�� (Rectangular Growth) [cite: 119, 128] ---
        HashSet<int> availableRoomsPhase1 = new HashSet<int>(_roomDefinitions.Keys);
        int totalPlaceable = _totalPlaceableCells; // �S�̂̔z�u�\�Z����
        int cellsAssigned = _roomDefinitions.Count; // �����V�[�h��
        int maxIterationsPhase1 = totalPlaceable * 2; // �������[�v�h�~�p
        int iterationPhase1 = 0;


        while (availableRoomsPhase1.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase1 < maxIterationsPhase1)
        {
            iterationPhase1++;
            // �ʐϔ䗦�Ɋ�Â��Ď��Ɋg�����镔����I��
            int roomId = SelectRoomToExpand(availableRoomsPhase1, true);
            if (roomId == -1) break; // �I���ł��镔�����Ȃ�

            // ��`�g�������݂�
            bool grew = GrowRect(roomId);

            if (!grew)
            {
                availableRoomsPhase1.Remove(roomId); // ����ȏ��`�g���ł��Ȃ�
            }
            else
            {
                // �g���ɐ��������ꍇ�A�ēx�g���̌��ɓ���邱�ƂŁA�A���I�Ȋg���𑣂�
                // (�������A�������[�v�ɂȂ�Ȃ��悤�ɒ��ӂ��K�v�BSelectRoomToExpand�̊m���I�I���ł�����x�ɘa�����)
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // ���蓖�čς݃Z�����X�V
                                                                             // Debug.Log($"Phase 1, Iter {iterationPhase1}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}");

        }
        if (iterationPhase1 >= maxIterationsPhase1) Debug.LogWarning("Phase 1 reached max iterations.");


        // --- �t�F�[�Y2: L���^�g�� (L-Shape Growth) ---
        HashSet<int> availableRoomsPhase2 = new HashSet<int>(_roomDefinitions.Keys);
        int maxIterationsPhase2 = (totalPlaceable - cellsAssigned) * _roomDefinitions.Count; // ���Ԗ��ߓI�ȏ����Ȃ̂ő��߂Ɍ��ς���
        int iterationPhase2 = 0;

        while (availableRoomsPhase2.Count > 0 && cellsAssigned < totalPlaceable && iterationPhase2 < maxIterationsPhase2)
        {
            iterationPhase2++;
            // �ʐϔ䗦�͂������܂�l�������A�g���\�ȕ�����D��H [cite: 138] �ł͍ő�T�C�Y�l������
            int roomId = SelectRoomToExpand(availableRoomsPhase2, false); // �䗦�l���Ȃ��őI��
            if (roomId == -1) break;

            // L���^�g�������݂� [cite: 120, 137, 139]
            bool grew = GrowLShape(roomId);

            if (!grew)
            {
                availableRoomsPhase2.Remove(roomId); // ����ȏ�L���g���ł��Ȃ�
            }
            cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // ���蓖�čς݃Z�����X�V
                                                                             // Debug.Log($"Phase 2, Iter {iterationPhase2}: Room {roomId} ({_roomDefinitions[roomId].Type}), Grew: {grew}, Assigned: {cellsAssigned}/{totalPlaceable}");
        }
        if (iterationPhase2 >= maxIterationsPhase2) Debug.LogWarning("Phase 2 reached max iterations.");


        // --- �t�F�[�Y3: ���Ԗ��� (Fill Gaps) [cite: 121, 140] ---
        FillGaps();
        cellsAssigned = _roomDefinitions.Values.Sum(r => r.CurrentSize); // ���蓖�čς݃Z�����X�V


        // �ŏI�`�F�b�N: �����蓖�ăZ�����Ȃ����H
        bool allCellsFilled = AreAllCellsFilled();
        Debug.Log($"Room expansion finished. Assigned cells: {cellsAssigned}/{totalPlaceable}. All filled: {allCellsFilled}");

        // �S�ẴZ�������܂�Ȃ������ꍇ�A���s�Ƃ݂Ȃ����A���e���邩�͗v������
        // if (!allCellsFilled) return false;

        return true;
    }


    /// <summary>
    /// �g���t�F�[�Y�Ŏ��Ɋg�����镔����I������
    /// </summary>
    /// <param name="availableRoomIds">�g���\�ȕ���ID�̃Z�b�g</param>
    /// <param name="useSizeRatio">�ʐϔ䗦���l�����邩�ǂ���</param>
    /// <returns>�I�����ꂽ����ID�A��₪�Ȃ����-1</returns>
    private int SelectRoomToExpand(HashSet<int> availableRoomIds, bool useSizeRatio)
    {
        if (availableRoomIds.Count == 0) return -1;

        // ���݂̃T�C�Y�ƖڕW�T�C�Y�̍��Ɋ�Â��đI������A�v���[�`���l������
        // float GetPriority(int id) {
        //     RoomDefinition room = _roomDefinitions[id];
        //     float targetRatio = room.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio);
        //     float currentRatio = (float)room.CurrentSize / _totalPlaceableCells;
        //     return Mathf.Max(0, targetRatio - currentRatio); // �ڕW�ɒB���Ă��Ȃ��قǗD��x��
        // }
        // var candidates = availableRoomIds.Select(id => new { id, priority = GetPriority(id) }).Where(x => x.priority > 0).ToList();
        // if (candidates.Count == 0) return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count)); // �D��x0�Ȃ烉���_��
        // float totalPriority = candidates.Sum(x => x.priority);
        // double randomValue = _random.NextDouble() * totalPriority;
        // float cumulative = 0;
        // foreach (var candidate in candidates) {
        //      cumulative += candidate.priority;
        //      if (randomValue < cumulative) return candidate.id;
        // }
        // return candidates.Last().id; // �t�H�[���o�b�N


        // �_�� �ɋ߂����� (�ʐϔ䗦�Ɋ�Â��m���I�I��)
        if (useSizeRatio)
        {
            var candidates = availableRoomIds.ToList();
            float totalRatio = candidates.Sum(id => _roomDefinitions[id].SizeRatio);

            if (totalRatio <= 0)
            {
                // �䗦���Ȃ��ꍇ�̓����_���ɑI��
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
            return candidates.Last(); // �t�H�[���o�b�N
        }
        else
        {
            // �䗦���l�����Ȃ��ꍇ�̓����_���ɑI��
            return availableRoomIds.ElementAt(_random.Next(availableRoomIds.Count));
        }
    }


    private bool GrowRect(int roomId)
    {
        // TODO: ����
        // 1. roomId�ɑ�����Z���ɗאڂ���󂫃Z��(-1)��T��
        // 2. �󂫃Z���𐅕��E���������́u���C���v�Ƃ��ăO���[�v��
        // 3. �e���C���ɂ��āA�g�������ꍇ�ɕ�������`���ێ��ł��邩�`�F�b�N
        // 4. ��`���ێ��ł��郉�C���̒��ōł��������̂�I�� (��������΃����_��)
        // 5. �I���������C���̃Z����roomId�Ɋ��蓖�āA_grid��room.CurrentSize���X�V
        // 6. �ڕW�T�C�Y(����)�ɒB������g�����~�߂�I�v�V���� [cite: 133] (�t�F�[�Y1�̂�)
        // 7. �g���ł�����true�A�ł��Ȃ����false
        return false; // �_�~�[
    }

    private bool GrowLShape(int roomId)
    {
        // TODO: ����
        // 1. roomId�ɑ�����Z���ɗאڂ���󂫃Z��(-1)��T��
        // 2. �󂫃Z���𐅕��E���������́u���C���v�Ƃ��ăO���[�v��
        // 3. �e���C���ɂ��āA�g�������ꍇ��L���^�ɂȂ邩�A�܂��͊�����L���^���ێ����邩�`�F�b�N
        // 4. U���^�ɂȂ�Ȃ��悤�Ƀ`�F�b�N [cite: 139]
        // 5. �g���\�ȃ��C���̒��ōł��������̂�I�� (��������΃����_��) [cite: 137]
        // 6. �I���������C���̃Z����roomId�Ɋ��蓖�āA_grid��room.CurrentSize���X�V
        // 7. �g���ł�����true�A�ł��Ȃ����false
        return false; // �_�~�[
    }

    private void FillGaps()
    {
        Debug.Log("Filling remaining gaps...");
        List<Vector2Int> gaps = FindEmptyCells();
        int filledCount = 0;

        // �e�����蓖�ăZ���ɂ��ď��� [cite: 140]
        foreach (Vector2Int gapPos in gaps)
        {
            if (_grid[gapPos.x, gapPos.y] != -1) continue; // ���łɖ��܂��Ă���ꍇ

            Dictionary<int, int> neighborCounts = new Dictionary<int, int>();
            int maxCount = 0;
            int bestNeighborId = -1;

            Vector2Int[] neighbors = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            // �אڂ��镔��ID���J�E���g
            foreach (var offset in neighbors)
            {
                Vector2Int nPos = gapPos + offset;
                if (nPos.x >= 0 && nPos.x < _gridSize.x && nPos.y >= 0 && nPos.y < _gridSize.y)
                {
                    int neighborId = _grid[nPos.x, nPos.y];
                    if (neighborId > 0) // �L���ȕ���ID
                    {
                        if (!neighborCounts.ContainsKey(neighborId)) neighborCounts[neighborId] = 0;
                        neighborCounts[neighborId]++;

                        if (neighborCounts[neighborId] > maxCount)
                        {
                            maxCount = neighborCounts[neighborId];
                            bestNeighborId = neighborId;
                        }
                        // �����̏ꍇ�́A���ʐς̏�����������D�悷��Ȃǂ̃��[�����l������
                        else if (neighborCounts[neighborId] == maxCount && bestNeighborId != -1)
                        {
                            // ��: �ʐϔ䗦������������D�� (���Ԃ͏������������z�����₷��?)
                            if (_roomDefinitions[neighborId].SizeRatio < _roomDefinitions[bestNeighborId].SizeRatio)
                            {
                                bestNeighborId = neighborId;
                            }
                        }
                    }
                }
            }

            // �ł������אڂ��Ă��镔���Ɋ��蓖��
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
    /// �O���b�h���ɖ����蓖�ăZ��(-1)���c���Ă��邩�m�F
    /// </summary>
    private bool AreAllCellsFilled()
    {
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == -1)
                {
                    return false; // �����蓖�ăZ������
                }
            }
        }
        return true; // �S�Ċ��蓖�čς�
    }

    /// <summary>
    /// �O���b�h���̖����蓖�ăZ��(-1)�̃��X�g���擾
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


}
