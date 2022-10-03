using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Common
{
    static int ppn = 0; //Participant number from settings.txt <MainFlow>
    static string scenario = ""; //Participant number from settings.txt <MainFlow>
    static string node = ""; //Participant number from settings.txt <MainFlow>
    static string filename = "";
    static string decision = "";
    static string currentFe = "";
    static GameObject table;
    static float pollingInterval = 2.0f;
    static int buttonTimeOut = 60;
    static bool abort = false;
    static string abortMSG = "";
    static float questionTimeOut = 3.0f;
    static bool pause = false;

    public static int Ppn { get => ppn; set => ppn = value; }
    public static string Scenario { get => scenario; set => scenario = value; }
    public static string Node { get => node; set => node = value; }
    public static string Filename { get => filename; set => filename = value; }
    public static string Decision { get => decision; set => decision = value; }
    public static string CurrentFe { get => currentFe; set => currentFe = value; }
    public static GameObject Table { get => table; set => table = value; }
    public static float PollingInterval { get => pollingInterval; set => pollingInterval = value; }
    public static int ButtonTimeOut { get => buttonTimeOut; set => buttonTimeOut = value; }
    public static bool Abort { get => abort; set => abort = value; }
    public static string AbortMSG { get => abortMSG; set => abortMSG = value; }
    public static float QuestionTimeOut { get => questionTimeOut; set => questionTimeOut = value; }
    public static bool Pause { get => pause; set => pause = value; }
}
