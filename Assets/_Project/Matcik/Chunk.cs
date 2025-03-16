using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
        private Vector2Int coord;
        private bool isGenerated;

        public void Initialize(Vector2Int coord)
        {
            this.coord = coord;
            GenerateContent();
        }

        private void GenerateContent()
        {
            if (isGenerated) return;
            
            // Здесь можно добавить генерацию ландшафта
            // Например: Instantiate(treePrefab, randomPosition, ...);
            
            isGenerated = true;
        }
    }

