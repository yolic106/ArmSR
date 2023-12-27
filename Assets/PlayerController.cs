using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.UI; // 引入UI命名空间
using System.Collections.Concurrent; // 引入并发集合命名空间


public class PlayerController : MonoBehaviour
{
    // 玩家控制相关变量
    float h;
    float v;
    Rigidbody rb;
    public float speed;
    public float handSpeed;
    public Rigidbody headRigidbody; // 头部的Rigidbody
    public Vector3 headOffset; // 头部相对于身体的偏移量

    // 网络通信相关变量
    TcpListener listener;
    TcpClient client;
    Thread listenerThread;
    int port = 8000; // 与Python程序相同的端口
    string receivedCommand = "";

    // 模式状态变量
    enum ControlMode { Idle, Manual, Autonomous, Follow };
    ControlMode currentMode = ControlMode.Idle;

    // 控制模式变量
    public bool isGestureControl = false; // 是否为手势控制模式

    // UI显示相关变量
    public Text statusText; // 确保在Unity编辑器中将这个变量链接到显示状态的UI Text

    // 金币收集变量
    private int coinCount = 0; // 用于跟踪金币数量
    public Text coinText; // 链接到UI上显示金币数量的Text组件

    // 用于在主线程上执行任务的队列
    private ConcurrentQueue<System.Action> tasks = new ConcurrentQueue<System.Action>();

    // 计时器等相关显示
    private float gameStartTime;
    private float firstMoveTime;
    private float gameEndTime;
    private bool gameStarted;
    private bool gameEnded;
    public Text timerText; // 链接到UI上显示计时器的Text组件
    public Text endGameText; // 链接到UI上显示游戏结束信息的Text组件
    public Text activateText;
    private float startControlTime = 0f;
    private bool isStartingControl = false;


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (headRigidbody != null)
        {
            // 计算初始时头部与身体的偏移量
            headOffset = headRigidbody.position - GetComponent<Rigidbody>().position;
        }
        // 只有在手势控制模式下才初始化网络监听
        if (isGestureControl)
        {
            listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            listenerThread = new Thread(ListenForClients);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
        // 计时器的相关代码
        gameStartTime = Time.time;
        gameStarted = false;
        gameEnded = false;
        endGameText.gameObject.SetActive(false); // 初始时隐藏游戏结束文本
    }

