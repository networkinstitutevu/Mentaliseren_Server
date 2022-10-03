using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;

public class Main : NetworkBehaviour
{
    void Start()
    {
        print("Starting server");
        NetworkManager.Singleton.StartHost();
        if (IsServer) { print("SERVER"); }
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            PongClientRpc(Time.frameCount, "Hello world");
        }
    }

    [ClientRpc]
    void PongClientRpc(int somenumber, string sometext)
    {
        print("sending..." + somenumber + "-" + sometext);
    }

    [ServerRpc]
    void MessageServerRpc(string theMsg)
    {
        print("Received: " + theMsg);
    }
    [ClientRpc]
    void BackClientRpc(int somenumber, string sometext)
    {
        print("received a call");
    }

}
