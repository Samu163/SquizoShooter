using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class TestSockets : MonoBehaviour
{
    private Thread client;
    private Thread server;

    private Socket clientSocket;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        client = new Thread(ClientProcess);
        server = new Thread(ServerProcess);
        server.Start();

    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void ServerProcess()
    {
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Loopback, 9050);

        s.Bind(ipep);

        s.Listen(10);
     
        client.Start();


        //Accept from the server process
        //Capture the return
        clientSocket = s.Accept();

        //Log return
        Debug.Log("Connection accepted from " + clientSocket.RemoteEndPoint.ToString());


        //ASCII has only 256 bits only for english caharters, UTF8 has more for other languages
        var data = new byte[4096];
        data = System.Text.Encoding.UTF8.GetBytes("Welcome to my test server");
        clientSocket.Send(data);
        int dataLength = data.Length;
        Debug.Log(System.Text.Encoding.UTF8.GetString(data,0,dataLength));;

    }

    public void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);
        clientSocket.Connect(serverEndPoint);
        var newData = new byte[4096];
        int attempts = 0;
        while (true)
        {
            if (attempts++ > 10) break;
            int dataLength = clientSocket.Receive(newData);

            if (dataLength == 0) break;

            if (dataLength > 0)
            {
                string message = System.Text.Encoding.UTF8.GetString(newData, 0, dataLength);

            }

            Thread.Sleep(10);

            Debug.Log(System.Text.Encoding.UTF8.GetString(newData, 0, dataLength));
        }

        clientSocket.Send(System.Text.Encoding.UTF8.GetBytes("Hello from the client"));
        //Wait until the server process is done


    }

    public void RecieveAllMesages()
    {
        var newData = new byte[1024];
        int attempts = 0;
        while (true)
        {
            if(attempts++ > 10) break;
            int dataLength = clientSocket.Receive(newData);
            if(dataLength == 0) break;
            Debug.Log(System.Text.Encoding.UTF8.GetString(newData, 0, dataLength));
        }
    }
}
