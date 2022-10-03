using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using UnityEngine.Networking;
using Unity.Networking.Transport;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class Server : MonoBehaviour
{
    [SerializeField] GameObject btnStart, pnlInfo, pnlReactions, btnQuit, connectDot;
    [SerializeField] GameObject txtScenario, txtStep, txtVoiceOver, txtLars, txtNetworkMessage;
    public NetworkDriver m_Driver; //Main network driver
    private NativeList<NetworkConnection> m_Connections; //List of connections to server
    bool clientConnected = false;
    int step = 10;
    bool doNextStep = false;

    Keyboard kb;
    DateTime lastSent; //Last time network message was sent
    float networkPulseTimer = 3.0f; //Time between <stay alive> network packages
    DateTime lastAliveReceived; //remember last pin from client
    bool checkClientConnection = false; //Flag to avoid checking the Alive Pulse too soon
    int ppn = 0;
    StreamWriter logFile;
    private string timePattern = @"H-mm-ss";
    string scenario, stap, decision, nextStep;

    DateTime lastPress;
    float keypressDelay = 0.5f;
    string serverIP = string.Empty;
    ushort thePort = 9000; //Have to read this from settings

    private void Awake()
    {
        GetSettings();
        if (!Directory.Exists(".\\LogsFiles\\"))
        {
            Directory.CreateDirectory(".\\LogsFiles\\");
        }
        string append = Common.Ppn + "_" + DateTime.Now.ToString(timePattern) + ".txt"; //Create last part of file name with timestamp
        logFile = new StreamWriter(".\\LogsFiles\\Decisions_" + append); //Create file

        StartCoroutine(SendToServer());
    }
    void Start()
    {
        kb = InputSystem.GetDevice<Keyboard>();

        m_Driver = NetworkDriver.Create();// new NetworkConfigParameter { disconnectTimeoutMS = 300000 }); //Start the networking
        var endpoint = NetworkEndPoint.AnyIpv4; //Accept any connection running over IPv4
        endpoint.Port = thePort; //Set connection port to 9000
        if (m_Driver.Bind(endpoint) != 0) //If the network cannot setup the server on port 9000, report failure
            Debug.Log("Failed to bind to port 9000");
        else
            m_Driver.Listen(); //Accept clients

        m_Connections = new NativeList<NetworkConnection>(1, Allocator.Persistent); //Allow for 16 clients who's connections are persistent

        btnStart.SetActive(false); //Disable start button
        btnQuit.SetActive(false);
        pnlInfo.SetActive(false); //Disable info panel
        pnlReactions.SetActive(false); //Disable reactions panel
    }

    public void OnDestroy() //Needed at the end
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }
    private void Update()
    {
        ListenForNetworkData();
        if(clientConnected) //True is a network connection is established
        {
/*            if (checkClientConnection && (DateTime.Now - lastAliveReceived).TotalSeconds > (networkPulseTimer * 3f)) //If it has been more than 3x the Alive Pulse timer, then assume connection lost. Normal time-out is too long!
            {
                Debug.Log("Client disconnected from server after 3 times the Alive Pulse timer");
                m_Connections[0] = default(NetworkConnection);
                clientConnected = false;
                connectDot.GetComponent<Image>().color = Color.red;
                pnlInfo.SetActive(false); //Hide Info panel
                pnlReactions.SetActive(false);
                txtNetworkMessage.GetComponent<TMP_Text>().text = "Verbinding verbroken! Sluit de applicatie en begin opnieuw.";
                txtNetworkMessage.SetActive(true);
                btnQuit.SetActive(true);
            }
*/
/*            switch (step)
            {
                case 10:
                    break;
                default:
                    break;
            }
*/        
        }
        //DEBUG
/*        if ((DateTime.Now - lastPress).TotalSeconds > keypressDelay)
        {
            if (kb.pKey.wasPressedThisFrame) //Positive
            {
                print("Sending: positive");
                SendNetworkData("positive");
                lastPress = DateTime.Now;
            }
            if (kb.nKey.wasPressedThisFrame) //Negative
            {
                print("Sending: negative");
                SendNetworkData("negative");
                lastPress = DateTime.Now;
            }
            if (kb.aKey.wasPressedThisFrame) //Generic
            {
                print("Sending: generic");
                SendNetworkData("generic");
                lastPress = DateTime.Now;
            }
        }*/
    }
    void ListenForNetworkData()
    {
        SetNetworkForData();
        //Process data
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++) //Run through all connections
        {
            Assert.IsTrue(m_Connections[i].IsCreated); //Is this connection active? Else next loop
            NetworkEvent.Type cmd; //New network type
            while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) != NetworkEvent.Type.Empty) //If there's something from this connection, push Type into <cmd> and data into <stream>
            {
                if (cmd == NetworkEvent.Type.Data) //Is it a Received Data event?
                {
                    var i_string = stream.ReadFixedString512();
                    Debug.Log("Got from the Client: " + i_string);
                    ProcessNetworkMessage(i_string.ToString());
                }
                else if (cmd == NetworkEvent.Type.Disconnect) //Is it a Disconnect event?
                {
                    Debug.Log("Client disconnected from server");
                    m_Connections[i] = default(NetworkConnection);
                    clientConnected = false;
                    connectDot.GetComponent<Image>().color = Color.red;
                    pnlInfo.SetActive(false); //Hide Info panel
                    pnlReactions.SetActive(false);
                    txtNetworkMessage.GetComponent<TMP_Text>().text = "Verbinding verbroken! Sluit de applicatie en begin opnieuw.";
                    txtNetworkMessage.SetActive(true);
                    btnQuit.SetActive(true);
                }
            }
            if((DateTime.Now - lastSent).TotalSeconds > networkPulseTimer) //Keep connection alive
            {
                SendNetworkData("alive");
            }
        }
    }
    void ProcessNetworkMessage(string msg)
    {
        string[] elements = msg.Split(','); //Split into <command> and <value>
        switch(elements[0])
        {
            //case "intro": //Client signaled Intro VoiceOver is done, show START button
                //txtNetworkMessage.GetComponent<TMP_Text>().text = "Intro klaar. Klik <Start> om te beginnen.";
                //btnStart.SetActive(true);
                //break;
            case "scenario": //Client sending new scenario number
                txtScenario.GetComponent<TMP_Text>().text = "Scenario: " + elements[1]; //Set the text on-screen
                scenario = elements[1];
                break;
            case "voiceover": //Status of voice over
                txtVoiceOver.GetComponent<TMP_Text>().text = "VoiceOver: " + elements[1];
                break;
            case "step": //Current step in scenario
                txtStep.GetComponent<TMP_Text>().text = "Stap: " + elements[1];
                stap = elements[1];
                break;
            case "lars": //Show Lars' text
                txtLars.GetComponent<TMP_Text>().text = elements[1];
                break;
            case "reaction": //Toggles reaction panel
                if(elements[1] == "true") //Show panel
                {
                    pnlReactions.SetActive(true);
                }
                else //Hide panel
                {
                    pnlReactions.SetActive(false);
                }
                break;
            case "end": //End of scenario of end of application
                if(elements[1] == "new") //New scenario
                {
                    txtScenario.GetComponent<TMP_Text>().text = "Scenario: -"; //Set the text on-screen
                    txtStep.GetComponent<TMP_Text>().text = "Stap: -";
                    txtLars.GetComponent<TMP_Text>().text = string.Empty;
                }
                else //elements[1] == "end"
                {
                    pnlInfo.SetActive(false); //Hide info panel
                    txtNetworkMessage.GetComponent<TMP_Text>().text = "Einde sessie";
                    txtNetworkMessage.SetActive(true);//Show end text
                }
                break;
            case "alive":
                lastAliveReceived = DateTime.Now; //reset timer
                break;
            default:
                break;
        }
    }
    void SendNetworkData(string message)
    {
        SetNetworkForData();
        //Process data
        for (int i = 0; i < m_Connections.Length; i++) //Run through all connections
        {
            Assert.IsTrue(m_Connections[i].IsCreated); //Is this connection active? Else next loop
            DataStreamWriter writer; //Create an outgoing data stream
            m_Driver.BeginSend(NetworkPipeline.Null, m_Connections[i], out writer);//Start sending using the default pipeline, this connection and the <writer> stream
            bool result = writer.WriteFixedString512(message); //Write the message
            m_Driver.EndSend(writer); //Close the stream
            lastSent = DateTime.Now; //Remember the sent time
            print("Sent " + message + ". Was " + result);
        }
    }
    void SetNetworkForData()
    {
        m_Driver.ScheduleUpdate().Complete(); //Signal communication is done this update so data is available
        for (int i = 0; i < m_Connections.Length; i++) // CleanUpConnections
        {
            if (!m_Connections[i].IsCreated)
            {
                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }
        // AcceptNewConnections
        NetworkConnection c;
        while ((c = m_Driver.Accept()) != default(NetworkConnection)) //If the Connection <c> is not default/empty
        {
            m_Connections.Add(c); //Add it to the list
            Debug.Log("Accepted a connection");
            clientConnected = true;
            lastSent = DateTime.Now; //Remember last network activity

            connectDot.GetComponent<Image>().color = Color.green;
            txtNetworkMessage.GetComponent<TMP_Text>().text = "Gebruiker verbonden. Klik <Start> om te beginnen.";
            btnStart.SetActive(true);

            lastAliveReceived = DateTime.Now;
        }
    }
    public void ProcessButtons(GameObject theButton)
    {
        bool logthis = true;
        switch(theButton.name)
        {
            case "btnStart": //Start button is pressed
                SendNetworkData("start"); //Let client know to start application
                theButton.SetActive(false); //Remove start button
                txtNetworkMessage.SetActive(false); //Remove "Press <start>" text
                pnlInfo.SetActive(true); //Show Info panel
                txtVoiceOver.GetComponent<TMP_Text>().text = "VoiceOver: Introtekst speelt.";
                //doNextStep = true;
                logthis = false;
                checkClientConnection = true;
                lastAliveReceived = DateTime.Now;
                break;
            case "btnPositive": //Positive (wel mentaliseren) button is pressed
                SendNetworkData("positive");
                pnlReactions.SetActive(false);
                decision = "wel";
                break;
            case "btnNegative": //Negative (niet mentaliseren) button is pressed
                SendNetworkData("negative");
                pnlReactions.SetActive(false);
                decision = "niet";
                break;
            case "btnGeneric1": //Generic button 1 (ik begrijp je niet) is pressed
                SendNetworkData("generic1");
                pnlReactions.SetActive(false);
                decision = "Alg1";
                break;
            case "btnGeneric2": //Generic button 2 (ik weet het niet hoor) is pressed
                SendNetworkData("generic2");
                pnlReactions.SetActive(false);
                decision = "Alg2";
                break;
            case "btnGeneric3": //Generic button 3 (ik volg je niet helemaal) is pressed
                SendNetworkData("generic3");
                pnlReactions.SetActive(false);
                decision = "Alg3";
                break;
            case "btnGeneric4": //Generic button 4 (ik weet niet wat je bedoelt) is pressed
                SendNetworkData("generic4");
                pnlReactions.SetActive(false);
                decision = "Alg4";
                break;
            case "btnGeneric5": //Generic button 5 (ik snap er niets meer van) is pressed
                SendNetworkData("generic5");
                pnlReactions.SetActive(false);
                decision = "Alg5";
                break;
            case "btnGeneric6": //Generic button 6 (ik weet niet wat ik hierop moet zeggen) is pressed
                SendNetworkData("generic6");
                pnlReactions.SetActive(false);
                decision = "Alg6";
                break;
            case "btnQuit":
                print("QUIT");
                logthis = false;
#if UNITY_EDITOR
                // Application.Quit() does not work in the editor so
                // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
            default:
                break;
        }
        if (logthis) { WriteLogLine(); }
    }
    void WriteLogLine()
    {
        logFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "," + ppn + "," + scenario + "," + stap + "," + decision);
    }
    IEnumerator SendToServer()
    {
        string thisIP = GetLocalIPAddress();
        string dataPush = "https://www.techlabs.nl/experiments/mentaliserenv2/ipserver.php?data=" + thisIP + "," + thePort;
        UnityWebRequest www = UnityWebRequest.Get(dataPush);// 
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log("Form upload complete!");
        }
    }
    IEnumerator SendToServer(string theCommand)
    {
        string dataPush = "https://www.techlabs.nl/experiments/mentaliserenv2/ipserver.php?data=" + theCommand;
        UnityWebRequest www = UnityWebRequest.Get(dataPush);// 
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log(www.downloadHandler.text);
        }
    }
    string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                print("Local IP: " + ip);
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
    void GetSettings()
    {
        //Read previous PPN number. Increase by one. Write out again.
        StreamReader ppnFile = new StreamReader("Settings/ppn.txt");
        string line = ppnFile.ReadLine();
        ppnFile.Close();
        ppn = Convert.ToInt32(line);
        ppn++;
        StreamWriter ppnOutFile = new StreamWriter("Settings/ppn.txt");
        ppnOutFile.WriteLine(ppn);
        ppnOutFile.Close();
        //Read port number for network communication
        StreamReader portFile = new StreamReader("Settings/port.txt");
        line = portFile.ReadLine();
        portFile.Close();
        thePort = (ushort)Convert.ToInt32(line);
    }
    private void OnApplicationQuit()
    {
        logFile.Close();
    }
}
