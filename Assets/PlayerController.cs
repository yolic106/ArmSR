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


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 只有在手势控制模式下才初始化网络监听
        if (isGestureControl)
        {
            listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            listenerThread = new Thread(ListenForClients);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
    }

    // Update is called once per frame
    void Update()
    {
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
    }

    void FixedUpdate()
    {
        if (currentMode == ControlMode.Manual && !isGestureControl)
        {
            // 键盘控制
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");

            Vector3 force = new Vector3(h, 0, v);
            force *= speed;
            rb.AddForce(force);
        }
        else if (currentMode == ControlMode.Manual && isGestureControl)
        {
            Vector3 force = new Vector3(h, 0, v);
            force *= handSpeed;
            rb.AddForce(force);
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