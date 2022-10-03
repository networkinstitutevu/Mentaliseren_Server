using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Unity.Networking.Transport;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class Server : MonoBehaviour
{
    [SerializeField] GameObject btnStart, pnlInfo, pnlReactions, btnQuit, connectDot;
    [SerializeField] GameObject txtScenario, txtStep, txtVoiceOver, txtLars, txtNetworkMessage;
    [SerializeField] GameObject errorPanel, txtError, pnlPause;
    public NetworkDriver m_Driver; //Main network driver

    StreamWriter logFile;
    private string timePattern = @"H-mm-ss";
    string scenario, stap, decision, nextStep;

    int serverNumber = 1; //Overwritten bij the settings file

    float pollingInterval = 2.0f; //Seconds between polling the web server's client file
    DateTime pollingPauseStart;
    string scenarios = ""; //Holds the scenario's to play
    bool waitingForServer = false; //If <true> we're waiting for a web server response after a request
    string serverReply = "";

    bool die = false; //Used to catch a fatal error only once
    private void Awake()
    {
    }
    void Start()
    {
        GetSettings();
        if (!Common.Abort)
        {
            if (!Directory.Exists(".\\LogsFiles\\"))
            {
                Directory.CreateDirectory(".\\LogsFiles\\");
            }
            string append = Common.Ppn + "_" + DateTime.Now.ToString(timePattern) + ".txt"; //Create last part of file name with timestamp
            logFile = new StreamWriter(".\\LogsFiles\\Decisions_" + append); //Create file

            print("Writing: CMD,INIT," + Common.PollingInterval + "," + Common.ButtonTimeOut + "," + Common.QuestionTimeOut + "," + scenarios);
            PrepServerResponse();
            StartCoroutine(SendToServer(serverNumber + ",CMD,INIT," + Common.PollingInterval + "," + Common.ButtonTimeOut + "," + Common.QuestionTimeOut + "," + scenarios)); //Tell web server this server has started.
                                                                                                                                               //<room#>,<command>,<content>
        }
        errorPanel.SetActive(false);
        pnlPause.SetActive(false);
        btnStart.SetActive(false); //Disable start button
        btnQuit.SetActive(false);
        pnlInfo.SetActive(false); //Disable info panel
        pnlReactions.SetActive(false); //Disable reactions panel
    }
    private void Update()
    {
        if(Common.Abort && !die)
        {
            die = true;
            AbortApp();
        }
        if (!Common.Abort)
        {
            if (waitingForServer && serverReply != "")
            {
                waitingForServer = false;
                ProcessNetworkMessage(serverReply);
                serverReply = "";
            }
            if (!waitingForServer && ((DateTime.Now - pollingPauseStart).TotalSeconds > pollingInterval))
            {
                PrepServerResponse();
                StartCoroutine(SendToServer(serverNumber + ",CMD,POLL"));
            }
        }
    }
    void PrepServerResponse()
    {
        waitingForServer = true; //Start waiting for web server response
        serverReply = "";
    }
    void ProcessNetworkMessage(string msg)
    {
        //These are the possible replies from the server.php script, called from this server.cs script
        //After server send:
        //INIT,OK/ERROR //web server stores: this server is ready
        //START,OK/ERROR //web server stores: START button was pressed 
        //POS/NEG/GEN,OK/ERROR //web server stores: button pressed
        //Replies after server POLL command
        //POLL,CMD,READY //client signals it's ready to start
        //POLL,CMD,RPS //client signals waiting for button press on server
        //POLL,CMD,NEW //client signals start of new scenario
        //POLL,CMD,END //client signals end of program
        //POLL,CMD,PAUSE //client signals start of pause after initial intro
        //POLL,ERROR/EMPTY //result of reading the client file's first line on the web server, EMPTY or ERROR
        //POLL,TXT,VO,<text> //result of reading the client file's first line on the web server, VoiceOver
        //POLL,TXT,SC,[int],<theme text> //result of reading the client file's first line on the web server, scenario number and theme description
        //POLL,TXT,STP,[int] //result of reading the client file's first line on the web server, step number
        //POLL,TXT,LARS,<text> //result of reading the client file's first line on the web server, text spoken by Lars

        //[0] = CMD, [1] = INIT, POLL, START, NEG, POS, GEN [2] = init/start/neg/pos/gen OK or ERROR, poll RESULT
        //POLL: either EMPTY or ERROR or the client CMD/TXT
        string[] elements = msg.Split(','); //Split into <command> and <value>. [2] to [4] elements
        switch(elements[1])
        {
            case "ERROR": //Server.php reports a default error - often wrongly formatted command to the PHP script
                print("Webserver error");
                pnlInfo.SetActive(false); //Hide info panel
                txtNetworkMessage.GetComponent<TMP_Text>().text = "Fout bij de webserver. Einde sessie.";
                txtNetworkMessage.SetActive(true);//Show end text
                btnQuit.SetActive(true); //Activate Quit button
                break;
            case "INIT": //Server reported that it's ready for client
                if(elements[1] == "ERROR")
                {
                    print("ERROR at INIT");
                    AbortProgram();
                }
                else
                {
                    print("Server ready. Waiting for client INIT");
                    pollingPauseStart = DateTime.Now; //Reset poll timer
                }
                break;
            case "POLL":
                if (elements[2] == "ERROR")
                {
                    print("ERROR at POLL");
                    AbortProgram();
                    break;
                }
                if(elements[2] == "EMPTY") //No reaction from client, do nothing
                {
                    //print("Poll returned EMPTY");
                    pollingPauseStart = DateTime.Now; //Reset poll timer
                    break;
                }
                switch(elements[2])
                {
                    case "CMD":
                        switch (elements[3])
                        {
                            case "ABORT":
                                print("Client error");
                                pnlInfo.SetActive(false); //Hide info panel
                                txtNetworkMessage.GetComponent<TMP_Text>().text = "Fout bij de applicatie in de Quest. Einde sessie.";
                                txtNetworkMessage.SetActive(true);//Show end text
                                btnQuit.SetActive(true); //Activate Quit button
                                break;
                            case "READY":
                                print("POLL returned READY");
                                connectDot.GetComponent<Image>().color = Color.green;
                                txtNetworkMessage.GetComponent<TMP_Text>().text = "Gebruiker verbonden. Klik <Start> om te beginnen.";
                                btnStart.SetActive(true);
                                break;
                            case "PAUSE":
                                print("POLL returned PAUSE");
                                pnlPause.SetActive(true); //enable pause panel with button
                                //then wait for button
                                break;
                            case "RPS": //Client asks server for response (button)
                                print("POLL returned RESPONSE");
                                pnlReactions.SetActive(true); //Show button panel
                                break;
                            case "NEW": //Client signals a new scenario
                                print("POLL returned NEW");
                                txtScenario.GetComponent<TMP_Text>().text = "Scenario: -"; //Set the text on-screen
                                txtStep.GetComponent<TMP_Text>().text = "Stap: -";
                                txtLars.GetComponent<TMP_Text>().text = string.Empty;
                                break;
                            case "END":
                                print("POLL returned END");
                                pnlInfo.SetActive(false); //Hide info panel
                                txtNetworkMessage.GetComponent<TMP_Text>().text = "Einde sessie";
                                txtNetworkMessage.SetActive(true);//Show end text
                                btnQuit.SetActive(true); //Activate Quit button
                                PrepServerResponse();
                                StartCoroutine(SendToServer(serverNumber + ",CMD,QUIT")); //Removes files
                                break;
                        }
                        break;
                    case "TXT":
                        switch(elements[3])
                        {
                            case "VO": //Client tells voice over content
                                txtVoiceOver.GetComponent<TMP_Text>().text = "VoiceOver: " + elements[4];
                                break;
                            case "SC": //Client tells scenario number and text
                                txtScenario.GetComponent<TMP_Text>().text = "Scenario: " + elements[4]; //Set the text on-screen
                                scenario = elements[4];
                                break;
                            case "STP": //Client tells current step in scenario
                                txtStep.GetComponent<TMP_Text>().text = "Stap: " + elements[4];
                                stap = elements[4];
                                break;
                            case "LARS": //Client tells spoken text by Lars
                                txtLars.GetComponent<TMP_Text>().text = elements[4];
                                break;
                        }
                        break;
                }
                break;
            case "START":
                if (elements[1] == "ERROR")
                {
                    print("ERROR at START");
                    AbortProgram();
                }
                else
                {
                    print("Server told client to start program.");
                    pollingPauseStart = DateTime.Now; //Reset poll timer
                }
                break;
            case "POS":
                if (elements[1] == "ERROR")
                {
                    print("ERROR at sending POS");
                    AbortProgram();
                }
                else
                {
                    print("Server told client: WEL mentaliseren.");
                    pollingPauseStart = DateTime.Now; //Reset poll timer
                }
                break;
            case "NEG":
                if (elements[1] == "ERROR")
                {
                    print("ERROR at sending NEG");
                    AbortProgram();
                }
                else
                {
                    print("Server told client: NIET mentaliseren.");
                    pollingPauseStart = DateTime.Now; //Reset poll timer
                }
                break;
            case "GEN":
                if (elements[1] == "ERROR")
                {
                    print("ERROR at sending GEN");
                    AbortProgram();
                }
                else
                {
                    print("Server told client: I do not understand version " + elements[2]);
                    pollingPauseStart = DateTime.Now; //Reset poll timer
                }
                break;
            default:
                print("Switch defaulted");
                break;
        }
    }
    public void ProcessButtons(GameObject theButton)
    {
        bool logthis = true;
        PrepServerResponse();
        switch(theButton.name)
        {
            case "btnStart": //Start button is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,START"));
                theButton.SetActive(false); //Remove start button
                txtNetworkMessage.SetActive(false); //Remove "Press <start>" text
                pnlInfo.SetActive(true); //Show Info panel
                //txtVoiceOver.GetComponent<TMP_Text>().text = "VoiceOver: Introtekst speelt.";
                logthis = false;
                //checkClientConnection = true;
                //lastAliveReceived = DateTime.Now;
                break;
            case "btnEndPause":
                pnlPause.SetActive(false); //remove end pause panel
                StartCoroutine(SendToServer(serverNumber + ",CMD,ENDPAUSE"));
                break;
            case "btnPositive": //Positive (wel mentaliseren) button is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,POS"));
                pnlReactions.SetActive(false);
                decision = "wel";
                WriteLogLine();
                break;
            case "btnNegative": //Negative (niet mentaliseren) button is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,NEG"));
                pnlReactions.SetActive(false);
                decision = "niet";
                WriteLogLine();
                break;
            case "btnGeneric1": //Generic button 1 (ik begrijp je niet) is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,GEN,1"));
                pnlReactions.SetActive(false);
                decision = "Alg1";
                WriteLogLine();
                break;
            case "btnGeneric2": //Generic button 2 (ik weet het niet hoor) is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,GEN,2"));
                pnlReactions.SetActive(false);
                decision = "Alg2";
                WriteLogLine();
                break;
            case "btnGeneric3": //Generic button 3 (ik volg je niet helemaal) is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,GEN,3"));
                pnlReactions.SetActive(false);
                decision = "Alg3";
                WriteLogLine();
                break;
            case "btnGeneric4": //Generic button 4 (ik weet niet wat je bedoelt) is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,GEN,4"));
                pnlReactions.SetActive(false);
                decision = "Alg4";
                WriteLogLine();
                break;
            case "btnGeneric5": //Generic button 5 (ik snap er niets meer van) is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,GEN,5"));
                pnlReactions.SetActive(false);
                decision = "Alg5";
                WriteLogLine();
                break;
            case "btnGeneric6": //Generic button 6 (ik weet niet wat ik hierop moet zeggen) is pressed
                StartCoroutine(SendToServer(serverNumber + ",CMD,GEN,6"));
                pnlReactions.SetActive(false);
                decision = "Alg6";
                WriteLogLine();
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
            case "btnStop": //On error
                logthis = false;
#if UNITY_EDITOR
                // Application.Quit() does not work in the editor so
                // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
            case "btnAbort":
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
        //if (logthis) { WriteLogLine(); }
    }
    void WriteLogLine()
    {
        logFile.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "," + Common.Ppn + "," + scenario + "," + stap + "," + decision);
    }
    IEnumerator SendToServer(string theCommand)
    {
        string dataPush = "https://www.techlabs.nl/experiments/mentaliserenv2/server.php?cmd=" + theCommand;
        UnityWebRequest www = UnityWebRequest.Get(dataPush);// 
        www.timeout = 30;
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log(www.error);

            }
            else
            {
                //print("Server raw: " + www.downloadHandler.text);

                serverReply = www.downloadHandler.text;
            }
        }
        else
        {
            print("Netwrok error - timeout");
            pnlInfo.SetActive(false); //Hide info panel
            txtNetworkMessage.GetComponent<TMP_Text>().text = "Time out in netwerkverbinding. Einde sessie.";
            txtNetworkMessage.SetActive(true);//Show end text
            btnQuit.SetActive(true); //Activate Quit button
        }
    }
    void AbortApp()
    {
        txtError.GetComponent<TMP_Text>().text = Common.AbortMSG; //Display why there's an error
        errorPanel.SetActive(true); //Show the Abort panel with messsage and STOP button
    }
    void GetSettings()
    {
        //Read previous PPN number. Increase by one. Write out again.
        StreamReader ppnFile = new StreamReader("Settings/ppn.txt");
        string line = ppnFile.ReadLine();
        ppnFile.Close();
        Common.Ppn = Convert.ToInt32(line);
        Common.Ppn++;
        StreamWriter ppnOutFile = new StreamWriter("Settings/ppn.txt");
        ppnOutFile.WriteLine(Common.Ppn);
        ppnOutFile.Close();
        //Read server number for network communication
        StreamReader serverFile = new StreamReader("Settings/settings.txt");
        try
        {
            line = serverFile.ReadLine();
            string[] elements = line.Split(':');
            serverNumber = Convert.ToInt32(elements[1]); //The room number this server will use to connect to the corresponding Quest
        }
        catch
        {
            Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <servernummer>. Corrigeer en herstart.";
            Common.Abort = true;
            serverFile.Close();
            return;
        }
        try
        {
            line = serverFile.ReadLine();
            string[] elements = line.Split(':');
            Common.PollingInterval = (float)Convert.ToDouble(elements[1]); //Number of seconds between WWW polls of the server and client when waiting
        }
        catch
        {
            Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <polling interval>. Corrigeer en herstart.";
            Common.Abort = true;
            serverFile.Close();
            return;
        }
        try
        {
            line = serverFile.ReadLine();
            string[] elements = line.Split(':');
            Common.ButtonTimeOut = Convert.ToInt32(elements[1]); //Number of seconds waiting for a server button press before a Time Out occurs (=abort)
        }
        catch
        {
            Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <button timeout>. Corrigeer en herstart.";
            Common.Abort = true;
            serverFile.Close();
            return;
        }
        try
        {
            line = serverFile.ReadLine();
            string[] elements = line.Split(':');
            Common.QuestionTimeOut = (float)Convert.ToDouble(elements[1]); //Number of seconds waiting before the question mark is shown above the avatar
        }
        catch
        {
            Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <vraagteken timeout>. Corrigeer en herstart.";
            Common.Abort = true;
            serverFile.Close();
            return;
        }
        try
        {
            line = serverFile.ReadLine();
            string[] elements  = line.Split(':');
            scenarios = elements[1]; //String of scenario numbers. All <int> separated by '-'
            if(scenarios.Length == 0)
            {
                Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <scenarios>. Corrigeer en herstart.";
                Common.Abort = true;
                serverFile.Close();
            }
            else
            {
                string[] scenarioNumbers = scenarios.Split('-');
                if(scenarioNumbers.Length == 0)
                {
                    Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <scenarios>. Corrigeer en herstart.";
                    Common.Abort = true;
                    serverFile.Close();
                }
                else
                {
                    foreach(string scNumber in scenarioNumbers)
                    {
                        //print("SC " + scNumber);
                        if(!int.TryParse(scNumber, out int val))
                        {
                            Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <scenarios>. Corrigeer en herstart.";
                            Common.Abort = true;
                            serverFile.Close();
                            break;
                        }
                    }
                }
            }
            return;
        }
        catch
        {
            Common.AbortMSG = "Fout bij het lezen van de settings. Probleem met <scenarios>. Corrigeer en herstart.";
            Common.Abort = true;
            serverFile.Close();
        }
        serverFile.Close();
    }
    void AbortProgram()
    {
#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void OnApplicationQuit()
    {
        if (logFile != null)
        {
            logFile.Close();
        }
    }
}
