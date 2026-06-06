using UnityEngine;

public class WorldManager : MonoBehaviour
{
    [Header("Chunks")]
    public GameObject[] tileMapChunks; // 3 TileMap GameObjects
    public float chunkWidth = 50f;
    private int currentChunkIndex = 1;
    // Update is called once per frame
    void Update()
    {
        float currentChunkCenter = tileMapChunks[currentChunkIndex].transform.position.x;
        float playerX = PlayerController.Instance.transform.position.x;

        if (playerX > currentChunkCenter + chunkWidth / 2)
        {
            MoveChunkToRight();
        }
        // Nach links gewechselt?
        else if (playerX < currentChunkCenter - chunkWidth/2)
        {
            MoveChunkToLeft();
        }
    }
    
    void MoveChunkToRight()
    {
        // Linken Chunk nach rechts verschieben
        int leftChunkIndex = (currentChunkIndex - 1 + 3) % 3;
        int rightChunkIndex = (currentChunkIndex + 1) % 3;
        
        Vector3 newPos = tileMapChunks[rightChunkIndex].transform.position;
        newPos.x += chunkWidth;
        tileMapChunks[leftChunkIndex].transform.position = newPos;
        
        currentChunkIndex = rightChunkIndex;
    }

    void MoveChunkToLeft()
    {
        // Rechten Chunk nach links verschieben
        int rightChunkIndex = (currentChunkIndex + 1) % 3;
        int leftChunkIndex = (currentChunkIndex - 1 + 3) % 3;
        
        Vector3 newPos = tileMapChunks[leftChunkIndex].transform.position;
        newPos.x -= chunkWidth;
        tileMapChunks[rightChunkIndex].transform.position = newPos;
        
        currentChunkIndex = leftChunkIndex;
    }
}
