using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine.UI; // ����UI�����ռ�
using System.Collections.Concurrent; // ���벢�����������ռ�


public class PlayerController : MonoBehaviour
{
    // ��ҿ�����ر���
    float h;
    float v;
    Rigidbody rb;
    public float speed;
    public float handSpeed;
    public Rigidbody headRigidbody; // ͷ����Rigidbody
    public Vector3 headOffset; // ͷ������������ƫ����

    // ����ͨ����ر���
    TcpListener listener;
    TcpClient client;
    Thread listenerThread;
    int port = 8000; // ��Python������ͬ�Ķ˿�
    string receivedCommand = "";

    // ģʽ״̬����
    enum ControlMode { Idle, Manual, Autonomous, Follow };
    ControlMode currentMode = ControlMode.Idle;

    // ����ģʽ����
    public bool isGestureControl = false; // �Ƿ�Ϊ���ƿ���ģʽ

    // UI��ʾ��ر���
    public Text statusText; // ȷ����Unity�༭���н�����������ӵ���ʾ״̬��UI Text

    // ����ռ�����
    private int coinCount = 0; // ���ڸ��ٽ������
    public Text coinText; // ���ӵ�UI����ʾ���������Text���

    // ���������߳���ִ������Ķ���
    private ConcurrentQueue<System.Action> tasks = new ConcurrentQueue<System.Action>();

    // ��ʱ���������ʾ
    private float gameStartTime;
    private float firstMoveTime;
    private float gameEndTime;
    private bool gameStarted;
    private bool gameEnded;
    public Text timerText; // ���ӵ�UI����ʾ��ʱ����Text���
    public Text endGameText; // ���ӵ�UI����ʾ��Ϸ������Ϣ��Text���
    public Text activateText;
    private float startControlTime = 0f;
    private bool isStartingControl = false;


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (headRigidbody != null)
        {
            // �����ʼʱͷ���������ƫ����
            headOffset = headRigidbody.position - GetComponent<Rigidbody>().position;
        }
        // ֻ�������ƿ���ģʽ�²ų�ʼ���������
        if (isGestureControl)
        {
            listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            listenerThread = new Thread(ListenForClients);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
        // ��ʱ������ش���
        gameStartTime = Time.time;
        gameStarted = false;
        gameEnded = false;
        endGameText.gameObject.SetActive(false); // ��ʼʱ������Ϸ�����ı�
    }

    // Update is called once per frame
    void Update()
    {
        if (headRigidbody != null)
        {
            // �����µ�ͷ��λ�ã���ʹ��MovePosition����
            Vector3 newHeadPosition = transform.position + headOffset;
            headRigidbody.MovePosition(newHeadPosition);
        }
        // ִ�����߳�����
        while (tasks.TryDequeue(out var task))
        {
            task();
        }

        if (!isGestureControl)//if (currentMode != ControlMode.Manual && !isGestureControl)
        {
            // ����ģʽ�л�
            if (!isGestureControl)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && currentMode != ControlMode.Manual)
                {
                    currentMode = ControlMode.Autonomous;
                    UpdateStatusText("����ģʽ");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    currentMode = ControlMode.Idle;
                    UpdateStatusText("����״̬");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3) && currentMode != ControlMode.Manual)
                {
                    currentMode = ControlMode.Follow;
                    UpdateStatusText("����ģʽ");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha4) && currentMode == ControlMode.Idle)
                {
                    currentMode = ControlMode.Manual;
                    UpdateStatusText("�˿�ģʽ");
                }
            }
        }
        // ��ʱ������Ϸ������ش���
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
                endGameText.text = "��Ϸ����\n��ʱ��: " + totalDuration.ToString("F2") + " ��\n�ƶ�����ʱ��: " + moveDuration.ToString("F2") + " ��";
                endGameText.gameObject.SetActive(true);
            }
        }
    }

    void FixedUpdate()
    {
        // ��ʱ������
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
            // ���̿���
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");

            Vector3 force = new Vector3(h, 0, v);
            force *= speed;
            if (!isStartingControl)
            {
                startControlTime = Time.time;
                isStartingControl = true;
                // �ڴ������ʾ��ʼ������ʾ���߼����������UI Text
                activateText.text="3�������...";
            }
            // ����ȴ�ʱ�䳬��3�룬����������˻
            if (Time.time - startControlTime >= 3f)
            {
                rb.AddForce(force);
                activateText.text = "���...";
            }
            else
            {
                rb.AddForce(0,0,0); // �ڵȴ�ʱ���ڱ��־�ֹ
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
                // �ڴ������ʾ��ʼ������ʾ���߼����������UI Text
                activateText.text = "3�������...";
            }
            // ����ȴ�ʱ�䳬��3�룬����������˻
            if (Time.time - startControlTime >= 3f)
            {
                rb.velocity = velocity;
                activateText.text = "���...";
            }
            else
            {
                rb.velocity = Vector3.zero; // �ڵȴ�ʱ���ڱ��־�ֹ // �ڵȴ�ʱ���ڱ��־�ֹ
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
            // ����������ĵ��÷������̶߳���
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

            // ���������˿�ģʽ�������յ�����2ʱ���Ŵ���ģʽ�л�
            if (currentMode != ControlMode.Manual || gestureCommand == 2)
            {
                switch (gestureCommand)
                {
                    
                    case 1:
                        // ����ģʽ
                        if (currentMode != ControlMode.Manual) // ���˿�ģʽʱ���л�
                        {
                            currentMode = ControlMode.Autonomous;
                            UpdateStatusText("����ģʽ");
                        }
                        break;
                    case 2:
                        // �ص�Idleģʽ
                        currentMode = ControlMode.Idle;
                        UpdateStatusText("����״̬");
                        break;
                    case 3:
                        // ����ģʽ
                        if (currentMode != ControlMode.Manual) // ���˿�ģʽʱ���л�
                        {
                            currentMode = ControlMode.Follow;
                            UpdateStatusText("����ģʽ");
                        }
                        break;
                    case 4:
                        // �˿�ģʽ
                        if (currentMode == ControlMode.Idle) // ����Idleģʽʱ���л�
                        {
                            currentMode = ControlMode.Manual;
                            UpdateStatusText("�˿�ģʽ");
                        }
                        break;
                }
            }

            // �����˿�ģʽ��ʹ��ax��ay�����������ƻ�����
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
            coinCount++; // ���ӽ������
            UpdateCoinText(); // ����UI�ı�
            Destroy(other.gameObject); // ���ٽ�Ҷ���
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