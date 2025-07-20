using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �f�B�[�v�R�s�[���s�����߂̐ÓI�w���p�[�N���X
/// </summary>
public static class DeepCopyHelper
{
    /// <summary>
    /// int�^��2�����z��(_grid)�̃f�B�[�v�R�s�[���쐬���܂��B
    /// </summary>
    /// <param name="originalGrid">�R�s�[����int[,]�^�̔z��</param>
    /// <returns>�e�v�f���R�s�[���ꂽ�V����int[,]�^�̔z��</returns>
    public static int[,] DeepCopyGrid(int[,] originalGrid)
    {
        if (originalGrid == null)
        {
            return null;
        }

        // �R�s�[���Ɠ����T�C�Y�̐V����2�����z����쐬
        int rows = originalGrid.GetLength(0);
        int cols = originalGrid.GetLength(1);
        int[,] newGrid = new int[rows, cols];

        // �S�Ă̗v�f��V�����z��ɃR�s�[
        // int�͒l�^�Ȃ̂ŁA�P���ȑ���Ńf�B�[�v�R�s�[����������܂�
        Array.Copy(originalGrid, newGrid, originalGrid.Length);

        return newGrid;
    }

    /// <summary>
    /// RoomDefinition�̃��X�g(List<RoomDefinition>)�̃f�B�[�v�R�s�[���쐬���܂��B
    /// </summary>
    /// <param name="originalList">�R�s�[����List<RoomDefinition></param>
    /// <returns>�eRoomDefinition�I�u�W�F�N�g���V�����C���X�^���X�����ꂽList<RoomDefinition></returns>
    public static List<RoomDefinition> DeepCopyRoomDefinitions(List<RoomDefinition> originalList)
    {
        if (originalList == null)
        {
            return null;
        }

        var newList = new List<RoomDefinition>();
        foreach (var originalRoom in originalList)
        {
            // RoomDefinition�̃R���X�g���N�^���g�p���Ċ�{�I�ȏ����R�s�[
            var newRoom = new RoomDefinition(originalRoom.ID, originalRoom.Type, originalRoom.SizeRatio);

            // �R���X�g���N�^�Őݒ肳��Ȃ����s���v���p�e�B���R�s�[
            // Vector2Int? �� RectInt �͍\����(�l�^)�Ȃ̂ŁA���ڑ���ŃR�s�[����܂�
            newRoom.InitialSeedPosition = originalRoom.InitialSeedPosition;
            newRoom.Bounds = originalRoom.Bounds;
            newRoom.CurrentSize = originalRoom.CurrentSize;

            newList.Add(newRoom);
        }
        return newList;
    }

    /// <summary>
    /// Door�̃��X�g(List<Door>)�̃f�B�[�v�R�s�[���쐬���܂��B
    /// </summary>
    /// <param name="originalList">�R�s�[����List<Door></param>
    /// <returns>�eDoor�I�u�W�F�N�g���V�����C���X�^���X�����ꂽList<Door></returns>
    public static List<Door> DeepCopyDoors(List<Door> originalList)
    {
        if (originalList == null)
        {
            return null;
        }

        var newList = new List<Door>();
        foreach (var originalDoor in originalList)
        {
            var newDoor = new Door
            {
                // Vector2Int�͍\����(�l�^)�Ȃ̂ŁA���ڑ���ŃR�s�[����܂�
                Cell1 = originalDoor.Cell1,
                Cell2 = originalDoor.Cell2,

                // Tuple�͎Q�ƌ^�Ȃ̂ŁA�V�����C���X�^���X���쐬���Ēl���R�s�[����K�v������܂�
                edge = new Tuple<int, int>(originalDoor.edge.Item1, originalDoor.edge.Item2)
            };
            newList.Add(newDoor);
        }
        return newList;
    }
}