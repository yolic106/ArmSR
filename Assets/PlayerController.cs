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

    // ���������߳���ִ������Ķ���
    private ConcurrentQueue<System.Action> tasks = new ConcurrentQueue<System.Action>();


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // ֻ�������ƿ���ģʽ�²ų�ʼ���������
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
        // ִ�����߳�����
        while (tasks.TryDequeue(out var task))
        {
            task();
        }

        if (currentMode != ControlMode.Manual || !isGestureControl)
        {
            // ���̿���
            h = Input.GetAxis("Horizontal");
            v = Input.GetAxis("Vertical");

            // ����ģʽ�л�
            if (Input.GetKeyDown(KeyCode.Alpha1)) ProcessCommand("1");
            if (Input.GetKeyDown(KeyCode.Alpha2)) ProcessCommand("2");
            if (Input.GetKeyDown(KeyCode.Alpha3)) ProcessCommand("3");
            if (Input.GetKeyDown(KeyCode.Alpha4)) ProcessCommand("4");
        }
    }

    void FixedUpdate()
    {
        Vector3 force = new Vector3(h, 0, v);
        force *= speed;
        rb.AddForce(force);
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
                    case 0:
                        // ���ض�������Ը�����Ҫ����
                        break;
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
                        UpdateStatusText("Idle");
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
            if (currentMode == ControlMode.Manual && isGestureControl)
            {
                h = float.Parse(parts[1]);
                v = float.Parse(parts[2]);
            }
        }
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