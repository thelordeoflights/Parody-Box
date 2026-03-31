using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] CinemachineVirtualCamera virtualCamera;

    public int score = 0;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        gameOverPanel.SetActive(false);
    }
    public void GameOver()
    {
        gameOverPanel.SetActive(true);
        virtualCamera.Follow = null;
        virtualCamera.LookAt = null;
    }
}