    // Update is called once per frame
    void Update()
    {
        if (headRigidbody != null)
        {
            // 计算新的头部位置，并使用MovePosition更新
            Vector3 newHeadPosition = transform.position + headOffset;
            headRigidbody.MovePosition(newHeadPosition);
        }
        // 执行主线程任务
        while (tasks.TryDequeue(out var task))
        {
            task();
        }

        if (!isGestureControl)//if (currentMode != ControlMode.Manual && !isGestureControl)
        {
            // 键盘模式切换
            if (!isGestureControl)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && currentMode != ControlMode.Manual)
                {
                    currentMode = ControlMode.Autonomous;
                    UpdateStatusText("自主模式");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    currentMode = ControlMode.Idle;
                    UpdateStatusText("待机状态");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3) && currentMode != ControlMode.Manual)
                {
                    currentMode = ControlMode.Follow;
                    UpdateStatusText("跟随模式");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha4) && currentMode == ControlMode.Idle)
                {
                    currentMode = ControlMode.Manual;
                    UpdateStatusText("人控模式");
                }
            }
        }
        // 计时器和游戏结束相关代码
        if (!gameEnded)
        {
            float currentTime = Time.time - gameStartTime;
            timerText.text = "Time: " + currentTime.ToString("F2");

            if (coinCount >= 11)
            {
                gameEnded = true;
                gameEndTime = Time.time;
                float totalDuration = gameEndTime - gameStartTime;
                float moveDuration = gameEndTime - (gameStarted ? firstMoveTime : gameStartTime);
                endGameText.text = "游戏结束\n总时长: " + totalDuration.ToString("F2") + " 秒\n移动任务时长: " + moveDuration.ToString("F2") + " 秒";
                endGameText.gameObject.SetActive(true);
            }
        }
    }

    void FixedUpdate()
    {
        // 计时器代码
        if (currentMode == ControlMode.Manual && !gameStarted)
        {
            if (!isGestureControl && (Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0) ||
                (isGestureControl && (h != 0 || v != 0)))
            {
                firstMoveTime = Time.time;
                gameStarted = true;
            }
        }

        if (currentMode == ControlMode.Manual && !isGestureControl)
        {
            // 键盘控制
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");

            Vector3 force = new Vector3(h, 0, v);
            force *= speed;
            if (!isStartingControl)
            {
                startControlTime = Time.time;
                isStartingControl = true;
                // 在此添加显示开始操纵提示的逻辑，例如更新UI Text
                activateText.text="3秒后启动...";
            }
            // 如果等待时间超过3秒，则允许机器人活动
            if (Time.time - startControlTime >= 3f)
            {
                rb.AddForce(force);
                activateText.text = "活动中...";
            }
            else
            {
                rb.AddForce(0,0,0); // 在等待时间内保持静止
            }
            
        }
        else if (currentMode == ControlMode.Manual && isGestureControl)
        {
            Vector3 velocity = new Vector3(h, 0, v);
            velocity *= (-handSpeed);
            if (!isStartingControl)
            {
                startControlTime = Time.time;
                isStartingControl = true;
                // 在此添加显示开始操纵提示的逻辑，例如更新UI Text
                activateText.text = "3秒后启动...";
            }
            // 如果等待时间超过3秒，则允许机器人活动
            if (Time.time - startControlTime >= 3f)
            {
                rb.velocity = velocity;
                activateText.text = "活动中...";
            }
            else
            {
                rb.velocity = Vector3.zero; // 在等待时间内保持静止 // 在等待时间内保持静止
            }
            
        }
    }

    void ListenForClients()
    {
        while (true)
        {
            client = listener.AcceptTcpClient();
            Thread clientThread = new Thread(HandleClientComm);
            clientThread.IsBackground = true;
            clientThread.Start();
        }
    }

    void HandleClientComm()
    {
        NetworkStream stream = client.GetStream();
        byte[] message = new byte[4096];
        int bytesRead;

        while (true)
        {
            bytesRead = 0;

            try
            {
                bytesRead = stream.Read(message, 0, 4096);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0)
            {
                break;
            }

            receivedCommand = Encoding.ASCII.GetString(message, 0, bytesRead);
            // 将处理命令的调用放入主线程队列
            tasks.Enqueue(() => ProcessCommand(receivedCommand));
        }

        client.Close();
    }

    void ProcessCommand(string command)
    {
        string[] parts = command.Split(':');
        if (parts.Length == 3)
        {
            int gestureCommand = int.Parse(parts[0]);

            // 仅当不在人控模式，或者收到命令2时，才处理模式切换
            if (currentMode != ControlMode.Manual || gestureCommand == 2)
            {
                switch (gestureCommand)
                {
                    
                    case 1:
                        // 自主模式
                        if (currentMode != ControlMode.Manual) // 非人控模式时可切换
                        {
                            currentMode = ControlMode.Autonomous;
                            UpdateStatusText("自主模式");
                        }
                        break;
                    case 2:
                        // 回到Idle模式
                        currentMode = ControlMode.Idle;
                        UpdateStatusText("待机状态");
                        break;
                    case 3:
                        // 跟随模式
                        if (currentMode != ControlMode.Manual) // 非人控模式时可切换
                        {
                            currentMode = ControlMode.Follow;
                            UpdateStatusText("跟随模式");
                        }
                        break;
                    case 4:
                        // 人控模式
                        if (currentMode == ControlMode.Idle) // 仅在Idle模式时可切换
                        {
                            currentMode = ControlMode.Manual;
                            UpdateStatusText("人控模式");
                        }
                        break;
                }
            }

            // 仅在人控模式下使用ax和ay的数据来控制机器人
            if (currentMode == ControlMode.Manual)
            {
                v = float.Parse(parts[1]);
                h = float.Parse(parts[2]);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("collections"))
        {
            coinCount++; // 增加金币数量
            UpdateCoinText(); // 更新UI文本
            Destroy(other.gameObject); // 销毁金币对象
        }
    }

    private void UpdateCoinText()
    {
        coinText.text = coinCount.ToString();
    }


    void UpdateStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    void OnDestroy()
    {
        if (listener != null)
            listener.Stop();
        if (listenerThread != null)
            listenerThread.Abort();
    }

}