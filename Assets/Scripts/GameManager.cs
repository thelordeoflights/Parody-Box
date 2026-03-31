using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    [SerializeField] GameObject gameOverPanel, gameWinPanel;
    [SerializeField] CinemachineVirtualCamera virtualCamera;
    [SerializeField] Animator playerAnimator;
    [HideInInspector] public UnityEvent @onGameWin;

    public bool isGameOver = false;

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
        gameWinPanel.SetActive(false);
        @onGameWin.AddListener(GameWin);
    }
    public void GameOver()
    {
        gameOverPanel.SetActive(true);
        virtualCamera.Follow = null;
        virtualCamera.LookAt = null;
        isGameOver = true;
        
    }

    public void GameWin()
    {
        score += 1;
        if(score == 5)
        {
            gameWinPanel.SetActive(true);
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
            isGameOver = true;
            playerAnimator.Play("Idle");
        }
    }
}
