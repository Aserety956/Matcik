using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chunk : MonoBehaviour
{
        public Vector2Int coord;
        public bool isGenerated;

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

